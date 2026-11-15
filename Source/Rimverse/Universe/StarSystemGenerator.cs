using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public static class StarSystemGenerator
    {
        private static readonly StarType[] StarTypes = { StarType.RedDwarf, StarType.Orange, StarType.Yellow, StarType.White, StarType.WhiteDwarf, StarType.Blue, StarType.Neutron, StarType.BlackHole };
        private static readonly float[] StarWeights = { 75f, 12f, 7f, 3f, 2f, 0.6f, 0.3f, 0.1f };
        private static readonly string[] SyllA = { "ka", "ve", "or", "ta", "mi", "su", "ra", "el", "dor", "xa", "ny", "lo", "ze", "qui", "an", "be", "cy", "ith" };
        private static readonly string[] SyllB = { "ros", "nia", "lix", "tar", "mon", "dra", "vus", "phe", "rion", "sa", "ket", "lune", "gor", "bis", "tha", "ria" };
        private static readonly string[] Letters = { "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m", "n", "o", "p", "q", "r", "s", "t", "u", "v", "w", "x", "y", "z" };
        private static readonly string[] Roman = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII" };

        public static StarSystemData Generate(StarSystemId id, int universeSeed, bool home, string homeWorldName)
        {
            UniverseRand rand = new UniverseRand(id.Seed(universeSeed));
            StarSystemData sys = new StarSystemData { id = id };
            sys.name = MakeName(rand);
            sys.sizeClass = home ? Math.Max(35, rand.SkewedSizeClass(1, 100)) : rand.SkewedSizeClass(1, 100);
            StarType primaryType = PrimaryTypeFor(id, universeSeed, home);
            sys.stars.Add(MakeStar(primaryType, rand));
            if (!home && rand.Chance(0.33f))
            {
                sys.stars.Add(MakeStar(rand.Chance(0.7f) ? StarType.RedDwarf : rand.Weighted(StarTypes, StarWeights), rand));
            }
            float lum = Mathf.Max(0.0001f, sys.TotalLuminosity);
            sys.frostLineAU = 2.7f * Mathf.Sqrt(lum);
            int planetCount = Mathf.Clamp(Mathf.RoundToInt(1 + sys.sizeClass / 100f * 14f) + (sys.stars.Count - 1) * 2, 1, 18);
            if (primaryType == StarType.BlackHole || primaryType == StarType.Neutron) planetCount = Mathf.Max(1, planetCount / 3);
            float orbit = rand.Range(0.15f, 0.4f) * Mathf.Sqrt(Mathf.Max(lum, 0.01f));
            List<BodyData> planets = new List<BodyData>();
            for (int i = 0; i < planetCount; i++)
            {
                BodyData b = new BodyData { orbitAU = orbit };
                b.type = PickPlanetType(rand, orbit, sys.frostLineAU);
                b.sizeClass = SizeFor(b.type, rand);
                FillPhysics(b, rand, lum);
                planets.Add(b);
                orbit *= rand.Range(1.4f, 2.0f);
            }
            if (home)
            {
                float target = Mathf.Sqrt(lum);
                int best = 0;
                for (int i = 1; i < planets.Count; i++)
                {
                    if (Mathf.Abs(planets[i].orbitAU - target) < Mathf.Abs(planets[best].orbitAU - target)) best = i;
                }
                if (planets.Count < 3)
                {
                    planets.Add(new BodyData { orbitAU = target * 1.6f, type = BodyType.Rocky, sizeClass = 30 });
                    FillPhysics(planets[planets.Count - 1], rand, lum);
                }
                BodyData hw = planets[best];
                hw.orbitAU = target;
                hw.type = BodyType.Rocky;
                hw.sizeClass = 45;
                hw.radiusEarth = 1f;
                hw.massEarth = 1f;
                hw.gravity = 1f;
                hw.temperatureC = 15f;
                hw.atmosphere = AtmosphereType.Breathable;
                hw.water = 0.7f;
                hw.homeWorld = true;
                hw.name = homeWorldName;
            }
            planets.Sort((a, b) => a.orbitAU.CompareTo(b.orbitAU));
            int letter = 0;
            foreach (BodyData p in planets)
            {
                p.index = sys.bodies.Count;
                if (p.name == null) p.name = p.type == BodyType.Belt ? (string)"Rimverse_BeltName".Translate(sys.name, Roman[Math.Min(letter, Roman.Length - 1)]) : sys.name + " " + Letters[Math.Min(letter, Letters.Length - 1)];
                letter++;
                sys.bodies.Add(p);
                int moons = MoonCount(p, rand);
                for (int m = 0; m < moons; m++)
                {
                    BodyData moon = new BodyData { parent = p.index, orbitAU = p.orbitAU, moonOrbitFactor = 1f + m };
                    moon.type = p.type == BodyType.GasGiant && rand.Chance(0.25f) ? BodyType.Rocky : (rand.Chance(0.5f) ? BodyType.SmallMoon : BodyType.DwarfPlanet);
                    moon.sizeClass = Mathf.Min(SizeFor(moon.type, rand), Mathf.Max(1, p.sizeClass - 5));
                    FillPhysics(moon, rand, lum);
                    moon.index = sys.bodies.Count;
                    moon.name = p.name + " " + Roman[Math.Min(m, Roman.Length - 1)];
                    sys.bodies.Add(moon);
                }
            }
            return sys;
        }

        public static StarType PrimaryTypeFor(StarSystemId id, int universeSeed, bool home)
        {
            UniverseRand r = new UniverseRand(UniverseRand.Hash((long)id.Seed(universeSeed), 7));
            if (home) return r.Chance(0.6f) ? StarType.Yellow : StarType.Orange;
            return r.Weighted(StarTypes, StarWeights);
        }

        private static string MakeName(UniverseRand rand)
        {
            string a = SyllA[rand.RangeInclusive(0, SyllA.Length - 1)];
            string b = SyllB[rand.RangeInclusive(0, SyllB.Length - 1)];
            string name = a + b;
            if (rand.Chance(0.3f)) name += "-" + rand.RangeInclusive(2, 999);
            return char.ToUpperInvariant(name[0]) + name.Substring(1);
        }

        private static StarData MakeStar(StarType type, UniverseRand rand)
        {
            StarData s = new StarData { type = type };
            switch (type)
            {
                case StarType.RedDwarf: s.luminosity = rand.Range(0.001f, 0.08f); s.massSun = rand.Range(0.08f, 0.45f); s.sizeClass = rand.RangeInclusive(1, 20); break;
                case StarType.Orange: s.luminosity = rand.Range(0.08f, 0.6f); s.massSun = rand.Range(0.45f, 0.8f); s.sizeClass = rand.RangeInclusive(15, 35); break;
                case StarType.Yellow: s.luminosity = rand.Range(0.6f, 1.5f); s.massSun = rand.Range(0.8f, 1.04f); s.sizeClass = rand.RangeInclusive(30, 50); break;
                case StarType.White: s.luminosity = rand.Range(1.5f, 25f); s.massSun = rand.Range(1.04f, 2.1f); s.sizeClass = rand.RangeInclusive(45, 70); break;
                case StarType.Blue: s.luminosity = rand.Range(25f, 30000f); s.massSun = rand.Range(2.1f, 30f); s.sizeClass = rand.RangeInclusive(70, 100); break;
                case StarType.WhiteDwarf: s.luminosity = rand.Range(0.0001f, 0.01f); s.massSun = rand.Range(0.5f, 1.2f); s.sizeClass = rand.RangeInclusive(1, 5); break;
                case StarType.Neutron: s.luminosity = rand.Range(0.00001f, 0.001f); s.massSun = rand.Range(1.4f, 2.1f); s.sizeClass = 1; break;
                default: s.luminosity = 0f; s.massSun = rand.Range(5f, 30f); s.sizeClass = rand.RangeInclusive(1, 10); break;
            }
            return s;
        }

        private static BodyType PickPlanetType(UniverseRand rand, float orbit, float frost)
        {
            if (orbit < frost)
            {
                return rand.Weighted(new[] { BodyType.Rocky, BodyType.SuperEarth, BodyType.Ocean, BodyType.DwarfPlanet, BodyType.Belt, BodyType.GasGiant }, new[] { 45f, 15f, 10f, 15f, 10f, 5f });
            }
            return rand.Weighted(new[] { BodyType.GasGiant, BodyType.DwarfPlanet, BodyType.Belt, BodyType.Ocean, BodyType.Rocky }, new[] { 55f, 25f, 15f, 3f, 2f });
        }

        public static int SizeFor(BodyType type, UniverseRand rand)
        {
            switch (type)
            {
                case BodyType.Asteroid: return rand.SkewedSizeClass(1, 10, 1.5f);
                case BodyType.DwarfPlanet:
                case BodyType.SmallMoon: return rand.SkewedSizeClass(5, 25, 1.5f);
                case BodyType.Rocky: return rand.SkewedSizeClass(20, 60, 1.3f);
                case BodyType.SuperEarth: return rand.SkewedSizeClass(55, 75, 1.3f);
                case BodyType.Ocean: return rand.SkewedSizeClass(30, 70, 1.3f);
                case BodyType.GasGiant: return rand.SkewedSizeClass(75, 100, 1.3f);
                default: return rand.SkewedSizeClass(1, 30, 1.5f);
            }
        }

        private static void Range(BodyType type, out int sMin, out int sMax, out float rMin, out float rMax, out float density)
        {
            switch (type)
            {
                case BodyType.Asteroid: sMin = 1; sMax = 10; rMin = 0.001f; rMax = 0.03f; density = 0.5f; break;
                case BodyType.DwarfPlanet:
                case BodyType.SmallMoon: sMin = 5; sMax = 25; rMin = 0.05f; rMax = 0.3f; density = 0.6f; break;
                case BodyType.SuperEarth: sMin = 55; sMax = 75; rMin = 1.3f; rMax = 2.0f; density = 1.1f; break;
                case BodyType.Ocean: sMin = 30; sMax = 70; rMin = 0.6f; rMax = 1.8f; density = 0.8f; break;
                case BodyType.GasGiant: sMin = 75; sMax = 100; rMin = 3.5f; rMax = 12f; density = 0.24f; break;
                case BodyType.Belt: sMin = 1; sMax = 30; rMin = 0f; rMax = 0f; density = 0f; break;
                default: sMin = 20; sMax = 60; rMin = 0.3f; rMax = 1.3f; density = 1f; break;
            }
        }

        private static void FillPhysics(BodyData b, UniverseRand rand, float lum)
        {
            Range(b.type, out int sMin, out int sMax, out float rMin, out float rMax, out float density);
            float t = sMax == sMin ? 0f : Mathf.Clamp01((b.sizeClass - sMin) / (float)(sMax - sMin));
            b.radiusEarth = Mathf.Lerp(rMin, rMax, t);
            b.massEarth = density * b.radiusEarth * b.radiusEarth * b.radiusEarth;
            b.gravity = b.type == BodyType.Belt ? 0f : density * b.radiusEarth;
            b.atmosphere = AtmosphereType.None;
            if (b.type == BodyType.GasGiant) b.atmosphere = AtmosphereType.Dense;
            else if ((b.type == BodyType.Rocky || b.type == BodyType.SuperEarth || b.type == BodyType.Ocean) && b.gravity >= 0.4f)
            {
                b.atmosphere = rand.Weighted(new[] { AtmosphereType.Thin, AtmosphereType.Breathable, AtmosphereType.Toxic, AtmosphereType.Dense }, new[] { 35f, 20f, 30f, 15f });
            }
            float eq = 278f * Mathf.Pow(Mathf.Max(lum, 0f), 0.25f) / Mathf.Sqrt(Mathf.Max(b.orbitAU, 0.01f)) - 273f;
            float greenhouse = b.atmosphere == AtmosphereType.Dense ? 60f : b.atmosphere == AtmosphereType.Breathable ? 15f : b.atmosphere == AtmosphereType.Thin ? 5f : b.atmosphere == AtmosphereType.Toxic ? 30f : 0f;
            b.temperatureC = eq + greenhouse;
            if (b.atmosphere == AtmosphereType.Breathable && (b.temperatureC < -30f || b.temperatureC > 45f)) b.atmosphere = AtmosphereType.Thin;
            b.water = b.type == BodyType.Ocean ? rand.Range(0.8f, 1f) : (b.atmosphere != AtmosphereType.None && b.temperatureC > -10f && b.temperatureC < 60f ? rand.Range(0f, 0.7f) : 0f);
            b.rings = b.type == BodyType.GasGiant && rand.Chance(0.4f);
        }

        private static int MoonCount(BodyData p, UniverseRand rand)
        {
            if (p.homeWorld) return rand.RangeInclusive(0, 2);
            switch (p.type)
            {
                case BodyType.GasGiant: return rand.RangeInclusive(1, 6);
                case BodyType.SuperEarth:
                case BodyType.Rocky:
                case BodyType.Ocean: return rand.Chance(0.5f) ? rand.RangeInclusive(1, 2) : 0;
                default: return 0;
            }
        }
    }
}
