using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public enum EventScope
    {
        Ship,
        Travel,
        Explore,
        Civ,
        Colony
    }

    public class RimverseEventExtension : DefModExtension
    {
        public string kind;
        public EventScope scope;
    }

    public class GameComponent_RimverseEvents : GameComponent
    {
        public int sensorBlindUntil = -1;
        public Dictionary<int, int> colonyHaltUntil = new Dictionary<int, int>();
        public Dictionary<int, int> embargoUntil = new Dictionary<int, int>();
        public Dictionary<int, int> techSaleUntil = new Dictionary<int, int>();

        public static GameComponent_RimverseEvents Instance => Current.Game?.GetComponent<GameComponent_RimverseEvents>();

        public GameComponent_RimverseEvents(Game game)
        {
        }

        public static bool SensorsBlind => Instance != null && Find.TickManager.TicksGame < Instance.sensorBlindUntil;

        public static bool Halted(PlayerColony c) => Instance != null && Instance.colonyHaltUntil.TryGetValue(c.id, out int t) && Find.TickManager.TicksGame < t;

        public static bool Embargoed(Civilization c) => c != null && Instance != null && Instance.embargoUntil.TryGetValue(c.id, out int t) && Find.TickManager.TicksGame < t;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref sensorBlindUntil, "sensorBlindUntil", -1);
            Scribe_Collections.Look(ref colonyHaltUntil, "colonyHaltUntil", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref embargoUntil, "embargoUntil", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref techSaleUntil, "techSaleUntil", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (colonyHaltUntil == null) colonyHaltUntil = new Dictionary<int, int>();
                if (embargoUntil == null) embargoUntil = new Dictionary<int, int>();
                if (techSaleUntil == null) techSaleUntil = new Dictionary<int, int>();
            }
        }
    }

    public class IncidentWorker_RimverseEvent : IncidentWorker
    {
        private RimverseEventExtension Ext => def.GetModExtension<RimverseEventExtension>();

        private static RimverseShip Ship(Map map) => map == null ? null : ColonyUtility.PlayerShipOn(map);

        private static MapComponent_ShipLocation Loc(Map map) => map == null ? null : MapComponent_ShipLocation.For(map);

        private static GameComponent_Civilizations Civs => GameComponent_Civilizations.Instance;

        private static List<PlayerColony> Colonies => GameComponent_PlayerColonies.Instance?.colonies ?? new List<PlayerColony>();

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            RimverseEventExtension ext = Ext;
            if (ext == null) return false;
            Map map = parms.target as Map;
            switch (ext.scope)
            {
                case EventScope.Ship:
                    return Ship(map) != null && Ready(ext.kind, map);
                case EventScope.Travel:
                    {
                        MapComponent_ShipLocation loc = Loc(map);
                        if (Ship(map) == null || loc == null || loc.OnSurface) return false;
                        if (ext.kind == "WarpDeviation") return loc.Warping && loc.Traveling;
                        return true;
                    }
                case EventScope.Explore:
                    {
                        MapComponent_ShipLocation loc = Loc(map);
                        return Ship(map) != null && loc != null && !loc.systemKey.NullOrEmpty();
                    }
                case EventScope.Civ:
                    return Civs != null && Civs.generated && Civs.Known.Any() && map != null;
                case EventScope.Colony:
                    return Colonies.Count > 0 && map != null;
            }
            return false;
        }

        private static bool Ready(string kind, Map map)
        {
            RimverseShip s = Ship(map);
            switch (kind)
            {
                case "AirRecyclerFailure": return Parts(s, ShipPartRole.AirGenerator).Any();
                case "FuelLeak": return Parts(s, ShipPartRole.FuelTank).Any();
                case "GravityFailure": return Parts(s, ShipPartRole.Gravity).Any();
                case "Sabotage": return s.parts.Any(p => p.parent.GetComp<CompBreakdownable>() != null);
            }
            return true;
        }

        private static IEnumerable<CompShipPart> Parts(RimverseShip s, ShipPartRole role) => s?.parts.Where(p => p != null && p.parent.Spawned && p.Role == role) ?? Enumerable.Empty<CompShipPart>();

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = parms.target as Map;
            string kind = Ext.kind;
            string text = null;
            LookTargets look = null;
            LetterDef letter = def.letterDef ?? LetterDefOf.NeutralEvent;
            switch (Ext.scope)
            {
                case EventScope.Ship: text = ShipEvent(kind, map, ref look); break;
                case EventScope.Travel: text = TravelEvent(kind, map, parms, ref look); break;
                case EventScope.Explore: text = ExploreEvent(kind, map, ref look); break;
                case EventScope.Civ: text = CivEvent(kind, map, parms, ref look); break;
                case EventScope.Colony: text = ColonyEvent(kind, map); break;
            }
            if (text == null) return false;
            Find.LetterStack.ReceiveLetter(def.letterLabel.NullOrEmpty() ? def.LabelCap.ToString() : def.letterLabel, text, letter, look);
            return true;
        }

        private string ShipEvent(string kind, Map map, ref LookTargets look)
        {
            RimverseShip s = Ship(map);
            List<IntVec3> cells = s.cells.ToList();
            switch (kind)
            {
                case "Micrometeors":
                    {
                        int hits = Rand.RangeInclusive(4, 10);
                        List<Thing> hit = new List<Thing>();
                        for (int i = 0; i < hits && cells.Count > 0; i++)
                        {
                            IntVec3 c = cells.RandomElement();
                            Building b = c.GetEdifice(map);
                            if (b == null) continue;
                            b.TakeDamage(new DamageInfo(DamageDefOf.Bomb, Rand.Range(20f, 60f)));
                            hit.Add(b);
                        }
                        look = new LookTargets(hit);
                        return def.letterText.Formatted(hits);
                    }
                case "SolarStorm":
                    {
                        GameConditionDef flare = DefDatabase<GameConditionDef>.GetNamedSilentFail("SolarFlare");
                        int duration = Mathf.RoundToInt(Rand.Range(0.5f, 1f) * GenDate.TicksPerDay);
                        if (flare != null) map.gameConditionManager.RegisterCondition(GameConditionMaker.MakeCondition(flare, duration));
                        foreach (Pawn p in map.mapPawns.AllPawnsSpawned.Where(p => p.RaceProps.IsFlesh).ToList())
                        {
                            float take = Rand.Range(0.05f, 0.15f) * (1f - GameComponent_Radiation.Resistance(p));
                            if (take > 0.001f) HealthUtility.AdjustSeverity(p, RadiationDefOf.Rimverse_RadiationSickness, take);
                        }
                        return def.letterText;
                    }
                case "AirRecyclerFailure":
                case "GravityFailure":
                    {
                        ShipPartRole role = kind == "AirRecyclerFailure" ? ShipPartRole.AirGenerator : ShipPartRole.Gravity;
                        CompShipPart part = Parts(s, role).RandomElementWithFallback();
                        if (part == null) return null;
                        CompBreakdownable br = part.parent.GetComp<CompBreakdownable>();
                        if (br != null && !br.BrokenDown) br.DoBreakdown();
                        else part.parent.TakeDamage(new DamageInfo(DamageDefOf.Bomb, part.parent.MaxHitPoints * 0.4f));
                        look = part.parent;
                        return def.letterText.Formatted(part.parent.LabelShort);
                    }
                case "FuelLeak":
                    {
                        CompShipPart part = Parts(s, ShipPartRole.FuelTank).RandomElement();
                        CompRefuelable fuel = part.parent.GetComp<CompRefuelable>();
                        if (fuel != null) fuel.ConsumeFuel(fuel.Fuel * Rand.Range(0.1f, 0.3f));
                        if (Rand.Chance(0.3f)) FireUtility.TryStartFireIn(part.parent.Position, map, 0.3f, null);
                        look = part.parent;
                        return def.letterText;
                    }
                case "HeatSpike":
                    {
                        s.state.heat += Mathf.Max(50f, s.heatCapacity * Rand.Range(0.25f, 0.45f));
                        IntVec3 c = s.bridge.Position;
                        GenTemperature.PushHeat(c, map, 400f);
                        look = s.bridge;
                        return def.letterText;
                    }
                case "ElectricFire":
                    {
                        Building b = map.listerBuildings.allBuildingsColonist.Where(x => x.GetComp<CompPowerTrader>() != null && s.cells.Contains(x.Position)).RandomElementWithFallback();
                        if (b == null) return null;
                        FireUtility.TryStartFireIn(b.Position, map, 0.5f, null);
                        look = b;
                        return def.letterText.Formatted(b.LabelShort);
                    }
                case "HoldPest":
                    {
                        PawnKindDef rat = DefDatabase<PawnKindDef>.GetNamedSilentFail("Rimverse_Fauna_HoldRat");
                        if (rat == null) return null;
                        IntVec3 spot = cells.Where(c => c.Standable(map)).RandomElementWithFallback(map.Center);
                        int n = Rand.RangeInclusive(3, 8);
                        List<Pawn> pawns = new List<Pawn>();
                        for (int i = 0; i < n; i++) pawns.Add((Pawn)GenSpawn.Spawn(PawnGenerator.GeneratePawn(rat), CellFinder.RandomClosewalkCellNear(spot, map, 4), map));
                        look = new LookTargets(pawns);
                        return def.letterText.Formatted(n);
                    }
                case "Sabotage":
                    {
                        CompShipPart part = s.parts.Where(p => p.parent.GetComp<CompBreakdownable>() != null).RandomElement();
                        part.parent.GetComp<CompBreakdownable>().DoBreakdown();
                        Pawn culprit = map.mapPawns.PrisonersOfColonySpawned.RandomElementWithFallback() ?? map.mapPawns.FreeColonistsSpawned.Where(p => p.needs?.mood != null).OrderBy(p => p.needs.mood.CurLevel).FirstOrDefault();
                        look = part.parent;
                        return def.letterText.Formatted(part.parent.LabelShort, culprit?.LabelShort ?? "?");
                    }
            }
            return null;
        }

        private static bool RunVanilla(string defName, Map map)
        {
            IncidentDef d = DefDatabase<IncidentDef>.GetNamedSilentFail(defName);
            if (d == null) return false;
            IncidentParms p = StorytellerUtility.DefaultParmsNow(d.category, map);
            p.forced = true;
            return d.Worker.TryExecute(p);
        }

        private static List<Thing> DropNear(Map map, ThingDef d, int count)
        {
            List<Thing> list = new List<Thing>();
            if (d == null || count <= 0) return list;
            while (count > 0)
            {
                Thing t = ThingMaker.MakeThing(d, d.MadeFromStuff ? GenStuff.DefaultStuffFor(d) : null);
                t.stackCount = Mathf.Min(count, d.stackLimit);
                count -= t.stackCount;
                list.Add(t);
            }
            DropPodUtility.DropThingsNear(DropCellFinder.TradeDropSpot(map), map, list, 110, false, false, true);
            return list;
        }

        private string TravelEvent(string kind, Map map, IncidentParms parms, ref LookTargets look)
        {
            MapComponent_ShipLocation loc = Loc(map);
            switch (kind)
            {
                case "DistressSignal":
                    if (!RunVanilla("RefugeePodCrash", map)) DropNear(map, ThingDefOf.MedicineIndustrial, 6);
                    return def.letterText;
                case "DriftingShip":
                    DropNear(map, ThingDefOf.Steel, Rand.RangeInclusive(80, 200));
                    DropNear(map, ThingDefOf.ComponentIndustrial, Rand.RangeInclusive(3, 8));
                    if (Rand.Chance(0.4f)) RunVanilla("RefugeePodCrash", map);
                    return def.letterText;
                case "DebrisField":
                    {
                        RimverseShip s = Ship(map);
                        int hits = Rand.RangeInclusive(2, 6);
                        foreach (IntVec3 c in s.cells.InRandomOrder().Take(hits))
                        {
                            Building b = c.GetEdifice(map);
                            if (b != null) b.TakeDamage(new DamageInfo(DamageDefOf.Blunt, Rand.Range(15f, 40f)));
                        }
                        DropNear(map, ThingDefOf.Steel, Rand.RangeInclusive(60, 150));
                        DropNear(map, ThingDefOf.Plasteel, Rand.RangeInclusive(10, 40));
                        return def.letterText;
                    }
                case "WarpDeviation":
                    {
                        int delay = Mathf.RoundToInt(Rand.Range(1f, 3f) * GenDate.TicksPerDay);
                        loc.arriveTick += delay;
                        return def.letterText.Formatted((delay / (float)GenDate.TicksPerDay).ToString("0.#"));
                    }
                case "ConvoyEscort":
                    if (!RunVanilla("OrbitalTraderArrival", map)) DropNear(map, ThingDefOf.Silver, 300);
                    DropNear(map, ThingDefOf.Silver, Rand.RangeInclusive(150, 400));
                    return def.letterText;
                case "VoidWhale":
                    {
                        PawnKindDef whale = DefDatabase<PawnKindDef>.GetNamedSilentFail("Rimverse_Fauna_VoidWhale");
                        if (whale == null) return null;
                        IntVec3 at;
                        if (!RCellFinder.TryFindRandomPawnEntryCell(out at, map, CellFinder.EdgeRoadChance_Animal)) at = CellFinder.RandomEdgeCell(map);
                        Pawn p = (Pawn)GenSpawn.Spawn(PawnGenerator.GeneratePawn(whale), at, map);
                        look = p;
                        return def.letterText;
                    }
                case "SensorNebula":
                    {
                        int days = Rand.RangeInclusive(2, 5);
                        GameComponent_RimverseEvents.Instance.sensorBlindUntil = Find.TickManager.TicksGame + days * GenDate.TicksPerDay;
                        ShipThoughts.GiveMap(map, "SawNebula");
                        return def.letterText.Formatted(days);
                    }
            }
            return null;
        }

        private static bool AddAnomaly(string systemKey, int body, string defName)
        {
            GameComponent_PlayerEmpire e = GameComponent_PlayerEmpire.Instance;
            SpaceAnomalyDef d = DefDatabase<SpaceAnomalyDef>.GetNamedSilentFail(defName) ?? DefDatabase<SpaceAnomalyDef>.AllDefsListForReading.RandomElementWithFallback();
            if (e == null || d == null) return false;
            e.anomalies.Add(new FoundAnomaly { def = d, systemKey = systemKey, body = body });
            return true;
        }

        private string ExploreEvent(string kind, Map map, ref LookTargets look)
        {
            MapComponent_ShipLocation loc = Loc(map);
            GameComponent_Universe u = GameComponent_Universe.Instance;
            StarSystemId id = loc.SystemId;
            StarSystemData sys = u?.GetSystem(id);
            if (sys == null || sys.bodies.Count == 0) return null;
            BodyData body = sys.bodies.RandomElement();
            if (kind != "DriftingCryoPod" && kind != "UnknownProbe") ShipThoughts.GiveMap(map, "DiscoveredAnomaly");
            switch (kind)
            {
                case "Anomaly":
                    AddAnomaly(id.Key, body.index, null);
                    return def.letterText.Formatted(body.name);
                case "PrecursorRuin":
                    AddAnomaly(id.Key, body.index, "Rimverse_Anomaly_PrecursorRuins");
                    CivTechTrade.RareFromRuins(300f);
                    return def.letterText.Formatted(body.name);
                case "AncientSignal":
                    AddAnomaly(id.Key, body.index, "Rimverse_Anomaly_AncientBeacon");
                    return def.letterText.Formatted(body.name);
                case "AbandonedStation":
                    AddAnomaly(id.Key, body.index, "Rimverse_Anomaly_DerelictColonyShip");
                    return def.letterText.Formatted(body.name);
                case "UncatalogedPlanet":
                    {
                        BodyData b = sys.bodies.Where(x => u.GetScan(id, x.index) < ScanLevel.Exact).RandomElementWithFallback() ?? body;
                        u.SetScan(id, b.index, ScanLevel.Exact);
                        GameComponent_RareFinds.MakeRecords(id, 5);
                        return def.letterText.Formatted(b.name);
                    }
                case "DriftingCryoPod":
                    if (!RunVanilla("RefugeePodCrash", map)) return null;
                    return def.letterText;
                case "UnknownProbe":
                    {
                        if (Find.ResearchManager.GetProject() != null) Find.ResearchManager.AddProgress(Find.ResearchManager.GetProject(), 300f);
                        DropNear(map, DefDatabase<ThingDef>.GetNamedSilentFail("Rimverse_Probe"), 1);
                        GameComponent_RareFinds.MakeRecords(id, 8);
                        return def.letterText;
                    }
                case "NewLifeWorld":
                    {
                        BodyData b = sys.bodies.Where(x => x.atmosphere != AtmosphereType.None && x.Landable).RandomElementWithFallback() ?? body;
                        u.SetScan(id, b.index, ScanLevel.Exact);
                        AddAnomaly(id.Key, b.index, "Rimverse_Anomaly_AlienGarden");
                        return def.letterText.Formatted(b.name);
                    }
                case "DormantGate":
                    DropNear(map, DefDatabase<ThingDef>.GetNamedSilentFail("Rimverse_GatePart"), Rand.RangeInclusive(2, 4));
                    return def.letterText.Formatted(body.name);
            }
            return null;
        }

        private static Civilization NearestKnown(System.Func<Civilization, bool> ok)
        {
            GameComponent_Civilizations gc = Civs;
            return gc.Known.Where(c => !c.NoDiplomacy && ok(c)).OrderBy(c => gc.DistanceToPlayer(c, out _)).FirstOrDefault();
        }

        private static Pawn JoinPawn(Map map, Civilization c, bool prisoner)
        {
            Faction f = c != null ? Civs.EnsureFaction(c) : null;
            PawnKindDef kind = f?.def.basicMemberKind ?? PawnKindDefOf.Colonist;
            Pawn p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, prisoner ? f : null, PawnGenerationContext.NonPlayer, -1, true));
            IntVec3 at = CellFinder.RandomClosewalkCellNear(DropCellFinder.TradeDropSpot(map), map, 6);
            GenSpawn.Spawn(p, at, map);
            if (prisoner) p.guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Prisoner);
            else if (p.Faction != Faction.OfPlayer) p.SetFaction(Faction.OfPlayer);
            return p;
        }

        private string CivEvent(string kind, Map map, IncidentParms parms, ref LookTargets look)
        {
            GameComponent_Civilizations gc = Civs;
            switch (kind)
            {
                case "FirstContact":
                    {
                        Civilization c = gc.Alive.Where(x => !x.known && !x.NoDiplomacy).OrderBy(x => gc.DistanceToPlayer(x, out _)).FirstOrDefault();
                        if (c == null) return null;
                        gc.MakeContact(c, false);
                        return def.letterText.Formatted(c.ShownName);
                    }
                case "Asylum":
                    {
                        Civilization c = NearestKnown(x => true);
                        Pawn p = JoinPawn(map, c, false);
                        if (c != null) gc.ChangePlayerOpinion(c, -5f);
                        look = p;
                        return def.letterText.Formatted(p.LabelShort, c?.ShownName ?? "?");
                    }
                case "VassalOffer":
                    {
                        Civilization c = NearestKnown(x => x.stage <= 10 && x.playerOpinion > 0f);
                        if (c == null) return null;
                        gc.ChangePlayerOpinion(c, 20f);
                        if (c.playerState < DiploState.Alliance) gc.SetPlayerState(c, DiploState.Alliance);
                        return def.letterText.Formatted(c.ShownName);
                    }
                case "Embargo":
                    {
                        Civilization c = NearestKnown(x => x.playerOpinion < 20f);
                        if (c == null) return null;
                        GameComponent_RimverseEvents.Instance.embargoUntil[c.id] = Find.TickManager.TicksGame + 30 * GenDate.TicksPerDay;
                        gc.ChangePlayerOpinion(c, -10f);
                        return def.letterText.Formatted(c.ShownName);
                    }
                case "NeighborWar":
                    {
                        List<Civilization> known = gc.Known.Where(x => !x.NoDiplomacy).ToList();
                        if (known.Count < 2) return null;
                        Civilization a = known.RandomElement();
                        Civilization b = known.Where(x => x != a).OrderBy(x => gc.DistanceLy(a, StarSystemId.FromKey(x.capitalKey))).First();
                        gc.SetState(a, b, DiploState.War);
                        return def.letterText.Formatted(a.ShownName, b.ShownName);
                    }
                case "AllyCoup":
                    {
                        Civilization c = NearestKnown(x => x.playerState >= DiploState.Alliance) ?? NearestKnown(x => x.playerOpinion > 20f);
                        if (c == null) return null;
                        PolicyDef gov = CivilizationGenerator.Pick(new UniverseRand(UniverseRand.Hash(c.id, Find.TickManager.TicksGame)), PolicyGroup.Government, c.tech, c.stage, false, p => p.profile == AiProfile.Warmonger || p.profile == AiProfile.Aggressive ? 4f : 0.3f);
                        if (gov != null) c.government = gov;
                        c.stability = Mathf.Max(0f, c.stability - 15f);
                        gc.ChangePlayerOpinion(c, -25f);
                        return def.letterText.Formatted(c.ShownName, gov?.label ?? "?");
                    }
                case "MassRefugees":
                    {
                        Civilization c = NearestKnown(x => x.stability < 50f) ?? NearestKnown(x => true);
                        int n = Rand.RangeInclusive(2, 4);
                        List<Pawn> list = new List<Pawn>();
                        for (int i = 0; i < n; i++) list.Add(JoinPawn(map, c, false));
                        look = new LookTargets(list);
                        return def.letterText.Formatted(n, c?.ShownName ?? "?");
                    }
                case "FederationInvite":
                    {
                        Civilization c = NearestKnown(x => x.playerOpinion > 30f);
                        if (c == null) return null;
                        gc.ChangePlayerOpinion(c, 15f);
                        if (c.playerState < DiploState.NonAggression) gc.SetPlayerState(c, DiploState.NonAggression);
                        return def.letterText.Formatted(c.ShownName);
                    }
                case "DynasticMarriage":
                    {
                        Civilization c = NearestKnown(x => x.playerOpinion > 10f);
                        if (c == null) return null;
                        Pawn p = JoinPawn(map, c, false);
                        gc.ChangePlayerOpinion(c, 25f);
                        look = p;
                        return def.letterText.Formatted(p.LabelShort, c.ShownName);
                    }
                case "SpyCaught":
                    {
                        Civilization c = NearestKnown(x => true);
                        if (c == null) return null;
                        Pawn p = JoinPawn(map, c, true);
                        gc.ChangePlayerOpinion(c, -15f);
                        look = p;
                        return def.letterText.Formatted(c.ShownName);
                    }
            }
            return null;
        }

        private static readonly string[] AntiAIIdeas = { "Rimverse_Policy_Humanism", "Rimverse_Policy_Traditionalism", "Rimverse_Policy_ReligiousZeal", "Rimverse_Policy_Environmentalism" };

        public static bool AntiAI(Civilization c) => c?.ideology != null && AntiAIIdeas.Contains(c.ideology.defName);

        private string ColonyEvent(string kind, Map map)
        {
            PlayerColony c = Colonies.RandomElement();
            GameComponent_RimverseEvents ev = GameComponent_RimverseEvents.Instance;
            switch (kind)
            {
                case "Strike":
                    ev.colonyHaltUntil[c.id] = Find.TickManager.TicksGame + Rand.RangeInclusive(5, 10) * GenDate.TicksPerDay;
                    c.stability = Mathf.Max(0f, c.stability - 5f);
                    return def.letterText.Formatted(c.name);
                case "MiningBoom":
                    c.AddStock("Steel", Rand.RangeInclusive(300, 800));
                    c.AddStock("Plasteel", Rand.RangeInclusive(30, 120));
                    c.population *= 1.05f;
                    return def.letterText.Formatted(c.name);
                case "Epidemic":
                    c.population = Mathf.Max(1f, c.population * Rand.Range(0.75f, 0.9f));
                    c.stability = Mathf.Max(0f, c.stability - 10f);
                    return def.letterText.Formatted(c.name);
                case "Independence":
                    c = Colonies.OrderBy(x => x.stability).First();
                    c.stability = Mathf.Max(0f, c.stability - 15f);
                    return def.letterText.Formatted(c.name);
                case "Blockade":
                    ev.colonyHaltUntil[c.id] = Find.TickManager.TicksGame + Rand.RangeInclusive(7, 15) * GenDate.TicksPerDay;
                    c.attention += 10f;
                    return def.letterText.Formatted(c.name);
                case "AICoreInspection":
                    {
                        if (!GameComponent_ColonyExtras.HasAI(c))
                        {
                            PlayerColony withAi = Colonies.FirstOrDefault(GameComponent_ColonyExtras.HasAI);
                            if (withAi == null) return null;
                            c = withAi;
                        }
                        GameComponent_Civilizations gc = Civs;
                        if (gc != null) foreach (Civilization civ in gc.Known.Where(x => AntiAI(x)).ToList()) gc.ChangePlayerOpinion(civ, -10f);
                        c.stability = Mathf.Max(0f, c.stability - 5f);
                        return def.letterText.Formatted(c.name);
                    }
            }
            return null;
        }
    }
}
