using Verse;

namespace Rimverse
{
    public enum CrisisKind
    {
        None,
        MechanoidSwarm,
        GreyPlague,
        ArchotechAwakening,
        Invasion
    }

    public enum EndingKind
    {
        CrisisDefeated,
        Ascension,
        GalacticEmpire,
        BeyondTheGroup
    }

    public class Wormhole : IExposable
    {
        public int id;
        public string originKey;
        public string destKey;
        public int expireTick;
        public bool stable;

        public bool Open => stable || Find.TickManager.TicksGame < expireTick;

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref originKey, "originKey");
            Scribe_Values.Look(ref destKey, "destKey");
            Scribe_Values.Look(ref expireTick, "expireTick");
            Scribe_Values.Look(ref stable, "stable");
        }
    }
}
