using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    [HarmonyPatch(typeof(MapTemperature), nameof(MapTemperature.OutdoorTemp), MethodType.Getter)]
    public static class Patch_MapTemperature_OutdoorTemp
    {
        public static void Postfix(Map ___map, ref float __result)
        {
            if (BodyTemperatureUtility.TryGet(___map, true, out float t)) __result = t;
        }
    }

    [HarmonyPatch(typeof(MapTemperature), nameof(MapTemperature.SeasonalTemp), MethodType.Getter)]
    public static class Patch_MapTemperature_SeasonalTemp
    {
        public static void Postfix(Map ___map, ref float __result)
        {
            if (BodyTemperatureUtility.TryGet(___map, false, out float t)) __result = t;
        }
    }

    public static class BodyTemperatureUtility
    {
        public static bool TryGet(Map map, bool daily, out float temperature)
        {
            temperature = 0f;
            if (map == null || !map.IsPocketMap) return false;
            if (!MapComponent_ShipLocation.ByMapId.TryGetValue(map.uniqueID, out MapComponent_ShipLocation loc) || loc.map != map) return false;
            if (!loc.TryGetBodyTemperature(out float baseTemp, out float swing)) return false;
            temperature = baseTemp;
            if (daily) temperature -= swing * Mathf.Cos(GenLocalDate.DayPercent(map) * Mathf.PI * 2f);
            return true;
        }
    }
}
