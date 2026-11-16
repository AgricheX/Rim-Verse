using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public static class GateUtility
    {
        public const int ActivationParts = 3;
        public const int StabilizeParts = 2;
        public const int BeyondParts = 10;
        private static readonly Dictionary<int, List<StarSystemId>> gates = new Dictionary<int, List<StarSystemId>>();

        public static void ClearCache() => gates.Clear();

        public static bool Researched(string defName)
        {
            ResearchProjectDef r = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(defName);
            return r == null || r.IsFinished;
        }

        public static List<StarSystemId> GatesIn(int galaxy)
        {
            if (gates.TryGetValue(galaxy, out List<StarSystemId> list)) return list;
            list = new List<StarSystemId>();
            GameComponent_Universe u = GameComponent_Universe.Instance;
            GalaxyData g = u?.Galaxy(galaxy);
            if (g == null) return list;
            UniverseRand r = new UniverseRand(UniverseRand.Hash(u.universeSeed, 9001, galaxy));
            HashSet<string> taken = new HashSet<string>();
            List<Vec3d> anchors = new List<Vec3d> { new Vec3d(g.radiusLy * 0.05, 0, 0) };
            if (galaxy == u.HomeSystemId.galaxy)
            {
                Vec3d home = u.PositionOf(u.HomeSystemId);
                double a = r.ValueDouble * Math.PI * 2.0;
                double d = r.Range(25f, 60f);
                anchors.Add(home + new Vec3d(Math.Cos(a) * d, 0, Math.Sin(a) * d));
            }
            int count = 12 + (g.hasRing ? 24 : 0) + g.sizeClass / 10;
            for (int i = 0; i < count; i++)
            {
                double a = r.ValueDouble * Math.PI * 2.0;
                double d = g.hasRing && i % 2 == 0 ? g.radiusLy * (g.ringRadius + (r.ValueDouble - 0.5) * 0.12) : g.radiusLy * (0.1 + 0.8 * r.ValueDouble);
                anchors.Add(new Vec3d(Math.Cos(a) * d, 0, Math.Sin(a) * d));
            }
            foreach (Vec3d p in anchors)
            {
                StarEntry best = null;
                double bestD = double.MaxValue;
                foreach (double radius in new[] { 40.0, 120.0 })
                {
                    foreach (StarEntry e in GalaxyQuery.StarsInRadius(u, galaxy, p, radius))
                    {
                        if (taken.Contains(e.id.Key) || u.IsHome(e.id)) continue;
                        double dd = Vec3d.Distance(e.position, p);
                        if (dd < bestD)
                        {
                            bestD = dd;
                            best = e;
                        }
                    }
                    if (best != null) break;
                }
                if (best == null) continue;
                taken.Add(best.id.Key);
                list.Add(best.id);
            }
            gates[galaxy] = list;
            return list;
        }

        public static bool IsGate(StarSystemId id) => GatesIn(id.galaxy).Contains(id);

        public static bool IsGreatGate(StarSystemId id)
        {
            List<StarSystemId> list = GatesIn(id.galaxy);
            return list.Count > 0 && list[0] == id;
        }

        public static List<StarSystemId> Destinations(StarSystemId origin)
        {
            List<StarSystemId> result = GatesIn(origin.galaxy).Where(g => g != origin).ToList();
            if (IsGreatGate(origin))
            {
                GameComponent_Universe u = GameComponent_Universe.Instance;
                for (int i = 0; i < u.GalaxyCount; i++)
                {
                    if (i == origin.galaxy) continue;
                    List<StarSystemId> other = GatesIn(i);
                    if (other.Count > 0) result.Add(other[0]);
                }
            }
            return result;
        }

        public static string Label(StarSystemId id)
        {
            GameComponent_Universe u = GameComponent_Universe.Instance;
            StarSystemData sys = u.GetSystem(id);
            string name = sys?.name ?? id.Key;
            if (id.galaxy != u.HomeSystemId.galaxy || IsGreatGate(id)) name += " (" + u.Galaxy(id.galaxy).name + ")";
            if (IsGreatGate(id)) name = "Rimverse_GreatGateLabel".Translate(name);
            return name;
        }

        public static void Instant(Map map, RimverseShip ship, StarSystemId dest)
        {
            MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(map);
            if (loc == null || ship == null) return;
            IdeoEvents.OnGateUsed(map);
            int cooldown = ship.state.lastWarpTick;
            loc.destinationSystemKey = dest.Key;
            loc.travelingShipId = ship.id;
            WarpTravel.Arrive(map, loc);
            Map now = ship.bridge?.Map;
            RimverseShip moved = now?.GetComponent<MapComponent_RimverseShips>()?.ShipById(ship.bridge.thingIDNumber);
            if (moved != null) moved.state.lastWarpTick = cooldown;
        }

        public static IEnumerable<Gizmo> Gizmos(RimverseShip ship)
        {
            Map map = ship.map;
            MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(map);
            GameComponent_Endgame eg = GameComponent_Endgame.Instance;
            if (loc == null || loc.Traveling || eg == null || !SpaceMapUtility.IsOrbit(map)) yield break;
            StarSystemId here = loc.SystemId;
            if (IsGate(here))
            {
                bool active = eg.activatedGates.Contains(here.Key);
                if (!active)
                {
                    Command_Action act = new Command_Action
                    {
                        defaultLabel = "Rimverse_CmdActivateGate".Translate(),
                        defaultDesc = "Rimverse_CmdActivateGateDesc".Translate(ActivationParts),
                        icon = RimverseTex.ShipPanel,
                        action = () =>
                        {
                            if (RoyalPermits.HasGatePass(map) || ColonyUtility.Consume(map, d => d.defName == "Rimverse_GatePart", ActivationParts))
                            {
                                eg.activatedGates.Add(here.Key);
                                Messages.Message("Rimverse_MsgGateActivated".Translate(Label(here)), MessageTypeDefOf.PositiveEvent, false);
                            }
                        }
                    };
                    if (!Researched("Rimverse_GateUse")) act.Disable("Rimverse_NeedResearch".Translate(DefDatabase<ResearchProjectDef>.GetNamedSilentFail("Rimverse_GateUse")?.LabelCap ?? "?"));
                    else if (!RoyalPermits.HasGatePass(map) && ColonyUtility.CountOnMap(map, d => d.defName == "Rimverse_GatePart") < ActivationParts) act.Disable("Rimverse_NeedGateParts".Translate(ActivationParts));
                    yield return act;
                }
                else
                {
                    Command_Action use = new Command_Action
                    {
                        defaultLabel = "Rimverse_CmdUseGate".Translate(),
                        defaultDesc = "Rimverse_CmdUseGateDesc".Translate(),
                        icon = RimverseTex.Launch,
                        action = () =>
                        {
                            List<FloatMenuOption> opts = new List<FloatMenuOption>();
                            foreach (StarSystemId d in Destinations(here))
                            {
                                StarSystemId dest = d;
                                opts.Add(new FloatMenuOption(Label(dest), () => Instant(map, ship, dest)));
                            }
                            if (IsGreatGate(here) && here.galaxy == GameComponent_Universe.Instance.HomeSystemId.galaxy)
                            {
                                opts.Add(new FloatMenuOption("Rimverse_GateBeyond".Translate(BeyondParts), () =>
                                {
                                    if (!Researched("Rimverse_GateConstruction") || !ColonyUtility.Consume(map, d => d.defName == "Rimverse_GatePart", BeyondParts))
                                    {
                                        Messages.Message("Rimverse_GateBeyondBlocked".Translate(BeyondParts), MessageTypeDefOf.RejectInput, false);
                                        return;
                                    }
                                    eg.Achieve(EndingKind.BeyondTheGroup);
                                }));
                            }
                            if (opts.Count > 0) Find.WindowStack.Add(new FloatMenu(opts));
                        }
                    };
                    yield return use;
                }
            }
            foreach (Wormhole w in eg.wormholes)
            {
                if (!w.Open || w.originKey != here.Key) continue;
                Wormhole hole = w;
                yield return new Command_Action
                {
                    defaultLabel = "Rimverse_CmdEnterWormhole".Translate(),
                    defaultDesc = hole.stable ? "Rimverse_CmdEnterWormholeStableDesc".Translate() : "Rimverse_CmdEnterWormholeDesc".Translate(),
                    icon = RimverseTex.Launch,
                    action = () => eg.EnterWormhole(map, ship, hole)
                };
                if (!hole.stable)
                {
                    Command_Action stab = new Command_Action
                    {
                        defaultLabel = "Rimverse_CmdStabilizeWormhole".Translate(),
                        defaultDesc = "Rimverse_CmdStabilizeWormholeDesc".Translate(StabilizeParts),
                        icon = RimverseTex.ShipPanel,
                        action = () =>
                        {
                            if (ColonyUtility.Consume(map, d => d.defName == "Rimverse_GatePart", StabilizeParts))
                            {
                                hole.stable = true;
                                Messages.Message("Rimverse_MsgWormholeStable".Translate(), MessageTypeDefOf.PositiveEvent, false);
                            }
                        }
                    };
                    if (!Researched("Rimverse_WormholeStabilization")) stab.Disable("Rimverse_NeedResearch".Translate(DefDatabase<ResearchProjectDef>.GetNamedSilentFail("Rimverse_WormholeStabilization")?.LabelCap ?? "?"));
                    else if (ColonyUtility.CountOnMap(map, d => d.defName == "Rimverse_GatePart") < StabilizeParts) stab.Disable("Rimverse_NeedGateParts".Translate(StabilizeParts));
                    yield return stab;
                }
            }
        }
    }
}
