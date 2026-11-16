using System;

namespace Rimverse
{
    public struct StarSystemId : IEquatable<StarSystemId>
    {
        public const double SectorSizeLy = 8192.0;
        public const double DistrictSizeLy = 64.0;
        public const int DistrictsPerSector = 128;
        public const double ZoneSizeLy = 1024.0;
        public const int ZonesPerSector = 8;
        public const int DistrictsPerZone = 16;
        public const double SubcellSizeLy = 16.0;
        public const int SubcellsPerDistrictAxis = 4;
        public const int MaxSystemsPerDistrict = 4095;
        public const int SectorMinSystems = 100;
        public const int ZoneMinSystems = 2;

        public int galaxy;
        public int sx;
        public int sy;
        public int sz;
        public int dx;
        public int dy;
        public int dz;
        public int index;
        public int zx;
        public int zy;
        public int zz;

        public StarSystemId(int galaxy, int sx, int sy, int sz, int dx, int dy, int dz, int index)
        {
            zx = Math.Max(0, Math.Min(ZonesPerSector - 1, dx / DistrictsPerZone));
            zy = Math.Max(0, Math.Min(ZonesPerSector - 1, dy / DistrictsPerZone));
            zz = Math.Max(0, Math.Min(ZonesPerSector - 1, dz / DistrictsPerZone));
            this.galaxy = galaxy;
            this.sx = sx;
            this.sy = sy;
            this.sz = sz;
            this.dx = dx;
            this.dy = dy;
            this.dz = dz;
            this.index = index;
        }

        public static readonly StarSystemId Invalid = new StarSystemId(-1, 0, 0, 0, 0, 0, 0, -1);

        public bool Valid => galaxy >= 0 && index >= 0;

        public DistrictId District => new DistrictId(galaxy, sx, sy, sz, dx, dy, dz);

        public ZoneId Zone => new ZoneId(galaxy, sx, sy, sz, zx, zy, zz);

        public static int SubcellOf(Vec3d position, DistrictId d)
        {
            Vec3d o = d.Origin;
            int ax = Clamp((int)Math.Floor((position.x - o.x) / SubcellSizeLy));
            int ay = Clamp((int)Math.Floor((position.y - o.y) / SubcellSizeLy));
            int az = Clamp((int)Math.Floor((position.z - o.z) / SubcellSizeLy));
            return ax + ay * SubcellsPerDistrictAxis + az * SubcellsPerDistrictAxis * SubcellsPerDistrictAxis;
        }

        private static int Clamp(int v) => v < 0 ? 0 : v >= SubcellsPerDistrictAxis ? SubcellsPerDistrictAxis - 1 : v;

        public string Address(Vec3d position)
        {
            DistrictId d = District;
            ZoneId z = d.Zone;
            return "S(" + sx + "," + sy + "," + sz + ") Z(" + z.zx + "," + z.zy + "," + z.zz + ") D(" + d.LocalX + "," + d.LocalY + "," + d.LocalZ + ") C" + SubcellOf(position, d) + " #" + index;
        }

        public ulong Seed(int universeSeed) => UniverseRand.Hash(universeSeed, galaxy, sx, sy, sz, dx, dy, dz, index);

        public string Key => galaxy + ":" + sx + ":" + sy + ":" + sz + ":" + dx + ":" + dy + ":" + dz + ":" + index;

        public static StarSystemId FromKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return Invalid;
            string[] p = key.Split(':');
            if (p.Length != 8) return Invalid;
            return new StarSystemId(int.Parse(p[0]), int.Parse(p[1]), int.Parse(p[2]), int.Parse(p[3]), int.Parse(p[4]), int.Parse(p[5]), int.Parse(p[6]), int.Parse(p[7]));
        }

        public bool Equals(StarSystemId o) => galaxy == o.galaxy && sx == o.sx && sy == o.sy && sz == o.sz && dx == o.dx && dy == o.dy && dz == o.dz && index == o.index;

        public override bool Equals(object obj) => obj is StarSystemId o && Equals(o);

        public override int GetHashCode() => (int)UniverseRand.Hash(galaxy, sx, sy, sz, dx, dy, dz, index);

        public static bool operator ==(StarSystemId a, StarSystemId b) => a.Equals(b);

        public static bool operator !=(StarSystemId a, StarSystemId b) => !a.Equals(b);

        public override string ToString() => Key;
    }

    public struct DistrictId : IEquatable<DistrictId>
    {
        public int galaxy;
        public int sx;
        public int sy;
        public int sz;
        public int dx;
        public int dy;
        public int dz;

        public DistrictId(int galaxy, int sx, int sy, int sz, int dx, int dy, int dz)
        {
            this.galaxy = galaxy;
            this.sx = sx;
            this.sy = sy;
            this.sz = sz;
            this.dx = dx;
            this.dy = dy;
            this.dz = dz;
        }

        public static DistrictId FromPosition(int galaxy, Vec3d pos)
        {
            long gx = (long)Math.Floor(pos.x / StarSystemId.DistrictSizeLy);
            long gy = (long)Math.Floor(pos.y / StarSystemId.DistrictSizeLy);
            long gz = (long)Math.Floor(pos.z / StarSystemId.DistrictSizeLy);
            return FromGlobal(galaxy, gx, gy, gz);
        }

        public static DistrictId FromGlobal(int galaxy, long gx, long gy, long gz)
        {
            int n = StarSystemId.DistrictsPerSector;
            int sx = (int)Math.Floor(gx / (double)n);
            int sy = (int)Math.Floor(gy / (double)n);
            int sz = (int)Math.Floor(gz / (double)n);
            return new DistrictId(galaxy, sx, sy, sz, (int)(gx - (long)sx * n), (int)(gy - (long)sy * n), (int)(gz - (long)sz * n));
        }

        public long GlobalX => (long)sx * StarSystemId.DistrictsPerSector + dx;

        public long GlobalY => (long)sy * StarSystemId.DistrictsPerSector + dy;

        public long GlobalZ => (long)sz * StarSystemId.DistrictsPerSector + dz;

        public Vec3d Origin => new Vec3d(GlobalX * StarSystemId.DistrictSizeLy, GlobalY * StarSystemId.DistrictSizeLy, GlobalZ * StarSystemId.DistrictSizeLy);

        public Vec3d Center => Origin + new Vec3d(StarSystemId.DistrictSizeLy / 2, StarSystemId.DistrictSizeLy / 2, StarSystemId.DistrictSizeLy / 2);

        public ZoneId Zone => new ZoneId(galaxy, sx, sy, sz, dx / StarSystemId.DistrictsPerZone, dy / StarSystemId.DistrictsPerZone, dz / StarSystemId.DistrictsPerZone);

        public int LocalX => dx % StarSystemId.DistrictsPerZone;

        public int LocalY => dy % StarSystemId.DistrictsPerZone;

        public int LocalZ => dz % StarSystemId.DistrictsPerZone;

        public Vec3d SectorCenter => new Vec3d((sx + 0.5) * StarSystemId.SectorSizeLy, (sy + 0.5) * StarSystemId.SectorSizeLy, (sz + 0.5) * StarSystemId.SectorSizeLy);

        public bool Equals(DistrictId o) => galaxy == o.galaxy && sx == o.sx && sy == o.sy && sz == o.sz && dx == o.dx && dy == o.dy && dz == o.dz;

        public override bool Equals(object obj) => obj is DistrictId o && Equals(o);

        public override int GetHashCode() => (int)UniverseRand.Hash(galaxy, sx, sy, sz, dx, dy, dz);
    }

    public struct ZoneId : IEquatable<ZoneId>
    {
        public int galaxy;
        public int sx;
        public int sy;
        public int sz;
        public int zx;
        public int zy;
        public int zz;

        public ZoneId(int galaxy, int sx, int sy, int sz, int zx, int zy, int zz)
        {
            this.galaxy = galaxy;
            this.sx = sx;
            this.sy = sy;
            this.sz = sz;
            this.zx = zx;
            this.zy = zy;
            this.zz = zz;
        }

        public static ZoneId FromPosition(int galaxy, Vec3d pos) => DistrictId.FromPosition(galaxy, pos).Zone;

        public Vec3d Origin => new Vec3d(sx * StarSystemId.SectorSizeLy + zx * StarSystemId.ZoneSizeLy, sy * StarSystemId.SectorSizeLy + zy * StarSystemId.ZoneSizeLy, sz * StarSystemId.SectorSizeLy + zz * StarSystemId.ZoneSizeLy);

        public Vec3d Center => Origin + new Vec3d(StarSystemId.ZoneSizeLy / 2, StarSystemId.ZoneSizeLy / 2, StarSystemId.ZoneSizeLy / 2);

        public bool Equals(ZoneId o) => galaxy == o.galaxy && sx == o.sx && sy == o.sy && sz == o.sz && zx == o.zx && zy == o.zy && zz == o.zz;

        public override bool Equals(object obj) => obj is ZoneId o && Equals(o);

        public override int GetHashCode() => (int)UniverseRand.Hash(galaxy, sx, sy, sz, zx, zy, zz, 77);
    }

    public struct Vec3d
    {
        public double x;
        public double y;
        public double z;

        public Vec3d(double x, double y, double z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static Vec3d operator +(Vec3d a, Vec3d b) => new Vec3d(a.x + b.x, a.y + b.y, a.z + b.z);

        public static Vec3d operator -(Vec3d a, Vec3d b) => new Vec3d(a.x - b.x, a.y - b.y, a.z - b.z);

        public static Vec3d operator *(Vec3d a, double f) => new Vec3d(a.x * f, a.y * f, a.z * f);

        public double Length => Math.Sqrt(x * x + y * y + z * z);

        public static double Dot(Vec3d a, Vec3d b) => a.x * b.x + a.y * b.y + a.z * b.z;

        public double PlanarLength => Math.Sqrt(x * x + z * z);

        public static double Distance(Vec3d a, Vec3d b) => (a - b).Length;
    }
}
