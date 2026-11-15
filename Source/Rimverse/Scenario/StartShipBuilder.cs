using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public enum RimverseStartLocation
    {
        Surface,
        Orbit,
        Belt,
        OuterSystem
    }

    public enum RimverseStartLayout
    {
        None,
        Wreck,
        Drifter,
        Station,
        Refugee,
        Explorer,
        CryoShip,
        Derelict
    }

    public class StartShipSpec
    {
        public int bands = 2;
        public int width = 17;
        public bool armored;
        public List<string> parts = new List<string>();

        public int Height => bands * 6 + 3;

        public StartShipSpec Add(string def, int count = 1)
        {
            for (int i = 0; i < count; i++) parts.Add(def);
            return this;
        }
    }

    public static class StartShipBuilder
    {
        public static StartShipSpec SpecFor(RimverseStartLayout layout, int crew)
        {
            int beds = Mathf.Max(1, crew);
            StartShipSpec s = new StartShipSpec();
            switch (layout)
            {
                case RimverseStartLayout.Drifter:
                    s.bands = 2; s.width = 17;
                    s.Add("Rimverse_BridgeSmall").Add("Rimverse_FuelCell", 2).Add("Rimverse_ChemThruster", 2).Add("Rimverse_ManeuverThruster", 2).Add("Rimverse_ChemTank").Add("Rimverse_AirGenerator").Add("Rimverse_AirTank").Add("Rimverse_RadiatorFixed", 2).Add("Rimverse_Telescope").Add("Rimverse_CargoBay");
                    s.Add("Bed", beds).Add("StandingLamp", 2).Add("Heater").Add("Rimverse_ShipGalley").Add("Rimverse_HydroSemi", 2);
                    break;
                case RimverseStartLayout.Refugee:
                    s.bands = 2; s.width = 23;
                    s.Add("Rimverse_BridgeSmall").Add("Rimverse_FuelCell", 3).Add("Rimverse_ChemThruster", 2).Add("Rimverse_ManeuverThruster", 2).Add("Rimverse_ChemTank").Add("Rimverse_AirGenerator", 2).Add("Rimverse_AirTank").Add("Rimverse_RadiatorFixed", 3).Add("Rimverse_ScannerLongRange").Add("Rimverse_CargoBay").Add("Rimverse_Autocannon");
                    s.Add("Bed", beds).Add("StandingLamp", 3).Add("Heater").Add("Rimverse_ShipGalley").Add("Rimverse_HydroSemi", 2).Add("Rimverse_RelicDisplay");
                    break;
                case RimverseStartLayout.Station:
                    s.bands = 3; s.width = 23;
                    s.Add("Rimverse_BridgeSmall").Add("Rimverse_FuelCell", 2).Add("Rimverse_ShipSolarPanel", 4).Add("Rimverse_BatteryBank", 2).Add("Rimverse_ManeuverThruster", 4).Add("Rimverse_AirGenerator", 2).Add("Rimverse_AirTank", 2).Add("Rimverse_RadiatorFixed", 3).Add("Rimverse_HeatBank").Add("Rimverse_ScannerLongRange").Add("Rimverse_CargoBay", 2);
                    s.Add("Bed", beds).Add("StandingLamp", 4).Add("Heater", 2).Add("Rimverse_ShipGalley").Add("Rimverse_HydroSemi", 3).Add("Rimverse_HoldContainer", 2).Add("Rimverse_ColdShelf");
                    break;
                case RimverseStartLayout.Explorer:
                    s.bands = 4; s.width = 31; s.armored = true;
                    s.Add("Rimverse_BridgeCommand").Add("Rimverse_FuelCell", 5).Add("Rimverse_BatteryBank", 2).Add("Rimverse_ChemThruster", 3).Add("Rimverse_ManeuverThruster", 4).Add("Rimverse_ChemTank", 2).Add("Rimverse_AirGenerator", 2).Add("Rimverse_AirTank", 2).Add("Rimverse_RadiatorFixed", 4).Add("Rimverse_HeatBank");
                    s.Add("Rimverse_ScannerAdvanced").Add("Rimverse_ScannerLongRange").Add("Rimverse_WarpDriveSmall").Add("Rimverse_WarpTank", 2).Add("Rimverse_ShieldGenerator").Add("Rimverse_GravityGenerator").Add("Rimverse_CargoBay");
                    s.Add("Rimverse_HydroAuto", 4).Add("Rimverse_AutoMedPod").Add("Rimverse_MaintenanceBay").Add("Rimverse_AutoGalley").Add("Rimverse_ColdShelf", 2).Add("Rimverse_Vault").Add("Rimverse_DefenseArmory");
                    s.Add("Bed", beds).Add("StandingLamp", 6).Add("Heater", 2).Add("Rimverse_ShipGalley");
                    break;
                case RimverseStartLayout.CryoShip:
                    s.bands = 2; s.width = 21;
                    s.Add("Rimverse_BridgeSmall").Add("Rimverse_FuelCell", 2).Add("Rimverse_ChemThruster", 2).Add("Rimverse_ManeuverThruster", 2).Add("Rimverse_ChemTank").Add("Rimverse_AirGenerator").Add("Rimverse_AirTank").Add("Rimverse_RadiatorFixed", 2).Add("Rimverse_Telescope").Add("Rimverse_CargoBay");
                    s.Add("CryptosleepCasket", beds).Add("Bed", beds).Add("StandingLamp", 2).Add("Heater").Add("Rimverse_ShipGalley");
                    break;
                case RimverseStartLayout.Derelict:
                    s.bands = 2; s.width = 19;
                    s.Add("Rimverse_BridgeSmall").Add("Rimverse_FuelCell", 2).Add("Rimverse_ChemThruster", 2).Add("Rimverse_ManeuverThruster", 2).Add("Rimverse_ChemTank").Add("Rimverse_AirGenerator").Add("Rimverse_AirTank").Add("Rimverse_RadiatorFixed", 2).Add("Rimverse_CargoBay");
                    s.Add("Bed", beds).Add("StandingLamp", 2).Add("Heater");
                    break;
            }
            return s;
        }

        public static Building Build(Map map, IntVec3 center, StartShipSpec spec, float fuel, float hull, out List<IntVec3> freeCells, out CellRect rect)
        {
            freeCells = new List<IntVec3>();
            int w = spec.width;
            int h = spec.Height;
            rect = new CellRect(center.x - w / 2, center.z - h / 2, w, h).ClipInsideMap(map);
            TerrainDef floor = DefDatabase<TerrainDef>.GetNamedSilentFail("Rimverse_HullFloorVacuum");
            ThingDef wall = Named(spec.armored ? "Rimverse_HullWallArmored" : "Rimverse_HullWall") ?? Named("Rimverse_HullWall");
            if (floor == null || wall == null) return null;
            Faction player = Faction.OfPlayer;
            foreach (IntVec3 c in rect)
            {
                foreach (Thing t in c.GetThingList(map).ToList())
                {
                    if (!(t is Pawn) && t.def.destroyable) t.Destroy(DestroyMode.Vanish);
                }
                map.terrainGrid.SetFoundation(c, floor);
            }
            CellRect inner = rect.ContractedBy(1);
            HashSet<IntVec3> lanes = new HashSet<IntVec3>();
            List<int> corridors = new List<int>();
            for (int z = inner.minZ; z <= inner.maxZ; z += 6) corridors.Add(z);
            if (corridors[corridors.Count - 1] != inner.maxZ) corridors.Add(inner.maxZ);
            foreach (IntVec3 c in inner)
            {
                if (corridors.Contains(c.z) || c.x == inner.minX || c.x == inner.maxX) lanes.Add(c);
            }
            IntVec3 doorCell = new IntVec3(rect.minX, 0, corridors[0] + 0);
            foreach (IntVec3 c in rect.EdgeCells)
            {
                if (c == doorCell) continue;
                Spawn(map, wall, c, player, Rot4.North);
            }
            ThingDef door = ThingDefOf.Door;
            Spawn(map, door, doorCell, player, Rot4.North);
            foreach (IntVec3 c in inner) map.roofGrid.SetRoof(c, RoofDefOf.RoofConstructed);
            HashSet<IntVec3> used = new HashSet<IntVec3>();
            Building bridge = null;
            foreach (string defName in spec.parts)
            {
                ThingDef def = Named(defName);
                if (def == null) continue;
                Thing t = Place(map, def, inner, corridors, lanes, used, player);
                if (t == null) continue;
                Prepare(t, fuel, hull);
                if (bridge == null && t is Building b && def.GetCompProperties<CompProperties_ShipPart>()?.role == ShipPartRole.Bridge) bridge = b;
            }
            foreach (IntVec3 c in inner)
            {
                if (used.Contains(c)) continue;
                Spawn(map, ThingDefOf.PowerConduit, c, player, Rot4.North);
                if (c.Standable(map)) freeCells.Add(c);
            }
            map.GetComponent<MapComponent_RimverseShips>()?.Dirty();
            return bridge;
        }

        private static ThingDef Named(string defName) => DefDatabase<ThingDef>.GetNamedSilentFail(defName);

        private static Thing Spawn(Map map, ThingDef def, IntVec3 c, Faction faction, Rot4 rot)
        {
            ThingDef stuff = null;
            if (def.MadeFromStuff)
            {
                stuff = ThingDefOf.Steel;
                if (!GenStuff.AllowedStuffsFor(def).Contains(stuff)) stuff = GenStuff.DefaultStuffFor(def);
            }
            Thing t = ThingMaker.MakeThing(def, stuff);
            if (def.CanHaveFaction) t.SetFaction(faction);
            return GenSpawn.Spawn(t, c, map, rot);
        }

        private static Thing Place(Map map, ThingDef def, CellRect inner, List<int> corridors, HashSet<IntVec3> lanes, HashSet<IntVec3> used, Faction faction)
        {
            for (int z = inner.minZ; z <= inner.maxZ; z++)
            {
                for (int x = inner.minX; x <= inner.maxX; x++)
                {
                    IntVec3 center = new IntVec3(x, 0, z);
                    CellRect occ = GenAdj.OccupiedRect(center, Rot4.North, def.size);
                    if (!Fits(occ, inner, corridors, lanes, used)) continue;
                    if (def.hasInteractionCell)
                    {
                        IntVec3 ic = ThingUtility.InteractionCellWhenAt(def, center, Rot4.North, map);
                        if (!lanes.Contains(ic)) continue;
                    }
                    Thing t = Spawn(map, def, center, faction, Rot4.North);
                    foreach (IntVec3 c in occ) used.Add(c);
                    return t;
                }
            }
            return null;
        }

        private static bool Fits(CellRect occ, CellRect inner, List<int> corridors, HashSet<IntVec3> lanes, HashSet<IntVec3> used)
        {
            if (occ.minX < inner.minX || occ.maxX > inner.maxX || occ.minZ < inner.minZ || occ.maxZ > inner.maxZ) return false;
            foreach (IntVec3 c in occ)
            {
                if (lanes.Contains(c) || used.Contains(c)) return false;
            }
            return corridors.Contains(occ.minZ - 1) || corridors.Contains(occ.maxZ + 1);
        }

        private static void Prepare(Thing t, float fuel, float hull)
        {
            CompRefuelable refuel = t.TryGetComp<CompRefuelable>();
            if (refuel != null)
            {
                float want = refuel.Props.fuelCapacity * Mathf.Clamp01(fuel) - refuel.Fuel;
                if (want > 0f) refuel.Refuel(want);
            }
            CompShipShield shield = t.TryGetComp<CompShipShield>();
            if (shield != null) shield.hitPoints = shield.Props.maxHitPoints;
            CompFlickable flick = t.TryGetComp<CompFlickable>();
            if (flick != null) flick.SwitchIsOn = true;
            if (hull < 1f && t.def.useHitPoints) t.HitPoints = Mathf.Max(1, Mathf.RoundToInt(t.MaxHitPoints * Mathf.Clamp(hull * Rand.Range(0.8f, 1.2f), 0.05f, 1f)));
        }

        public static void ScatterWreck(Map map, IntVec3 near)
        {
            Faction player = Faction.OfPlayer;
            string[] parts = { "Rimverse_BridgeSmall", "Rimverse_ChemThruster", "Rimverse_FuelCell", "Rimverse_AirGenerator", "Rimverse_AirTank", "Rimverse_ChemTank", "Rimverse_ManeuverThruster", "Rimverse_ManeuverThruster" };
            foreach (string name in parts)
            {
                ThingDef def = Named(name);
                if (def == null) continue;
                for (int tries = 0; tries < 60; tries++)
                {
                    if (!CellFinder.TryFindRandomCellNear(near, map, 22, c => c.Standable(map), out IntVec3 cell)) break;
                    CellRect occ = GenAdj.OccupiedRect(cell, Rot4.North, def.size);
                    if (!occ.InBounds(map) || occ.Cells.Any(c => !c.Standable(map) || c.GetEdifice(map) != null || c.GetFirstPawn(map) != null || c.GetFirstItem(map) != null)) continue;
                    Thing t = Spawn(map, def, cell, player, Rot4.North);
                    Prepare(t, Rand.Range(0f, 0.25f), Rand.Range(0.2f, 0.5f));
                    CompFlickable flick = t.TryGetComp<CompFlickable>();
                    if (flick != null) flick.SwitchIsOn = false;
                    break;
                }
            }
            ThingDef chunk = Named("ShipChunk");
            ThingDef slag = Named("ChunkSlagSteel");
            for (int i = 0; i < 14; i++)
            {
                ThingDef def = i < 5 ? chunk : slag;
                if (def == null) continue;
                if (!CellFinder.TryFindRandomCellNear(near, map, 26, c => c.Standable(map) && c.GetEdifice(map) == null && c.GetFirstItem(map) == null && c.GetFirstPawn(map) == null, out IntVec3 cell)) continue;
                GenSpawn.Spawn(ThingMaker.MakeThing(def), cell, map);
            }
        }
    }
}
