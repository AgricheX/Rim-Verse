using System;

namespace Rimverse
{
    public struct StarSystemId : IEquatable<StarSystemId>
    {
        public const double SectorSizeLy = 8192.0;
        public const double DistrictSizeLy = 64.0;
        public const int DistrictsPerSector = 128;

        public int galaxy;
        public int sx;
        public int sy;
        public int sz;
        public int dx;
        public int dy;
        public int dz;
        public int index;

        public StarSystemId(int galaxy, int sx, int sy, int sz, int dx, int dy, int dz, int index)
        {
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

        public bool Equals(DistrictId o) => galaxy == o.galaxy && sx == o.sx && sy == o.sy && sz == o.sz && dx == o.dx && dy == o.dy && dz == o.dz;

        public override bool Equals(object obj) => obj is DistrictId o && Equals(o);

        public override int GetHashCode() => (int)UniverseRand.Hash(galaxy, sx, sy, sz, dx, dy, dz);
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

        public double PlanarLength => Math.Sqrt(x * x + z * z);

        public static double Distance(Vec3d a, Vec3d b) => (a - b).Length;
    }
}
