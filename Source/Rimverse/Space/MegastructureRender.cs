using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public static class MegastructureRender
    {
        public static int Version;

        private static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        private static Texture2D glow;

        private static Texture2D Tex(string key, System.Func<Texture2D> make)
        {
            if (cache.TryGetValue(key, out Texture2D t) && t != null) return t;
            if (cache.Count > 40) cache.Clear();
            t = make();
            cache[key] = t;
            return t;
        }

        public static Texture2D Icon(MegastructureData m)
        {
            switch (m.kind)
            {
                case MegastructureKind.OrbitalRing:
                case MegastructureKind.RingHabitat: return Tex("i_ring", () => MegastructureArt.Gate(96, false, false, 1));
                case MegastructureKind.StellarCollector: return Tex("i_sw", () => MegastructureArt.Swarm(96, 1f, 2));
                case MegastructureKind.GiantHabitat: return Tex("i_cy", () => MegastructureArt.Cylinder(96, 1f, 3));
                case MegastructureKind.DeepObservatory: return Tex("i_dish", () => MegastructureArt.Dish(96, 4));
                case MegastructureKind.Citadel: return Tex("i_cit", () => MegastructureArt.Citadel(96, 5));
                case MegastructureKind.WormholeStabilizer: return Tex("i_stab", () => MegastructureArt.Stabilizer(96, 6));
                case MegastructureKind.PirateBase: return Tex("i_pir", () => MegastructureArt.Citadel(96, 11));
                case MegastructureKind.CivStation: return Tex("i_sta", () => MegastructureArt.Cylinder(96, 1f, 12));
                default: return Tex("i_gate", () => MegastructureArt.Gate(96, true, false, 7));
            }
        }

        private static int Bucket(float completion) => Mathf.RoundToInt(completion * 20f);

        public static void AddToBackground(MapComponent_SpaceBackground bg, StarSystemData sys, BodyData body, long now, Vector2 sunDir, float mapW)
        {
            GameComponent_Megastructures gc = GameComponent_Megastructures.Instance;
            if (gc == null || sys == null) return;
            MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(bg.map);
            if (loc != null && (loc.Warping || loc.Traveling)) return;
            if (glow == null) glow = SpaceBackgroundArt.Glow(128, 0.02f, 2.4f);
            MapComponent_MegaSection section = MapComponent_MegaSection.For(bg.map);
            int bodyIndex = body?.index ?? -1;
            Vector2 planetOff = -sunDir * mapW * 0.58f;
            float disk = body != null ? mapW * MapComponent_SpaceBackground.PlanetScale(body, 0.62f) : 0f;
            Vector2 sunOff = sunDir * mapW * 0.62f;
            GameComponent_Endgame eg = GameComponent_Endgame.Instance;
            foreach (MegastructureData m in gc.At(sys.id.Key, bodyIndex))
            {
                if (m.stage <= 0) continue;
                bool here = section != null && section.megaId == m.id;
                float comp = m.Completion;
                int b = Bucket(comp);
                float rad = m.angle * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                switch (m.kind)
                {
                    case MegastructureKind.OrbitalRing:
                    case MegastructureKind.RingHabitat:
                    {
                        bool hab = m.kind == MegastructureKind.RingHabitat;
                        Texture2D back = Tex("rb" + m.id + ":" + b, () => MegastructureArt.RingHalf(1024, 0.28f, comp, false, hab, m.seed));
                        Texture2D front = Tex("rf" + m.id + ":" + b, () => MegastructureArt.RingHalf(1024, 0.28f, comp, true, hab, m.seed));
                        if (here)
                        {
                            Vector2 off = new Vector2(0f, mapW * 0.18f);
                            bg.AddSprite(bg.NewMat(back, ShaderDatabase.Transparent, Color.white, 1003), off, mapW * 3.4f, 0f, 0.12f);
                        }
                        else if (body != null)
                        {
                            float size = disk * 2.1f;
                            float tilt = (m.angle % 40f) - 20f;
                            bg.AddSprite(bg.NewMat(back, ShaderDatabase.Transparent, Color.white, 1003), planetOff, size, tilt, 0.18f);
                            bg.AddSprite(bg.NewMat(front, ShaderDatabase.Transparent, Color.white, 1005), planetOff, size, tilt, 0.18f);
                        }
                        break;
                    }
                    case MegastructureKind.StellarCollector:
                    {
                        Texture2D t = Tex("sw" + m.id + ":" + b, () => MegastructureArt.Swarm(1024, comp, m.seed));
                        float size = here ? mapW * 2.2f : mapW * 0.75f;
                        bg.AddSprite(bg.NewMat(t, ShaderDatabase.Transparent, Color.white, 1003), here ? sunOff * 0.6f : sunOff, size, m.angle, 0.05f);
                        break;
                    }
                    case MegastructureKind.GiantHabitat:
                    {
                        Texture2D t = Tex("cy" + m.id + ":" + b, () => MegastructureArt.Cylinder(512, comp, m.seed));
                        Vector2 off = here ? new Vector2(mapW * 0.15f, mapW * 0.42f) : (body != null ? planetOff + dir * disk * 0.8f : dir * mapW * 0.35f);
                        bg.AddSprite(bg.NewMat(t, ShaderDatabase.Transparent, Color.white, Mathf.Sin(rad) > 0f ? 1003 : 1005), off, here ? mapW * 1.6f : mapW * 0.24f, m.angle % 60f - 30f, 0.14f);
                        break;
                    }
                    case MegastructureKind.DeepObservatory:
                    {
                        Texture2D t = Tex("dish" + m.seed, () => MegastructureArt.Dish(256, m.seed));
                        Vector2 off = body != null ? planetOff + dir * disk * 0.7f : dir * mapW * 0.3f;
                        if (!here) bg.AddSprite(bg.NewMat(t, ShaderDatabase.Transparent, Color.white, 1005), off, mapW * 0.12f, m.angle % 30f, 0.14f);
                        break;
                    }
                    case MegastructureKind.Citadel:
                    {
                        Texture2D t = Tex("cit" + m.seed, () => MegastructureArt.Citadel(512, m.seed));
                        Vector2 off = body != null ? planetOff + dir * disk * 0.75f : dir * mapW * 0.3f;
                        if (!here) bg.AddSprite(bg.NewMat(t, ShaderDatabase.Transparent, Color.white, Mathf.Sin(rad) > 0f ? 1003 : 1005), off, mapW * 0.16f, m.angle, 0.14f);
                        break;
                    }
                    case MegastructureKind.WormholeStabilizer:
                    {
                        Texture2D t = Tex("stab" + m.seed, () => MegastructureArt.Stabilizer(512, m.seed));
                        Vector2 off = here ? new Vector2(0f, mapW * 0.45f) : body != null ? planetOff + dir * disk * 0.9f : dir * mapW * 0.35f;
                        bg.AddSprite(bg.NewMat(glow, ShaderDatabase.MoteGlow, new Color(0.7f, 0.4f, 1f, 0.5f), 1003), off, here ? mapW * 1.2f : mapW * 0.3f, 0f, 0.12f);
                        bg.AddSprite(bg.NewMat(t, ShaderDatabase.Transparent, Color.white, 1003), off, here ? mapW * 1.4f : mapW * 0.22f, m.angle, 0.12f);
                        break;
                    }
                    case MegastructureKind.PirateBase:
                    case MegastructureKind.CivStation:
                    {
                        if (here) break;
                        bool pirate = m.kind == MegastructureKind.PirateBase;
                        Texture2D t = Tex("base" + m.seed, () => MegastructureArt.Cylinder(256, 1f, m.seed));
                        Vector2 off = body != null ? planetOff + dir * disk * 0.62f : dir * mapW * 0.3f;
                        Color tint = m.ruin ? new Color(0.55f, 0.5f, 0.48f) : pirate ? new Color(1f, 0.72f, 0.66f) : Color.white;
                        bg.AddSprite(bg.NewMat(t, ShaderDatabase.Transparent, tint, Mathf.Sin(rad) > 0f ? 1003 : 1005), off, mapW * 0.07f, m.angle, 0.14f);
                        break;
                    }
                    case MegastructureKind.Gate:
                    {
                        bool active = eg != null && eg.activatedGates.Contains(m.systemKey);
                        bool ruin = m.ruin && !active;
                        Texture2D t = Tex("gate" + m.seed + ":" + active + ruin, () => MegastructureArt.Gate(1024, active, ruin, m.seed));
                        Vector2 off = here ? new Vector2(0f, mapW * 0.5f) : dir * mapW * 0.42f;
                        float size = here ? mapW * 1.9f : mapW * 0.3f;
                        if (active) bg.AddSprite(bg.NewMat(glow, ShaderDatabase.MoteGlow, new Color(0.4f, 0.75f, 1f, 0.55f), 1003), off, size * 1.3f, 0f, here ? 0.12f : 0.08f);
                        bg.AddSprite(bg.NewMat(t, ShaderDatabase.Transparent, Color.white, 1003), off, size, m.angle % 50f - 25f, here ? 0.12f : 0.08f);
                        break;
                    }
                }
            }
        }
    }
}
