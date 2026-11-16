using Verse;

namespace Rimverse
{
    public static class ResearchGate
    {
        public static bool Done(string defName)
        {
            ResearchProjectDef r = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(defName);
            return r == null || r.IsFinished;
        }

        public static string Need(string defName)
        {
            ResearchProjectDef r = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(defName);
            return "Rimverse_NeedsResearch".Translate(r != null ? r.LabelCap.ToString() : defName);
        }
    }
}
