using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    [HarmonyPatch(typeof(Need_Mood), nameof(Need_Mood.MoodString), MethodType.Getter)]
    public static class Patch_QolMoodColor
    {
        public static void Postfix(Need_Mood __instance, Pawn ___pawn, ref string __result)
        {
            if (__result.NullOrEmpty() || ___pawn?.mindState?.mentalBreaker == null || !QolFeatures.IsEnabled("Rimverse_Qol_MoodColors")) return;
            float m = __instance.CurLevel;
            Color c;
            if (m < ___pawn.mindState.mentalBreaker.BreakThresholdMinor) c = ColorLibrary.RedReadable;
            else if (m < 0.5f) c = new Color(1f, 0.85f, 0.35f);
            else if (m >= 0.75f) c = new Color(0.45f, 0.9f, 0.45f);
            else return;
            __result = __result.Colorize(c);
        }
    }

    public class TransferableComparer_ValuePerMass : TransferableComparer
    {
        private static float Ratio(Transferable t)
        {
            Thing a = t.AnyThing;
            if (a == null) return 0f;
            float mass = a.GetStatValue(StatDefOf.Mass);
            return mass <= 0.001f ? a.MarketValue * 1000f : a.MarketValue / mass;
        }

        public override int Compare(Transferable lhs, Transferable rhs)
        {
            return Ratio(rhs).CompareTo(Ratio(lhs));
        }
    }
}
