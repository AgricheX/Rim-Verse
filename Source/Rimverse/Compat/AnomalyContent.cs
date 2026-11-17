using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace Rimverse
{
    public class CompProperties_RimverseEntity : CompProperties
    {
        public float dreadRadius;
        public float airDrainPerInterval;
        public float healPerInterval;

        public CompProperties_RimverseEntity()
        {
            compClass = typeof(CompRimverseEntity);
        }
    }

    public class CompRimverseEntity : ThingComp
    {
        public CompProperties_RimverseEntity Props => (CompProperties_RimverseEntity)props;

        public override void CompTick()
        {
            if (!parent.IsHashIntervalTick(250) || !(parent is Pawn p) || !p.Spawned || p.Dead || p.Downed) return;
            if (p.Faction == Faction.OfPlayer || p.IsPrisoner) return;
            CompCanBeDormant dormant = p.TryGetComp<CompCanBeDormant>();
            if (dormant != null && !dormant.Awake) return;
            if (Props.dreadRadius > 0f)
            {
                ThoughtDef dread = DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimverse_EntityDread");
                if (dread != null)
                {
                    foreach (Pawn c in p.Map.mapPawns.FreeColonistsSpawned)
                    {
                        if (c.Position.InHorDistOf(p.Position, Props.dreadRadius) && c.needs?.mood != null) c.needs.mood.thoughts.memories.TryGainMemory(dread);
                    }
                }
            }
            if (Props.airDrainPerInterval > 0f)
            {
                RimverseShip ship = p.Map.GetComponent<MapComponent_RimverseShips>()?.ShipAt(p.Position);
                if (ship?.state != null) ship.state.airStock = Mathf.Max(0f, ship.state.airStock - Props.airDrainPerInterval);
            }
            if (Props.healPerInterval > 0f)
            {
                Hediff_Injury injury = p.health.hediffSet.hediffs.OfType<Hediff_Injury>().FirstOrDefault();
                if (injury != null) injury.Heal(Props.healPerInterval);
            }
        }
    }

    public static class AnomalyContent
    {
        public static Faction EntityFaction => Faction.OfEntities ?? Faction.OfInsects ?? Faction.OfAncientsHostile;

        public static Pawn Make(string kind)
        {
            PawnKindDef k = DefDatabase<PawnKindDef>.GetNamedSilentFail(kind);
            if (k == null) return null;
            return PawnGenerator.GeneratePawn(new PawnGenerationRequest(k, EntityFaction, PawnGenerationContext.NonPlayer, -1, true));
        }

        public static List<Pawn> SpawnGroup(Map map, string kind, int count, IEnumerable<IntVec3> cells, bool dormant)
        {
            List<Pawn> list = new List<Pawn>();
            List<IntVec3> pool = cells.Where(c => c.InBounds(map) && c.Standable(map)).ToList();
            if (pool.Count == 0) return list;
            for (int i = 0; i < count; i++)
            {
                Pawn p = Make(kind);
                if (p == null) break;
                GenSpawn.Spawn(p, pool.RandomElement(), map);
                if (dormant) p.TryGetComp<CompCanBeDormant>()?.ToSleep();
                list.Add(p);
            }
            if (!dormant && list.Count > 0)
            {
                LordMaker.MakeNewLord(EntityFaction, new LordJob_AssaultColony(EntityFaction, false, false, false, false, false), map, list);
            }
            return list;
        }

        public static void Letter(string key, IncidentParms parms, LookTargets look, params NamedArgument[] args)
        {
            Find.LetterStack.ReceiveLetter((key + "Label").Translate(), key.Translate(args), LetterDefOf.ThreatBig, look);
        }

        public static BlueprintBuildResult BuildDerelict(Map map, RimverseShip player, int sizeClass, bool station)
        {
            BlueprintRequest req = new BlueprintRequest
            {
                layout = station ? BlueprintLayout.Station : BlueprintLayout.Ship,
                style = BlueprintStyle.Derelict,
                sizeClass = sizeClass,
                damage = 0.6f,
                seed = Rand.Int
            };
            req.Add(station ? "Rimverse_BridgeCommand" : "Rimverse_BridgeSmall").Add("Rimverse_ChemThruster", station ? 0 : sizeClass + 1).Add("Rimverse_FuelCell", sizeClass).Add("Rimverse_AirGenerator").Add("Rimverse_AirTank").Add("Rimverse_CargoBay", sizeClass).Add("Bed", 2 + sizeClass * 2).Add("Rimverse_RadiatorFixed", sizeClass + 1);
            ShipBlueprint bp = ShipBlueprintGenerator.Generate(req);
            if (bp == null) return null;
            if (!EnemyShipGenerator.TryFindRect(map, player, bp.width, bp.height, out CellRect rect)) return null;
            BlueprintBuildResult res = ShipBlueprintBuilder.Build(map, rect, bp, new BlueprintBuildOptions { faction = null, fuel = Rand.Range(0f, 0.2f), hull = Rand.Range(0.3f, 0.8f), powerOn = false, clearArea = false });
            if (res == null) return null;
            if (res.freeCells.Count == 0) res.freeCells.AddRange(res.interior);
            for (int i = 0; i < 4 && res.freeCells.Count > 0; i++) FilthMaker.TryMakeFilth(res.freeCells.RandomElement(), map, ThingDefOf.Filth_Blood, 2);
            return res;
        }

        public static void Loot(Map map, CellRect inner, float value) => Loot(map, inner.Cells.ToList(), value);

        public static void Loot(Map map, List<IntVec3> cells, float value)
        {
            string[] pool = { "Steel", "Plasteel", "ComponentIndustrial", "ComponentSpacer", "MedicineIndustrial", "Bioferrite", "Rimverse_VoidShard", "Rimverse_WarpMatter", "Gold", "Uranium" };
            float left = value;
            int guard = 0;
            while (left > 0f && guard++ < 30)
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(pool.RandomElement());
                if (def == null) continue;
                int count = Mathf.Clamp(Mathf.RoundToInt(Rand.Range(150f, 400f) / Mathf.Max(1f, def.BaseMarketValue)), 1, def.stackLimit);
                Thing t = ThingMaker.MakeThing(def);
                t.stackCount = count;
                IntVec3 c = cells.Where(x => x.Standable(map) && x.GetFirstItem(map) == null).RandomElementWithFallback(cells.Count > 0 ? cells[0] : IntVec3.Invalid);
                if (!c.IsValid) return;
                GenPlace.TryPlaceThing(t, c, map, ThingPlaceMode.Near);
                left -= count * def.BaseMarketValue;
            }
        }

        public static void Corpses(Map map, List<IntVec3> cells, int count)
        {
            if (cells.Count == 0) return;
            for (int i = 0; i < count; i++)
            {
                Pawn p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.SpaceRefugee, null, PawnGenerationContext.NonPlayer, -1, true));
                IntVec3 c = cells.Where(x => x.Standable(map)).RandomElementWithFallback(cells[0]);
                GenSpawn.Spawn(p, c, map);
                p.Kill(null);
                p.Corpse?.GetComp<CompRottable>()?.RotImmediately();
            }
        }

        public static bool SpaceTarget(IncidentParms parms, out Map map, out RimverseShip ship)
        {
            map = parms.target as Map;
            ship = null;
            if (!ModCompat.Anomaly || map == null || !SpaceMapUtility.IsRimverseSpace(map)) return false;
            ship = ColonyUtility.PlayerShipOn(map);
            return ship != null;
        }

        public static bool VoidNavigationKnown => DefDatabase<ResearchProjectDef>.GetNamedSilentFail("Rimverse_VoidNavigation")?.IsFinished ?? false;

        public const int VoidJumpCost = 25;
        public const double VoidJumpRadius = 40.0;

        public static int Bioferrite(Map map)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail("Bioferrite");
            return def == null ? 0 : map.listerThings.ThingsOfDef(def).Where(t => !t.Position.Fogged(map)).Sum(t => t.stackCount);
        }

        public static IEnumerable<Gizmo> BridgeGizmos(RimverseShip ship)
        {
            if (!ModCompat.Anomaly || ship?.bridge == null || !VoidNavigationKnown || !SpaceMapUtility.IsRimverseSpace(ship.map)) yield break;
            Command_Action cmd = new Command_Action
            {
                defaultLabel = "Rimverse_CmdVoidJump".Translate(),
                defaultDesc = "Rimverse_CmdVoidJumpDesc".Translate(VoidJumpCost, VoidJumpRadius),
                icon = RimverseTex.Launch,
                action = () => TryVoidJump(ship)
            };
            MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(ship.map);
            if (loc == null || loc.Traveling) cmd.Disable("Rimverse_ProblemAlreadyTraveling".Translate());
            else if (Bioferrite(ship.map) < VoidJumpCost) cmd.Disable("Rimverse_VoidJumpNoFerrite".Translate(VoidJumpCost));
            yield return cmd;
        }

        public static void TryVoidJump(RimverseShip ship)
        {
            MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(ship.map);
            GameComponent_Universe u = GameComponent_Universe.Instance;
            if (loc == null || u == null) return;
            StarSystemId here = loc.SystemId;
            List<StarEntry> near = GalaxyQuery.StarsInRadius(u, here.galaxy, u.PositionOf(here), VoidJumpRadius).Where(e => e.id != here).ToList();
            if (near.Count == 0) return;
            ThingDef fer = DefDatabase<ThingDef>.GetNamedSilentFail("Bioferrite");
            int need = VoidJumpCost;
            foreach (Thing t in ship.map.listerThings.ThingsOfDef(fer).ToList())
            {
                if (need <= 0) break;
                int take = Mathf.Min(need, t.stackCount);
                t.SplitOff(take).Destroy();
                need -= take;
            }
            StarSystemId dest = near.RandomElement().id;
            int now = Find.TickManager.TicksGame;
            loc.destinationSystemKey = dest.Key;
            loc.destinationBody = -1;
            loc.departTick = now;
            loc.arriveTick = now + RimverseUnits.TicksPerHour * 6;
            loc.travelAU = 0f;
            loc.travelingShipId = ship.id;
            ship.state.thrusting = true;
            ship.state.warping = true;
            ship.map.GetComponent<MapComponent_RimverseShips>().Dirty();
            GameComponent_RimverseExtras.Instance?.Add("voidjump_" + ship.bridge.thingIDNumber);
            Messages.Message("Rimverse_MsgVoidJump".Translate(ship.Label), ship.bridge, MessageTypeDefOf.ThreatSmall, false);
        }

        public static void OnWarpArrived(Map target, Building bridge, StarSystemData sys)
        {
            if (!ModCompat.Anomaly || target == null || bridge == null) return;
            GameComponent_RimverseExtras ex = GameComponent_RimverseExtras.Instance;
            string key = "voidjump_" + bridge.thingIDNumber;
            if (ex != null && ex.Count(key) > 0)
            {
                ex.counters.Remove(key);
                if (Rand.Chance(0.4f))
                {
                    RimverseShip ship = ColonyUtility.PlayerShipOn(target);
                    if (ship != null)
                    {
                        List<Pawn> got = SpawnGroup(target, "Rimverse_Entity_VoidHunger", Rand.RangeInclusive(1, 3), ship.cells, false);
                        if (got.Count > 0) Find.LetterStack.ReceiveLetter("Rimverse_LetterVoidStowawayLabel".Translate(), "Rimverse_LetterVoidStowaway".Translate(), LetterDefOf.ThreatBig, got[0]);
                    }
                }
            }
            if (sys?.Primary != null && sys.Primary.type == StarType.BlackHole)
            {
                IncidentDef edge = DefDatabase<IncidentDef>.GetNamedSilentFail("Rimverse_BlackHoleEdge");
                if (edge != null)
                {
                    IncidentParms p = StorytellerUtility.DefaultParmsNow(edge.category, target);
                    p.forced = true;
                    Find.Storyteller.incidentQueue.Add(edge, Find.TickManager.TicksGame + RimverseUnits.TicksPerHour * Rand.RangeInclusive(4, 20), p);
                }
            }
        }
    }

    public class IncidentWorker_GhostShip : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms) => AnomalyContent.SpaceTarget(parms, out _, out _);

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!AnomalyContent.SpaceTarget(parms, out Map map, out RimverseShip ship)) return false;
            BlueprintBuildResult r = AnomalyContent.BuildDerelict(map, ship, 2, false);
            if (r == null) return false;
            List<IntVec3> inner = r.freeCells;
            AnomalyContent.Loot(map, inner, 1800f);
            AnomalyContent.SpawnGroup(map, "Rimverse_Entity_Sleeper", 1, inner, true);
            AnomalyContent.SpawnGroup(map, "Rimverse_Entity_HullPassenger", Rand.RangeInclusive(2, 3), inner, true);
            AnomalyContent.Letter("Rimverse_LetterGhostShip", parms, new LookTargets(new TargetInfo(r.rect.CenterCell, map)));
            return true;
        }
    }

    public class IncidentWorker_SilentStation : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms) => AnomalyContent.SpaceTarget(parms, out _, out _);

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!AnomalyContent.SpaceTarget(parms, out Map map, out RimverseShip ship)) return false;
            BlueprintBuildResult r = AnomalyContent.BuildDerelict(map, ship, 2, true);
            if (r == null) return false;
            List<IntVec3> inner = r.freeCells;
            AnomalyContent.Loot(map, inner, 3000f);
            AnomalyContent.Corpses(map, inner, Rand.RangeInclusive(3, 6));
            AnomalyContent.SpawnGroup(map, "Rimverse_Entity_CrewEcho", Rand.RangeInclusive(2, 4), inner, true);
            Pawn victim = map.mapPawns.FreeColonistsSpawned.Where(p => p.RaceProps.Humanlike).RandomElementWithFallback();
            if (victim != null)
            {
                Pawn copy = CrewCopy.Make(victim);
                if (copy != null)
                {
                    IntVec3 c = inner.Where(x => x.Standable(map)).RandomElementWithFallback(r.rect.CenterCell);
                    GenSpawn.Spawn(copy, c, map);
                    LordMaker.MakeNewLord(copy.Faction, new LordJob_DefendPoint(c, 8f, 12f, false, false), map, new List<Pawn> { copy });
                }
            }
            AnomalyContent.Letter("Rimverse_LetterSilentStation", parms, new LookTargets(new TargetInfo(r.rect.CenterCell, map)));
            return true;
        }
    }

    public static class CrewCopy
    {
        public static Pawn Make(Pawn source)
        {
            Faction f = Faction.OfAncientsHostile ?? Find.FactionManager.RandomEnemyFaction(false, false, true, TechLevel.Undefined);
            if (f == null) return null;
            Pawn p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(source.kindDef, f, PawnGenerationContext.NonPlayer, -1, true, fixedGender: source.gender));
            p.Name = source.Name;
            if (p.story != null && source.story != null)
            {
                p.story.hairDef = source.story.hairDef;
                p.story.HairColor = source.story.HairColor;
                p.story.headType = source.story.headType;
                p.story.bodyType = source.story.bodyType;
                p.story.skinColorOverride = new Color(0.82f, 0.84f, 0.86f);
            }
            HediffDef h = DefDatabase<HediffDef>.GetNamedSilentFail("Rimverse_FleshCopy");
            if (h != null) p.health.AddHediff(h);
            p.Drawer?.renderer?.SetAllGraphicsDirty();
            return p;
        }
    }

    public class IncidentWorker_MonolithMoon : IncidentWorker
    {
        private static bool Valid(Map map)
        {
            if (!ModCompat.Anomaly || map == null || SpaceMapUtility.IsSpace(map)) return false;
            BodyData b = MapComponent_ShipLocation.For(map)?.Body;
            return b != null && b.IsMoon;
        }

        protected override bool CanFireNowSub(IncidentParms parms) => Valid(parms.target as Map);

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            if (!Valid(map)) return false;
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail("Rimverse_MoonMonolith");
            if (def == null) return false;
            if (!CellFinder.TryFindRandomCellNear(map.Center, map, 40, c => c.Standable(map) && !c.Fogged(map) && GenAdj.OccupiedRect(c, Rot4.North, def.size).All(x => x.InBounds(map) && x.Standable(map)), out IntVec3 cell)) return false;
            Thing mono = GenSpawn.Spawn(ThingMaker.MakeThing(def), cell, map);
            AnomalyContent.SpawnGroup(map, "Rimverse_Entity_ShadowSwarm", Rand.RangeInclusive(6, 10), GenRadial.RadialCellsAround(cell, 8f, true), true);
            AnomalyContent.Letter("Rimverse_LetterMonolithMoon", parms, mono);
            return true;
        }
    }

    public class IncidentWorker_BlackHoleEdge : IncidentWorker
    {
        private static bool Valid(Map map)
        {
            if (!ModCompat.Anomaly || map == null || !SpaceMapUtility.IsRimverseSpace(map)) return false;
            MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(map);
            StarSystemData sys = loc == null ? null : GameComponent_Universe.Instance?.GetSystem(loc.SystemId);
            return sys?.Primary != null && sys.Primary.type == StarType.BlackHole && ColonyUtility.PlayerShipOn(map) != null;
        }

        protected override bool CanFireNowSub(IncidentParms parms) => Valid(parms.target as Map);

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            if (!Valid(map)) return false;
            RimverseShip ship = ColonyUtility.PlayerShipOn(map);
            ThingDef shard = DefDatabase<ThingDef>.GetNamedSilentFail("Rimverse_VoidShard");
            CellRect b = ShipMover.Bounds(ship).ExpandedBy(14).ClipInsideMap(map);
            List<IntVec3> outside = b.Cells.Where(c => !ship.cells.Contains(c) && c.Standable(map)).ToList();
            if (shard != null && outside.Count > 0)
            {
                for (int i = 0; i < Rand.RangeInclusive(4, 8); i++)
                {
                    Thing t = ThingMaker.MakeThing(shard);
                    t.stackCount = Rand.RangeInclusive(2, 6);
                    GenPlace.TryPlaceThing(t, outside.RandomElement(), map, ThingPlaceMode.Near);
                }
            }
            AnomalyContent.SpawnGroup(map, "Rimverse_Entity_VoidHunger", Rand.RangeInclusive(1, 2), outside, false);
            AnomalyContent.Letter("Rimverse_LetterBlackHoleEdge", parms, ship.bridge);
            return true;
        }
    }

    public class CompProperties_PurgeAirlock : CompProperties
    {
        public int cooldownTicks = 60000;
        public float damage = 30f;

        public CompProperties_PurgeAirlock()
        {
            compClass = typeof(CompPurgeAirlock);
        }
    }

    public class CompPurgeAirlock : ThingComp
    {
        public int lastTick = -999999;

        public CompProperties_PurgeAirlock Props => (CompProperties_PurgeAirlock)props;

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref lastTick, "lastTick", -999999);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (parent.Faction != Faction.OfPlayer) yield break;
            Command_Action cmd = new Command_Action
            {
                defaultLabel = "Rimverse_CmdPurge".Translate(),
                defaultDesc = "Rimverse_CmdPurgeDesc".Translate(),
                icon = RimverseTex.ShipPanel,
                action = Purge
            };
            int left = lastTick + Props.cooldownTicks - Find.TickManager.TicksGame;
            if (left > 0) cmd.Disable("Rimverse_PurgeCooldown".Translate((left / (float)RimverseUnits.TicksPerHour).ToString("0.#")));
            yield return cmd;
        }

        private void Purge()
        {
            Room room = parent.GetRoom();
            if (room == null || room.PsychologicallyOutdoors) return;
            lastTick = Find.TickManager.TicksGame;
            int hit = 0;
            foreach (Pawn p in parent.Map.mapPawns.AllPawnsSpawned.ToList())
            {
                if (p.GetRoom() != room) continue;
                if (p.HostileTo(Faction.OfPlayer))
                {
                    p.TakeDamage(new DamageInfo(DamageDefOf.Blunt, Props.damage));
                    p.stances?.stunner?.StunFor(600, parent, false);
                    hit++;
                }
                else if (SpaceMapUtility.Breathes(p) && SpaceMapUtility.VacuumProtection(p) < 1f)
                {
                    HealthUtility.AdjustSeverity(p, RimverseDefOf.Rimverse_Hypoxia, 0.25f);
                }
            }
            RimverseShip ship = parent.Map.GetComponent<MapComponent_RimverseShips>()?.ShipAt(parent.Position);
            if (ship?.state != null) ship.state.airStock = Mathf.Max(0f, ship.state.airStock - room.CellCount * 0.5f);
            FleckMaker.ThrowDustPuffThick(parent.DrawPos, parent.Map, 3f, Color.white);
            Messages.Message("Rimverse_MsgPurged".Translate(hit), parent, MessageTypeDefOf.NeutralEvent, false);
        }
    }

    public class CompProperties_VoidSensor : CompProperties
    {
        public CompProperties_VoidSensor()
        {
            compClass = typeof(CompVoidSensor);
        }
    }

    public class CompVoidSensor : ThingComp
    {
        private int hostile;
        private int sleeping;
        private int lastWarned = -1;

        public override void CompTick()
        {
            if (!parent.IsHashIntervalTick(600) || !parent.Spawned) return;
            CompPowerTrader power = parent.TryGetComp<CompPowerTrader>();
            if (power != null && !power.PowerOn) { hostile = sleeping = 0; return; }
            Faction ef = AnomalyContent.EntityFaction;
            hostile = 0;
            sleeping = 0;
            foreach (Pawn p in parent.Map.mapPawns.AllPawnsSpawned)
            {
                if (p.Dead || p.Faction != ef) continue;
                CompCanBeDormant d = p.TryGetComp<CompCanBeDormant>();
                if (d != null && !d.Awake) sleeping++;
                else hostile++;
            }
            if (hostile + sleeping > 0 && Find.TickManager.TicksGame - lastWarned > 60000)
            {
                lastWarned = Find.TickManager.TicksGame;
                Messages.Message("Rimverse_MsgVoidSensor".Translate(hostile + sleeping), parent, MessageTypeDefOf.CautionInput, false);
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (parent.Faction != Faction.OfPlayer) yield break;
            Command_Action cmd = new Command_Action
            {
                defaultLabel = "Rimverse_CmdVoidPulse".Translate(),
                defaultDesc = "Rimverse_CmdVoidPulseDesc".Translate(),
                icon = RimverseTex.ShipPanel,
                action = () =>
                {
                    int n = 0;
                    foreach (Pawn p in parent.Map.mapPawns.AllPawnsSpawned.ToList())
                    {
                        if (p.Faction != AnomalyContent.EntityFaction) continue;
                        CompCanBeDormant d = p.TryGetComp<CompCanBeDormant>();
                        if (d != null && !d.Awake) { d.WakeUp(); n++; }
                    }
                    Messages.Message("Rimverse_MsgVoidPulse".Translate(n), parent, MessageTypeDefOf.NeutralEvent, false);
                }
            };
            if (sleeping == 0) cmd.Disable("Rimverse_VoidPulseNone".Translate());
            yield return cmd;
        }

        public override string CompInspectStringExtra() => "Rimverse_VoidSensorInspect".Translate(hostile, sleeping);
    }
}
