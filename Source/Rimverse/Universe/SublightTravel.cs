using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public static class SublightTravel
    {
        public const float MinSpeedAUPerDay = 0.2f;
        public const float FuelPerAUPer100t = 2f;

        public static float SpeedAUPerDay(RimverseShip ship) => ship.mass <= 0f ? MinSpeedAUPerDay : Mathf.Max(ship.thrust / ship.mass, MinSpeedAUPerDay);

        public static float FuelCost(RimverseShip ship, float au) => FuelPerAUPer100t * au * ship.mass / 100f * RimverseMod.Settings.fuelConsumptionMultiplier;

        public static float TravelDays(RimverseShip ship, float au) => au / SpeedAUPerDay(ship);

        public static List<string> Problems(RimverseShip ship, MapComponent_ShipLocation loc, int dest, out float au, out float fuel)
        {
            List<string> problems = new List<string>();
            au = loc.System?.DistanceAU(loc.bodyIndex, dest) ?? 0f;
            fuel = FuelCost(ship, au);
            if (!SpaceMapUtility.IsRimverseSpace(ship.map)) problems.Add("Rimverse_ProblemNotInSpace".Translate());
            if (loc.Traveling) problems.Add("Rimverse_ProblemAlreadyTraveling".Translate());
            if (dest == loc.bodyIndex) problems.Add("Rimverse_ProblemAlreadyThere".Translate());
            if (ship.CountRole(ShipPartRole.Thruster, true) == 0) problems.Add("Rimverse_ProblemNoThruster".Translate());
            if (ship.multipleBridges) problems.Add("Rimverse_ProblemMultipleBridges".Translate());
            float have = ShipLaunchUtility.FuelAvailable(ship);
            if (have < fuel) problems.Add("Rimverse_ProblemFuel".Translate(have.ToString("0"), fuel.ToString("0")));
            ShipCrewUtility.PilotAndCopilot(ship, out Pawn pilot, out _);
            if (!ShipCrewUtility.HasHelm(ship, pilot)) problems.Add("Rimverse_ProblemNoPilot".Translate());
            return problems;
        }

        public static bool TryStart(RimverseShip ship, int dest)
        {
            MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(ship.map);
            if (loc == null) return false;
            List<string> problems = Problems(ship, loc, dest, out float au, out float fuel);
            if (problems.Count > 0)
            {
                Messages.Message("Rimverse_CannotTravel".Translate() + "\n- " + string.Join("\n- ", problems), ship.bridge, MessageTypeDefOf.RejectInput, false);
                return false;
            }
            ShipLaunchUtility.ConsumeFuel(ship, fuel);
            int now = Find.TickManager.TicksGame;
            loc.destinationBody = dest;
            loc.departTick = now;
            loc.arriveTick = now + Mathf.Max(RimverseUnits.TicksPerHour, RimverseUnits.TicksFromDays(TravelDays(ship, au)));
            loc.travelAU = au;
            loc.travelingShipId = ship.id;
            ship.state.thrusting = true;
            ship.map.GetComponent<MapComponent_RimverseShips>().Dirty();
            Messages.Message("Rimverse_MsgTravelStarted".Translate(ship.Label, loc.System.Body(dest).name, RimverseUnits.DaysFromTicks(loc.arriveTick - now).ToString("0.#")), ship.bridge, MessageTypeDefOf.TaskCompletion, false);
            return true;
        }
    }
}
