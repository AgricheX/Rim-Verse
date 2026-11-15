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

        public float DistanceAU(int from, int to)
        {
            BodyData a = Body(from);
            BodyData b = Body(to);
            if (a == null || b == null) return 0f;
            if (from == to) return 0f;
            float oa = OrbitOf(a);
            float ob = OrbitOf(b);
            float d = System.Math.Abs(oa - ob);
            if (d < 0.01f) d = 0.05f;
            return d;
        }
    }
}
