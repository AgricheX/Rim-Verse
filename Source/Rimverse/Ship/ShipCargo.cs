using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Rimverse
{
    public static class ShipCargo
    {
        private static readonly Dictionary<int, (int tick, float tons)> cache = new Dictionary<int, (int, float)>();

        public static float Tons(RimverseShip ship)
        {
            if (ship?.map == null) return 0f;
            int now = Find.TickManager.TicksGame;
            if (cache.TryGetValue(ship.id, out var c) && now - c.tick < 250 && now >= c.tick) return c.tons;
            float kg = 0f;
            List<Thing> items = ship.map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver);
            for (int i = 0; i < items.Count; i++)
            {
                Thing t = items[i];
                if (!t.Spawned || !ship.Contains(t.Position) || t.def.category != ThingCategory.Item) continue;
                kg += t.GetStatValue(StatDefOf.Mass) * t.stackCount;
            }
            float tons = kg / 1000f;
            cache[ship.id] = (now, tons);
            return tons;
        }
    }
}
