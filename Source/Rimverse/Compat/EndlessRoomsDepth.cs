using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Rimverse
{
    public class GameComponent_RoomsBridge : GameComponent
    {
        private HashSet<int> inRooms = new HashSet<int>();
        public List<Pawn> strayPawns = new List<Pawn>();
        public List<string> strayKeys = new List<string>();
        public List<int> entityTicks = new List<int>();
        public int expeditionUntil = -1;
        public Pawn researcher;
        public string impossibleKey;

        public static GameComponent_RoomsBridge Instance => Current.Game?.GetComponent<GameComponent_RoomsBridge>();

        public GameComponent_RoomsBridge(Game game)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref inRooms, "inRooms", LookMode.Value);
            Scribe_Collections.Look(ref strayPawns, "strayPawns", LookMode.Reference);
            Scribe_Collections.Look(ref strayKeys, "strayKeys", LookMode.Value);
            Scribe_Collections.Look(ref entityTicks, "entityTicks", LookMode.Value);
            Scribe_Values.Look(ref expeditionUntil, "expeditionUntil", -1);
            Scribe_References.Look(ref researcher, "researcher", true);
            Scribe_Values.Look(ref impossibleKey, "impossibleKey");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (inRooms == null) inRooms = new HashSet<int>();
                if (strayPawns == null) strayPawns = new List<Pawn>();
                if (strayKeys == null) strayKeys = new List<string>();
                if (entityTicks == null) entityTicks = new List<int>();
                strayPawns.RemoveAll(p => p == null);
                while (strayKeys.Count > strayPawns.Count) strayKeys.RemoveAt(strayKeys.Count - 1);
            }
        }

        public override void GameComponentTick()
        {
            if (!EndlessRoomsCompat.Active || Find.TickManager.TicksGame % 250 != 77) return;
            int now = Find.TickManager.TicksGame;
            HashSet<int> seen = new HashSet<int>();
            foreach (Map m in Find.Maps)
            {
                bool rooms = EndlessRoomsCompat.IsBackrooms(m);
                foreach (Pawn p in m.mapPawns.FreeColonistsSpawned.ToList())
                {
                    if (rooms) seen.Add(p.thingIDNumber);
                    else if (inRooms.Contains(p.thingIDNumber)) Returned(p);
                }
                if (rooms && researcher != null && m.mapPawns.FreeColonistsSpawned.Any(c => c != researcher) && researcher.Spawned && researcher.Map == m) RescueResearcher();
            }
            inRooms = seen;
            for (int i = entityTicks.Count - 1; i >= 0; i--)
            {
                if (now < entityTicks[i]) continue;
                entityTicks.RemoveAt(i);
                SpawnStowaway();
            }
            if (expeditionUntil > 0 && now > expeditionUntil)
            {
                expeditionUntil = -1;
                Messages.Message("Rimverse_AsyncExpeditionFailed".Translate(), MessageTypeDefOf.NegativeEvent, false);
            }
            CheckStrays();
            CheckImpossible();
            if (now % 60000 == 77 && EndlessRoomsCompat.AnyLost && Rand.Chance(0.15f)) WreckInRooms();
        }

        private void Returned(Pawn p)
        {
            HediffDef ghost = DefDatabase<HediffDef>.GetNamedSilentFail("Rimverse_RoomsGhost");
            if (ghost != null) p.health.AddHediff(ghost);
            ResearchProjectDef r = DefDatabase<ResearchProjectDef>.GetNamedSilentFail("Rimverse_BackroomsStudies");
            if (r != null && !r.IsFinished) Find.ResearchManager.AddProgress(r, 250f);
            string key = RandomPlanetKey();
            if (key != null)
            {
                GameComponent_Universe.Instance?.SetSystemScan(StarSystemId.FromKey(key), ScanLevel.Exact);
                Messages.Message("Rimverse_RoomsReturnedCoords".Translate(p.LabelShort, ContractUtility.SystemName(key)), p, MessageTypeDefOf.PositiveEvent, false);
            }
            if (expeditionUntil > 0)
            {
                expeditionUntil = -1;
                Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                silver.stackCount = 1500;
                GenPlace.TryPlaceThing(silver, p.Position, p.Map, ThingPlaceMode.Near);
                Civilization a = AsyncCiv();
                if (a != null) GameComponent_Civilizations.Instance.ChangePlayerOpinion(a, 10f);
                Find.LetterStack.ReceiveLetter("Rimverse_AsyncExpeditionDoneLabel".Translate(), "Rimverse_AsyncExpeditionDone".Translate(p.LabelShort), LetterDefOf.PositiveEvent, p);
            }
            if (Rand.Chance(0.1f)) entityTicks.Add(Find.TickManager.TicksGame + Rand.RangeInclusive(2, 5) * GenDate.TicksPerDay);
            if (key != null && Rand.Chance(0.08f) && p.Map.mapPawns.FreeColonistsSpawnedCount > 1)
            {
                Map from = p.Map;
                p.DeSpawn();
                Find.WorldPawns.PassToWorld(p, RimWorld.Planet.PawnDiscardDecideMode.KeepForever);
                strayPawns.Add(p);
                strayKeys.Add(key);
                Find.LetterStack.ReceiveLetter("Rimverse_RoomsWrongReturnLabel".Translate(), "Rimverse_RoomsWrongReturn".Translate(p.LabelShort, ContractUtility.SystemName(key)), LetterDefOf.NegativeEvent, new TargetInfo(from.Center, from));
            }
        }

        private static string RandomPlanetKey()
        {
            GameComponent_Civilizations gc = GameComponent_Civilizations.Instance;
            return gc?.civilizations?.Where(c => !c.dead).SelectMany(c => c.planets).Select(pl => pl.systemKey).Where(k => !k.NullOrEmpty()).RandomElementWithFallback();
        }

        public static Civilization AsyncCiv() => GameComponent_Civilizations.Instance?.civilizations?.FirstOrDefault(c => !c.dead && c.HasCivic("Rimverse_Policy_CivicAsync"));

        private void CheckStrays()
        {
            if (strayPawns.Count == 0) return;
            for (int i = strayPawns.Count - 1; i >= 0; i--)
            {
                foreach (Map m in Find.Maps)
                {
                    MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(m);
                    if (loc == null || loc.Traveling || loc.systemKey != strayKeys[i] || ColonyUtility.PlayerShipOn(m) == null) continue;
                    Pawn p = strayPawns[i];
                    strayPawns.RemoveAt(i);
                    strayKeys.RemoveAt(i);
                    if (p.Dead || p.Spawned) break;
                    if (Find.WorldPawns.Contains(p)) Find.WorldPawns.RemovePawn(p);
                    IntVec3 at = DropCellFinder.TradeDropSpot(m);
                    GenSpawn.Spawn(p, at, m);
                    Find.LetterStack.ReceiveLetter("Rimverse_RoomsStrayFoundLabel".Translate(), "Rimverse_RoomsStrayFound".Translate(p.LabelShort), LetterDefOf.PositiveEvent, p);
                    break;
                }
            }
        }

        private void SpawnStowaway()
        {
            Map map = Find.Maps.Where(m => m.IsPlayerHome && SpaceMapUtility.IsSpace(m)).RandomElementWithFallback() ?? Find.AnyPlayerHomeMap;
            if (map == null) return;
            PawnKindDef kind = DefDatabase<PawnKindDef>.AllDefsListForReading.Where(k => k.race != null && !k.RaceProps.Humanlike && k.modContentPack != null && k.modContentPack.PackageId.ToLowerInvariant().Contains("endlessrooms")).RandomElementWithFallback()
                ?? DefDatabase<PawnKindDef>.GetNamedSilentFail("Rimverse_Fauna_Glassfang");
            if (kind == null) return;
            if (!RCellFinder.TryFindRandomCellNearTheCenterOfTheMapWith(c => c.Standable(map) && c.Roofed(map) && !c.Fogged(map), map, out IntVec3 at)) return;
            Pawn e = PawnGenerator.GeneratePawn(kind, null);
            GenSpawn.Spawn(e, at, map);
            e.mindState?.mentalStateHandler?.TryStartMentalState(MentalStateDefOf.ManhunterPermanent);
            Find.LetterStack.ReceiveLetter("Rimverse_RoomsStowawayLabel".Translate(), "Rimverse_RoomsStowaway".Translate(), LetterDefOf.ThreatSmall, e);
        }

        private void WreckInRooms()
        {
            Pawn p = EndlessRoomsCompat.LostColonists().RandomElementWithFallback();
            if (p == null) return;
            foreach (ThingDef d in new[] { ThingDefOf.ComponentIndustrial, ThingDefOf.Plasteel, ThingDefOf.Steel })
            {
                if (d == null) continue;
                Thing t = ThingMaker.MakeThing(d);
                t.stackCount = System.Math.Max(1, System.Math.Min(d.stackLimit, Rand.RangeInclusive(3, 40)));
                GenPlace.TryPlaceThing(t, p.Position, p.Map, ThingPlaceMode.Near);
            }
            Messages.Message("Rimverse_RoomsWreck".Translate(p.LabelShort), p, MessageTypeDefOf.PositiveEvent, false);
        }

        private void RescueResearcher()
        {
            Pawn r = researcher;
            researcher = null;
            if (r.Faction != Faction.OfPlayer) r.SetFaction(Faction.OfPlayer);
            Civilization a = AsyncCiv();
            if (a != null) GameComponent_Civilizations.Instance.ChangePlayerOpinion(a, 20f);
            Find.LetterStack.ReceiveLetter("Rimverse_AsyncRescueLabel".Translate(), "Rimverse_AsyncRescue".Translate(r.LabelShort), LetterDefOf.PositiveEvent, r);
        }

        private void CheckImpossible()
        {
            if (impossibleKey.NullOrEmpty()) return;
            foreach (Map m in Find.Maps)
            {
                MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(m);
                if (loc == null || loc.Traveling || loc.systemKey != impossibleKey || ColonyUtility.PlayerShipOn(m) == null) continue;
                impossibleKey = null;
                RoomsDoor.SpawnOn(m, true);
                return;
            }
        }

        public static void EnsureAsyncCiv(GameComponent_Civilizations gc)
        {
            if (!(ModCompat.EndlessRooms) || gc?.civilizations == null || AsyncCiv() != null) return;
            PolicyDef civic = DefDatabase<PolicyDef>.GetNamedSilentFail("Rimverse_Policy_CivicAsync");
            Civilization c = gc.civilizations.Where(x => !x.dead && !x.Hostile && !x.NoDiplomacy && x.stage >= 8).OrderBy(x => x.sizeClass).FirstOrDefault();
            if (civic == null || c == null) return;
            c.civics.Add(civic);
            c.lineage = "Rimverse_Lineage_Wanderer";
        }
    }

    public static class RoomsDoor
    {
        public static void SpawnOn(Map map, bool letter)
        {
            ThingDef d = DefDatabase<ThingDef>.GetNamedSilentFail("Rimverse_RoomsDoor");
            if (d == null || map == null) return;
            IntVec3 at = IntVec3.Invalid;
            foreach (IntVec3 c in map.AllCells.InRandomOrder().Take(4000))
            {
                if (!c.Standable(map) || !c.Roofed(map) || c.GetFirstBuilding(map) != null) continue;
                if (!GenAdj.CellsAdjacentCardinal(c, Rot4.North, IntVec2.One).Any(n => n.InBounds(map) && n.GetEdifice(map) is Building b && b.def.building?.isNaturalRock != true && b.def.passability == Traversability.Impassable)) continue;
                at = c;
                break;
            }
            if (!at.IsValid) return;
            GenSpawn.Spawn(ThingMaker.MakeThing(d), at, map);
            if (letter) Find.LetterStack.ReceiveLetter("Rimverse_RoomsDoorLabel".Translate(), "Rimverse_RoomsDoor".Translate(), LetterDefOf.NeutralEvent, new TargetInfo(at, map));
        }
    }

    public class CompProperties_RoomsDoor : CompProperties
    {
        public int lifeTicks = 120000;

        public CompProperties_RoomsDoor()
        {
            compClass = typeof(CompRoomsDoor);
        }
    }

    public class CompRoomsDoor : ThingComp
    {
        private int born = -1;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref born, "born", -1);
        }

        public override void CompTick()
        {
            base.CompTick();
            if (!parent.IsHashIntervalTick(60)) return;
            if (born < 0) born = Find.TickManager.TicksGame;
            if (Find.TickManager.TicksGame - born > ((CompProperties_RoomsDoor)props).lifeTicks)
            {
                parent.Destroy();
                return;
            }
            Pawn p = parent.Position.GetFirstPawn(parent.Map);
            if (p != null && p.RaceProps.Humanlike && !p.Downed) EndlessRoomsCompat.Drop(p);
        }

        public override string CompInspectStringExtra()
        {
            if (born < 0) return null;
            int left = ((CompProperties_RoomsDoor)props).lifeTicks - (Find.TickManager.TicksGame - born);
            return "Rimverse_RoomsDoorInspect".Translate(left.ToStringTicksToPeriod());
        }
    }

    public class IncidentWorker_RoomsDoor : IncidentWorker_RoomsShip
    {
        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            RoomsDoor.SpawnOn((Map)parms.target, true);
            return true;
        }
    }

    public class IncidentWorker_AsyncExpedition : IncidentWorker_RoomsShip
    {
        protected override bool CanFireNowSub(IncidentParms parms) => base.CanFireNowSub(parms) && GameComponent_RoomsBridge.AsyncCiv() != null && GameComponent_RoomsBridge.Instance?.expeditionUntil < 0;

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            GameComponent_RoomsBridge.Instance.expeditionUntil = Find.TickManager.TicksGame + 15 * GenDate.TicksPerDay;
            SendStandardLetter(def.letterLabel, "Rimverse_AsyncExpedition".Translate(GameComponent_RoomsBridge.AsyncCiv().ShownName), def.letterDef, parms, LookTargets.Invalid);
            return true;
        }
    }

    public class IncidentWorker_AsyncRescue : IncidentWorker_RoomsShip
    {
        protected override bool CanFireNowSub(IncidentParms parms) => base.CanFireNowSub(parms) && GameComponent_RoomsBridge.Instance?.researcher == null;

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            Pawn r = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Villager, null, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true));
            IntVec3 at = DropCellFinder.TradeDropSpot(map);
            GenSpawn.Spawn(r, at, map);
            if (!EndlessRoomsCompat.Banish(r))
            {
                r.Destroy();
                return false;
            }
            GameComponent_RoomsBridge.Instance.researcher = r;
            SendStandardLetter(def.letterLabel, "Rimverse_AsyncRescueAsk".Translate(r.LabelShort), def.letterDef, parms, LookTargets.Invalid);
            return true;
        }
    }

    public class IncidentWorker_ImpossibleMap : IncidentWorker_RoomsShip
    {
        protected override bool CanFireNowSub(IncidentParms parms) => base.CanFireNowSub(parms) && GameComponent_RoomsBridge.Instance?.impossibleKey == null;

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            List<string> near = ContractUtility.NearbySystems(null);
            if (near.Count == 0) return false;
            string key = near.RandomElement();
            GameComponent_RoomsBridge.Instance.impossibleKey = key;
            SendStandardLetter(def.letterLabel, "Rimverse_ImpossibleMap".Translate(ContractUtility.SystemName(key)), def.letterDef, parms, LookTargets.Invalid);
            return true;
        }
    }

    [HarmonyPatch(typeof(RimverseShip), "get_Signature")]
    public static class Patch_RoomsGhostSignature
    {
        public static void Postfix(RimverseShip __instance, ref float __result)
        {
            if (!EndlessRoomsCompat.Active || __instance?.map == null) return;
            HediffDef ghost = DefDatabase<HediffDef>.GetNamedSilentFail("Rimverse_RoomsGhost");
            if (ghost != null && __instance.map.mapPawns.FreeColonistsSpawned.Any(p => p.health.hediffSet.HasHediff(ghost))) __result *= 0.5f;
        }
    }
}
