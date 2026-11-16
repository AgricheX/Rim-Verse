using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.QuestGen;
using UnityEngine;
using Verse;
using Verse.Grammar;

namespace Rimverse
{
    public enum ContractKind
    {
        UrgentDelivery,
        Passenger,
        ConvoyEscort,
        PirateHunt,
        CrewRescue,
        CargoRecovery,
        PlanetSurvey,
        SampleCollection,
        RouteMap,
        Smuggling,
        Espionage,
        BlockadeBreak,
        ColonyDefense,
        RuinExpedition,
        Diplomatic
    }

    public static class ContractUtility
    {
        public const float MaxRangeLy = 80f;

        public static Map PlayerShipMap()
        {
            Map best = null;
            foreach (Map m in Find.Maps)
            {
                if (ColonyUtility.PlayerShipOn(m) == null) continue;
                if (best == null || m.IsPlayerHome && !best.IsPlayerHome) best = m;
            }
            return best;
        }

        public static MapComponent_ShipLocation Here() => MapComponent_ShipLocation.For(PlayerShipMap());

        public static Civilization Giver(bool anyone)
        {
            GameComponent_Civilizations gc = GameComponent_Civilizations.Instance;
            if (gc == null || !gc.generated) return null;
            return gc.Known.Where(c => !c.dead && !c.NoDiplomacy && (anyone || c.playerState != DiploState.War) && !GameComponent_RimverseEvents.Embargoed(c))
                .OrderBy(c => gc.DistanceToPlayer(c, out _)).Take(4).RandomElementWithFallback();
        }

        public static List<string> NearbySystems(string exclude)
        {
            GameComponent_Civilizations gc = GameComponent_Civilizations.Instance;
            GameComponent_Universe u = GameComponent_Universe.Instance;
            MapComponent_ShipLocation here = Here();
            List<string> list = new List<string>();
            if (gc == null || u == null || here == null || here.systemKey.NullOrEmpty()) return list;
            StarSystemId from = here.SystemId;
            foreach (Civilization c in gc.Known.Where(x => !x.dead))
                foreach (CivPlanet p in c.planets)
                    if (p.systemKey != exclude && !list.Contains(p.systemKey) && u.DistanceLy(from, StarSystemId.FromKey(p.systemKey)) <= MaxRangeLy) list.Add(p.systemKey);
            return list;
        }

        public static string SystemName(string key) => GameComponent_Universe.Instance?.GetSystem(StarSystemId.FromKey(key))?.name ?? key;

        public static float DistanceTo(string key)
        {
            MapComponent_ShipLocation here = Here();
            if (here == null || here.systemKey.NullOrEmpty()) return 10f;
            return (float)GameComponent_Universe.Instance.DistanceLy(here.SystemId, StarSystemId.FromKey(key));
        }

        public static int CountOn(Map map, ThingDef def) => map == null || def == null ? 0 : map.listerThings.ThingsOfDef(def).Where(t => !t.Position.Fogged(map)).Sum(t => t.stackCount);

        public static void Take(Map map, ThingDef def, int count)
        {
            foreach (Thing t in map.listerThings.ThingsOfDef(def).ToList())
            {
                if (count <= 0) break;
                int n = Mathf.Min(count, t.stackCount);
                t.SplitOff(n).Destroy();
                count -= n;
            }
        }

        public static void Drop(Map map, ThingDef def, int count)
        {
            if (map == null || def == null || count <= 0) return;
            List<Thing> list = new List<Thing>();
            while (count > 0)
            {
                Thing t = ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
                t.stackCount = Mathf.Min(count, def.stackLimit);
                count -= t.stackCount;
                list.Add(t);
            }
            DropPodUtility.DropThingsNear(DropCellFinder.TradeDropSpot(map), map, list, 110, false, false, true);
        }

        public static bool Ambush(Map map, float factor)
        {
            IncidentDef d = DefDatabase<IncidentDef>.GetNamedSilentFail("Rimverse_ShipAmbush");
            if (d == null || map == null) return false;
            IncidentParms p = StorytellerUtility.DefaultParmsNow(d.category, map);
            p.forced = true;
            p.points *= factor;
            return d.Worker.TryExecute(p);
        }
    }

    public class QuestNode_Root_RimverseContract : QuestNode
    {
        public ContractKind kind;

        private static readonly string[] DeliveryGoods = { "MedicineIndustrial", "ComponentIndustrial", "Chemfuel", "Plasteel" };

        protected override bool TestRunInt(Slate slate)
        {
            if (ContractUtility.PlayerShipMap() == null) return false;
            MapComponent_ShipLocation here = ContractUtility.Here();
            if (here == null || here.systemKey.NullOrEmpty()) return false;
            if (ContractUtility.Giver(kind == ContractKind.Smuggling) == null) return false;
            List<PlayerColony> cols = GameComponent_PlayerColonies.Instance?.colonies ?? new List<PlayerColony>();
            switch (kind)
            {
                case ContractKind.BlockadeBreak:
                case ContractKind.ColonyDefense:
                    return cols.Count > 0;
                case ContractKind.PlanetSurvey:
                case ContractKind.SampleCollection:
                case ContractKind.RuinExpedition:
                    return true;
                case ContractKind.RouteMap:
                    return ContractUtility.NearbySystems(here.systemKey).Count >= 3;
                case ContractKind.Espionage:
                case ContractKind.Diplomatic:
                    return GameComponent_Civilizations.Instance.Known.Count(c => !c.dead && !c.NoDiplomacy) >= 2;
            }
            return ContractUtility.NearbySystems(here.systemKey).Count > 0;
        }

        protected override void RunInt()
        {
            Quest quest = QuestGen.quest;
            Slate slate = QuestGen.slate;
            MapComponent_ShipLocation here = ContractUtility.Here();
            GameComponent_Civilizations gc = GameComponent_Civilizations.Instance;
            GameComponent_Universe u = GameComponent_Universe.Instance;
            Civilization giver = ContractUtility.Giver(kind == ContractKind.Smuggling);
            QuestPart_RimverseContract part = new QuestPart_RimverseContract
            {
                kind = kind,
                giverId = giver?.id ?? -1,
                originKey = here.systemKey,
                inSignalEnable = slate.Get<string>("inSignal")
            };
            List<string> near = ContractUtility.NearbySystems(here.systemKey);
            string target = near.RandomElementWithFallback(here.systemKey);
            float baseReward = 400f;
            switch (kind)
            {
                case ContractKind.UrgentDelivery:
                    part.thingDefName = DeliveryGoods.RandomElement();
                    part.count = part.thingDefName == "Chemfuel" ? 150 : part.thingDefName == "Plasteel" ? 60 : 12;
                    baseReward = 500f;
                    break;
                case ContractKind.Smuggling:
                    part.thingDefName = DefDatabase<ThingDef>.GetNamedSilentFail("Flake") != null ? "Flake" : "Beer";
                    part.count = 40;
                    baseReward = 900f;
                    break;
                case ContractKind.CargoRecovery:
                    part.thingDefName = "ComponentSpacer";
                    part.count = Rand.RangeInclusive(4, 8);
                    baseReward = 700f;
                    break;
                case ContractKind.PirateHunt: baseReward = 900f; break;
                case ContractKind.ConvoyEscort: baseReward = 650f; break;
                case ContractKind.CrewRescue: baseReward = 600f; break;
                case ContractKind.Passenger: baseReward = 450f; break;
                case ContractKind.Espionage:
                case ContractKind.Diplomatic:
                    {
                        Civilization other = gc.Known.Where(c => c != giver && !c.dead && !c.NoDiplomacy).RandomElementWithFallback();
                        part.targetCivId = other?.id ?? -1;
                        if (other != null && !other.capitalKey.NullOrEmpty()) target = other.capitalKey;
                        baseReward = kind == ContractKind.Espionage ? 1000f : 600f;
                        break;
                    }
                case ContractKind.PlanetSurvey:
                case ContractKind.SampleCollection:
                case ContractKind.RuinExpedition:
                    {
                        if (Rand.Chance(0.5f)) target = here.systemKey;
                        StarSystemData sys = u.GetSystem(StarSystemId.FromKey(target));
                        BodyData b = kind == ContractKind.PlanetSurvey
                            ? sys?.bodies.Where(x => u.GetScan(sys.id, x.index) < ScanLevel.Exact).RandomElementWithFallback() ?? sys?.bodies.RandomElementWithFallback()
                            : sys?.bodies.Where(x => x.LandableNow).RandomElementWithFallback() ?? sys?.bodies.RandomElementWithFallback();
                        part.body = b?.index ?? 0;
                        slate.Set("body", b?.name ?? "?");
                        baseReward = kind == ContractKind.RuinExpedition ? 800f : 450f;
                        break;
                    }
                case ContractKind.RouteMap:
                    part.route = near.InRandomOrder().Take(3).ToList();
                    target = part.route[0];
                    baseReward = 750f;
                    break;
                case ContractKind.BlockadeBreak:
                case ContractKind.ColonyDefense:
                    {
                        PlayerColony c = GameComponent_PlayerColonies.Instance.colonies.RandomElement();
                        part.colonyId = c.id;
                        target = c.systemKey;
                        slate.Set("colony", c.name);
                        baseReward = 800f;
                        break;
                    }
            }
            part.targetKey = target;
            float dist = ContractUtility.DistanceTo(target);
            int days = Mathf.Clamp(Mathf.RoundToInt(8f + dist * 0.6f), 8, 60);
            part.days = days;
            part.reward = Mathf.RoundToInt(baseReward * (1f + dist / 25f) / 10f) * 10;
            quest.AddPart(part);
            List<Rule> rules = new List<Rule>
            {
                new Rule_String("system", ContractUtility.SystemName(target)),
                new Rule_String("giver", giver?.ShownName ?? "?"),
                new Rule_String("reward", part.reward.ToString()),
                new Rule_String("days", days.ToString()),
                new Rule_String("count", part.count.ToString()),
                new Rule_String("item", part.ThingDef?.label ?? "?"),
                new Rule_String("body", slate.Get<string>("body") ?? "?"),
                new Rule_String("colony", slate.Get<string>("colony") ?? "?"),
                new Rule_String("target", part.TargetCiv?.ShownName ?? "?"),
                new Rule_String("route", part.route == null ? "?" : string.Join(", ", part.route.Select(ContractUtility.SystemName)))
            };
            QuestGen.AddQuestNameRules(rules);
            QuestGen.AddQuestDescriptionRules(rules);
        }
    }

    public class QuestPart_RimverseContract : QuestPartActivable
    {
        public ContractKind kind;
        public int giverId = -1;
        public int targetCivId = -1;
        public int colonyId = -1;
        public string originKey;
        public string targetKey;
        public int body = -1;
        public string thingDefName;
        public int count;
        public int days;
        public int reward;
        public List<string> route;
        public List<string> visited = new List<string>();
        public int deadlineTick = -1;
        public int phase;
        public int phaseTick = -1;
        public Pawn passenger;

        public ThingDef ThingDef => thingDefName.NullOrEmpty() ? null : DefDatabase<ThingDef>.GetNamedSilentFail(thingDefName);

        private static GameComponent_Civilizations Civs => GameComponent_Civilizations.Instance;

        public Civilization Giver => giverId < 0 || Civs == null ? null : Civs.Civ(giverId);

        public Civilization TargetCiv => targetCivId < 0 || Civs == null ? null : Civs.Civ(targetCivId);

        private PlayerColony Colony => GameComponent_PlayerColonies.Instance?.colonies.FirstOrDefault(c => c.id == colonyId);

        public override IEnumerable<Faction> InvolvedFactions
        {
            get
            {
                Civilization g = Giver;
                if (g?.faction != null) yield return g.faction;
            }
        }

        public override bool QuestPartReserves(Pawn p) => p != null && p == passenger;

        public override string ExpiryInfoPart => State == QuestPartState.Enabled && deadlineTick > 0 ? "Rimverse_ContractDeadline".Translate((deadlineTick - Find.TickManager.TicksGame).ToStringTicksToPeriod()).ToString() : null;

        public override string DescriptionPart => State == QuestPartState.Enabled ? ProgressText() : null;

        private string ProgressText()
        {
            switch (kind)
            {
                case ContractKind.RouteMap: return "Rimverse_ContractRouteProgress".Translate(visited.Count, route?.Count ?? 0).ToString();
                case ContractKind.CargoRecovery: return phase == 0 ? "Rimverse_ContractGoTo".Translate(ContractUtility.SystemName(targetKey)).ToString() : "Rimverse_ContractReturn".Translate(ContractUtility.SystemName(originKey)).ToString();
            }
            return "Rimverse_ContractGoTo".Translate(ContractUtility.SystemName(targetKey)).ToString();
        }

        protected override void Enable(SignalArgs receivedArgs)
        {
            base.Enable(receivedArgs);
            deadlineTick = Find.TickManager.TicksGame + days * GenDate.TicksPerDay;
            Map map = ContractUtility.PlayerShipMap();
            switch (kind)
            {
                case ContractKind.UrgentDelivery:
                case ContractKind.Smuggling:
                    ContractUtility.Drop(map, ThingDef, count);
                    break;
                case ContractKind.Passenger:
                case ContractKind.Diplomatic:
                    passenger = MakePassenger(map);
                    break;
                case ContractKind.RuinExpedition:
                    {
                        GameComponent_PlayerEmpire e = GameComponent_PlayerEmpire.Instance;
                        SpaceAnomalyDef d = DefDatabase<SpaceAnomalyDef>.GetNamedSilentFail("Rimverse_Anomaly_PrecursorRuins");
                        if (e != null && d != null) e.anomalies.Add(new FoundAnomaly { def = d, systemKey = targetKey, body = body });
                        break;
                    }
                case ContractKind.BlockadeBreak:
                    {
                        PlayerColony c = Colony;
                        if (c != null) GameComponent_RimverseEvents.Instance.colonyHaltUntil[c.id] = deadlineTick;
                        break;
                    }
                case ContractKind.ColonyDefense:
                    phaseTick = Find.TickManager.TicksGame + days * GenDate.TicksPerDay / 2;
                    break;
            }
        }

        private Pawn MakePassenger(Map map)
        {
            if (map == null) return null;
            Civilization g = Giver;
            Faction f = g != null ? Civs.EnsureFaction(g) : null;
            PawnKindDef pk = f?.def.basicMemberKind ?? PawnKindDefOf.Colonist;
            Pawn p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(pk, Faction.OfPlayer, PawnGenerationContext.NonPlayer, -1, true));
            GenSpawn.Spawn(p, CellFinder.RandomClosewalkCellNear(DropCellFinder.TradeDropSpot(map), map, 5), map);
            return p;
        }

        public override void QuestPartTick()
        {
            base.QuestPartTick();
            if (State != QuestPartState.Enabled || Find.TickManager.TicksGame % 250 != 0) return;
            int now = Find.TickManager.TicksGame;
            if (passenger != null && passenger.Dead)
            {
                Fail();
                return;
            }
            Map map = ContractUtility.PlayerShipMap();
            MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(map);
            bool atTarget = loc != null && !loc.Traveling && loc.systemKey == targetKey;
            bool inCombat = MapComponent_ShipCombat.For(map)?.InCombat ?? false;
            if (kind == ContractKind.ColonyDefense && now >= phaseTick && phase == 0)
            {
                if (atTarget && ContractUtility.Ambush(map, 1.2f)) phase = 1;
                else
                {
                    PlayerColony c = Colony;
                    if (c != null)
                    {
                        c.population = Mathf.Max(1f, c.population * 0.8f);
                        c.stability = Mathf.Max(0f, c.stability - 15f);
                    }
                    Fail();
                    return;
                }
            }
            if (now >= deadlineTick && kind != ContractKind.ColonyDefense)
            {
                Fail();
                return;
            }
            if (Check(map, loc, atTarget, inCombat)) Succeed(map);
        }

        private bool Check(Map map, MapComponent_ShipLocation loc, bool atTarget, bool inCombat)
        {
            int now = Find.TickManager.TicksGame;
            GameComponent_Universe u = GameComponent_Universe.Instance;
            switch (kind)
            {
                case ContractKind.UrgentDelivery:
                    if (!atTarget || ContractUtility.CountOn(map, ThingDef) < count) return false;
                    ContractUtility.Take(map, ThingDef, count);
                    return true;
                case ContractKind.Smuggling:
                    {
                        if (!atTarget || ContractUtility.CountOn(map, ThingDef) < count) return false;
                        ContractUtility.Take(map, ThingDef, count);
                        Civilization owner = Civs?.OwnerOf(StarSystemId.FromKey(targetKey));
                        if (owner != null && owner != Giver && Rand.Chance(0.3f))
                        {
                            Civs.ChangePlayerOpinion(owner, -15f);
                            Messages.Message("Rimverse_ContractSmuggleCaught".Translate(owner.ShownName), MessageTypeDefOf.NegativeEvent, false);
                            Fail();
                            return false;
                        }
                        return true;
                    }
                case ContractKind.Passenger:
                case ContractKind.Diplomatic:
                    {
                        if (!atTarget || passenger == null || !passenger.Spawned || passenger.Map != map || passenger.Downed) return false;
                        Civilization g = Giver;
                        Faction f = g != null ? Civs.EnsureFaction(g) : null;
                        passenger.DeSpawn();
                        if (f != null) passenger.SetFaction(f);
                        Find.WorldPawns.PassToWorld(passenger);
                        if (kind == ContractKind.Diplomatic && TargetCiv != null) Civs.ChangePlayerOpinion(TargetCiv, 15f);
                        passenger = null;
                        return true;
                    }
                case ContractKind.ConvoyEscort:
                    if (!atTarget) return false;
                    if (phase == 0)
                    {
                        phase = 1;
                        phaseTick = now + GenDate.TicksPerDay;
                        if (Rand.Chance(0.6f)) ContractUtility.Ambush(map, 1f);
                        return false;
                    }
                    return now >= phaseTick && !inCombat;
                case ContractKind.PirateHunt:
                case ContractKind.BlockadeBreak:
                    if (!atTarget) return false;
                    if (phase == 0)
                    {
                        if (ContractUtility.Ambush(map, kind == ContractKind.PirateHunt ? 1.3f : 1.1f)) phase = 1;
                        return false;
                    }
                    if (inCombat) return false;
                    if (kind == ContractKind.BlockadeBreak && Colony != null) GameComponent_RimverseEvents.Instance.colonyHaltUntil.Remove(Colony.id);
                    return true;
                case ContractKind.ColonyDefense:
                    return phase == 1 && !inCombat;
                case ContractKind.CrewRescue:
                    {
                        if (!atTarget) return false;
                        IncidentDef d = DefDatabase<IncidentDef>.GetNamedSilentFail("RefugeePodCrash");
                        if (d != null)
                        {
                            IncidentParms p = StorytellerUtility.DefaultParmsNow(d.category, map);
                            p.forced = true;
                            d.Worker.TryExecute(p);
                        }
                        return true;
                    }
                case ContractKind.CargoRecovery:
                    if (phase == 0)
                    {
                        if (!atTarget) return false;
                        ContractUtility.Drop(map, ThingDef, count);
                        phase = 1;
                        return false;
                    }
                    if (loc == null || loc.Traveling || loc.systemKey != originKey || ContractUtility.CountOn(map, ThingDef) < count) return false;
                    ContractUtility.Take(map, ThingDef, count);
                    return true;
                case ContractKind.PlanetSurvey:
                    return u != null && u.GetScan(StarSystemId.FromKey(targetKey), body) >= ScanLevel.Exact;
                case ContractKind.SampleCollection:
                    return atTarget && loc.OnSurface && loc.bodyIndex == body;
                case ContractKind.RuinExpedition:
                    if (phase == 0 && atTarget && !loc.OnSurface && Faction.OfMechanoids != null)
                    {
                        phase = 1;
                        RimverseShip guard = ColonyUtility.PlayerShipOn(map);
                        if (guard != null && Rand.Chance(0.35f))
                        {
                            EnemyRoster.current = RaiderKind.Guardians;
                            ShipCombatUtility.StartEncounter(map, guard, Faction.OfMechanoids, 40f, TechLevel.Ultra, -2);
                            EnemyRoster.current = RaiderKind.Pirates;
                        }
                    }
                    if (!(atTarget && loc.OnSurface && loc.bodyIndex == body)) return false;
                    CivTechTrade.RareFromRuins(1500f);
                    return true;
                case ContractKind.RouteMap:
                    if (loc != null && !loc.Traveling && route != null && route.Contains(loc.systemKey) && !visited.Contains(loc.systemKey))
                    {
                        visited.Add(loc.systemKey);
                        u?.SetSystemScan(loc.SystemId, ScanLevel.Range);
                    }
                    return route != null && visited.Count >= route.Count;
                case ContractKind.Espionage:
                    {
                        if (!atTarget) return false;
                        if (phase == 0)
                        {
                            phase = 1;
                            phaseTick = now + 2 * GenDate.TicksPerDay;
                            if (Rand.Chance(ResearchGate.Done("Rimverse_Espionage") ? 0.1f : 0.25f))
                            {
                                if (TargetCiv != null) Civs.ChangePlayerOpinion(TargetCiv, -15f);
                                Messages.Message("Rimverse_ContractSpyCaught".Translate(TargetCiv?.ShownName ?? "?"), MessageTypeDefOf.NegativeEvent, false);
                                Fail();
                            }
                            return false;
                        }
                        if (now < phaseTick) return false;
                        Civilization t = TargetCiv;
                        if (t != null) t.known = true;
                        if (t != null && !t.capitalKey.NullOrEmpty()) u?.SetSystemScan(t.Capital, ScanLevel.Exact);
                        CivTechTrade.Steal(t);
                        return true;
                    }
            }
            return false;
        }

        private void Succeed(Map map)
        {
            ContractUtility.Drop(map, ThingDefOf.Silver, reward);
            Civilization g = Giver;
            if (g != null) Civs.ChangePlayerOpinion(g, 8f);
            if (State == QuestPartState.Enabled) Complete();
            quest.End(QuestEndOutcome.Success);
        }

        private void Fail()
        {
            if (quest.State != QuestState.Ongoing) return;
            Civilization g = Giver;
            if (g != null) Civs.ChangePlayerOpinion(g, -5f);
            if (passenger != null && !passenger.Dead && passenger.Spawned)
            {
                Faction f = g != null ? Civs.EnsureFaction(g) : null;
                passenger.DeSpawn();
                if (f != null) passenger.SetFaction(f);
                Find.WorldPawns.PassToWorld(passenger);
            }
            passenger = null;
            quest.End(QuestEndOutcome.Fail);
        }

        public override void Cleanup()
        {
            base.Cleanup();
            if (kind == ContractKind.BlockadeBreak && Colony != null && GameComponent_RimverseEvents.Instance != null) GameComponent_RimverseEvents.Instance.colonyHaltUntil.Remove(Colony.id);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref giverId, "giverId", -1);
            Scribe_Values.Look(ref targetCivId, "targetCivId", -1);
            Scribe_Values.Look(ref colonyId, "colonyId", -1);
            Scribe_Values.Look(ref originKey, "originKey");
            Scribe_Values.Look(ref targetKey, "targetKey");
            Scribe_Values.Look(ref body, "body", -1);
            Scribe_Values.Look(ref thingDefName, "thingDefName");
            Scribe_Values.Look(ref count, "count");
            Scribe_Values.Look(ref days, "days");
            Scribe_Values.Look(ref reward, "reward");
            Scribe_Collections.Look(ref route, "route", LookMode.Value);
            Scribe_Collections.Look(ref visited, "visited", LookMode.Value);
            Scribe_Values.Look(ref deadlineTick, "deadlineTick", -1);
            Scribe_Values.Look(ref phase, "phase");
            Scribe_Values.Look(ref phaseTick, "phaseTick", -1);
            Scribe_References.Look(ref passenger, "passenger");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && visited == null) visited = new List<string>();
        }
    }
}
