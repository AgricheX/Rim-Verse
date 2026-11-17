using System.Collections.Generic;
using HarmonyLib;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    [HarmonyPatch(typeof(Pawn_DrawTracker), nameof(Pawn_DrawTracker.DrawPos), MethodType.Getter)]
    public static class Patch_ZeroGFloat
    {
        private static readonly Dictionary<int, int> checkedAt = new Dictionary<int, int>();
        private static readonly HashSet<int> floating = new HashSet<int>();

        public static void Postfix(Pawn ___pawn, ref Vector3 __result)
        {
            if (___pawn == null || !___pawn.Spawned || RimverseMod.Settings == null || !RimverseMod.Settings.lifeAboardEnabled) return;
            int id = ___pawn.thingIDNumber;
            int now = Find.TickManager.TicksGame;
            if (!checkedAt.TryGetValue(id, out int at) || now - at > 90 || now < at)
            {
                checkedAt[id] = now;
                if (Floats(___pawn)) floating.Add(id);
                else floating.Remove(id);
            }
            if (!floating.Contains(id)) return;
            float t = Time.realtimeSinceStartup * 1.3f + (id % 97) * 0.37f;
            __result.z += 0.07f + Mathf.Sin(t) * 0.05f;
            __result.x += Mathf.Sin(t * 0.6f) * 0.02f;
        }

        private static bool Floats(Pawn p)
        {
            if (p.InBed() || p.Downed && p.CurrentBed() != null) return false;
            if (!SpaceMapUtility.IsSpace(p.Map)) return false;
            if (GravityUtility.GravityAt(p) >= LifeAboard.ZeroG) return false;
            if (p.apparel != null && p.apparel.WornApparel.Any(a => a.def.defName == "Rimverse_Gear_MagBoots")) return false;
            return true;
        }
    }
}
