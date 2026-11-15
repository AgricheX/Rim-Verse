using HarmonyLib;
using Verse;

namespace Rimverse
{
    [HarmonyPatch]
    public static class TerrainGridPatches
    {
        private static void MarkDirty(TerrainGrid grid)
        {
            Map map = Traverse.Create(grid).Field("map").GetValue<Map>();
            map?.GetComponent<MapComponent_RimverseShips>()?.Dirty();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(TerrainGrid), nameof(TerrainGrid.SetTerrain))]
        public static void SetTerrain(TerrainGrid __instance) => MarkDirty(__instance);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(TerrainGrid), nameof(TerrainGrid.SetFoundation))]
        public static void SetFoundation(TerrainGrid __instance) => MarkDirty(__instance);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(TerrainGrid), nameof(TerrainGrid.RemoveTopLayer))]
        public static void RemoveTopLayer(TerrainGrid __instance) => MarkDirty(__instance);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(TerrainGrid), nameof(TerrainGrid.RemoveFoundation))]
        public static void RemoveFoundation(TerrainGrid __instance) => MarkDirty(__instance);
    }
}
