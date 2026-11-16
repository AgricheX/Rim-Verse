using System.Collections.Generic;

namespace Rimverse
{
    public enum StarType
    {
        RedDwarf,
        Orange,
        Yellow,
        White,
        Blue,
        WhiteDwarf,
        Neutron,
        BlackHole
    }

    public enum BodyType
    {
        Asteroid,
        DwarfPlanet,
        SmallMoon,
        Rocky,
        SuperEarth,
        Ocean,
        GasGiant,
        Belt
    }

    public enum AtmosphereType
    {
        None,
        Thin,
        Breathable,
        Toxic,
        Dense
    }

    public class StarData
    {
        public StarType type;
        public int sizeClass;
        public float luminosity;
        public float massSun;
    }

    public class BodyData
    {
        public int index;
        public int parent = -1;
        public BodyType type;
        public int sizeClass;
        public float orbitAU;
        public float moonOrbitFactor;
        public float radiusEarth;
        public float massEarth;
        public float gravity;
        public float temperatureC;
        public AtmosphereType atmosphere;
        public float water;
        public bool rings;
        public bool homeWorld;
        public string name;
        public float periodYears;
        public float phase;
        public float moonOrbitKm;
        public float densityRel = 1f;

        public float RadiusKm => radiusEarth * 6371f;

        public float DensityGcm3 => densityRel * 5.51f;

        public float EscapeVelocityKms => radiusEarth > 0f ? 11.186f * (float)System.Math.Sqrt(massEarth / radiusEarth) : 0f;

        public float PeriodDays => periodYears * RimverseUnits.DaysPerYear;

        public float AngleAt(long ticks)
        {
            if (periodYears <= 0f) return phase;
            double turns = ticks / (periodYears * (double)RimverseUnits.TicksPerYear);
            return phase + (float)((turns - System.Math.Floor(turns)) * System.Math.PI * 2.0);
        }

        public bool IsMoon => parent >= 0;

        public bool Airless => atmosphere == AtmosphereType.None;

        public bool Landable => type != BodyType.GasGiant && type != BodyType.Belt;

        public bool LandableNow => Landable;
    }

    public class StarSystemData
    {
        public StarSystemId id;
        public string name;
        public int sizeClass;
        public List<StarData> stars = new List<StarData>();
        public List<BodyData> bodies = new List<BodyData>();
        public float frostLineAU;

        public StarData Primary => stars.Count > 0 ? stars[0] : null;

        public float TotalLuminosity
        {
            get
            {
                float l = 0f;
                foreach (StarData s in stars) l += s.luminosity;
                return l;
            }
        }

        public BodyData Body(int index) => index >= 0 && index < bodies.Count ? bodies[index] : null;

        public BodyData HomeWorld => bodies.Find(b => b.homeWorld);

        public float OrbitOf(BodyData b) => b.IsMoon ? bodies[b.parent].orbitAU : b.orbitAU;

        public const float KmPerAU = 149597870.7f;

        public UnityEngine.Vector2 PositionAU(BodyData b, long ticks)
        {
            if (b == null) return UnityEngine.Vector2.zero;
            if (b.IsMoon)
            {
                BodyData p = Body(b.parent);
                float m = b.AngleAt(ticks);
                return PositionAU(p, ticks) + new UnityEngine.Vector2(UnityEngine.Mathf.Cos(m), UnityEngine.Mathf.Sin(m)) * (b.moonOrbitKm / KmPerAU);
            }
            if (b.type == BodyType.Belt) return new UnityEngine.Vector2(b.orbitAU, 0f);
            float a = b.AngleAt(ticks);
            return new UnityEngine.Vector2(UnityEngine.Mathf.Cos(a), UnityEngine.Mathf.Sin(a)) * b.orbitAU;
        }

        public static long NowTicks => Verse.Find.TickManager != null ? Verse.Find.TickManager.TicksAbs : 0L;

        public float DistanceAU(int from, int to)
        {
            BodyData a = Body(from);
            BodyData b = Body(to);
            if (a == null || b == null) return 0f;
            if (from == to) return 0f;
            float d;
            if (a.type == BodyType.Belt || b.type == BodyType.Belt)
            {
                d = System.Math.Abs(OrbitOf(a) - OrbitOf(b));
            }
            else
            {
                long now = NowTicks;
                d = (PositionAU(a, now) - PositionAU(b, now)).magnitude;
            }
            if (d < 0.01f) d = a.IsMoon || b.IsMoon ? UnityEngine.Mathf.Max(d, 0.003f) : 0.05f;
            return d;
        }

        public float TotalMassSun
        {
            get
            {
                float m = 0f;
                foreach (StarData s in stars) m += s.massSun;
                return UnityEngine.Mathf.Max(0.05f, m);
            }
        }
    }
}
