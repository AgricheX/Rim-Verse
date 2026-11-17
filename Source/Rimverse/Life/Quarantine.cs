using System.Linq;
using RimWorld;
using Verse;

namespace Rimverse
{
    public class GameComponent_Quarantine : GameComponent
    {
        private static HediffDef plague;

        public GameComponent_Quarantine(Game game)
        {
        }

        public static bool IsQuarantineRoom(Room room)
        {
            return room != null && !room.PsychologicallyOutdoors && room.ContainedAndAdjacentThings.Any(t => t.def.defName == "Rimverse_QuarantineChamber");
        }

        private static bool Protected(Pawn p)
        {
            return p.apparel != null && p.apparel.WornApparel.Any(a => a.def.defName == "Rimverse_Clothes_QuarantineSuit");
        }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % 2500 != 1234) return;
            if (plague == null) plague = DefDatabase<HediffDef>.GetNamedSilentFail("Rimverse_AlienPlague");
            if (plague == null) return;
            foreach (Map map in Find.Maps)
            {
                foreach (Pawn sick in map.mapPawns.AllPawnsSpawned.Where(p => p.RaceProps.Humanlike && p.health.hediffSet.HasHediff(plague)).ToList())
                {
                    Room room = sick.GetRoom();
                    if (room == null || room.PsychologicallyOutdoors || IsQuarantineRoom(room)) continue;
                    foreach (Pawn other in room.ContainedAndAdjacentThings.OfType<Pawn>().ToList())
                    {
                        if (other == sick || !other.RaceProps.Humanlike || other.Dead || other.health.hediffSet.HasHediff(plague) || Protected(other)) continue;
                        if (other.health.immunity.GetImmunityRecord(plague) != null && other.health.immunity.GetImmunity(plague) >= 1f) continue;
                        if (!Rand.Chance(0.03f)) continue;
                        other.health.AddHediff(plague);
                        if (other.Faction == Faction.OfPlayer) Messages.Message("Rimverse_PlagueSpread".Translate(other.LabelShort, sick.LabelShort), other, MessageTypeDefOf.NegativeHealthEvent, true);
                    }
                }
            }
        }
    }
}
