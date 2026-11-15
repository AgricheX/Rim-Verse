using RimWorld;
using Verse;

namespace Rimverse
{
    [DefOf]
    public static class LifeDefOf
    {
        public static HediffDef Rimverse_ZeroGSickness;
        public static HediffDef Rimverse_ZeroGAdaptation;
        public static HediffDef Rimverse_BoneLoss;
        public static HediffDef Rimverse_GravityCrush;
        public static HediffDef Rimverse_WarpSickness;
        public static HediffDef Rimverse_CabinFever;
        public static HediffDef Rimverse_LowGravityClumsy;
        public static HediffDef Rimverse_LowGravityStride;
        public static HediffDef Rimverse_DryClimate;

        static LifeDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(LifeDefOf));
        }
    }
}
