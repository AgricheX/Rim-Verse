using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Rimverse
{
    public class GameComponent_ShipGym : GameComponent
    {
        public const int Interval = 250;
        public const int ProtectTicks = 60000;
        public Dictionary<int, int> lastWorkout = new Dictionary<int, int>();

        public static GameComponent_ShipGym Instance => Current.Game?.GetComponent<GameComponent_ShipGym>();

        public GameComponent_ShipGym(Game game)
        {
        }

        public static bool IsGym(Thing t) => t != null && t.def.defName == "Rimverse_ShipGym";

        public static float BoneLossFactor(Pawn p)
        {
            GameComponent_ShipGym g = Instance;
            if (g == null || !g.lastWorkout.TryGetValue(p.thingIDNumber, out int last)) return 1f;
            return Find.TickManager.TicksGame - last <= ProtectTicks ? 0.2f : 1f;
        }

        public override void GameComponentTick()
        {
            int tick = Find.TickManager.TicksGame;
            if (tick % Interval != 41) return;
            foreach (Map map in Find.Maps)
            {
                foreach (Pawn p in map.mapPawns.FreeColonistsSpawned)
                {
                    Job job = p.CurJob;
                    if (job == null || !IsGym(job.targetA.Thing) && !IsGym(job.targetB.Thing)) continue;
                    if (p.Position.DistanceTo((IsGym(job.targetA.Thing) ? job.targetA.Thing : job.targetB.Thing).Position) > 2.5f) continue;
                    lastWorkout[p.thingIDNumber] = tick;
                    Hediff bone = p.health.hediffSet.GetFirstHediffOfDef(LifeDefOf.Rimverse_BoneLoss);
                    if (bone != null) bone.Severity -= 0.004f;
                    Hediff atrophy = p.health.hediffSet.GetFirstHediffOfDef(CatalogDefOf.Rimverse_MuscleAtrophy);
                    if (atrophy != null) atrophy.Severity -= 0.004f;
                }
            }
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref lastWorkout, "lastWorkout", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && lastWorkout == null) lastWorkout = new Dictionary<int, int>();
        }
    }
}
