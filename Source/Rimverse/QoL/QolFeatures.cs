using Verse;

namespace Rimverse
{
    public static class QolFeatures
    {
        public static bool IsEnabled(string defName)
        {
            QolFeatureDef def = DefDatabase<QolFeatureDef>.GetNamedSilentFail(defName);
            return def != null && def.Enabled;
        }
    }
}
