using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public class WorldObject_PlayerOutpost : WorldObject
    {
        public int colonyId;

        public PlayerColony Colony => GameComponent_PlayerColonies.Instance?.colonies.FirstOrDefault(c => c.id == colonyId);

        public override string Label => Colony?.name ?? base.Label;

        public override bool HasName => true;

        protected override void Tick()
        {
            base.Tick();
            if (this.IsHashIntervalTick(2000) && Colony == null) Destroy();
        }

        public override string GetInspectString()
        {
            PlayerColony c = Colony;
            if (c == null) return base.GetInspectString();
            return "Rimverse_OutpostInspect".Translate(c.TierLabel, c.population.ToString("0"), c.stability.ToString("0"), c.lastIncome.ToString("0"), ("Rimverse_Tithe_" + c.tithe).Translate());
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo g in base.GetGizmos()) yield return g;
            PlayerColony c = Colony;
            if (c == null) yield break;
            yield return new Command_Action
            {
                defaultLabel = "Rimverse_OpenEmpire".Translate(),
                defaultDesc = "Rimverse_OpenEmpireDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/Rimverse_Empire", false) ?? BaseContent.BadTex,
                action = () => Find.WindowStack.Add(new Dialog_ColonyManage(c))
            };
        }

        public override IEnumerable<Gizmo> GetCaravanGizmos(Caravan caravan)
        {
            foreach (Gizmo g in base.GetCaravanGizmos(caravan)) yield return g;
            PlayerColony c = Colony;
            if (c == null) yield break;
            Command_Action collect = new Command_Action
            {
                defaultLabel = "Rimverse_OutpostCollect".Translate(),
                defaultDesc = "Rimverse_OutpostCollectDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/LoadTransporter", false) ?? BaseContent.BadTex,
                action = () =>
                {
                    foreach (KeyValuePair<string, int> kv in c.stock.ToList())
                    {
                        ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(kv.Key);
                        if (def == null) continue;
                        List<Thing> things = new List<Thing>();
                        ColonyUtility.AddStacks(things, def, kv.Value);
                        foreach (Thing t in things) CaravanInventoryUtility.GiveThing(caravan, t);
                    }
                    c.stock.Clear();
                }
            };
            if (c.stock.Count == 0) collect.Disable("Rimverse_OutpostEmpty".Translate());
            yield return collect;
            yield return new Command_Action
            {
                defaultLabel = "Rimverse_OutpostAbandon".Translate(),
                defaultDesc = "Rimverse_OutpostAbandonDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/AbandonHome", false) ?? BaseContent.BadTex,
                action = () => Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("Rimverse_OutpostAbandonConfirm".Translate(c.name), () =>
                {
                    foreach (Pawn p in c.settlers.Where(p => p != null && !p.Dead).ToList())
                    {
                        if (Find.WorldPawns.Contains(p)) Find.WorldPawns.RemovePawn(p);
                        caravan.AddPawn(p, true);
                    }
                    GameComponent_PlayerColonies.Instance.colonies.Remove(c);
                    Destroy();
                }, true))
            };
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref colonyId, "colonyId");
        }
    }

    public static class OutpostUtility
    {
        public const int SteelCost = 150;

        public static AcceptanceReport CanFound(Caravan caravan)
        {
            if (caravan == null || caravan.Faction != Faction.OfPlayer) return false;
            ResearchProjectDef research = DefDatabase<ResearchProjectDef>.GetNamedSilentFail("Rimverse_ColonyAdministration");
            if (research != null && !research.IsFinished) return "Rimverse_ColonyNeedResearch".Translate(research.LabelCap);
            if (Find.WorldObjects.ObjectsAt(caravan.Tile).Any(o => !(o is Caravan))) return "Rimverse_OutpostTileTaken".Translate();
            if (Find.World.Impassable(caravan.Tile)) return "Rimverse_OutpostTileTaken".Translate();
            if (caravan.PawnsListForReading.Count(p => p.IsFreeColonist) < ColonyUtility.MinSettlers) return "Rimverse_OutpostNeedColonists".Translate(ColonyUtility.MinSettlers);
            if (CaravanSteel(caravan) < SteelCost) return "Rimverse_OutpostNeedSteel".Translate(SteelCost);
            return true;
        }

        public static int CaravanSteel(Caravan caravan) => CaravanInventoryUtility.AllInventoryItems(caravan).Where(t => t.def == ThingDefOf.Steel).Sum(t => t.stackCount);

        public static void Found(Caravan caravan, List<Pawn> settlers)
        {
            GameComponent_PlayerColonies gc = GameComponent_PlayerColonies.Instance;
            GameComponent_Universe u = GameComponent_Universe.Instance;
            if (gc == null || u == null || settlers.Count < ColonyUtility.MinSettlers) return;
            int need = SteelCost;
            foreach (Thing t in CaravanInventoryUtility.AllInventoryItems(caravan).Where(t => t.def == ThingDefOf.Steel).ToList())
            {
                if (need <= 0) break;
                int take = Mathf.Min(need, t.stackCount);
                t.SplitOff(take).Destroy();
                need -= take;
            }
            StarSystemData home = u.HomeSystem;
            BodyData hw = home?.HomeWorld;
            string name = Faction.OfPlayer.def.settlementNameMaker != null ? NameGenerator.GenerateName(Faction.OfPlayer.def.settlementNameMaker, gc.colonies.Select(c => c.name)) : "Rimverse_OutpostDefaultName".Translate(gc.nextId).ToString();
            PlayerColony col = new PlayerColony
            {
                id = gc.nextId++,
                name = name,
                systemKey = u.HomeSystemId.Key,
                bodyIndex = hw?.index ?? 0,
                foundedTick = Find.TickManager.TicksGame,
                population = settlers.Count * 5f,
                surface = true,
                surfaceTileId = caravan.Tile.tileId,
                nextStepTick = Find.TickManager.TicksGame + RimverseUnits.TicksPerQuadrum,
                nextSituationTick = Find.TickManager.TicksGame + RimverseUnits.TicksFromDays(30f)
            };
            foreach (Pawn p in settlers)
            {
                caravan.RemovePawn(p);
                if (!Find.WorldPawns.Contains(p)) Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.KeepForever);
                col.settlers.Add(p);
            }
            gc.colonies.Add(col);
            WorldObject_PlayerOutpost o = (WorldObject_PlayerOutpost)WorldObjectMaker.MakeWorldObject(RimverseEmpireDefOf.Rimverse_PlayerOutpost);
            o.Tile = caravan.Tile;
            o.colonyId = col.id;
            o.SetFaction(Faction.OfPlayer);
            Find.WorldObjects.Add(o);
            if (!caravan.PawnsListForReading.Any())
            {
                foreach (Thing t in CaravanInventoryUtility.AllInventoryItems(caravan).ToList()) col.AddStock(t.def.defName, t.stackCount);
                caravan.Destroy();
            }
            Find.LetterStack.ReceiveLetter("Rimverse_LetterColonyFoundedLabel".Translate(), "Rimverse_LetterColonyFounded".Translate(col.name, settlers.Count), LetterDefOf.PositiveEvent, o);
        }
    }

    public class Dialog_FoundOutpost : Window
    {
        private readonly Caravan caravan;
        private readonly HashSet<Pawn> chosen = new HashSet<Pawn>();
        private Vector2 scroll;

        public override Vector2 InitialSize => new Vector2(480f, 520f);

        public Dialog_FoundOutpost(Caravan caravan)
        {
            this.caravan = caravan;
            forcePause = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), "Rimverse_OutpostTitle".Translate());
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(inRect.x, inRect.y + 36f, inRect.width, 48f), "Rimverse_OutpostInfo".Translate(OutpostUtility.SteelCost, ColonyUtility.MinSettlers));
            List<Pawn> pawns = caravan.PawnsListForReading.Where(p => p.IsFreeColonist).ToList();
            Rect outer = new Rect(inRect.x, inRect.y + 90f, inRect.width, inRect.height - 140f);
            Rect view = new Rect(0f, 0f, outer.width - 16f, pawns.Count * 30f);
            Widgets.BeginScrollView(outer, ref scroll, view);
            float y = 0f;
            foreach (Pawn p in pawns)
            {
                bool on = chosen.Contains(p);
                Widgets.CheckboxLabeled(new Rect(0f, y, view.width, 28f), p.LabelShortCap + " (" + "Rimverse_SocialLevel".Translate(p.skills?.GetSkill(SkillDefOf.Social).Level ?? 0) + ")", ref on);
                if (on) chosen.Add(p);
                else chosen.Remove(p);
                y += 30f;
            }
            Widgets.EndScrollView();
            bool ok = chosen.Count >= ColonyUtility.MinSettlers;
            Rect btn = new Rect(inRect.x + inRect.width - 200f, inRect.yMax - 40f, 200f, 36f);
            if (!ok) GUI.color = Color.gray;
            if (Widgets.ButtonText(btn, "Rimverse_FoundColonyConfirm".Translate(chosen.Count)) && ok)
            {
                OutpostUtility.Found(caravan, chosen.ToList());
                Close();
            }
            GUI.color = Color.white;
        }
    }

    [HarmonyPatch(typeof(Caravan), nameof(Caravan.GetGizmos))]
    public static class Patch_CaravanOutpostGizmo
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Caravan __instance)
        {
            foreach (Gizmo g in __result) yield return g;
            if (__instance.Faction != Faction.OfPlayer) yield break;
            if (DefDatabase<ResearchProjectDef>.GetNamedSilentFail("Rimverse_ColonyAdministration") is ResearchProjectDef r && !r.IsFinished) yield break;
            Caravan caravan = __instance;
            Command_Action cmd = new Command_Action
            {
                defaultLabel = "Rimverse_OutpostFound".Translate(),
                defaultDesc = "Rimverse_OutpostFoundDesc".Translate(OutpostUtility.SteelCost),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/Rimverse_Outpost", false) ?? ContentFinder<Texture2D>.Get("UI/Commands/Settle", false) ?? BaseContent.BadTex,
                action = () => Find.WindowStack.Add(new Dialog_FoundOutpost(caravan))
            };
            AcceptanceReport ok = OutpostUtility.CanFound(caravan);
            if (!ok.Accepted) cmd.Disable(ok.Reason);
            yield return cmd;
        }
    }
}
