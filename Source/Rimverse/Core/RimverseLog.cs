using Verse;

namespace Rimverse
{
    public static class RimverseLog
    {
        private const string Prefix = "[Rimverse] ";

        public static void Message(string text) => Log.Message(Prefix + text);

        public static void Warning(string text) => Log.Warning(Prefix + text);

        public static void Error(string text) => Log.Error(Prefix + text);

        public static void Debug(string text)
        {
            if (RimverseMod.Settings != null && RimverseMod.Settings.debugLogging)
            {
                Log.Message(Prefix + "[debug] " + text);
            }
        }

        public static void ErrorOnce(string text, string key) => Log.ErrorOnce(Prefix + text, key.GetHashCode());
    }
}
