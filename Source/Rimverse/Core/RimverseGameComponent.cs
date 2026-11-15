using Verse;

namespace Rimverse
{
    public class RimverseGameComponent : GameComponent
    {
        public string createdWithVersion;
        public string lastLoadedVersion;

        public static RimverseGameComponent Current => Verse.Current.Game?.GetComponent<RimverseGameComponent>();

        public RimverseGameComponent(Game game)
        {
        }

        public override void StartedNewGame()
        {
            createdWithVersion = CurrentVersion;
            lastLoadedVersion = CurrentVersion;
        }

        public override void LoadedGame()
        {
            if (createdWithVersion.NullOrEmpty())
            {
                createdWithVersion = CurrentVersion;
                RimverseLog.Message("Rimverse added to an existing save.");
            }
            lastLoadedVersion = CurrentVersion;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref createdWithVersion, "createdWithVersion");
            Scribe_Values.Look(ref lastLoadedVersion, "lastLoadedVersion");
        }

        public static string CurrentVersion => RimverseMod.Instance?.Content?.ModMetaData?.ModVersion ?? "0";
    }
}
