using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public enum CosmicLevel
    {
        Chunk,
        Web,
        Supercluster,
        Cluster,
        LargeGroup,
        Group,
        Galaxy,
        Sector,
        Zone,
        District,
        Subcell,
        System,
        Body,
        Surface
    }

    public class CosmicNode
    {
        public CosmicLevel level;
        public int index;
        public string name;
        public double radiusMly;
        public Vec3d positionMly;
        public int childCount;
        public bool isVoid;
        public ulong seed;
        public bool home;
        public bool region;
        public bool wall;
        public Vec3d wallEnd;
    }

    public static class CosmicScale
    {
        public const double ChunkSizeMly = 1000.0;
        public const double VoidCellMly = 500.0;
        public const double WallMaxMly = 700.0;
        public const double BaseAgeGyr = 7.1;
        public const double AgeSpreadGyr = 0.1;
        public const double CalibrationAgeGyr = 13.8;
        public const float VoidMissingChance = 0.1f;
        public const int SuperclustersPerChunkMax = 40;
        public const int RegionsPerChunkMax = 48;
        public const int ClustersPerRegionMax = 20;
        public const int LargeGroupsPerClusterMax = 150;
        public const int GroupsPerLargeGroupMax = 16;
        public const int GalaxiesPerGroupMax = 32;

        private static readonly string[] SyllA = { "Lan", "Ser", "Hel", "Vir", "Cen", "Hyd", "Per", "Pho", "Ara", "Tuc", "Lyr", "Dor", "Fen", "Ori", "Cas", "Mon" };
        private static readonly string[] SyllB = { "iakea", "aeus", "ix", "go", "taur", "ra", "seid", "enix", "ion", "ana", "ae", "ado", "rir", "on", "siope", "oceros" };

        public static double UniverseAgeGyr(int seed)
        {
            UniverseRand r = new UniverseRand(UniverseRand.Hash(seed, 900));
            return BaseAgeGyr + r.ValueDouble * AgeSpreadGyr;
        }

        public static float AgeFactor(int seed) => (float)((UniverseAgeGyr(seed) - BaseAgeGyr) / AgeSpreadGyr);

        public static void HomeChunk(int seed, out short cx, out short cy, out short cz)
        {
            UniverseRand r = new UniverseRand(UniverseRand.Hash(seed, 901));
            cx = (short)r.RangeInclusive(short.MinValue, short.MaxValue);
            cy = (short)r.RangeInclusive(short.MinValue, short.MaxValue);
            cz = (short)r.RangeInclusive(short.MinValue, short.MaxValue);
        }

        public static CosmicNode Chunk(int seed)
        {
            HomeChunk(seed, out short cx, out short cy, out short cz);
            return new CosmicNode
            {
                level = CosmicLevel.Chunk,
                index = 0,
                name = "(" + cx + ", " + cy + ", " + cz + ")",
                radiusMly = ChunkSizeMly / 2,
                positionMly = new Vec3d(cx * ChunkSizeMly, cy * ChunkSizeMly, cz * ChunkSizeMly),
                childCount = 8,
                seed = UniverseRand.Hash(seed, 902, cx, cy, cz),
                home = true
            };
        }

        public static List<CosmicNode> Web(CosmicNode chunk)
        {
            List<CosmicNode> list = new List<CosmicNode>();
            UniverseRand miss = new UniverseRand(UniverseRand.Hash((long)chunk.seed, 909));
            int missing = miss.Chance(VoidMissingChance) ? miss.RangeInclusive(0, 7) : -1;
            for (int i = 0; i < 8; i++)
            {
                if (i == missing) continue;
                UniverseRand r = new UniverseRand(UniverseRand.Hash((long)chunk.seed, 910, i));
                Vec3d cell = new Vec3d(((i & 1) - 0.5) * VoidCellMly, (((i >> 1) & 1) - 0.5) * VoidCellMly, (((i >> 2) & 1) - 0.5) * VoidCellMly);
                Vec3d jitter = new Vec3d((r.ValueDouble - 0.5) * 80, (r.ValueDouble - 0.5) * 80, (r.ValueDouble - 0.5) * 80);
                list.Add(new CosmicNode
                {
                    level = CosmicLevel.Web,
                    index = i,
                    isVoid = true,
                    name = Name(r),
                    radiusMly = 150 + r.ValueDouble * 100,
                    positionMly = cell + jitter,
                    seed = r.NextULong()
                });
            }
            return list;
        }

        public static List<CosmicNode> Walls(CosmicNode chunk, List<CosmicNode> voids)
        {
            List<CosmicNode> list = new List<CosmicNode>();
            for (int a = 0; a < voids.Count; a++)
            {
                for (int b = a + 1; b < voids.Count; b++)
                {
                    int diff = voids[a].index ^ voids[b].index;
                    if (diff != 1 && diff != 2 && diff != 4) continue;
                    Vec3d pa = voids[a].positionMly;
                    Vec3d pb = voids[b].positionMly;
                    Vec3d mid = (pa + pb) * 0.5;
                    Vec3d axis = pb - pa;
                    UniverseRand r = new UniverseRand(UniverseRand.Hash((long)chunk.seed, 915, voids[a].index, voids[b].index));
                    Vec3d side = RandomDirection(r);
                    Vec3d perp = side - axis * (Vec3d.Dot(side, axis) / Math.Max(1e-6, Vec3d.Dot(axis, axis)));
                    double plen = perp.Length;
                    if (plen < 1e-6) continue;
                    perp = perp * (1.0 / plen);
                    double half = Math.Min(WallMaxMly, 300 + r.ValueDouble * 400) / 2;
                    list.Add(new CosmicNode
                    {
                        level = CosmicLevel.Web,
                        index = list.Count,
                        wall = true,
                        name = Name(r),
                        radiusMly = 20 + r.ValueDouble * 30,
                        positionMly = mid - perp * half,
                        wallEnd = mid + perp * half,
                        seed = r.NextULong()
                    });
                }
            }
            return list;
        }

        public static Vec3d PointOnWall(CosmicNode wall, double t) => wall.positionMly + (wall.wallEnd - wall.positionMly) * t;

        public static List<CosmicNode> Superclusters(CosmicNode chunk, List<CosmicNode> voids)
        {
            List<CosmicNode> walls = Walls(chunk, voids);
            UniverseRand r = new UniverseRand(UniverseRand.Hash((long)chunk.seed, 920));
            int supers = r.RangeInclusive(SuperclustersPerChunkMax / 2, SuperclustersPerChunkMax);
            int regions = r.RangeInclusive(RegionsPerChunkMax / 2, RegionsPerChunkMax);
            List<CosmicNode> list = new List<CosmicNode>();
            for (int i = 0; i < supers + regions; i++)
            {
                UniverseRand nr = new UniverseRand(UniverseRand.Hash((long)chunk.seed, 921, i));
                bool region = i >= supers;
                Vec3d pos = new Vec3d();
                if (!region && walls.Count > 0)
                {
                    CosmicNode w = walls[nr.RangeInclusive(0, walls.Count - 1)];
                    pos = PointOnWall(w, nr.ValueDouble) + RandomDirection(nr) * (nr.ValueDouble * w.radiusMly);
                }
                else
                {
                    for (int tries = 0; tries < 30; tries++)
                    {
                        pos = new Vec3d((nr.ValueDouble - 0.5) * ChunkSizeMly, (nr.ValueDouble - 0.5) * ChunkSizeMly, (nr.ValueDouble - 0.5) * ChunkSizeMly);
                        if (OnWall(pos, voids)) break;
                    }
                }
                list.Add(new CosmicNode
                {
                    level = CosmicLevel.Supercluster,
                    index = i,
                    region = region,
                    name = Name(nr),
                    radiusMly = 60 + nr.ValueDouble * 190,
                    positionMly = pos,
                    childCount = nr.RangeInclusive(4, ClustersPerRegionMax),
                    seed = nr.NextULong()
                });
            }
            return list;
        }

        private static bool OnWall(Vec3d p, List<CosmicNode> voids)
        {
            foreach (CosmicNode v in voids)
            {
                if (Vec3d.Distance(p, v.positionMly) < v.radiusMly) return false;
            }
            return true;
        }

        public static List<CosmicNode> Children(CosmicNode parent)
        {
            List<CosmicNode> list = new List<CosmicNode>();
            CosmicLevel level;
            double rMin, rMax;
            int kidsMin, kidsMax;
            switch (parent.level)
            {
                case CosmicLevel.Supercluster: level = CosmicLevel.Cluster; rMin = 8; rMax = 25; kidsMin = 10; kidsMax = LargeGroupsPerClusterMax; break;
                case CosmicLevel.Cluster: level = CosmicLevel.LargeGroup; rMin = 2; rMax = 5; kidsMin = 1; kidsMax = GroupsPerLargeGroupMax; break;
                case CosmicLevel.LargeGroup: level = CosmicLevel.Group; rMin = 0.5; rMax = 1.5; kidsMin = 1; kidsMax = GalaxiesPerGroupMax; break;
                default: return list;
            }
            for (int i = 0; i < parent.childCount; i++)
            {
                UniverseRand r = new UniverseRand(UniverseRand.Hash((long)parent.seed, 930, i));
                double radius = rMin + r.ValueDouble * (rMax - rMin);
                double spread = Math.Max(0.0, parent.radiusMly - radius);
                Vec3d dir = RandomDirection(r);
                list.Add(new CosmicNode
                {
                    level = level,
                    index = i,
                    name = Name(r),
                    radiusMly = radius,
                    positionMly = dir * (spread * Math.Pow(r.ValueDouble, 0.6)),
                    childCount = level == CosmicLevel.Group ? GroupGalaxyCount(r) : r.RangeInclusive(kidsMin, kidsMax),
                    seed = r.NextULong()
                });
            }
            return list;
        }

        public static int GroupGalaxyCount(UniverseRand r)
        {
            return Mathf.Clamp(2 + (int)Math.Round(Math.Pow(r.ValueDouble, 1.6) * (GalaxiesPerGroupMax - 2)), 1, GalaxiesPerGroupMax);
        }

        public static Vec3d RandomDirection(UniverseRand r)
        {
            double u = r.ValueDouble * 2 - 1;
            double t = r.ValueDouble * Math.PI * 2;
            double s = Math.Sqrt(1 - u * u);
            return new Vec3d(s * Math.Cos(t), u, s * Math.Sin(t));
        }

        private static string Name(UniverseRand r) => SyllA[r.RangeInclusive(0, SyllA.Length - 1)] + SyllB[r.RangeInclusive(0, SyllB.Length - 1)];

        public class HomeChain
        {
            public CosmicNode chunk;
            public List<CosmicNode> web;
            public List<CosmicNode> walls;
            public List<CosmicNode> superclusters;
            public CosmicNode supercluster;
            public List<CosmicNode> clusters;
            public CosmicNode cluster;
            public List<CosmicNode> largeGroups;
            public CosmicNode largeGroup;
            public List<CosmicNode> groups;
            public CosmicNode group;

            public CosmicNode Node(CosmicLevel level)
            {
                switch (level)
                {
                    case CosmicLevel.Chunk: return chunk;
                    case CosmicLevel.Supercluster: return supercluster;
                    case CosmicLevel.Cluster: return cluster;
                    case CosmicLevel.LargeGroup: return largeGroup;
                    case CosmicLevel.Group: return group;
                    default: return null;
                }
            }

            public List<CosmicNode> Siblings(CosmicLevel level)
            {
                switch (level)
                {
                    case CosmicLevel.Web: return web;
                    case CosmicLevel.Supercluster: return superclusters;
                    case CosmicLevel.Cluster: return clusters;
                    case CosmicLevel.LargeGroup: return largeGroups;
                    case CosmicLevel.Group: return groups;
                    default: return null;
                }
            }
        }

        private static readonly Dictionary<int, HomeChain> chains = new Dictionary<int, HomeChain>();

        public static HomeChain Home(int seed, int galaxyCount)
        {
            if (chains.TryGetValue(seed, out HomeChain c) && c.group.childCount == galaxyCount) return c;
            c = new HomeChain();
            UniverseRand r = new UniverseRand(UniverseRand.Hash(seed, 940));
            c.chunk = Chunk(seed);
            c.web = Web(c.chunk);
            c.walls = Walls(c.chunk, c.web);
            c.superclusters = Superclusters(c.chunk, c.web);
            c.supercluster = PickWhere(c.superclusters, r, n => !n.region);
            c.clusters = Children(c.supercluster);
            c.cluster = Pick(c.clusters, r);
            c.largeGroups = Children(c.cluster);
            c.largeGroup = Pick(c.largeGroups, r);
            c.groups = Children(c.largeGroup);
            c.group = Pick(c.groups, r);
            c.group.childCount = galaxyCount;
            chains[seed] = c;
            return c;
        }

        private static CosmicNode PickWhere(List<CosmicNode> list, UniverseRand r, Func<CosmicNode, bool> ok)
        {
            List<CosmicNode> pool = list.FindAll(n => ok(n));
            return Pick(pool.Count > 0 ? pool : list, r);
        }

        private static CosmicNode Pick(List<CosmicNode> list, UniverseRand r)
        {
            CosmicNode n = list[r.RangeInclusive(0, list.Count - 1)];
            n.home = true;
            return n;
        }

        public static string LevelLabel(CosmicLevel level) => ("Rimverse_CosmicLevel_" + level).Translate();
    }
}
