using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace Rimverse
{
    public class Alert_TraderLeaving : Alert
    {
        public const int WarnTicks = RimverseUnits.TicksPerHour * 6;

        private readonly List<string> names = new List<string>();

        public Alert_TraderLeaving()
        {
            defaultLabel = "Rimverse_AlertTraderLeaving".Translate();
            defaultPriority = AlertPriority.Medium;
        }

        public override AlertReport GetReport()
        {
            names.Clear();
            if (!QolFeatures.IsEnabled("Rimverse_Qol_TraderLeaving")) return false;
            foreach (Map m in Find.Maps)
            {
                foreach (PassingShip s in m.passingShipManager.passingShips)
                {
                    if (s is TradeShip && !s.Departed && s.ticksUntilDeparture < WarnTicks) names.Add(s.name + " (" + (s.ticksUntilDeparture / (float)RimverseUnits.TicksPerHour).ToString("0.#") + " h)");
                }
            }
            return names.Count > 0;
        }

        public override TaggedString GetExplanation()
        {
            return "Rimverse_AlertTraderLeavingDesc".Translate(string.Join("\n", names));
        }
    }

    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.EndCurrentJob))]
    public static class Patch_UndraftAfterTucking
    {
        private static readonly HashSet<string> Jobs = new HashSet<string> { "Rescue", "TakeWoundedPrisonerToBed", "TakeDownedPawnToBedDrafted", "CarryToCryptosleepCasket", "CarryToCryptosleepCasketDrafted", "DeliverToBed" };

        public static void Prefix(Pawn_JobTracker __instance, out JobDef __state)
        {
            __state = __instance.curJob?.def;
        }

        public static void Postfix(Pawn ___pawn, JobCondition condition, JobDef __state)
        {
            if (__state == null || condition != JobCondition.Succeeded || !Jobs.Contains(__state.defName)) return;
            if (___pawn?.drafter == null || !___pawn.Drafted || !___pawn.IsColonistPlayerControlled) return;
            if (!QolFeatures.IsEnabled("Rimverse_Qol_UndraftAfterTucking")) return;
            ___pawn.drafter.Drafted = false;
        }
    }

    [HarmonyPatch(typeof(ThingDef), nameof(ThingDef.SpecialDisplayStats))]
    public static class Patch_ModSourceStat
    {
        public static IEnumerable<StatDrawEntry> Postfix(IEnumerable<StatDrawEntry> __result, ThingDef __instance)
        {
            foreach (StatDrawEntry e in __result) yield return e;
            if (__instance?.modContentPack == null || !QolFeatures.IsEnabled("Rimverse_Qol_ModSource")) yield break;
            string name = __instance.modContentPack.IsOfficialMod ? "Rimverse_ModSourceOfficial".Translate(__instance.modContentPack.Name).ToString() : __instance.modContentPack.Name;
            yield return new StatDrawEntry(StatCategoryDefOf.Basics, "Rimverse_ModSource".Translate(), name, "Rimverse_ModSourceDesc".Translate(), 9999);
        }
    }

    [StaticConstructorOnStartup]
    public static class QolLoadedDing
    {
        static QolLoadedDing()
        {
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                if (QolFeatures.IsEnabled("Rimverse_Qol_LoadedDing")) SoundDefOf.TinyBell.PlayOneShotOnCamera();
            });
        }
    }

    public class GameComponent_RimverseQol : GameComponent
    {
        private Dictionary<int, string> notes = new Dictionary<int, string>();
        private float fpsTimer;
        private int frames;
        private int lastTicks;
        private float fps;
        private float tps;

        public static GameComponent_RimverseQol Instance => Current.Game?.GetComponent<GameComponent_RimverseQol>();

        public GameComponent_RimverseQol(Game game)
        {
        }

        public string NoteFor(Pawn p) => p != null && notes.TryGetValue(p.thingIDNumber, out string s) ? s : "";

        public void SetNote(Pawn p, string text)
        {
            if (p == null) return;
            if (text.NullOrEmpty()) notes.Remove(p.thingIDNumber);
            else notes[p.thingIDNumber] = text;
        }

        public override void GameComponentUpdate()
        {
            ApplyCamera();
            frames++;
            fpsTimer += Time.unscaledDeltaTime;
            if (fpsTimer >= 1f)
            {
                int ticks = Find.TickManager.TicksGame;
                fps = frames / fpsTimer;
                tps = (ticks - lastTicks) / fpsTimer;
                lastTicks = ticks;
                frames = 0;
                fpsTimer = 0f;
            }
        }

        public static void ApplyCamera()
        {
            if (Find.CameraDriver?.config == null || !QolFeatures.IsEnabled("Rimverse_Qol_CameraZoom")) return;
            FloatRange r = Find.CameraDriver.config.sizeRange;
            if (r.max >= 110f && r.min <= 8f) return;
            Find.CameraDriver.config.sizeRange = new FloatRange(Mathf.Min(r.min, 8f), Mathf.Max(r.max, 110f));
        }

        public override void GameComponentOnGUI()
        {
            if (!QolFeatures.IsEnabled("Rimverse_Qol_FpsTps") || Find.UIRoot == null) return;
            Text.Font = GameFont.Tiny;
            Rect r = new Rect(4f, 4f, 200f, 20f);
            GUI.color = new Color(1f, 1f, 1f, 0.8f);
            Widgets.Label(r, "Rimverse_FpsTps".Translate(fps.ToString("0"), tps.ToString("0")));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref notes, "pawnNotes", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && notes == null) notes = new Dictionary<int, string>();
        }
    }

    public class ITab_PawnNotes : ITab
    {
        private Vector2 scroll;

        public ITab_PawnNotes()
        {
            size = new Vector2(420f, 380f);
            labelKey = "Rimverse_TabNotes";
        }

        public override bool IsVisible => SelPawn != null && SelPawn.RaceProps.Humanlike && QolFeatures.IsEnabled("Rimverse_Qol_PawnNotes");

        protected override void FillTab()
        {
            GameComponent_RimverseQol gc = GameComponent_RimverseQol.Instance;
            if (gc == null) return;
            Rect r = new Rect(0f, 0f, size.x, size.y).ContractedBy(12f);
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(r.x, r.y, r.width, 24f), "Rimverse_NotesFor".Translate(SelPawn.LabelShortCap) + (NavalRanks.RankLabel(SelPawn) is string rk ? " · " + rk : ""));
            Rect area = new Rect(r.x, r.y + 28f, r.width, r.height - 28f);
            string before = gc.NoteFor(SelPawn);
            float h = Mathf.Max(area.height, Text.CalcHeight(before, area.width - 20f) + 40f);
            Rect inner = new Rect(0f, 0f, area.width - 16f, h);
            Widgets.BeginScrollView(area, ref scroll, inner);
            string after = Widgets.TextArea(inner, before);
            Widgets.EndScrollView();
            if (after != before) gc.SetNote(SelPawn, after);
        }
    }

    public class MainButtonWorker_RimverseMedical : MainButtonWorker_ToggleTab
    {
        public override bool Visible => base.Visible && QolFeatures.IsEnabled("Rimverse_Qol_MedicalTab");
    }

    public class MainTabWindow_RimverseMedical : MainTabWindow
    {
        private Vector2 scroll;

        public override Vector2 RequestedTabSize => new Vector2(900f, 520f);

        private static IEnumerable<Pawn> Patients(Map map)
        {
            if (map == null) return Enumerable.Empty<Pawn>();
            return map.mapPawns.AllPawnsSpawned.Where(p => !p.Dead && (p.IsColonist || p.IsPrisonerOfColony || p.IsSlaveOfColony || p.Faction == Faction.OfPlayer && p.RaceProps.Animal)).OrderBy(p => p.health.summaryHealth.SummaryHealthPercent);
        }

        public override void DoWindowContents(Rect inRect)
        {
            Map map = Find.CurrentMap;
            List<Pawn> list = Patients(map).ToList();
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), "Rimverse_MedicalTitle".Translate(list.Count(p => p.health.HasHediffsNeedingTend() || p.health.hediffSet.BleedRateTotal > 0.001f)));
            Text.Font = GameFont.Small;
            float[] cols = { 0f, 220f, 320f, 420f, 520f, 620f };
            string[] heads = { "Rimverse_MedName", "Rimverse_MedHealth", "Rimverse_MedBleeding", "Rimverse_MedPain", "Rimverse_MedNeedsTend", "Rimverse_MedWorst" };
            float y = inRect.y + 36f;
            for (int i = 0; i < cols.Length; i++) Widgets.Label(new Rect(inRect.x + cols[i], y, 200f, 24f), heads[i].Translate());
            y += 26f;
            Widgets.DrawLineHorizontal(inRect.x, y - 2f, inRect.width);
            Rect outer = new Rect(inRect.x, y, inRect.width, inRect.yMax - y);
            Rect view = new Rect(0f, 0f, outer.width - 16f, list.Count * 28f);
            Widgets.BeginScrollView(outer, ref scroll, view);
            float ry = 0f;
            foreach (Pawn p in list)
            {
                Rect row = new Rect(0f, ry, view.width, 26f);
                if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);
                float bleed = p.health.hediffSet.BleedRateTotal;
                int tend = p.health.hediffSet.hediffs.Count(h => h.TendableNow());
                Hediff worst = p.health.hediffSet.hediffs.Where(h => h.Visible && h.def.isBad).OrderByDescending(h => h.Severity * (h.def.lethalSeverity > 0f ? 1f / h.def.lethalSeverity : 0.1f)).FirstOrDefault();
                if (bleed > 0.3f) GUI.color = new Color(1f, 0.5f, 0.5f);
                Widgets.Label(new Rect(cols[0], ry, 215f, 26f), p.LabelShortCap);
                Widgets.Label(new Rect(cols[1], ry, 95f, 26f), p.health.summaryHealth.SummaryHealthPercent.ToStringPercent());
                Widgets.Label(new Rect(cols[2], ry, 95f, 26f), bleed > 0.001f ? (bleed).ToStringPercent() + "/d" : "-");
                Widgets.Label(new Rect(cols[3], ry, 95f, 26f), p.health.hediffSet.PainTotal.ToStringPercent());
                Widgets.Label(new Rect(cols[4], ry, 95f, 26f), tend > 0 ? tend.ToString() : "-");
                Widgets.Label(new Rect(cols[5], ry, view.width - cols[5], 26f), worst?.LabelCap ?? "-");
                GUI.color = Color.white;
                if (Widgets.ButtonInvisible(row))
                {
                    CameraJumper.TryJumpAndSelect(p);
                }
                ry += 28f;
            }
            Widgets.EndScrollView();
        }
    }
}
