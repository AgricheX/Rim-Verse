using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public class MapComponent_RimverseShips : MapComponent
    {
        public const int MeterInterval = 60;
        public const int RecalcInterval = 250;
        public const float TicksPerSecond = 60f;
        public const float SecondsPerHour = RimverseUnits.TicksPerHour / TicksPerSecond;
        public const float BaseHeatCapacity = 100f;
        public const float PlanetHeatDissipation = 5f;
        public const float HypoxiaAirThreshold = 0.2f;
        public const float BreachDrainPerSecond = 0.25f;
        public const float RoomEqualizePerSecond = 0.1f;

        private Dictionary<int, ShipState> states = new Dictionary<int, ShipState>();
        private readonly List<RimverseShip> ships = new List<RimverseShip>();
        private readonly Dictionary<IntVec3, RimverseShip> cellToShip = new Dictionary<IntVec3, RimverseShip>();
        private readonly Dictionary<Room, RimverseShip> roomToShip = new Dictionary<Room, RimverseShip>();
        private bool dirty = true;
        private int nextShipNumber = 1;

        public MapComponent_RimverseShips(Map map) : base(map)
        {
        }

        public IReadOnlyList<RimverseShip> Ships
        {
            get
            {
                if (dirty) Recalculate();
                return ships;
            }
        }

        public void Dirty() => dirty = true;

        public RimverseShip ShipById(int id)
        {
            List<RimverseShip> list = (List<RimverseShip>)Ships;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].id == id) return list[i];
            }
            return null;
        }

        public RimverseShip ShipAt(IntVec3 c)
        {
            if (dirty) Recalculate();
            return cellToShip.TryGetValue(c, out RimverseShip ship) ? ship : null;
        }

        public RimverseShip ShipOfRoom(Room room)
        {
            if (dirty) Recalculate();
            return room != null && roomToShip.TryGetValue(room, out RimverseShip ship) ? ship : null;
        }

        public void NotifyHullLost(int shipId, IntVec3 cell, float points)
        {
            if (!states.TryGetValue(shipId, out ShipState state)) return;
            state.lostHullPoints += points;
            if (!state.lostHullCells.Contains(cell)) state.lostHullCells.Add(cell);
            state.lastBreachTick = Find.TickManager.TicksGame;
        }

        public void NotifyHullRebuilt(IntVec3 cell, float points)
        {
            foreach (ShipState state in states.Values)
            {
                if (state.lostHullCells.Remove(cell))
                {
                    state.lostHullPoints = Mathf.Max(0f, state.lostHullPoints - points);
                    return;
                }
            }
        }

        public float RoomAir(Room room)
        {
            RimverseShip ship = ShipOfRoom(room);
            if (ship == null) return SpaceMapUtility.RimverseHandlesAir(map) ? 0f : 1f;
            if (!SpaceMapUtility.RimverseHandlesAir(map)) return 1f;
            return ship.state.roomAir.TryGetValue(room.ID, out float v) ? v : ship.AirFraction;
        }

        public static bool RoomSealed(Room room, RimverseShip ship)
        {
            if (room == null || room.TouchesMapEdge || !room.ProperRoom) return false;
            if (room.OpenRoofCount > 0) return false;
            foreach (IntVec3 c in room.Cells)
            {
                if (!ship.cells.Contains(c)) return false;
                if (HullUtility.IsVacuumTerrain(room.Map, c) && !HullUtility.IsHull(room.Map, c)) return false;
            }
            return true;
        }

        public void Recalculate()
        {
            dirty = false;
            ships.Clear();
            cellToShip.Clear();
            roomToShip.Clear();
            List<CompShipPart> allParts = new List<CompShipPart>();
            List<Thing> buildings = map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial);
            for (int i = 0; i < buildings.Count; i++)
            {
                CompShipPart part = buildings[i].TryGetComp<CompShipPart>();
                if (part != null)
                {
                    part.shipId = -1;
                    allParts.Add(part);
                }
            }
            IEnumerable<CompShipPart> bridges = allParts.Where(p => p.Role == ShipPartRole.Bridge).OrderBy(p => p.parent.thingIDNumber);
            HashSet<int> liveIds = new HashSet<int>();
            foreach (CompShipPart bridgePart in bridges)
            {
                Building bridge = (Building)bridgePart.parent;
                if (cellToShip.TryGetValue(bridge.Position, out RimverseShip owner))
                {
                    owner.multipleBridges = true;
                    bridgePart.shipId = owner.id;
                    continue;
                }
                RimverseShip ship = BuildShip(bridge, bridgePart);
                ships.Add(ship);
                liveIds.Add(ship.id);
            }
            foreach (int id in states.Keys.ToList())
            {
                if (!liveIds.Contains(id) && !ShipTravelUtility.IsStateReserved(id))
                {
                    states.Remove(id);
                }
            }
        }

        private RimverseShip BuildShip(Building bridge, CompShipPart bridgePart)
        {
            RimverseShip ship = new RimverseShip
            {
                id = bridge.thingIDNumber,
                map = map,
                bridge = bridge,
                cellLimit = bridgePart.Props.maxCells
            };
            if (!states.TryGetValue(ship.id, out ShipState state))
            {
                state = new ShipState
                {
                    shipId = ship.id,
                    name = "Rimverse_DefaultShipName".Translate(nextShipNumber++)
                };
                states[ship.id] = state;
            }
            ship.state = state;
            HashSet<CompShipPart> found = new HashSet<CompShipPart>();
            Queue<IntVec3> queue = new Queue<IntVec3>();
            foreach (IntVec3 c in bridge.OccupiedRect())
            {
                if (ship.cells.Add(c)) queue.Enqueue(c);
            }
            while (queue.Count > 0)
            {
                IntVec3 cur = queue.Dequeue();
                CollectParts(cur, ship, found);
                for (int i = 0; i < 4; i++)
                {
                    IntVec3 next = cur + GenAdj.CardinalDirections[i];
                    if (!next.InBounds(map) || ship.cells.Contains(next) || cellToShip.ContainsKey(next)) continue;
                    if (!IsShipCell(next)) continue;
                    if (ship.cells.Count >= ship.cellLimit)
                    {
                        ship.overLimit = true;
                        continue;
                    }
                    ship.cells.Add(next);
                    queue.Enqueue(next);
                }
            }
            foreach (IntVec3 c in ship.cells)
            {
                cellToShip[c] = ship;
            }
            ship.parts.AddRange(found);
            ComputeTotals(ship);
            return ship;
        }

        private bool IsShipCell(IntVec3 c)
        {
            if (HullUtility.IsHull(map, c)) return true;
            Building edifice = c.GetEdifice(map);
            return edifice != null && edifice.TryGetComp<CompShipPart>() != null;
        }

        private void CollectParts(IntVec3 c, RimverseShip ship, HashSet<CompShipPart> found)
        {
            List<Thing> things = map.thingGrid.ThingsListAtFast(c);
            for (int i = 0; i < things.Count; i++)
            {
                CompShipPart part = things[i].TryGetComp<CompShipPart>();
                if (part == null || !found.Add(part)) continue;
                part.shipId = ship.id;
                if (part.Role == ShipPartRole.Structure) ship.cellLimit += part.Props.extraCells;
                if (part.Role == ShipPartRole.Bridge && part.parent != ship.bridge) ship.multipleBridges = true;
            }
        }

        private void ComputeTotals(RimverseShip ship)
        {
            ShipState state = ship.state;
            ship.heatCapacity = BaseHeatCapacity;
            int maneuvers = 0;
            foreach (IntVec3 c in ship.cells)
            {
                HullTerrainExtension hull = HullUtility.HullAt(map, c);
                if (hull == null) continue;
                ship.mass += hull.mass;
                ship.hullMax += hull.hullPoints;
                ship.hullCurrent += hull.hullPoints;
            }
            for (int i = 0; i < ship.parts.Count; i++)
            {
                CompShipPart part = ship.parts[i];
                CompProperties_ShipPart p = part.Props;
                float eff = part.Efficiency;
                ship.mass += p.mass;
                ship.hullMax += p.hullPoints;
                ship.hullCurrent += part.CurrentHullPoints;
                ship.heatCapacity += p.heatCapacity;
                ship.airCapacity += p.airCapacity;
                ship.fuelCapacity += p.fuelCapacity;
                ship.warpCapacity += p.warpCapacity;
                ship.cargoTons += p.cargoTons;
                if (eff <= 0f) continue;
                if (p.role == ShipPartRole.Thruster) ship.thrust += p.thrust * eff;
                if (p.role == ShipPartRole.WarpDrive && part.Operating)
                {
                    if (p.warpRangeLy > ship.warpRangeLy) ship.warpRangeLy = p.warpRangeLy;
                    if (p.warpCooldownHours > ship.warpCooldownHours) ship.warpCooldownHours = p.warpCooldownHours;
                    ship.warpMaxMass += p.warpMaxMass;
                }
                if (p.role == ShipPartRole.Maneuver && maneuvers < 4)
                {
                    maneuvers++;
                    ship.dodgeBonus += p.dodgeBonus * eff;
                }
                if (p.role != ShipPartRole.Maneuver && p.dodgeBonus > 0f) ship.dodgeBonus += p.dodgeBonus * eff;
                if (p.heatPerSecond > 0f && (!p.heatOnlyWhenThrusting || state.thrusting)) ship.heatGeneration += p.heatPerSecond * eff;
                ship.heatRemoval += p.heatRemovalPerSecond * eff;
                ship.airGeneration += p.airPerHour * eff;
                ship.gravityCells += p.gravityCells;
                if (p.sensorSensitivity > 0f && (ship.sensorSensitivity <= 0f || p.sensorSensitivity < ship.sensorSensitivity)) ship.sensorSensitivity = p.sensorSensitivity;
            }
            if (state.warping) ship.heatGeneration *= WarpTravel.HeatFactor;
            HashSet<Room> seen = new HashSet<Room>();
            foreach (IntVec3 c in ship.cells)
            {
                Room room = c.GetRoom(map);
                if (room == null || !seen.Add(room)) continue;
                if (!RoomSealed(room, ship)) continue;
                ship.rooms.Add(room);
                roomToShip[room] = ship;
                ship.sealedCells += room.CellCount;
            }
            ship.airCapacity += ship.sealedCells;
            state.airStock = Mathf.Min(state.airStock, ship.airCapacity);
            ship.shipClass = ShipClassifier.Classify(ship);
        }

        public override void MapComponentTick()
        {
            int tick = Find.TickManager.TicksGame;
            if (dirty || tick % RecalcInterval == 0) Recalculate();
            if (tick % LifeAboard.IntervalTicks == 37) LifeAboard.Tick(map, this);
            if (tick % MeterInterval != 0) return;
            float seconds = MeterInterval / TicksPerSecond;
            bool handlesAir = SpaceMapUtility.RimverseHandlesAir(map);
            if (!handlesAir && tick % (MeterInterval * 4) == 0) ForeignVacuumCompat.Tick(map, seconds * 4f);
            if (ships.Count == 0)
            {
                if (handlesAir && tick % (MeterInterval * 4) == 0) ShipMeters.TickPawns(map, this, seconds * 4f);
                return;
            }
            bool space = SpaceMapUtility.IsSpace(map);
            for (int i = 0; i < ships.Count; i++)
            {
                RimverseShip ship = ships[i];
                ShipMeters.TickHeat(ship, seconds, space, tick);
                ShipMeters.TickAir(ship, seconds, handlesAir);
                ShipMeters.TickIntegrity(ship, seconds, tick);
            }
            if (handlesAir) ShipMeters.TickPawns(map, this, seconds);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref states, "states", LookMode.Value, LookMode.Deep);
            Scribe_Values.Look(ref nextShipNumber, "nextShipNumber", 1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (states == null) states = new Dictionary<int, ShipState>();
                dirty = true;
            }
        }

        public ShipState StateFor(int id) => states.TryGetValue(id, out ShipState s) ? s : null;

        public void AdoptState(int newId, ShipState state)
        {
            state.shipId = newId;
            state.roomAir.Clear();
            states[newId] = state;
            dirty = true;
        }
    }
}
