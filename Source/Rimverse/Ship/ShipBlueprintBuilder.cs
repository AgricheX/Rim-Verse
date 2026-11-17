using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public class BlueprintBuildResult
    {
        public Building bridge;
        public CellRect rect;
        public List<IntVec3> freeCells = new List<IntVec3>();
        public List<IntVec3> interior = new List<IntVec3>();
        public List<Thing> things = new List<Thing>();
        public Dictionary<RoomRole, List<IntVec3>> roomCells = new Dictionary<RoomRole, List<IntVec3>>();

        public IntVec3 RandomCellIn(RoomRole role, IntVec3 fallback)
        {
            if (roomCells.TryGetValue(role, out List<IntVec3> cells) && cells.Count > 0) return cells.RandomElement();
            return freeCells.Count > 0 ? freeCells.RandomElement() : fallback;
        }

        public List<IntVec3> RandomCellsIn(RoomRole role, int count)
        {
            List<IntVec3> pool = roomCells.TryGetValue(role, out List<IntVec3> cells) && cells.Count > 0 ? cells : freeCells;
            return pool.InRandomOrder().Take(count).ToList();
        }
    }

    public class BlueprintBuildOptions
    {
        public Faction faction;
        public float fuel = 1f;
        public float hull = 1f;
        public bool powerOn = true;
        public bool vacuumFloor = true;
        public bool clearArea = true;
        public ThingDef wall;
        public ThingDef stuff;
    }

    public static class ShipBlueprintBuilder
    {
        public static CellRect RectAt(IntVec3 center, ShipBlueprint bp) => new CellRect(center.x - bp.width / 2, center.z - bp.height / 2, bp.width, bp.height);

        public static bool Fits(Map map, CellRect rect)
        {
            foreach (IntVec3 c in rect)
            {
                if (!c.InBounds(map) || c.InNoBuildEdgeArea(map)) return false;
            }
            return true;
        }

        public static BlueprintBuildResult Build(Map map, CellRect rect, ShipBlueprint bp, BlueprintBuildOptions opt)
        {
            BlueprintBuildResult res = new BlueprintBuildResult { rect = rect };
            Faction faction = opt.faction;
            TerrainDef hullFloor = DefDatabase<TerrainDef>.GetNamedSilentFail(opt.vacuumFloor ? "Rimverse_HullFloorVacuum" : "Rimverse_HullFloor") ?? DefDatabase<TerrainDef>.GetNamedSilentFail("Rimverse_HullFloorVacuum");
            ThingDef wall = opt.wall ?? ShipBlueprintGenerator.Named(bp.request.armored ? "Rimverse_HullWallArmored" : "Rimverse_HullWall") ?? ShipBlueprintGenerator.Named("Rimverse_HullWall");
            ThingDef window = ShipBlueprintGenerator.Named("Rimverse_HullViewport");
            ThingDef door = ThingDefOf.Door;
            ThingDef airlock = ShipBlueprintGenerator.Named("Rimverse_Airlock") ?? door;
            ThingDef conduit = ShipBlueprintGenerator.Named("HiddenConduit") ?? ThingDefOf.PowerConduit;
            TerrainDef corridorFloor = DefDatabase<TerrainDef>.GetNamedSilentFail("Rimverse_DeckPlating");
            if (hullFloor == null || wall == null) return null;
            IntVec3 o = new IntVec3(rect.minX, 0, rect.minZ);
            if (opt.clearArea)
            {
                foreach (IntVec3 lc in bp.AllCells)
                {
                    if (bp.At(lc) == BlueprintCell.Empty) continue;
                    IntVec3 c = o + lc;
                    if (!c.InBounds(map)) continue;
                    foreach (Thing t in c.GetThingList(map).ToList())
                    {
                        if (!(t is Pawn) && t.def.destroyable) t.Destroy(DestroyMode.Vanish);
                    }
                }
            }
            MarkWindows(bp);
            foreach (IntVec3 lc in bp.AllCells)
            {
                BlueprintCell k = bp.At(lc);
                if (k == BlueprintCell.Empty) continue;
                IntVec3 c = o + lc;
                if (!c.InBounds(map)) continue;
                if (bp.holes.Contains(lc) && (k == BlueprintCell.Wall || k == BlueprintCell.Exterior || Rand.Chance(0.5f))) continue;
                map.terrainGrid.SetFoundation(c, hullFloor);
                if (k == BlueprintCell.Floor && !bp.holes.Contains(lc))
                {
                    BlueprintRoom room = bp.RoomAt(lc);
                    TerrainDef top = room != null ? ShipBlueprintGenerator.FloorFor(room.role, bp.request.style) : corridorFloor;
                    if (top != null) map.terrainGrid.SetTerrain(c, top);
                }
            }
            foreach (IntVec3 lc in bp.AllCells)
            {
                BlueprintCell k = bp.At(lc);
                IntVec3 c = o + lc;
                if (!c.InBounds(map) || bp.holes.Contains(lc)) continue;
                if (k == BlueprintCell.Wall) res.things.Add(Spawn(map, wall, c, faction, Rot4.North, opt.stuff));
                else if (k == BlueprintCell.Window) res.things.Add(Spawn(map, window ?? wall, c, faction, Rot4.North, opt.stuff));
                else if (k == BlueprintCell.Door) res.things.Add(Spawn(map, lc == bp.entrance ? airlock : door, c, faction, DoorRot(bp, lc), opt.stuff));
                if (k == BlueprintCell.Floor || k == BlueprintCell.Door)
                {
                    map.roofGrid.SetRoof(c, RoofDefOf.RoofConstructed);
                    res.interior.Add(c);
                }
                else if (k == BlueprintCell.Wall || k == BlueprintCell.Window) map.roofGrid.SetRoof(c, RoofDefOf.RoofConstructed);
            }
            foreach (BlueprintPlacement p in bp.placements)
            {
                IntVec3 c = o + p.center;
                if (!GenAdj.OccupiedRect(c, p.rot, p.def.size).InBounds(map)) continue;
                Thing t = Spawn(map, p.def, c, p.def.CanHaveFaction ? faction : null, p.rot, p.stuff);
                if (t == null) continue;
                res.things.Add(t);
                Prepare(t, opt);
                if (res.bridge == null && t is Building b && p.def.GetCompProperties<CompProperties_ShipPart>()?.role == ShipPartRole.Bridge) res.bridge = b;
            }
            foreach (IntVec3 lc in bp.AllCells)
            {
                BlueprintCell k = bp.At(lc);
                if (k != BlueprintCell.Floor && k != BlueprintCell.Exterior) continue;
                if (bp.occupied.Contains(lc) || bp.holes.Contains(lc)) continue;
                IntVec3 c = o + lc;
                if (!c.InBounds(map)) continue;
                if (conduit != null && opt.powerOn) Spawn(map, conduit, c, faction, Rot4.North, null);
                if (k == BlueprintCell.Floor && c.Standable(map))
                {
                    res.freeCells.Add(c);
                    BlueprintRoom room = bp.RoomAt(lc);
                    RoomRole role = room?.role ?? RoomRole.Corridor;
                    if (!res.roomCells.TryGetValue(role, out List<IntVec3> list)) res.roomCells[role] = list = new List<IntVec3>();
                    list.Add(c);
                }
            }
            if (opt.hull < 1f)
            {
                foreach (Thing t in res.things)
                {
                    if (t != null && !t.Destroyed && t.def.useHitPoints) t.HitPoints = Mathf.Max(1, Mathf.RoundToInt(t.MaxHitPoints * Mathf.Clamp(opt.hull * Rand.Range(0.7f, 1.2f), 0.05f, 1f)));
                }
            }
            map.GetComponent<MapComponent_RimverseShips>()?.Dirty();
            return res;
        }

        private static void MarkWindows(ShipBlueprint bp)
        {
            if (ShipBlueprintGenerator.Named("Rimverse_HullViewport") == null) return;
            System.Random r = new System.Random(bp.request.seed ^ 0x3c6ef372);
            foreach (BlueprintRoom room in bp.rooms)
            {
                if (room.role != RoomRole.Bridge && room.role != RoomRole.Mess && room.role != RoomRole.Quarters && room.role != RoomRole.Hydroponics) continue;
                int made = 0;
                foreach (IntVec3 c in room.cells)
                {
                    foreach (IntVec3 d in GenAdj.CardinalDirections)
                    {
                        IntVec3 w = c + d;
                        if (bp.At(w) != BlueprintCell.Wall || bp.At(w + d) != BlueprintCell.Empty) continue;
                        if (room.role != RoomRole.Bridge && r.NextDouble() > 0.35) continue;
                        if (made > (room.role == RoomRole.Bridge ? 8 : 3)) continue;
                        bp.cells[w.x, w.z] = BlueprintCell.Window;
                        made++;
                    }
                }
            }
        }

        private static Rot4 DoorRot(ShipBlueprint bp, IntVec3 lc)
        {
            bool ew = bp.At(lc + IntVec3.East) == BlueprintCell.Wall || bp.At(lc + IntVec3.West) == BlueprintCell.Wall;
            return ew ? Rot4.North : Rot4.East;
        }

        public static Thing Spawn(Map map, ThingDef def, IntVec3 c, Faction faction, Rot4 rot, ThingDef stuff)
        {
            if (def == null) return null;
            ThingDef s = null;
            if (def.MadeFromStuff)
            {
                s = stuff ?? ThingDefOf.Steel;
                if (!GenStuff.AllowedStuffsFor(def).Contains(s)) s = GenStuff.DefaultStuffFor(def);
            }
            Thing t = ThingMaker.MakeThing(def, s);
            if (faction != null && def.CanHaveFaction) t.SetFaction(faction);
            return GenSpawn.Spawn(t, c, map, rot, WipeMode.Vanish);
        }

        private static void Prepare(Thing t, BlueprintBuildOptions opt)
        {
            CompRefuelable refuel = t.TryGetComp<CompRefuelable>();
            if (refuel != null)
            {
                float want = refuel.Props.fuelCapacity * Mathf.Clamp01(opt.fuel) - refuel.Fuel;
                if (want > 0f) refuel.Refuel(want);
            }
            CompShipShield shield = t.TryGetComp<CompShipShield>();
            if (shield != null) shield.hitPoints = shield.Props.maxHitPoints;
            CompFlickable flick = t.TryGetComp<CompFlickable>();
            if (flick != null) flick.SwitchIsOn = opt.powerOn;
        }

        public static void Clear(Map map, CellRect rect)
        {
            foreach (IntVec3 c in rect)
            {
                if (!c.InBounds(map)) continue;
                foreach (Thing t in c.GetThingList(map).ToList())
                {
                    if (t.def.destroyable && !(t is Pawn)) t.Destroy(DestroyMode.Vanish);
                }
                map.roofGrid.SetRoof(c, null);
                if (map.terrainGrid.FoundationAt(c) != null) map.terrainGrid.RemoveFoundation(c, false);
            }
            map.GetComponent<MapComponent_RimverseShips>()?.Dirty();
        }
    }
}
