using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Rimverse
{
    public static class BodyMapUtility
    {
        public static Map AnchorMap(Map fallback)
        {
            Map home = Find.Maps.FirstOrDefault(m => m.IsPlayerHome && !(m.Parent is PocketMapParent) && !SpaceMapUtility.IsSpace(m) && !EndlessRoomsCompat.IsBackrooms(m));
            return home ?? Find.AnyPlayerHomeMap ?? fallback;
        }

        public static Map FindBodyMap(StarSystemId system, int body)
        {
            foreach (Map m in Find.Maps)
            {
                MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(m);
                if (loc == null || !loc.OnSurface || loc.bodyIndex != body) continue;
                if (loc.SystemId == system) return m;
            }
            return null;
        }

        public static IntVec3 SizeFor(BodyData body)
        {
            if (body.type == BodyType.Asteroid || body.type == BodyType.Belt) return new IntVec3(100, 1, 100);
            if (body.sizeClass < 15) return new IntVec3(120, 1, 120);
            if (body.atmosphere == AtmosphereType.Breathable) return new IntVec3(200, 1, 200);
            return new IntVec3(170, 1, 170);
        }

        public static string BiomeFor(BodyData body)
        {
            float t = body.temperatureC;
            float w = body.water;
            if (body.type == BodyType.Ocean || w > 0.85f)
            {
                if (t < -5f) return "SeaIce";
                if (t < 12f) return "ColdBog";
                if (t < 24f) return "TemperateSwamp";
                return "TropicalSwamp";
            }
            if (t < -20f) return "IceSheet";
            if (t < -6f) return "Tundra";
            if (t < 4f) return w > 0.3f ? "BorealForest" : "Tundra";
            if (t < 18f) return w > 0.35f ? "TemperateForest" : "AridShrubland";
            if (t < 27f)
            {
                if (w > 0.6f) return "TropicalRainforest";
                if (w > 0.3f) return "TemperateForest";
                return "AridShrubland";
            }
            if (w > 0.55f) return "TropicalRainforest";
            if (t > 40f || w < 0.12f) return "ExtremeDesert";
            return "Desert";
        }

        public static MapGeneratorDef GeneratorFor(BodyData body)
        {
            string special = BiomeFeatures.Pick(body, BodyGenContext.systemKey);
            if (special != null)
            {
                MapGeneratorDef sp = DefDatabase<MapGeneratorDef>.GetNamedSilentFail("Rimverse_Surface_" + special);
                if (sp != null) return sp;
            }
            if (body.Airless) return RimverseDefOf.Rimverse_AirlessBodyMap;
            if (body.atmosphere == AtmosphereType.Breathable)
            {
                MapGeneratorDef def = DefDatabase<MapGeneratorDef>.GetNamedSilentFail("Rimverse_Surface_" + BiomeFor(body));
                return def ?? DefDatabase<MapGeneratorDef>.GetNamedSilentFail("Rimverse_Surface_TemperateForest") ?? RimverseDefOf.Rimverse_HostileBodyMap;
            }
            return RimverseDefOf.Rimverse_HostileBodyMap;
        }

        public static Map GetOrCreateBodyMap(Map from, StarSystemId system, BodyData body)
        {
            Map existing = FindBodyMap(system, body.index);
            if (existing != null) return existing;
            Map anchor = AnchorMap(from);
            BodyGenContext.current = body;
            BodyGenContext.systemKey = system.Key;
            Map map;
            try
            {
                map = TryGenerateOnLayer(system, body) ?? PocketMapUtility.GeneratePocketMap(SizeFor(body), GeneratorFor(body), SettlementGen.ExtraFor(system, body), anchor);
            }
            finally
            {
                BodyGenContext.current = null;
                BodyGenContext.systemKey = null;
            }
            MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(map);
            loc.SetLocation(system, body.index);
            loc.SetSurface(true);
            return map;
        }

        private static Map TryGenerateOnLayer(StarSystemId system, BodyData body)
        {
            GameComponent_Universe u = GameComponent_Universe.Instance;
            if (u == null || !u.IsHome(system) || !body.Airless) return null;
            PlanetLayer layer = GameComponent_OdysseyLayers.LayerForBody(body);
            if (layer == null) return null;
            PlanetTile tile = PlanetTile.Invalid;
            for (int tries = 0; tries < 200; tries++)
            {
                PlanetTile t = layer.PlanetTileForID(Rand.Range(0, layer.TilesCount));
                if (!Find.WorldObjects.AnyWorldObjectAt(t))
                {
                    tile = t;
                    break;
                }
            }
            if (!tile.Valid) return null;
            Settlement home = SettleUtility.AddNewHome(tile, Faction.OfPlayer);
            return MapGenerator.GenerateMap(SizeFor(body), home, GeneratorFor(body), home.ExtraGenStepDefs.Concat(SettlementGen.ExtraFor(system, body)), null, false);
        }

        public static Map CreateOrbitPocket(Map from, IntVec3 size)
        {
            MapComponent_ShipLocation src = MapComponent_ShipLocation.For(from);
            Map map = PocketMapUtility.GeneratePocketMap(size, RimverseDefOf.Rimverse_OrbitPocket, null, AnchorMap(from));
            MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(map);
            if (src != null) loc.SetLocation(src.SystemId, src.bodyIndex);
            loc.SetSurface(false);
            return map;
        }

        public static void CleanupIfEmpty(Map map)
        {
            if (map == null || !(map.Parent is PocketMapParent)) return;
            if (!SpaceMapUtility.IsOrbit(map)) return;
            if (map.mapPawns.AnyPawnBlockingMapRemoval) return;
            MapComponent_RimverseShips ships = map.GetComponent<MapComponent_RimverseShips>();
            if (ships != null && ships.Ships.Any(s => s.bridge != null && s.bridge.Faction == Faction.OfPlayer)) return;
            if (map.listerBuildings.allBuildingsColonist.Count > 0) return;
            PocketMapUtility.DestroyPocketMap(map);
        }
    }

    public static class BodyGenContext
    {
        public static BodyData current;
        public static string systemKey;
    }
}
