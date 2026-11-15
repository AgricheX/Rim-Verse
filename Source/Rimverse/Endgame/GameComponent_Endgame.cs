using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public class GameComponent_Endgame : GameComponent
    {
        public HashSet<string> activatedGates = new HashSet<string>();
        public List<Wormhole> wormholes = new List<Wormhole>();
        public List<EndingKind> achieved = new List<EndingKind>();
        public CrisisKind crisis = CrisisKind.None;
        public bool crisisHappened;
        public int crisisCivId = -2;
        public int crisisStartTick = -1;
        public int crisisSteps;
        public int nextWormholeTick = -1;
        public int nextWormholeId = 1;

        public static GameComponent_Endgame Instance => Current.Game?.GetComponent<GameComponent_Endgame>();

        public GameComponent_Endgame(Game game)
        {
        }

        public override void FinalizeInit()
        {
            GateUtility.ClearCache();
        }

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (now % RimverseUnits.TicksPerHour != 1337) return;
            if (!RimverseMod.Settings.endgameEnabled) return;
            wormholes.RemoveAll(w => !w.Open);
            if (now % RimverseUnits.TicksPerDay == 1337 + RimverseUnits.TicksPerHour * 2)
            {
                if (nextWormholeTick < 0) nextWormholeTick = now + Rand.RangeInclusive(30, 60) * RimverseUnits.TicksPerDay;
                if (now >= nextWormholeTick) SpawnWormhole();
                CheckEndings();
            }
            GameComponent_Civilizations gc = GameComponent_Civilizations.Instance;
            if (gc == null || !gc.generated || !RimverseMod.Settings.civilizationSimulation) return;
            if (now % RimverseUnits.TicksPerQuadrum != 1337 + RimverseUnits.TicksPerHour * 3) return;
            if (crisis == CrisisKind.None)
            {
                if (!crisisHappened && now >= RimverseMod.Settings.crisisStartYears * RimverseUnits.TicksPerYear && Rand.Chance(0.25f)) StartCrisis(gc);
            }
            else CrisisStep(gc);
        }

        private List<RimverseShip> PlayerShipsInSpace()
        {
            List<RimverseShip> list = new List<RimverseShip>();
            foreach (Map m in Find.Maps)
            {
                if (!SpaceMapUtility.IsRimverseSpace(m)) continue;
                RimverseShip s = ColonyUtility.PlayerShipOn(m);
                if (s != null) list.Add(s);
            }
            return list;
        }

        private StarSystemId FarStar(StarSystemId from, double minLy, double maxLy, bool otherGalaxy)
        {
            GameComponent_Universe u = GameComponent_Universe.Instance;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                int galaxy = from.galaxy;
                Vec3d center;
                if (otherGalaxy && u.GalaxyCount > 1)
                {
                    do galaxy = Rand.Range(0, u.GalaxyCount); while (galaxy == from.galaxy);
                    GalaxyData g = u.Galaxy(galaxy);
                    double a = Rand.Range(0f, Mathf.PI * 2f);
                    double d = g.radiusLy * Rand.Range(0.1f, 0.6f);
                    center = new Vec3d(Math.Cos(a) * d, 0, Math.Sin(a) * d);
                }
                else
                {
                    double a = Rand.Range(0f, Mathf.PI * 2f);
                    double d = Rand.Range((float)minLy, (float)maxLy);
                    center = u.PositionOf(from) + new Vec3d(Math.Cos(a) * d, 0, Math.Sin(a) * d);
                }
                List<StarEntry> stars = GalaxyQuery.StarsInRadius(u, galaxy, center, 64);
                if (stars.Count > 0) return stars.RandomElement().id;
            }
            return default;
        }

        private void SpawnWormhole()
        {
            GameComponent_Universe u = GameComponent_Universe.Instance;
            List<RimverseShip> ships = PlayerShipsInSpace();
            if (ships.Count == 0 || u == null)
            {
                nextWormholeTick = Find.TickManager.TicksGame + RimverseUnits.TicksPerDay * 10;
                return;
            }
            RimverseShip ship = ships.RandomElement();
            MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(ship.map);
            if (loc == null || loc.Traveling) return;
            StarSystemId here = loc.SystemId;
            List<StarEntry> near = GalaxyQuery.StarsInRadius(u, here.galaxy, u.PositionOf(here), 30).Where(e => !u.IsHome(e.id)).ToList();
            if (near.Count == 0) return;
            StarSystemId origin = near.RandomElement().id;
            StarSystemId dest = FarStar(origin, 300, 3000, Rand.Chance(0.3f));
            bool frequent = u.Galaxy(here.galaxy).rule == TravelRule.Wormholes;
            nextWormholeTick = Find.TickManager.TicksGame + (frequent ? Rand.RangeInclusive(15, 25) : Rand.RangeInclusive(45, 75)) * RimverseUnits.TicksPerDay;
            if (!dest.Valid) return;
            wormholes.Add(new Wormhole { id = nextWormholeId++, originKey = origin.Key, destKey = dest.Key, expireTick = Find.TickManager.TicksGame + RimverseUnits.TicksPerDay * 20 });
            double ly = u.DistanceLy(here, origin);
            Find.LetterStack.ReceiveLetter("Rimverse_LetterWormholeLabel".Translate(), "Rimverse_LetterWormhole".Translate(u.GetSystem(origin)?.name ?? "?", ly.ToString("0.#")), LetterDefOf.NeutralEvent);
        }

        public void EnterWormhole(Map map, RimverseShip ship, Wormhole w)
        {
            StarSystemId dest = StarSystemId.FromKey(w.destKey);
            if (!w.stable && Rand.Chance(0.15f))
            {
                StarSystemId alt = FarStar(StarSystemId.FromKey(w.originKey), 500, 5000, Rand.Chance(0.3f));
                if (alt.Valid)
                {
                    dest = alt;
                    Messages.Message("Rimverse_MsgWormholeShifted".Translate(), MessageTypeDefOf.NegativeEvent, false);
                }
            }
            GateUtility.Instant(map, ship, dest);
        }

        private void StartCrisis(GameComponent_Civilizations gc)
        {
            CrisisKind kind = (CrisisKind)Rand.RangeInclusive(1, 4);
            crisisHappened = true;
            crisisStartTick = Find.TickManager.TicksGame;
            crisisSteps = 0;
            if (kind != CrisisKind.GreyPlague)
            {
                Civilization c = CivilizationGenerator.CreateCrisis(gc, kind);
                if (c == null) kind = CrisisKind.GreyPlague;
                else
                {
                    crisisCivId = c.id;
                    foreach (Civilization o in gc.Alive.ToList()) if (o != c) gc.SetState(c, o, DiploState.War);
                    gc.EnsureFaction(c);
                    gc.SetPlayerState(c, DiploState.War);
                    c.known = true;
                    gc.fleets.Add(new Fleet { id = gc.NextFleetId(), civId = c.id, mission = FleetMission.Patrol, systemKey = c.capitalKey, power = 500f });
                    gc.InvalidateTerritory();
                }
            }
            crisis = kind;
            Civilization civ = gc.Civ(crisisCivId);
            Find.LetterStack.ReceiveLetter(("Rimverse_CrisisLabel_" + kind).Translate(), ("Rimverse_CrisisStart_" + kind).Translate(civ?.Name ?? ""), LetterDefOf.ThreatBig);
        }

        private void CrisisStep(GameComponent_Civilizations gc)
        {
            crisisSteps++;
            if (crisis == CrisisKind.GreyPlague)
            {
                foreach (Civilization c in gc.Alive)
                {
                    foreach (CivPlanet p in c.planets) p.population *= 0.97f;
                    c.stability = Mathf.Max(0f, c.stability - 3f);
                }
                GameComponent_PlayerColonies pc = GameComponent_PlayerColonies.Instance;
                if (pc != null) foreach (PlayerColony col in pc.colonies) col.population = Mathf.Max(1f, col.population * 0.95f);
                if (crisisSteps >= 24) EndCrisis();
                return;
            }
            Civilization civ = gc.Civ(crisisCivId);
            if (civ == null || civ.dead || civ.planets.Count == 0)
            {
                EndCrisis();
                return;
            }
            Fleet home = gc.fleets.FirstOrDefault(f => f.civId == civ.id && !f.Moving && f.mission == FleetMission.Patrol);
            if (home != null) home.power += 120f + crisisSteps * 10f;
            else gc.fleets.Add(new Fleet { id = gc.NextFleetId(), civId = civ.id, mission = FleetMission.Patrol, systemKey = civ.capitalKey, power = 200f });
            Civilization target = gc.Alive.Where(o => o != civ && o.planets.Count > 0).OrderBy(o => gc.DistanceLy(civ, o)).FirstOrDefault();
            if (target != null) CivSimulation.LaunchFleet(gc, civ, target.planets.RandomElement().systemKey, target.id, FleetMission.War, 0.4f);
            if (crisisSteps % 2 == 0)
            {
                List<StarSystemId> player = gc.PlayerSystems();
                if (player.Count > 0) CivSimulation.LaunchFleet(gc, civ, player.RandomElement().Key, Civilization.PlayerId, FleetMission.Raid, 0.25f);
            }
        }

        private void EndCrisis()
        {
            Find.LetterStack.ReceiveLetter("Rimverse_CrisisEndLabel".Translate(), ("Rimverse_CrisisEnd_" + crisis).Translate(), LetterDefOf.PositiveEvent);
            crisis = CrisisKind.None;
            crisisCivId = -2;
            Achieve(EndingKind.CrisisDefeated);
        }

        private void CheckEndings()
        {
            if (!achieved.Contains(EndingKind.Ascension))
            {
                ResearchProjectDef r = DefDatabase<ResearchProjectDef>.GetNamedSilentFail("Rimverse_ArchotechAscension");
                if (r != null && r.IsFinished) Achieve(EndingKind.Ascension);
            }
            if (!achieved.Contains(EndingKind.GalacticEmpire))
            {
                GameComponent_PlayerColonies pc = GameComponent_PlayerColonies.Instance;
                GameComponent_Civilizations gc = GameComponent_Civilizations.Instance;
                int allies = gc?.Alive.Count(c => c.playerState == DiploState.Alliance || c.playerState == DiploState.Federation) ?? 0;
                if (pc != null && pc.colonies.Count >= 8 && allies >= 3) Achieve(EndingKind.GalacticEmpire);
            }
        }

        public void Achieve(EndingKind kind)
        {
            if (achieved.Contains(kind)) return;
            achieved.Add(kind);
            string text = ("Rimverse_Ending_" + kind).Translate() + "\n\n" + "Rimverse_EndingContinue".Translate();
            GameVictoryUtility.ShowCredits(text, SongDefOf.EndCreditsSong, false);
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref activatedGates, "activatedGates", LookMode.Value);
            Scribe_Collections.Look(ref wormholes, "wormholes", LookMode.Deep);
            Scribe_Collections.Look(ref achieved, "achieved", LookMode.Value);
            Scribe_Values.Look(ref crisis, "crisis");
            Scribe_Values.Look(ref crisisHappened, "crisisHappened");
            Scribe_Values.Look(ref crisisCivId, "crisisCivId", -2);
            Scribe_Values.Look(ref crisisStartTick, "crisisStartTick", -1);
            Scribe_Values.Look(ref crisisSteps, "crisisSteps");
            Scribe_Values.Look(ref nextWormholeTick, "nextWormholeTick", -1);
            Scribe_Values.Look(ref nextWormholeId, "nextWormholeId", 1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (activatedGates == null) activatedGates = new HashSet<string>();
                if (wormholes == null) wormholes = new List<Wormhole>();
                if (achieved == null) achieved = new List<EndingKind>();
            }
        }
    }
}
