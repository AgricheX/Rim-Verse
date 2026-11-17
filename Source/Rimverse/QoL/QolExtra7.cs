using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace Rimverse
{
    [StaticConstructorOnStartup]
    public static class QolDeconstructFirst
    {
        static QolDeconstructFirst()
        {
            if (!QolFeatures.IsEnabled("Rimverse_Qol_DeconstructFirst")) return;
            foreach (WorkTypeDef wt in DefDatabase<WorkTypeDef>.AllDefsListForReading)
            {
                List<WorkGiverDef> list = wt.workGiversByPriority;
                if (list == null || list.Count < 2) continue;
                List<WorkGiverDef> first = list.Where(IsRemoval).ToList();
                if (first.Count == 0 || first.Count == list.Count) continue;
                list.RemoveAll(IsRemoval);
                list.InsertRange(0, first);
            }
        }

        private static bool IsRemoval(WorkGiverDef d)
        {
            return d.giverClass != null && (typeof(WorkGiver_Deconstruct).IsAssignableFrom(d.giverClass) || typeof(WorkGiver_Uninstall).IsAssignableFrom(d.giverClass));
        }
    }

    [HarmonyPatch(typeof(Building_TurretGun), nameof(Building_TurretGun.TryFindNewTarget))]
    public static class Patch_QolTurretTargetPriority
    {
        private static readonly Func<Building_TurretGun, Thing, bool> isValid = AccessTools.MethodDelegate<Func<Building_TurretGun, Thing, bool>>(AccessTools.Method(typeof(Building_TurretGun), "IsValidTarget"));

        public static void Postfix(Building_TurretGun __instance, ref LocalTargetInfo __result)
        {
            if (!(__result.Thing is Pawn) || __instance.Faction != Faction.OfPlayer || !QolFeatures.IsEnabled("Rimverse_Qol_TargetPriority")) return;
            Verb verb = __instance.AttackVerb;
            if (verb == null || verb.ProjectileFliesOverhead()) return;
            float range = verb.EffectiveRange;
            Thing best = __result.Thing;
            float bestScore = Score(__instance, best);
            foreach (IAttackTarget t in __instance.Map.attackTargetsCache.GetPotentialTargetsFor(__instance))
            {
                if (!(t.Thing is Pawn p) || p == best || p.Downed || t.ThreatDisabled(__instance)) continue;
                if (p.Position.DistanceTo(__instance.Position) > range) continue;
                float s = Score(__instance, p);
                if (s <= bestScore) continue;
                if (!isValid(__instance, p) || !verb.CanHitTarget(p)) continue;
                best = p;
                bestScore = s;
            }
            __result = best;
        }

        private static float Score(Building_TurretGun turret, Thing t)
        {
            Pawn p = t as Pawn;
            if (p == null) return 0f;
            float s = 0f;
            if (p.stances?.curStance is Stance_Busy busy && busy.focusTarg.IsValid && busy.focusTarg.Thing != null && busy.focusTarg.Thing.Faction == Faction.OfPlayer) s += 100f;
            if (p.equipment?.Primary != null && p.equipment.Primary.def.IsRangedWeapon) s += 20f;
            if (p.RaceProps.IsMechanoid) s += 10f;
            s += (1f - p.health.summaryHealth.SummaryHealthPercent) * 30f;
            s -= p.Position.DistanceTo(turret.Position) * 0.5f;
            return s;
        }
    }
}
