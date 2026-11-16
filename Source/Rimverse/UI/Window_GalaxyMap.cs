using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    [StaticConstructorOnStartup]
    public class Window_GalaxyMap : Window
    {
        private static readonly Color Background = new Color(0.01f, 0.01f, 0.035f);
        private static readonly Color SelectColor = new Color(1f, 0.85f, 0.3f);
        private static readonly Color WarpRangeColor = new Color(0.3f, 0.8f, 1f, 0.5f);
        private static readonly Color SensorRangeColor = new Color(0.4f, 1f, 0.5f, 0.35f);
        private static readonly Color GridColor = new Color(1f, 1f, 1f, 0.05f);
        private static readonly Color GridStrong = new Color(0.6f, 0.75f, 1f, 0.16f);
        private static readonly int[] Radii = { 10, 25, 50, 100 };
        private static readonly CosmicLevel[] Levels = { CosmicLevel.Web, CosmicLevel.Supercluster, CosmicLevel.Cluster, CosmicLevel.LargeGroup, CosmicLevel.Group, CosmicLevel.Galaxy, CosmicLevel.Sector, CosmicLevel.Zone, CosmicLevel.District };
        private static readonly Dictionary<string, Texture2D> galaxyTextures = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Texture2D> densityTextures = new Dictionary<string, Texture2D>();
        private static Texture2D glowTex;
        private const int TexSize = 256;

        private readonly Map shipMap;
        private CosmicLevel level = CosmicLevel.District;
        private int radiusIndex = 1;
        private int viewedGalaxy;
        private Vec3d focus;
        private bool focusSet;
        private StarSystemId selected = StarSystemId.Invalid;
        private CosmicNode selectedNode;
        private List<StarEntry> stars;
        private Vec3d starsCenter;
        private int starsGalaxy = -1;
        private int starsRadius = -1;
        private Vector2 infoScroll;
        private float infoHeight = 600f;
        private List<Civilization> starOwners;
        private int ownersFrame;

        public Window_GalaxyMap(Map shipMap)
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
            if (loc != null) viewedGalaxy = loc.SystemId.galaxy;
        }

        public override Vector2 InitialSize => new Vector2(1120f, 760f);

        private static GameComponent_Universe U => GameComponent_Universe.Instance;

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

        private static bool GalaxySpace(CosmicLevel l) => l == CosmicLevel.Galaxy || l == CosmicLevel.Sector || l == CosmicLevel.Zone || l == CosmicLevel.District;

        public override void DoWindowContents(Rect inRect)
        {
            MapComponent_ShipLocation loc = Loc;
            if (U == null || loc == null)
            {
                Widgets.Label(inRect, "Rimverse_NoSystem".Translate());
                return;
            }
            StarSystemId here = loc.SystemId;
            if (!focusSet)
            {
                focus = U.PositionOf(here);
                focusSet = true;
            }
            if (glowTex == null) glowTex = SpaceBackgroundArt.Glow(64, 0.05f, 2.2f);
            GalaxyData g = U.Galaxy(viewedGalaxy);
            Text.Font = GameFont.Medium;
            string title = GalaxySpace(level) ? "Rimverse_GalaxyMapTitle".Translate(g.name, g.TypeLabel).ToString() : CosmicScale.LevelLabel(level).ToString().CapitalizeFirst();
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width * 0.4f, 34f), title);
            Text.Font = GameFont.Small;
            DrawBreadcrumb(new Rect(inRect.x, inRect.y + 34f, inRect.width, 28f));
            if (level == CosmicLevel.District) DrawRadii(new Rect(inRect.x + inRect.width * 0.4f, inRect.y, inRect.width * 0.6f, 30f));
            Rect body = new Rect(inRect.x, inRect.y + 66f, inRect.width, inRect.height - 66f);
            Rect mapRect = body.LeftPart(0.64f).ContractedBy(4f);
            Rect infoRect = body.RightPart(0.36f).ContractedBy(4f);
            float side = Mathf.Min(mapRect.width, mapRect.height);
            mapRect = new Rect(mapRect.x + (mapRect.width - side) / 2f, mapRect.y, side, side);
            Widgets.DrawBoxSolid(mapRect, Background);
            HandleWheel(mapRect, here);
            switch (level)
            {
                case CosmicLevel.Group:
                    DrawGroup(mapRect, here);
                    DrawGroupInfo(infoRect, here);
                    break;
                case CosmicLevel.Galaxy:
                    DrawGalaxy(mapRect, here);
                    DrawGalaxyInfo(infoRect, here);
                    break;
                case CosmicLevel.Sector:
                case CosmicLevel.Zone:
                    DrawGrid(mapRect, here, level);
                    DrawGridInfo(infoRect, here, level);
                    break;
                case CosmicLevel.District:
                    DrawLocal(mapRect, here, loc);
                    DrawStarInfo(infoRect, here, loc);
                    break;
                default:
                    DrawCosmic(mapRect, level);
                    DrawCosmicInfo(infoRect, level);
                    break;
            }
        }

        private void DrawBreadcrumb(Rect rect)
        {
            float x = rect.x;
            float w = Mathf.Min(118f, rect.width / Levels.Length - 4f);
            for (int i = 0; i < Levels.Length; i++)
            {
                Rect b = new Rect(x, rect.y, w, rect.height);
                if (Levels[i] == level) Widgets.DrawHighlight(b);
                Text.Font = GameFont.Tiny;
                if (Widgets.ButtonText(b, CosmicScale.LevelLabel(Levels[i]).ToString().CapitalizeFirst())) SetLevel(Levels[i]);
                Text.Font = GameFont.Small;
                x += w + 4f;
            }
        }

        private void DrawRadii(Rect rect)
        {
            float x = rect.xMax;
            for (int i = Radii.Length - 1; i >= 0; i--)
            {
                x -= 58f;
                Rect b = new Rect(x, rect.y, 54f, 28f);
                if (i == radiusIndex) Widgets.DrawHighlight(b);
                if (Widgets.ButtonText(b, "Rimverse_UnitLy".Translate(Radii[i]))) radiusIndex = i;
            }
            x -= 130f;
            if (Widgets.ButtonText(new Rect(x, rect.y, 126f, 28f), "Rimverse_ViewHere".Translate()))
            {
                MapComponent_ShipLocation loc = Loc;
                if (loc != null)
                {
                    viewedGalaxy = loc.SystemId.galaxy;
                    focus = U.PositionOf(loc.SystemId);
                }
            }
        }

        private void SetLevel(CosmicLevel l)
        {
            level = l;
            selectedNode = null;
            infoScroll = Vector2.zero;
        }

        private void HandleWheel(Rect mapRect, StarSystemId here)
        {
            Event e = Event.current;
            if (e.type != EventType.ScrollWheel || !Mouse.IsOver(mapRect)) return;
            int idx = Array.IndexOf(Levels, level);
            if (e.delta.y > 0 && idx > 0)
            {
                if (level == CosmicLevel.District && radiusIndex < Radii.Length - 1) radiusIndex++;
                else SetLevel(Levels[idx - 1]);
            }
            else if (e.delta.y < 0 && idx < Levels.Length - 1)
            {
                if (GalaxySpace(level)) focus = Unproject(mapRect, e.mousePosition);
                SetLevel(Levels[idx + 1]);
            }
            else if (e.delta.y < 0 && level == CosmicLevel.District && radiusIndex > 0) radiusIndex--;
            e.Use();
        }

        private double Extent(CosmicLevel l)
        {
            GalaxyData g = U.Galaxy(viewedGalaxy);
            switch (l)
            {
                case CosmicLevel.Galaxy: return g.radiusLy * 1.15;
                case CosmicLevel.Sector: return StarSystemId.SectorSizeLy / 2;
                case CosmicLevel.Zone: return StarSystemId.ZoneSizeLy / 2;
                default: return Radii[radiusIndex];
            }
        }

        private Vec3d ViewCenter(CosmicLevel l)
        {
            switch (l)
            {
                case CosmicLevel.Galaxy: return new Vec3d(0, 0, 0);
                case CosmicLevel.Sector: return DistrictId.FromPosition(viewedGalaxy, focus).SectorCenter;
                case CosmicLevel.Zone: return ZoneId.FromPosition(viewedGalaxy, focus).Center;
                default: return focus;
            }
        }

        private Vector2 Project(Rect r, Vec3d p)
        {
            Vec3d c = ViewCenter(level);
            double ext = Extent(level);
            return new Vector2(r.center.x + (float)((p.x - c.x) / ext) * r.width / 2f, r.center.y - (float)((p.z - c.z) / ext) * r.height / 2f);
        }

        private Vec3d Unproject(Rect r, Vector2 s)
        {
            Vec3d c = ViewCenter(level);
            double ext = Extent(level);
            return new Vec3d(c.x + (s.x - r.center.x) / (r.width / 2f) * ext, focus.y, c.z - (s.y - r.center.y) / (r.height / 2f) * ext);
        }

        private static void Glow(Vector2 p, float size, Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(new Rect(p.x - size / 2f, p.y - size / 2f, size, size), glowTex);
        }

        private void DrawCosmic(Rect rect, CosmicLevel l)
        {
            CosmicScale.HomeChain chain = U.Cosmos;
            List<CosmicNode> nodes = l == CosmicLevel.Web ? chain.superclusters : chain.Siblings(l);
            if (nodes == null) return;
            double ext = l == CosmicLevel.Web ? CosmicScale.ChunkSizeMly / 2 : nodes.Max(n => n.positionMly.Length + n.radiusMly) * 1.1;
            Vector2 P(Vec3d v) => new Vector2(rect.center.x + (float)(v.x / ext) * rect.width / 2f, rect.center.y - (float)(v.z / ext) * rect.height / 2f);
            float S(double r) => Mathf.Max(6f, (float)(r / ext) * rect.width);
            GUI.BeginClip(rect);
            Rect local = new Rect(0f, 0f, rect.width, rect.height);
            Rect saved = rect;
            rect = local;
            if (l == CosmicLevel.Web)
            {
                foreach (CosmicNode v in chain.web)
                {
                    Vector2 p = P(v.positionMly);
                    float d = S(v.radiusMly * 2);
                    GUI.color = new Color(0.25f, 0.3f, 0.5f, 0.22f);
                    Window_SystemMap.DrawCircle(p, d / 2f, 1f);
                }
                GUI.color = GridStrong;
                Widgets.DrawBox(new Rect(2f, 2f, rect.width - 4f, rect.height - 4f));
            }
            foreach (CosmicNode n in nodes)
            {
                Vector2 p = P(n.positionMly);
                float d = S(n.radiusMly * 2);
                Glow(p, d * 1.6f, n.home ? new Color(1f, 0.9f, 0.7f, 0.55f) : new Color(0.6f, 0.7f, 1f, 0.35f));
                Glow(p, d * 0.35f, new Color(1f, 1f, 1f, 0.8f));
                Rect click = new Rect(p.x - d / 2f, p.y - d / 2f, d, d);
                if (n.home)
                {
                    GUI.color = Color.green;
                    Window_SystemMap.DrawCircle(p, d / 2f + 4f, 1.5f);
                }
                if (selectedNode == n)
                {
                    GUI.color = SelectColor;
                    Window_SystemMap.DrawCircle(p, d / 2f + 7f, 2f);
                }
                if (Mouse.IsOver(click)) TooltipHandler.TipRegion(click, n.name + "\n" + "Rimverse_CosmicRadius".Translate(n.radiusMly.ToString("0.#")));
                if (Widgets.ButtonInvisible(click, false))
                {
                    if (n.home && selectedNode == n)
                    {
                        int idx = Array.IndexOf(Levels, l);
                        SetLevel(Levels[idx + 1]);
                    }
                    else selectedNode = n;
                }
            }
            GUI.color = Color.white;
            GUI.EndClip();
            rect = saved;
            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            Widgets.Label(new Rect(rect.x + 6f, rect.yMax - 20f, rect.width, 20f), "Rimverse_CosmicScaleLine".Translate((ext * 2).ToString("N0")));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        private void DrawCosmicInfo(Rect rect, CosmicLevel l)
        {
            CosmicScale.HomeChain chain = U.Cosmos;
            Listing_Standard ls = new Listing_Standard();
            ls.Begin(rect);
            Text.Font = GameFont.Medium;
            ls.Label(CosmicScale.LevelLabel(l).ToString().CapitalizeFirst());
            Text.Font = GameFont.Small;
            ls.Label("Rimverse_UniverseAge".Translate(U.AgeGyr.ToString("0.0")));
            ls.Label("Rimverse_CosmicChunk".Translate(chain.chunk.name));
            ls.Label(("Rimverse_CosmicDesc_" + l).Translate());
            ls.GapLine();
            CosmicNode home = l == CosmicLevel.Web ? chain.supercluster : chain.Node(l);
            CosmicNode n = selectedNode ?? home;
            if (n != null)
            {
                Text.Font = GameFont.Medium;
                ls.Label(n.name);
                Text.Font = GameFont.Small;
                ls.Label("Rimverse_CosmicRadius".Translate(n.radiusMly.ToString("0.##")));
                if (n.childCount > 0) ls.Label("Rimverse_CosmicChildren".Translate(n.childCount, CosmicScale.LevelLabel(ChildLevel(n.level))));
                if (n.home)
                {
                    ls.Label("Rimverse_CosmicHome".Translate());
                    if (ls.ButtonText("Rimverse_CosmicZoomIn".Translate()))
                    {
                        int idx = Array.IndexOf(Levels, l == CosmicLevel.Web ? CosmicLevel.Web : n.level);
                        SetLevel(Levels[Mathf.Min(idx + 1, Levels.Length - 1)]);
                    }
                }
                else ls.Label("Rimverse_CosmicFar".Translate());
            }
            ls.End();
        }

        private static CosmicLevel ChildLevel(CosmicLevel l)
        {
            switch (l)
            {
                case CosmicLevel.Supercluster: return CosmicLevel.Cluster;
                case CosmicLevel.Cluster: return CosmicLevel.LargeGroup;
                case CosmicLevel.LargeGroup: return CosmicLevel.Group;
                default: return CosmicLevel.Galaxy;
            }
        }

        private void DrawGroup(Rect rect, StarSystemId here)
        {
            int n = U.GalaxyCount;
            float ext = 0.1f;
            for (int i = 0; i < n; i++) ext = Mathf.Max(ext, U.Galaxy(i).groupPositionMly.magnitude + (float)(U.Galaxy(i).radiusLy / 1e6));
            ext *= 1.25f;
            for (int i = 0; i < n; i++)
            {
                GalaxyData g = U.Galaxy(i);
                Vector2 p = new Vector2(rect.center.x + g.groupPositionMly.x / ext * rect.width / 2f, rect.center.y - g.groupPositionMly.y / ext * rect.height / 2f);
                float size = Mathf.Clamp((float)(g.radiusLy / 1e6) / ext * rect.width * 6f, 18f, 90f);
                Rect r = new Rect(p.x - size / 2f, p.y - size / 2f, size, size);
                GUI.color = Color.white;
                GUI.DrawTexture(r, GalaxyTexture(g), ScaleMode.StretchToFill, true);
                if (i == here.galaxy)
                {
                    GUI.color = Color.green;
                    Widgets.DrawBox(r.ExpandedBy(3f));
                }
                if (i == viewedGalaxy)
                {
                    GUI.color = SelectColor;
                    Widgets.DrawBox(r.ExpandedBy(6f), 2);
                }
                GUI.color = new Color(1f, 1f, 1f, 0.8f);
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.UpperCenter;
                Widgets.Label(new Rect(p.x - 60f, r.yMax + 2f, 120f, 18f), g.name);
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
                if (Mouse.IsOver(r)) TooltipHandler.TipRegion(r, g.name + "\n" + g.TypeLabel + "\n" + "Rimverse_GalaxyClassShort".Translate(g.sizeClass));
                if (Widgets.ButtonInvisible(r, false))
                {
                    if (viewedGalaxy == i) SetLevel(CosmicLevel.Galaxy);
                    viewedGalaxy = i;
                }
            }
            GUI.color = Color.white;
        }

        private void DrawGroupInfo(Rect rect, StarSystemId here)
        {
            CosmicNode group = U.Cosmos.group;
            Listing_Standard ls = new Listing_Standard();
            ls.Begin(rect);
            Text.Font = GameFont.Medium;
            ls.Label(group.name);
            Text.Font = GameFont.Small;
            ls.Label("Rimverse_UniverseAge".Translate(U.AgeGyr.ToString("0.0")));
            ls.Label("Rimverse_LocalGroup".Translate(U.GalaxyCount));
            ls.Label("Rimverse_CosmicRadius".Translate(group.radiusMly.ToString("0.##")));
            ls.GapLine();
            GalaxyData g = U.Galaxy(viewedGalaxy);
            ls.Label(g.name + " (" + g.TypeLabel + ")");
            ls.Label("Rimverse_GalaxyRadiusLine".Translate(g.radiusLy.ToString("N0")));
            if (ls.ButtonText("Rimverse_ViewGalaxyButton".Translate())) SetLevel(CosmicLevel.Galaxy);
            ls.End();
        }

        private List<StarEntry> LocalStars(int galaxy, Vec3d center, int radius)
        {
            if (stars != null && starsGalaxy == galaxy && Vec3d.Distance(starsCenter, center) < 0.01 && starsRadius == radius) return stars;
            stars = GalaxyQuery.StarsInRadius(U, galaxy, center, radius);
            starsCenter = center;
            starsGalaxy = galaxy;
            starsRadius = radius;
            starOwners = null;
            return stars;
        }

        private List<Civilization> Owners(List<StarEntry> list)
        {
            GameComponent_Civilizations gc = GameComponent_Civilizations.Instance;
            if (gc == null || !gc.generated) return null;
            if (starOwners != null && Time.frameCount - ownersFrame < 600) return starOwners;
            starOwners = new List<Civilization>(list.Count);
            foreach (StarEntry e in list) starOwners.Add(gc.OwnerOf(e.id));
            ownersFrame = Time.frameCount;
            return starOwners;
        }

        public static Color StarColor(StarType t)
        {
            switch (t)
            {
                case StarType.RedDwarf: return new Color(1f, 0.45f, 0.35f);
                case StarType.Orange: return new Color(1f, 0.7f, 0.4f);
                case StarType.Yellow: return new Color(1f, 0.95f, 0.6f);
                case StarType.White: return new Color(0.95f, 0.95f, 1f);
                case StarType.Blue: return new Color(0.55f, 0.7f, 1f);
                case StarType.WhiteDwarf: return new Color(0.85f, 0.9f, 1f);
                case StarType.Neutron: return new Color(0.6f, 1f, 1f);
                default: return new Color(0.6f, 0.3f, 0.8f);
            }
        }

        private static float StarSize(StarType t)
        {
            switch (t)
            {
                case StarType.RedDwarf: return 4f;
                case StarType.WhiteDwarf:
                case StarType.Neutron: return 3f;
                case StarType.Blue: return 8f;
                case StarType.White: return 6.5f;
                case StarType.BlackHole: return 6f;
                default: return 5.5f;
            }
        }

        private static string StarName(StarSystemId id, ScanLevel scan)
        {
            if (scan == ScanLevel.Unknown) return "Rimverse_UnknownStar".Translate();
            return U.GetSystem(id)?.name ?? "?";
        }

        private void DrawLocal(Rect rect, StarSystemId here, MapComponent_ShipLocation loc)
        {
            int radius = Radii[radiusIndex];
            if (viewedGalaxy != here.galaxy) viewedGalaxy = here.galaxy;
            Vec3d center = focus;
            Vec3d herePos = U.PositionOf(here);
            float scale = rect.width * 0.48f / radius;
            GUI.BeginGroup(rect);
            Vector2 lc = new Vector2(rect.width / 2f, rect.height / 2f);
            Vector2 hc = lc + new Vector2((float)(herePos.x - center.x), (float)-(herePos.z - center.z)) * scale;
            GUI.color = GridColor;
            for (int r = radius / 4; r <= radius; r += Math.Max(1, radius / 4)) Window_SystemMap.DrawCircle(lc, r * scale, 1f);
            GUI.color = new Color(1f, 1f, 1f, 0.03f);
            double sub = StarSystemId.SubcellSizeLy;
            if (radius <= 50)
            {
                for (double v = Math.Floor((center.x - radius) / sub) * sub; v <= center.x + radius; v += sub) Widgets.DrawLineVertical(lc.x + (float)(v - center.x) * scale, 0f, rect.height);
                for (double v = Math.Floor((center.z - radius) / sub) * sub; v <= center.z + radius; v += sub) Widgets.DrawLineHorizontal(0f, lc.y - (float)(v - center.z) * scale, rect.width);
            }
            RimverseShip ship = PlayerShip;
            if (ship != null && ship.warpRangeLy > 0 && ship.warpRangeLy <= radius * 1.6f)
            {
                GUI.color = WarpRangeColor;
                Window_SystemMap.DrawCircle(hc, ship.warpRangeLy * scale, 2f);
            }
            if (ship != null)
            {
                GameComponent_Universe.SensorReach(ship.sensorSensitivity, out double sensorLy, out _);
                if (sensorLy > 0 && sensorLy <= radius * 1.6f)
                {
                    GUI.color = SensorRangeColor;
                    Window_SystemMap.DrawCircle(hc, (float)sensorLy * scale, 1f);
                }
            }
            HashSet<string> shipSystems = PlayerShipSystems();
            HashSet<string> colonySystems = new HashSet<string>();
            if (GameComponent_PlayerColonies.Instance != null) foreach (PlayerColony pc in GameComponent_PlayerColonies.Instance.colonies) colonySystems.Add(pc.systemKey);
            Dictionary<string, Civilization> capitals = new Dictionary<string, Civilization>();
            if (GameComponent_Civilizations.Instance != null) foreach (Civilization cv in GameComponent_Civilizations.Instance.Alive) if (cv.known && !cv.capitalKey.NullOrEmpty()) capitals[cv.capitalKey] = cv;
            List<StarEntry> list = LocalStars(here.galaxy, center, radius);
            List<Civilization> owners = Owners(list);
            StarEntry hovered = null;
            bool dense = list.Count > 1500;
            for (int i = 0; i < list.Count; i++)
            {
                StarEntry e = list[i];
                Vec3d d = e.position - center;
                Vector2 pos = lc + new Vector2((float)d.x, (float)-d.z) * scale;
                if (pos.x < -8f || pos.y < -8f || pos.x > rect.width + 8f || pos.y > rect.height + 8f) continue;
                ScanLevel scan = U.GetSystemScan(e.id);
                float size = StarSize(e.type) * (dense ? 0.6f : 1f);
                Color col = StarColor(e.type);
                if (scan == ScanLevel.Unknown) col.a = 0.3f;
                else if (scan == ScanLevel.Word) col.a = 0.65f;
                Rect dot = new Rect(pos.x - size / 2f, pos.y - size / 2f, size, size);
                Civilization owner = owners != null && i < owners.Count ? owners[i] : null;
                if (owner != null && owner.known)
                {
                    Color oc = owner.color;
                    oc.a = 0.35f;
                    GUI.color = oc;
                    GUI.DrawTexture(dot.ExpandedBy(4f), BaseContent.WhiteTex);
                }
                if (!dense && scan != ScanLevel.Unknown) Glow(pos, size * 3.2f, new Color(col.r, col.g, col.b, 0.25f));
                GUI.color = col;
                GUI.DrawTexture(dot, BaseContent.WhiteTex);
                if (e.id == here)
                {
                    GUI.color = Color.green;
                    Widgets.DrawBox(dot.ExpandedBy(5f));
                }
                else if (shipSystems.Contains(e.id.Key))
                {
                    GUI.color = new Color(0.4f, 0.9f, 0.4f, 0.7f);
                    Widgets.DrawBox(dot.ExpandedBy(4f));
                }
                if (U.IsHome(e.id))
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.8f);
                    Widgets.DrawBox(dot.ExpandedBy(8f));
                }
                if (GateUtility.IsGate(e.id))
                {
                    GUI.color = Color.white;
                    GUI.DrawTexture(dot.ExpandedBy(6f), MegastructureRender.Icon(new MegastructureData { kind = MegastructureKind.Gate }));
                }
                if (colonySystems.Contains(e.id.Key)) MapIcons.Draw(new Rect(dot.xMax + 1f, dot.y - 11f, 11f, 11f), MapIcons.Colony, new Color(0.4f, 1f, 0.55f));
                else if (capitals.TryGetValue(e.id.Key, out Civilization capCiv)) MapIcons.Draw(new Rect(dot.xMax + 1f, dot.y - 11f, 11f, 11f), MapIcons.CivWorld, capCiv.color);
                if (loc.Warping && e.id.Key == loc.destinationSystemKey)
                {
                    Widgets.DrawLine(hc, pos, Color.cyan, 1f);
                    GUI.color = Color.cyan;
                    Widgets.DrawBox(dot.ExpandedBy(6f));
                }
                if (e.id == selected)
                {
                    GUI.color = SelectColor;
                    Widgets.DrawBox(dot.ExpandedBy(3f), 2);
                }
                Rect click = dot.ExpandedBy(4f);
                if (Mouse.IsOver(click)) hovered = e;
                if (Widgets.ButtonInvisible(click, false))
                {
                    if (selected == e.id && e.id == here) Find.WindowStack.Add(new Window_SystemMap(shipMap));
                    selected = e.id;
                }
            }
            DrawFleets(lc, center, scale, radius);
            GUI.color = Color.white;
            GUI.EndGroup();
            if (hovered != null)
            {
                ScanLevel scan = U.GetSystemScan(hovered.id);
                TooltipHandler.TipRegion(new Rect(Event.current.mousePosition.x - 4f, Event.current.mousePosition.y - 4f, 8f, 8f), StarName(hovered.id, scan) + "\n" + ("Rimverse_Star_" + hovered.type).Translate() + "\n" + "Rimverse_DistanceLy".Translate(Vec3d.Distance(hovered.position, herePos).ToString("0.#")));
            }
            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            Widgets.Label(new Rect(rect.x + 6f, rect.yMax - 20f, rect.width, 20f), "Rimverse_StarsShown".Translate(list.Count, radius));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        private static void DrawFleets(Vector2 lc, Vec3d center, float scale, int radius)
        {
            GameComponent_Civilizations gc = GameComponent_Civilizations.Instance;
            if (gc == null || !gc.generated) return;
            int now = Find.TickManager.TicksGame;
            foreach (Fleet f in gc.fleets)
            {
                Civilization c = gc.Civ(f.civId);
                if (c == null || !c.known) continue;
                Vec3d a = U.PositionOf(StarSystemId.FromKey(f.systemKey));
                Vec3d p = a;
                if (f.Moving)
                {
                    Vec3d b = U.PositionOf(StarSystemId.FromKey(f.destKey));
                    float t = f.arriveTick > f.departTick ? Mathf.Clamp01((now - f.departTick) / (float)(f.arriveTick - f.departTick)) : 1f;
                    p = a + (b - a) * t;
                }
                Vec3d d = p - center;
                if (Math.Abs(d.x) > radius || Math.Abs(d.z) > radius) continue;
                Vector2 pos = lc + new Vector2((float)d.x, (float)-d.z) * scale;
                Rect r = new Rect(pos.x - 7f, pos.y - 7f, 14f, 14f);
                float ang = 0f;
                if (f.Moving)
                {
                    Vec3d b2 = U.PositionOf(StarSystemId.FromKey(f.destKey)) - a;
                    ang = Mathf.Atan2((float)b2.x, (float)b2.z) * Mathf.Rad2Deg;
                }
                MapIcons.Draw(r, MapIcons.Fleet, f.TargetsPlayer ? Color.red : c.color, ang);
                if (Mouse.IsOver(r.ExpandedBy(3f))) TooltipHandler.TipRegion(r.ExpandedBy(3f), "Rimverse_FleetTip".Translate(c.Name, ("Rimverse_Mission_" + f.mission).Translate(), f.power.ToString("0")));
            }
        }

        private static HashSet<string> PlayerShipSystems()
        {
            HashSet<string> set = new HashSet<string>();
            foreach (Map m in Find.Maps)
            {
                MapComponent_ShipLocation l = MapComponent_ShipLocation.For(m);
                MapComponent_RimverseShips ships = m.GetComponent<MapComponent_RimverseShips>();
                if (l == null || ships == null || l.systemKey.NullOrEmpty()) continue;
                foreach (RimverseShip s in ships.Ships)
                {
                    if (s.bridge != null && s.bridge.Faction == Faction.OfPlayer)
                    {
                        set.Add(l.systemKey);
                        break;
                    }
                }
            }
            return set;
        }

        private void DrawStarInfo(Rect rect, StarSystemId here, MapComponent_ShipLocation loc)
        {
            Rect view = new Rect(0f, 0f, rect.width - 16f, infoHeight);
            Widgets.BeginScrollView(rect, ref infoScroll, view);
            Listing_Standard l = new Listing_Standard();
            l.Begin(view);
            StarSystemData current = U.GetSystem(here);
            l.Label("Rimverse_CurrentSystem".Translate(current?.name ?? "?"));
            if (loc.Warping)
            {
                StarSystemData target = U.GetSystem(StarSystemId.FromKey(loc.destinationSystemKey));
                l.Label("Rimverse_WarpingTo".Translate(target?.name ?? "?", loc.Progress.ToStringPercent(), RimverseUnits.DaysFromTicks(loc.arriveTick - Find.TickManager.TicksGame).ToString("0.#")));
            }
            RimverseShip ship = PlayerShip;
            if (ship != null)
            {
                if (ship.warpRangeLy > 0) l.Label("Rimverse_WarpSummary".Translate(ship.warpRangeLy, ship.warpMaxMass.ToString("0"), WarpTravel.MatterAvailable(ship).ToString("0")));
                else l.Label("Rimverse_NoWarpDriveShort".Translate());
                int cd = WarpTravel.CooldownTicksLeft(ship);
                if (cd > 0) l.Label("Rimverse_WarpCooldownLeft".Translate((cd / (float)RimverseUnits.TicksPerHour).ToString("0.#")));
            }
            l.Label("Rimverse_SystemsCataloged".Translate(U.SystemCatalogCount));
            l.Label("Rimverse_ScannedVolume".Translate(U.ScanBubbleCount, U.ScannedVolumeLy3.ToString("N0")));
            l.GapLine();
            if (!selected.Valid)
            {
                l.Label("Rimverse_SelectStar".Translate());
                l.End();
                infoHeight = l.CurHeight + 20f;
                Widgets.EndScrollView();
                return;
            }
            ScanLevel scan = U.GetSystemScan(selected);
            Text.Font = GameFont.Medium;
            l.Label(StarName(selected, scan));
            Text.Font = GameFont.Small;
            StarSystemData sys = U.GetSystem(selected);
            double ly = U.DistanceLy(here, selected);
            l.Label("Rimverse_DistanceLy".Translate(ly.ToString("0.#")));
            Civilization ownerCiv = GameComponent_Civilizations.Instance?.generated == true ? GameComponent_Civilizations.Instance.OwnerOf(selected) : null;
            if (ownerCiv != null && ownerCiv.known) l.Label("Rimverse_TerritoryOf".Translate(ownerCiv.Name));
            else if (ownerCiv != null && scan >= ScanLevel.Range) l.Label("Rimverse_TerritoryUnknownCiv".Translate());
            l.Label("Rimverse_ScanLevelLine".Translate(("Rimverse_ScanLevel_" + scan).Translate()));
            l.Label("Rimverse_SystemAddress".Translate(selected.Address(U.PositionOf(selected))));
            if (scan == ScanLevel.Unknown)
            {
                l.Label(("Rimverse_Star_" + StarSystemGenerator.PrimaryTypeFor(selected, U.universeSeed, U.IsHome(selected))).Translate());
                l.Label("Rimverse_StarNeedsScan".Translate());
            }
            else
            {
                List<string> starTypes = new List<string>();
                foreach (StarData s in sys.stars) starTypes.Add(("Rimverse_Star_" + s.type).Translate());
                l.Label(string.Join(" + ", starTypes));
                if (scan >= ScanLevel.Range)
                {
                    int planets = 0, moons = 0, belts = 0;
                    foreach (BodyData b in sys.bodies)
                    {
                        if (b.type == BodyType.Belt) belts++;
                        else if (b.IsMoon) moons++;
                        else planets++;
                    }
                    l.Label("Rimverse_SystemBodiesCount".Translate(planets, moons, belts));
                    foreach (BodyData b in sys.bodies)
                    {
                        if (b.IsMoon) continue;
                        string extra = scan == ScanLevel.Exact ? " (" + ("Rimverse_Atmosphere_" + b.atmosphere).Translate().ToString() + ")" : "";
                        l.Label("  " + b.name + ": " + ("Rimverse_BodyType_" + b.type).Translate().ToString() + extra);
                    }
                }
                else
                {
                    l.Label("Rimverse_SystemSizeWord".Translate(Window_SystemMap.SizeWord(sys.sizeClass)));
                }
            }
            l.GapLine();
            if (ship != null && selected != here)
            {
                List<string> problems = WarpTravel.Problems(ship, loc, selected, out double dist, out float matter);
                l.Label("Rimverse_WarpEstimate".Translate(dist.ToString("0.#"), matter.ToString("0"), WarpTravel.JumpDays.ToString("0.#")));
                if (problems.Count == 0)
                {
                    if (l.ButtonText("Rimverse_WarpHere".Translate()))
                    {
                        if (WarpTravel.TryStart(ship, selected)) Close();
                    }
                }
                else
                {
                    GUI.color = new Color(1f, 0.5f, 0.45f);
                    foreach (string p in problems) l.Label("- " + p);
                    GUI.color = Color.white;
                }
            }
            l.End();
            infoHeight = l.CurHeight + 20f;
            Widgets.EndScrollView();
        }

        private static Texture2D GalaxyTexture(GalaxyData g)
        {
            string key = U.universeSeed + ":" + g.index + ":" + U.DensityFactor;
            if (galaxyTextures.TryGetValue(key, out Texture2D tex) && tex != null) return tex;
            tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            Color[] pixels = new Color[TexSize * TexSize];
            double extent = g.radiusLy * 1.15;
            double peak = g.baseDensity > 0 ? g.baseDensity : 1e-9;
            Color tint = g.type == GalaxyType.Elliptical ? new Color(1f, 0.82f, 0.6f) : g.type == GalaxyType.DwarfSpheroidal ? new Color(0.95f, 0.85f, 0.75f) : g.type == GalaxyType.Irregular ? new Color(0.7f, 0.8f, 1f) : new Color(0.68f, 0.78f, 1f);
            Color core = new Color(1f, 0.9f, 0.7f);
            System.Random r = new System.Random(g.index * 977 + U.universeSeed);
            for (int y = 0; y < TexSize; y++)
            {
                for (int x = 0; x < TexSize; x++)
                {
                    double px = ((x + 0.5) / TexSize * 2.0 - 1.0) * extent;
                    double pz = ((y + 0.5) / TexSize * 2.0 - 1.0) * extent;
                    double d = g.DensityAt(new Vec3d(px, 0, pz)) / peak;
                    float v = (float)Math.Pow(Math.Max(0, d), 0.45);
                    float rr = (float)(Math.Sqrt(px * px + pz * pz) / g.radiusLy);
                    Color c = Color.Lerp(tint, core, Mathf.Clamp01(1.2f - rr * 2.2f));
                    c = Color.Lerp(c, Color.white, Mathf.Clamp01(v - 0.7f));
                    if (r.NextDouble() < v * 0.04) c = Color.white;
                    c.a = Mathf.Clamp01(v * 1.1f);
                    pixels[y * TexSize + x] = c;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            galaxyTextures[key] = tex;
            return tex;
        }

        private Texture2D DensityTexture(GalaxyData g, Vec3d center, double size, int res, string key)
        {
            if (densityTextures.TryGetValue(key, out Texture2D tex) && tex != null) return tex;
            if (densityTextures.Count > 24) densityTextures.Clear();
            tex = new Texture2D(res, res, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            Color[] px = new Color[res * res];
            double peak = g.baseDensity > 0 ? g.baseDensity : 1e-9;
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    Vec3d p = new Vec3d(center.x + ((x + 0.5) / res - 0.5) * size, center.y, center.z + ((y + 0.5) / res - 0.5) * size);
                    float v = (float)Math.Pow(Math.Max(0, g.DensityAt(p) / peak), 0.5);
                    Color c = Color.Lerp(new Color(0.35f, 0.45f, 0.8f), new Color(1f, 0.92f, 0.8f), v);
                    c.a = Mathf.Clamp01(v * 0.9f);
                    px[y * res + x] = c;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            densityTextures[key] = tex;
            return tex;
        }

        private void DrawMarkers(Rect rect, StarSystemId here, double ext)
        {
            GameComponent_Civilizations gc = GameComponent_Civilizations.Instance;
            if (gc != null && gc.generated)
            {
                foreach (Civilization c in gc.Known)
                {
                    StarSystemId cap = c.Capital;
                    if (cap.galaxy != viewedGalaxy) continue;
                    Vector2 p = Project(rect, U.PositionOf(cap));
                    if (!rect.Contains(p)) continue;
                    Color col = c.color;
                    col.a = 0.45f;
                    float terr = Mathf.Clamp((float)(Math.Sqrt(c.planets.Count) * 40.0 / ext) * rect.width, 8f, rect.width * 0.25f);
                    Glow(p, terr, col);
                    GUI.color = c.color;
                    GUI.DrawTexture(new Rect(p.x - 3f, p.y - 3f, 6f, 6f), BaseContent.WhiteTex);
                    Text.Font = GameFont.Tiny;
                    Widgets.Label(new Rect(p.x + 5f, p.y - 8f, 140f, 18f), c.Name);
                    Text.Font = GameFont.Small;
                }
            }
            foreach (StarSystemId gate in GateUtility.GatesIn(viewedGalaxy))
            {
                Vector2 p = Project(rect, U.PositionOf(gate));
                if (!rect.Contains(p)) continue;
                Rect r = new Rect(p.x - 7f, p.y - 7f, 14f, 14f);
                GUI.color = GameComponent_Endgame.Instance?.activatedGates.Contains(gate.Key) == true ? Color.cyan : new Color(0.6f, 0.65f, 0.75f);
                GUI.DrawTexture(r, MegastructureRender.Icon(new MegastructureData { kind = MegastructureKind.Gate }));
                if (Mouse.IsOver(r)) TooltipHandler.TipRegion(r, GateUtility.Label(gate));
            }
            foreach (ScanBubble b in U.ScanBubbles)
            {
                if (b.galaxy != viewedGalaxy) continue;
                Vector2 p = Project(rect, b.Center);
                float rad = (float)(b.radiusLy / ext) * rect.width / 2f;
                if (rad < 1.5f || !rect.ExpandedBy(rad).Contains(p)) continue;
                GUI.color = SensorRangeColor;
                Window_SystemMap.DrawCircle(p, rad, 1f);
            }
            if (here.galaxy == viewedGalaxy)
            {
                Vector2 hp = Project(rect, U.PositionOf(U.HomeSystemId));
                GUI.color = new Color(1f, 1f, 1f, 0.9f);
                if (rect.Contains(hp)) Widgets.DrawBox(new Rect(hp.x - 6f, hp.y - 6f, 12f, 12f));
                Vector2 cp = Project(rect, U.PositionOf(here));
                if (rect.Contains(cp))
                {
                    GUI.color = Color.green;
                    GUI.DrawTexture(new Rect(cp.x - 3f, cp.y - 3f, 6f, 6f), BaseContent.WhiteTex);
                    Widgets.DrawBox(new Rect(cp.x - 9f, cp.y - 9f, 18f, 18f));
                }
            }
            GUI.color = Color.white;
        }

        private void DrawGalaxy(Rect rect, StarSystemId here)
        {
            GalaxyData g = U.Galaxy(viewedGalaxy);
            GUI.color = Color.white;
            GUI.DrawTexture(rect, GalaxyTexture(g), ScaleMode.StretchToFill, true);
            double extent = Extent(CosmicLevel.Galaxy);
            double sector = StarSystemId.SectorSizeLy;
            GUI.color = GridColor;
            for (double v = -Math.Floor(extent / sector) * sector; v <= extent; v += sector)
            {
                float sx = rect.center.x + (float)(v / extent) * rect.width / 2f;
                float sy = rect.center.y - (float)(v / extent) * rect.height / 2f;
                Widgets.DrawLineVertical(sx, rect.y, rect.height);
                Widgets.DrawLineHorizontal(rect.x, sy, rect.width);
            }
            DrawMarkers(rect, here, extent);
            if (Mouse.IsOver(rect))
            {
                Vec3d m = Unproject(rect, Event.current.mousePosition);
                DistrictId d = DistrictId.FromPosition(viewedGalaxy, m);
                Vector2 a = Project(rect, d.SectorCenter - new Vec3d(sector / 2, 0, sector / 2));
                Vector2 b = Project(rect, d.SectorCenter + new Vec3d(sector / 2, 0, sector / 2));
                GUI.color = SelectColor;
                Widgets.DrawBox(Rect.MinMaxRect(a.x, b.y, b.x, a.y));
                GUI.color = Color.white;
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    focus = m;
                    SetLevel(CosmicLevel.Sector);
                    Event.current.Use();
                }
            }
            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            Widgets.Label(new Rect(rect.x + 6f, rect.yMax - 20f, rect.width, 20f), "Rimverse_GalaxyScale".Translate((g.radiusLy * 2.0).ToString("N0")));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        private void DrawGrid(Rect rect, StarSystemId here, CosmicLevel l)
        {
            GalaxyData g = U.Galaxy(viewedGalaxy);
            Vec3d c = ViewCenter(l);
            double size = Extent(l) * 2;
            int cells = l == CosmicLevel.Sector ? StarSystemId.ZonesPerSector : StarSystemId.DistrictsPerZone;
            double cell = size / cells;
            string key = viewedGalaxy + ":" + l + ":" + Math.Round(c.x) + ":" + Math.Round(c.z) + ":" + U.DensityFactor;
            GUI.color = Color.white;
            GUI.DrawTexture(rect, DensityTexture(g, c, size, 96, key), ScaleMode.StretchToFill, true);
            Vec3d origin = c - new Vec3d(size / 2, 0, size / 2);
            for (int i = 0; i < cells; i++)
            {
                for (int j = 0; j < cells; j++)
                {
                    Vec3d cc = origin + new Vec3d((i + 0.5) * cell, 0, (j + 0.5) * cell);
                    Vector2 a = Project(rect, cc - new Vec3d(cell / 2, 0, cell / 2));
                    Vector2 b = Project(rect, cc + new Vec3d(cell / 2, 0, cell / 2));
                    Rect r = Rect.MinMaxRect(a.x, b.y, b.x, a.y);
                    bool exists = l == CosmicLevel.Sector ? GalaxyQuery.ZoneExists(g, ZoneId.FromPosition(viewedGalaxy, cc)) : GalaxyQuery.SystemCount(U, g, DistrictId.FromPosition(viewedGalaxy, cc)) > 0;
                    GUI.color = exists ? GridStrong : new Color(0f, 0f, 0f, 0.55f);
                    if (exists) Widgets.DrawBox(r);
                    else GUI.DrawTexture(r, BaseContent.WhiteTex);
                    if (Mouse.IsOver(r))
                    {
                        GUI.color = SelectColor;
                        Widgets.DrawBox(r, 2);
                        double expected = l == CosmicLevel.Sector ? GalaxyQuery.ExpectedInZone(g, ZoneId.FromPosition(viewedGalaxy, cc)) : GalaxyQuery.SystemCount(U, g, DistrictId.FromPosition(viewedGalaxy, cc));
                        TooltipHandler.TipRegion(r, (l == CosmicLevel.Sector ? CosmicScale.LevelLabel(CosmicLevel.Zone) : CosmicScale.LevelLabel(CosmicLevel.District)).ToString().CapitalizeFirst() + "\n" + "Rimverse_GridSystems".Translate(expected.ToString("N0")));
                        if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && exists)
                        {
                            focus = cc;
                            if (l == CosmicLevel.Zone) radiusIndex = 2;
                            SetLevel(l == CosmicLevel.Sector ? CosmicLevel.Zone : CosmicLevel.District);
                            Event.current.Use();
                        }
                    }
                }
            }
            DrawMarkers(rect, here, Extent(l));
            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            Widgets.Label(new Rect(rect.x + 6f, rect.yMax - 20f, rect.width, 20f), "Rimverse_GridScale".Translate(size.ToString("N0"), cell.ToString("N0")));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        private void DrawGridInfo(Rect rect, StarSystemId here, CosmicLevel l)
        {
            GalaxyData g = U.Galaxy(viewedGalaxy);
            Vec3d c = ViewCenter(l);
            Listing_Standard ls = new Listing_Standard();
            ls.Begin(rect);
            Text.Font = GameFont.Medium;
            ls.Label(CosmicScale.LevelLabel(l).ToString().CapitalizeFirst());
            Text.Font = GameFont.Small;
            ls.Label(g.name + " (" + g.TypeLabel + ")");
            ls.Label("Rimverse_SystemAddress".Translate(StarSystemId.Invalid.Address(c)));
            double size = Extent(l) * 2;
            ls.Label("Rimverse_GridSize".Translate(size.ToString("N0")));
            double expected = g.ExpectedSystems(c, size);
            ls.Label("Rimverse_GridSystems".Translate(expected.ToString("N0")));
            if (l == CosmicLevel.Sector) ls.Label(GalaxyQuery.SectorExists(g, c) ? "Rimverse_SectorCharted".Translate() : "Rimverse_SectorEmpty".Translate());
            if (g.InBar(c)) ls.Label("Rimverse_InBar".Translate());
            ls.GapLine();
            ls.Label("Rimverse_GridHelp".Translate());
            ls.Label("Rimverse_ScannedVolume".Translate(U.ScanBubbleCount, U.ScannedVolumeLy3.ToString("N0")));
            ls.End();
        }

        private void DrawGalaxyInfo(Rect rect, StarSystemId here)
        {
            Rect view = new Rect(0f, 0f, rect.width - 16f, infoHeight);
            Widgets.BeginScrollView(rect, ref infoScroll, view);
            Listing_Standard l = new Listing_Standard();
            l.Begin(view);
            GalaxyData g = U.Galaxy(viewedGalaxy);
            Text.Font = GameFont.Medium;
            l.Label(g.name);
            Text.Font = GameFont.Small;
            l.Label("Rimverse_GalaxyTypeLine".Translate(g.TypeLabel));
            l.Label("Rimverse_GalaxyClassLine".Translate(g.sizeClass));
            l.Label("Rimverse_GalaxyRadiusLine".Translate(g.radiusLy.ToString("N0")));
            l.Label("Rimverse_GalaxyRuleLine".Translate(("Rimverse_TravelRule_" + g.rule).Translate()));
            l.Label(("Rimverse_TravelRuleDesc_" + g.rule).Translate());
            if (here.galaxy == viewedGalaxy) l.Label("Rimverse_GalaxyYouAreHere".Translate());
            l.Label("Rimverse_UniverseAge".Translate(U.AgeGyr.ToString("0.0")));
            l.Label("Rimverse_ScannedVolume".Translate(U.ScanBubbleCount, U.ScannedVolumeLy3.ToString("N0")));
            l.Label("Rimverse_GalaxyClickHelp".Translate());
            l.GapLine();
            l.Label("Rimverse_LocalGroup".Translate(U.GalaxyCount));
            for (int i = 0; i < U.GalaxyCount; i++)
            {
                GalaxyData o = U.Galaxy(i);
                float mly = (o.groupPositionMly - g.groupPositionMly).magnitude;
                string line = o.name + " (" + o.TypeLabel + ", " + "Rimverse_GalaxyClassShort".Translate(o.sizeClass) + ", " + mly.ToString("0.##") + " Mly)";
                if (i == viewedGalaxy)
                {
                    GUI.color = SelectColor;
                    l.Label("> " + line);
                    GUI.color = Color.white;
                }
                else if (l.ButtonTextLabeled(line, "Rimverse_ViewGalaxyButton".Translate()))
                {
                    viewedGalaxy = i;
                }
            }
            l.End();
            infoHeight = l.CurHeight + 20f;
            Widgets.EndScrollView();
        }
    }
}
