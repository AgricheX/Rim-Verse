using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Rimverse
{
    public static class BiotechCompat
    {
        private static readonly Dictionary<string, string> XenoByLineage = new Dictionary<string, string>
        {
            { "Rimverse_Lineage_VoidBorn", "Rimverse_Xeno_VoidAdapted" },
            { "Rimverse_Lineage_Gravitated", "Rimverse_Xeno_HeavyWorlder" },
            { "Rimverse_Lineage_Boreal", "Rimverse_Xeno_IceBorn" },
            { "Rimverse_Lineage_Abyssal", "Rimverse_Xeno_Oceanic" },
            { "Rimverse_Lineage_Rad", "Rimverse_Xeno_RadBorn" }
        };

        public static XenotypeDef XenotypeFor(string lineage)
        {
            if (!ModCompat.Biotech || lineage.NullOrEmpty()) return null;
            return XenoByLineage.TryGetValue(lineage, out string name) ? DefDatabase<XenotypeDef>.GetNamedSilentFail(name) : null;
        }

        public static bool TryXenotype(Pawn pawn, string lineage)
        {
            XenotypeDef x = XenotypeFor(lineage);
            if (x == null || pawn?.genes == null) return false;
            if (pawn.genes.Xenotype != null && pawn.genes.Xenotype != XenotypeDefOf.Baseliner) return false;
            pawn.genes.SetXenotype(x);
            return true;
        }
    }
}
