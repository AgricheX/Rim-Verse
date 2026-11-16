using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public enum InterstellarMode
    {
        Warp,
        Hyperlane,
        Cryosleep
    }

    public static class Hyperlanes
    {
        public const double MaxLaneLy = 24;
        public const int LanesPerStar = 3;
        public const float LyPerDay = 12f;
        private static readonly Dictionary<string, List<StarSystemId>> nearest = new Dictionary<string, List<StarSystemId>>();
        private static readonly Dictionary<string, List<StarSystemId>> lanes = new Dictionary<string, List<StarSystemId>>();
        private static int cacheSeed = int.MinValue;

        private static void CheckCache(GameComponent_Universe u)
        {
            if (cacheSeed == u.universeSeed && nearest.Count < 6000) return;
            nearest.Clear();
            lanes.Clear();
            cacheSeed = u.universeSeed;
        }

        public static bool GalaxyHasLanes(GameComponent_Universe u, int galaxy) => u.Galaxy(galaxy)?.rule == TravelRule.Hyperlanes;

        private static List<StarSystemId> Candidates(GameComponent_Universe u, StarSystemId id, int count)
        {
            Vec3d p = u.PositionOf(id);
            return GalaxyQuery.StarsInRadius(u, id.galaxy, p, MaxLaneLy).Where(e => e.id != id).OrderBy(e => Vec3d.Distance(e.position, p)).Take(count).Select(e => e.id).ToList();
        }

        public static List<StarSystemId> Nearest(GameComponent_Universe u, StarSystemId id)
        {
            CheckCache(u);
            if (nearest.TryGetValue(id.Key, out List<StarSystemId> list)) return list;
            list = Candidates(u, id, LanesPerStar);
            nearest[id.Key] = list;
            return list;
        }

        public static bool TryCachedNearest(StarSystemId id, out List<StarSystemId> list) => nearest.TryGetValue(id.Key, out list);

        public static List<StarSystemId> LanesFrom(GameComponent_Universe u, StarSystemId id)
        {
            CheckCache(u);
            if (lanes.TryGetValue(id.Key, out List<StarSystemId> list)) return list;
            list = new List<StarSystemId>(Nearest(u, id));
            foreach (StarSystemId other in Candidates(u, id, LanesPerStar * 4))
            {
                if (list.Contains(other)) continue;
                if (Nearest(u, other).Contains(id)) list.Add(other);
            }
            lanes[id.Key] = list;
            return list;
        }

        public static bool Connected(GameComponent_Universe u, StarSystemId a, StarSystemId b) => a.galaxy == b.galaxy && LanesFrom(u, a).Contains(b);

        public static float Days(double ly) => Mathf.Max(1f, (float)ly / LyPerDay);
    }

    public static class CryoTravel
    {
        public const double MaxLy = 6;
        public const double SpeedC = 0.1;
        public const float FuelPerLyPer100t = 30f;
        public const float TransitDays = 2f;

        public static float Years(double ly) => (float)(ly / SpeedC);

        public static float BaseFuelCost(RimverseShip ship, double ly) => (float)(FuelPerLyPer100t * ly * ship.mass / 100.0) * RimverseMod.Settings.fuelConsumptionMultiplier;

        public static float FuelCost(RimverseShip ship, double ly) => BaseFuelCost(ship, ly) * ThrusterFuel.Factor(ship);

        public static List<Pawn> AwakeColonists(Map map) => map.mapPawns.FreeColonistsSpawned.Where(p => !p.Dead).ToList();

        public static List<string> Problems(RimverseShip ship, MapComponent_ShipLocation loc, StarSystemId dest, out double ly, out float fuel)
        {
            List<string> problems = new List<string>();
            GameComponent_Universe u = GameComponent_Universe.Instance;
            StarSystemId here = loc.SystemId;
            ly = u.DistanceLy(here, dest);
            fuel = double.IsInfinity(ly) ? 0f : FuelCost(ship, ly);
            if (!SpaceMapUtility.IsRimverseSpace(ship.map)) problems.Add("Rimverse_ProblemNotInSpace".Translate());
            if (loc.Traveling) problems.Add("Rimverse_ProblemAlreadyTraveling".Translate());
            if (dest == here) problems.Add("Rimverse_ProblemAlreadyThere".Translate());
            if (dest.galaxy != here.galaxy) problems.Add("Rimverse_ProblemOtherGalaxy".Translate());
            else if (ly > MaxLy) problems.Add("Rimverse_ProblemCryoRange".Translate(ly.ToString("0.#"), MaxLy.ToString("0")));
            if (!ResearchGate.Done("Rimverse_CryoCrossings")) problems.Add(ResearchGate.Need("Rimverse_CryoCrossings"));
            if (ship.multipleBridges) problems.Add("Rimverse_ProblemMultipleBridges".Translate());
            if (ship.CountRole(ShipPartRole.Thruster, true) == 0) problems.Add("Rimverse_ProblemNoThruster".Translate());
            float have = ShipLaunchUtility.FuelAvailable(ship);
            if (have < fuel) problems.Add("Rimverse_ProblemFuel".Translate(have.ToString("0"), fuel.ToString("0")));
            int awake = AwakeColonists(ship.map).Count;
            if (awake > 0) problems.Add("Rimverse_ProblemCryoAwake".Translate(awake, Years(ly).ToString("0")));
            return problems;
        }

        public static bool TryStart(RimverseShip ship, StarSystemId dest)
        {
            MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(ship.map);
            if (loc == null) return false;
            List<string> problems = Problems(ship, loc, dest, out double ly, out float fuel);
            if (problems.Count > 0)
            {
                Messages.Message("Rimverse_CannotTravel".Translate() + "\n- " + string.Join("\n- ", problems), ship.bridge, MessageTypeDefOf.RejectInput, false);
                return false;
            }
            ThrusterFuel.BurnFusionGas(ship, BaseFuelCost(ship, ly));
            ShipLaunchUtility.ConsumeFuel(ship, fuel);
            int now = Find.TickManager.TicksGame;
            loc.destinationSystemKey = dest.Key;
            loc.destinationBody = -1;
            loc.departTick = now;
            loc.arriveTick = now + RimverseUnits.TicksFromDays(TransitDays);
            loc.travelAU = 0f;
            loc.travelingShipId = ship.id;
            loc.travelMode = InterstellarMode.Cryosleep;
            loc.travelYears = Years(ly);
            ship.state.thrusting = true;
            ship.map.GetComponent<MapComponent_RimverseShips>().Dirty();
            StarSystemData target = GameComponent_Universe.Instance.GetSystem(dest);
            Messages.Message("Rimverse_MsgCryoStarted".Translate(ship.Label, target.name, Years(ly).ToString("0")), ship.bridge, MessageTypeDefOf.TaskCompletion, false);
            return true;
        }

        public static void OnArrived(Map map, float years)
        {
            if (years <= 0f) return;
            GameComponent_Universe u = GameComponent_Universe.Instance;
            if (u != null) u.yearsInCryo += years;
            long ageTicks = (long)(years * GenDate.TicksPerYear);
            foreach (Pawn p in map.mapPawns.AllPawnsSpawned.ToList())
            {
                if (p.Dead || p.RaceProps.Humanlike || p.Faction != Faction.OfPlayer) continue;
                p.ageTracker.AgeBiologicalTicks += ageTicks;
                p.ageTracker.AgeChronologicalTicks += ageTicks;
            }
            foreach (Pawn p in map.mapPawns.AllPawns.ToList())
            {
                if (p.Spawned || p.Dead || !p.RaceProps.Humanlike) continue;
                p.ageTracker.AgeChronologicalTicks += ageTicks;
            }
            string summary = SimulateYears(years);
            Find.LetterStack.ReceiveLetter("Rimverse_LetterCryoWakeLabel".Translate(), "Rimverse_LetterCryoWake".Translate(years.ToString("0")) + (summary.NullOrEmpty() ? "" : "\n\n" + summary), LetterDefOf.NeutralEvent);
        }

        public static string SimulateYears(float years)
        {
            GameComponent_Civilizations gc = GameComponent_Civilizations.Instance;
            if (gc == null) return null;
            gc.EnsureGenerated();
            Dictionary<int, int> stageBefore = new Dictionary<int, int>();
            Dictionary<int, int> worldsBefore = new Dictionary<int, int>();
            HashSet<int> aliveBefore = new HashSet<int>();
            foreach (Civilization c in gc.civilizations)
            {
                stageBefore[c.id] = c.stage;
                worldsBefore[c.id] = c.planets.Count;
                if (!c.dead) aliveBefore.Add(c.id);
            }
            int steps = Mathf.Clamp(Mathf.RoundToInt(years), 1, 200);
            float per = years / steps;
            for (int y = 0; y < steps; y++)
            {
                foreach (Civilization c in gc.Alive.ToList())
                {
                    try
                    {
                        CivSimulation.Step(gc, c, per);
                    }
                    catch (System.Exception ex)
                    {
                        RimverseLog.Warning("Cryosleep step: " + ex.Message);
                    }
                }
            }
            int now = Find.TickManager.TicksGame;
            foreach (Civilization c in gc.civilizations) c.lastSimTick = now;
            gc.InvalidateTerritory();
            MegastructureRender.Version++;
            List<string> lines = new List<string>();
            foreach (Civilization c in gc.civilizations)
            {
                if (!c.known) continue;
                if (aliveBefore.Contains(c.id) && c.dead) lines.Add("Rimverse_CryoFell".Translate(c.Name));
                else if (!stageBefore.ContainsKey(c.id)) lines.Add("Rimverse_CryoNewCiv".Translate(c.Name));
                else if (!c.dead)
                {
                    int ds = c.stage - stageBefore[c.id];
                    int dw = c.planets.Count - worldsBefore[c.id];
                    if (ds > 0) lines.Add("Rimverse_CryoAdvanced".Translate(c.Name, ds));
                    if (dw > 0) lines.Add("Rimverse_CryoExpanded".Translate(c.Name, dw));
                    else if (dw < 0) lines.Add("Rimverse_CryoShrank".Translate(c.Name, -dw));
                }
            }
            int newUnknown = gc.civilizations.Count(c => !c.known && !stageBefore.ContainsKey(c.id));
            if (newUnknown > 0) lines.Add("Rimverse_CryoRumours".Translate(newUnknown));
            if (lines.Count == 0) return "Rimverse_CryoQuiet".Translate();
            return string.Join("\n", lines.Take(14).Select(s => "- " + s));
        }
    }
}
