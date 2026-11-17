using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public static class FaunaGravity
    {
        private static readonly Dictionary<int, float> scaleByMap = new Dictionary<int, float>();
        private static readonly Dictionary<int, List<PawnKindDef>> speciesByMap = new Dictionary<int, List<PawnKindDef>>();
        private static int cacheTick = -99999;

        private static void Refresh()
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            if (now - cacheTick < 2500 && now >= cacheTick) return;
            cacheTick = now;
            scaleByMap.Clear();
            speciesByMap.Clear();
        }

        public static float ScaleFor(Map map)
        {
            if (map == null) return 1f;
            Refresh();
            if (scaleByMap.TryGetValue(map.uniqueID, out float s)) return s;
            MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(map);
            s = 1f;
            if (loc != null && loc.OnSurface && loc.Body != null && !loc.Body.homeWorld)
            {
                float g = Mathf.Max(0.05f, loc.Body.gravity);
                s = Mathf.Clamp(1f / Mathf.Sqrt(g), 0.75f, 1.35f);
            }
            scaleByMap[map.uniqueID] = s;
            return s;
        }

        public static List<PawnKindDef> Species(Map map)
        {
            Refresh();
            if (speciesByMap.TryGetValue(map.uniqueID, out List<PawnKindDef> list)) return list;
            MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(map);
            list = loc != null && loc.OnSurface && loc.Body != null && loc.Body.atmosphere == AtmosphereType.Breathable ? GenStep_AlienFauna.SpeciesFor(loc.Body) : new List<PawnKindDef>();
            speciesByMap[map.uniqueID] = list;
            return list;
        }
    }

    [HarmonyPatch(typeof(PawnRenderNodeWorker), nameof(PawnRenderNodeWorker.ScaleFor))]
    public static class Patch_FaunaGravityScale
    {
        public static void Postfix(PawnDrawParms parms, ref Vector3 __result)
        {
            Pawn p = parms.pawn;
            if (p == null || !p.RaceProps.Animal || !p.Spawned) return;
            if (!p.kindDef.HasModExtension<AlienFaunaExtension>()) return;
            float s = FaunaGravity.ScaleFor(p.Map);
            if (s == 1f) return;
            __result.x *= s;
            __result.z *= s;
        }
    }

    [HarmonyPatch(typeof(WildAnimalSpawner), nameof(WildAnimalSpawner.SpawnRandomWildAnimalAt))]
    public static class Patch_AlienWildSpawn
    {
        public static void Prefix(WildAnimalSpawner __instance, IntVec3 loc, ref PawnKindDef animalKind)
        {
            if (animalKind != null) return;
            Map map = Traverse.Create(__instance).Field("map").GetValue<Map>();
            if (map == null) return;
            List<PawnKindDef> list = FaunaGravity.Species(map);
            if (list.Count == 0 || !Rand.Chance(0.75f)) return;
            animalKind = list.RandomElement();
        }
    }
}
