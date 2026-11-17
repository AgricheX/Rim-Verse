using System;
using System.Collections.Generic;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public class MapComponent_SpaceBackground : MapComponent
    {
        private const int QueueSky = 1000;
        private const int QueueStars = 1001;
        private const int QueueSun = 1002;
        private const int QueueFar = 1003;
        private const int QueuePlanet = 1004;
        private const int QueueNear = 1005;
        private const int QueueStreak = 1006;

        private static Texture2D glowTex;
        private static Texture2D starTex;
        private static readonly Dictionary<string, Texture2D> skyCache = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Texture2D> planetCache = new Dictionary<string, Texture2D>();
        private static readonly List<Texture2D> rockTex = new List<Texture2D>();

        private string builtKey;
        private Material skyMat;
        private readonly List<Layer> layers = new List<Layer>();
        private readonly List<Sprite> sprites = new List<Sprite>();
        private Mesh streakMesh;
        private Material streakMat;
        private Vector3 streakDir;
        private int nextCheckFrame;
        private float streakT;
        private readonly List<Material> owned = new List<Material>();
        private readonly List<Mover> movers = new List<Mover>();
        private float moverT;

        private class Mover
        {
            public Material mat;
            public Vector2 center;
            public float radius;
            public float speed;
            public float phase;
            public float size;
            public float squash;
            public float tilt;
            public float parallax;
        }

        private class Layer
        {
            public Mesh mesh;
            public Material mat;
            public float parallax;
        }

        private class Sprite
        {
            public Material mat;
            public Vector2 offset;
            public float size;
            public float angle;
            public float parallax;
        }

        public MapComponent_SpaceBackground(Map map) : base(map)
        {
        }

        private bool Active => RimverseMod.Settings.spaceBackground && SpaceMapUtility.IsRimverseSpace(map);

        public override void MapComponentUpdate()
        {
            if (!Active || Find.CurrentMap != map || WorldRendererUtility.WorldRendered) return;
            if (Time.frameCount >= nextCheckFrame)
            {
                nextCheckFrame = Time.frameCount + 120;
                string key = CurrentKey();
                if (key != builtKey)
                {
                    try
                    {
                        Build();
                    }
                    catch (Exception e)
                    {
                        RimverseLog.Warning("Space background failed: " + e.Message);
                    }
                    builtKey = key;
                }
            }
            if (skyMat == null) return;
            Draw();
        }

        public void Rebuild() => builtKey = null;

        private string CurrentKey()
        {
            MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(map);
            if (loc == null) return "none";
            long day = StarSystemData.NowTicks / RimverseUnits.TicksPerDay;
            return loc.systemKey + "|" + loc.bodyIndex + "|" + loc.Warping + "|" + loc.Traveling + "|" + day + "|" + MegastructureRender.Version + "|" + RimverseMod.Settings.PerfKey;
        }

        private static Vector3 MapCenter(Map m) => new Vector3(m.Size.x / 2f, 0f, m.Size.z / 2f);

        private void Build()
        {
            foreach (Material m in owned) UnityEngine.Object.Destroy(m);
            owned.Clear();
            layers.Clear();
            sprites.Clear();
            movers.Clear();
            streakMesh = null;
            GameComponent_Universe u = GameComponent_Universe.Instance;
            MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(map);
            if (u == null || loc == null) return;
            StarSystemData sys = loc.System;
            if (sys == null) return;
            if (glowTex == null) glowTex = SpaceBackgroundArt.Glow(128, 0.02f, 2.6f);
            if (starTex == null) starTex = SpaceBackgroundArt.Glow(32, 0.12f, 2.2f);
            GalaxyData g = u.Galaxy(sys.id.galaxy);
            Vec3d pos = u.PositionOf(sys.id);
            ulong sysSeed = sys.id.Seed(u.universeSeed);
            float mapW = Mathf.Max(map.Size.x, map.Size.z);
            string skyKey = u.universeSeed + ":" + sys.id.Key + ":" + loc.Warping;
            if (!skyCache.TryGetValue(skyKey, out Texture2D sky) || sky == null)
            {
                if (skyCache.Count > 6) skyCache.Clear();
                sky = SpaceBackgroundArt.Sky(512, SkyFor(u, g, pos, sysSeed, loc.Warping));
                skyCache[skyKey] = sky;
            }
            skyMat = NewMat(sky, ShaderDatabase.Transparent, Color.white, QueueSky);
            BuildStars(u, g, pos, sysSeed, mapW);
            long now = StarSystemData.NowTicks;
            BodyData body = loc.Body;
            Vector2 sunDir = Vector2.right;
            if (body != null)
            {
                Vector2 bp = sys.PositionAU(body, now);
                if (bp.sqrMagnitude > 1e-8f) sunDir = -bp.normalized;
            }
            float orbitAU = Mathf.Max(0.02f, body != null ? sys.OrbitOf(body) : 1f);
            for (int i = 0; i < sys.stars.Count; i++)
            {
                StarData s = sys.stars[i];
                if (s.type == StarType.BlackHole)
                {
                    AddSprite(NewMat(glowTex, ShaderDatabase.MoteGlow, new Color(1f, 0.55f, 0.25f, 0.8f), QueueSun), sunDir * mapW * 0.62f, mapW * 0.22f, 0f, 0.05f);
                    AddSprite(NewMat(glowTex, ShaderDatabase.Transparent, new Color(0f, 0f, 0f, 1f), QueueSun + 1), sunDir * mapW * 0.62f, mapW * 0.05f, 0f, 0.05f);
                    continue;
                }
                float apparent = Mathf.Sqrt(Mathf.Max(s.luminosity, 0.00001f)) / orbitAU;
                float glow = mapW * Mathf.Clamp(0.18f + 0.22f * Mathf.Log10(1f + apparent * 9f), 0.06f, 0.9f);
                Vector2 off = sunDir * mapW * 0.62f + (i == 0 ? Vector2.zero : new Vector2(-sunDir.y, sunDir.x) * mapW * 0.08f * i);
                Color sc = SpaceBackgroundArt.StarColor(s.type);
                AddSprite(NewMat(glowTex, ShaderDatabase.MoteGlow, new Color(sc.r, sc.g, sc.b, 0.85f), QueueSun), off, glow, 0f, 0.05f);
                AddSprite(NewMat(glowTex, ShaderDatabase.MoteGlow, new Color(1f, 1f, 1f, 1f), QueueSun + 1), off, glow * 0.16f, 0f, 0.05f);
            }
            if (!loc.Warping && body != null && !loc.Traveling) BuildBodies(u, sys, body, now, sunDir, mapW, sysSeed);
            if (RimverseMod.Settings.warpStreaks && (loc.Warping || loc.Traveling)) BuildStreaks(mapW, sysSeed, loc.Warping);
            if (RimverseMod.Settings.megastructureVisuals) MegastructureRender.AddToBackground(this, sys, body, now, sunDir, mapW);
            if (RimverseMod.Settings.orbitalTraffic && !loc.Warping && !loc.Traveling) OrbitalTraffic.AddTo(this, sys, body, sunDir, mapW);
        }

        private SkyParams SkyFor(GameComponent_Universe u, GalaxyData g, Vec3d pos, ulong seed, bool warping)
        {
            UniverseRand r = new UniverseRand(UniverseRand.Hash((long)seed, 300));
            SkyParams p = new SkyParams { seed = UniverseRand.Hash((long)seed, 301) };
            double rr = g.IsDisk || g.type == GalaxyType.Irregular ? pos.PlanarLength / g.radiusLy : pos.Length / g.radiusLy;
            float local = (float)Math.Min(3.0, g.DensityAt(pos) / Math.Max(1e-9, g.baseDensity));
            p.bandAngle = r.Range(0f, Mathf.PI);
            float age = CosmicScale.AgeFactor(u.universeSeed);
            if (g.IsDisk)
            {
                p.bandStrength = 0.08f + 0.22f * Mathf.Exp(-1.4f * (float)rr) + 0.04f * local;
                p.bandWidth = 0.05f + 0.06f * Mathf.Clamp01((float)(Math.Abs(pos.y) / g.thicknessLy));
                double phi = Math.Atan2(-pos.z, -pos.x);
                p.coreOffset = 0.32f * Mathf.Cos((float)phi - p.bandAngle);
                p.coreStrength = 0.45f * Mathf.Exp(-1.8f * (float)rr) * (g.hasBar ? 1.4f : 1f);
                p.coreSize = 0.06f + 0.08f * Mathf.Clamp01(1f - (float)rr);
                p.bandWarmth = Mathf.Clamp01(0.3f + 0.4f * age + 0.3f * Mathf.Exp(-2f * (float)rr));
            }
            else if (g.type == GalaxyType.Irregular)
            {
                p.bandStrength = 0.06f + 0.05f * local;
                p.bandWidth = 0.14f;
                p.bandWarmth = 0.2f;
                p.haze = 0.02f;
            }
            else
            {
                p.haze = (g.type == GalaxyType.Elliptical ? 0.08f : 0.025f) * Mathf.Exp(-2.5f * (float)rr) + 0.01f;
                p.bandWarmth = 0.6f + 0.4f * age;
            }
            float nebulaChance = g.type == GalaxyType.Irregular ? 0.6f : g.IsDisk ? (g.InBar(pos) || (g.hasRing && Math.Abs(pos.PlanarLength / g.radiusLy - g.ringRadius) < 0.1) ? 0.55f : 0.3f) : 0.08f;
            if (r.Chance(nebulaChance))
            {
                p.nebula = r.Range(0.12f, 0.3f);
                switch (r.RangeInclusive(0, 2))
                {
                    case 0: p.nebulaA = new Color(0.85f, 0.25f, 0.35f); p.nebulaB = new Color(0.4f, 0.18f, 0.55f); break;
                    case 1: p.nebulaA = new Color(0.25f, 0.45f, 0.95f); p.nebulaB = new Color(0.55f, 0.65f, 1f); break;
                    default: p.nebulaA = new Color(0.2f, 0.75f, 0.65f); p.nebulaB = new Color(0.3f, 0.45f, 0.9f); break;
                }
            }
            int count = u.GalaxyCount;
            for (int i = 0; i < count; i++)
            {
                if (i == g.index) continue;
                GalaxyData o = u.Galaxy(i);
                Vector2 d = o.groupPositionMly - g.groupPositionMly;
                float dist = Mathf.Max(0.02f, Mathf.Sqrt(d.sqrMagnitude + (float)((o.groupHeightMly - g.groupHeightMly) * (o.groupHeightMly - g.groupHeightMly))));
                float ang = Mathf.Atan2(d.y, d.x);
                float ang2 = (float)(o.groupHeightMly - g.groupHeightMly) / dist;
                Vector2 sp = new Vector2(0.5f + 0.42f * Mathf.Cos(ang) * (1f - Mathf.Abs(ang2) * 0.5f), 0.5f + 0.42f * Mathf.Sin(ang) * (1f - Mathf.Abs(ang2) * 0.5f));
                float size = Mathf.Clamp((float)(o.radiusLy / 1000000.0) / dist * 4f, 0.006f, 0.08f);
                UniverseRand gr = new UniverseRand(UniverseRand.Hash(u.universeSeed, 302, i));
                float a2 = gr.Range(0f, Mathf.PI);
                p.smudges.Add(new SkySmudge
                {
                    pos = sp,
                    axis = new Vector2(Mathf.Cos(a2), Mathf.Sin(a2)),
                    size = size,
                    flat = o.IsDisk ? gr.Range(0.2f, 0.6f) : gr.Range(0.6f, 1f),
                    bright = Mathf.Clamp(0.06f + 0.1f / dist, 0.05f, 0.35f),
                    color = o.type == GalaxyType.Elliptical ? new Color(1f, 0.85f, 0.65f) : new Color(0.75f, 0.82f, 1f)
                });
            }
            if (warping)
            {
                p.haze += 0.03f;
                p.nebula = Mathf.Max(p.nebula, 0.12f);
                p.nebulaA = new Color(0.2f, 0.35f, 0.9f);
                p.nebulaB = new Color(0.6f, 0.3f, 0.9f);
            }
            return p;
        }

        private void BuildStars(GameComponent_Universe u, GalaxyData g, Vec3d pos, ulong seed, float mapW)
        {
            float local = (float)Math.Min(3.0, g.DensityAt(pos) / Math.Max(1e-9, g.baseDensity));
            int n = Mathf.RoundToInt(Mathf.Lerp(700f, 3200f, Mathf.Clamp01(local / 1.5f)) * Mathf.Clamp(u.DensityFactor, 0.3f, 1f) * Mathf.Clamp(RimverseMod.Settings.backgroundStarFactor, 0.1f, 1f));
            UniverseRand r = new UniverseRand(UniverseRand.Hash((long)seed, 310));
            float span = mapW * 2.4f;
            float bandAngle = new UniverseRand(UniverseRand.Hash((long)seed, 300)).Range(0f, Mathf.PI);
            Vector2 bandDir = new Vector2(Mathf.Cos(bandAngle), Mathf.Sin(bandAngle));
            Vector2 bandN = new Vector2(-bandDir.y, bandDir.x);
            float age = CosmicScale.AgeFactor(u.universeSeed);
            Color[] cols = { new Color(0.72f, 0.8f, 1f), new Color(1f, 1f, 1f), new Color(1f, 0.86f, 0.66f), new Color(1f, 0.62f, 0.45f) };
            float[] w = { 1.2f - 0.6f * age, 3f, 2f + age, 1.2f + age };
            List<Vector3>[] verts = new List<Vector3>[4];
            List<Vector2>[] uvs = new List<Vector2>[4];
            for (int k = 0; k < 4; k++) { verts[k] = new List<Vector3>(); uvs[k] = new List<Vector2>(); }
            for (int i = 0; i < n; i++)
            {
                Vector2 p;
                if (g.IsDisk && r.Chance(0.45f))
                {
                    float along = (r.Value - 0.5f) * span;
                    float across = (r.Value + r.Value + r.Value - 1.5f) * span * 0.06f;
                    p = bandDir * along + bandN * across;
                }
                else p = new Vector2((r.Value - 0.5f) * span, (r.Value - 0.5f) * span);
                float m = Mathf.Pow(r.Value, 5f);
                float size = 0.12f + m * 0.9f;
                int c = r.Weighted(new[] { 0, 1, 2, 3 }, w);
                Quad(verts[c], uvs[c], p, size);
            }
            for (int k = 0; k < 4; k++)
            {
                if (verts[k].Count == 0) continue;
                layers.Add(new Layer { mesh = BuildMesh(verts[k], uvs[k]), mat = NewMat(starTex, ShaderDatabase.Transparent, cols[k], QueueStars), parallax = 0.04f });
            }
        }

        private void BuildBodies(GameComponent_Universe u, StarSystemData sys, BodyData body, long now, Vector2 sunDir, float mapW, ulong sysSeed)
        {
            float sunDeg = Mathf.Atan2(sunDir.y, sunDir.x) * Mathf.Rad2Deg;
            if (body.type == BodyType.Belt)
            {
                BuildBelt(mapW, UniverseRand.Hash((long)sysSeed, 320, body.index));
                return;
            }
            AddPlanet(sys, body, -sunDir * mapW * 0.58f, mapW * PlanetScale(body, 0.62f), sunDeg, 0.18f, QueuePlanet, sysSeed);
            if (body.IsMoon)
            {
                BodyData parent = sys.Body(body.parent);
                Vector2 rel = sys.PositionAU(parent, now) - sys.PositionAU(body, now);
                Vector2 dir = rel.sqrMagnitude > 1e-12f ? rel.normalized : new Vector2(-sunDir.y, sunDir.x);
                float distKm = Mathf.Max(1f, body.moonOrbitKm);
                float ang = Mathf.Clamp(parent.RadiusKm / distKm, 0.02f, 0.5f);
                AddPlanet(sys, parent, dir * mapW * 0.5f, mapW * Mathf.Clamp(ang * 2.4f, 0.12f, 0.9f), sunDeg, 0.08f, QueueFar, sysSeed);
            }
            foreach (BodyData o in sys.bodies)
            {
                if (o == body) continue;
                if (o.IsMoon && o.parent == body.index)
                {
                    Vector2 rel = sys.PositionAU(o, now) - sys.PositionAU(body, now);
                    Vector2 dir = rel.sqrMagnitude > 1e-12f ? rel.normalized : Vector2.up;
                    float ang = Mathf.Clamp(o.RadiusKm / Mathf.Max(1f, o.moonOrbitKm), 0.004f, 0.2f);
                    AddPlanet(sys, o, dir * mapW * 0.46f, mapW * Mathf.Clamp(ang * 3f, 0.03f, 0.35f), sunDeg, 0.1f, QueueFar, sysSeed);
                    continue;
                }
                if (o.IsMoon || o.type == BodyType.Belt || o.index == body.parent) continue;
                Vector2 d = sys.PositionAU(o, now) - sys.PositionAU(body, now);
                float au = d.magnitude;
                if (au <= 0f) continue;
                float brightness = Mathf.Clamp01(o.radiusEarth / (au * 4f));
                if (brightness < 0.02f) continue;
                Color c = o.type == BodyType.GasGiant ? new Color(1f, 0.9f, 0.75f) : o.type == BodyType.Ocean ? new Color(0.7f, 0.8f, 1f) : new Color(0.95f, 0.9f, 0.85f);
                c.a = Mathf.Clamp(0.4f + brightness, 0.4f, 1f);
                AddSprite(NewMat(starTex, ShaderDatabase.MoteGlow, c, QueueFar), d.normalized * mapW * 0.44f, 0.6f + brightness * 2.5f, 0f, 0.06f);
            }
        }

        public static float PlanetScale(BodyData b, float baseScale)
        {
            float r = Mathf.Max(0.05f, b.radiusEarth);
            return Mathf.Clamp(baseScale * (0.75f + 0.28f * Mathf.Log(r * 3f + 1f, 2f)), 0.35f, 1.6f);
        }

        private void AddPlanet(StarSystemData sys, BodyData b, Vector2 offset, float diskDiameter, float sunDeg, float parallax, int queue, ulong sysSeed)
        {
            GameComponent_Universe u = GameComponent_Universe.Instance;
            string key = u.universeSeed + ":" + sys.id.Key + ":" + b.index;
            if (!planetCache.TryGetValue(key, out Texture2D tex) || tex == null)
            {
                if (planetCache.Count > 10) planetCache.Clear();
                tex = SpaceBackgroundArt.Planet(b.type == BodyType.GasGiant || b.homeWorld ? 1024 : 768, b, UniverseRand.Hash((long)sysSeed, 330, b.index), sys.frostLineAU, false);
                planetCache[key] = tex;
            }
            float frac = b.rings ? 0.30f : 0.48f;
            float quad = diskDiameter / (2f * frac);
            AddSprite(NewMat(tex, ShaderDatabase.Transparent, Color.white, queue), offset, quad, sunDeg - 12f, parallax);
        }

        private void BuildBelt(float mapW, ulong seed)
        {
            UniverseRand r = new UniverseRand(seed);
            if (rockTex.Count == 0)
            {
                for (int i = 0; i < 6; i++) rockTex.Add(SpaceBackgroundArt.Rock(128, UniverseRand.Hash(4242, i)));
            }
            for (int i = 0; i < 70; i++)
            {
                Vector2 p = new Vector2((r.Value - 0.5f) * mapW * 2.2f, (r.Value - 0.5f) * mapW * 2.2f);
                float near = r.Value;
                float size = Mathf.Lerp(0.6f, 9f, near * near);
                Color tint = Color.Lerp(new Color(0.35f, 0.35f, 0.38f, 1f), Color.white, near);
                AddSprite(NewMat(rockTex[r.RangeInclusive(0, rockTex.Count - 1)], ShaderDatabase.Transparent, tint, near > 0.6f ? QueueNear : QueueFar), p, size, r.Range(0f, 360f), Mathf.Lerp(0.05f, 0.35f, near));
            }
        }

        private void BuildStreaks(float mapW, ulong seed, bool warp)
        {
            UniverseRand r = new UniverseRand(UniverseRand.Hash((long)seed, 340));
            float a = r.Range(0f, Mathf.PI * 2f);
            streakDir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            List<Vector3> verts = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            int n = warp ? 380 : 120;
            float span = mapW * 2.4f;
            Vector2 dir = new Vector2(streakDir.x, streakDir.z);
            Vector2 nrm = new Vector2(-dir.y, dir.x);
            for (int i = 0; i < n; i++)
            {
                Vector2 c = new Vector2((r.Value - 0.5f) * span, (r.Value - 0.5f) * span);
                float len = warp ? r.Range(6f, 22f) : r.Range(1.5f, 4f);
                float wid = warp ? r.Range(0.08f, 0.22f) : 0.08f;
                Vector2 h = dir * len / 2f, v = nrm * wid / 2f;
                int b0 = verts.Count;
                verts.Add(V(c - h - v)); verts.Add(V(c - h + v)); verts.Add(V(c + h + v)); verts.Add(V(c + h - v));
                uvs.Add(new Vector2(0f, 0.5f)); uvs.Add(new Vector2(0f, 0.5f)); uvs.Add(new Vector2(0.5f, 0.5f)); uvs.Add(new Vector2(0.5f, 0.5f));
            }
            streakMesh = BuildMesh(verts, uvs);
            streakMat = NewMat(starTex, ShaderDatabase.MoteGlow, warp ? new Color(0.65f, 0.8f, 1f, 0.9f) : new Color(1f, 1f, 1f, 0.5f), QueueStreak);
        }

        private static Vector3 V(Vector2 p) => new Vector3(p.x, 0f, p.y);

        private static void Quad(List<Vector3> verts, List<Vector2> uvs, Vector2 c, float size)
        {
            float h = size / 2f;
            verts.Add(new Vector3(c.x - h, 0f, c.y - h));
            verts.Add(new Vector3(c.x - h, 0f, c.y + h));
            verts.Add(new Vector3(c.x + h, 0f, c.y + h));
            verts.Add(new Vector3(c.x + h, 0f, c.y - h));
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(0f, 1f));
            uvs.Add(new Vector2(1f, 1f));
            uvs.Add(new Vector2(1f, 0f));
        }

        private static Mesh BuildMesh(List<Vector3> verts, List<Vector2> uvs)
        {
            Mesh m = new Mesh();
            m.indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            int[] tris = new int[verts.Count / 4 * 6];
            for (int q = 0, t = 0; q < verts.Count; q += 4, t += 6)
            {
                tris[t] = q; tris[t + 1] = q + 1; tris[t + 2] = q + 2;
                tris[t + 3] = q; tris[t + 4] = q + 2; tris[t + 5] = q + 3;
            }
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            m.bounds = new Bounds(Vector3.zero, new Vector3(100000f, 10f, 100000f));
            return m;
        }

        public Material NewMat(Texture2D tex, Shader shader, Color color, int queue)
        {
            Material m = new Material(shader);
            owned.Add(m);
            m.mainTexture = tex;
            m.color = color;
            m.renderQueue = queue;
            return m;
        }

        public void AddMover(Material mat, Vector2 center, float radius, float speed, float phase, float size, float squash, float tilt, float parallax)
        {
            movers.Add(new Mover { mat = mat, center = center, radius = radius, speed = speed, phase = phase, size = size, squash = squash, tilt = tilt, parallax = parallax });
        }

        public void AddSprite(Material mat, Vector2 offset, float size, float angle, float parallax)
        {
            sprites.Add(new Sprite { mat = mat, offset = offset, size = size, angle = angle, parallax = parallax });
        }

        private void Draw()
        {
            Vector3 center = MapCenter(map);
            Vector3 cam = Find.Camera != null ? Find.Camera.transform.position : center;
            Vector3 camOff = new Vector3(cam.x - center.x, 0f, cam.z - center.z);
            float y = -3f;
            float skySize = Mathf.Max(map.Size.x, map.Size.z) * 2.6f;
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(center + camOff * 0.85f + new Vector3(0f, y, 0f), Quaternion.identity, new Vector3(skySize, 1f, skySize)), skyMat, 0);
            foreach (Layer l in layers)
            {
                Graphics.DrawMesh(l.mesh, Matrix4x4.TRS(center + camOff * (1f - l.parallax) + new Vector3(0f, y + 0.1f, 0f), Quaternion.identity, Vector3.one), l.mat, 0);
            }
            float yy = y + 0.2f;
            foreach (Sprite s in sprites)
            {
                Vector3 p = center + camOff * (1f - s.parallax) + new Vector3(s.offset.x, yy, s.offset.y);
                Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(p, Quaternion.AngleAxis(-s.angle, Vector3.up), new Vector3(s.size, 1f, s.size)), s.mat, 0);
                yy += 0.001f;
            }
            if (movers.Count > 0)
            {
                if (!Find.TickManager.Paused) moverT += Time.deltaTime * Mathf.Min(Find.TickManager.TickRateMultiplier, 3f);
                foreach (Mover m in movers)
                {
                    float a = m.phase + moverT * m.speed;
                    Vector2 local = new Vector2(Mathf.Cos(a) * m.radius, Mathf.Sin(a) * m.radius * m.squash);
                    float tr = m.tilt * Mathf.Deg2Rad;
                    Vector2 rot = new Vector2(local.x * Mathf.Cos(tr) - local.y * Mathf.Sin(tr), local.x * Mathf.Sin(tr) + local.y * Mathf.Cos(tr));
                    Vector3 p = center + camOff * (1f - m.parallax) + new Vector3(m.center.x + rot.x, yy + (Mathf.Sin(a) < 0f ? 0.05f : -0.05f), m.center.y + rot.y);
                    Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(p, Quaternion.identity, new Vector3(m.size, 1f, m.size)), m.mat, 0);
                }
            }
            if (streakMesh != null)
            {
                float span = Mathf.Max(map.Size.x, map.Size.z) * 2.4f;
                if (!Find.TickManager.Paused) streakT = (streakT + Time.deltaTime * 40f * Mathf.Min(Find.TickManager.TickRateMultiplier, 3f)) % span;
                float t = streakT;
                Vector3 baseP = center + camOff * 0.9f + new Vector3(0f, y + 0.5f, 0f);
                Graphics.DrawMesh(streakMesh, Matrix4x4.TRS(baseP + streakDir * (t - span / 2f), Quaternion.identity, Vector3.one), streakMat, 0);
                Graphics.DrawMesh(streakMesh, Matrix4x4.TRS(baseP + streakDir * (t + span / 2f), Quaternion.identity, Vector3.one), streakMat, 0);
            }
        }
    }
}
