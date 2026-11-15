using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public static class ShipMeters
    {
        public const float ShutdownThreshold = 0.8f;
        public const float RestoreThreshold = 0.6f;
        public const int ShutdownIntervalTicks = 600;
        public const int FireIntervalTicks = 600;
        public const int BreachCheckIntervalTicks = 1800;
        public const float HypoxiaPerSecondLowAir = 0.006f;
        public const float HypoxiaPerSecondVacuum = 0.03f;
        public const float ToxicPerSecond = 0.0015f;

        private static readonly List<Pawn> tmpPawns = new List<Pawn>();

        public static void TickHeat(RimverseShip ship, float seconds, bool space, int tick)
        {
            ShipState s = ship.state;
            float removal = ship.heatRemoval + (space ? 0f : MapComponent_RimverseShips.PlanetHeatDissipation);
            s.heat = Mathf.Clamp(s.heat + (ship.heatGeneration - removal) * seconds, 0f, ship.heatCapacity * 1.05f);
            float frac = ship.HeatFraction;
            if (frac >= ShutdownThreshold && tick - s.lastShutdownTick >= ShutdownIntervalTicks)
            {
                CompShipPart victim = null;
                foreach (CompShipPart part in ship.parts)
                {
                    if (part.heatShutdown || part.Props.lifeSupport || part.Props.essential) continue;
                    if (part.Role == ShipPartRole.HullWall || part.Role == ShipPartRole.Window || part.Role == ShipPartRole.Radiator) continue;
                    if (!part.Operating) continue;
                    if (victim == null || part.Props.heatPerSecond > victim.Props.heatPerSecond) victim = part;
                }
                if (victim != null)
                {
                    victim.heatShutdown = true;
                    s.lastShutdownTick = tick;
                    Messages.Message("Rimverse_MsgHeatShutdown".Translate(victim.parent.LabelShort, ship.Label), victim.parent, MessageTypeDefOf.NegativeEvent, false);
                    ship.map.GetComponent<MapComponent_RimverseShips>().Dirty();
                }
            }
            else if (frac < RestoreThreshold && tick - s.lastShutdownTick >= ShutdownIntervalTicks)
            {
                foreach (CompShipPart part in ship.parts)
                {
                    if (!part.heatShutdown) continue;
                    part.heatShutdown = false;
                    s.lastShutdownTick = tick;
                    ship.map.GetComponent<MapComponent_RimverseShips>().Dirty();
                    break;
                }
            }
            if (frac >= 1f && tick - s.lastFireTick >= FireIntervalTicks && ship.parts.Count > 0)
            {
                s.lastFireTick = tick;
                CompShipPart part = ship.parts.RandomElement();
                if (FireUtility.TryStartFireIn(part.parent.Position, ship.map, 0.5f, null))
                {
                    Messages.Message("Rimverse_MsgHeatFire".Translate(ship.Label), part.parent, MessageTypeDefOf.ThreatSmall, false);
                }
            }
        }

        public static void TickAir(RimverseShip ship, float seconds, bool handlesAir)
        {
            ShipState s = ship.state;
            if (!handlesAir)
            {
                s.airStock = ship.airCapacity;
                s.roomAir.Clear();
                return;
            }
            float perSecondFactor = seconds / MapComponent_RimverseShips.SecondsPerHour;
            float consumption = CrewConsumptionPerHour(ship) * RimverseMod.Settings.airConsumptionMultiplier;
            s.airStock += (ship.airGeneration - consumption) * perSecondFactor;
            float breachDrain = MapComponent_RimverseShips.BreachDrainPerSecond * RimverseMod.Settings.breachDrainMultiplier * seconds;
            HashSet<int> live = new HashSet<int>();
            foreach (Room room in ship.rooms)
            {
                live.Add(room.ID);
                if (!s.roomAir.TryGetValue(room.ID, out float cur)) cur = ship.AirFraction;
                float target = ship.AirFraction;
                float step = MapComponent_RimverseShips.RoomEqualizePerSecond * seconds;
                cur = cur < target ? Mathf.Min(target, cur + step) : Mathf.Max(target, cur - step);
                s.roomAir[room.ID] = cur;
            }
            List<int> stale = null;
            foreach (KeyValuePair<int, float> pair in s.roomAir)
            {
                if (!live.Contains(pair.Key))
                {
                    if (stale == null) stale = new List<int>();
                    stale.Add(pair.Key);
                }
            }
            if (stale != null)
            {
                foreach (int id in stale)
                {
                    float lost = s.roomAir[id];
                    s.roomAir.Remove(id);
                    s.airStock -= lost * breachDrain * 40f;
                }
            }
            s.airStock = Mathf.Clamp(s.airStock, 0f, ship.airCapacity);
        }

        public static float CrewConsumptionPerHour(RimverseShip ship)
        {
            float total = 0f;
            int count = 0;
            IReadOnlyList<Pawn> pawns = ship.map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn p = pawns[i];
                if (!ship.cells.Contains(p.Position) || !SpaceMapUtility.Breathes(p)) continue;
                count++;
                total += (p.Awake() ? 1f : 0.6f) * GeneSpace.Factor(p, e => e.airFactor);
            }
            ship.crewCount = count;
            return total;
        }

        public static void TickIntegrity(RimverseShip ship, float seconds, int tick)
        {
            float integrity = ship.Integrity;
            if (integrity >= 0.5f || tick % BreachCheckIntervalTicks != 0) return;
            float chance = (0.5f - integrity) * 0.5f;
            if (!Rand.Chance(chance)) return;
            List<CompShipPart> walls = new List<CompShipPart>();
            foreach (CompShipPart part in ship.PartsWithRole(ShipPartRole.HullWall)) walls.Add(part);
            if (walls.Count == 0) return;
            Thing wall = walls.RandomElement().parent;
            Messages.Message("Rimverse_MsgRandomBreach".Translate(ship.Label), new TargetInfo(wall.Position, ship.map), MessageTypeDefOf.NegativeEvent, false);
            wall.TakeDamage(new DamageInfo(DamageDefOf.Blunt, wall.HitPoints + 1));
        }

        public static void TickPawns(Map map, MapComponent_RimverseShips comp, float seconds)
        {
            tmpPawns.Clear();
            tmpPawns.AddRange(map.mapPawns.AllPawnsSpawned);
            bool vacuum = SpaceMapUtility.IsSpace(map);
            bool toxic = !vacuum && SpaceMapUtility.OutsideAtmosphere(map) == AtmosphereType.Toxic;
            for (int i = 0; i < tmpPawns.Count; i++)
            {
                Pawn pawn = tmpPawns[i];
                if (!SpaceMapUtility.Breathes(pawn)) continue;
                Room room = pawn.GetRoom();
                RimverseShip ship = comp.ShipOfRoom(room);
                float rate;
                if (ship == null)
                {
                    rate = vacuum ? HypoxiaPerSecondVacuum : HypoxiaPerSecondLowAir;
                }
                else
                {
                    float air = comp.RoomAir(room);
                    if (air >= MapComponent_RimverseShips.HypoxiaAirThreshold)
                    {
                        if (air >= 0.5f) VacuumSuitUtility.Refill(pawn, seconds, ship);
                        continue;
                    }
                    rate = Mathf.Lerp(HypoxiaPerSecondVacuum, HypoxiaPerSecondLowAir, air / MapComponent_RimverseShips.HypoxiaAirThreshold);
                }
                if (SpaceMapUtility.VacuumProtection(pawn) >= 1f) continue;
                if (VacuumSuitUtility.TryBreatheFromSuit(pawn, seconds)) continue;
                HealthUtility.AdjustSeverity(pawn, RimverseDefOf.Rimverse_Hypoxia, rate * seconds * GeneSpace.Factor(pawn, e => e.hypoxiaFactor));
                if (toxic && ship == null)
                {
                    float resist = Mathf.Max(pawn.GetStatValue(StatDefOf.ToxicResistance), pawn.GetStatValue(StatDefOf.ToxicEnvironmentResistance));
                    if (resist < 1f) HealthUtility.AdjustSeverity(pawn, HediffDefOf.ToxicBuildup, ToxicPerSecond * (1f - resist) * seconds);
                }
            }
            tmpPawns.Clear();
        }
    }
}
