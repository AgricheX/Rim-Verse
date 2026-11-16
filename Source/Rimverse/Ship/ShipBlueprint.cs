using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public enum BlueprintLayout
    {
        Ship,
        Station
    }

    public enum BlueprintStyle
    {
        Civil,
        Military,
        Pirate,
        Imperial,
        Mechanoid,
        Derelict
    }

    public enum RoomRole
    {
        None,
        Corridor,
        Bridge,
        Engineering,
        Reactor,
        LifeSupport,
        Quarters,
        Mess,
        Medbay,
        Cargo,
        Sensors,
        Armory,
        Hydroponics,
        Workshop,
        Cryo,
        Hangar
    }

    public enum BlueprintCell : byte
    {
        Empty,
        Floor,
        Wall,
        Door,
        Window,
        Exterior
    }

    public class BlueprintRequest
    {
        public BlueprintLayout layout = BlueprintLayout.Ship;
        public BlueprintStyle style = BlueprintStyle.Civil;
        public int sizeClass = 1;
        public bool armored;
        public float damage;
        public int seed;
        public List<string> parts = new List<string>();
        public int beds;

        public BlueprintRequest Add(string def, int count = 1)
        {
            for (int i = 0; i < count; i++) parts.Add(def);
            return this;
        }
    }

    public class BlueprintRoom
    {
        public int id;
        public RoomRole role;
        public List<IntVec3> cells = new List<IntVec3>();
        public IntVec3 door = IntVec3.Invalid;
        public IntVec3 doorInside = IntVec3.Invalid;
        public List<IntVec3> extraInside = new List<IntVec3>();
        public float centerZ;
        public float centerX;
    }

    public class BlueprintPlacement
    {
        public ThingDef def;
        public IntVec3 center;
        public Rot4 rot = Rot4.North;
        public ThingDef stuff;
    }

    public class ShipBlueprint
    {
        public int width;
        public int height;
        public BlueprintCell[,] cells;
        public int[,] roomOf;
        public List<BlueprintRoom> rooms = new List<BlueprintRoom>();
        public List<BlueprintPlacement> placements = new List<BlueprintPlacement>();
        public HashSet<IntVec3> corridor = new HashSet<IntVec3>();
        public HashSet<IntVec3> occupied = new HashSet<IntVec3>();
        public HashSet<IntVec3> holes = new HashSet<IntVec3>();
        public List<string> skipped = new List<string>();
        public BlueprintRequest request;
        public IntVec3 entrance = IntVec3.Invalid;

        public bool In(IntVec3 c) => c.x >= 0 && c.z >= 0 && c.x < width && c.z < height;

        public BlueprintCell At(IntVec3 c) => In(c) ? cells[c.x, c.z] : BlueprintCell.Empty;

        public BlueprintRoom RoomAt(IntVec3 c) => In(c) && roomOf[c.x, c.z] >= 0 ? rooms[roomOf[c.x, c.z]] : null;

        public IEnumerable<IntVec3> AllCells
        {
            get
            {
                for (int x = 0; x < width; x++)
                    for (int z = 0; z < height; z++)
                        yield return new IntVec3(x, 0, z);
            }
        }

        public bool Walkable(IntVec3 c)
        {
            BlueprintCell k = At(c);
            return (k == BlueprintCell.Floor || k == BlueprintCell.Door) && !occupied.Contains(c);
        }
    }

    public static class ShipBlueprintGenerator
    {
        private static readonly int[] HalfWidths = { 3, 4, 5, 7, 9, 12 };

        public static ThingDef Named(string defName) => DefDatabase<ThingDef>.GetNamedSilentFail(defName);

        public static RoomRole RoleFor(ThingDef def)
        {
            if (def == null) return RoomRole.None;
            CompProperties_ShipPart sp = def.GetCompProperties<CompProperties_ShipPart>();
            if (sp != null)
            {
                switch (sp.role)
                {
                    case ShipPartRole.Bridge: return RoomRole.Bridge;
                    case ShipPartRole.Reactor:
                    case ShipPartRole.Battery:
                    case ShipPartRole.Capacitor:
                    case ShipPartRole.Gravity:
                    case ShipPartRole.Shield:
                    case ShipPartRole.Cloak: return RoomRole.Reactor;
                    case ShipPartRole.FuelTank:
                    case ShipPartRole.WarpDrive:
                    case ShipPartRole.WarpTank:
                    case ShipPartRole.HeatBank:
                    case ShipPartRole.HeatSink:
                    case ShipPartRole.Structure: return RoomRole.Engineering;
                    case ShipPartRole.AirGenerator:
                    case ShipPartRole.AirTank:
                    case ShipPartRole.IceProcessor:
                    case ShipPartRole.WaterRecycler: return RoomRole.LifeSupport;
                    case ShipPartRole.Garden: return RoomRole.Hydroponics;
                    case ShipPartRole.Sensor:
                    case ShipPartRole.SignatureDetector:
                    case ShipPartRole.ProbeLauncher: return RoomRole.Sensors;
                    case ShipPartRole.Cargo: return def.defName.Contains("Weapon") || def.defName.Contains("Armory") ? RoomRole.Armory : RoomRole.Cargo;
                    case ShipPartRole.Hangar: return RoomRole.Hangar;
                }
            }
            string n = def.defName;
            if (def.IsBed) return n.Contains("Cryptosleep") ? RoomRole.Cryo : n.Contains("Hospital") || n.Contains("MedPod") ? RoomRole.Medbay : RoomRole.Quarters;
            if (n.Contains("Cryptosleep")) return RoomRole.Cryo;
            if (n.Contains("Galley") || n.Contains("Kitchen") || n.Contains("Dispenser") || n.Contains("Table") || n.Contains("Chair")) return RoomRole.Mess;
            if (n.Contains("Med") || n.Contains("Vitals")) return RoomRole.Medbay;
            if (n.Contains("Hydro") || n.Contains("Garden")) return RoomRole.Hydroponics;
            if (n.Contains("Bench") || n.Contains("Fabricator") || n.Contains("Lab") || n.Contains("Foundry") || n.Contains("Workshop") || n.Contains("Maintenance")) return RoomRole.Workshop;
            if (n.Contains("Armory") || n.Contains("Weapon")) return RoomRole.Armory;
            if (n.Contains("AICore") || n.Contains("Security") || n.Contains("Climate")) return RoomRole.Reactor;
            if (n.Contains("TradeTerminal") || n.Contains("HoldContainer") || n.Contains("Shelf")) return RoomRole.Cargo;
            return RoomRole.None;
        }

        public static bool IsExterior(ThingDef def)
        {
            CompProperties_ShipPart sp = def?.GetCompProperties<CompProperties_ShipPart>();
            if (sp == null) return false;
            return sp.role == ShipPartRole.Thruster || sp.role == ShipPartRole.Radiator || sp.role == ShipPartRole.Solar || sp.role == ShipPartRole.Maneuver || sp.role == ShipPartRole.Weapon || def.defName == "Rimverse_DockingPort";
        }

        public static ShipBlueprint Generate(BlueprintRequest req)
        {
            List<ThingDef> defs = req.parts.Select(Named).Where(d => d != null).ToList();
            ShipBlueprint best = null;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                float grow = 1.35f + attempt * 0.3f;
                List<RoomPlan> plans = PlanRooms(req, defs, grow);
                ShipBlueprint bp = req.layout == BlueprintLayout.Station ? LayoutStation(req, plans) : LayoutShip(req, plans);
                foreach (RoomPlan p in plans)
                {
                    BlueprintRoom room = bp.rooms.FirstOrDefault(r => r.id == p.roomId);
                    if (room == null) { bp.skipped.AddRange(p.parts.Select(x => x.defName)); continue; }
                    foreach (ThingDef d in p.parts.OrderByDescending(x => x.size.x * x.size.z))
                    {
                        if (TryPlaceInRoom(bp, room, d, null)) continue;
                        if (bp.rooms.Where(r => r != room).OrderByDescending(r => r.cells.Count(c => bp.Walkable(c))).Any(r => TryPlaceInRoom(bp, r, d, null))) continue;
                        bp.skipped.Add(d.defName);
                    }
                }
                PlaceExterior(bp, defs, req);
                Furnish(bp, req);
                if (best == null || bp.skipped.Count < best.skipped.Count) best = bp;
                if (bp.skipped.Count == 0) break;
            }
            if (req.damage > 0f) Damage(best, req);
            return best;
        }

        private class RoomPlan
        {
            public RoomRole role;
            public List<ThingDef> parts = new List<ThingDef>();
            public int width;
            public int height;
            public int roomId = -1;
            public int Area => parts.Sum(p => p.size.x * p.size.z + (p.hasInteractionCell ? 1 : 0));
            public int MaxDim => parts.Count == 0 ? 2 : parts.Max(p => Math.Max(p.size.x, p.size.z));
            public int MaxX => parts.Count == 0 ? 2 : parts.Max(p => p.size.x);
            public int MaxZ => parts.Count == 0 ? 2 : parts.Max(p => p.size.z);
        }

        private static readonly RoomRole[] SternToBow = { RoomRole.Engineering, RoomRole.Reactor, RoomRole.LifeSupport, RoomRole.Hangar, RoomRole.Cargo, RoomRole.Workshop, RoomRole.Armory, RoomRole.Hydroponics, RoomRole.Mess, RoomRole.Medbay, RoomRole.Quarters, RoomRole.Cryo, RoomRole.Sensors, RoomRole.Bridge };

        private static List<RoomPlan> PlanRooms(BlueprintRequest req, List<ThingDef> defs, float grow)
        {
            System.Random r = new System.Random(req.seed);
            Dictionary<RoomRole, RoomPlan> byRole = new Dictionary<RoomRole, RoomPlan>();
            foreach (ThingDef d in defs)
            {
                if (IsExterior(d)) continue;
                RoomRole role = RoleFor(d);
                if (role == RoomRole.None) role = RoomRole.Cargo;
                if (role == RoomRole.Reactor && req.sizeClass <= 1) role = RoomRole.Engineering;
                if (role == RoomRole.Sensors && req.sizeClass <= 1) role = RoomRole.Bridge;
                if (!byRole.TryGetValue(role, out RoomPlan p)) byRole[role] = p = new RoomPlan { role = role };
                p.parts.Add(d);
            }
            if (!byRole.ContainsKey(RoomRole.Engineering) && req.layout == BlueprintLayout.Ship) byRole[RoomRole.Engineering] = new RoomPlan { role = RoomRole.Engineering };
            int maxRoomArea = 18 + req.sizeClass * 10;
            List<RoomPlan> plans = new List<RoomPlan>();
            foreach (RoomRole role in SternToBow)
            {
                if (!byRole.TryGetValue(role, out RoomPlan p)) continue;
                int target = Mathf.CeilToInt(p.Area * grow);
                if (target > maxRoomArea * 1.6f && p.parts.Count > 1 && role != RoomRole.Bridge)
                {
                    int pieces = Mathf.CeilToInt(target / (float)maxRoomArea);
                    List<RoomPlan> split = new List<RoomPlan>();
                    for (int i = 0; i < pieces; i++) split.Add(new RoomPlan { role = role });
                    int k = 0;
                    foreach (ThingDef d in p.parts.OrderByDescending(x => x.size.x * x.size.z)) split[k++ % pieces].parts.Add(d);
                    plans.AddRange(split.Where(s => s.parts.Count > 0));
                }
                else plans.Add(p);
            }
            foreach (RoomPlan p in plans)
            {
                int target = Mathf.Max(8, Mathf.CeilToInt(p.Area * grow));
                int minW = Mathf.Max(3, p.MaxDim + 1);
                p.width = Mathf.Clamp(minW + r.Next(0, 3), 3, 9 + req.sizeClass);
                p.height = Mathf.Max(p.MaxDim + 1, Mathf.CeilToInt(target / (float)p.width));
            }
            return plans;
        }

        private static ShipBlueprint Canvas(BlueprintRequest req, int size)
        {
            ShipBlueprint bp = new ShipBlueprint { width = size, height = size, request = req };
            bp.cells = new BlueprintCell[size, size];
            bp.roomOf = new int[size, size];
            for (int x = 0; x < size; x++) for (int z = 0; z < size; z++) bp.roomOf[x, z] = -1;
            return bp;
        }

        private static BlueprintRoom Stamp(ShipBlueprint bp, CellRect interior, RoomRole role)
        {
            BlueprintRoom room = new BlueprintRoom { id = bp.rooms.Count, role = role };
            foreach (IntVec3 c in interior.ExpandedBy(1))
            {
                if (!bp.In(c)) continue;
                if (bp.cells[c.x, c.z] == BlueprintCell.Empty) bp.cells[c.x, c.z] = BlueprintCell.Wall;
            }
            foreach (IntVec3 c in interior)
            {
                if (!bp.In(c)) continue;
                bp.cells[c.x, c.z] = BlueprintCell.Floor;
                bp.roomOf[c.x, c.z] = room.id;
                room.cells.Add(c);
            }
            room.centerX = (float)room.cells.Average(c => c.x);
            room.centerZ = (float)room.cells.Average(c => c.z);
            bp.rooms.Add(room);
            return room;
        }

        private static void Door(ShipBlueprint bp, IntVec3 wall, BlueprintRoom room, IntVec3 inside)
        {
            if (!bp.In(wall)) return;
            bp.cells[wall.x, wall.z] = BlueprintCell.Door;
            if (room == null) return;
            if (!room.door.IsValid) { room.door = wall; room.doorInside = inside; }
            room.extraInside.Add(inside);
        }

        private static void Corridor(ShipBlueprint bp, IntVec3 c)
        {
            if (!bp.In(c)) return;
            foreach (IntVec3 o in GenAdj.AdjacentCellsAndInside)
            {
                IntVec3 n = c + o;
                if (bp.In(n) && bp.cells[n.x, n.z] == BlueprintCell.Empty) bp.cells[n.x, n.z] = BlueprintCell.Wall;
            }
            bp.cells[c.x, c.z] = BlueprintCell.Floor;
            bp.roomOf[c.x, c.z] = -1;
            bp.corridor.Add(c);
        }

        private static ShipBlueprint LayoutShip(BlueprintRequest req, List<RoomPlan> plans)
        {
            System.Random r = new System.Random(req.seed * 31 + 7);
            ShipBlueprint bp = Canvas(req, 320);
            int cx = 160;
            int z = 12;
            RoomPlan eng = plans.FirstOrDefault(p => p.role == RoomRole.Engineering);
            RoomPlan bridge = plans.FirstOrDefault(p => p.role == RoomRole.Bridge);
            List<RoomPlan> middle = plans.Where(p => p != eng && p != bridge).ToList();
            bool wide = req.sizeClass >= 3;
            int spineHalf = 0;
            if (!wide)
            {
                int baseHalf = Mathf.Clamp(plans.Max(p => p.MaxX) + 1, 3, 4);
                BlueprintRoom prev = null;
                foreach (RoomPlan p in new[] { eng }.Concat(middle).Concat(new[] { bridge }).Where(x => x != null))
                {
                    int half = Mathf.Clamp(Math.Max(baseHalf, p.MaxX + 1) + (req.style == BlueprintStyle.Pirate ? r.Next(-1, 2) : r.Next(0, 2)), 3, 5);
                    int h = Mathf.Max(p.MaxZ + 1, Mathf.CeilToInt(p.width * p.height / (float)(half * 2 + 1)));
                    if (p == bridge) h = Mathf.Max(p.MaxZ + 2, h - 2);
                    CellRect rect = new CellRect(cx - half, z, half * 2 + 1, h);
                    BlueprintRoom room = Stamp(bp, rect, p.role);
                    p.roomId = room.id;
                    if (prev != null) Door(bp, new IntVec3(cx, 0, z - 1), room, new IntVec3(cx, 0, z));
                    if (prev != null) prev.extraInside.Add(new IntVec3(cx, 0, z - 2));
                    prev = room;
                    z += h + 1;
                }
                spineHalf = baseHalf;
            }
            else
            {
                if (eng != null)
                {
                    int half = Mathf.Clamp((eng.width + 1) / 2 + 3, 5, 9);
                    int h = Mathf.Max(eng.MaxDim + 1, Mathf.CeilToInt(eng.width * eng.height / (float)(half * 2 + 1)) + 1);
                    BlueprintRoom room = Stamp(bp, new CellRect(cx - half, z, half * 2 + 1, h), RoomRole.Engineering);
                    eng.roomId = room.id;
                    z += h + 1;
                    Door(bp, new IntVec3(cx, 0, z - 1), room, new IntVec3(cx, 0, z - 2));
                }
                int zStart = z;
                int zl = z, zr = z;
                List<RoomPlan> left = new List<RoomPlan>(), right = new List<RoomPlan>();
                foreach (RoomPlan p in middle)
                {
                    int sl = left.Sum(x => x.height + 1), sr = right.Sum(x => x.height + 1);
                    (sl <= sr ? left : right).Add(p);
                }
                int sumL = left.Sum(x => x.height + 1), sumR = right.Sum(x => x.height + 1);
                int len = Math.Max(sumL, sumR);
                if (left.Count > 0) left[left.Count - 1].height += len - sumL;
                if (right.Count > 0) right[right.Count - 1].height += len - sumR;
                for (int i = 0; i < len; i++) Corridor(bp, new IntVec3(cx, 0, zStart + i));
                foreach (RoomPlan p in left)
                {
                    BlueprintRoom room = Stamp(bp, new CellRect(cx - 1 - p.width, zl, p.width, p.height), p.role);
                    p.roomId = room.id;
                    int dz = zl + Math.Min(p.height - 1, 1 + r.Next(Math.Max(1, p.height - 1)));
                    Door(bp, new IntVec3(cx - 1, 0, dz), room, new IntVec3(cx - 2, 0, dz));
                    zl += p.height + 1;
                }
                foreach (RoomPlan p in right)
                {
                    BlueprintRoom room = Stamp(bp, new CellRect(cx + 2, zr, p.width, p.height), p.role);
                    p.roomId = room.id;
                    int dz = zr + Math.Min(p.height - 1, 1 + r.Next(Math.Max(1, p.height - 1)));
                    Door(bp, new IntVec3(cx + 1, 0, dz), room, new IntVec3(cx + 2, 0, dz));
                    zr += p.height + 1;
                }
                z = zStart + len;
                spineHalf = 4;
            }
            BlueprintRoom bridgeRoom = bp.rooms.FirstOrDefault(x => x.role == RoomRole.Bridge);
            if (wide && bridge != null)
            {
                int half = Mathf.Clamp((bridge.width + 1) / 2 + 2, 4, 8);
                int h = Mathf.Max(bridge.MaxDim + 2, Mathf.CeilToInt(bridge.width * bridge.height / (float)(half * 2 + 1)) + 1);
                bridgeRoom = Stamp(bp, new CellRect(cx - half, z, half * 2 + 1, h), RoomRole.Bridge);
                bridge.roomId = bridgeRoom.id;
                Door(bp, new IntVec3(cx, 0, z - 1), bridgeRoom, new IntVec3(cx, 0, z));
                z += h + 1;
            }
            if (bridgeRoom != null) Nose(bp, bridgeRoom, req);
            ConnectCorridorToEngineering(bp);
            return Crop(bp, 6);
        }

        private static void ConnectCorridorToEngineering(ShipBlueprint bp)
        {
            foreach (BlueprintRoom room in bp.rooms)
            {
                if (room.extraInside.Count == 0 && room.door.IsValid) room.extraInside.Add(room.doorInside);
            }
        }

        private static void Nose(ShipBlueprint bp, BlueprintRoom bridge, BlueprintRequest req)
        {
            int top = bridge.cells.Max(c => c.z);
            int minX = bridge.cells.Where(c => c.z == top).Min(c => c.x);
            int maxX = bridge.cells.Where(c => c.z == top).Max(c => c.x);
            bool pointed = req.style == BlueprintStyle.Military || req.style == BlueprintStyle.Imperial || req.style == BlueprintStyle.Mechanoid;
            int step = 0;
            int z = top + 1;
            while (maxX - minX >= 2)
            {
                step++;
                int shrink = pointed ? 1 : step <= 1 ? 0 : 1;
                if (!pointed && step % 2 == 0) shrink = 1;
                minX += shrink; maxX -= shrink;
                if (maxX - minX < 0) break;
                if (bp.In(new IntVec3(minX - 1, 0, z + 1)) == false) break;
                for (int x = minX - 1; x <= maxX + 1; x++)
                {
                    IntVec3 w = new IntVec3(x, 0, z + 1);
                    if (bp.cells[x, z + 1] == BlueprintCell.Empty) bp.cells[x, z + 1] = BlueprintCell.Wall;
                }
                for (int x = minX - 1; x <= maxX + 1; x++)
                {
                    IntVec3 c = new IntVec3(x, 0, z);
                    if (x == minX - 1 || x == maxX + 1) { if (bp.cells[x, z] == BlueprintCell.Empty) bp.cells[x, z] = BlueprintCell.Wall; continue; }
                    bp.cells[x, z] = BlueprintCell.Floor;
                    bp.roomOf[x, z] = bridge.id;
                    bridge.cells.Add(c);
                }
                z++;
                if (step > 3) break;
            }
        }

        private static ShipBlueprint LayoutStation(BlueprintRequest req, List<RoomPlan> plans)
        {
            System.Random r = new System.Random(req.seed * 17 + 3);
            ShipBlueprint bp = Canvas(req, 320);
            int c = 160;
            RoomPlan core = plans.FirstOrDefault(p => p.role == RoomRole.Bridge) ?? plans.FirstOrDefault();
            int coreHalf = Mathf.Clamp((core?.MaxDim ?? 3) / 2 + 3, 4, 7);
            BlueprintRoom coreRoom = Stamp(bp, new CellRect(c - coreHalf, c - coreHalf, coreHalf * 2 + 1, coreHalf * 2 + 1), RoomRole.Bridge);
            if (core != null) core.roomId = coreRoom.id;
            List<RoomPlan> rest = plans.Where(p => p != core).ToList();
            IntVec3[] dirs = { new IntVec3(0, 0, 1), new IntVec3(1, 0, 0), new IntVec3(0, 0, -1), new IntVec3(-1, 0, 0) };
            List<RoomPlan>[] arms = { new List<RoomPlan>(), new List<RoomPlan>(), new List<RoomPlan>(), new List<RoomPlan>() };
            for (int i = 0; i < rest.Count; i++) arms[i % 4].Add(rest[i]);
            for (int a = 0; a < 4; a++)
            {
                IntVec3 d = dirs[a];
                IntVec3 side = new IntVec3(d.z, 0, -d.x);
                IntVec3 start = new IntVec3(c, 0, c) + d * (coreHalf + 1);
                Door(bp, start, coreRoom, new IntVec3(c, 0, c) + d * coreHalf);
                int len = 3;
                List<RoomPlan> list = arms[a];
                List<RoomPlan> sideA = new List<RoomPlan>(), sideB = new List<RoomPlan>();
                foreach (RoomPlan p in list) (sideA.Sum(x => x.height + 1) <= sideB.Sum(x => x.height + 1) ? sideA : sideB).Add(p);
                len += Math.Max(sideA.Sum(x => x.height + 1), sideB.Sum(x => x.height + 1)) + 2;
                for (int i = 1; i <= len; i++) Corridor(bp, start + d * i);
                StampAlong(bp, sideA, start + d * 3, d, side, r);
                StampAlong(bp, sideB, start + d * 3, d, new IntVec3(-side.x, 0, -side.z), r);
                IntVec3 tip = start + d * (len + 1);
                int podHalf = 3;
                CellRect pod = RectAround(tip + d * (podHalf + 1), podHalf);
                BlueprintRoom podRoom = Stamp(bp, pod, a % 2 == 0 ? RoomRole.Cargo : RoomRole.Hangar);
                Door(bp, tip, podRoom, tip + d);
            }
            ConnectCorridorToEngineering(bp);
            return Crop(bp, 6);
        }

        private static CellRect RectAround(IntVec3 center, int half) => new CellRect(center.x - half, center.z - half, half * 2 + 1, half * 2 + 1);

        private static void StampAlong(ShipBlueprint bp, List<RoomPlan> list, IntVec3 start, IntVec3 d, IntVec3 side, System.Random r)
        {
            int along = 0;
            foreach (RoomPlan p in list)
            {
                IntVec3 a = start + d * along + side * 2;
                IntVec3 b = start + d * (along + p.height - 1) + side * (1 + p.width);
                CellRect rect = new CellRect(Math.Min(a.x, b.x), Math.Min(a.z, b.z), Math.Abs(a.x - b.x) + 1, Math.Abs(a.z - b.z) + 1);
                BlueprintRoom room = Stamp(bp, rect, p.role);
                p.roomId = room.id;
                int k = along + Math.Min(p.height - 1, r.Next(Math.Max(1, p.height)));
                Door(bp, start + d * k + side, room, start + d * k + side * 2);
                along += p.height + 1;
            }
        }

        private static ShipBlueprint Crop(ShipBlueprint src, int margin)
        {
            int minX = int.MaxValue, minZ = int.MaxValue, maxX = -1, maxZ = -1;
            foreach (IntVec3 c in src.AllCells)
            {
                if (src.At(c) == BlueprintCell.Empty) continue;
                minX = Math.Min(minX, c.x); maxX = Math.Max(maxX, c.x); minZ = Math.Min(minZ, c.z); maxZ = Math.Max(maxZ, c.z);
            }
            int ox = minX - margin, oz = minZ - margin;
            ShipBlueprint bp = Canvas(src.request, 1);
            bp.width = maxX - minX + 1 + margin * 2;
            bp.height = maxZ - minZ + 1 + margin * 2;
            bp.cells = new BlueprintCell[bp.width, bp.height];
            bp.roomOf = new int[bp.width, bp.height];
            IntVec3 off = new IntVec3(ox, 0, oz);
            for (int x = 0; x < bp.width; x++)
                for (int z = 0; z < bp.height; z++)
                {
                    IntVec3 s = new IntVec3(x, 0, z) + off;
                    bp.cells[x, z] = src.At(s);
                    bp.roomOf[x, z] = src.In(s) ? src.roomOf[s.x, s.z] : -1;
                }
            foreach (BlueprintRoom room in src.rooms)
            {
                room.cells = room.cells.Select(c => c - off).ToList();
                if (room.door.IsValid) { room.door -= off; room.doorInside -= off; }
                room.extraInside = room.extraInside.Select(c => c - off).ToList();
                room.centerX -= ox; room.centerZ -= oz;
                bp.rooms.Add(room);
            }
            foreach (IntVec3 c in src.corridor) bp.corridor.Add(c - off);
            MakeEntrance(bp);
            return bp;
        }

        private static void MakeEntrance(ShipBlueprint bp)
        {
            float best = float.MaxValue;
            foreach (IntVec3 c in bp.corridor)
            {
                foreach (IntVec3 o in GenAdj.CardinalDirections)
                {
                    IntVec3 w = c + o;
                    if (bp.At(w) != BlueprintCell.Wall || bp.At(w + o) != BlueprintCell.Empty) continue;
                    IntVec3 side = new IntVec3(o.z, 0, o.x);
                    if (bp.At(w + side) != BlueprintCell.Wall || bp.At(w - side) != BlueprintCell.Wall) continue;
                    float score = Mathf.Abs(c.z - bp.height * 0.55f) + (o.x == 0 ? 6f : 0f);
                    if (score < best) { best = score; bp.entrance = w; }
                }
            }
            if (bp.entrance.IsValid) bp.cells[bp.entrance.x, bp.entrance.z] = BlueprintCell.Door;
        }

        public static bool TryPlaceInRoom(ShipBlueprint bp, BlueprintRoom room, ThingDef d, ThingDef stuff, Rot4? forced = null)
        {
            List<IntVec3> candidates = room.cells.OrderBy(c => WallScore(bp, c)).ThenBy(c => c.x * 31 + c.z).ToList();
            Rot4[] rots = forced.HasValue ? new[] { forced.Value } : d.rotatable ? new[] { Rot4.North, Rot4.East, Rot4.South, Rot4.West } : new[] { Rot4.North };
            foreach (Rot4 rot in rots)
            {
                foreach (IntVec3 c in candidates)
                {
                    CellRect occ = GenAdj.OccupiedRect(c, rot, d.size);
                    bool fits = true;
                    foreach (IntVec3 o in occ)
                    {
                        if (!bp.In(o) || bp.RoomAt(o) != room || bp.occupied.Contains(o) || o == room.doorInside || room.extraInside.Contains(o) || bp.At(o) != BlueprintCell.Floor) { fits = false; break; }
                    }
                    if (!fits) continue;
                    IntVec3 ic = IntVec3.Invalid;
                    if (d.hasInteractionCell)
                    {
                        ic = c + d.interactionCellOffset.RotatedBy(rot);
                        if (!bp.In(ic) || occ.Contains(ic) || !bp.Walkable(ic)) continue;
                    }
                    foreach (IntVec3 o in occ) bp.occupied.Add(o);
                    if (!RoomStillWorks(bp, room, occ, ic))
                    {
                        foreach (IntVec3 o in occ) bp.occupied.Remove(o);
                        continue;
                    }
                    bp.placements.Add(new BlueprintPlacement { def = d, center = c, rot = rot, stuff = stuff });
                    return true;
                }
            }
            return false;
        }

        private static int WallScore(ShipBlueprint bp, IntVec3 c)
        {
            int walls = 0;
            foreach (IntVec3 o in GenAdj.CardinalDirections)
            {
                BlueprintCell k = bp.At(c + o);
                if (k == BlueprintCell.Wall || k == BlueprintCell.Window) walls++;
            }
            return -walls;
        }

        private static bool RoomStillWorks(ShipBlueprint bp, BlueprintRoom room, CellRect newOcc, IntVec3 newIc)
        {
            if (!room.doorInside.IsValid) return true;
            HashSet<IntVec3> reach = new HashSet<IntVec3>();
            Queue<IntVec3> q = new Queue<IntVec3>();
            q.Enqueue(room.doorInside);
            reach.Add(room.doorInside);
            while (q.Count > 0)
            {
                IntVec3 c = q.Dequeue();
                foreach (IntVec3 o in GenAdj.CardinalDirections)
                {
                    IntVec3 n = c + o;
                    if (reach.Contains(n) || bp.RoomAt(n) != room || !bp.Walkable(n)) continue;
                    reach.Add(n);
                    q.Enqueue(n);
                }
            }
            if (newIc.IsValid && !reach.Contains(newIc)) return false;
            foreach (IntVec3 e in room.extraInside)
            {
                if (bp.RoomAt(e) == room && !reach.Contains(e)) return false;
            }
            foreach (BlueprintPlacement p in bp.placements)
            {
                if (bp.RoomAt(p.center) != room) continue;
                if (p.def.hasInteractionCell)
                {
                    IntVec3 ic = p.center + p.def.interactionCellOffset.RotatedBy(p.rot);
                    if (!reach.Contains(ic)) return false;
                }
                else if (!Touches(GenAdj.OccupiedRect(p.center, p.rot, p.def.size), reach)) return false;
            }
            return Touches(newOcc, reach);
        }

        private static bool Touches(CellRect r, HashSet<IntVec3> reach)
        {
            foreach (IntVec3 c in r.ExpandedBy(1).EdgeCells)
            {
                if (reach.Contains(c)) return true;
            }
            return false;
        }

        private static void PlaceExterior(ShipBlueprint bp, List<ThingDef> defs, BlueprintRequest req)
        {
            List<ThingDef> ext = defs.Where(IsExterior).ToList();
            if (ext.Count == 0) return;
            HashSet<IntVec3> outside = new HashSet<IntVec3>();
            foreach (IntVec3 c in bp.AllCells)
            {
                if (bp.At(c) != BlueprintCell.Empty) continue;
                foreach (IntVec3 o in GenAdj.AdjacentCellsAndInside)
                {
                    BlueprintCell k = bp.At(c + o);
                    if (k == BlueprintCell.Wall || k == BlueprintCell.Window) { outside.Add(c); break; }
                }
            }
            int minZ = int.MaxValue;
            foreach (IntVec3 c in bp.AllCells) if (bp.At(c) == BlueprintCell.Wall) minZ = Math.Min(minZ, c.z);
            int cx = bp.width / 2;
            foreach (ThingDef d in ext.OrderByDescending(x => x.size.x * x.size.z))
            {
                CompProperties_ShipPart sp = d.GetCompProperties<CompProperties_ShipPart>();
                IEnumerable<IntVec3> spots;
                if (sp.role == ShipPartRole.Thruster && req.layout == BlueprintLayout.Ship)
                {
                    spots = Enumerable.Range(0, bp.width).SelectMany(x => Enumerable.Range(0, minZ).Select(z => new IntVec3(x, 0, z))).OrderByDescending(c => c.z).ThenBy(c => Math.Abs(c.x - cx));
                }
                else if (sp.role == ShipPartRole.Weapon)
                {
                    spots = bp.AllCells.Where(c => bp.At(c) == BlueprintCell.Empty).OrderBy(c => -c.z).ThenBy(c => Math.Abs(c.x - cx));
                }
                else if (sp.role == ShipPartRole.Radiator || sp.role == ShipPartRole.Solar)
                {
                    spots = bp.AllCells.Where(c => bp.At(c) == BlueprintCell.Empty).OrderBy(c => -Math.Abs(c.x - cx)).ThenBy(c => Math.Abs(c.z - bp.height / 2));
                }
                else
                {
                    spots = bp.AllCells.Where(c => bp.At(c) == BlueprintCell.Empty).OrderBy(c => Math.Abs(c.z - bp.height / 2));
                }
                bool placed = false;
                foreach (IntVec3 c in spots)
                {
                    CellRect occ = GenAdj.OccupiedRect(c, Rot4.North, d.size);
                    bool ok = true, touches = false;
                    foreach (IntVec3 o in occ)
                    {
                        if (!bp.In(o) || bp.At(o) != BlueprintCell.Empty || bp.occupied.Contains(o)) { ok = false; break; }
                    }
                    if (!ok) continue;
                    if (sp.role == ShipPartRole.Thruster && req.layout == BlueprintLayout.Ship && occ.maxZ >= minZ) continue;
                    foreach (IntVec3 o in occ.ExpandedBy(1).EdgeCells)
                    {
                        if ((o.x < occ.minX || o.x > occ.maxX) && (o.z < occ.minZ || o.z > occ.maxZ)) continue;
                        BlueprintCell k = bp.At(o);
                        if (k == BlueprintCell.Wall || k == BlueprintCell.Window || (k == BlueprintCell.Exterior && sp.role == ShipPartRole.Thruster)) { touches = true; break; }
                    }
                    if (!touches) continue;
                    foreach (IntVec3 o in occ)
                    {
                        bp.cells[o.x, o.z] = BlueprintCell.Exterior;
                        bp.occupied.Add(o);
                    }
                    bp.placements.Add(new BlueprintPlacement { def = d, center = c, rot = Rot4.North });
                    placed = true;
                    break;
                }
                if (!placed) bp.skipped.Add(d.defName);
            }
        }

        private static void Furnish(ShipBlueprint bp, BlueprintRequest req)
        {
            ThingDef lamp = Named("StandingLamp");
            ThingDef table = Named("Table2x2c");
            ThingDef chair = Named("DiningChair");
            ThingDef shelf = Named("Shelf");
            ThingDef hospital = Named("HospitalBed");
            ThingDef vitals = Named("VitalsMonitor");
            foreach (BlueprintRoom room in bp.rooms)
            {
                if (room.role == RoomRole.Mess && table != null)
                {
                    if (TryPlaceInRoom(bp, room, table, ThingDefOf.Steel) && chair != null)
                    {
                        for (int i = 0; i < 4; i++) TryPlaceInRoom(bp, room, chair, ThingDefOf.Steel);
                    }
                }
                if (room.role == RoomRole.Medbay)
                {
                    if (hospital != null) TryPlaceInRoom(bp, room, hospital, ThingDefOf.Steel);
                    if (vitals != null && req.style != BlueprintStyle.Pirate) TryPlaceInRoom(bp, room, vitals, null);
                }
                if ((room.role == RoomRole.Cargo || room.role == RoomRole.Armory) && shelf != null)
                {
                    TryPlaceInRoom(bp, room, shelf, ThingDefOf.Steel);
                }
                if (lamp != null && room.cells.Count >= 6) TryPlaceInRoom(bp, room, lamp, null);
            }
        }

        private static void Damage(ShipBlueprint bp, BlueprintRequest req)
        {
            System.Random r = new System.Random(req.seed ^ 0x5f3759df);
            int breaches = Mathf.CeilToInt(req.damage * 6f);
            List<IntVec3> walls = bp.AllCells.Where(c => bp.At(c) == BlueprintCell.Wall).ToList();
            for (int i = 0; i < breaches && walls.Count > 0; i++)
            {
                IntVec3 c = walls[r.Next(walls.Count)];
                int rad = 1 + r.Next(2);
                foreach (IntVec3 o in GenRadial.RadialPatternInRadius(rad))
                {
                    IntVec3 n = c + o;
                    if (bp.In(n) && r.NextDouble() < 0.75) bp.holes.Add(n);
                }
            }
            if (req.damage > 0.6f)
            {
                int cutZ = bp.height / 2 + r.Next(-bp.height / 6, bp.height / 6 + 1);
                for (int x = 0; x < bp.width; x++)
                {
                    int jag = r.Next(-1, 2);
                    for (int dz = -1; dz <= 1; dz++) bp.holes.Add(new IntVec3(x, 0, cutZ + jag + dz));
                }
            }
            bp.placements.RemoveAll(p => GenAdj.OccupiedRect(p.center, p.rot, p.def.size).Cells.Any(c => bp.holes.Contains(c)));
        }

        public static TerrainDef FloorFor(RoomRole role, BlueprintStyle style)
        {
            string n;
            switch (role)
            {
                case RoomRole.Engineering:
                case RoomRole.Reactor:
                case RoomRole.LifeSupport:
                case RoomRole.Hangar: n = "Rimverse_DeckGrating"; break;
                case RoomRole.Quarters:
                case RoomRole.Cryo: n = style == BlueprintStyle.Pirate || style == BlueprintStyle.Derelict ? "Rimverse_DeckPlating" : "Rimverse_DeckCarpet"; break;
                case RoomRole.Mess:
                case RoomRole.Medbay:
                case RoomRole.Hydroponics: n = "Rimverse_DeckSterileTile"; break;
                case RoomRole.Bridge: n = style == BlueprintStyle.Imperial ? "Rimverse_DeckCarpet" : "Rimverse_DeckSterileTile"; break;
                default: n = "Rimverse_DeckPlating"; break;
            }
            return DefDatabase<TerrainDef>.GetNamedSilentFail(n);
        }
    }
}
