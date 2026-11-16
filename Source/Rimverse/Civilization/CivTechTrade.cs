using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public static class CivTechTrade
    {
        public const int CooldownDays = 30;

        private static readonly string[] Rare = { "Rimverse_ExoticMaterials", "Rimverse_ExoticPower", "Rimverse_ArchotechAscension" };

        private static Dictionary<int, int> Sales => GameComponent_RimverseEvents.Instance?.techSaleUntil;

        public static IEnumerable<ResearchProjectDef> Offered(Civilization c) =>
            DefDatabase<ResearchProjectDef>.AllDefsListForReading.Where(r => !r.IsFinished && r.techLevel <= c.tech && r.PrerequisitesCompleted && !Rare.Contains(r.defName));

        public static int Price(Civilization c, ResearchProjectDef r)
        {
            float f = c.playerState >= DiploState.Alliance ? 0.7f : c.playerState >= DiploState.TradeAgreement ? 0.85f : 1f;
            return Mathf.RoundToInt(r.baseCost * 1.5f * f / 10f) * 10;
        }

        public static AcceptanceReport CanBuy(Civilization c, Map map)
        {
            if (c == null || map == null || c.NoDiplomacy || c.playerState == DiploState.War) return false;
            if (c.playerOpinion < 0f) return "Rimverse_TechSaleOpinion".Translate();
            Dictionary<int, int> s = Sales;
            if (s != null && s.TryGetValue(c.id, out int until) && Find.TickManager.TicksGame < until) return "Rimverse_TechSaleCooldown".Translate(((until - Find.TickManager.TicksGame) / (float)GenDate.TicksPerDay).ToString("0"));
            if (!Offered(c).Any()) return "Rimverse_TechSaleNothing".Translate();
            return true;
        }

        public static void OpenMenu(Civilization c, Map map)
        {
            List<FloatMenuOption> opts = new List<FloatMenuOption>();
            foreach (ResearchProjectDef r in Offered(c).OrderBy(x => x.baseCost).Take(12))
            {
                int price = Price(c, r);
                bool can = TradeUtility.ColonyHasEnoughSilver(map, price);
                ResearchProjectDef proj = r;
                opts.Add(new FloatMenuOption("Rimverse_TechSaleOption".Translate(r.LabelCap, price).ToString() + (can ? "" : " (" + "Rimverse_TechSaleNoSilver".Translate().ToString() + ")"), can ? () => Buy(c, map, proj, price) : (System.Action)null));
            }
            if (opts.Count > 0) Find.WindowStack.Add(new FloatMenu(opts));
        }

        public static void Buy(Civilization c, Map map, ResearchProjectDef r, int price)
        {
            if (!TradeUtility.ColonyHasEnoughSilver(map, price)) return;
            TradeUtility.LaunchSilver(map, price);
            Find.ResearchManager.FinishProject(r, false, null, false);
            if (Sales != null) Sales[c.id] = Find.TickManager.TicksGame + CooldownDays * GenDate.TicksPerDay;
            GameComponent_Civilizations.Instance?.ChangePlayerOpinion(c, 2f);
            Messages.Message("Rimverse_TechSaleDone".Translate(r.LabelCap, c.ShownName), MessageTypeDefOf.PositiveEvent, false);
        }

        public static void Steal(Civilization c)
        {
            if (c == null) return;
            ResearchProjectDef r = Offered(c).RandomElementWithFallback();
            if (r == null) return;
            if (Rand.Chance(0.5f)) Find.ResearchManager.FinishProject(r, false, null, false);
            else Find.ResearchManager.AddProgress(r, r.baseCost * 0.5f);
            Messages.Message("Rimverse_TechStolen".Translate(r.LabelCap, c.ShownName), MessageTypeDefOf.PositiveEvent, false);
        }

        public static void RareFromRuins(float amount)
        {
            ResearchProjectDef r = Rare.Select(n => DefDatabase<ResearchProjectDef>.GetNamedSilentFail(n)).FirstOrDefault(x => x != null && !x.IsFinished);
            if (r == null) return;
            Find.ResearchManager.AddProgress(r, amount);
            Messages.Message("Rimverse_RuinResearch".Translate(r.LabelCap), MessageTypeDefOf.PositiveEvent, false);
        }
    }

    public class CompPowerPlantShipSolar : CompPowerPlant
    {
        public static float StarFactor(Map map)
        {
            MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(map);
            StarSystemData sys = loc?.System;
            BodyData b = loc?.Body;
            if (sys == null || b == null) return 1f;
            BodyData orbit = b.parent >= 0 ? sys.Body(b.parent) ?? b : b;
            float au = Mathf.Max(0.05f, orbit.orbitAU);
            return Mathf.Clamp(sys.TotalLuminosity / (au * au), 0.05f, 3f);
        }

        protected override float DesiredPowerOutput => base.DesiredPowerOutput * (parent.Spawned ? StarFactor(parent.Map) : 1f);
    }

    public class GameComponent_PrisonerSabotage : GameComponent
    {
        public GameComponent_PrisonerSabotage(Game game)
        {
        }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % 2500 != 777) return;
            foreach (Map map in Find.Maps)
            {
                RimverseShip ship = ColonyUtility.PlayerShipOn(map);
                if (ship == null) continue;
                foreach (Pawn p in map.mapPawns.PrisonersOfColonySpawned)
                {
                    if (!PrisonBreakUtility.IsPrisonBreaking(p)) continue;
                    if (!Rand.Chance(0.25f)) continue;
                    CompShipPart part = ship.parts.Where(x => x != null && x.parent.Spawned && x.parent.GetComp<CompBreakdownable>() != null && !x.parent.GetComp<CompBreakdownable>().BrokenDown).OrderBy(x => x.parent.Position.DistanceToSquared(p.Position)).FirstOrDefault();
                    if (part == null) continue;
                    part.parent.GetComp<CompBreakdownable>().DoBreakdown();
                    Messages.Message("Rimverse_MsgPrisonerSabotage".Translate(p.LabelShort, part.parent.LabelShort), part.parent, MessageTypeDefOf.NegativeEvent, false);
                    break;
                }
            }
        }
    }
}
