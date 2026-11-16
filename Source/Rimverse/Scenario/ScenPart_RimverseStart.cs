using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public class CivStartRelation
    {
        public LoreKind lore;
        public float opinion;
        public bool contact = true;
    }

    public enum InfoFogLevel
    {
        Full,
        Partial,
        None
    }

    public class ScenPart_RimverseStart : ScenPart
    {
        public InfoFogLevel infoFog = InfoFogLevel.Partial;
        public RimverseStartLocation location = RimverseStartLocation.Surface;
        public RimverseStartLayout layout = RimverseStartLayout.None;
        public List<ResearchProjectDef> research = new List<ResearchProjectDef>();
        public List<CivStartRelation> civs = new List<CivStartRelation>();
        public float fuel = 1f;
        public float air = 1f;
        public float hull = 1f;
        public int yearsAsleep;
        public bool waitForBackroomsExit;
        public string shipNameKey;

        private bool pendingExit;
        private int shipMapId = -1;
        private int bridgeId = -1;

        public override string Summary(Scenario scen)
        {
            string text = ("Rimverse_StartSummary_" + location).Translate();
            if (layout != RimverseStartLayout.None) text += " " + ("Rimverse_StartLayout_" + layout).Translate();
            return text;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref location, "location", RimverseStartLocation.Surface);
            Scribe_Values.Look(ref layout, "layout", RimverseStartLayout.None);
            Scribe_Collections.Look(ref research, "research", LookMode.Def);
            Scribe_Values.Look(ref fuel, "fuel", 1f);
            Scribe_Values.Look(ref air, "air", 1f);
            Scribe_Values.Look(ref hull, "hull", 1f);
            Scribe_Values.Look(ref yearsAsleep, "yearsAsleep");
            Scribe_Values.Look(ref waitForBackroomsExit, "waitForBackroomsExit");
            Scribe_Values.Look(ref shipNameKey, "shipNameKey");
            Scribe_Values.Look(ref infoFog, "infoFog", InfoFogLevel.Partial);
            Scribe_Values.Look(ref pendingExit, "pendingExit");
            Scribe_Values.Look(ref shipMapId, "shipMapId", -1);
            Scribe_Values.Look(ref bridgeId, "bridgeId", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (research == null) research = new List<ResearchProjectDef>();
                if (civs == null) civs = new List<CivStartRelation>();
            }
        }

        public override void PostMapGenerate(Map map)
        {
            if (Find.GameInitData == null || layout != RimverseStartLayout.Wreck) return;
            IntVec3 spot = MapGenerator.PlayerStartSpotValid ? MapGenerator.PlayerStartSpot : map.Center;
            StartShipBuilder.ScatterWreck(map, spot);
        }

        public override void PostGameStart()
        {
            foreach (ResearchProjectDef r in research)
            {
                if (r != null && !r.IsFinished) Find.ResearchManager.FinishProject(r, false, null, false);
            }
            Map start = Find.CurrentMap ?? Find.AnyPlayerHomeMap;
            IntVec3 spot = MapGenerator.PlayerStartSpotValid ? MapGenerator.PlayerStartSpot : start?.Center ?? IntVec3.Zero;
            LongEventHandler.ExecuteWhenFinished(() => Apply(start, spot));
        }

        private void Apply(Map start, IntVec3 spot)
        {
            try
            {
                ApplyCivs();
            }
            catch (System.Exception ex)
            {
                RimverseLog.Warning("Scenario civilizations: " + ex.Message);
            }
            if (start == null || location == RimverseStartLocation.Surface || layout == RimverseStartLayout.None || layout == RimverseStartLayout.Wreck) return;
            PlanetTile surfaceTile = start.Tile;
            if (!surfaceTile.Valid || surfaceTile.Layer != Find.WorldGrid.Surface) surfaceTile = TileFinder.RandomSettlementTileFor(Faction.OfPlayer);
            string name = shipNameKey.NullOrEmpty() ? null : shipNameKey.Translate().ToString();
            Map orbit = null;
            try
            {
                orbit = OrbitUtility.CreateOrbitMap(surfaceTile, name, ShipLaunchUtility.OrbitMapSize);
            }
            catch (System.Exception ex)
            {
                RimverseLog.Warning("Scenario: orbit map failed: " + ex);
            }
            if (orbit == null)
            {
                FailStart(start, spot);
                return;
            }
            List<Pawn> pawns = start.mapPawns.AllPawnsSpawned.Where(p => p.Faction == Faction.OfPlayer).ToList();
            int crew = Mathf.Max(1, pawns.Count(p => p.RaceProps.Humanlike));
            StartShipSpec spec = StartShipBuilder.SpecFor(layout, crew);
            Building bridge = StartShipBuilder.Build(orbit, orbit.Center, spec, layout, fuel, hull, out List<IntVec3> free, out CellRect rect);
            if (bridge == null)
            {
                RimverseLog.Warning("Scenario: starting ship could not be built.");
                FailStart(start, spot);
                return;
            }
            bridgeId = bridge.thingIDNumber;
            shipMapId = orbit.uniqueID;
            MapComponent_RimverseShips ships = orbit.GetComponent<MapComponent_RimverseShips>();
            RimverseShip ship = ships.ShipById(bridge.thingIDNumber);
            if (ship != null)
            {
                ship.state.airStock = ship.airCapacity * Mathf.Clamp01(air);
                if (!name.NullOrEmpty()) ship.state.name = name;
            }
            SetLocation(orbit);
            if (waitForBackroomsExit)
            {
                pendingExit = true;
                return;
            }
            List<Thing> items = start.listerThings.AllThings.Where(t => t.Spawned && t.def.category == ThingCategory.Item && t.def.EverHaulable && t.Position.InHorDistOf(spot, 16f)).ToList();
            MoveInto(orbit, free, pawns, items);
            Unforbid(items);
            if (Current.Game.CurrentMap == start) Current.Game.CurrentMap = orbit;
            MapParent parent = start.Parent;
            if (start.mapPawns.FreeColonistsSpawnedCount == 0)
            {
                Current.Game.DeinitAndRemoveMap(start, false);
                if (parent != null && !parent.Destroyed) parent.Destroy();
            }
            CameraJumper.TryJump(bridge);
            Find.LetterStack.ReceiveLetter(("Rimverse_StartLetter_" + layout).Translate(), ("Rimverse_StartLetterText_" + layout).Translate(), LetterDefOf.NeutralEvent, bridge);
        }

        private static void Unforbid(IEnumerable<Thing> things)
        {
            foreach (Thing t in things)
            {
                if (t != null && !t.Destroyed) t.SetForbidden(false, false);
            }
        }

        private void FailStart(Map start, IntVec3 spot)
        {
            Unforbid(start.listerThings.AllThings.Where(t => t.Spawned && t.def.category == ThingCategory.Item && t.Position.InHorDistOf(spot, 20f)).ToList());
            Find.LetterStack.ReceiveLetter("Rimverse_StartFailed".Translate(), "Rimverse_StartFailedText".Translate(), LetterDefOf.NegativeEvent);
        }

        private void SetLocation(Map orbit)
        {
            MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(orbit);
            GameComponent_Universe u = GameComponent_Universe.Instance;
            if (loc == null || u == null) return;
            StarSystemData sys = u.HomeSystem;
            if (sys == null) return;
            BodyData target = sys.HomeWorld;
            if (location == RimverseStartLocation.Belt)
            {
                target = sys.bodies.FirstOrDefault(b => b.type == BodyType.Belt) ?? sys.bodies.FirstOrDefault(b => b.type == BodyType.Asteroid) ?? target;
            }
            else if (location == RimverseStartLocation.OuterSystem)
            {
                BodyData outer = sys.bodies.Where(b => !b.IsMoon).OrderByDescending(b => b.orbitAU).FirstOrDefault();
                if (outer != null) target = outer;
            }
            if (target != null) loc.SetLocation(sys.id, target.index);
            loc.SetSurface(false);
            if (target != null) u.SetScan(sys.id, target.index, ScanLevel.Exact);
        }

        private static void MoveInto(Map orbit, List<IntVec3> free, List<Pawn> pawns, List<Thing> items)
        {
            List<IntVec3> cells = free.InRandomOrder().ToList();
            int i = 0;
            HashSet<IntVec3> shipCells = new HashSet<IntVec3>(free);
            foreach (Pawn p in pawns)
            {
                if (!p.Spawned) continue;
                p.jobs?.StopAll();
                p.pather?.StopDead();
                p.DeSpawn();
                IntVec3 c = cells.Count > 0 ? cells[i++ % cells.Count] : orbit.Center;
                GenSpawn.Spawn(p, c, orbit);
            }
            foreach (Thing t in items)
            {
                if (!t.Spawned) continue;
                t.DeSpawn();
                IntVec3 c = cells.Count > 0 ? cells[i++ % cells.Count] : orbit.Center;
                if (!GenPlace.TryPlaceThing(t, c, orbit, ThingPlaceMode.Near, null, x => shipCells.Contains(x))) GenPlace.TryPlaceThing(t, c, orbit, ThingPlaceMode.Near);
            }
        }

        private void ApplyCivs()
        {
            GameComponent_Civilizations gc = GameComponent_Civilizations.Instance;
            if (gc == null) return;
            gc.EnsureGenerated();
            for (int y = 0; y < Mathf.Min(yearsAsleep, 200); y++)
            {
                foreach (Civilization c in gc.Alive.ToList())
                {
                    try
                    {
                        CivSimulation.Step(gc, c, 1f);
                    }
                    catch (System.Exception ex)
                    {
                        RimverseLog.Warning("Scenario sleep step: " + ex.Message);
                    }
                }
            }
            if (yearsAsleep > 0)
            {
                int now = Find.TickManager.TicksGame;
                foreach (Civilization c in gc.civilizations) c.lastSimTick = now;
                gc.InvalidateTerritory();
            }
            StarSystemId homeId = GameComponent_Universe.Instance.HomeSystemId;
            HashSet<int> origin = new HashSet<int>();
            foreach (CivStartRelation rel in civs)
            {
                Civilization civ = gc.Alive.Where(c => c.lore == rel.lore).OrderBy(c => gc.DistanceLy(c, homeId)).FirstOrDefault();
                if (civ == null) continue;
                if (rel.contact && infoFog != InfoFogLevel.None)
                {
                    if (!civ.known) gc.MakeContact(civ, false);
                    civ.AddIntel(60f - civ.intel);
                    origin.Add(civ.id);
                }
                gc.ChangePlayerOpinion(civ, rel.opinion);
            }
            foreach (Civilization c in gc.Alive.ToList())
            {
                if (origin.Contains(c.id) || !gc.IsNearPlayer(c)) continue;
                if (infoFog == InfoFogLevel.Full)
                {
                    if (!c.known) gc.MakeContact(c, false);
                    c.AddIntel(45f - c.intel);
                }
                else if (infoFog == InfoFogLevel.Partial)
                {
                    c.rumored = true;
                }
            }
        }

        public override void Tick()
        {
            if (!pendingExit || Find.TickManager.TicksGame % 250 != 0) return;
            Map ship = Find.Maps.FirstOrDefault(m => m.uniqueID == shipMapId);
            if (ship == null)
            {
                pendingExit = false;
                return;
            }
            foreach (Map map in Find.Maps.ToList())
            {
                if (map == ship || EndlessRoomsCompat.IsBackrooms(map)) continue;
                List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned.ToList();
                if (colonists.Count == 0) continue;
                RimverseShip rs = ship.GetComponent<MapComponent_RimverseShips>()?.ShipById(bridgeId);
                List<IntVec3> free = rs != null ? rs.cells.Where(c => c.Standable(ship) && c.Roofed(ship) && c.GetEdifice(ship) == null).ToList() : new List<IntVec3> { ship.Center };
                MoveInto(ship, free, colonists, new List<Thing>());
                pendingExit = false;
                Thing bridge = rs?.bridge;
                if (bridge != null) CameraJumper.TryJump(bridge);
                Find.LetterStack.ReceiveLetter("Rimverse_StartLetter_AsyncExit".Translate(), "Rimverse_StartLetterText_AsyncExit".Translate(), LetterDefOf.PositiveEvent, bridge);
                return;
            }
        }
    }
}
