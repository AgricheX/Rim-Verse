using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public class GameComponent_QolFollow : GameComponent
    {
        public static Pawn followed;

        public GameComponent_QolFollow(Game game)
        {
        }

        public override void GameComponentUpdate()
        {
            if (followed == null) return;
            if (followed.Destroyed || followed.Dead || !followed.Spawned || followed.Map != Find.CurrentMap || !QolFeatures.IsEnabled("Rimverse_Qol_FollowPawn"))
            {
                followed = null;
                return;
            }
            Find.CameraDriver.JumpToCurrentMapLoc(followed.DrawPos);
        }

        public override void GameComponentOnGUI()
        {
            KeyBindingDef k = DefDatabase<KeyBindingDef>.GetNamedSilentFail("Rimverse_UltraSpeed");
            if (!QolKeyChords.Pressed(k) || !QolFeatures.IsEnabled("Rimverse_Qol_ExtraSpeed")) return;
            Find.TickManager.CurTimeSpeed = Find.TickManager.CurTimeSpeed == TimeSpeed.Ultrafast ? TimeSpeed.Superfast : TimeSpeed.Ultrafast;
            Event.current.Use();
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    public static class Patch_QolFollowGizmo
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> values, Pawn __instance)
        {
            foreach (Gizmo g in values) yield return g;
            if (__instance.Faction != Faction.OfPlayer || !__instance.Spawned || !QolFeatures.IsEnabled("Rimverse_Qol_FollowPawn")) yield break;
            Pawn p = __instance;
            yield return new Command_Toggle
            {
                defaultLabel = "Rimverse_QolFollow".Translate(),
                defaultDesc = "Rimverse_QolFollowDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/FollowMe", false) ?? TexButton.Search,
                isActive = () => GameComponent_QolFollow.followed == p,
                toggleAction = () => GameComponent_QolFollow.followed = GameComponent_QolFollow.followed == p ? null : p
            };
        }
    }

    [HarmonyPatch(typeof(DateReadout), nameof(DateReadout.DateOnGUI))]
    public static class Patch_QolDetailedDate
    {
        public static void Postfix(Rect dateRect)
        {
            if (!QolFeatures.IsEnabled("Rimverse_Qol_DetailedDate") || Find.CurrentMap == null) return;
            Vector2 ll = Find.WorldGrid.LongLatOf(Find.CurrentMap.Tile);
            long abs = Find.TickManager.TicksAbs;
            string tip = "Rimverse_QolDateTip".Translate(GenDate.DateFullStringWithHourAt(abs, ll), GenDate.DayOfYear(abs, ll.x) + 1, GenDate.DaysPassed, Find.TickManager.TicksGame.ToStringCached());
            TooltipHandler.TipRegion(dateRect, tip);
        }
    }

    [HarmonyPatch(typeof(ThingComp), nameof(ThingComp.SpecialDisplayStats))]
    public static class Patch_QolDaysToRot
    {
        public static IEnumerable<StatDrawEntry> Postfix(IEnumerable<StatDrawEntry> values, ThingComp __instance)
        {
            foreach (StatDrawEntry e in values) yield return e;
            if (!(__instance is CompRottable rot) || !QolFeatures.IsEnabled("Rimverse_Qol_DetailedDate")) yield break;
            int ticks = rot.TicksUntilRotAtCurrentTemp;
            string v = ticks >= 3600000 ? "Rimverse_QolNeverRots".Translate().ToString() : (ticks / (float)GenDate.TicksPerDay).ToString("0.#");
            yield return new StatDrawEntry(StatCategoryDefOf.BasicsNonPawn, "Rimverse_QolDaysToRot".Translate(), v, "Rimverse_QolDaysToRotDesc".Translate(), 5000);
        }
    }

    public class CompProperties_TempGauge : CompProperties
    {
        public CompProperties_TempGauge()
        {
            compClass = typeof(CompTempGauge);
        }
    }

    public class CompTempGauge : ThingComp
    {
        public override void DrawGUIOverlay()
        {
            base.DrawGUIOverlay();
            if (!parent.Spawned || Find.CameraDriver.CurrentZoom > CameraZoomRange.Middle) return;
            Room r = parent.GetRoom();
            float t = r != null && !r.UsesOutdoorTemperature ? r.Temperature : parent.Map.mapTemperature.OutdoorTemp;
            Color c = t < 0f ? new Color(0.5f, 0.75f, 1f) : t > 35f ? new Color(1f, 0.5f, 0.35f) : Color.white;
            GenMapUI.DrawThingLabel(parent, t.ToStringTemperature("F0"), c);
        }

        public override string CompInspectStringExtra()
        {
            Room r = parent.GetRoom();
            return r == null ? null : "Rimverse_QolGaugeInspect".Translate(r.Temperature.ToStringTemperature("F1"));
        }
    }

    [HarmonyPatch(typeof(OptionListingUtility), nameof(OptionListingUtility.DrawOptionListing))]
    public static class Patch_QolContinue
    {
        private static string loadLabel;

        public static void Prefix(List<ListableOption> optList)
        {
            if (Current.ProgramState != ProgramState.Entry || optList == null || !QolFeatures.IsEnabled("Rimverse_Qol_Continue")) return;
            if (loadLabel == null) loadLabel = "LoadGame".Translate();
            int idx = optList.FindIndex(o => o.label == loadLabel);
            string cont = "Rimverse_QolContinue".Translate();
            if (idx < 0 || optList.Any(o => o.label == cont)) return;
            FileInfo last = GenFilePaths.AllSavedGameFiles.OrderByDescending(f => f.LastWriteTime).FirstOrDefault();
            if (last == null) return;
            string name = Path.GetFileNameWithoutExtension(last.Name);
            optList.Insert(idx, new ListableOption(cont, () => GameDataSaveLoader.LoadGame(name)));
        }
    }
}
