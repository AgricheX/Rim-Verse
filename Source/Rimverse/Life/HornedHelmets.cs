using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace Rimverse
{
    [HarmonyPatch(typeof(JobDriver_Wear), nameof(JobDriver_Wear.Notify_Starting))]
    public static class Patch_HornedHelmets
    {
        public static float FactorFor(Pawn p)
        {
            float a = LineageUtility.ExtensionOf(p)?.helmetWorkFactor ?? 1f;
            float b = GeneSpace.Factor(p, e => e.helmetWorkFactor);
            return a > b ? a : b;
        }

        public static bool IsHelmet(Apparel a) => a?.def.apparel?.bodyPartGroups != null && (a.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.FullHead) || a.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.UpperHead));

        public static void Postfix(JobDriver_Wear __instance, ref int ___duration)
        {
            Pawn p = __instance.pawn;
            Apparel a = __instance.job?.GetTarget(TargetIndex.A).Thing as Apparel;
            if (p == null || !IsHelmet(a)) return;
            float f = FactorFor(p);
            if (f > 1f) ___duration = (int)(___duration * f);
        }
    }
}
