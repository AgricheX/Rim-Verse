using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace Rimverse
{
    public static class ShipMover
    {
        private struct CellData
        {
            public TerrainDef foundation;
            public TerrainDef top;
            public RoofDef roof;
        }

        public static CellRect Bounds(RimverseShip ship)
        {
            return CellRect.FromCellList(ship.cells.ToList());
        }

        public static bool AreaFree(Map map, CellRect rect, IntVec3 offset, HashSet<IntVec3> shipCells)
        {
            foreach (IntVec3 c in shipCells)
            {
                IntVec3 d = c + offset;
                if (!d.InBounds(map) || d.InNoBuildEdgeArea(map)) return false;
                if (d.GetEdifice(map) != null) return false;
                if (d.GetFirstPawn(map) != null) return false;
                if (SpaceMapUtility.IsRimverseSpace(map)) continue;
                TerrainDef t = d.GetTerrain(map);
                if (t.passability == Traversability.Impassable || t.IsWater) return false;
                if (map.roofGrid.RoofAt(d) != null && map.roofGrid.RoofAt(d).isThickRoof) return false;
            }
            return true;
        }

        public static bool TryFindLandingOffset(Map map, RimverseShip ship, out IntVec3 offset)
        {
            CellRect bounds = Bounds(ship);
            IntVec3 center = bounds.CenterCell;
            List<IntVec3> candidates = new List<IntVec3>();
            int radius = map.Size.x / 2;
            foreach (IntVec3 c in GenRadial.RadialCellsAround(map.Center, System.Math.Min(radius, 56), true))
            {
                candidates.Add(c);
            }
            foreach (IntVec3 target in candidates)
            {
                IntVec3 off = target - center;
                if (AreaFree(map, bounds, off, ship.cells))
                {
                    offset = off;
                    return true;
                }
            }
            offset = IntVec3.Invalid;
            return false;
        }

        public static void Move(RimverseShip ship, Map dest, IntVec3 offset)
        {
            Map source = ship.map;
            MapComponent_RimverseShips srcComp = source.GetComponent<MapComponent_RimverseShips>();
            MapComponent_RimverseShips dstComp = dest.GetComponent<MapComponent_RimverseShips>();
            ShipState state = ship.state;
            int id = ship.id;
            ShipTravelUtility.Reserve(id);
            List<IntVec3> cells = ship.cells.ToList();
            Dictionary<IntVec3, CellData> data = new Dictionary<IntVec3, CellData>();
            foreach (IntVec3 c in cells)
            {
                data[c] = new CellData
                {
                    foundation = source.terrainGrid.FoundationAt(c),
                    top = source.terrainGrid.TopTerrainAt(c),
                    roof = source.roofGrid.RoofAt(c)
                };
            }
            HashSet<Thing> things = new HashSet<Thing>();
            foreach (IntVec3 c in cells)
            {
                foreach (Thing t in c.GetThingList(source))
                {
                    if (t is Plant || t is Filth || t is Mote) continue;
                    things.Add(t);
                }
            }
            List<(Thing thing, IntVec3 pos, Rot4 rot)> moving = new List<(Thing, IntVec3, Rot4)>();
            foreach (Thing t in things.OrderBy(t => t is Pawn ? 2 : t.def.category == ThingCategory.Building ? 0 : 1))
            {
                moving.Add((t, t.Position, t.Rotation));
            }
            foreach ((Thing thing, IntVec3 pos, Rot4 rot) in moving)
            {
                if (thing is Pawn pawn)
                {
                    pawn.jobs?.StopAll();
                    pawn.pather?.StopDead();
                }
                if (thing.Spawned) thing.DeSpawn(DestroyMode.WillReplace);
            }
            foreach (IntVec3 c in cells)
            {
                CellData cd = data[c];
                if (cd.foundation != null) source.terrainGrid.RemoveFoundation(c, false);
                if (cd.top != null && cd.top.layerable && source.terrainGrid.CanRemoveTopLayerAt(c)) source.terrainGrid.RemoveTopLayer(c, false);
                source.roofGrid.SetRoof(c, null);
            }
            foreach (IntVec3 c in cells)
            {
                IntVec3 d = c + offset;
                CellData cd = data[c];
                if (cd.foundation != null) dest.terrainGrid.SetFoundation(d, cd.foundation);
                if (cd.top != null && cd.top.layerable) dest.terrainGrid.SetTerrain(d, cd.top);
                else if (cd.foundation == null && cd.top != null && cd.top.HasModExtension<HullTerrainExtension>()) dest.terrainGrid.SetTerrain(d, cd.top);
                foreach (Thing t in d.GetThingList(dest).ToList())
                {
                    if (t is Plant || t is Filth || t.def.category == ThingCategory.Item) t.Destroy(DestroyMode.Vanish);
                }
            }
            foreach ((Thing thing, IntVec3 pos, Rot4 rot) in moving)
            {
                if (thing.Destroyed) continue;
                GenSpawn.Spawn(thing, pos + offset, dest, rot, WipeMode.VanishOrMoveAside);
            }
            foreach (IntVec3 c in cells)
            {
                RoofDef roof = data[c].roof;
                if (roof != null) dest.roofGrid.SetRoof(c + offset, roof);
            }
            dstComp.AdoptState(id, state);
            ShipTravelUtility.Release(id);
            srcComp.Dirty();
            dstComp.Dirty();
        }
    }
}
