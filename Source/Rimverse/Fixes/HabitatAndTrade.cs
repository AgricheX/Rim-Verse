using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Rimverse
{
    public static class HabitatAir
    {
        private static readonly Dictionary<int, KeyValuePair<int, bool>> cache = new Dictionary<int, KeyValuePair<int, bool>>();

        public static bool Pressurized(Map map, Room room)
        {
            if (room == null || map == null) return false;
            if (MegaSectionUtility.Pressurized(map, room)) return true;
            if (!ResearchGate.Done("Rimverse_HabitatSealing")) return false;
            int now = Find.TickManager.TicksGame;
            if (cache.TryGetValue(room.ID, out KeyValuePair<int, bool> hit) && now - hit.Key < 250) return hit.Value;
            bool ok = Compute(room);
            if (cache.Count > 2000) cache.Clear();
            cache[room.ID] = new KeyValuePair<int, bool>(now, ok);
            return ok;
        }

        private static bool Compute(Room room)
        {
            if (room.TouchesMapEdge || room.PsychologicallyOutdoors || room.OpenRoofCount > 0 || room.CellCount > 2500) return false;
            foreach (Thing t in room.ContainedAndAdjacentThings)
            {
                CompShipPart part = t.TryGetComp<CompShipPart>();
                if (part == null || part.Props.role != ShipPartRole.AirGenerator) continue;
                if (t.Faction != Faction.OfPlayer) continue;
                CompPowerTrader power = t.TryGetComp<CompPowerTrader>();
                if (power == null || power.PowerOn) return true;
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(TradeUtility), nameof(TradeUtility.AllLaunchableThingsForTrade))]
    public static class Patch_TradeWithoutBeacon
    {
        public static void Postfix(Map map, ref IEnumerable<Thing> __result)
        {
            if (map == null || !ResearchGate.Done("Rimverse_OrbitalTradeDocking") || !Applies(map)) return;
            List<Thing> extra = Extra(map);
            if (extra.Count == 0) return;
            __result = __result.Concat(extra).Distinct().ToList();
        }

        private static bool Applies(Map map)
        {
            MapComponent_TraderDocks docks = map.GetComponent<MapComponent_TraderDocks>();
            if (docks != null && docks.docks.Count > 0) return true;
            if (map.listerBuildings.allBuildingsColonist.Any(b => b.TryGetComp<CompDockingPort>() != null)) return true;
            return map.listerThings.ThingsOfDef(RimverseTradeDefOf.Rimverse_TradeTerminal).Count > 0;
        }

        public static List<Thing> SellableNear(Map map, IntVec3 at)
        {
            List<Thing> list = Extra(map);
            foreach (Thing t in map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver))
            {
                if (t.Spawned && t.def.category == ThingCategory.Item && !t.IsForbidden(Faction.OfPlayer) && t.Position.InHorDistOf(at, 14f) && !list.Contains(t)) list.Add(t);
            }
            return list;
        }

        private static List<Thing> Extra(Map map)
        {
            List<IntVec3> anchors = new List<IntVec3>();
            foreach (Building b in map.listerBuildings.allBuildingsColonist) if (b.TryGetComp<CompDockingPort>() != null) anchors.Add(b.Position);
            foreach (Thing t in map.listerThings.ThingsOfDef(RimverseTradeDefOf.Rimverse_TradeTerminal)) anchors.Add(t.Position);
            MapComponent_RimverseShips ships = map.GetComponent<MapComponent_RimverseShips>();
            List<Thing> list = new List<Thing>();
            foreach (Thing t in map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver))
            {
                if (!t.Spawned || t is Pawn || t.def.category != ThingCategory.Item || t.IsForbidden(Faction.OfPlayer)) continue;
                if (!TradeUtility.EverPlayerSellable(t.def)) continue;
                bool ok = t.IsInAnyStorage();
                if (!ok && ships != null)
                {
                    RimverseShip s = ships.ShipAt(t.Position);
                    ok = s != null && s.bridge != null && s.bridge.Faction == Faction.OfPlayer;
                }
                if (!ok) ok = anchors.Any(a => a.InHorDistOf(t.Position, 14f));
                if (ok) list.Add(t);
            }
            return list;
        }
    }
}
