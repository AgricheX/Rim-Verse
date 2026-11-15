using System.Linq;
using RimWorld;
using Verse;

namespace Rimverse
{
    public abstract class IncidentWorker_RoomsShip : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = parms.target as Map;
            return EndlessRoomsCompat.Active && map != null && map.IsPlayerHome && SpaceMapUtility.IsSpace(map) && map.mapPawns.FreeColonistsSpawnedCount >= 2;
        }

        protected static Pawn RandomCrew(Map map) => map.mapPawns.FreeColonistsSpawned.Where(p => !p.Downed && !p.InMentalState).RandomElementWithFallback();
    }

    public class IncidentWorker_YellowSignal : IncidentWorker_RoomsShip
    {
        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            Pawn p = RandomCrew(map);
            if (p == null) return false;
            string name = p.LabelShort;
            if (!EndlessRoomsCompat.Drop(p)) return false;
            Find.LetterStack.ReceiveLetter("Rimverse_LetterYellowSignalLabel".Translate(), "Rimverse_LetterYellowSignal".Translate(name), LetterDefOf.NegativeEvent);
            return true;
        }
    }

    public class IncidentWorker_RoomsSurvivor : IncidentWorker_RoomsShip
    {
        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            Pawn anchor = RandomCrew(map);
            if (anchor == null) return false;
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail("YellowRooms_Survivor_Scavenger") ?? PawnKindDefOf.Villager;
            Pawn pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, Faction.OfPlayer, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true));
            IntVec3 cell = CellFinder.RandomClosewalkCellNear(anchor.Position, map, 8, c => c.Standable(map) && c.GetRoom(map) == anchor.GetRoom());
            if (!cell.IsValid) cell = anchor.Position;
            GenSpawn.Spawn(pawn, cell, map);
            if (pawn.Faction != Faction.OfPlayer) pawn.SetFaction(Faction.OfPlayer);
            Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
            silver.stackCount = Rand.RangeInclusive(40, 160);
            GenPlace.TryPlaceThing(silver, cell, map, ThingPlaceMode.Near);
            Find.LetterStack.ReceiveLetter("Rimverse_LetterRoomsSurvivorLabel".Translate(), "Rimverse_LetterRoomsSurvivor".Translate(pawn.LabelShort), LetterDefOf.PositiveEvent, pawn);
            return true;
        }
    }

    public class IncidentWorker_OfficeEcho : IncidentWorker_RoomsShip
    {
        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            Pawn p = RandomCrew(map);
            ThoughtDef t = DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimverse_OfficeEcho");
            if (p?.needs?.mood == null || t == null) return false;
            p.needs.mood.thoughts.memories.TryGainMemory(t);
            Messages.Message("Rimverse_MsgOfficeEcho".Translate(p.LabelShort), p, MessageTypeDefOf.NeutralEvent, false);
            return true;
        }
    }

    public class CompProperties_AbilityBanishToRooms : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityBanishToRooms()
        {
            compClass = typeof(CompAbilityEffect_BanishToRooms);
        }
    }

    public class CompAbilityEffect_BanishToRooms : CompAbilityEffect
    {
        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!EndlessRoomsCompat.Active || !(target.Thing is Pawn p) || EndlessRoomsCompat.IsBackrooms(p.Map)) return false;
            return base.Valid(target, throwMessages);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            if (target.Thing is Pawn p) EndlessRoomsCompat.Banish(p);
        }
    }

    public class CompProperties_AbilityRecallFromRooms : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityRecallFromRooms()
        {
            compClass = typeof(CompAbilityEffect_RecallFromRooms);
        }
    }

    public class CompAbilityEffect_RecallFromRooms : CompAbilityEffect
    {
        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!EndlessRoomsCompat.AnyLost)
            {
                if (throwMessages) Messages.Message("Rimverse_PsyNoneLost".Translate(), MessageTypeDefOf.RejectInput, false);
                return false;
            }
            return base.Valid(target, throwMessages);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            Pawn lost = EndlessRoomsCompat.LostColonists().RandomElementWithFallback();
            if (caster?.Map == null || lost == null) return;
            if (EndlessRoomsCompat.Pull(lost, caster.Map, caster.Position)) Messages.Message("Rimverse_PsyRecalled".Translate(lost.LabelShort), caster, MessageTypeDefOf.PositiveEvent, false);
        }
    }
}
