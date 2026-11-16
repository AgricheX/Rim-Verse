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
            Widgets.DrawBoxSolid(mapRect, new Color(0.02f, 0.02f, 0.05f));
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
            return maxRadius * Mathf.Log10(1f + au * 10f) / Mathf.Log10(1f + maxAu * 10f);
        }

        private void DrawSystem(Rect rect, StarSystemData sys, MapComponent_ShipLocation loc)
        {
            Vector2 center = rect.center;
            float maxR = Mathf.Min(rect.width, rect.height) * 0.47f;
            float maxAu = MaxOrbit(sys);
            GUI.color = new Color(1f, 0.9f, 0.5f);
            Widgets.DrawTextureFitted(new Rect(center.x - 9f, center.y - 9f, 18f, 18f), BaseContent.WhiteTex, 1f);
            Dictionary<int, Vector2> positions = new Dictionary<int, Vector2>();
            foreach (BodyData b in sys.bodies)
            {
                if (b.IsMoon) continue;
                float r = RadiusFor(b.orbitAU, maxAu, maxR);
                GUI.color = OrbitColor;
                DrawCircle(center, r, b.type == BodyType.Belt ? 3f : 1f);
                float angle = (b.index * 2.39996f) % (Mathf.PI * 2f);
                Vector2 pos = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
                positions[b.index] = pos;
            }
            foreach (BodyData b in sys.bodies)
            {
                if (!b.IsMoon) continue;
                Vector2 parent = positions[b.parent];
                float angle = b.moonOrbitFactor * 1.7f;
                positions[b.index] = parent + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (8f + 6f * b.moonOrbitFactor);
            }
            foreach (BodyData b in sys.bodies)
            {
                Vector2 pos = positions[b.index];
                float size = b.type == BodyType.Belt ? 6f : Mathf.Lerp(4f, 14f, b.sizeClass / 100f);
                if (b.IsMoon) size *= 0.7f;
                Rect dot = new Rect(pos.x - size / 2f, pos.y - size / 2f, size, size);
                GUI.color = BodyColor(b);
                GUI.DrawTexture(dot, BaseContent.WhiteTex);
                if (b.index == selected)
                {
                    GUI.color = SelectColor;
                    Widgets.DrawBox(dot.ExpandedBy(3f));
                }
                if (b.index == loc.bodyIndex || b.index == loc.destinationBody)
                {
                    GUI.color = b.index == loc.bodyIndex ? Color.green : Color.cyan;
                    Widgets.DrawBox(dot.ExpandedBy(6f));
                }
                GUI.color = Color.white;
                Rect click = dot.ExpandedBy(6f);
                if (Mouse.IsOver(click))
                {
                    TooltipHandler.TipRegion(click, BodyName(sys, b));
                }
                if (Widgets.ButtonInvisible(click)) selected = b.index;
            }
            DrawMegastructures(sys, center, positions);
            GUI.color = Color.white;
        }

        private void DrawMegastructures(StarSystemData sys, Vector2 center, Dictionary<int, Vector2> positions)
        {
            GameComponent_Megastructures gc = GameComponent_Megastructures.Instance;
            if (gc == null) return;
            int n = 0;
            foreach (MegastructureData m in gc.In(sys.id.Key))
            {
                Vector2 anchor = m.body >= 0 && positions.TryGetValue(m.body, out Vector2 p) ? p : center;
                float a = m.angle * Mathf.Deg2Rad + n * 0.9f;
                float dist = m.body >= 0 ? 16f : 26f;
                Vector2 pos = anchor + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * dist;
                Rect r = new Rect(pos.x - 9f, pos.y - 9f, 18f, 18f);
                GUI.color = m.stage <= 0 ? new Color(1f, 1f, 1f, 0.35f) : m.Complete ? Color.white : new Color(1f, 1f, 1f, 0.75f);
                GUI.DrawTexture(r, MegastructureRender.Icon(m));
                GUI.color = Color.white;
                if (Mouse.IsOver(r.ExpandedBy(3f))) TooltipHandler.TipRegion(r.ExpandedBy(3f), m.Label + "\n" + "Rimverse_MegaTip".Translate(m.OwnerLabel, m.StageLabel, m.Completion.ToStringPercent()));
                n++;
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
