using System;
using UnityEngine;

namespace Rimverse
{
    public static class MegastructureArt
    {
        private static float Hash01(int a, int b)
        {
            unchecked
            {
                uint h = (uint)(a * 374761393 + b * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xffffff) / 16777215f;
            }
        }

        private static void Over(Color[] px, int size, int x, int y, Color c)
        {
            if (x < 0 || y < 0 || x >= size || y >= size || c.a <= 0f) return;
            int i = y * size + x;
            Color d = px[i];
            float a = c.a + d.a * (1f - c.a);
            if (a <= 0f) return;
            px[i] = new Color((c.r * c.a + d.r * d.a * (1f - c.a)) / a, (c.g * c.a + d.g * d.a * (1f - c.a)) / a, (c.b * c.a + d.b * d.a * (1f - c.a)) / a, a);
        }

        private static void Dot(Color[] px, int size, float cx, float cy, float r, Color c)
        {
            int x0 = (int)(cx - r - 1), x1 = (int)(cx + r + 1), y0 = (int)(cy - r - 1), y1 = (int)(cy + r + 1);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float d = new Vector2(x + 0.5f - cx, y + 0.5f - cy).magnitude;
                    float a = Mathf.Clamp01(r + 0.5f - d);
                    if (a > 0f) Over(px, size, x, y, new Color(c.r, c.g, c.b, c.a * a));
                }
            }
        }

        private static Texture2D Finish(int size, Color[] px)
        {
            Texture2D t = SpaceBackgroundArt.NewTex(size);
            t.SetPixels(px);
            t.Apply(true, true);
            return t;
        }

        public static Texture2D RingHalf(int size, float aspect, float completion, bool front, bool habitat, int seed)
        {
            Color[] px = new Color[size * size];
            float c = size / 2f;
            float rx = size * 0.48f;
            float ry = rx * aspect;
            float thick = 0.045f;
            Color metal = new Color(0.66f, 0.68f, 0.72f);
            Color inner = habitat ? new Color(0.36f, 0.62f, 0.42f) : new Color(0.52f, 0.62f, 0.78f);
            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f - c) / ry;
                if (front ? v > 0.05f : v < -0.05f) continue;
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f - c) / rx;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float d = Mathf.Abs(r - 1f);
                    float edge = thick * (0.6f + 0.4f * Mathf.Abs(v));
                    if (d > edge + 0.01f) continue;
                    float aa = Mathf.Clamp01((edge + 0.004f - d) / 0.008f);
                    float theta = (Mathf.Atan2(v, u) / (2f * Mathf.PI) + 1f) % 1f;
                    bool builtHere = theta <= completion;
                    float across = (r - 1f) / edge;
                    float seg = (theta * 192f) % 1f;
                    Color col;
                    float alpha;
                    if (builtHere)
                    {
                        float shade = 0.55f + 0.45f * (front ? 1f - Mathf.Abs(v) * 0.5f : 0.6f + 0.4f * Mathf.Abs(u));
                        col = across < -0.2f && front == false ? Color.Lerp(metal, inner, 0.7f) : metal;
                        if (across > -0.2f && across < 0.25f) col = Color.Lerp(col, inner, 0.35f);
                        col = col * shade;
                        if (seg < 0.06f) col = col * 0.55f;
                        if (Mathf.Abs(across) > 0.8f) col = col * 0.7f;
                        float lights = Hash01((int)(theta * 4000f), (int)((across + 1f) * 6f) + seed);
                        if (lights > 0.93f) col = Color.Lerp(col, new Color(1f, 0.85f, 0.55f), 0.8f);
                        col.a = 1f;
                        alpha = aa;
                    }
                    else
                    {
                        bool lattice = seg < 0.12f || Mathf.Abs(across) > 0.75f;
                        if (!lattice) continue;
                        col = new Color(0.45f, 0.47f, 0.52f);
                        alpha = aa * 0.45f;
                    }
                    Over(px, size, x, y, new Color(col.r, col.g, col.b, alpha));
                }
            }
            return Finish(size, px);
        }

        public static Texture2D Swarm(int size, float completion, int seed)
        {
            Color[] px = new Color[size * size];
            float c = size / 2f;
            System.Random r = new System.Random(seed);
            int shells = 4;
            for (int s = 0; s < shells; s++)
            {
                float rad = size * (0.26f + 0.05f * s);
                float aspect = 0.25f + 0.55f * (float)r.NextDouble();
                float rot = (float)(r.NextDouble() * Math.PI);
                float cr = Mathf.Cos(rot), sr = Mathf.Sin(rot);
                for (int k = 0; k < 720; k++)
                {
                    float a = k / 720f * 2f * Mathf.PI;
                    float ex = Mathf.Cos(a) * rad, ey = Mathf.Sin(a) * rad * aspect;
                    Over(px, size, (int)(c + ex * cr - ey * sr), (int)(c + ex * sr + ey * cr), new Color(1f, 0.85f, 0.5f, 0.07f));
                }
                int n = (int)(500 * completion);
                for (int k = 0; k < n; k++)
                {
                    float a = (float)(r.NextDouble() * 2 * Math.PI);
                    float jitter = 1f + ((float)r.NextDouble() - 0.5f) * 0.04f;
                    float ex = Mathf.Cos(a) * rad * jitter, ey = Mathf.Sin(a) * rad * aspect * jitter;
                    float front = Mathf.Sin(a);
                    Color col = front < 0f ? new Color(1f, 0.92f, 0.7f, 0.95f) : new Color(0.85f, 0.7f, 0.45f, 0.6f);
                    Dot(px, size, c + ex * cr - ey * sr, c + ex * sr + ey * cr, size / 512f * (front < 0f ? 1.3f : 0.9f), col);
                }
            }
            return Finish(size, px);
        }

        public static Texture2D Cylinder(int size, float completion, int seed)
        {
            Color[] px = new Color[size * size];
            float c = size / 2f;
            float half = size * 0.40f;
            float rad = size * 0.11f;
            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f - c) / rad;
                if (Mathf.Abs(v) > 1.02f) continue;
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f - c) / half;
                    if (Mathf.Abs(u) > 1f) continue;
                    float t = (u + 1f) / 2f;
                    bool built = t <= completion;
                    float aa = Mathf.Clamp01((1.02f - Mathf.Abs(v)) * rad / 1.5f);
                    float shade = Mathf.Sqrt(Mathf.Max(0f, 1f - v * v)) * (0.55f + 0.45f * Mathf.Clamp01(1f - v));
                    float ring = (t * 40f) % 1f;
                    Color col;
                    float alpha = aa;
                    if (built)
                    {
                        col = new Color(0.68f, 0.7f, 0.74f) * (0.3f + 0.75f * shade);
                        float stripe = Mathf.Abs(v) % 0.66f;
                        if (stripe < 0.12f && Mathf.Abs(v) < 0.9f) col = Color.Lerp(col, new Color(0.55f, 0.8f, 1f), 0.6f);
                        if (ring < 0.04f) col = col * 0.6f;
                        if (Hash01(x / 2, y / 2 + seed) > 0.985f) col = Color.Lerp(col, new Color(1f, 0.85f, 0.6f), 0.7f);
                        col.a = 1f;
                    }
                    else
                    {
                        if (ring > 0.08f && Mathf.Abs(Mathf.Abs(v) - 0.95f) > 0.06f && Mathf.Abs(v) % 0.5f > 0.06f) continue;
                        col = new Color(0.5f, 0.52f, 0.56f);
                        alpha *= 0.5f;
                    }
                    Over(px, size, x, y, new Color(col.r, col.g, col.b, alpha));
                }
            }
            for (int side = -1; side <= 1; side += 2)
            {
                float ex = c + side * half;
                for (int y = (int)(c - rad); y <= (int)(c + rad); y++)
                {
                    for (int x = (int)(ex - rad * 0.3f); x <= (int)(ex + rad * 0.3f); x++)
                    {
                        float u = (x + 0.5f - ex) / (rad * 0.28f);
                        float v = (y + 0.5f - c) / rad;
                        float r = u * u + v * v;
                        if (r > 1f || side * u < 0f) continue;
                        Color col = new Color(0.55f, 0.57f, 0.62f) * (0.6f + 0.4f * (1f - r));
                        if (r < 0.15f) col = new Color(0.75f, 0.9f, 1f);
                        Over(px, size, x, y, new Color(col.r, col.g, col.b, Mathf.Clamp01((1f - r) * 12f) * (side < 0 || completion >= 1f ? 1f : 0.35f)));
                    }
                }
            }
            return Finish(size, px);
        }

        public static Texture2D Dish(int size, int seed)
        {
            Color[] px = new Color[size * size];
            float c = size / 2f;
            float rad = size * 0.36f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f - c) / rad;
                    float v = (y + 0.5f - c - size * 0.05f) / (rad * 0.55f);
                    float r = u * u + v * v;
                    if (r > 1f || v < -0.15f + u * u * 0.3f) continue;
                    float shade = 0.5f + 0.5f * (1f - r);
                    Color col = new Color(0.82f, 0.84f, 0.88f) * shade;
                    if (((int)(Mathf.Sqrt(r) * 10f)) % 2 == 0) col = col * 0.92f;
                    Over(px, size, x, y, new Color(col.r, col.g, col.b, Mathf.Clamp01((1f - r) * 30f)));
                }
            }
            for (int k = 0; k < size * 0.3f; k++)
            {
                Dot(px, size, c, c - size * 0.05f - k, size / 300f, new Color(0.6f, 0.62f, 0.66f, 1f));
                Dot(px, size, c - k * 0.4f, c - size * 0.05f - k, size / 400f, new Color(0.55f, 0.57f, 0.6f, 1f));
                Dot(px, size, c + k * 0.4f, c - size * 0.05f - k, size / 400f, new Color(0.55f, 0.57f, 0.6f, 1f));
            }
            Dot(px, size, c, c + rad * 0.62f, size / 70f, new Color(0.7f, 0.9f, 1f, 1f));
            return Finish(size, px);
        }

        public static Texture2D Gate(int size, bool active, bool ruin, int seed)
        {
            Color[] px = new Color[size * size];
            float c = size / 2f;
            float rad = size * 0.42f;
            float aspect = 0.86f;
            float thick = 0.09f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f - c) / rad;
                    float v = (y + 0.5f - c) / (rad * aspect);
                    float r = Mathf.Sqrt(u * u + v * v);
                    float theta = (Mathf.Atan2(v, u) / (2f * Mathf.PI) + 1f) % 1f;
                    if (r < 1f - thick && active)
                    {
                        float swirl = SpaceBackgroundArt.Fbm(Mathf.Cos(theta * 6.283f + r * 9f) * 3f + r * 4f, Mathf.Sin(theta * 6.283f + r * 9f) * 3f, 4, seed % 97, seed % 89);
                        Color inner = Color.Lerp(new Color(0.15f, 0.55f, 1f), new Color(0.75f, 0.35f, 1f), swirl);
                        inner = Color.Lerp(inner, Color.white, Mathf.Pow(Mathf.Clamp01(1f - r), 3f));
                        float a = Mathf.Clamp01(0.35f + 0.65f * swirl) * Mathf.Clamp01((1f - thick - r) * 25f);
                        Over(px, size, x, y, new Color(inner.r, inner.g, inner.b, a * 0.9f));
                        continue;
                    }
                    float d = Mathf.Abs(r - (1f - thick / 2f));
                    if (d > thick / 2f) continue;
                    int segIndex = (int)(theta * 48f);
                    if (ruin && Hash01(segIndex, seed) < 0.22f) continue;
                    float seg = (theta * 48f) % 1f;
                    float across = (r - (1f - thick / 2f)) / (thick / 2f);
                    float shade = 0.5f + 0.35f * (1f - Mathf.Abs(across)) + 0.15f * (-v);
                    Color col = new Color(0.62f, 0.64f, 0.7f) * shade;
                    if (seg < 0.08f) col = col * 0.5f;
                    if (ruin) col = Color.Lerp(col, new Color(0.35f, 0.3f, 0.28f), 0.5f);
                    bool node = segIndex % 6 == 0 && seg > 0.3f && seg < 0.7f && Mathf.Abs(across) < 0.5f;
                    if (node) col = active ? new Color(0.6f, 0.95f, 1f) : ruin ? new Color(0.35f, 0.3f, 0.3f) : new Color(0.5f, 0.6f, 0.7f);
                    float aa = Mathf.Clamp01((thick / 2f - d) * rad / 1.2f);
                    Over(px, size, x, y, new Color(col.r, col.g, col.b, aa));
                }
            }
            return Finish(size, px);
        }

        public static Texture2D Stabilizer(int size, int seed)
        {
            Color[] px = new Color[size * size];
            float c = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = new Vector2(x + 0.5f - c, y + 0.5f - c).magnitude / (size * 0.5f);
                    if (d > 1f) continue;
                    float lens = Mathf.Exp(-Mathf.Pow((d - 0.22f) / 0.05f, 2f));
                    float halo = Mathf.Exp(-Mathf.Pow((d - 0.22f) / 0.16f, 2f)) * 0.5f;
                    if (d < 0.18f) Over(px, size, x, y, new Color(0f, 0f, 0.02f, Mathf.Clamp01((0.18f - d) * 60f)));
                    float a = Mathf.Clamp01(lens + halo);
                    if (a > 0.01f) Over(px, size, x, y, new Color(0.7f + 0.3f * lens, 0.45f, 1f, a * 0.85f));
                }
            }
            System.Random r = new System.Random(seed);
            for (int s = 0; s < 3; s++)
            {
                float rad = size * (0.3f + 0.06f * s);
                float aspect = 0.2f + 0.3f * (float)r.NextDouble();
                float rot = (float)(r.NextDouble() * Math.PI);
                float cr = Mathf.Cos(rot), sr = Mathf.Sin(rot);
                for (int k = 0; k < 1400; k++)
                {
                    float a = k / 1400f * 2f * Mathf.PI;
                    float ex = Mathf.Cos(a) * rad, ey = Mathf.Sin(a) * rad * aspect;
                    bool node = k % 175 == 0;
                    Dot(px, size, c + ex * cr - ey * sr, c + ex * sr + ey * cr, size / (node ? 110f : 420f), node ? new Color(0.85f, 0.7f, 1f, 1f) : new Color(0.6f, 0.62f, 0.68f, 0.9f));
                }
            }
            return Finish(size, px);
        }

        public static Texture2D Citadel(int size, int seed)
        {
            Color[] px = new Color[size * size];
            float c = size / 2f;
            int points = 7 + seed % 3;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f - c, (y + 0.5f - c) / 0.7f);
                    float d = p.magnitude / (size * 0.46f);
                    float ang = Mathf.Atan2(p.y, p.x);
                    float star = 0.55f + 0.45f * Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * points / 2f)), 6f);
                    if (d > star) continue;
                    float layer = d / star;
                    Color col = new Color(0.4f, 0.42f, 0.47f) * (0.6f + 0.4f * (1f - layer)) * (0.75f + 0.25f * -Mathf.Sin(ang));
                    if (Mathf.Abs(layer - 0.6f) < 0.02f || Mathf.Abs(layer - 0.85f) < 0.015f) col = col * 0.6f;
                    if (Hash01(x / 3, y / 3 + seed) > 0.985f) col = Color.Lerp(col, new Color(1f, 0.35f, 0.25f), 0.8f);
                    Over(px, size, x, y, new Color(col.r, col.g, col.b, Mathf.Clamp01((star - d) * size * 0.3f)));
                }
            }
            Dot(px, size, c, c, size * 0.025f, new Color(1f, 0.7f, 0.35f, 0.9f));
            return Finish(size, px);
        }
    }
}
