using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public static class UnseenMaps
    {
        private static bool[] paused = new bool[0];
        private static int lastTick = -1;

        public static bool ShouldPause(Map m)
        {
            if (m == null || m == Find.CurrentMap || m.IsPlayerHome) return false;
            if (m.mapPawns.FreeColonistsSpawnedCount > 0) return false;
            if (m.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer).Count > 0) return false;
            if (m.Parent is RimWorld.Planet.Caravan) return false;
            return true;
        }

        public static void Refresh()
        {
            int now = Find.TickManager.TicksGame;
            if (now == lastTick) return;
            lastTick = now;
            List<Map> maps = Find.Maps;
            if (paused.Length != maps.Count) paused = new bool[maps.Count];
            bool on = RimverseMod.Settings.pauseUnseenMaps;
            for (int i = 0; i < maps.Count; i++) paused[i] = on && (now % 60 == 0 ? ShouldPause(maps[i]) : paused[i]);
        }

        public static bool Paused(int index) => index >= 0 && index < paused.Length && paused[index];

        public static bool Paused(Map m) => m != null && Paused(Find.Maps.IndexOf(m));
    }

    [HarmonyPatch(typeof(TickManager), nameof(TickManager.DoSingleTick))]
    public static class Patch_UnseenMapsRefresh
    {
        public static void Prefix() => UnseenMaps.Refresh();
    }

    [HarmonyPatch(typeof(Map), nameof(Map.MapPreTick))]
    public static class Patch_UnseenMapsPre
    {
        public static bool Prefix(Map __instance) => !RimverseMod.Settings.pauseUnseenMaps || !UnseenMaps.Paused(__instance);
    }

    [HarmonyPatch(typeof(Map), nameof(Map.MapPostTick))]
    public static class Patch_UnseenMapsPost
    {
        public static bool Prefix(Map __instance) => !RimverseMod.Settings.pauseUnseenMaps || !UnseenMaps.Paused(__instance);
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.DoTick))]
    public static class Patch_UnseenMapsThing
    {
        public static bool Prefix(sbyte ___mapIndexOrState)
        {
            if (!RimverseMod.Settings.pauseUnseenMaps) return true;
            return !UnseenMaps.Paused(___mapIndexOrState);
        }
    }

    public static class SignatureUtility
    {
        public const float Warn1 = 400000f;
        public const float Warn2 = 600000f;
        public const float Threat = 800000f;

        public static float TechSight(TechLevel t)
        {
            switch (t)
            {
                case TechLevel.Archotech: return 1.6f;
                case TechLevel.Ultra: return 1.3f;
                case TechLevel.Spacer: return 1f;
                default: return 0.75f;
            }
        }

        public static float DetectChance(RimverseShip ship, TechLevel hunterTech)
        {
            if (ship == null) return 1f;
            return Mathf.Lerp(0.3f, 1f, Mathf.Clamp01(ship.Signature * TechSight(hunterTech)));
        }

        public static float EarlyWarning(RimverseShip ship)
        {
            if (ship == null) return 0f;
            int n = ship.CountRole(ShipPartRole.SignatureDetector, true) * 2 + ship.CountRole(ShipPartRole.Sensor, true);
            return Mathf.Clamp(0.1f * n, 0f, 0.5f);
        }

        public static float ColonyPoints(Map map)
        {
            if (map == null) return 0f;
            float power = 0f;
            foreach (PowerNet net in map.powerNetManager.AllNetsListForReading)
            {
                foreach (CompPowerTrader t in net.powerComps) if (t.PowerOn && t.PowerOutput > 0f) power += t.PowerOutput;
            }
            float heat = 0f;
            foreach (Building b in map.listerBuildings.allBuildingsColonist)
            {
                CompHeatPusher h = b.TryGetComp<CompHeatPusher>();
                if (h != null && h.ShouldPushHeatNow) heat += h.Props.heatPerSecond;
            }
            return map.wealthWatcher.WealthTotal * 1.5f + power * 8f + heat * 40f;
        }

        public static float ShipPoints(Map map)
        {
            RimverseShip ship = ColonyUtility.PlayerShipOn(map);
            return ship == null ? 0f : ship.Signature * 400000f;
        }

        public static float Total(out Map loudest)
        {
            loudest = null;
            float best = -1f;
            float total = 0f;
            foreach (Map m in Find.Maps)
            {
                if (!m.IsPlayerHome) continue;
                float v = ColonyPoints(m) + ShipPoints(m);
                total += v;
                if (v > best) { best = v; loudest = m; }
            }
            GameComponent_Visibility vis = GameComponent_Visibility.Instance;
            if (vis != null) total += vis.jumpHeat;
            if (RimGramBridge.Ready) total += Mathf.Max(0, RimGramBridge.Reputation) * 1500f;
            return total;
        }
    }

    public class GameComponent_Visibility : GameComponent
    {
        public float jumpHeat;
        public int level;
        public int nextThreatTick;
        public float lastTotal;

        public static GameComponent_Visibility Instance => Current.Game?.GetComponent<GameComponent_Visibility>();

        public GameComponent_Visibility(Game game)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref jumpHeat, "jumpHeat");
            Scribe_Values.Look(ref level, "level");
            Scribe_Values.Look(ref nextThreatTick, "nextThreatTick");
            Scribe_Values.Look(ref lastTotal, "lastTotal");
        }

        public void RecordJump() => jumpHeat = Mathf.Min(jumpHeat + 120000f, 360000f);

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (now % 2500 != 1234) return;
            jumpHeat = Mathf.Max(0f, jumpHeat - 3000f);
            lastTotal = SignatureUtility.Total(out Map loudest);
            int newLevel = lastTotal >= SignatureUtility.Threat ? 3 : lastTotal >= SignatureUtility.Warn2 ? 2 : lastTotal >= SignatureUtility.Warn1 ? 1 : 0;
            if (newLevel > level && newLevel < 3) Messages.Message(("Rimverse_VisibilityWarn" + newLevel).Translate(lastTotal.ToString("N0")), MessageTypeDefOf.CautionInput, false);
            level = newLevel;
            if (level >= 3 && now >= nextThreatTick && loudest != null)
            {
                nextThreatTick = now + 15 * GenDate.TicksPerDay;
                FireThreat(loudest);
            }
        }

        private void FireThreat(Map map)
        {
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, map);
            parms.points *= 1.25f;
            IncidentDef ambush = DefDatabase<IncidentDef>.GetNamedSilentFail("Rimverse_ShipAmbush");
            bool ok = false;
            if (ambush != null && ColonyUtility.PlayerShipOn(map) != null && ambush.Worker.CanFireNow(parms)) ok = ambush.Worker.TryExecute(parms);
            if (!ok && IncidentDefOf.RaidEnemy.Worker.CanFireNow(parms)) ok = IncidentDefOf.RaidEnemy.Worker.TryExecute(parms);
            if (ok) Find.LetterStack.ReceiveLetter("Rimverse_VisibilityThreatLabel".Translate(), "Rimverse_VisibilityThreat".Translate(), LetterDefOf.ThreatBig, new TargetInfo(map.Center, map));
        }
    }

    [HarmonyPatch(typeof(WarpTravel), nameof(WarpTravel.Arrive))]
    public static class Patch_VisibilityJump
    {
        public static void Postfix() => GameComponent_Visibility.Instance?.RecordJump();
    }

    public static class CommRelay
    {
        public static bool On(Map map)
        {
            if (map == null) return false;
            ThingDef d = DefDatabase<ThingDef>.GetNamedSilentFail("Rimverse_CommRelay");
            if (d == null) return false;
            foreach (Building b in map.listerBuildings.AllBuildingsColonistOfDef(d))
            {
                CompPowerTrader p = b.TryGetComp<CompPowerTrader>();
                if (p == null || p.PowerOn) return true;
            }
            return false;
        }

        public static bool AnyOn() => Find.Maps.Any(m => m.IsPlayerHome && On(m));
    }

    public static class OrbitalTraffic
    {
        private static Texture2D dot;
        private static readonly Dictionary<int, Texture2D> stations = new Dictionary<int, Texture2D>();

        public static void AddTo(MapComponent_SpaceBackground bg, StarSystemData sys, BodyData body, Vector2 sunDir, float mapW)
        {
            if (sys == null || body == null) return;
            Civilization civ = SettlementGen.CivOn(sys.id.Key, body.index, out CivPlanet planet);
            if (civ == null) return;
            bool seat = civ.capitalKey == sys.id.Key;
            if (dot == null) dot = SpaceBackgroundArt.Glow(32, 0.18f, 2.4f);
            Vector2 planetOff = -sunDir * mapW * 0.58f;
            float disk = mapW * MapComponent_SpaceBackground.PlanetScale(body, 0.62f);
            System.Random r = new System.Random(sys.id.Key.GetHashCode() ^ body.index * 131);
            int stationCount = seat ? 3 : 1;
            for (int i = 0; i < stationCount; i++)
            {
                int seed = r.Next(1, 9999);
                if (!stations.TryGetValue(seed, out Texture2D t) || t == null)
                {
                    if (stations.Count > 12) stations.Clear();
                    t = MegastructureArt.Cylinder(256, 1f, seed);
                    stations[seed] = t;
                }
                float ang = (float)r.NextDouble() * Mathf.PI * 2f;
                Vector2 off = planetOff + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) * 0.45f) * disk * (0.62f + 0.12f * i);
                bg.AddSprite(bg.NewMat(t, ShaderDatabase.Transparent, Color.Lerp(Color.white, civ.color, 0.25f), Mathf.Sin(ang) > 0f ? 1003 : 1005), off, mapW * (seat ? 0.06f : 0.045f), (float)r.NextDouble() * 360f, 0.16f);
            }
            int ships = Mathf.RoundToInt((seat ? 16 : 6) * Mathf.Clamp(0.5f + (planet?.population ?? 1f) / 20f, 0.5f, 1.5f));
            for (int i = 0; i < ships; i++)
            {
                Color c = Color.Lerp(new Color(1f, 0.95f, 0.8f, 0.9f), civ.color, 0.35f);
                float radius = disk * (0.55f + (float)r.NextDouble() * 0.5f);
                float speed = (0.02f + (float)r.NextDouble() * 0.05f) * (r.Next(2) == 0 ? 1f : -1f);
                bg.AddMover(bg.NewMat(dot, ShaderDatabase.MoteGlow, c, 1005), planetOff, radius, speed, (float)r.NextDouble() * Mathf.PI * 2f, mapW * 0.006f, 0.3f + (float)r.NextDouble() * 0.3f, (float)r.NextDouble() * 40f - 20f, 0.17f);
            }
        }
    }

    public class MapComponent_CityHorizon : MapComponent
    {
        public bool city;
        public TechLevel tech = TechLevel.Spacer;
        public bool capital;
        private Material mat;
        private static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        public MapComponent_CityHorizon(Map map) : base(map)
        {
        }

        public static void Mark(Map map, TechLevel tech, bool capital)
        {
            MapComponent_CityHorizon c = map?.GetComponent<MapComponent_CityHorizon>();
            if (c == null) return;
            c.city = true;
            c.tech = tech;
            c.capital = capital;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref city, "city");
            Scribe_Values.Look(ref tech, "tech", TechLevel.Spacer);
            Scribe_Values.Look(ref capital, "capital");
        }

        public override void MapComponentUpdate()
        {
            if (!city || !RimverseMod.Settings.cityHorizon || Find.CurrentMap != map || RimWorld.Planet.WorldRendererUtility.WorldRendered) return;
            if (mat == null)
            {
                string key = tech + ":" + capital;
                if (!cache.TryGetValue(key, out Texture2D t) || t == null)
                {
                    t = CityTexture(512, tech, capital, key.GetHashCode());
                    cache[key] = t;
                }
                mat = new Material(ShaderDatabase.Transparent) { mainTexture = t, renderQueue = 1000 };
                mat.mainTextureScale = new Vector2(6f, 6f);
            }
            Vector3 center = new Vector3(map.Size.x / 2f, -2f, map.Size.z / 2f);
            float size = Mathf.Max(map.Size.x, map.Size.z) * 4f;
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(center, Quaternion.identity, new Vector3(size, 1f, size)), mat, 0);
        }

        private static Texture2D CityTexture(int size, TechLevel tech, bool capital, int seed)
        {
            System.Random r = new System.Random(seed);
            Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            Color street = tech >= TechLevel.Spacer ? new Color(0.16f, 0.17f, 0.19f, 1f) : new Color(0.24f, 0.22f, 0.2f, 1f);
            Color[] px = Enumerable.Repeat(street, size * size).ToArray();
            int block = capital ? 40 : 56;
            int gap = 8;
            for (int by = 0; by < size; by += block + gap)
            {
                for (int bx = 0; bx < size; bx += block + gap)
                {
                    int splits = r.Next(1, 4);
                    int sub = block / splits;
                    for (int sy = 0; sy < splits; sy++)
                    {
                        for (int sx = 0; sx < splits; sx++)
                        {
                            float h = (float)r.NextDouble();
                            if (capital) h = Mathf.Max(h, (float)r.NextDouble());
                            Color roof = Color.Lerp(new Color(0.26f, 0.27f, 0.3f), tech >= TechLevel.Ultra ? new Color(0.62f, 0.68f, 0.74f) : new Color(0.48f, 0.46f, 0.43f), h);
                            int x0 = bx + sx * sub + 1, y0 = by + sy * sub + 1, x1 = bx + (sx + 1) * sub - 1, y1 = by + (sy + 1) * sub - 1;
                            for (int y = y0; y < y1 && y < size; y++)
                            {
                                for (int x = x0; x < x1 && x < size; x++)
                                {
                                    bool edge = x == x0 || y == y0 || x == x1 - 1 || y == y1 - 1;
                                    Color c = edge ? roof * 0.7f : roof;
                                    if (!edge && ((x * 7 + y * 13) % 11 == 0) && r.NextDouble() < 0.3) c = Color.Lerp(c, new Color(1f, 0.85f, 0.5f), 0.6f);
                                    c.a = 1f;
                                    px[y * size + x] = c;
                                }
                            }
                        }
                    }
                }
            }
            t.SetPixels(px);
            t.Apply(false, true);
            return t;
        }
    }
}
