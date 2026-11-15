using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace Rimverse
{
    public class GameComponent_OdysseyLayers : GameComponent
    {
        private static readonly string[] Layers = { "Rimverse_MoonLayer", "Rimverse_RingLayer", "Rimverse_BeltLayer", "Rimverse_StationLayer" };
        private static readonly Vector3[] Origins = { new Vector3(240f, 40f, 0f), Vector3.zero, Vector3.zero, Vector3.zero };
        private static readonly int[] Subdivisions = { 4, 4, 4, 3 };
        private static readonly float[] Fuel = { 80f, 30f, 40f, 15f };

        public GameComponent_OdysseyLayers(Game game)
        {
        }

        public override void FinalizeInit()
        {
            if (!ModCompat.Odyssey || !RimverseMod.Settings.odysseyIntegration || Find.World == null) return;
            try
            {
                EnsureLayers();
            }
            catch (System.Exception e)
            {
                RimverseLog.Warning("Odyssey layers: " + e.Message);
            }
        }

        public static BodyData BodyFor(int layerIndex, StarSystemData sys)
        {
            BodyData home = sys?.HomeWorld;
            if (home == null) return null;
            switch (layerIndex)
            {
                case 0:
                    return sys.bodies.Where(b => b.parent == home.index).OrderByDescending(b => b.sizeClass).FirstOrDefault();
                case 1:
                    return home.rings ? home : null;
                case 2:
                    return sys.bodies.Where(b => b.type == BodyType.Belt && !b.IsMoon && b.orbitAU <= home.orbitAU * 3f && b.orbitAU >= home.orbitAU / 3f).OrderBy(b => Mathf.Abs(b.orbitAU - home.orbitAU)).FirstOrDefault();
                default:
                    return home;
            }
        }

        public static int LayerIndex(PlanetLayerDef def)
        {
            if (def == null) return -1;
            for (int i = 0; i < Layers.Length; i++) if (def.defName == Layers[i]) return i;
            return -1;
        }

        public static bool TryLinkedBody(Map map, out BodyData body, out bool surface)
        {
            body = null;
            surface = false;
            if (!OdysseyCompat.Active || map == null || !map.Tile.Valid) return false;
            int i = LayerIndex(map.Tile.Layer?.Def);
            if (i < 0) return false;
            body = BodyFor(i, GameComponent_Universe.Instance?.HomeSystem);
            surface = i == 0;
            return body != null;
        }

        public static PlanetLayer LayerForBody(BodyData body)
        {
            if (!OdysseyCompat.Active || body == null) return null;
            StarSystemData sys = GameComponent_Universe.Instance?.HomeSystem;
            if (sys == null || !sys.bodies.Contains(body)) return null;
            if (BodyFor(0, sys) != body) return null;
            PlanetLayerDef def = DefDatabase<PlanetLayerDef>.GetNamedSilentFail(Layers[0]);
            return def != null && Find.WorldGrid.TryGetFirstLayerOfDef(def, out PlanetLayer layer) ? layer : null;
        }

        public static void EnsureLayers()
        {
            WorldGrid grid = Find.WorldGrid;
            PlanetLayer surface = grid.Surface;
            PlanetLayer orbit = grid.Orbit;
            StarSystemData sys = GameComponent_Universe.Instance?.HomeSystem;
            if (sys == null) return;
            for (int i = 0; i < Layers.Length; i++)
            {
                PlanetLayerDef def = DefDatabase<PlanetLayerDef>.GetNamedSilentFail(Layers[i]);
                if (def == null || grid.TryGetFirstLayerOfDef(def, out _)) continue;
                BodyData body = BodyFor(i, sys);
                if (body == null) continue;
                float radius = i == 0 ? Mathf.Clamp(10f + body.sizeClass * 0.5f, 12f, 45f) : i == 1 ? 155f : i == 2 ? 185f : 112f;
                PlanetLayer layer = grid.RegisterPlanetLayer(def, Origins[i], radius, 180f, 300f, Subdivisions[i], 160f, 2.5f);
                WorldGenerator.GeneratePlanetLayer(layer, Find.World.info.seedString, Gen.HashCombineInt(Find.World.info.Seed, 91131 + i));
                if (surface != null)
                {
                    if (!surface.HasConnectionFromTo(layer)) surface.AddConnection(layer, Fuel[i]);
                    if (!layer.HasConnectionFromTo(surface)) layer.AddConnection(surface, Fuel[i]);
                }
                if (orbit != null)
                {
                    if (!orbit.HasConnectionFromTo(layer)) orbit.AddConnection(layer, Fuel[i] * 0.5f);
                    if (!layer.HasConnectionFromTo(orbit)) layer.AddConnection(orbit, Fuel[i] * 0.5f);
                }
                if (i == 0 && !body.name.NullOrEmpty()) RimverseLog.Message("Moon layer linked to " + body.name + ".");
            }
        }
    }

    public static class OdysseyCompat
    {
        public static bool Active => ModCompat.Odyssey && RimverseMod.Settings.odysseyIntegration;

        public static bool OrbitMap(Map map) => Active && map != null && map.IsPlayerHome && SpaceMapUtility.IsOdysseyVacuum(map);

        public static bool AnySpaceHome(Map map) => Active && map != null && map.IsPlayerHome && SpaceMapUtility.IsSpace(map);

        public static IEnumerable<Gizmo> BridgeGizmos(RimverseShip ship)
        {
            if (!Active || ship?.bridge == null) yield break;
            Map map = ship.map;
            if (SpaceMapUtility.IsOdysseyVacuum(map))
            {
                yield return new Command_Action
                {
                    defaultLabel = "Rimverse_CmdEnterSystem".Translate(),
                    defaultDesc = "Rimverse_CmdEnterSystemDesc".Translate(),
                    icon = RimverseTex.Launch,
                    action = () => EnterSystem(ship)
                };
            }
            else if (SpaceMapUtility.IsRimverseSpace(map))
            {
                MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(map);
                GameComponent_Universe u = GameComponent_Universe.Instance;
                if (loc == null || u == null || !u.IsHome(loc.SystemId)) yield break;
                List<Map> targets = Find.Maps.Where(m => m != map && SpaceMapUtility.IsOdysseyVacuum(m)).ToList();
                Command_Action dock = new Command_Action
                {
                    defaultLabel = "Rimverse_CmdDockOdyssey".Translate(),
                    defaultDesc = "Rimverse_CmdDockOdysseyDesc".Translate(),
                    icon = RimverseTex.Land,
                    action = () =>
                    {
                        List<FloatMenuOption> opts = targets.Select(m => new FloatMenuOption(m.Parent?.LabelCap ?? m.ToString(), () => Dock(ship, m))).ToList();
                        Find.WindowStack.Add(new FloatMenu(opts));
                    }
                };
                if (targets.Count == 0) dock.Disable("Rimverse_NoOdysseyMaps".Translate());
                if (loc.Traveling) dock.Disable("Rimverse_ProblemAlreadyTraveling".Translate());
                yield return dock;
            }
        }

        public static void EnterSystem(RimverseShip ship)
        {
            Map from = ship.map;
            Building bridge = ship.bridge;
            Map target = BodyMapUtility.CreateOrbitPocket(from, ShipLaunchUtility.OrbitMapSize);
            MapComponent_ShipLocation dst = MapComponent_ShipLocation.For(target);
            GameComponent_Universe u = GameComponent_Universe.Instance;
            StarSystemData home = u?.HomeSystem;
            MapComponent_ShipLocation src = MapComponent_ShipLocation.For(from);
            int body = home?.HomeWorld?.index ?? 0;
            if (GameComponent_OdysseyLayers.TryLinkedBody(from, out BodyData linked, out _)) body = linked.index;
            if (home != null) dst.SetLocation(home.id, body);
            dst.SetSurface(false);
            CellRect bounds = ShipMover.Bounds(ship);
            ShipMover.Move(ship, target, target.Center - bounds.CenterCell);
            Messages.Message("Rimverse_MsgEnteredSystem".Translate(ship.Label), bridge, MessageTypeDefOf.PositiveEvent, false);
        }

        public static void Dock(RimverseShip ship, Map target)
        {
            if (!ShipMover.TryFindLandingOffset(target, ship, out IntVec3 offset))
            {
                Messages.Message("Rimverse_NoDockSpace".Translate(), ship.bridge, MessageTypeDefOf.RejectInput, false);
                return;
            }
            Map from = ship.map;
            Building bridge = ship.bridge;
            string label = ship.Label;
            ShipMover.Move(ship, target, offset);
            BodyMapUtility.CleanupIfEmpty(from);
            Messages.Message("Rimverse_MsgDocked".Translate(label), bridge, MessageTypeDefOf.PositiveEvent, false);
        }

        public static Civilization RandomKnownCiv(bool hostile)
        {
            GameComponent_Civilizations gc = GameComponent_Civilizations.Instance;
            if (gc == null) return null;
            return gc.Known.Where(c => (c.playerState == DiploState.War) == hostile).RandomElementWithFallback();
        }
    }

    public abstract class IncidentWorker_OrbitBase : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms) => OdysseyCompat.AnySpaceHome(parms.target as Map) && Extra((Map)parms.target);

        protected virtual bool Extra(Map map) => true;
    }

    public class IncidentWorker_OrbitCivTrader : IncidentWorker_OrbitBase
    {
        protected override bool Extra(Map map) => OdysseyCompat.RandomKnownCiv(false) != null;

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            Civilization c = OdysseyCompat.RandomKnownCiv(false);
            if (c == null) return false;
            Faction f = GameComponent_Civilizations.Instance.EnsureFaction(c);
            if (f == null) return false;
            TradeShip ship = new TradeShip(RimverseDefOf.Rimverse_CivSpaceTrader, f) { name = c.Name };
            map.passingShipManager.AddShip(ship);
            SendStandardLetter("Rimverse_LetterOrbitTraderLabel".Translate(), "Rimverse_LetterOrbitTrader".Translate(c.Name), LetterDefOf.PositiveEvent, parms, LookTargets.Invalid);
            return true;
        }
    }

    public class IncidentWorker_OrbitCivPatrol : IncidentWorker_OrbitBase
    {
        protected override bool Extra(Map map) => GameComponent_Civilizations.Instance?.Known.Any() ?? false;

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            GameComponent_Civilizations gc = GameComponent_Civilizations.Instance;
            Civilization c = gc.Known.RandomElementWithFallback();
            if (c == null) return false;
            if (c.playerState == DiploState.War)
            {
                Faction f = gc.EnsureFaction(c);
                if (f == null) return false;
                IncidentParms raid = new IncidentParms
                {
                    target = map,
                    faction = f,
                    points = Mathf.Max(300f, StorytellerUtility.DefaultThreatPointsNow(map)),
                    raidStrategy = RaidStrategyDefOf.ImmediateAttack,
                    raidArrivalMode = PawnsArrivalModeDefOf.CenterDrop,
                    forced = true
                };
                if (!IncidentDefOf.RaidEnemy.Worker.TryExecute(raid)) return false;
                SendStandardLetter("Rimverse_LetterOrbitPatrolHostileLabel".Translate(), "Rimverse_LetterOrbitPatrolHostile".Translate(c.Name), LetterDefOf.ThreatBig, parms, LookTargets.Invalid);
                return true;
            }
            gc.ChangePlayerOpinion(c, 3f);
            SendStandardLetter("Rimverse_LetterOrbitPatrolLabel".Translate(), "Rimverse_LetterOrbitPatrol".Translate(c.Name), LetterDefOf.NeutralEvent, parms, LookTargets.Invalid);
            return true;
        }
    }

    public class IncidentWorker_OrbitFleetSighted : IncidentWorker_OrbitBase
    {
        protected override bool Extra(Map map) => OdysseyCompat.RandomKnownCiv(true) != null;

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            Civilization c = OdysseyCompat.RandomKnownCiv(true);
            if (c == null) return false;
            Faction f = GameComponent_Civilizations.Instance.EnsureFaction(c);
            if (f == null) return false;
            IncidentParms raid = new IncidentParms
            {
                target = map,
                faction = f,
                points = Mathf.Max(500f, StorytellerUtility.DefaultThreatPointsNow(map) * 1.3f),
                raidStrategy = RaidStrategyDefOf.ImmediateAttack,
                raidArrivalMode = PawnsArrivalModeDefOf.EdgeDrop,
                forced = true
            };
            int delay = RimverseUnits.TicksFromDays(2f);
            Find.Storyteller.incidentQueue.Add(IncidentDefOf.RaidEnemy, Find.TickManager.TicksGame + delay, raid);
            SendStandardLetter("Rimverse_LetterOrbitFleetLabel".Translate(), "Rimverse_LetterOrbitFleet".Translate(c.Name), LetterDefOf.ThreatSmall, parms, LookTargets.Invalid);
            return true;
        }
    }

    public class IncidentWorker_OrbitDerelictDrift : IncidentWorker_OrbitBase
    {
        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            if (!CellFinder.TryFindRandomCellNear(map.Center, map, Mathf.Min(60, map.Size.x / 2 - 5), c => c.Standable(map) && !c.Roofed(map), out IntVec3 center)) return false;
            CellRect area = CellRect.CenteredOn(center, 7).ClipInsideMap(map);
            List<IntVec3> cells = area.Cells.Where(c => c.Standable(map) && !c.Roofed(map)).ToList();
            if (cells.Count == 0) return false;
            for (int i = 0; i < Rand.RangeInclusive(2, 4); i++) GenPlace.TryPlaceThing(ThingMaker.MakeThing(ThingDefOf.ShipChunk), cells.RandomElement(), map, ThingPlaceMode.Near);
            AnomalyContent.Loot(map, area, 1200f);
            SendStandardLetter("Rimverse_LetterOrbitDerelictLabel".Translate(), "Rimverse_LetterOrbitDerelict".Translate(), LetterDefOf.PositiveEvent, parms, new TargetInfo(center, map));
            return true;
        }
    }
}
