using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public static class ShipLaunchUtility
    {
        public const float LaunchFuelPerTon = 0.5f;
        public const float LandingFuelFactor = 0.25f;
        public const float AirHoursPerCrew = 12f;
        public static readonly IntVec3 OrbitMapSize = new IntVec3(150, 1, 150);

        public static float Gravity(Map map)
        {
            if (map == null) return 1f;
            if (SpaceMapUtility.IsOrbit(map)) return 0f;
            MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(map);
            BodyData body = loc?.Body;
            if (body != null) return body.gravity;
            return 1f;
        }

        public static float FuelAvailable(RimverseShip ship)
        {
            float total = 0f;
            foreach (CompShipPart tank in ship.PartsWithRole(ShipPartRole.FuelTank))
            {
                CompRefuelable fuel = tank.parent.TryGetComp<CompRefuelable>();
                if (fuel != null) total += fuel.Fuel;
            }
            return total;
        }

        public static void ConsumeFuel(RimverseShip ship, float amount)
        {
            foreach (CompShipPart tank in ship.PartsWithRole(ShipPartRole.FuelTank))
            {
                if (amount <= 0f) return;
                CompRefuelable fuel = tank.parent.TryGetComp<CompRefuelable>();
                if (fuel == null || fuel.Fuel <= 0f) continue;
                float take = Mathf.Min(fuel.Fuel, amount);
                fuel.ConsumeFuel(take);
                amount -= take;
            }
        }

        public static float LaunchFuelCost(RimverseShip ship)
        {
            return ship.mass * Gravity(ship.map) * LaunchFuelPerTon * RimverseMod.Settings.fuelConsumptionMultiplier;
        }

        public static float LandingFuelCost(RimverseShip ship, Map target)
        {
            return ship.mass * Mathf.Max(0.1f, Gravity(target)) * LaunchFuelPerTon * LandingFuelFactor * RimverseMod.Settings.fuelConsumptionMultiplier;
        }

        public static List<string> LaunchProblems(RimverseShip ship, float fuelNeeded, bool checkThrust = true)
        {
            List<string> problems = new List<string>();
            if (ship.multipleBridges) problems.Add("Rimverse_ProblemMultipleBridges".Translate());
            if (ship.overLimit) problems.Add("Rimverse_ProblemOverLimit".Translate(ship.cellLimit));
            CompShipPart bridgePart = ship.bridge.TryGetComp<CompShipPart>();
            if (bridgePart == null || !bridgePart.Operating) problems.Add("Rimverse_ProblemBridgeOffline".Translate());
            if (ship.CountRole(ShipPartRole.Thruster, true) == 0) problems.Add("Rimverse_ProblemNoThruster".Translate());
            float gravity = Gravity(ship.map);
            if (checkThrust && gravity > 0f && ship.thrust < ship.mass * gravity) problems.Add("Rimverse_ProblemThrust".Translate(ship.thrust.ToString("0"), (ship.mass * gravity).ToString("0")));
            float fuel = FuelAvailable(ship);
            if (fuel < fuelNeeded) problems.Add("Rimverse_ProblemFuel".Translate(fuel.ToString("0"), fuelNeeded.ToString("0")));
            int crew = ShipCrewUtility.Crew(ship).Count(SpaceMapUtility.Breathes);
            if (crew > 0 && ship.airCapacity < crew * AirHoursPerCrew) problems.Add("Rimverse_ProblemAir".Translate(ship.airCapacity.ToString("0"), (crew * AirHoursPerCrew).ToString("0")));
            ShipCrewUtility.PilotAndCopilot(ship, out Pawn pilot, out _);
            if (pilot == null) problems.Add("Rimverse_ProblemNoPilot".Translate());
            return problems;
        }

        public static float LaunchQuality(RimverseShip ship, Pawn pilot, Pawn copilot)
        {
            float q = 0.3f;
            if (pilot != null) q += 0.025f * ShipCrewUtility.PilotLevel(pilot);
            if (copilot != null) q += 0.0125f * ShipCrewUtility.PilotLevel(copilot);
            if (ship.mass > 0f) q += 0.1f * (Mathf.Min(ship.thrust / ship.mass, 3f) - 1f);
            return Mathf.Clamp(q, 0.05f, 0.98f);
        }

        public static void TryLaunchToOrbit(RimverseShip ship)
        {
            float fuel = LaunchFuelCost(ship);
            List<string> problems = LaunchProblems(ship, fuel);
            if (problems.Count > 0)
            {
                Messages.Message("Rimverse_CannotLaunch".Translate() + "\n- " + string.Join("\n- ", problems), ship.bridge, MessageTypeDefOf.RejectInput, false);
                return;
            }
            MapComponent_ShipLocation srcLocation = MapComponent_ShipLocation.For(ship.map);
            BodyData fromBody = srcLocation?.Body;
            string name = ship.Label;
            Map orbit = fromBody == null || fromBody.homeWorld ? OrbitUtility.CreateOrbitMap(ship.map.Tile, name, OrbitMapSize) : BodyMapUtility.CreateOrbitPocket(ship.map, OrbitMapSize);
            if (orbit == null)
            {
                Messages.Message("Rimverse_NoOrbitTile".Translate(), ship.bridge, MessageTypeDefOf.RejectInput, false);
                return;
            }
            MapComponent_ShipLocation srcLoc = MapComponent_ShipLocation.For(ship.map);
            MapComponent_ShipLocation dstLoc = MapComponent_ShipLocation.For(orbit);
            if (srcLoc != null && dstLoc != null) dstLoc.SetLocation(srcLoc.SystemId, srcLoc.bodyIndex);
            ShipCrewUtility.PilotAndCopilot(ship, out Pawn pilot, out Pawn copilot);
            float quality = LaunchQuality(ship, pilot, copilot);
            ConsumeFuel(ship, fuel);
            CellRect bounds = ShipMover.Bounds(ship);
            IntVec3 offset = orbit.Center - bounds.CenterCell;
            Building bridge = ship.bridge;
            Map launchSource = ship.map;
            ShipMover.Move(ship, orbit, offset);
            RimverseShip moved = orbit.GetComponent<MapComponent_RimverseShips>().ShipById(bridge.thingIDNumber);
            ApplyQuality(moved, quality, "Rimverse_LetterLaunch");
            BodyMapUtility.CleanupIfEmpty(launchSource);
            pilot?.skills?.Learn(SkillDefOf.Intellectual, 400f);
            CameraJumper.TryJump(bridge);
        }

        public static Map LandingMap(Map orbitMap, out PlanetTile surfaceTile)
        {
            surfaceTile = PlanetTile.Invalid;
            if (orbitMap.Parent is ShipOrbitMapParent parent && parent.surfaceTile.Valid) surfaceTile = parent.surfaceTile;
            else surfaceTile = OrbitUtility.SurfaceTileBelow(orbitMap.Tile);
            if (!surfaceTile.Valid) return null;
            MapParent existing = Find.WorldObjects.MapParentAt(surfaceTile);
            return existing != null && existing.HasMap ? existing.Map : null;
        }

        public static void TryLand(RimverseShip ship)
        {
            MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(ship.map);
            BodyData here = loc?.Body;
            if (loc != null && loc.Traveling)
            {
                Messages.Message("Rimverse_ProblemAlreadyTraveling".Translate(), ship.bridge, MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (here != null && !here.homeWorld)
            {
                TryLandOnBody(ship, loc, here);
                return;
            }
            Map target = LandingMap(ship.map, out PlanetTile surfaceTile);
            float fuel = LandingFuelCost(ship, target);
            List<string> problems = LaunchProblems(ship, fuel, false);
            if (problems.Count > 0)
            {
                Messages.Message("Rimverse_CannotLand".Translate() + "\n- " + string.Join("\n- ", problems), ship.bridge, MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (target == null)
            {
                if (!surfaceTile.Valid || Find.WorldObjects.AnyWorldObjectAt(surfaceTile))
                {
                    Messages.Message("Rimverse_NoLandingTile".Translate(), ship.bridge, MessageTypeDefOf.RejectInput, false);
                    return;
                }
                SettleUtility.AddNewHome(surfaceTile, Faction.OfPlayer);
                target = GetOrGenerateMapUtility.GetOrGenerateMap(surfaceTile, Find.World.info.initialMapSize, null);
            }
            if (!ShipMover.TryFindLandingOffset(target, ship, out IntVec3 offset))
            {
                Messages.Message("Rimverse_NoLandingSpot".Translate(), ship.bridge, MessageTypeDefOf.RejectInput, false);
                return;
            }
            ShipCrewUtility.PilotAndCopilot(ship, out Pawn pilot, out Pawn copilot);
            float quality = LaunchQuality(ship, pilot, copilot);
            ConsumeFuel(ship, fuel);
            Building bridge = ship.bridge;
            ShipMover.Move(ship, target, offset);
            RimverseShip moved = target.GetComponent<MapComponent_RimverseShips>().ShipById(bridge.thingIDNumber);
            ApplyQuality(moved, quality, "Rimverse_LetterLanding");
            CameraJumper.TryJump(bridge);
        }

        public static void TryLandOnBody(RimverseShip ship, MapComponent_ShipLocation loc, BodyData body)
        {
            if (!body.LandableNow)
            {
                Messages.Message("Rimverse_BodyNotLandableNow".Translate(body.name), ship.bridge, MessageTypeDefOf.RejectInput, false);
                return;
            }
            float fuel = ship.mass * Mathf.Max(0.1f, body.gravity) * LaunchFuelPerTon * LandingFuelFactor * RimverseMod.Settings.fuelConsumptionMultiplier;
            List<string> problems = LaunchProblems(ship, fuel, false);
            if (problems.Count > 0)
            {
                Messages.Message("Rimverse_CannotLand".Translate() + "\n- " + string.Join("\n- ", problems), ship.bridge, MessageTypeDefOf.RejectInput, false);
                return;
            }
            Map target = BodyMapUtility.GetOrCreateBodyMap(ship.map, loc.SystemId, body);
            if (target == null || !ShipMover.TryFindLandingOffset(target, ship, out IntVec3 offset))
            {
                Messages.Message("Rimverse_NoLandingSpot".Translate(), ship.bridge, MessageTypeDefOf.RejectInput, false);
                return;
            }
            ShipCrewUtility.PilotAndCopilot(ship, out Pawn pilot, out Pawn copilot);
            float quality = LaunchQuality(ship, pilot, copilot);
            ConsumeFuel(ship, fuel);
            Building bridge = ship.bridge;
            Map source = ship.map;
            ShipMover.Move(ship, target, offset);
            RimverseShip moved = target.GetComponent<MapComponent_RimverseShips>().ShipById(bridge.thingIDNumber);
            ApplyQuality(moved, quality, "Rimverse_LetterLanding");
            BodyMapUtility.CleanupIfEmpty(source);
            GameComponent_Universe.Instance?.SetScan(loc.SystemId, body.index, ScanLevel.Exact);
            CameraJumper.TryJump(bridge);
        }

        public static void ApplyQuality(RimverseShip ship, float quality, string letterKey)
        {
            if (ship == null) return;
            string qualityText = quality.ToStringPercent();
            if (Rand.Chance(quality))
            {
                Messages.Message((letterKey + "_Good").Translate(ship.Label, qualityText), ship.bridge, MessageTypeDefOf.PositiveEvent, false);
                return;
            }
            List<string> lines = new List<string>();
            int accidents = quality < 0.3f ? 2 : 1;
            for (int i = 0; i < accidents; i++)
            {
                switch (Rand.RangeInclusive(0, 3))
                {
                    case 0:
                        foreach (CompShipPart part in ship.parts.InRandomOrder().Take(3))
                        {
                            part.parent.TakeDamage(new DamageInfo(DamageDefOf.Blunt, Rand.Range(30, 80)));
                        }
                        lines.Add("Rimverse_AccidentDamage".Translate());
                        break;
                    case 1:
                        CompShipPart thruster = ship.PartsWithRole(ShipPartRole.Thruster).RandomElementWithFallback();
                        if (thruster != null)
                        {
                            CompBreakdownable bd = thruster.parent.TryGetComp<CompBreakdownable>();
                            if (bd != null) bd.DoBreakdown();
                            else thruster.parent.TakeDamage(new DamageInfo(DamageDefOf.Blunt, thruster.parent.MaxHitPoints * 0.5f));
                            lines.Add("Rimverse_AccidentThruster".Translate(thruster.parent.LabelShort));
                        }
                        break;
                    case 2:
                        ship.state.heat += ship.heatCapacity * 0.4f;
                        lines.Add("Rimverse_AccidentHeat".Translate());
                        break;
                    default:
                        foreach (Pawn p in ShipCrewUtility.Crew(ship).Where(p => p.RaceProps.Humanlike))
                        {
                            HealthUtility.AdjustSeverity(p, RimverseDefOf.Rimverse_LaunchSickness, 0.6f);
                        }
                        lines.Add("Rimverse_AccidentSickness".Translate());
                        break;
                }
            }
            Find.LetterStack.ReceiveLetter((letterKey + "_BadLabel").Translate(), (letterKey + "_Bad").Translate(ship.Label, qualityText) + "\n\n" + string.Join("\n", lines), LetterDefOf.NegativeEvent, ship.bridge);
        }
    }
}
