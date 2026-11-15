using System.Collections.Generic;
using Verse;

namespace Rimverse
{
    public enum ScanLevel
    {
        Unknown,
        Word,
        Range,
        Exact
    }

    public class GameComponent_Universe : GameComponent
    {
        public int universeSeed;
        public string homeSystemKey;
        private Dictionary<string, int> catalog = new Dictionary<string, int>();
        private Dictionary<string, string> renamedBodies = new Dictionary<string, string>();
        private Dictionary<string, int> systemCatalog = new Dictionary<string, int>();
        public const int ScanInterval = 15000;
        private readonly Dictionary<StarSystemId, StarSystemData> cache = new Dictionary<StarSystemId, StarSystemData>();
        private readonly Dictionary<int, GalaxyData> galaxies = new Dictionary<int, GalaxyData>();
        private StarSystemId homeIdCached = StarSystemId.Invalid;

        public static GameComponent_Universe Instance => Current.Game?.GetComponent<GameComponent_Universe>();

        public GameComponent_Universe(Game game)
        {
        }

        public StarSystemId HomeSystemId
        {
            get
            {
                EnsureInitialized();
                if (!homeIdCached.Valid) homeIdCached = StarSystemId.FromKey(homeSystemKey);
                return homeIdCached;
            }
        }

        public bool IsHome(StarSystemId id) => !homeSystemKey.NullOrEmpty() && id.Key == homeSystemKey;

        public bool IsHomeDistrict(DistrictId d)
        {
            if (homeSystemKey.NullOrEmpty()) return false;
            return HomeSystemId.District.Equals(d);
        }

        public int GalaxyCount => GalaxyGenerator.GroupSize(universeSeed);

        public GalaxyData Galaxy(int index)
        {
            if (galaxies.TryGetValue(index, out GalaxyData g)) return g;
            g = GalaxyGenerator.Generate(universeSeed, index);
            galaxies[index] = g;
            return g;
        }

        public Vec3d PositionOf(StarSystemId id) => GalaxyQuery.PositionOf(this, id);

        public double DistanceLy(StarSystemId a, StarSystemId b)
        {
            if (a.galaxy != b.galaxy) return double.PositiveInfinity;
            return Vec3d.Distance(PositionOf(a), PositionOf(b));
        }

        public StarSystemData HomeSystem => GetSystem(HomeSystemId);

        public void EnsureInitialized()
        {
            if (!homeSystemKey.NullOrEmpty()) return;
            universeSeed = Find.World?.info != null ? Find.World.info.Seed : Rand.Int;
            UniverseRand rand = new UniverseRand(UniverseRand.Hash(universeSeed, 1));
            GalaxyData g = Galaxy(0);
            double angle = rand.ValueDouble * System.Math.PI * 2.0;
            double r = g.radiusLy * (0.45 + 0.15 * rand.ValueDouble);
            Vec3d pos = new Vec3d(System.Math.Cos(angle) * r, 0.0, System.Math.Sin(angle) * r);
            DistrictId d = DistrictId.FromPosition(0, pos);
            StarSystemId home = new StarSystemId(0, d.sx, d.sy, d.sz, d.dx, d.dy, d.dz, 0);
            homeSystemKey = home.Key;
            homeIdCached = home;
            GalaxyQuery.ClearCache();
            StarSystemData sys = GetSystem(home);
            foreach (BodyData b in sys.bodies)
            {
                SetScan(home, b.index, b.homeWorld ? ScanLevel.Exact : ScanLevel.Word);
            }
        }

        public StarSystemData GetSystem(StarSystemId id)
        {
            if (!id.Valid) return null;
            if (cache.TryGetValue(id, out StarSystemData data)) return data;
            bool home = IsHome(id);
            string homeName = Find.World?.info?.name ?? "Rim";
            data = StarSystemGenerator.Generate(id, universeSeed, home, homeName);
            foreach (BodyData b in data.bodies)
            {
                if (renamedBodies.TryGetValue(BodyKey(id, b.index), out string n)) b.name = n;
            }
            cache[id] = data;
            return data;
        }

        public static string BodyKey(StarSystemId id, int body) => id.Key + "#" + body;

        public ScanLevel GetScan(StarSystemId id, int body) => catalog.TryGetValue(BodyKey(id, body), out int v) ? (ScanLevel)v : ScanLevel.Unknown;

        public void SetScan(StarSystemId id, int body, ScanLevel level)
        {
            string key = BodyKey(id, body);
            if (catalog.TryGetValue(key, out int cur) && cur >= (int)level) return;
            catalog[key] = (int)level;
            if (level == ScanLevel.Exact) EmpireAnomalies.OnScanned(id, body);
        }

        public int CatalogCount => catalog.Count;

        public int SystemCatalogCount => systemCatalog.Count;

        public ScanLevel GetSystemScan(StarSystemId id)
        {
            if (IsHome(id)) return ScanLevel.Exact;
            return systemCatalog.TryGetValue(id.Key, out int v) ? (ScanLevel)v : ScanLevel.Unknown;
        }

        public void SetSystemScan(StarSystemId id, ScanLevel level)
        {
            if (systemCatalog.TryGetValue(id.Key, out int cur) && cur >= (int)level) return;
            systemCatalog[id.Key] = (int)level;
            IdeoEvents.NoteNewScan();
            if (level >= ScanLevel.Range)
            {
                StarSystemData sys = GetSystem(id);
                ScanLevel bodyLevel = level == ScanLevel.Exact ? ScanLevel.Range : ScanLevel.Word;
                foreach (BodyData b in sys.bodies) SetScan(id, b.index, bodyLevel);
            }
        }

        public static void SensorReach(float sensitivity, out double rangeLy, out ScanLevel level)
        {
            if (sensitivity > 0f && sensitivity <= 100f) { rangeLy = 50; level = ScanLevel.Exact; return; }
            if (sensitivity > 0f && sensitivity <= 1000f) { rangeLy = 20; level = ScanLevel.Range; return; }
            if (sensitivity > 0f && sensitivity <= 10000f) { rangeLy = 5; level = ScanLevel.Word; return; }
            rangeLy = 0;
            level = ScanLevel.Unknown;
        }

        public override void GameComponentTick()
        {
            int tick = Find.TickManager.TicksGame;
            if (tick % 60 == 11) WarpTravel.CheckArrivals();
            if (tick % ScanInterval != 37) return;
            EnsureInitialized();
            foreach (Map map in Find.Maps)
            {
                MapComponent_RimverseShips ships = map.GetComponent<MapComponent_RimverseShips>();
                MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(map);
                if (ships == null || loc == null || loc.Warping) continue;
                float best = 0f;
                bool any = false;
                foreach (RimverseShip ship in ships.Ships)
                {
                    if (ship.bridge == null || ship.bridge.Faction != RimWorld.Faction.OfPlayer) continue;
                    any = true;
                    if (ship.sensorSensitivity > 0f && (best <= 0f || ship.sensorSensitivity < best)) best = ship.sensorSensitivity;
                }
                if (!any) continue;
                StarSystemId here = loc.SystemId;
                SetSystemScan(here, ScanLevel.Range);
                SensorReach(best, out double range, out ScanLevel level);
                if (range <= 0) continue;
                foreach (StarEntry e in GalaxyQuery.StarsInRadius(this, here.galaxy, PositionOf(here), range))
                {
                    SetSystemScan(e.id, level);
                }
            }
        }

        public void RenameBody(StarSystemId id, int body, string name)
        {
            renamedBodies[BodyKey(id, body)] = name;
            BodyData b = GetSystem(id)?.Body(body);
            if (b != null) b.name = name;
        }

        public override void FinalizeInit()
        {
            EnsureInitialized();
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref universeSeed, "universeSeed");
            Scribe_Values.Look(ref homeSystemKey, "homeSystemKey");
            Scribe_Collections.Look(ref catalog, "catalog", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref renamedBodies, "renamedBodies", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref systemCatalog, "systemCatalog", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (catalog == null) catalog = new Dictionary<string, int>();
                if (renamedBodies == null) renamedBodies = new Dictionary<string, string>();
                if (systemCatalog == null) systemCatalog = new Dictionary<string, int>();
                cache.Clear();
                galaxies.Clear();
                homeIdCached = StarSystemId.Invalid;
                GalaxyQuery.ClearCache();
            }
        }
    }
}
