using Verse;

namespace Rimverse
{
    public enum FleetMission
    {
        Patrol,
        War,
        Escort,
        Colonize,
        Explore,
        Trade,
        Raid,
        Siege
    }

    public class Fleet : IExposable
    {
        public int id;
        public int civId;
        public FleetMission mission;
        public string systemKey;
        public string destKey;
        public int departTick = -1;
        public int arriveTick = -1;
        public float power;
        public float morale = 1f;
        public int targetCiv = -2;
        public bool returning;
        public float cargo;
        public int siegeUntil = -1;

        public bool Moving => arriveTick > 0 && !destKey.NullOrEmpty();

        public bool TargetsPlayer => targetCiv == Civilization.PlayerId;

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref civId, "civId");
            Scribe_Values.Look(ref mission, "mission");
            Scribe_Values.Look(ref systemKey, "systemKey");
            Scribe_Values.Look(ref destKey, "destKey");
            Scribe_Values.Look(ref departTick, "departTick", -1);
            Scribe_Values.Look(ref arriveTick, "arriveTick", -1);
            Scribe_Values.Look(ref power, "power");
            Scribe_Values.Look(ref morale, "morale", 1f);
            Scribe_Values.Look(ref targetCiv, "targetCiv", -2);
            Scribe_Values.Look(ref returning, "returning");
            Scribe_Values.Look(ref cargo, "cargo");
            Scribe_Values.Look(ref siegeUntil, "siegeUntil", -1);
        }
    }
}
