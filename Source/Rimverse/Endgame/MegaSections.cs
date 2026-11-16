using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Rimverse
{
    public static class MegaSectionContext
    {
        public static MegastructureData mega;
        public static int section;
    }

    public enum SectionBlock
    {
        Scaffold,
        Residential,
        Market,
        Park,
        Hydroponics,
        Mess,
        Medical,
        Industrial,
        Storage,
        Control,
        Sensors,
        Barracks
    }

    public class MapComponent_MegaSection : MapComponent
    {
        public int megaId = -1;
        public int section;
        public int bandMinZ;
        public int bandMaxZ;
        public int genBuildings;

        public MapComponent_MegaSection(Map map) : base(map)
        {
        }

        public bool IsSection => megaId >= 0;

        public MegastructureData Mega => IsSection ? GameComponent_Megastructures.Instance?.ById(megaId) : null;

        public static MapComponent_MegaSection For(Map map) => map?.GetComponent<MapComponent_MegaSection>();

        public override void MapComponentTick()
        {
            if (!IsSection) return;
            int tick = Find.TickManager.TicksGame;
            if (tick % 30 == 17) CheckEdges();
            if (tick % 250 == 31) Climate();
        }

        private void CheckEdges()
        {
            MegastructureData m = Mega;
            if (m == null) return;
            foreach (Pawn p in map.mapPawns.FreeColonistsSpawned.ToList())
            {
                if (p.Downed || !p.Spawned) continue;
                IntVec3 c = p.Position;
                if (c.z <= bandMinZ || c.z >= bandMaxZ) continue;
                int dir = c.x <= 0 ? -1 : c.x >= map.Size.x - 1 ? 1 : 0;
                if (dir == 0) continue;
                if (!p.Drafted && p.CurJobDef != JobDefOf.Goto) continue;
                Transit(p, m, dir);
            }
        }

        private void Transit(Pawn p, MegastructureData m, int dir)
        {
            int count = m.SectionCount;
            int target = ((section + dir) % count + count) % count;
            Map dest = MegaSectionUtility.GetOrCreate(m, target, map);
            if (dest == null) return;
            IntVec3 want = new IntVec3(dir > 0 ? 1 : dest.Size.x - 2, 0, p.Position.z);
            if (!CellFinder.TryFindRandomCellNear(want, dest, 5, x => x.Standable(dest) && x.x > 0 && x.x < dest.Size.x - 1 && !x.Fogged(dest), out IntVec3 cell)) return;
            bool viewing = Find.CurrentMap == map && Find.Selector.IsSelected(p);
            bool drafted = p.Drafted;
            p.DeSpawn(DestroyMode.Vanish);
            GenSpawn.Spawn(p, cell, dest);
            if (drafted && p.drafter != null) p.drafter.Drafted = true;
            if (viewing) CameraJumper.TryJumpAndSelect(p);
            Messages.Message("Rimverse_MsgMegaSection".Translate(p.LabelShort, m.KindLabel, target + 1, count), p, MessageTypeDefOf.NeutralEvent, false);
            MegaSectionUtility.CleanupIfEmpty(map);
        }

        private void Climate()
        {
            List<Room> rooms = map.regionGrid.AllRooms.ToList();
            MapComponent_RimverseShips ships = map.GetComponent<MapComponent_RimverseShips>();
            foreach (Room r in rooms)
            {
                if (r == null || r.UsesOutdoorTemperature) continue;
                if (ships?.ShipOfRoom(r) != null) continue;
                if (!MegaSectionUtility.Pressurized(map, r)) continue;
                r.Temperature = Mathf.Lerp(r.Temperature, 21f, 0.5f);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref megaId, "megaId", -1);
            Scribe_Values.Look(ref section, "section");
            Scribe_Values.Look(ref bandMinZ, "bandMinZ");
            Scribe_Values.Look(ref bandMaxZ, "bandMaxZ");
            Scribe_Values.Look(ref genBuildings, "genBuildings");
        }
    }

    public static class MegaSectionUtility
    {
        public const int Width = 180;
        public const int Height = 200;

        private static MapGeneratorDef def;

        public static MapGeneratorDef Def => def ?? (def = DefDatabase<MapGeneratorDef>.GetNamed("Rimverse_MegaSectionMap"));

        public static Map Find(MegastructureData m, int section)
        {
            foreach (Map map in Verse.Find.Maps)
            {
                MapComponent_MegaSection s = MapComponent_MegaSection.For(map);
                if (s != null && s.megaId == m.id && s.section == section) return map;
            }
            return null;
        }

        public static Map GetOrCreate(MegastructureData m, int section, Map from)
        {
            Map existing = Find(m, section);
            if (existing != null) return existing;
            MegaSectionContext.mega = m;
            MegaSectionContext.section = section;
            Map map;
            try
            {
                map = PocketMapUtility.GeneratePocketMap(new IntVec3(Width, 1, Height), Def, null, BodyMapUtility.AnchorMap(from));
            }
            finally
            {
                MegaSectionContext.mega = null;
            }
            if (map == null) return null;
            MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(map);
            loc.SetLocation(m.System, m.body);
            loc.SetSurface(false);
            return map;
        }

        public static bool Pressurized(Map map, Room room)
        {
            if (room == null || room.TouchesMapEdge || room.OpenRoofCount > 0) return false;
            MapComponent_MegaSection s = MapComponent_MegaSection.For(map);
            if (s == null || !s.IsSection) return false;
            foreach (IntVec3 c in room.Cells)
            {
                if (HullUtility.IsVacuumTerrain(map, c)) return false;
            }
            return true;
        }

        public static bool IsSection(Map map) => MapComponent_MegaSection.For(map)?.IsSection == true;

        public static void CleanupIfEmpty(Map map)
        {
            MapComponent_MegaSection s = MapComponent_MegaSection.For(map);
            if (s == null || !s.IsSection || !(map.Parent is PocketMapParent)) return;
            if (map.mapPawns.AnyPawnBlockingMapRemoval) return;
            if (map.mapPawns.FreeColonistsSpawnedCount > 0) return;
            MapComponent_RimverseShips ships = map.GetComponent<MapComponent_RimverseShips>();
            if (ships != null && ships.Ships.Any(x => x.bridge != null && x.bridge.Faction == Faction.OfPlayer)) return;
            if (map.listerBuildings.allBuildingsColonist.Count > s.genBuildings) return;
            if (Verse.Find.CurrentMap == map) return;
            PocketMapUtility.DestroyPocketMap(map);
        }

        public static IEnumerable<Gizmo> Gizmos(RimverseShip ship)
        {
            Map map = ship.map;
            MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(map);
            GameComponent_Megastructures gc = GameComponent_Megastructures.Instance;
            if (loc == null || loc.Traveling || gc == null || !SpaceMapUtility.IsOrbit(map) || loc.systemKey.NullOrEmpty()) yield break;
            if (IsSection(map))
            {
                yield return new Command_Action
                {
                    defaultLabel = "Rimverse_CmdMegaUndock".Translate(),
                    defaultDesc = "Rimverse_CmdMegaUndockDesc".Translate(),
                    icon = RimverseTex.Launch,
                    action = () => Undock(ship)
                };
                yield break;
            }
            List<MegastructureData> here = gc.At(loc.systemKey, loc.bodyIndex).Where(m => m.Visitable).ToList();
            if (here.Count == 0) yield break;
            yield return new Command_Action
            {
                defaultLabel = "Rimverse_CmdMegaDock".Translate(),
                defaultDesc = "Rimverse_CmdMegaDockDesc".Translate(),
                icon = RimverseTex.Board,
                action = () =>
                {
                    List<FloatMenuOption> opts = new List<FloatMenuOption>();
                    foreach (MegastructureData m in here)
                    {
                        MegastructureData mm = m;
                        opts.Add(new FloatMenuOption(mm.Label + " (" + mm.OwnerLabel + ", " + mm.StageLabel + ")", () => Dock(ship, mm)));
                    }
                    Verse.Find.WindowStack.Add(new FloatMenu(opts));
                }
            };
        }

        public static int DockSection(MegastructureData m) => Mathf.Abs(m.seed) % m.SectionCount;

        public static void Dock(RimverseShip ship, MegastructureData m)
        {
            Map source = ship.map;
            Map target = GetOrCreate(m, DockSection(m), source);
            if (target == null || !TryDockOffset(target, ship, out IntVec3 offset))
            {
                Messages.Message("Rimverse_NoLandingSpot".Translate(), ship.bridge, MessageTypeDefOf.RejectInput, false);
                return;
            }
            Building bridge = ship.bridge;
            ShipMover.Move(ship, target, offset);
            RimverseShip moved = target.GetComponent<MapComponent_RimverseShips>().ShipById(bridge.thingIDNumber);
            if (moved != null) Gangway(target, moved);
            BodyMapUtility.CleanupIfEmpty(source);
            Messages.Message("Rimverse_MsgMegaDocked".Translate(m.Label), bridge, MessageTypeDefOf.PositiveEvent, false);
            CameraJumper.TryJump(bridge);
        }

        public static bool TryDockOffset(Map map, RimverseShip ship, out IntVec3 offset)
        {
            MapComponent_MegaSection s = MapComponent_MegaSection.For(map);
            CellRect b = ShipMover.Bounds(ship);
            int baseZ = s.bandMaxZ + 3;
            for (int dz = 0; dz < 40; dz += 2)
            {
                for (int i = 0; i < map.Size.x; i++)
                {
                    int dx = (i % 2 == 0 ? 1 : -1) * (i / 2);
                    IntVec3 off = new IntVec3(map.Size.x / 2 + dx - b.CenterCell.x, 0, baseZ + dz - b.minZ);
                    if (Clear(map, ship.cells, off))
                    {
                        offset = off;
                        return true;
                    }
                }
            }
            offset = IntVec3.Invalid;
            return false;
        }

        private static bool Clear(Map map, HashSet<IntVec3> cells, IntVec3 off)
        {
            foreach (IntVec3 c in cells)
            {
                IntVec3 d = c + off;
                if (!d.InBounds(map) || d.InNoBuildEdgeArea(map)) return false;
                if (map.terrainGrid.FoundationAt(d) != null || d.GetEdifice(map) != null || d.GetFirstPawn(map) != null) return false;
                if (map.terrainGrid.FoundationAt(d + IntVec3.South) != null && !cells.Contains(c + IntVec3.South)) return false;
            }
            return true;
        }

        public static void Gangway(Map map, RimverseShip ship)
        {
            MapComponent_MegaSection s = MapComponent_MegaSection.For(map);
            if (s == null) return;
            TerrainDef f = DefDatabase<TerrainDef>.GetNamedSilentFail("Rimverse_MegaFoundation");
            if (f == null) return;
            Building_Door exit = ship.cells.Select(c => c.GetEdifice(map) as Building_Door).Where(d => d != null).OrderBy(d => d.Position.z).FirstOrDefault();
            IntVec3 start = exit != null ? exit.Position : ship.cells.OrderBy(c => c.z).ThenBy(c => Mathf.Abs(c.x - map.Size.x / 2)).First();
            List<IntVec3> locks = new List<IntVec3>();
            for (int x = 1; x < map.Size.x - 1; x++)
            {
                IntVec3 c = new IntVec3(x, 0, s.bandMaxZ);
                if (c.GetEdifice(map) is Building_Door) locks.Add(c);
            }
            IntVec3 target = locks.Count > 0 ? locks.OrderBy(c => Mathf.Abs(c.x - start.x)).First() : new IntVec3(start.x, 0, s.bandMaxZ);
            int row = s.bandMaxZ + 1;
            IntVec3 cur = start;
            while (cur.z > row)
            {
                cur += IntVec3.South;
                if (!ship.cells.Contains(cur) && map.terrainGrid.FoundationAt(cur) == null) map.terrainGrid.SetFoundation(cur, f);
            }
            int step = target.x >= cur.x ? 1 : -1;
            while (cur.x != target.x)
            {
                cur += new IntVec3(step, 0, 0);
                if (!ship.cells.Contains(cur) && map.terrainGrid.FoundationAt(cur) == null) map.terrainGrid.SetFoundation(cur, f);
                IntVec3 up = cur + IntVec3.North;
                if (!ship.cells.Contains(up) && up.z < row + 2 && map.terrainGrid.FoundationAt(up) == null) map.terrainGrid.SetFoundation(up, f);
            }
            if (!(target.GetEdifice(map) is Building_Door))
            {
                target.GetEdifice(map)?.Destroy(DestroyMode.Vanish);
                ShipBlueprintBuilder.Spawn(map, DefDatabase<ThingDef>.GetNamedSilentFail("Rimverse_Airlock") ?? ThingDefOf.Door, target, null, Rot4.North, null);
            }
        }

        public static void Undock(RimverseShip ship)
        {
            Map source = ship.map;
            Map target = BodyMapUtility.CreateOrbitPocket(source, ShipLaunchUtility.OrbitMapSize);
            if (target == null || !ShipMover.TryFindLandingOffset(target, ship, out IntVec3 offset))
            {
                Messages.Message("Rimverse_NoLandingSpot".Translate(), ship.bridge, MessageTypeDefOf.RejectInput, false);
                return;
            }
            Building bridge = ship.bridge;
            ShipMover.Move(ship, target, offset);
            CleanupIfEmpty(source);
            CameraJumper.TryJump(bridge);
        }
    }

    public class GenStep_MegaSection : GenStep
    {
        public override int SeedPart => 518447201;

        private Map map;
        private MegastructureData mega;
        private int section;
        private Faction faction;
        private TerrainDef foundation;
        private ThingDef wall;
        private ThingDef window;
        private ThingDef door;
        private ThingDef airlock;
        private ThingDef light;
        private System.Random rand;
        private bool ruin;
        private float built;

        private static ThingDef Named(string n) => DefDatabase<ThingDef>.GetNamedSilentFail(n);

        private static TerrainDef T(string n) => DefDatabase<TerrainDef>.GetNamedSilentFail(n);

        public override void Generate(Map map, GenStepParams parms)
        {
            mega = MegaSectionContext.mega;
            if (mega == null) return;
            this.map = map;
            section = MegaSectionContext.section;
            faction = mega.player ? Faction.OfPlayer : null;
            ruin = mega.ruin;
            built = mega.Complete ? 1f : Mathf.Clamp(mega.Completion * 1.15f, 0.2f, 1f);
            rand = new System.Random(mega.seed * 31 + section * 7919);
            foundation = T("Rimverse_MegaFoundation") ?? T("Rimverse_HullFloor");
            wall = Named(mega.kind == MegastructureKind.Citadel ? "Rimverse_HullWallArmored" : "Rimverse_HullWall") ?? Named("Rimverse_HullWall");
            window = Named("Rimverse_HullViewport") ?? wall;
            door = ThingDefOf.Door;
            airlock = Named("Rimverse_Airlock") ?? door;
            light = Named("Rimverse_MegaLightPanel");
            MapComponent_MegaSection comp = MapComponent_MegaSection.For(map);
            comp.megaId = mega.id;
            comp.section = section;
            int z0, z1;
            switch (mega.kind)
            {
                case MegastructureKind.StellarCollector: z0 = 40; z1 = 78; break;
                case MegastructureKind.DeepObservatory: z0 = 22; z1 = 84; break;
                case MegastructureKind.WormholeStabilizer:
                case MegastructureKind.Gate: z0 = 30; z1 = 82; break;
                default: z0 = 8; z1 = 92; break;
            }
            comp.bandMinZ = z0;
            comp.bandMaxZ = z1;
            BuildBand(z0, z1);
            Exterior(z0, z1);
            if (ruin) Ruin(z0, z1);
            comp.genBuildings = map.listerBuildings.allBuildingsColonist.Count;
            MapGenerator.PlayerStartSpot = new IntVec3(map.Size.x / 2, 0, Mathf.Min(map.Size.z - 10, z1 + 30));
        }

        private int GlobalX(int x) => section * map.Size.x + x;

        private int Hash(int a, int b) => (int)(((uint)(mega.seed * 73856093) ^ (uint)(a * 19349663) ^ (uint)(b * 83492791)) & 0x7fffffff);

        private void Floor(IntVec3 c, TerrainDef top)
        {
            map.terrainGrid.SetFoundation(c, foundation);
            if (top != null) map.terrainGrid.SetTerrain(c, top);
        }

        private Thing Put(ThingDef def, IntVec3 c, Rot4 rot, Faction f = null, ThingDef stuff = null)
        {
            if (def == null || !c.InBounds(map)) return null;
            CellRect occ = GenAdj.OccupiedRect(c, rot, def.size);
            if (!occ.InBounds(map)) return null;
            if (def.passability == Traversability.Impassable || def.building == null || def.building.isEdifice)
            {
                foreach (IntVec3 o in occ)
                {
                    if (o.GetEdifice(map) != null || map.terrainGrid.FoundationAt(o) == null) return null;
                }
                if (def.hasInteractionCell)
                {
                    IntVec3 ic = ThingUtility.InteractionCellWhenAt(def, c, rot, map);
                    if (!ic.InBounds(map) || ic.GetEdifice(map) != null) return null;
                }
            }
            return ShipBlueprintBuilder.Spawn(map, def, c, f ?? (def.CanHaveFaction ? faction : null), rot, stuff);
        }

        private void Wall(IntVec3 c, ThingDef def = null)
        {
            Floor(c, null);
            foreach (Thing t in c.GetThingList(map).ToList()) if (t.def.category == ThingCategory.Building) t.Destroy(DestroyMode.Vanish);
            Put(def ?? wall, c, Rot4.North);
            map.roofGrid.SetRoof(c, RoofDefOf.RoofConstructed);
        }

        private void Door(IntVec3 c, bool isAirlock = false)
        {
            foreach (Thing t in c.GetThingList(map).ToList()) if (t.def.category == ThingCategory.Building) t.Destroy(DestroyMode.Vanish);
            Floor(c, null);
            bool ew = (c + IntVec3.East).GetEdifice(map) != null || (c + IntVec3.West).GetEdifice(map) != null;
            Put(isAirlock ? airlock : door, c, ew ? Rot4.North : Rot4.East);
            map.roofGrid.SetRoof(c, RoofDefOf.RoofConstructed);
        }

        private void Interior(IntVec3 c, TerrainDef top)
        {
            Floor(c, top);
            map.roofGrid.SetRoof(c, RoofDefOf.RoofConstructed);
        }

        private void Light(IntVec3 c)
        {
            if (light != null && c.InBounds(map) && c.GetEdifice(map) == null) Put(light, c, Rot4.North, null);
        }

        private void BuildBand(int z0, int z1)
        {
            int w = map.Size.x;
            int mid = (z0 + z1) / 2;
            int span = z1 - z0;
            bool wide = span >= 60;
            TerrainDef plating = T("Rimverse_DeckPlating");
            TerrainDef promenadeFloor = Habitat ? (T("Rimverse_DeckSterileTile") ?? plating) : plating;
            TerrainDef street = T("Rimverse_DeckGrating") ?? plating;
            int zA = wide ? (z0 + 1 + mid - 4) / 2 : -100;
            int zB = wide ? (mid + 4 + z1 - 1) / 2 : -100;
            for (int x = 0; x < w; x++)
            {
                for (int z = z0; z <= z1; z++)
                {
                    IntVec3 c = new IntVec3(x, 0, z);
                    Interior(c, null);
                }
            }
            for (int x = 0; x < w; x++)
            {
                int gx = GlobalX(x);
                bool view = gx % 5 == 2;
                Wall(new IntVec3(x, 0, z0), view ? window : wall);
                bool lockHere = (gx % 45) == 22;
                if (lockHere) Door(new IntVec3(x, 0, z1), true);
                else Wall(new IntVec3(x, 0, z1), gx % 7 == 3 ? window : wall);
            }
            for (int x = 1; x < w - 1; x++)
            {
                for (int z = mid - 3; z <= mid + 3; z++)
                {
                    IntVec3 c = new IntVec3(x, 0, z);
                    bool strip = Habitat && z == mid;
                    Interior(c, strip ? (T("Rimverse_HabitatSoil") ?? T("Soil")) : promenadeFloor);
                    if (strip && GlobalX(x) % 6 == 0) Put(ThingDef.Named(GlobalX(x) % 12 == 0 ? "Plant_TreeOak" : "Plant_TreePoplar"), c, Rot4.North) ?.Let(t => ((Plant)t).Growth = 1f);
                    else if (strip && GlobalX(x) % 2 == 0) Put(ThingDefOf.Plant_Grass, c, Rot4.North)?.Let(t => ((Plant)t).Growth = 1f);
                }
                if (GlobalX(x) % 6 == 3)
                {
                    Light(new IntVec3(x, 0, mid - 2));
                    Light(new IntVec3(x, 0, mid + 2));
                }
                if (wide)
                {
                    foreach (int zs in new[] { zA, zB })
                    {
                        for (int z = zs - 1; z <= zs + 1; z++) Interior(new IntVec3(x, 0, z), street);
                        if (GlobalX(x) % 8 == 4) Light(new IntVec3(x, 0, zs));
                    }
                }
            }
            for (int z = z0 + 1; z < z1; z++)
            {
                for (int x = 1; x < w - 1; x++)
                {
                    int m = GlobalX(x) % 20;
                    if (m < 9 || m > 11) continue;
                    if (z >= mid - 3 && z <= mid + 3) continue;
                    Interior(new IntVec3(x, 0, z), street);
                    if (m == 10 && z % 8 == 0) Light(new IntVec3(x, 0, z));
                }
            }
            for (int z = z0 + 1; z < z1; z++)
            {
                foreach (int x in new[] { 0, w - 1 })
                {
                    IntVec3 c = new IntVec3(x, 0, z);
                    if (z == mid || wide && (z == zA || z == zB)) Door(c);
                    else Wall(c);
                }
            }
            List<(int, int)> rows = new List<(int, int)>();
            if (wide)
            {
                rows.Add((z0 + 1, zA - 2));
                rows.Add((zA + 2, mid - 4));
                rows.Add((mid + 4, zB - 2));
                rows.Add((zB + 2, z1 - 1));
            }
            else
            {
                rows.Add((z0 + 1, mid - 4));
                rows.Add((mid + 4, z1 - 1));
            }
            int k = 0;
            for (int x = 1; x < w - 1; x++)
            {
                int m = GlobalX(x) % 20;
                if (m != 12 && x != 1) continue;
                int xEnd = x;
                while (xEnd + 1 < w - 1 && GlobalX(xEnd + 1) % 20 != 9) xEnd++;
                for (int r = 0; r < rows.Count; r++)
                {
                    (int za, int zb) = rows[r];
                    if (zb - za < 4 || xEnd - x < 3) continue;
                    CellRect block = CellRect.FromLimits(x, za, xEnd, zb);
                    int blockId = (GlobalX(x) + 9) / 20;
                    bool partial = x == 1 || xEnd == w - 2 && GlobalX(xEnd) % 20 != 8;
                    Block(block, partial ? SectionBlock.Storage : PickBlock(blockId, r), r < rows.Count / 2, Hash(blockId, r));
                    k++;
                }
                x = xEnd;
            }
        }

        private bool Habitat => mega.kind == MegastructureKind.GiantHabitat || mega.kind == MegastructureKind.RingHabitat;

        private SectionBlock PickBlock(int blockId, int row)
        {
            int h = Hash(blockId, row * 31 + 7);
            if ((h % 1000) / 1000f > built) return SectionBlock.Scaffold;
            (SectionBlock, int)[] table;
            switch (mega.kind)
            {
                case MegastructureKind.GiantHabitat:
                case MegastructureKind.RingHabitat:
                    table = new[] { (SectionBlock.Residential, 35), (SectionBlock.Park, 25), (SectionBlock.Hydroponics, 15), (SectionBlock.Mess, 10), (SectionBlock.Medical, 5), (SectionBlock.Market, 10) };
                    break;
                case MegastructureKind.Citadel:
                    table = new[] { (SectionBlock.Barracks, 30), (SectionBlock.Storage, 20), (SectionBlock.Medical, 10), (SectionBlock.Industrial, 15), (SectionBlock.Mess, 10), (SectionBlock.Control, 15) };
                    break;
                case MegastructureKind.StellarCollector:
                    table = new[] { (SectionBlock.Control, 30), (SectionBlock.Industrial, 35), (SectionBlock.Storage, 25), (SectionBlock.Barracks, 10) };
                    break;
                case MegastructureKind.DeepObservatory:
                    table = new[] { (SectionBlock.Sensors, 40), (SectionBlock.Residential, 20), (SectionBlock.Mess, 10), (SectionBlock.Storage, 15), (SectionBlock.Control, 15) };
                    break;
                case MegastructureKind.WormholeStabilizer:
                case MegastructureKind.Gate:
                    table = new[] { (SectionBlock.Control, 35), (SectionBlock.Industrial, 30), (SectionBlock.Storage, 20), (SectionBlock.Barracks, 15) };
                    break;
                default:
                    table = new[] { (SectionBlock.Market, 25), (SectionBlock.Residential, 25), (SectionBlock.Industrial, 15), (SectionBlock.Park, 10), (SectionBlock.Storage, 15), (SectionBlock.Mess, 10) };
                    break;
            }
            int total = table.Sum(t => t.Item2);
            int pick = (h / 1000) % total;
            foreach ((SectionBlock b, int wgt) in table)
            {
                if (pick < wgt) return b;
                pick -= wgt;
            }
            return table[0].Item1;
        }

        private void Block(CellRect r, SectionBlock type, bool belowPromenade, int h)
        {
            foreach (IntVec3 c in r.EdgeCells) Wall(c);
            CellRect inner = r.ContractedBy(1);
            if (type == SectionBlock.Scaffold)
            {
                foreach (IntVec3 c in inner)
                {
                    map.roofGrid.SetRoof(c, null);
                    if ((c.x + c.z) % 4 == 0 && c.x % 4 == 0) Wall(c);
                    else if ((Hash(c.x, c.z) % 3) == 0) map.terrainGrid.RemoveFoundation(c, false);
                }
                return;
            }
            TerrainDef floor = FloorFor(type);
            foreach (IntVec3 c in inner) Interior(c, floor);
            IntVec3 doorCell = belowPromenade ? new IntVec3(r.CenterCell.x, 0, r.maxZ) : new IntVec3(r.CenterCell.x, 0, r.minZ);
            Door(doorCell);
            IntVec3 side = (h & 1) == 0 ? new IntVec3(r.minX, 0, r.CenterCell.z) : new IntVec3(r.maxX, 0, r.CenterCell.z);
            Door(side);
            for (int x = inner.minX + 2; x <= inner.maxX; x += 5)
            {
                for (int z = inner.minZ + 2; z <= inner.maxZ; z += 5) Light(new IntVec3(x, 0, z));
            }
            switch (type)
            {
                case SectionBlock.Residential:
                case SectionBlock.Barracks:
                    Rooms(inner, belowPromenade, type);
                    break;
                case SectionBlock.Park:
                    foreach (IntVec3 c in inner)
                    {
                        if (c.GetFirstBuilding(map) != null) continue;
                        int v = Hash(c.x * 3 + 1, c.z * 5 + 2) % 100;
                        if (v < 6) Put(ThingDef.Named(v < 3 ? "Plant_TreeOak" : "Plant_TreePoplar"), c, Rot4.North)?.Let(t => ((Plant)t).Growth = Rand.Range(0.6f, 1f));
                        else if (v < 55) Put(ThingDefOf.Plant_Grass, c, Rot4.North)?.Let(t => ((Plant)t).Growth = Rand.Range(0.4f, 1f));
                    }
                    break;
                case SectionBlock.Hydroponics:
                    for (int x = inner.minX + 1; x <= inner.maxX - 1; x += 2)
                    {
                        for (int z = inner.minZ + 1; z + 3 <= inner.maxZ - 1; z += 5) Put(Named("Rimverse_HydroSemi") ?? Named("HydroponicsBasin"), new IntVec3(x, 0, z + 1), Rot4.North);
                    }
                    break;
                case SectionBlock.Market:
                    for (int x = inner.minX + 1; x <= inner.maxX - 1; x += 3)
                    {
                        for (int z = inner.minZ + 1; z <= inner.maxZ - 1; z += 4) Put(Named("Shelf"), new IntVec3(x, 0, z), Rot4.North, null, ThingDefOf.Steel);
                    }
                    break;
                case SectionBlock.Mess:
                    for (int x = inner.minX + 2; x <= inner.maxX - 2; x += 4)
                    {
                        for (int z = inner.minZ + 2; z <= inner.maxZ - 2; z += 4)
                        {
                            if (Put(Named("Table2x2c"), new IntVec3(x, 0, z), Rot4.North, null, ThingDefOf.Steel) == null) continue;
                            Put(Named("DiningChair"), new IntVec3(x - 1, 0, z), Rot4.East, null, ThingDefOf.Steel);
                            Put(Named("DiningChair"), new IntVec3(x + 2, 0, z), Rot4.West, null, ThingDefOf.Steel);
                        }
                    }
                    break;
                case SectionBlock.Medical:
                    for (int x = inner.minX + 1; x <= inner.maxX - 1; x += 3) Put(Named("HospitalBed"), new IntVec3(x, 0, inner.maxZ - 1), Rot4.South, null, ThingDefOf.Steel);
                    Scatter(inner, "VitalsMonitor", 2);
                    break;
                case SectionBlock.Industrial:
                    Scatter(inner, "TableMachining", 2);
                    Scatter(inner, "FabricationBench", 1);
                    Scatter(inner, "ElectricSmithy", 1);
                    Scatter(inner, "Rimverse_HoldContainer", 3);
                    break;
                case SectionBlock.Storage:
                    for (int x = inner.minX; x + 1 <= inner.maxX; x += 3)
                    {
                        for (int z = inner.minZ; z + 1 <= inner.maxZ; z += 3)
                        {
                            if (Hash(x, z) % 3 == 0) continue;
                            Put(Named("Rimverse_HoldContainer") ?? Named("Shelf"), new IntVec3(x, 0, z), Rot4.North, null, ThingDefOf.Steel);
                        }
                    }
                    if (ruin || !mega.player) Loot(inner, ruin ? 1400f : 500f);
                    break;
                case SectionBlock.Control:
                    for (int x = inner.minX + 1; x + 1 <= inner.maxX - 1; x += 4) Put(Named("Rimverse_MegaConsole"), new IntVec3(x, 0, inner.maxZ - 1), Rot4.North);
                    Scatter(inner, "DiningChair", 2);
                    break;
                case SectionBlock.Sensors:
                    Scatter(inner, "Rimverse_Telescope", 2);
                    Scatter(inner, "Rimverse_ScannerLongRange", 1);
                    Scatter(inner, "HiTechResearchBench", 1);
                    break;
            }
        }

        private TerrainDef FloorFor(SectionBlock b)
        {
            switch (b)
            {
                case SectionBlock.Park:
                case SectionBlock.Hydroponics: return (T("Rimverse_HabitatSoil") ?? T("Soil"));
                case SectionBlock.Residential: return T("Rimverse_DeckCarpet") ?? T("Rimverse_DeckPlating");
                case SectionBlock.Medical:
                case SectionBlock.Sensors:
                case SectionBlock.Mess: return T("Rimverse_DeckSterileTile") ?? T("Rimverse_DeckPlating");
                case SectionBlock.Industrial:
                case SectionBlock.Storage: return T("Rimverse_DeckGrating") ?? T("Rimverse_DeckPlating");
                default: return T("Rimverse_DeckPlating");
            }
        }

        private void Rooms(CellRect inner, bool belowPromenade, SectionBlock type)
        {
            int cols = Mathf.Max(1, inner.Width / 6);
            int roomW = inner.Width / cols;
            int hallZ = belowPromenade ? inner.maxZ - 1 : inner.minZ + 1;
            int wallZ = belowPromenade ? hallZ - 1 : hallZ + 1;
            for (int i = 1; i < cols; i++)
            {
                int x = inner.minX + i * roomW;
                int fromZ = belowPromenade ? inner.minZ : wallZ;
                int toZ = belowPromenade ? wallZ : inner.maxZ;
                for (int z = fromZ; z <= toZ; z++) Wall(new IntVec3(x, 0, z));
            }
            for (int x = inner.minX; x <= inner.maxX; x++) Wall(new IntVec3(x, 0, wallZ));
            for (int i = 0; i < cols; i++)
            {
                int xa = inner.minX + i * roomW + (i == 0 ? 0 : 1);
                int xb = i == cols - 1 ? inner.maxX : inner.minX + (i + 1) * roomW - 1;
                Door(new IntVec3((xa + xb) / 2, 0, wallZ));
                int za = belowPromenade ? inner.minZ : wallZ + 1;
                int zb = belowPromenade ? wallZ - 1 : inner.maxZ;
                Rot4 bedRot = belowPromenade ? Rot4.North : Rot4.South;
                int beds = type == SectionBlock.Barracks ? Mathf.Max(1, (xb - xa + 1) / 2) : 1;
                for (int b = 0; b < beds; b++)
                {
                    IntVec3 bc = new IntVec3(xa + b * 2, 0, belowPromenade ? za : zb);
                    Put(Named("Bed"), bc, bedRot, null, ThingDefOf.Steel);
                }
                if (type == SectionBlock.Residential)
                {
                    Put(Named("Table1x2c") ?? Named("Table2x2c"), new IntVec3(xb, 0, (za + zb) / 2), Rot4.North, null, ThingDefOf.Steel);
                    Put(Named("Dresser"), new IntVec3(xa, 0, (za + zb) / 2), Rot4.East, null, ThingDefOf.Steel);
                }
                Light(new IntVec3((xa + xb) / 2, 0, (za + zb) / 2));
            }
        }

        private void Scatter(CellRect inner, string defName, int count)
        {
            ThingDef d = Named(defName);
            if (d == null) return;
            for (int i = 0, tries = 0; i < count && tries < 40; tries++)
            {
                IntVec3 c = new IntVec3(rand.Next(inner.minX + 1, inner.maxX), 0, rand.Next(inner.minZ + 1, inner.maxZ));
                if (Put(d, c, Rot4.North, null, d.MadeFromStuff ? ThingDefOf.Steel : null) != null) i++;
            }
        }

        private void Loot(CellRect inner, float value)
        {
            string[] pool = { "Steel", "Plasteel", "ComponentIndustrial", "ComponentSpacer", "MedicineIndustrial", "Gold", "Uranium", "Rimverse_ArmorAlloy", "Rimverse_ThermalCeramic" };
            float left = value;
            for (int guard = 0; left > 0f && guard < 12; guard++)
            {
                ThingDef def = Named(pool[rand.Next(pool.Length)]);
                if (def == null) continue;
                int count = Mathf.Clamp(Mathf.RoundToInt(Rand.Range(100f, 300f) / Mathf.Max(1f, def.BaseMarketValue)), 1, def.stackLimit);
                IntVec3 c = new IntVec3(rand.Next(inner.minX, inner.maxX + 1), 0, rand.Next(inner.minZ, inner.maxZ + 1));
                if (!c.Standable(map)) continue;
                Thing t = ThingMaker.MakeThing(def);
                t.stackCount = count;
                GenPlace.TryPlaceThing(t, c, map, ThingPlaceMode.Near);
                left -= count * def.BaseMarketValue;
            }
        }

        private void Exterior(int z0, int z1)
        {
            int w = map.Size.x;
            switch (mega.kind)
            {
                case MegastructureKind.StellarCollector:
                    Arrays(4, z0 - 2, "Rimverse_ShipSolarPanel", 3);
                    Arrays(z1 + 2, z1 + 22, "Rimverse_ShipSolarPanel", 3);
                    break;
                case MegastructureKind.DeepObservatory:
                    Dish(new IntVec3(w / 2, 0, z1 + 9), 8);
                    break;
                case MegastructureKind.Citadel:
                    for (int x = 6; x < w - 6; x += 15)
                    {
                        Pad(CellRect.FromLimits(x - 1, z1 + 1, x + 1, z1 + 3));
                        Put(Named("Turret_Autocannon") ?? Named("Turret_MiniTurret"), new IntVec3(x, 0, z1 + 2), Rot4.North, mega.player ? Faction.OfPlayer : null, ThingDefOf.Steel);
                    }
                    break;
                case MegastructureKind.WormholeStabilizer:
                case MegastructureKind.Gate:
                    for (int x = 3; x < w - 3; x += 12) Pylon(new IntVec3(x, 0, z0 - 1), new IntVec3(x, 0, Mathf.Max(2, z0 - 14)));
                    break;
                default:
                    if (mega.kind == MegastructureKind.OrbitalRing)
                    {
                        for (int x = 2; x < w - 2; x += 30) Pad(CellRect.FromLimits(x, z1 + 1, x + 9, z1 + 6));
                    }
                    break;
            }
        }

        private void Pad(CellRect r)
        {
            foreach (IntVec3 c in r)
            {
                if (!c.InBounds(map)) continue;
                map.terrainGrid.SetFoundation(c, foundation);
            }
        }

        private void Arrays(int za, int zb, string defName, int step)
        {
            ThingDef d = Named(defName);
            if (d == null) return;
            for (int z = za; z + 1 <= zb; z += step)
            {
                for (int x = 1; x + 1 < map.Size.x - 1; x += step)
                {
                    if (Hash(GlobalX(x), z) % 1000 / 1000f > built) continue;
                    Pad(CellRect.FromLimits(x, z, x + 1, z + 1));
                    Put(d, new IntVec3(x, 0, z), Rot4.North);
                }
                for (int x = 0; x < map.Size.x; x++) if (z % (step * 3) == 0) Pad(CellRect.FromLimits(x, z + 2, x, z + 2));
            }
        }

        private void Dish(IntVec3 center, int radius)
        {
            foreach (IntVec3 c in GenRadial.RadialCellsAround(center, radius, true))
            {
                if (!c.InBounds(map)) continue;
                map.terrainGrid.SetFoundation(c, foundation);
            }
            for (int x = center.x; x <= center.x; x++)
            {
                for (int z = (center.z - radius - 1); z < center.z; z++) map.terrainGrid.SetFoundation(new IntVec3(x, 0, z), foundation);
            }
            Put(Named("Rimverse_ScannerAdvanced"), center, Rot4.North);
            foreach (IntVec3 o in new[] { new IntVec3(-5, 0, 0), new IntVec3(5, 0, 0), new IntVec3(0, 0, 5), new IntVec3(0, 0, -4) }) Put(Named("Rimverse_Telescope"), center + o, Rot4.North);
        }

        private void Pylon(IntVec3 from, IntVec3 to)
        {
            for (int z = to.z; z <= from.z; z++)
            {
                IntVec3 c = new IntVec3(from.x, 0, z);
                if (c.InBounds(map)) Wall(c, Named("Rimverse_HullWallArmored") ?? wall);
            }
            Light(new IntVec3(from.x + 1, 0, to.z));
        }

        private void Ruin(int z0, int z1)
        {
            foreach (Building b in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial).OfType<Building>().ToList())
            {
                if (b.def.useHitPoints && Rand.Chance(0.6f)) b.HitPoints = Mathf.Max(1, Mathf.RoundToInt(b.MaxHitPoints * Rand.Range(0.2f, 0.9f)));
            }
            for (int i = 0; i < 6; i++)
            {
                IntVec3 c = new IntVec3(rand.Next(2, map.Size.x - 2), 0, rand.Next(z0 + 2, z1 - 2));
                int rad = rand.Next(2, 5);
                foreach (IntVec3 h in GenRadial.RadialCellsAround(c, rad, true))
                {
                    if (!h.InBounds(map)) continue;
                    map.roofGrid.SetRoof(h, null);
                    if (Rand.Chance(0.5f))
                    {
                        Building e = h.GetEdifice(map);
                        if (e != null && e.def.destroyable) e.Destroy(DestroyMode.Vanish);
                    }
                    if (Rand.Chance(0.15f) && h.Standable(map)) GenSpawn.Spawn(ThingMaker.MakeThing(ThingDefOf.ChunkSlagSteel), h, map);
                }
            }
            foreach (IntVec3 c in CellRect.FromLimits(1, z0 + 1, map.Size.x - 2, z1 - 1))
            {
                if (Rand.Chance(0.02f) && c.Standable(map)) FilthMaker.TryMakeFilth(c, map, ThingDefOf.Filth_RubbleBuilding, 1);
            }
        }
    }

    public static class MegaGenExtensions
    {
        public static void Let(this Thing t, System.Action<Thing> a)
        {
            if (t != null) a(t);
        }
    }
}
