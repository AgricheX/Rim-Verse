using System;
using System.Collections.Generic;
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
        private static readonly int[] Radii = { 10, 25, 50, 100 };
        private static readonly Dictionary<string, Texture2D> galaxyTextures = new Dictionary<string, Texture2D>();
        private const int TexSize = 160;

        private readonly Map shipMap;
        private bool galaxyView;
        private int radiusIndex = 1;
        private int viewedGalaxy;
        private StarSystemId selected = StarSystemId.Invalid;
        private List<StarEntry> stars;
        private StarSystemId starsCenter = StarSystemId.Invalid;
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

        public override Vector2 InitialSize => new Vector2(1040f, 720f);

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

        public override void DoWindowContents(Rect inRect)
        {
            MapComponent_ShipLocation loc = Loc;
            if (U == null || loc == null)
            {
                Widgets.Label(inRect, "Rimverse_NoSystem".Translate());
                return;
            }
            StarSystemId here = loc.SystemId;
            Text.Font = GameFont.Medium;
            GalaxyData g = U.Galaxy(galaxyView ? viewedGalaxy : here.galaxy);
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width * 0.5f, 34f), "Rimverse_GalaxyMapTitle".Translate(g.name, g.TypeLabel));
            Text.Font = GameFont.Small;
            DrawToolbar(new Rect(inRect.x + inRect.width * 0.5f, inRect.y, inRect.width * 0.5f, 30f));
            Rect body = new Rect(inRect.x, inRect.y + 38f, inRect.width, inRect.height - 38f);
            Rect mapRect = body.LeftPart(0.64f).ContractedBy(4f);
            Rect infoRect = body.RightPart(0.36f).ContractedBy(4f);
            float side = Mathf.Min(mapRect.width, mapRect.height);
            mapRect = new Rect(mapRect.x + (mapRect.width - side) / 2f, mapRect.y, side, side);
            Widgets.DrawBoxSolid(mapRect, Background);
            if (galaxyView)
            {
                DrawGalaxy(mapRect, here);
                DrawGalaxyInfo(infoRect, here);
            }
            else
            {
                DrawLocal(mapRect, here, loc);
                DrawStarInfo(infoRect, here, loc);
            }
        }

        private void DrawToolbar(Rect rect)
        {
            float x = rect.xMax;
            if (!galaxyView)
            {
                for (int i = Radii.Length - 1; i >= 0; i--)
                {
                    x -= 58f;
                    Rect b = new Rect(x, rect.y, 54f, 28f);
                    if (i == radiusIndex) Widgets.DrawHighlight(b);
                    if (Widgets.ButtonText(b, "Rimverse_UnitLy".Translate(Radii[i]))) radiusIndex = i;
                }
            }
            x -= 130f;
            if (Widgets.ButtonText(new Rect(x, rect.y, 126f, 28f), galaxyView ? "Rimverse_ViewLocal".Translate() : "Rimverse_ViewGalaxy".Translate()))
            {
                galaxyView = !galaxyView;
                MapComponent_ShipLocation loc = Loc;
                if (loc != null) viewedGalaxy = loc.SystemId.galaxy;
            }
        }

        private List<StarEntry> LocalStars(StarSystemId here, int radius)
        {
            if (stars != null && starsCenter == here && starsRadius == radius) return stars;
            stars = GalaxyQuery.StarsInRadius(U, here.galaxy, U.PositionOf(here), radius);
            starsCenter = here;
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
            Vec3d center = U.PositionOf(here);
            float scale = rect.width * 0.48f / radius;
            Vector2 c = rect.center;
            GUI.BeginGroup(rect);
            Vector2 lc = new Vector2(rect.width / 2f, rect.height / 2f);
            GUI.color = GridColor;
            for (int r = radius / 4; r <= radius; r += Math.Max(1, radius / 4)) Window_SystemMap.DrawCircle(lc, r * scale, 1f);
            RimverseShip ship = PlayerShip;
            if (ship != null && ship.warpRangeLy > 0 && ship.warpRangeLy <= radius * 1.2f)
            {
                GUI.color = WarpRangeColor;
                Window_SystemMap.DrawCircle(lc, ship.warpRangeLy * scale, 2f);
            }
            if (ship != null)
            {
                GameComponent_Universe.SensorReach(ship.sensorSensitivity, out double sensorLy, out _);
                if (sensorLy > 0 && sensorLy <= radius * 1.2f)
                {
                    GUI.color = SensorRangeColor;
                    Window_SystemMap.DrawCircle(lc, (float)sensorLy * scale, 1f);
                }
            }
            HashSet<string> shipSystems = PlayerShipSystems();
            List<StarEntry> list = LocalStars(here, radius);
            List<Civilization> owners = Owners(list);
            StarEntry hovered = null;
            for (int i = 0; i < list.Count; i++)
            {
                StarEntry e = list[i];
                Vec3d d = e.position - center;
                Vector2 pos = lc + new Vector2((float)d.x, (float)-d.z) * scale;
                ScanLevel scan = U.GetSystemScan(e.id);
                float size = StarSize(e.type);
                Color col = StarColor(e.type);
                if (scan == ScanLevel.Unknown) col.a = 0.3f;
                else if (scan == ScanLevel.Word) col.a = 0.65f;
                GUI.color = col;
                Rect dot = new Rect(pos.x - size / 2f, pos.y - size / 2f, size, size);
                Civilization owner = owners != null && i < owners.Count ? owners[i] : null;
                if (owner != null && owner.known)
                {
                    Color oc = owner.color;
                    oc.a = 0.35f;
                    GUI.color = oc;
                    GUI.DrawTexture(dot.ExpandedBy(4f), BaseContent.WhiteTex);
                    GUI.color = col;
                }
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
                if (loc.Warping && e.id.Key == loc.destinationSystemKey)
                {
                    GUI.color = Color.cyan;
                    Widgets.DrawLine(lc, pos, Color.cyan, 1f);
                    Widgets.DrawBox(dot.ExpandedBy(6f));
                }
                if (e.id == selected)
                {
                    GUI.color = SelectColor;
                    Widgets.DrawBox(dot.ExpandedBy(3f), 2);
                }
                Rect click = dot.ExpandedBy(4f);
                if (Mouse.IsOver(click)) hovered = e;
                if (Widgets.ButtonInvisible(click, false)) selected = e.id;
            }
            DrawFleets(lc, center, scale, radius);
            GUI.color = Color.white;
            GUI.EndGroup();
            if (hovered != null)
            {
                ScanLevel scan = U.GetSystemScan(hovered.id);
                TooltipHandler.TipRegion(new Rect(Event.current.mousePosition.x - 4f, Event.current.mousePosition.y - 4f, 8f, 8f), StarName(hovered.id, scan) + "\n" + ("Rimverse_Star_" + hovered.type).Translate() + "\n" + "Rimverse_DistanceLy".Translate(Vec3d.Distance(hovered.position, center).ToString("0.#")));
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
                Rect r = new Rect(pos.x - 3f, pos.y - 3f, 6f, 6f);
                GUI.color = f.TargetsPlayer ? Color.red : c.color;
                Widgets.DrawBox(r, 2);
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
            Color tint = g.type == GalaxyType.Elliptical ? new Color(1f, 0.85f, 0.65f) : new Color(0.75f, 0.82f, 1f);
            for (int y = 0; y < TexSize; y++)
            {
                for (int x = 0; x < TexSize; x++)
                {
                    double px = ((x + 0.5) / TexSize * 2.0 - 1.0) * extent;
                    double pz = ((y + 0.5) / TexSize * 2.0 - 1.0) * extent;
                    double d = g.DensityAt(new Vec3d(px, 0, pz)) / peak;
                    float v = (float)Math.Pow(Math.Max(0, d), 0.45);
                    Color c = Color.Lerp(tint, Color.white, Mathf.Clamp01(v - 0.6f));
                    c.a = Mathf.Clamp01(v);
                    pixels[y * TexSize + x] = c;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            galaxyTextures[key] = tex;
            return tex;
        }

        private void DrawGalaxy(Rect rect, StarSystemId here)
        {
            GalaxyData g = U.Galaxy(viewedGalaxy);
            GUI.color = Color.white;
            GUI.DrawTexture(rect, GalaxyTexture(g), ScaleMode.StretchToFill, true);
            double extent = g.radiusLy * 1.15;
            Vector2 Project(Vec3d p) => new Vector2(rect.x + (float)((p.x / extent + 1.0) / 2.0) * rect.width, rect.y + (float)((p.z / extent + 1.0) / 2.0) * rect.height);
            if (here.galaxy == viewedGalaxy)
            {
                StarSystemId home = U.HomeSystemId;
                Vector2 hp = Project(U.PositionOf(home));
                GUI.color = new Color(1f, 1f, 1f, 0.9f);
                Widgets.DrawBox(new Rect(hp.x - 6f, hp.y - 6f, 12f, 12f));
                Vector2 cp = Project(U.PositionOf(here));
                GUI.color = Color.green;
                GUI.DrawTexture(new Rect(cp.x - 3f, cp.y - 3f, 6f, 6f), BaseContent.WhiteTex);
                Widgets.DrawBox(new Rect(cp.x - 9f, cp.y - 9f, 18f, 18f));
            }
            GUI.color = Color.white;
            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            Widgets.Label(new Rect(rect.x + 6f, rect.yMax - 20f, rect.width, 20f), "Rimverse_GalaxyScale".Translate((g.radiusLy * 2.0).ToString("N0")));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
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
