using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public enum GalaxyType
    {
        Spiral,
        Elliptical,
        DwarfSpheroidal,
        Irregular
    }

    public enum TravelRule
    {
        Warp,
        WarpBarRoutes,
        AncientGates,
        Hyperlanes,
        Sublight,
        Wormholes
    }

    public class GalaxyData
    {
        public int index;
        public string name;
        public GalaxyType type;
        public bool hasBar;
        public bool hasRing;
        public int sizeClass;
        public double radiusLy;
        public double thicknessLy;
        public TravelRule rule;
        public float baseDensity;
        public Vector2 groupPositionMly;
        public double groupHeightMly;
        public double armPitch = 5.0;
        public double barLength = 0.4;
        public double ringRadius = 0.65;

        public bool IsDisk => type == GalaxyType.Spiral;

        public string TypeLabel
        {
            get
            {
                string s = ("Rimverse_GalaxyType_" + type).Translate();
                if (hasBar) s = "Rimverse_GalaxyAttr_Barred".Translate(s);
                if (hasRing) s = "Rimverse_GalaxyAttr_Ringed".Translate(s);
                return s;
            }
        }

        public double DensityAt(Vec3d p)
        {
            double r = p.PlanarLength / radiusLy;
            if (r > 1.15) return 0;
            double vertical = Math.Abs(p.y) / thicknessLy;
            double radial = Math.Exp(-2.2 * r);
            if (type == GalaxyType.Elliptical || type == GalaxyType.DwarfSpheroidal)
            {
                double rr = p.Length / radiusLy;
                if (rr > 1.15) return 0;
                radial = Math.Exp(-3.0 * rr);
                vertical = 0;
            }
            if (type == GalaxyType.Spiral && r > 0.08)
            {
                double theta = Math.Atan2(p.z, p.x);
                double phase = 2.0 * theta - armPitch * Math.Log(r);
                double arm = Math.Pow(0.5 + 0.5 * Math.Cos(phase), 3.0);
                radial *= 0.35 + 1.3 * arm;
            }
            if (type == GalaxyType.Irregular)
            {
                double n = Math.Sin(p.x / radiusLy * 7.3 + index) * Math.Cos(p.z / radiusLy * 5.1 - index) * 0.5 + 0.5;
                radial *= 0.4 + 1.2 * n;
            }
            if (hasRing && Math.Abs(r - ringRadius) < 0.1) radial *= 2.0;
            if (hasBar && InBar(p)) radial *= 2.5;
            return baseDensity * radial * Math.Exp(-vertical * vertical);
        }

        public bool InBar(Vec3d p) => hasBar && Math.Abs(p.z) < radiusLy * 0.08 && Math.Abs(p.x) < radiusLy * barLength;

        public double MeanDensity(Vec3d center, double size)
        {
            double h = size * 0.35;
            double sum = DensityAt(center);
            sum += DensityAt(center + new Vec3d(h, 0, h));
            sum += DensityAt(center + new Vec3d(-h, 0, h));
            sum += DensityAt(center + new Vec3d(h, 0, -h));
            sum += DensityAt(center + new Vec3d(-h, 0, -h));
            return sum / 5.0;
        }

        public double ExpectedSystems(Vec3d center, double size) => MeanDensity(center, size) * size * size * size;
    }

    public static class GalaxyGenerator
    {
        private static readonly GalaxyType[] Types = { GalaxyType.Spiral, GalaxyType.Elliptical, GalaxyType.DwarfSpheroidal, GalaxyType.Irregular };
        private static readonly float[] Weights = { 44f, 6f, 35f, 15f };
        private static readonly string[] NameA = { "Vel", "Aster", "Cor", "Nym", "Tal", "Ery", "Mor", "Sel", "Var", "Oph", "Kal", "Dre" };
        private static readonly string[] NameB = { "ion", "ara", "eth", "os", "ume", "aris", "enne", "ox", "ira", "ados" };

        public const int GroupSizeMax = CosmicScale.GalaxiesPerGroupMax;
        public const float BarChance = 0.08f;
        public const float RingChance = 0.30f;
        public const float OrigemDensity = 0.004f;
        public const int LegacyGroupSize = 3;
        public const float LegacyDensity = 0.1f;

        public static int SeedGroupSize(int universeSeed)
        {
            UniverseRand r = new UniverseRand(UniverseRand.Hash(universeSeed, 49));
            return CosmicScale.GroupGalaxyCount(r);
        }

        public static GalaxyData Generate(int universeSeed, int index, float densityFactor, double groupRadiusMly)
        {
            UniverseRand rand = new UniverseRand(UniverseRand.Hash(universeSeed, 50, index));
            GalaxyData g = new GalaxyData { index = index };
            g.type = index == 0 ? GalaxyType.Spiral : rand.Weighted(Types, Weights);
            g.sizeClass = index == 0 ? Math.Max(45, rand.SkewedSizeClass(1, 100, 1.6f)) : rand.SkewedSizeClass(1, 100, 2.2f);
            if (g.type == GalaxyType.DwarfSpheroidal) g.sizeClass = Math.Min(g.sizeClass, 25);
            if (g.type == GalaxyType.Elliptical) g.sizeClass = Math.Max(g.sizeClass, 50);
            UniverseRand attr = new UniverseRand(UniverseRand.Hash(universeSeed, 51, index));
            if (g.IsDisk)
            {
                g.hasBar = attr.Chance(BarChance);
                g.hasRing = attr.Chance(RingChance);
                g.armPitch = 4.0 + attr.ValueDouble * 2.5;
                g.barLength = 0.25 + attr.ValueDouble * 0.2;
                g.ringRadius = 0.5 + attr.ValueDouble * 0.3;
            }
            float t = g.sizeClass / 100f;
            g.radiusLy = Mathf.Lerp(3000f, 262144f, t * t);
            g.thicknessLy = g.IsDisk ? Math.Max(300.0, g.radiusLy * 0.02) : g.radiusLy;
            g.baseDensity = OrigemDensity * densityFactor;
            switch (g.type)
            {
                case GalaxyType.Elliptical: g.rule = TravelRule.Hyperlanes; g.baseDensity *= 0.6f; break;
                case GalaxyType.DwarfSpheroidal: g.rule = TravelRule.Sublight; g.baseDensity *= 0.5f; break;
                case GalaxyType.Irregular: g.rule = TravelRule.Wormholes; break;
                default: g.rule = g.hasBar ? TravelRule.WarpBarRoutes : g.hasRing ? TravelRule.AncientGates : TravelRule.Warp; break;
            }
            g.name = NameA[rand.RangeInclusive(0, NameA.Length - 1)] + NameB[rand.RangeInclusive(0, NameB.Length - 1)];
            float angle = rand.Range(0f, Mathf.PI * 2f);
            float dist = index == 0 ? 0f : rand.Range(0.05f, (float)Math.Max(0.1, groupRadiusMly));
            g.groupPositionMly = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
            g.groupHeightMly = index == 0 ? 0 : (attr.ValueDouble - 0.5) * groupRadiusMly * 0.6;
            return g;
        }

        public static float SettingDensity => Mathf.Clamp(RimverseMod.Settings?.starDensityFactor ?? 1f, 0.05f, 1f);

        public static int SettingCap => Mathf.Clamp(RimverseMod.Settings?.galaxyCap ?? GroupSizeMax, 1, GroupSizeMax);
    }

    public class StarEntry
    {
        public StarSystemId id;
        public Vec3d position;
        public StarType type;
    }

    public static class GalaxyQuery
    {
        private static readonly Dictionary<DistrictId, List<StarEntry>> cache = new Dictionary<DistrictId, List<StarEntry>>();
        private const int CacheLimit = 4000;

        public static void ClearCache() => cache.Clear();

        public static List<StarEntry> StarsInDistrict(GameComponent_Universe u, GalaxyData g, DistrictId d)
        {
            if (cache.TryGetValue(d, out List<StarEntry> list)) return list;
            if (cache.Count > CacheLimit) cache.Clear();
            list = new List<StarEntry>();
            int count = SystemCount(u, g, d);
            Vec3d origin = d.Origin;
            for (int i = 0; i < count; i++)
            {
                StarSystemId id = new StarSystemId(g.index, d.sx, d.sy, d.sz, d.dx, d.dy, d.dz, i);
                UniverseRand pr = new UniverseRand(UniverseRand.Hash((long)id.Seed(u.universeSeed), 61));
                Vec3d pos = origin + new Vec3d(pr.ValueDouble * StarSystemId.DistrictSizeLy, pr.ValueDouble * StarSystemId.DistrictSizeLy, pr.ValueDouble * StarSystemId.DistrictSizeLy);
                list.Add(new StarEntry { id = id, position = pos, type = StarSystemGenerator.PrimaryTypeFor(id, u.universeSeed, u.IsHome(id)) });
            }
            cache[d] = list;
            return list;
        }

        public static int SystemCount(GameComponent_Universe u, GalaxyData g, DistrictId d)
        {
            bool home = u.IsHomeDistrict(d);
            if (!home && !SectorExists(g, d.SectorCenter)) return 0;
            if (!home && !ZoneExists(g, d.Zone)) return 0;
            double density = g.DensityAt(d.Center);
            double volume = StarSystemId.DistrictSizeLy * StarSystemId.DistrictSizeLy * StarSystemId.DistrictSizeLy;
            UniverseRand rand = new UniverseRand(UniverseRand.Hash(u.universeSeed, 60, d.GetHashCode()));
            double expected = density * volume;
            int count = (int)Math.Floor(expected);
            if (rand.Value < expected - count) count++;
            count = Math.Min(count, StarSystemId.MaxSystemsPerDistrict);
            if (home) count = Math.Max(count, 1);
            return count;
        }

        public static bool SectorExists(GalaxyData g, Vec3d sectorCenter) => g.ExpectedSystems(sectorCenter, StarSystemId.SectorSizeLy) >= StarSystemId.SectorMinSystems;

        public static bool ZoneExists(GalaxyData g, ZoneId z) => g.ExpectedSystems(z.Center, StarSystemId.ZoneSizeLy) >= StarSystemId.ZoneMinSystems;

        public static double ExpectedInZone(GalaxyData g, ZoneId z) => ZoneExists(g, z) ? g.ExpectedSystems(z.Center, StarSystemId.ZoneSizeLy) : 0;

        public static Vec3d PositionOf(GameComponent_Universe u, StarSystemId id)
        {
            GalaxyData g = u.Galaxy(id.galaxy);
            foreach (StarEntry e in StarsInDistrict(u, g, id.District))
            {
                if (e.id == id) return e.position;
            }
            return id.District.Center;
        }

        public static List<StarEntry> StarsInRadius(GameComponent_Universe u, int galaxy, Vec3d center, double radius)
        {
            GalaxyData g = u.Galaxy(galaxy);
            List<StarEntry> result = new List<StarEntry>();
            double ds = StarSystemId.DistrictSizeLy;
            long minX = (long)Math.Floor((center.x - radius) / ds), maxX = (long)Math.Floor((center.x + radius) / ds);
            long minY = (long)Math.Floor((center.y - radius) / ds), maxY = (long)Math.Floor((center.y + radius) / ds);
            long minZ = (long)Math.Floor((center.z - radius) / ds), maxZ = (long)Math.Floor((center.z + radius) / ds);
            for (long x = minX; x <= maxX; x++)
            for (long y = minY; y <= maxY; y++)
            for (long z = minZ; z <= maxZ; z++)
            {
                DistrictId d = DistrictId.FromGlobal(galaxy, x, y, z);
                foreach (StarEntry e in StarsInDistrict(u, g, d))
                {
                    if (Vec3d.Distance(e.position, center) <= radius) result.Add(e);
                }
            }
            return result;
        }
    }
}
