using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Rimverse
{
    public class SealedGrowerExtension : DefModExtension
    {
    }

    [HarmonyPatch(typeof(Plant), nameof(Plant.GrowthRate), MethodType.Getter)]
    public static class Patch_ZeroGPlants
    {
        private static readonly HashSet<string> sealedGrowers = new HashSet<string> { "Rimverse_HydroAuto", "Rimverse_ClosedGarden", "Rimverse_ShipPlanter" };

        public static bool Sealed(ThingDef d) => d != null && (sealedGrowers.Contains(d.defName) || d.HasModExtension<SealedGrowerExtension>());

        public static void Postfix(Plant __instance, ref float __result)
        {
            if (__result <= 0f || !__instance.Spawned || !SpaceMapUtility.IsSpace(__instance.Map)) return;
            if (GravityUtility.GravityAt(__instance.Map, __instance.Position) >= LifeAboard.ZeroG) return;
            if (Sealed(__instance.Position.GetEdifice(__instance.Map)?.def)) return;
            __result = 0f;
        }
    }

    [HarmonyPatch(typeof(Plant), nameof(Plant.GetInspectString))]
    public static class Patch_ZeroGPlantsInspect
    {
        public static void Postfix(Plant __instance, ref string __result)
        {
            if (!__instance.Spawned || !SpaceMapUtility.IsSpace(__instance.Map) || GravityUtility.GravityAt(__instance.Map, __instance.Position) >= LifeAboard.ZeroG) return;
            if (Patch_ZeroGPlants.Sealed(__instance.Position.GetEdifice(__instance.Map)?.def)) return;
            __result = (__result.NullOrEmpty() ? "" : __result + "\n") + "Rimverse_ZeroGPlantStopped".Translate();
        }
    }
}
