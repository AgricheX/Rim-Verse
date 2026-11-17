using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace Rimverse
{
    public static class SuitUpOnBreach
    {
        private static readonly Dictionary<int, int> lastAlert = new Dictionary<int, int>();

        public static void Alert(Map map)
        {
            if (map == null || !QolFeatures.IsEnabled("Rimverse_Qol_SuitUpOnBreach")) return;
            int now = Find.TickManager.TicksGame;
            if (lastAlert.TryGetValue(map.uniqueID, out int last) && now - last < 2500 && now >= last) return;
            lastAlert[map.uniqueID] = now;
            List<Apparel> free = map.listerThings.ThingsInGroup(ThingRequestGroup.Apparel).OfType<Apparel>().Where(a => a.Spawned && a.TryGetComp<CompVacuumSuit>() != null && !a.IsForbidden(Faction.OfPlayer)).ToList();
            int n = 0;
            foreach (Pawn p in map.mapPawns.FreeColonistsSpawned.ToList())
            {
                if (p.Downed || p.Drafted || p.InMentalState || p.apparel == null) continue;
                CompVacuumSuit worn = VacuumSuitUtility.WornSuit(p, out bool helmet);
                Job first = null;
                if (worn == null) first = WearJob(p, free, false);
                Job second = helmet ? null : WearJob(p, free, true);
                if (first == null && second == null) continue;
                if (first != null)
                {
                    p.jobs.TryTakeOrderedJob(first, JobTag.Misc);
                    if (second != null) p.jobs.jobQueue.EnqueueLast(second);
                }
                else p.jobs.TryTakeOrderedJob(second, JobTag.Misc);
                n++;
            }
            if (n > 0) Messages.Message("Rimverse_SuitUpOnBreach".Translate(n), MessageTypeDefOf.CautionInput, false);
        }

        private static Job WearJob(Pawn p, List<Apparel> free, bool helmet)
        {
            Apparel best = null;
            float bestDist = float.MaxValue;
            foreach (Apparel a in free)
            {
                if (a.TryGetComp<CompVacuumSuit>().Props.helmet != helmet || !ApparelUtility.HasPartsToWear(p, a.def) || !p.CanReserveAndReach(a, PathEndMode.ClosestTouch, Danger.Deadly)) continue;
                float d = a.Position.DistanceToSquared(p.Position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = a;
                }
            }
            if (best == null) return null;
            free.Remove(best);
            return JobMaker.MakeJob(JobDefOf.Wear, best);
        }
    }

    [HarmonyPatch(typeof(ShipThoughts), nameof(ShipThoughts.GiveMap))]
    public static class Patch_SuitUpGiveMap
    {
        public static void Postfix(Map map, string key)
        {
            if (key == "SawHullBreach") SuitUpOnBreach.Alert(map);
        }
    }

    [HarmonyPatch(typeof(ShipThoughts), nameof(ShipThoughts.GiveShip))]
    public static class Patch_SuitUpGiveShip
    {
        public static void Postfix(RimverseShip ship, string key)
        {
            if (key == "SawHullBreach") SuitUpOnBreach.Alert(ship?.map);
        }
    }
}
