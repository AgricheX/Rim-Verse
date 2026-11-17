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
        public int width = 17;
        public bool armored;
        public bool station;
        public List<string> parts = new List<string>();

        public int SizeClass => width <= 17 ? 1 : width <= 23 ? 2 : 3;

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
                    s.width = 17;
                    s.Add("Rimverse_BridgeSmall").Add("Rimverse_FuelCell", 2).Add("Rimverse_ChemThruster", 2).Add("Rimverse_ManeuverThruster", 2).Add("Rimverse_ChemTank").Add("Rimverse_AirGenerator").Add("Rimverse_AirTank").Add("Rimverse_RadiatorFixed", 2).Add("Rimverse_Telescope").Add("Rimverse_CargoBay");
                    s.Add("Bed", beds).Add("StandingLamp", 2).Add("Heater").Add("Rimverse_ShipGalley").Add("Rimverse_HydroSemi", 2);
                    break;
                case RimverseStartLayout.Refugee:
                    s.width = 23;
                    s.Add("Rimverse_BridgeSmall").Add("Rimverse_FuelCell", 3).Add("Rimverse_ChemThruster", 2).Add("Rimverse_ManeuverThruster", 2).Add("Rimverse_ChemTank").Add("Rimverse_AirGenerator", 2).Add("Rimverse_AirTank").Add("Rimverse_RadiatorFixed", 3).Add("Rimverse_ScannerLongRange").Add("Rimverse_CargoBay").Add("Rimverse_Autocannon");
                    s.Add("Bed", beds).Add("StandingLamp", 3).Add("Heater").Add("Rimverse_ShipGalley").Add("Rimverse_HydroSemi", 2).Add("Rimverse_RelicDisplay");
                    break;
                case RimverseStartLayout.Station:
                    s.width = 23; s.station = true;
                    s.Add("Rimverse_BridgeSmall").Add("Rimverse_FuelCell", 2).Add("Rimverse_ShipSolarPanel", 4).Add("Rimverse_BatteryBank", 2).Add("Rimverse_ManeuverThruster", 4).Add("Rimverse_AirGenerator", 2).Add("Rimverse_AirTank", 2).Add("Rimverse_RadiatorFixed", 3).Add("Rimverse_HeatBank").Add("Rimverse_ScannerLongRange").Add("Rimverse_CargoBay", 2);
                    s.Add("Bed", beds).Add("StandingLamp", 4).Add("Heater", 2).Add("Rimverse_ShipGalley").Add("Rimverse_HydroSemi", 3).Add("Rimverse_HoldContainer", 2).Add("Rimverse_ColdShelf");
                    break;
                case RimverseStartLayout.Explorer:
                    s.width = 31; s.armored = true;
                    s.Add("Rimverse_BridgeCommand").Add("Rimverse_FuelCell", 5).Add("Rimverse_BatteryBank", 2).Add("Rimverse_ChemThruster", 3).Add("Rimverse_ManeuverThruster", 4).Add("Rimverse_ChemTank", 2).Add("Rimverse_AirGenerator", 2).Add("Rimverse_AirTank", 2).Add("Rimverse_RadiatorFixed", 4).Add("Rimverse_HeatBank");
                    s.Add("Rimverse_ScannerAdvanced").Add("Rimverse_ScannerLongRange").Add("Rimverse_WarpDriveSmall").Add("Rimverse_WarpTank", 2).Add("Rimverse_ShieldGenerator").Add("Rimverse_GravityGenerator").Add("Rimverse_CargoBay");
                    s.Add("Rimverse_HydroAuto", 4).Add("Rimverse_AutoMedPod").Add("Rimverse_MaintenanceBay").Add("Rimverse_AutoGalley").Add("Rimverse_ColdShelf", 2).Add("Rimverse_Vault").Add("Rimverse_DefenseArmory");
                    s.Add("Bed", beds).Add("StandingLamp", 6).Add("Heater", 2).Add("Rimverse_ShipGalley");
                    break;
                case RimverseStartLayout.CryoShip:
                    s.width = 21;
                    s.Add("Rimverse_BridgeSmall").Add("Rimverse_FuelCell", 2).Add("Rimverse_ChemThruster", 2).Add("Rimverse_ManeuverThruster", 2).Add("Rimverse_ChemTank").Add("Rimverse_AirGenerator").Add("Rimverse_AirTank").Add("Rimverse_RadiatorFixed", 2).Add("Rimverse_Telescope").Add("Rimverse_CargoBay");
                    s.Add("CryptosleepCasket", beds).Add("Bed", beds).Add("StandingLamp", 2).Add("Heater").Add("Rimverse_ShipGalley");
                    break;
                case RimverseStartLayout.Derelict:
                    s.width = 19;
                    s.Add("Rimverse_BridgeSmall").Add("Rimverse_FuelCell", 2).Add("Rimverse_ChemThruster", 2).Add("Rimverse_ManeuverThruster", 2).Add("Rimverse_ChemTank").Add("Rimverse_AirGenerator").Add("Rimverse_AirTank").Add("Rimverse_RadiatorFixed", 2).Add("Rimverse_CargoBay");
                    s.Add("Bed", beds).Add("StandingLamp", 2).Add("Heater");
                    break;
            }
            return s;
        }

        public static BlueprintRequest RequestFor(StartShipSpec spec, RimverseStartLayout layout, int seed)
        {
            BlueprintRequest req = new BlueprintRequest
            {
                layout = spec.station ? BlueprintLayout.Station : BlueprintLayout.Ship,
                style = layout == RimverseStartLayout.Derelict ? BlueprintStyle.Derelict : layout == RimverseStartLayout.Explorer ? BlueprintStyle.Military : BlueprintStyle.Civil,
                sizeClass = spec.SizeClass,
                armored = spec.armored,
                seed = seed,
                damage = layout == RimverseStartLayout.Derelict ? 0.25f : 0f
            };
            req.parts.AddRange(spec.parts);
            return req;
        }

        public static Building Build(Map map, IntVec3 center, StartShipSpec spec, RimverseStartLayout layout, float fuel, float hull, out List<IntVec3> freeCells, out CellRect rect)
        {
            freeCells = new List<IntVec3>();
            int seed = Gen.HashCombineInt(Find.World?.info?.Seed ?? 0, (int)layout * 7919 + spec.parts.Count);
            ShipBlueprint bp = ShipBlueprintGenerator.Generate(RequestFor(spec, layout, seed));
            rect = ShipBlueprintBuilder.RectAt(center, bp).ClipInsideMap(map);
            if (bp.skipped.Count > 0) RimverseLog.Message("Start ship: no room for " + string.Join(", ", bp.skipped));
            BlueprintBuildResult res = ShipBlueprintBuilder.Build(map, ShipBlueprintBuilder.RectAt(center, bp), bp, new BlueprintBuildOptions { faction = Faction.OfPlayer, fuel = fuel, hull = hull });
            if (res == null) return null;
            freeCells = res.freeCells;
            map.GetComponent<MapComponent_RimverseShips>()?.Dirty();
            return res.bridge;
        }

        private static ThingDef Named(string defName) => DefDatabase<ThingDef>.GetNamedSilentFail(defName);

        public static void ScatterWreck(Map map, IntVec3 near)
        {
            BlueprintRequest req = new BlueprintRequest { style = BlueprintStyle.Derelict, sizeClass = 1, damage = 0.8f, seed = Gen.HashCombineInt(map.Tile.tileId, 4441) };
            req.Add("Rimverse_BridgeSmall").Add("Rimverse_ChemThruster", 2).Add("Rimverse_FuelCell").Add("Rimverse_AirGenerator").Add("Rimverse_AirTank").Add("Rimverse_ChemTank").Add("Rimverse_ManeuverThruster", 2).Add("Rimverse_CargoBay").Add("Bed", 2);
            ShipBlueprint bp = ShipBlueprintGenerator.Generate(req);
            IntVec3 center = near + new IntVec3(Rand.RangeInclusive(-14, 14), 0, Rand.RangeInclusive(10, 18));
            CellRect rect = ShipBlueprintBuilder.RectAt(center, bp);
            if (!ShipBlueprintBuilder.Fits(map, rect))
            {
                center = near;
                rect = ShipBlueprintBuilder.RectAt(center, bp);
                if (!ShipBlueprintBuilder.Fits(map, rect)) return;
            }
            foreach (IntVec3 c in rect)
            {
                if (c.GetFirstPawn(map) != null) return;
            }
            BlueprintBuildResult res = ShipBlueprintBuilder.Build(map, rect, bp, new BlueprintBuildOptions { faction = Faction.OfPlayer, fuel = Rand.Range(0f, 0.25f), hull = Rand.Range(0.2f, 0.5f), powerOn = false, vacuumFloor = false });
            if (res == null) return;
            foreach (IntVec3 c in rect.ExpandedBy(3))
            {
                if (!c.InBounds(map) || rect.Contains(c) && map.terrainGrid.FoundationAt(c) != null) continue;
                if (Rand.Chance(0.25f)) FilthMaker.TryMakeFilth(c, map, ThingDefOf.Filth_Ash, 1);
            }
        }
    }
}
