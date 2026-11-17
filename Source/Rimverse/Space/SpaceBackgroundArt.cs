using System;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public static class SpaceBackgroundArt
    {
        public static Color StarColor(StarType t)
        {
            switch (t)
            {
                case StarType.RedDwarf: return new Color(1f, 0.55f, 0.38f);
                case StarType.Orange: return new Color(1f, 0.74f, 0.48f);
                case StarType.Yellow: return new Color(1f, 0.93f, 0.78f);
                case StarType.White: return new Color(0.93f, 0.95f, 1f);
                case StarType.Blue: return new Color(0.66f, 0.78f, 1f);
                case StarType.WhiteDwarf: return new Color(0.85f, 0.9f, 1f);
                case StarType.Neutron: return new Color(0.7f, 0.85f, 1f);
                case StarType.Pulsar: return new Color(0.8f, 0.9f, 1f);
                default: return new Color(1f, 0.6f, 0.3f);
            }
        }

        public static float Fbm(float x, float y, int octaves, float ox, float oy)
        {
            float sum = 0f, amp = 0.5f, freq = 1f;
            for (int i = 0; i < octaves; i++)
            {
                sum += amp * Mathf.PerlinNoise(x * freq + ox + i * 17.3f, y * freq + oy - i * 11.7f);
                freq *= 2.03f;
                amp *= 0.5f;
            }
            return sum;
        }

        public static Texture2D NewTex(int size, bool smooth = true)
        {
            Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, true);
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = smooth ? FilterMode.Trilinear : FilterMode.Point;
            t.anisoLevel = 2;
            return t;
        }

        public static Texture2D Glow(int size, float core, float falloff)
        {
            Texture2D t = NewTex(size);
            Color[] px = new Color[size * size];
            float half = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = new Vector2(x + 0.5f - half, y + 0.5f - half).magnitude / half;
                    float a = d < core ? 1f : Mathf.Pow(Mathf.Clamp01(1f - (d - core) / (1f - core)), falloff);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            t.SetPixels(px);
            t.Apply(true, true);
            return t;
        }

        public static Texture2D Sky(int size, SkyParams p)
        {
            Texture2D t = NewTex(size);
            Color[] px = new Color[size * size];
            UniverseRand r = new UniverseRand(p.seed);
            float ox = r.Range(0f, 1000f), oy = r.Range(0f, 1000f);
            float nx = r.Range(0f, 1000f), ny = r.Range(0f, 1000f);
            Vector2 bandDir = new Vector2(Mathf.Cos(p.bandAngle), Mathf.Sin(p.bandAngle));
            Vector2 bandNormal = new Vector2(-bandDir.y, bandDir.x);
            Vector2 core = new Vector2(0.5f, 0.5f) + bandDir * p.coreOffset;
            Color baseCol = new Color(0.008f, 0.010f, 0.022f);
            Color bandCol = Color.Lerp(new Color(0.55f, 0.60f, 0.78f), new Color(0.95f, 0.82f, 0.62f), p.bandWarmth);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                    Vector2 q = new Vector2(u, v);
                    Color c = baseCol;
                    float n = Fbm(u * 3f, v * 3f, 5, ox, oy);
                    if (p.bandStrength > 0f)
                    {
                        float dist = Vector2.Dot(q - new Vector2(0.5f, 0.5f), bandNormal);
                        float w = p.bandWidth * (0.8f + 0.5f * n);
                        float band = Mathf.Exp(-dist * dist / (2f * w * w));
                        float dust = Mathf.Clamp01((Fbm(u * 9f, v * 9f, 4, ox + 50f, oy) - 0.48f) * 4f) * Mathf.Exp(-dist * dist / (2f * w * w * 0.25f));
                        float coreGlow = Mathf.Exp(-(q - core).sqrMagnitude / (2f * p.coreSize * p.coreSize)) * p.coreStrength;
                        float b = p.bandStrength * band * (0.55f + 0.7f * n) * (1f - 0.75f * dust) + coreGlow * (1f - 0.5f * dust);
                        c += bandCol * b;
                    }
                    if (p.haze > 0f) c += bandCol * (p.haze * (0.6f + 0.4f * n));
                    if (p.nebula > 0f)
                    {
                        float m = Fbm(u * 2.2f, v * 2.2f, 6, nx, ny);
                        float cloud = Mathf.Clamp01((m - 0.52f) * 3.2f);
                        float fil = Mathf.Clamp01((Fbm(u * 7f, v * 7f, 4, nx + 9f, ny) - 0.5f) * 3f);
                        c += Color.Lerp(p.nebulaA, p.nebulaB, fil) * cloud * p.nebula;
                    }
                    foreach (SkySmudge s in p.smudges)
                    {
                        Vector2 d = q - s.pos;
                        float along = Vector2.Dot(d, s.axis) / s.size;
                        float across = Vector2.Dot(d, new Vector2(-s.axis.y, s.axis.x)) / (s.size * s.flat);
                        float g = Mathf.Exp(-(along * along + across * across) * 3f);
                        if (g > 0.002f) c += s.color * g * s.bright;
                    }
                    c.a = 1f;
                    px[y * size + x] = c;
                }
            }
            t.SetPixels(px);
            t.Apply(true, true);
            return t;
        }

        public static Texture2D Planet(int size, BodyData b, ulong seed, float frostLineAU, bool airlessHome)
        {
            Texture2D t = NewTex(size);
            Color[] px = new Color[size * size];
            UniverseRand r = new UniverseRand(seed);
            float ox = r.Range(0f, 1000f), oy = r.Range(0f, 1000f);
            bool rings = b.rings;
            float diskR = rings ? 0.30f : 0.48f;
            Vector2 c0 = new Vector2(0.5f, 0.5f);
            Color atmo = AtmosphereColor(b);
            float atmoThick = b.atmosphere == AtmosphereType.None ? 0f : b.atmosphere == AtmosphereType.Dense || b.type == BodyType.GasGiant ? 0.06f : b.atmosphere == AtmosphereType.Thin ? 0.02f : 0.04f;
            bool icy = b.orbitAU > frostLineAU && b.type != BodyType.GasGiant;
            float ringTilt = r.Range(0.18f, 0.32f);
            float ringIn = diskR * 1.25f, ringOut = diskR * r.Range(1.9f, 2.3f);
            Color g1 = Color.HSVToRGB(r.Range(0.05f, 0.14f), r.Range(0.25f, 0.55f), r.Range(0.75f, 0.95f));
            Color g2 = Color.HSVToRGB(r.Range(0.02f, 0.12f), r.Range(0.35f, 0.7f), r.Range(0.45f, 0.7f));
            if (r.Chance(0.3f)) { g1 = Color.HSVToRGB(r.Range(0.52f, 0.62f), r.Range(0.2f, 0.45f), r.Range(0.75f, 0.95f)); g2 = Color.HSVToRGB(r.Range(0.55f, 0.65f), r.Range(0.35f, 0.6f), r.Range(0.5f, 0.75f)); }
            Color rock1 = Color.HSVToRGB(r.Range(0.03f, 0.11f), r.Range(0.15f, 0.45f), r.Range(0.45f, 0.7f));
            Color rock2 = Color.HSVToRGB(r.Range(0.0f, 0.1f), r.Range(0.1f, 0.35f), r.Range(0.25f, 0.45f));
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 q = new Vector2((x + 0.5f) / size, (y + 0.5f) / size) - c0;
                    Color outc = new Color(0f, 0f, 0f, 0f);
                    Color ringc = new Color(0f, 0f, 0f, 0f);
                    float d = q.magnitude / diskR;
                    if (rings)
                    {
                        Vector2 rq = new Vector2(q.x, q.y / ringTilt);
                        float rd = rq.magnitude;
                        if (rd > ringIn && rd < ringOut)
                        {
                            float k = (rd - ringIn) / (ringOut - ringIn);
                            float bands = 0.55f + 0.45f * Mathf.PerlinNoise(k * 40f + ox, 0.5f);
                            float gap = Mathf.Abs(k - 0.62f) < 0.03f ? 0.15f : 1f;
                            float shadow = q.x < 0f ? Mathf.Clamp01((diskR - Mathf.Abs(q.y)) / (diskR * 0.08f)) * Mathf.Clamp01(-q.x / (diskR * 0.1f)) : 0f;
                            float lit = 1f - 0.75f * shadow;
                            Color rc = Color.Lerp(g1, Color.white, 0.3f) * bands * lit;
                            rc.a = 0.75f * bands * gap * Mathf.Clamp01(Mathf.Min(k, 1f - k) * 12f);
                            ringc = rc;
                        }
                    }
                    if (d < 1f)
                    {
                        float zz = Mathf.Sqrt(1f - d * d);
                        Vector3 n = new Vector3(q.x / diskR, q.y / diskR, zz);
                        float lon = Mathf.Atan2(n.x, n.z), lat = Mathf.Asin(Mathf.Clamp(n.y, -1f, 1f));
                        float su = lon * 0.9f, sv = lat * 1.2f;
                        Color surf = Surface(b, icy, airlessHome, su, sv, lat, ox, oy, g1, g2, rock1, rock2);
                        float light = Mathf.Clamp01(Vector3.Dot(n, new Vector3(0.82f, 0.18f, 0.55f).normalized));
                        float term = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(light * 2.2f + 0.02f));
                        Color lit = surf * (0.03f + 0.97f * term);
                        if (atmoThick > 0f)
                        {
                            float rim = Mathf.Pow(1f - zz, 3f);
                            lit = Color.Lerp(lit, atmo * Mathf.Clamp01(light * 1.6f + 0.15f), rim * 0.8f);
                        }
                        lit.a = Mathf.Clamp01((1f - d) * size * diskR * 0.9f);
                        outc = q.y < 0f ? Blend(lit, ringc) : Blend(ringc, lit);
                    }
                    else if (atmoThick > 0f && d < 1f + atmoThick / diskR)
                    {
                        float k = 1f - (d - 1f) / (atmoThick / diskR);
                        Vector2 dir = q.normalized;
                        float light = Mathf.Clamp01(Vector2.Dot(dir, new Vector2(0.95f, 0.3f).normalized) * 0.8f + 0.35f);
                        Color a = atmo * light;
                        a.a = k * k * 0.85f * light;
                        outc = Blend(a, ringc);
                    }
                    else outc = ringc;
                    px[y * size + x] = outc;
                }
            }
            t.SetPixels(px);
            t.Apply(true, true);
            return t;
        }

        private static Color Blend(Color under, Color over)
        {
            float a = over.a + under.a * (1f - over.a);
            if (a <= 0f) return new Color(0f, 0f, 0f, 0f);
            Color c = (over * over.a + under * under.a * (1f - over.a)) / a;
            c.a = a;
            return c;
        }

        private static Color Surface(BodyData b, bool icy, bool airlessHome, float u, float v, float lat, float ox, float oy, Color g1, Color g2, Color rock1, Color rock2)
        {
            if (b.type == BodyType.GasGiant)
            {
                float turb = Fbm(u * 3f, v * 3f, 4, ox, oy);
                float band = Mathf.Sin((lat * 9f + turb * 2.4f) * 2f) * 0.5f + 0.5f;
                float fine = Mathf.Sin((lat * 31f + turb * 5f)) * 0.5f + 0.5f;
                Color gc = Color.Lerp(g2, g1, band * 0.75f + fine * 0.25f);
                float storm = Mathf.Exp(-((u - 0.4f) * (u - 0.4f) * 18f + (v + 0.25f) * (v + 0.25f) * 60f));
                return Color.Lerp(gc, g2 * 0.8f, storm * 0.7f);
            }
            float h = Fbm(u * 2.4f, v * 2.4f, 6, ox, oy);
            float detail = Fbm(u * 12f, v * 12f, 3, ox + 31f, oy);
            bool habit = b.homeWorld || b.atmosphere == AtmosphereType.Breathable;
            float water = b.type == BodyType.Ocean ? Mathf.Max(0.8f, b.water) : b.water;
            Color c;
            if (water > 0.05f && h < 0.25f + water * 0.35f)
            {
                c = Color.Lerp(new Color(0.03f, 0.12f, 0.32f), new Color(0.08f, 0.32f, 0.55f), h * 1.6f);
            }
            else if (habit)
            {
                c = Color.Lerp(new Color(0.18f, 0.35f, 0.14f), new Color(0.55f, 0.48f, 0.3f), Mathf.Clamp01((h - 0.45f) * 3f + detail * 0.3f));
            }
            else if (icy)
            {
                c = Color.Lerp(new Color(0.62f, 0.7f, 0.78f), new Color(0.92f, 0.95f, 1f), detail);
            }
            else
            {
                c = Color.Lerp(rock2, rock1, Mathf.Clamp01(h * 1.3f - 0.1f + detail * 0.25f));
                float crater = Mathf.Clamp01((Mathf.PerlinNoise(u * 22f + ox, v * 22f + oy) - 0.72f) * 6f);
                c *= 1f - crater * 0.35f;
            }
            if (habit || water > 0.1f || icy)
            {
                float cap = Mathf.Clamp01((Mathf.Abs(lat) - 1.02f + (detail - 0.5f) * 0.25f) * 5f);
                c = Color.Lerp(c, new Color(0.93f, 0.95f, 0.98f), cap);
            }
            if (b.atmosphere == AtmosphereType.Toxic) c = Color.Lerp(c, new Color(0.75f, 0.7f, 0.3f), 0.45f);
            if (b.atmosphere == AtmosphereType.Dense) c = Color.Lerp(c, new Color(0.85f, 0.75f, 0.55f), 0.6f);
            if (b.atmosphere != AtmosphereType.None)
            {
                float cl = Fbm(u * 4f + 3f, v * 6f, 5, ox + 77f, oy + 13f);
                float clouds = Mathf.Clamp01((cl - (b.atmosphere == AtmosphereType.Thin ? 0.62f : 0.5f)) * 3.5f);
                c = Color.Lerp(c, Color.white, clouds * 0.85f);
            }
            c.a = 1f;
            return c;
        }

        public static Color AtmosphereColor(BodyData b)
        {
            if (b.type == BodyType.GasGiant) return new Color(0.75f, 0.8f, 1f);
            switch (b.atmosphere)
            {
                case AtmosphereType.Breathable: return new Color(0.45f, 0.65f, 1f);
                case AtmosphereType.Toxic: return new Color(0.85f, 0.85f, 0.4f);
                case AtmosphereType.Dense: return new Color(1f, 0.82f, 0.55f);
                case AtmosphereType.Thin: return new Color(0.85f, 0.6f, 0.5f);
                default: return Color.clear;
            }
        }

        public static Texture2D Rock(int size, ulong seed)
        {
            Texture2D t = NewTex(size);
            Color[] px = new Color[size * size];
            UniverseRand r = new UniverseRand(seed);
            float ox = r.Range(0f, 999f), oy = r.Range(0f, 999f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 q = new Vector2((x + 0.5f) / size - 0.5f, (y + 0.5f) / size - 0.5f) * 2f;
                    float ang = Mathf.Atan2(q.y, q.x);
                    float edge = 0.72f + 0.22f * Fbm(Mathf.Cos(ang) * 1.5f + 2f, Mathf.Sin(ang) * 1.5f + 2f, 3, ox, oy);
                    float d = q.magnitude / edge;
                    if (d >= 1f) { px[y * size + x] = Color.clear; continue; }
                    float sh = Mathf.Clamp01(0.35f + 0.65f * Vector2.Dot(q, new Vector2(0.7f, 0.7f)) + 0.25f);
                    float tex = 0.7f + 0.3f * Fbm(q.x * 4f, q.y * 4f, 4, ox + 5f, oy);
                    Color c = new Color(0.36f, 0.33f, 0.31f) * sh * tex;
                    c.a = Mathf.Clamp01((1f - d) * size * 0.25f);
                    px[y * size + x] = c;
                }
            }
            t.SetPixels(px);
            t.Apply(true, true);
            return t;
        }
    }

    public class SkySmudge
    {
        public Vector2 pos;
        public Vector2 axis;
        public float size;
        public float flat;
        public float bright;
        public Color color;
    }

    public class SkyParams
    {
        public ulong seed;
        public float bandAngle;
        public float bandWidth = 0.08f;
        public float bandStrength;
        public float bandWarmth;
        public float coreOffset;
        public float coreSize = 0.1f;
        public float coreStrength;
        public float haze;
        public float nebula;
        public Color nebulaA;
        public Color nebulaB;
        public System.Collections.Generic.List<SkySmudge> smudges = new System.Collections.Generic.List<SkySmudge>();
    }
}
