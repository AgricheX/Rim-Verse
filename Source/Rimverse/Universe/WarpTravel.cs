using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public static class WarpTravel
    {
        public const float MatterPerLyPer100t = 1f;
        public const float HeatFactor = 1.4f;
        public const float JumpDays = 1f;

        public static float MatterAvailable(RimverseShip ship)
        {
            float total = 0f;
            foreach (CompShipPart tank in ship.PartsWithRole(ShipPartRole.WarpTank))
            {
                CompRefuelable fuel = tank.parent.TryGetComp<CompRefuelable>();
                if (fuel != null) total += fuel.Fuel;
            }
            return total;
        }

        public static void ConsumeMatter(RimverseShip ship, float amount)
        {
            foreach (CompShipPart tank in ship.PartsWithRole(ShipPartRole.WarpTank))
            {
                if (amount <= 0f) return;
                CompRefuelable fuel = tank.parent.TryGetComp<CompRefuelable>();
                if (fuel == null || fuel.Fuel <= 0f) continue;
                float take = Mathf.Min(fuel.Fuel, amount);
                fuel.ConsumeFuel(take);
                amount -= take;
            }
        }

        public static float MatterCost(RimverseShip ship, double ly) => (float)(MatterPerLyPer100t * ly * ship.mass / 100.0) * RimverseMod.Settings.fuelConsumptionMultiplier;

        public static bool BarRoute(StarSystemId a, StarSystemId b)
        {
            GameComponent_Universe u = GameComponent_Universe.Instance;
            if (u == null || a.galaxy != b.galaxy) return false;
            GalaxyData g = u.Galaxy(a.galaxy);
            return g != null && g.rule == TravelRule.WarpBarRoutes && ResearchGate.Done("Rimverse_BarRouteCharts") && g.InBar(u.PositionOf(a)) && g.InBar(u.PositionOf(b));
        }

        public static float DaysFor(StarSystemId a, StarSystemId b) => BarRoute(a, b) ? JumpDays * 0.5f : JumpDays;

        public static int CooldownTicksLeft(RimverseShip ship)
        {
            int end = ship.state.lastWarpTick + ship.warpCooldownHours * RimverseUnits.TicksPerHour;
            return Mathf.Max(0, end - Find.TickManager.TicksGame);
        }

        public static bool RuleAllowsWarp(TravelRule rule) => rule == TravelRule.Warp || rule == TravelRule.WarpBarRoutes;

        public static List<string> Problems(RimverseShip ship, MapComponent_ShipLocation loc, StarSystemId dest, out double ly, out float matter)
        {
            List<string> problems = new List<string>();
            GameComponent_Universe u = GameComponent_Universe.Instance;
            StarSystemId here = loc.SystemId;
            ly = u.DistanceLy(here, dest);
            bool bar = BarRoute(here, dest);
            matter = double.IsInfinity(ly) ? 0f : MatterCost(ship, ly) * (bar ? 0.5f : 1f);
            int range = bar ? ship.warpRangeLy * 2 : ship.warpRangeLy;
            if (!SpaceMapUtility.IsRimverseSpace(ship.map)) problems.Add("Rimverse_ProblemNotInSpace".Translate());
            if (loc.Traveling) problems.Add("Rimverse_ProblemAlreadyTraveling".Translate());
            if (dest == here) problems.Add("Rimverse_ProblemAlreadyThere".Translate());
            if (dest.galaxy != here.galaxy) problems.Add("Rimverse_ProblemOtherGalaxy".Translate());
            else if (!RuleAllowsWarp(u.Galaxy(here.galaxy).rule)) problems.Add("Rimverse_ProblemTravelRule".Translate(("Rimverse_TravelRule_" + u.Galaxy(here.galaxy).rule).Translate()));
            if (ship.multipleBridges) problems.Add("Rimverse_ProblemMultipleBridges".Translate());
            if (ship.warpRangeLy > 0 && !ShipClassifier.CanInterstellarWarp(ship.shipClass)) problems.Add("Rimverse_ProblemWarpClass".Translate(("Rimverse_Class_" + ship.shipClass).Translate()));
            if (ship.warpRangeLy <= 0) problems.Add("Rimverse_ProblemNoWarpDrive".Translate());
            else
            {
                if (ly > range) problems.Add("Rimverse_ProblemWarpRange".Translate(ly.ToString("0.#"), range));
                if (ship.mass > ship.warpMaxMass) problems.Add("Rimverse_ProblemWarpMass".Translate(ship.mass.ToString("0"), ship.warpMaxMass.ToString("0")));
                int cd = CooldownTicksLeft(ship);
                if (cd > 0) problems.Add("Rimverse_ProblemWarpCooldown".Translate((cd / (float)RimverseUnits.TicksPerHour).ToString("0.#")));
            }
            float have = MatterAvailable(ship);
            if (have < matter) problems.Add("Rimverse_ProblemWarpMatter".Translate(have.ToString("0"), matter.ToString("0")));
            ShipCrewUtility.PilotAndCopilot(ship, out Pawn pilot, out _);
            if (!ShipCrewUtility.HasHelm(ship, pilot)) problems.Add("Rimverse_ProblemNoPilot".Translate());
            return problems;
        }

        public static bool TryStart(RimverseShip ship, StarSystemId dest)
        {
            MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(ship.map);
            if (loc == null) return false;
            List<string> problems = Problems(ship, loc, dest, out double ly, out float matter);
            if (problems.Count > 0)
            {
                Messages.Message("Rimverse_CannotWarp".Translate() + "\n- " + string.Join("\n- ", problems), ship.bridge, MessageTypeDefOf.RejectInput, false);
                return false;
            }
            ConsumeMatter(ship, matter);
            int now = Find.TickManager.TicksGame;
            loc.destinationSystemKey = dest.Key;
            loc.destinationBody = -1;
            loc.departTick = now;
            loc.arriveTick = now + RimverseUnits.TicksFromDays(DaysFor(loc.SystemId, dest));
            loc.travelAU = 0f;
            loc.travelingShipId = ship.id;
            loc.travelMode = InterstellarMode.Warp;
            loc.travelYears = 0f;
            ship.state.thrusting = true;
            ship.state.warping = true;
            ship.map.GetComponent<MapComponent_RimverseShips>().Dirty();
            StarSystemData target = GameComponent_Universe.Instance.GetSystem(dest);
            Messages.Message("Rimverse_MsgWarpStarted".Translate(ship.Label, target.name, ly.ToString("0.#")), ship.bridge, MessageTypeDefOf.TaskCompletion, false);
            return true;
        }

        public static List<string> LaneProblems(RimverseShip ship, MapComponent_ShipLocation loc, StarSystemId dest, out double ly)
        {
            List<string> problems = new List<string>();
            GameComponent_Universe u = GameComponent_Universe.Instance;
            StarSystemId here = loc.SystemId;
            ly = u.DistanceLy(here, dest);
            if (!SpaceMapUtility.IsRimverseSpace(ship.map)) problems.Add("Rimverse_ProblemNotInSpace".Translate());
            if (loc.Traveling) problems.Add("Rimverse_ProblemAlreadyTraveling".Translate());
            if (dest == here) problems.Add("Rimverse_ProblemAlreadyThere".Translate());
            if (!Hyperlanes.GalaxyHasLanes(u, here.galaxy)) problems.Add("Rimverse_ProblemNoLanes".Translate());
            else if (!Hyperlanes.Connected(u, here, dest)) problems.Add("Rimverse_ProblemNotOnLane".Translate());
            if (!ResearchGate.Done("Rimverse_HyperlaneNavigation")) problems.Add(ResearchGate.Need("Rimverse_HyperlaneNavigation"));
            if (!ShipClassifier.CanUseHyperlanes(ship.shipClass)) problems.Add("Rimverse_ProblemLaneClass".Translate(("Rimverse_Class_" + ship.shipClass).Translate()));
            if (ship.multipleBridges) problems.Add("Rimverse_ProblemMultipleBridges".Translate());
            ShipCrewUtility.PilotAndCopilot(ship, out Pawn pilot, out _);
            if (!ShipCrewUtility.HasHelm(ship, pilot)) problems.Add("Rimverse_ProblemNoPilot".Translate());
            return problems;
        }

        public static bool TryStartLane(RimverseShip ship, StarSystemId dest)
        {
            MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(ship.map);
            if (loc == null) return false;
            List<string> problems = LaneProblems(ship, loc, dest, out double ly);
            if (problems.Count > 0)
            {
                Messages.Message("Rimverse_CannotTravel".Translate() + "\n- " + string.Join("\n- ", problems), ship.bridge, MessageTypeDefOf.RejectInput, false);
                return false;
            }
            int now = Find.TickManager.TicksGame;
            loc.destinationSystemKey = dest.Key;
            loc.destinationBody = -1;
            loc.departTick = now;
            loc.arriveTick = now + RimverseUnits.TicksFromDays(Hyperlanes.Days(ly));
            loc.travelAU = 0f;
            loc.travelingShipId = ship.id;
            loc.travelMode = InterstellarMode.Hyperlane;
            loc.travelYears = 0f;
            ship.state.thrusting = true;
            ship.map.GetComponent<MapComponent_RimverseShips>().Dirty();
            StarSystemData target = GameComponent_Universe.Instance.GetSystem(dest);
            Messages.Message("Rimverse_MsgLaneStarted".Translate(ship.Label, target.name, Hyperlanes.Days(ly).ToString("0.#")), ship.bridge, MessageTypeDefOf.TaskCompletion, false);
            return true;
        }

        public static int ArrivalBody(StarSystemData sys)
        {
            int best = 0;
            float bestOrbit = -1f;
            foreach (BodyData b in sys.bodies)
            {
                if (b.IsMoon) continue;
                if (b.orbitAU > bestOrbit)
                {
                    bestOrbit = b.orbitAU;
                    best = b.index;
                }
            }
            return best;
        }

        public static void CheckArrivals()
        {
            List<Map> maps = Find.Maps;
            for (int i = maps.Count - 1; i >= 0; i--)
            {
                MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(maps[i]);
                if (loc != null && loc.WarpArrivalDue) Arrive(maps[i], loc);
            }
        }

        public static void Arrive(Map map, MapComponent_ShipLocation loc)
        {
            GameComponent_Universe u = GameComponent_Universe.Instance;
            StarSystemId dest = StarSystemId.FromKey(loc.destinationSystemKey);
            int shipId = loc.travelingShipId;
            InterstellarMode mode = loc.travelMode;
            float years = loc.travelYears;
            loc.travelMode = InterstellarMode.Warp;
            loc.travelYears = 0f;
            StarSystemId origin = loc.SystemId;
            int originBody = loc.bodyIndex;
            loc.SetLocation(origin, originBody);
            loc.travelingShipId = -1;
            StarSystemData sys = u.GetSystem(dest);
            if (sys == null) return;
            int body = ArrivalBody(sys);
            MapComponent_RimverseShips comp = map.GetComponent<MapComponent_RimverseShips>();
            RimverseShip ship = comp?.ShipById(shipId);
            if (ship == null || ship.bridge == null)
            {
                loc.SetLocation(dest, body);
                return;
            }
            ship.state.warping = false;
            ship.state.thrusting = false;
            if (mode == InterstellarMode.Warp) ship.state.lastWarpTick = Find.TickManager.TicksGame;
            comp.Dirty();
            Map target = BodyMapUtility.CreateOrbitPocket(map, ShipLaunchUtility.OrbitMapSize);
            MapComponent_ShipLocation dstLoc = MapComponent_ShipLocation.For(target);
            dstLoc.SetLocation(dest, body);
            dstLoc.SetSurface(false);
            CellRect bounds = ShipMover.Bounds(ship);
            IntVec3 offset = target.Center - bounds.CenterCell;
            Building bridge = ship.bridge;
            string label = ship.Label;
            ShipMover.Move(ship, target, offset);
            BodyMapUtility.CleanupIfEmpty(map);
            u.SetSystemScan(dest, ScanLevel.Range);
            if (mode == InterstellarMode.Warp) LifeAboard.WarpSickness(target);
            RimverseShip moved = target.GetComponent<MapComponent_RimverseShips>().ShipById(bridge.thingIDNumber);
            if (moved != null && moved.sensorSensitivity > 0f && moved.sensorSensitivity <= 100f) u.SetSystemScan(dest, ScanLevel.Exact);
            Find.LetterStack.ReceiveLetter("Rimverse_LetterWarpArrivedLabel".Translate(), "Rimverse_LetterWarpArrived".Translate(label, sys.name, sys.Body(body).name), LetterDefOf.NeutralEvent, bridge);
            AnomalyContent.OnWarpArrived(target, bridge, sys);
            if (mode == InterstellarMode.Cryosleep) CryoTravel.OnArrived(target, years);
        }
    }
}
