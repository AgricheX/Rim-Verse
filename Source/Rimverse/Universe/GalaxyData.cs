using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rimverse
{
    public enum GalaxyType
    {
        Spiral,
        BarredSpiral,
        Ringed,
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
        public int sizeClass;
        public double radiusLy;
        public double thicknessLy;
        public TravelRule rule;
        public float baseDensity;
        public Vector2 groupPositionMly;

        public double DensityAt(Vec3d p)
        {
            double r = p.PlanarLength / radiusLy;
            if (r > 1.15) return 0;
            double vertical = Math.Abs(p.y) / thicknessLy;
            double radial = Math.Exp(-2.2 * r);
            if (type == GalaxyType.Elliptical || type == GalaxyType.DwarfSpheroidal)
            {
                double rr = p.Length / radiusLy;
                radial = Math.Exp(-3.0 * rr);
                vertical = 0;
            }
            if ((type == GalaxyType.Spiral || type == GalaxyType.BarredSpiral) && r > 0.08)
            {
                double theta = Math.Atan2(p.z, p.x);
                double phase = 2.0 * theta - 5.0 * Math.Log(r);
                double arm = Math.Pow(0.5 + 0.5 * Math.Cos(phase), 3.0);
                radial *= 0.35 + 1.3 * arm;
            }
            if (type == GalaxyType.Irregular)
            {
                double n = Math.Sin(p.x / radiusLy * 7.3 + index) * Math.Cos(p.z / radiusLy * 5.1 - index) * 0.5 + 0.5;
                radial *= 0.4 + 1.2 * n;
            }
            if (type == GalaxyType.Ringed && r > 0.55 && r < 0.75) radial *= 2.0;
            if (type == GalaxyType.BarredSpiral && Math.Abs(p.z) < radiusLy * 0.08 && Math.Abs(p.x) < radiusLy * 0.4) radial *= 2.5;
            return baseDensity * radial * Math.Exp(-vertical * vertical);
        }
    }

    public static class GalaxyGenerator
    {
        private static readonly GalaxyType[] Types = { GalaxyType.Spiral, GalaxyType.BarredSpiral, GalaxyType.Ringed, GalaxyType.Elliptical, GalaxyType.DwarfSpheroidal, GalaxyType.Irregular };
        private static readonly float[] Weights = { 30f, 4f, 10f, 6f, 35f, 15f };
        private static readonly string[] NameA = { "Vel", "Aster", "Cor", "Nym", "Tal", "Ery", "Mor", "Sel", "Var", "Oph", "Kal", "Dre" };
        private static readonly string[] NameB = { "ion", "ara", "eth", "os", "ume", "aris", "enne", "ox", "ira", "ados" };

        public const int GroupSizeMax = 32;

        public static int GroupSize(int universeSeed)
        {
            return Mathf.Clamp(RimverseMod.Settings?.galaxyCount ?? 3, 1, GroupSizeMax);
        }

        public static GalaxyData Generate(int universeSeed, int index)
        {
            UniverseRand rand = new UniverseRand(UniverseRand.Hash(universeSeed, 50, index));
            GalaxyData g = new GalaxyData { index = index };
            g.type = index == 0 ? GalaxyType.Spiral : rand.Weighted(Types, Weights);
            g.sizeClass = index == 0 ? Math.Max(45, rand.SkewedSizeClass(1, 100, 1.6f)) : rand.SkewedSizeClass(1, 100, 2.2f);
            if (g.type == GalaxyType.DwarfSpheroidal) g.sizeClass = Math.Min(g.sizeClass, 25);
            if (g.type == GalaxyType.Elliptical) g.sizeClass = Math.Max(g.sizeClass, 50);
            float t = g.sizeClass / 100f;
            g.radiusLy = Mathf.Lerp(3000f, 262144f, t * t);
            g.thicknessLy = g.type == GalaxyType.Spiral || g.type == GalaxyType.BarredSpiral || g.type == GalaxyType.Ringed ? Math.Max(300.0, g.radiusLy * 0.02) : g.radiusLy;
            g.baseDensity = 0.004f * StarDensityFactor;
            switch (g.type)
            {
                case GalaxyType.BarredSpiral: g.rule = TravelRule.WarpBarRoutes; break;
                case GalaxyType.Ringed: g.rule = TravelRule.AncientGates; break;
                case GalaxyType.Elliptical: g.rule = TravelRule.Hyperlanes; g.baseDensity *= 0.6f; break;
                case GalaxyType.DwarfSpheroidal: g.rule = TravelRule.Sublight; g.baseDensity *= 0.5f; break;
                case GalaxyType.Irregular: g.rule = TravelRule.Wormholes; break;
                default: g.rule = TravelRule.Warp; break;
            }
            g.name = NameA[rand.RangeInclusive(0, NameA.Length - 1)] + NameB[rand.RangeInclusive(0, NameB.Length - 1)];
            float angle = rand.Range(0f, Mathf.PI * 2f);
            float dist = index == 0 ? 0f : rand.Range(0.05f, 1.5f);
            g.groupPositionMly = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
            return g;
        }

        public static float StarDensityFactor => RimverseMod.Settings?.starDensity ?? 0.1f;
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
            double density = g.DensityAt(d.Center);
            double volume = StarSystemId.DistrictSizeLy * StarSystemId.DistrictSizeLy * StarSystemId.DistrictSizeLy;
            UniverseRand rand = new UniverseRand(UniverseRand.Hash(u.universeSeed, 60, d.GetHashCode()));
            double expected = density * volume;
            int count = (int)Math.Floor(expected);
            if (rand.Value < expected - count) count++;
            count = Math.Min(count, 4095);
            if (u.IsHomeDistrict(d)) count = Math.Max(count, 1);
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
