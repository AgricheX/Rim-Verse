using HarmonyLib;
using Verse;
using Verse.AI;

namespace Rimverse
{
    [HarmonyPatch(typeof(Pawn_PathFollower), "CostToMoveIntoCell", new[] { typeof(Pawn), typeof(IntVec3) })]
    public static class Patch_WaterSpeed
    {
        public static float FactorFor(Pawn pawn)
        {
            if (pawn == null || !pawn.RaceProps.Humanlike) return 1f;
            float f = LineageUtility.ExtensionOf(pawn)?.waterSpeedFactor ?? 1f;
            float g = GeneSpace.Factor(pawn, e => e.waterSpeedFactor);
            return f > g ? f : g;
        }

        public static void Postfix(Pawn pawn, IntVec3 c, ref float __result)
        {
            Map map = pawn?.Map;
            if (map == null || !c.InBounds(map)) return;
            TerrainDef t = c.GetTerrain(map);
            if (t == null || !t.IsWater) return;
            float f = FactorFor(pawn);
            if (f > 1f) __result /= f;
        }
    }
}
