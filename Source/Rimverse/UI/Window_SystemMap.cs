using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    [StaticConstructorOnStartup]
    public class Window_SystemMap : Window
    {
        private static readonly Color OrbitColor = new Color(1f, 1f, 1f, 0.15f);
        private static readonly Color SelectColor = new Color(1f, 0.85f, 0.3f);

        private readonly Map shipMap;
        private int selected = -1;
        private Vector2 infoScroll;
        private float zoom = 1f;
        private Vector2 pan;
        private int hovered = -1;
        private int iconBudget;
        private readonly Dictionary<string, Texture2D> icons = new Dictionary<string, Texture2D>();
        private Texture2D zoneTex;
        private string zoneKey;
        private static Texture2D glowTex;
        private static Texture2D bgTex;
        private static Texture2D ringTex;

        public Window_SystemMap(Map shipMap)
        {
            this.shipMap = shipMap;
            doCloseX = true;
            doCloseButton = false;
            forcePause = false;
            absorbInputAroundWindow = false;
            closeOnClickedOutside = false;
            draggable = true;
            preventCameraMotion = false;
            MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(shipMap);
            selected = loc?.bodyIndex ?? -1;
        }

        public override Vector2 InitialSize => new Vector2(980f, 680f);

        private MapComponent_ShipLocation Loc => MapComponent_ShipLocation.For(shipMap);

        private RimverseShip PlayerShip
        {
            get
            {
                MapComponent_RimverseShips comp = shipMap.GetComponent<MapComponent_RimverseShips>();
                if (comp == null) return null;
                foreach (RimverseShip s in comp.Ships)
                {
                    if (s.bridge != null && s.bridge.Faction == Faction.OfPlayer) return s;
                }
                return null;
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            MapComponent_ShipLocation loc = Loc;
            StarSystemData sys = loc?.System;
            if (sys == null)
            {
                Widgets.Label(inRect, "Rimverse_NoSystem".Translate());
                return;
            }
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 34f), "Rimverse_SystemMapTitle".Translate(sys.name, StarLabel(sys)));
            Text.Font = GameFont.Small;
            Rect body = new Rect(inRect.x, inRect.y + 38f, inRect.width, inRect.height - 38f);
            Rect mapRect = body.LeftPart(0.64f).ContractedBy(4f);
            Rect infoRect = body.RightPart(0.36f).ContractedBy(4f);
            Widgets.DrawBoxSolid(mapRect, new Color(0.01f, 0.012f, 0.03f));
            DrawSystem(mapRect, sys, loc);
            DrawInfo(infoRect, sys, loc);
        }

        private static string StarLabel(StarSystemData sys)
        {
            List<string> parts = new List<string>();
            foreach (StarData s in sys.stars) parts.Add(("Rimverse_Star_" + s.type).Translate());
            return string.Join(" + ", parts);
        }

        private float MaxOrbit(StarSystemData sys)
        {
            float max = 1f;
            foreach (BodyData b in sys.bodies) if (!b.IsMoon && b.orbitAU > max) max = b.orbitAU;
            return max;
        }

        private float RadiusFor(float au, float maxAu, float maxRadius)
        {
            return maxRadius * zoom * Mathf.Log10(1f + au * 10f) / Mathf.Log10(1f + maxAu * 10f);
        }

        private Vector2 MapPos(Vector2 au, Vector2 center, float maxAu, float maxR)
        {
            float d = au.magnitude;
            if (d < 1e-5f) return center;
            Vector2 dir = au / d;
            return center + new Vector2(dir.x, -dir.y) * RadiusFor(d, maxAu, maxR);
        }

        private Texture2D IconFor(StarSystemData sys, BodyData b)
        {
            string key = sys.id.Key + ":" + b.index;
            if (icons.TryGetValue(key, out Texture2D t) && t != null) return t;
            if (iconBudget <= 0) return null;
            iconBudget--;
            GameComponent_Universe u = GameComponent_Universe.Instance;
            ulong seed = sys.id.Seed(u.universeSeed);
            t = SpaceBackgroundArt.Planet(b.type == BodyType.GasGiant ? 128 : 96, b, UniverseRand.Hash((long)seed, 330, b.index), sys.frostLineAU, false);
            icons[key] = t;
            return t;
        }

        private Texture2D ZoneTex(float inner, float outer)
        {
            string key = inner.ToString("0.000") + ":" + outer.ToString("0.000");
            if (zoneTex != null && zoneKey == key) return zoneTex;
            zoneKey = key;
            int size = 256;
            Texture2D t = SpaceBackgroundArt.NewTex(size);
            Color[] px = new Color[size * size];
            float half = size / 2f;
            float fi = Mathf.Clamp01(inner / Mathf.Max(outer, 0.0001f));
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = new Vector2(x + 0.5f - half, y + 0.5f - half).magnitude / half;
                    float a = 0f;
                    if (d >= fi && d <= 1f)
                    {
                        float k = (d - fi) / Mathf.Max(1f - fi, 0.001f);
                        a = Mathf.Clamp01(Mathf.Min(k, 1f - k) * 8f) * 0.85f + 0.15f;
                        if (k < 0.02f || k > 0.98f) a = 1f;
                    }
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            t.SetPixels(px);
            t.Apply(true, true);
            zoneTex = t;
            return t;
        }

        private void HandleInput(Rect rect)
        {
            Event e = Event.current;
            if (!Mouse.IsOver(rect)) return;
            if (e.type == EventType.ScrollWheel)
            {
                float f = e.delta.y < 0f ? 1.2f : 1f / 1.2f;
                float nz = Mathf.Clamp(zoom * f, 0.6f, 40f);
                f = nz / zoom;
                Vector2 mouse = e.mousePosition - rect.position - rect.size / 2f;
                pan = mouse - (mouse - pan) * f;
                zoom = nz;
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && (e.button == 1 || e.button == 2))
            {
                pan += e.delta;
                e.Use();
            }
        }

        private void DrawSystem(Rect rect, StarSystemData sys, MapComponent_ShipLocation loc)
        {
            HandleInput(rect);
            iconBudget = 3;
            GUI.BeginGroup(rect);
            Rect local = new Rect(0f, 0f, rect.width, rect.height);
            if (bgTex == null) bgTex = SpaceBackgroundArt.Glow(128, 0.0f, 1.4f);
            GUI.color = new Color(0.10f, 0.12f, 0.22f, 0.6f);
            GUI.DrawTexture(local.ExpandedBy(rect.width * 0.3f), bgTex);
            Vector2 center = local.center + pan;
            float maxR = Mathf.Min(rect.width, rect.height) * 0.47f;
            float maxAu = MaxOrbit(sys);
            long now = StarSystemData.NowTicks;
            float lum = Mathf.Max(sys.TotalLuminosity, 0.0001f);
            float hzIn = Mathf.Sqrt(lum / 1.1f), hzOut = Mathf.Sqrt(lum / 0.53f);
            float rOut = RadiusFor(hzOut, maxAu, maxR), rIn = RadiusFor(hzIn, maxAu, maxR);
            GUI.color = new Color(0.25f, 0.85f, 0.4f, 0.10f);
            GUI.DrawTexture(new Rect(center.x - rOut, center.y - rOut, rOut * 2f, rOut * 2f), ZoneTex(rIn, rOut));
            if (sys.frostLineAU > 0f)
            {
                GUI.color = new Color(0.55f, 0.8f, 1f, 0.35f);
                DrawDashed(center, RadiusFor(sys.frostLineAU, maxAu, maxR));
            }
            if (glowTex == null) glowTex = SpaceBackgroundArt.Glow(96, 0.06f, 2.2f);
            for (int i = 0; i < sys.stars.Count; i++)
            {
                StarData s = sys.stars[i];
                float size = Mathf.Clamp(26f + 10f * Mathf.Log10(Mathf.Max(s.luminosity, 0.0001f) + 1f) * 3f, 18f, 70f) * Mathf.Sqrt(zoom);
                Vector2 c = center + (i == 0 ? Vector2.zero : new Vector2(Mathf.Cos(i * 2.1f), Mathf.Sin(i * 2.1f)) * (8f + 6f * i) * Mathf.Sqrt(zoom));
                GUI.color = SpaceBackgroundArt.StarColor(s.type);
                GUI.DrawTexture(new Rect(c.x - size, c.y - size, size * 2f, size * 2f), glowTex);
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(c.x - size * 0.18f, c.y - size * 0.18f, size * 0.36f, size * 0.36f), glowTex);
            }
            Dictionary<int, Vector2> positions = new Dictionary<int, Vector2>();
            foreach (BodyData b in sys.bodies)
            {
                if (b.IsMoon) continue;
                float r = RadiusFor(b.orbitAU, maxAu, maxR);
                if (b.type == BodyType.Belt)
                {
                    DrawBelt(center, r, b.index);
                    positions[b.index] = center + new Vector2(r, 0f);
                    continue;
                }
                GUI.color = b.index == selected ? new Color(1f, 0.85f, 0.3f, 0.35f) : OrbitColor;
                DrawCircle(center, r, 1f);
                positions[b.index] = MapPos(sys.PositionAU(b, now), center, maxAu, maxR);
            }
            Dictionary<int, int> moonCount = new Dictionary<int, int>();
            foreach (BodyData b in sys.bodies)
            {
                if (!b.IsMoon || !positions.ContainsKey(b.parent)) continue;
                moonCount.TryGetValue(b.parent, out int k);
                moonCount[b.parent] = k + 1;
                float dist = (12f + 7f * k) * Mathf.Sqrt(zoom);
                float a = b.AngleAt(now);
                Vector2 parent = positions[b.parent];
                if (zoom > 2.5f)
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.08f);
                    DrawCircle(parent, dist, 1f);
                }
                positions[b.index] = parent + new Vector2(Mathf.Cos(a), -Mathf.Sin(a)) * dist;
            }
            hovered = -1;
            Text.Font = GameFont.Tiny;
            foreach (BodyData b in sys.bodies)
            {
                if (b.type == BodyType.Belt || !positions.TryGetValue(b.index, out Vector2 pos)) continue;
                DrawBody(sys, b, pos, loc);
            }
            foreach (BodyData b in sys.bodies)
            {
                if (b.type != BodyType.Belt || !positions.TryGetValue(b.index, out Vector2 pos)) continue;
                Rect click = new Rect(pos.x - 8f, pos.y - 8f, 16f, 16f);
                GUI.color = b.index == selected ? SelectColor : new Color(0.75f, 0.7f, 0.6f);
                GUI.DrawTexture(click.ContractedBy(5f), BaseContent.WhiteTex);
                if (Mouse.IsOver(click)) TooltipHandler.TipRegion(click, BodyName(sys, b));
                if (Widgets.ButtonInvisible(click)) selected = b.index;
            }
            DrawMegastructures(sys, center, positions);
            DrawFleetsAndAnomalies(sys, center, positions);
            DrawShip(sys, loc, positions);
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(1f, 1f, 1f, 0.55f);
            Widgets.Label(new Rect(6f, local.height - 34f, local.width - 12f, 16f), "Rimverse_SysLegend".Translate());
            Widgets.Label(new Rect(6f, local.height - 18f, local.width - 12f, 16f), "Rimverse_SysZoomHelp".Translate());
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            GUI.EndGroup();
        }

        private void DrawBody(StarSystemData sys, BodyData b, Vector2 pos, MapComponent_ShipLocation loc)
        {
            ScanLevel scan = GameComponent_Universe.Instance.GetScan(sys.id, b.index);
            float diam = Mathf.Clamp(7f + 6f * Mathf.Log(Mathf.Max(b.radiusEarth, 0.02f) * 2f + 1f, 2f), 6f, 34f);
            if (b.IsMoon) diam *= 0.75f;
            diam *= Mathf.Clamp(Mathf.Sqrt(zoom), 1f, 2.6f);
            Texture2D tex = IconFor(sys, b);
            float frac = b.rings ? 0.30f : 0.48f;
            float quad = diam / (2f * frac);
            Rect dot = new Rect(pos.x - diam / 2f, pos.y - diam / 2f, diam, diam);
            Civilization civ = SettlementGen.CivOn(sys.id.Key, b.index, out CivPlanet cp);
            PlayerColony colony = SettlementGen.ColonyOn(sys.id.Key, b.index);
            if (civ != null || colony != null)
            {
                if (ringTex == null) ringTex = RingTex();
                Color own = colony != null ? new Color(0.35f, 1f, 0.5f, 0.9f) : civ.known ? new Color(civ.color.r, civ.color.g, civ.color.b, 0.9f) : new Color(0.7f, 0.7f, 0.7f, 0.6f);
                GUI.color = own;
                GUI.DrawTexture(dot.ExpandedBy(5f), ringTex);
                float bs = 13f * Mathf.Clamp(Mathf.Sqrt(zoom), 1f, 1.8f);
                MapIcons.Draw(new Rect(dot.xMax - bs * 0.2f, dot.y - bs * 0.8f, bs, bs), colony != null ? MapIcons.Colony : MapIcons.CivWorld, own);
            }
            if (tex != null)
            {
                GUI.color = scan == ScanLevel.Unknown ? new Color(0.35f, 0.35f, 0.4f) : Color.white;
                GUI.DrawTexture(new Rect(pos.x - quad / 2f, pos.y - quad / 2f, quad, quad), tex);
            }
            else
            {
                GUI.color = BodyColor(b);
                GUI.DrawTexture(dot.ContractedBy(diam * 0.2f), BaseContent.WhiteTex);
            }
            if (b.index == selected)
            {
                GUI.color = SelectColor;
                Widgets.DrawBox(dot.ExpandedBy(8f));
            }
            if (b.index == loc.destinationBody && loc.Traveling)
            {
                GUI.color = Color.cyan;
                Widgets.DrawBox(dot.ExpandedBy(10f));
            }
            bool showName = !b.IsMoon || zoom > 2.5f;
            if (showName && scan != ScanLevel.Unknown)
            {
                string name = b.name;
                if (colony != null) name += " (" + colony.name + ")";
                else if (civ != null && civ.known) name += " (" + civ.ShownName + ")";
                GUI.color = b.homeWorld || colony != null ? new Color(0.6f, 1f, 0.7f, 0.85f) : new Color(1f, 1f, 1f, 0.7f);
                Vector2 sz = Text.CalcSize(name);
                Widgets.Label(new Rect(pos.x - sz.x / 2f, dot.yMax + 3f, sz.x + 2f, sz.y), name);
            }
            GUI.color = Color.white;
            Rect click = dot.ExpandedBy(6f);
            if (Mouse.IsOver(click))
            {
                hovered = b.index;
                string tip = BodyName(sys, b);
                if (colony != null) tip += "\n" + "Rimverse_SysYourColony".Translate(colony.name);
                else if (civ != null) tip += "\n" + "Rimverse_SysOwnedBy".Translate(civ.ShownName);
                TooltipHandler.TipRegion(click, tip);
            }
            if (Widgets.ButtonInvisible(click)) selected = b.index;
        }

        private void DrawBelt(Vector2 center, float r, int index)
        {
            System.Random rand = new System.Random(index * 7919 + 13);
            int count = Mathf.Clamp((int)(r * 1.6f), 60, 900);
            Color c = new Color(0.75f, 0.68f, 0.58f, 0.55f);
            float spread = Mathf.Max(3f, r * 0.035f);
            for (int i = 0; i < count; i++)
            {
                float a = (float)rand.NextDouble() * Mathf.PI * 2f;
                float d = r + ((float)rand.NextDouble() - 0.5f) * spread * 2f;
                float s = 1f + (float)rand.NextDouble() * 1.5f;
                c.a = 0.25f + (float)rand.NextDouble() * 0.45f;
                GUI.color = c;
                GUI.DrawTexture(new Rect(center.x + Mathf.Cos(a) * d - s / 2f, center.y + Mathf.Sin(a) * d - s / 2f, s, s), BaseContent.WhiteTex);
            }
        }

        private static void DrawDashed(Vector2 c, float r)
        {
            int seg = Mathf.Clamp((int)(r / 3f), 32, 240);
            for (int i = 0; i < seg; i += 2)
            {
                float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                Widgets.DrawLine(c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r, c + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r, GUI.color, 1f);
            }
        }

        private static Texture2D RingTex()
        {
            int size = 64;
            Texture2D t = SpaceBackgroundArt.NewTex(size);
            Color[] px = new Color[size * size];
            float half = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = new Vector2(x + 0.5f - half, y + 0.5f - half).magnitude / half;
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.86f) / 0.1f);
                    px[y * size + x] = new Color(1f, 1f, 1f, a * a);
                }
            }
            t.SetPixels(px);
            t.Apply(true, true);
            return t;
        }

        private void DrawShip(StarSystemData sys, MapComponent_ShipLocation loc, Dictionary<int, Vector2> positions)
        {
            if (loc.Warping || !positions.TryGetValue(loc.bodyIndex, out Vector2 from)) return;
            Vector2 at = from + new Vector2(0f, -1f) * 4f;
            Vector2 dir = new Vector2(1f, 0f);
            if (loc.Traveling && positions.TryGetValue(loc.destinationBody, out Vector2 to))
            {
                GUI.color = new Color(0.4f, 0.9f, 1f, 0.5f);
                Vector2 seg = to - from;
                int dashes = Mathf.Max(2, (int)(seg.magnitude / 8f));
                for (int i = 0; i < dashes; i += 2) Widgets.DrawLine(from + seg * (i / (float)dashes), from + seg * ((i + 1) / (float)dashes), GUI.color, 1f);
                at = Vector2.Lerp(from, to, loc.Progress);
                if (seg.sqrMagnitude > 0.01f) dir = seg.normalized;
            }
            else
            {
                float a = Find.TickManager.TicksGame * 0.002f;
                at = from + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 18f * Mathf.Clamp(Mathf.Sqrt(zoom), 1f, 2.6f);
                dir = new Vector2(-Mathf.Sin(a), Mathf.Cos(a));
            }
            float sz = 20f * Mathf.Clamp(Mathf.Sqrt(zoom), 1f, 1.8f);
            Rect icon = new Rect(at.x - sz / 2f, at.y - sz / 2f, sz, sz);
            MapIcons.Draw(icon, MapIcons.ForShip(PlayerShip?.shipClass ?? ShipClass.Tin), new Color(0.45f, 1f, 0.55f), Mathf.Atan2(dir.x, -dir.y) * Mathf.Rad2Deg);
            Rect hit = new Rect(at.x - 9f, at.y - 9f, 18f, 18f);
            if (Mouse.IsOver(hit)) TooltipHandler.TipRegion(hit, "Rimverse_SysYourShip".Translate());
            GUI.color = Color.white;
        }

        private void DrawMegastructures(StarSystemData sys, Vector2 center, Dictionary<int, Vector2> positions)
        {
            GameComponent_Megastructures gc = GameComponent_Megastructures.Instance;
            if (gc == null) return;
            int n = 0;
            float scale = Mathf.Clamp(Mathf.Sqrt(zoom), 1f, 2.2f);
            foreach (MegastructureData m in gc.In(sys.id.Key))
            {
                Vector2 anchor = m.body >= 0 && positions.TryGetValue(m.body, out Vector2 p) ? p : center;
                float a = m.angle * Mathf.Deg2Rad + n * 0.9f;
                float dist = (m.body >= 0 ? 24f : 40f) * scale;
                Vector2 pos = anchor + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * dist;
                float s = 18f * scale;
                Rect r = new Rect(pos.x - s / 2f, pos.y - s / 2f, s, s);
                GUI.color = m.stage <= 0 ? new Color(1f, 1f, 1f, 0.35f) : m.Complete ? Color.white : new Color(1f, 1f, 1f, 0.75f);
                if (m.IsBase)
                {
                    Civilization owner = m.Civ;
                    Color tint = m.kind == MegastructureKind.PirateBase ? (m.ruin ? new Color(0.5f, 0.5f, 0.5f) : new Color(1f, 0.4f, 0.35f)) : owner != null && owner.known ? owner.color : new Color(0.8f, 0.85f, 0.95f);
                    MapIcons.Draw(r, MapIcons.ForMegastructure(m), tint);
                }
                else GUI.DrawTexture(r, MegastructureRender.Icon(m));
                GUI.color = Color.white;
                if (Mouse.IsOver(r.ExpandedBy(3f))) TooltipHandler.TipRegion(r.ExpandedBy(3f), m.Label + "\n" + "Rimverse_MegaTip".Translate(m.OwnerLabel, m.StageLabel, m.Completion.ToStringPercent()));
                n++;
            }
        }

        private void DrawFleetsAndAnomalies(StarSystemData sys, Vector2 center, Dictionary<int, Vector2> positions)
        {
            float scale = Mathf.Clamp(Mathf.Sqrt(zoom), 1f, 2f);
            string key = sys.id.Key;
            GameComponent_Civilizations gc = GameComponent_Civilizations.Instance;
            if (gc != null && gc.generated)
            {
                int n = 0;
                foreach (Fleet f in gc.fleets)
                {
                    if (f.systemKey != key || f.Moving) continue;
                    Civilization c = gc.Civ(f.civId);
                    if (c == null) continue;
                    CivPlanet cp = c.planets.Find(p => p.systemKey == key);
                    Vector2 anchor = cp != null && positions.TryGetValue(cp.body, out Vector2 bp) ? bp : center;
                    float a = 4.1f + n * 0.8f;
                    Vector2 pos = anchor + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (cp != null ? 30f : 52f) * scale;
                    float s = 16f * scale;
                    Rect r = new Rect(pos.x - s / 2f, pos.y - s / 2f, s, s);
                    MapIcons.Draw(r, MapIcons.Fleet, f.TargetsPlayer ? new Color(1f, 0.3f, 0.3f) : c.known ? c.color : new Color(0.7f, 0.7f, 0.7f));
                    if (Mouse.IsOver(r.ExpandedBy(3f))) TooltipHandler.TipRegion(r.ExpandedBy(3f), "Rimverse_FleetTip".Translate(c.ShownName, ("Rimverse_Mission_" + f.mission).Translate(), f.power.ToString("0")));
                    n++;
                }
            }
            GameComponent_PlayerEmpire emp = GameComponent_PlayerEmpire.Instance;
            if (emp == null) return;
            foreach (FoundAnomaly an in emp.anomalies)
            {
                if (an.systemKey != key || an.done || an.def == null) continue;
                Vector2 anchor = positions.TryGetValue(an.body, out Vector2 bp) ? bp : center;
                float s = 14f * scale;
                Vector2 pos = anchor + new Vector2(-0.7f, 0.7f) * 18f * scale;
                Rect r = new Rect(pos.x - s / 2f, pos.y - s / 2f, s, s);
                float pulse = 0.75f + 0.25f * Mathf.Sin(Time.realtimeSinceStartup * 3f);
                MapIcons.Draw(r, MapIcons.Anomaly, new Color(0.85f, 0.55f, 1f, pulse));
                if (Mouse.IsOver(r.ExpandedBy(3f))) TooltipHandler.TipRegion(r.ExpandedBy(3f), an.def.LabelCap.ToString());
            }
        }

        public static void DrawCircle(Vector2 c, float r, float width)
        {
            int seg = Mathf.Clamp((int)(r / 2f), 24, 160);
            Vector2 prev = c + new Vector2(r, 0f);
            for (int i = 1; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                Vector2 next = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                Widgets.DrawLine(prev, next, GUI.color, width);
                prev = next;
            }
        }

        private static Color BodyColor(BodyData b)
        {
            if (b.homeWorld) return new Color(0.3f, 0.8f, 0.4f);
            switch (b.type)
            {
                case BodyType.GasGiant: return new Color(0.9f, 0.7f, 0.45f);
                case BodyType.Ocean: return new Color(0.3f, 0.5f, 1f);
                case BodyType.Belt: return new Color(0.6f, 0.6f, 0.6f);
                case BodyType.SuperEarth: return new Color(0.75f, 0.55f, 0.4f);
                case BodyType.Rocky: return new Color(0.7f, 0.6f, 0.5f);
                default: return new Color(0.65f, 0.65f, 0.7f);
            }
        }

        private string BodyName(StarSystemData sys, BodyData b)
        {
            ScanLevel scan = GameComponent_Universe.Instance.GetScan(sys.id, b.index);
            return scan == ScanLevel.Unknown ? (string)"Rimverse_UnknownBody".Translate() : b.name;
        }

        private void DrawInfo(Rect rect, StarSystemData sys, MapComponent_ShipLocation loc)
        {
            Listing_Standard l = new Listing_Standard();
            l.Begin(rect);
            BodyData here = sys.Body(loc.bodyIndex);
            l.Label("Rimverse_CurrentLocation".Translate(here != null ? BodyName(sys, here) : "?"));
            if (loc.Warping)
            {
                StarSystemData target = GameComponent_Universe.Instance.GetSystem(StarSystemId.FromKey(loc.destinationSystemKey));
                l.Label("Rimverse_WarpingTo".Translate(target?.name ?? "?", loc.Progress.ToStringPercent(), RimverseUnits.DaysFromTicks(loc.arriveTick - Find.TickManager.TicksGame).ToString("0.#")));
            }
            else if (loc.Traveling)
            {
                BodyData dest = sys.Body(loc.destinationBody);
                l.Label("Rimverse_TravelingTo".Translate(dest != null ? BodyName(sys, dest) : "?", loc.Progress.ToStringPercent(), RimverseUnits.DaysFromTicks(loc.arriveTick - Find.TickManager.TicksGame).ToString("0.#")));
            }
            l.GapLine();
            BodyData b = sys.Body(selected);
            if (b == null)
            {
                l.Label("Rimverse_SelectBody".Translate());
                l.End();
                return;
            }
            ScanLevel scan = GameComponent_Universe.Instance.GetScan(sys.id, b.index);
            Text.Font = GameFont.Medium;
            l.Label(BodyName(sys, b));
            Text.Font = GameFont.Small;
            Civilization owner = SettlementGen.CivOn(sys.id.Key, b.index, out CivPlanet ownerPlanet);
            PlayerColony myColony = SettlementGen.ColonyOn(sys.id.Key, b.index);
            if (myColony != null) l.Label("Rimverse_SysYourColony".Translate(myColony.name));
            else if (owner != null) l.Label("Rimverse_SysOwnedBy".Translate(owner.ShownName));
            if (b.type != BodyType.Belt && !b.IsMoon)
            {
                float lum = Mathf.Max(sys.TotalLuminosity, 0.0001f);
                if (b.orbitAU >= Mathf.Sqrt(lum / 1.1f) && b.orbitAU <= Mathf.Sqrt(lum / 0.53f)) l.Label("Rimverse_SysInHabitable".Translate());
                else if (sys.frostLineAU > 0f && b.orbitAU > sys.frostLineAU) l.Label("Rimverse_SysBeyondFrost".Translate());
            }
            foreach (string line in BodyLines(b, scan)) l.Label(line);
            l.GapLine();
            RimverseShip ship = PlayerShip;
            if (ship != null && b.index != loc.bodyIndex && !loc.Traveling)
            {
                float au = sys.DistanceAU(loc.bodyIndex, b.index);
                l.Label("Rimverse_TravelEstimate".Translate(au.ToString("0.##"), SublightTravel.TravelDays(ship, au).ToString("0.#"), SublightTravel.FuelCost(ship, au).ToString("0")));
                if (l.ButtonText("Rimverse_TravelHere".Translate()))
                {
                    if (SublightTravel.TryStart(ship, b.index)) Close();
                }
            }
            l.End();
        }

        public static IEnumerable<string> BodyLines(BodyData b, ScanLevel scan)
        {
            if (scan == ScanLevel.Unknown)
            {
                yield return "Rimverse_ScanUnknown".Translate();
                yield break;
            }
            yield return "Rimverse_BodyType".Translate(("Rimverse_BodyType_" + b.type).Translate());
            if (scan == ScanLevel.Word)
            {
                yield return "Rimverse_BodySizeWord".Translate(SizeWord(b.sizeClass));
                yield return "Rimverse_ScanMore".Translate();
                yield break;
            }
            if (scan == ScanLevel.Range)
            {
                int low = Mathf.Max(1, (b.sizeClass / 20) * 20);
                yield return "Rimverse_BodySizeRange".Translate(low, Mathf.Min(100, low + 20));
                yield return "Rimverse_BodyAtmosphere".Translate(("Rimverse_Atmosphere_" + b.atmosphere).Translate());
                yield return "Rimverse_ScanMore".Translate();
                yield break;
            }
            yield return "Rimverse_BodySizeExact".Translate(b.sizeClass);
            if (b.type != BodyType.Belt)
            {
                yield return "Rimverse_BodyKm".Translate(b.RadiusKm.ToString("N0"));
                yield return "Rimverse_BodyDensity".Translate(b.DensityGcm3.ToString("0.##"));
                yield return "Rimverse_BodyGravity".Translate(b.gravity.ToString("0.##"));
                yield return "Rimverse_BodyEscape".Translate(b.EscapeVelocityKms.ToString("0.##"));
                yield return "Rimverse_BodyTemperature".Translate(b.temperatureC.ToStringTemperature("F0"));
                yield return "Rimverse_BodyEqTemperature".Translate(b.equilibriumC.ToStringTemperature("F0"));
                if (b.tidalLocked) yield return "Rimverse_BodyTidalLocked".Translate();
                if (b.eccentricity >= 0.1f) yield return "Rimverse_BodyEccentric".Translate(b.eccentricity.ToString("0.00"));
                yield return "Rimverse_BodyAtmosphere".Translate(("Rimverse_Atmosphere_" + b.atmosphere).Translate());
                yield return "Rimverse_BodyWater".Translate(b.water.ToStringPercent());
                if (b.atmosphere == AtmosphereType.Breathable && !b.homeWorld) yield return "Rimverse_BodyFauna".Translate(GenStep_AlienFauna.SpeciesFor(b).Count);
            }
            yield return "Rimverse_BodyOrbit".Translate(b.orbitAU.ToString("0.##"));
            if (b.type != BodyType.Belt && b.periodYears > 0f) yield return b.PeriodDays < 120f ? "Rimverse_BodyPeriod".Translate(b.PeriodDays.ToString("0.#")) : "Rimverse_BodyPeriodYears".Translate(b.periodYears.ToString("0.##"));
            if (b.rings) yield return "Rimverse_BodyRings".Translate();
            yield return b.LandableNow ? "Rimverse_BodyLandable".Translate() : "Rimverse_BodyNotLandable".Translate();
        }

        public static string SizeWord(int size)
        {
            if (size < 15) return "Rimverse_SizeTiny".Translate();
            if (size < 35) return "Rimverse_SizeSmall".Translate();
            if (size < 60) return "Rimverse_SizeMedium".Translate();
            if (size < 85) return "Rimverse_SizeLarge".Translate();
            return "Rimverse_SizeHuge".Translate();
        }
    }
}
