using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Noise;

namespace Rimverse
{
    public enum BodyFlavor
    {
        None,
        Crystal,
        Fungal,
        Irradiated
    }

    public class BiomeFeatureExtension : DefModExtension
    {
        public string feature;
    }

    public static class BiomeFeatures
    {
        public static string Of(Map map) => map?.Biome?.GetModExtension<BiomeFeatureExtension>()?.feature;

        public static string Pick(BodyData body, string systemKey)
        {
            if (body == null) return null;
            if (body.type == BodyType.GasGiant) return "HighClouds";
            if (body.Airless) return null;
            if (systemKey != null)
            {
                Civilization owner = SettlementGen.CivOn(systemKey, body.index, out _);
                if (owner != null && owner.tech >= TechLevel.Ultra && body.atmosphere == AtmosphereType.Breathable) return "GlitterGarden";
                GameComponent_Civilizations gc = GameComponent_Civilizations.Instance;
                if (gc != null && owner == null && gc.civilizations.Any(c => c.dead && c.planets.Any(p => p.systemKey == systemKey && p.body == body.index))) return "UrbanRuin";
            }
            float t = body.temperatureC;
            if (body.tidalLocked)
            {
                if (t > 50f) return "Dayside";
                if (t < -30f) return "Nightside";
                return "TwilightBand";
            }
            if (body.type == BodyType.Ocean || body.water > 0.9f) return t < -5f ? "DeepGlacier" : "GlobalOcean";
            if (t >= 80f) return "LavaPlains";
            if (t <= -60f) return "DeepGlacier";
            switch (body.flavor)
            {
                case BodyFlavor.Crystal: return "CrystalForest";
                case BodyFlavor.Fungal: return "FungalJungle";
                case BodyFlavor.Irradiated: return "RadiationDesert";
            }
            return null;
        }

        public static float ExtraRadiationPerHour(Map map) => Of(map) == "Radiation" ? 0.01f : 0f;
    }

    public class GenStep_BiomeFeatures : GenStep
    {
        public override int SeedPart => 913377021;

        public override void Generate(Map map, GenStepParams parms)
        {
            string f = BiomeFeatures.Of(map);
            if (f == null) return;
            switch (f)
            {
                case "Twilight": Twilight(map); break;
                case "Dayside": Pools(map, 0.82f, 18f); break;
                case "Lava": Pools(map, 0.7f, 32f); break;
                case "Ocean": Ocean(map); break;
                case "Clouds": Clouds(map); break;
                case "Ruins": Ruins(map); break;
                case "Crystal": Scatter(map, "Rimverse_MineableIce", 0.004f); break;
                case "Glacier": Scatter(map, "Rimverse_MineableIce", 0.01f); break;
            }
        }

        private static void Set(Map map, IntVec3 c, string terrain)
        {
            TerrainDef t = DefDatabase<TerrainDef>.GetNamedSilentFail(terrain);
            if (t != null) map.terrainGrid.SetTerrain(c, t);
        }

        private static void ClearEdifice(Map map, IntVec3 c)
        {
            Building b = c.GetEdifice(map);
            if (b != null && b.def.building != null && b.def.building.isNaturalRock) b.Destroy();
        }

        private static void Twilight(Map map)
        {
            int w = map.Size.x;
            foreach (IntVec3 c in map.AllCells)
            {
                float u = c.x / (float)w + Rand.Range(-0.03f, 0.03f);
                if (u < 0.15f) Set(map, c, "Rimverse_ScorchedRock");
                else if (u > 0.85f) Set(map, c, "Rimverse_GlacierIce");
            }
        }

        private static void Pools(Map map, float threshold, float scale)
        {
            Perlin p = new Perlin(1.0 / scale, 2.0, 0.5, 4, Rand.Int, QualityMode.Medium);
            IntVec3 center = map.Center;
            foreach (IntVec3 c in map.AllCells)
            {
                if (c.InHorDistOf(center, 14f)) continue;
                if (p.GetValue(c.x, 0, c.z) > threshold * 0.6f)
                {
                    ClearEdifice(map, c);
                    Set(map, c, "Rimverse_LavaPool");
                }
            }
        }

        private static void Ocean(Map map)
        {
            MapGenFloatGrid elev = MapGenerator.Elevation;
            if (elev == null) return;
            IntVec3 center = map.Center;
            foreach (IntVec3 c in map.AllCells)
            {
                if (c.InHorDistOf(center, 12f)) continue;
                float e = elev[c];
                if (e < 0.55f)
                {
                    ClearEdifice(map, c);
                    map.terrainGrid.SetTerrain(c, e < 0.4f ? TerrainDefOf.WaterOceanDeep : TerrainDefOf.WaterOceanShallow);
                }
            }
        }

        private static void Clouds(Map map)
        {
            IntVec3 center = map.Center;
            List<IntVec3> pads = new List<IntVec3> { center };
            for (int i = 0; i < 4; i++) pads.Add(center + new IntVec3(Rand.RangeInclusive(-50, 50), 0, Rand.RangeInclusive(-50, 50)));
            foreach (IntVec3 c in map.AllCells)
            {
                ClearEdifice(map, c);
                bool floor = false;
                for (int i = 0; i < pads.Count; i++)
                {
                    if (c.InHorDistOf(pads[i], i == 0 ? 22f : 9f)) floor = true;
                }
                for (int i = 1; i < pads.Count && !floor; i++)
                {
                    if (DistToSegment(c, center, pads[i]) < 1.6f) floor = true;
                }
                Set(map, c, floor ? "Rimverse_HullFloor" : "Rimverse_CloudSea");
            }
        }

        private static float DistToSegment(IntVec3 c, IntVec3 a, IntVec3 b)
        {
            Vector2 p = new Vector2(c.x, c.z), va = new Vector2(a.x, a.z), vb = new Vector2(b.x, b.z);
            Vector2 ab = vb - va;
            float t = Mathf.Clamp01(Vector2.Dot(p - va, ab) / Mathf.Max(0.001f, ab.sqrMagnitude));
            return Vector2.Distance(p, va + ab * t);
        }

        private static void Ruins(Map map)
        {
            ThingDef wall = ThingDefOf.Wall;
            ThingDef stuff = ThingDefOf.Steel;
            IntVec3 center = map.Center;
            int count = Rand.RangeInclusive(10, 18);
            for (int i = 0; i < count; i++)
            {
                int w = Rand.RangeInclusive(6, 16);
                int h = Rand.RangeInclusive(6, 16);
                IntVec3 o = new IntVec3(Rand.RangeInclusive(5, map.Size.x - w - 5), 0, Rand.RangeInclusive(5, map.Size.z - h - 5));
                CellRect r = new CellRect(o.x, o.z, w, h);
                if (r.Contains(center) || r.ExpandedBy(6).Contains(center)) continue;
                foreach (IntVec3 c in r.EdgeCells)
                {
                    if (!c.InBounds(map) || Rand.Chance(0.35f) || c.GetEdifice(map) != null) continue;
                    Thing t = ThingMaker.MakeThing(wall, stuff);
                    t.HitPoints = Mathf.Max(1, (int)(t.MaxHitPoints * Rand.Range(0.2f, 0.7f)));
                    GenSpawn.Spawn(t, c, map);
                }
                foreach (IntVec3 c in r.Cells)
                {
                    if (c.InBounds(map) && c.GetEdifice(map) == null) Set(map, c, "Rimverse_Rubble");
                    if (c.InBounds(map) && c.GetEdifice(map) == null && Rand.Chance(0.03f)) GenSpawn.Spawn(ThingDefOf.ChunkSlagSteel, c, map);
                }
            }
        }

        private static void Scatter(Map map, string defName, float chance)
        {
            ThingDef d = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (d == null) return;
            IntVec3 center = map.Center;
            foreach (IntVec3 c in map.AllCells)
            {
                if (c.InHorDistOf(center, 12f) || c.GetEdifice(map) != null || !c.Standable(map) || !Rand.Chance(chance)) continue;
                GenSpawn.Spawn(d, c, map);
            }
        }
    }

    [StaticConstructorOnStartup]
    public static class BiomeFishing
    {
        static BiomeFishing()
        {
            BiomeDef source = DefDatabase<BiomeDef>.GetNamedSilentFail("TemperateSwamp") ?? DefDatabase<BiomeDef>.GetNamedSilentFail("TemperateForest");
            if (source == null) return;
            foreach (string name in new[] { "Rimverse_GlobalOcean", "Rimverse_TwilightBand", "Rimverse_FungalJungle", "Rimverse_GlitterGarden" })
            {
                BiomeDef target = DefDatabase<BiomeDef>.GetNamedSilentFail(name);
                if (target == null) continue;
                foreach (FieldInfo f in typeof(BiomeDef).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (f.Name.IndexOf("fish", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    try { f.SetValue(target, f.GetValue(source)); }
                    catch (Exception) { }
                }
            }
        }
    }
}
