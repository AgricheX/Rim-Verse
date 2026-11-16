using RimWorld;
using Verse;

namespace Rimverse
{
    [DefOf]
    public static class CatalogDefOf
    {
        public static TraitDef Rimverse_VoidBorn;
        public static TraitDef Rimverse_Grounded;
        public static TraitDef Rimverse_Claustrophobic;
        public static TraitDef Rimverse_SpaceLoner;
        public static HediffDef Rimverse_BalanceEar;
        public static HediffDef Rimverse_ReinforcedBones;
        public static HediffDef Rimverse_BoneStabilized;
        public static HediffDef Rimverse_MuscleAtrophy;
        public static HediffDef Rimverse_SkyLonging;

        static CatalogDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(CatalogDefOf));
        }
    }

    public static class CatalogTraits
    {
        public static bool Has(Pawn p, TraitDef t) => t != null && p.story?.traits != null && p.story.traits.HasTrait(t);
        public static bool HasHediff(Pawn p, HediffDef h) => h != null && p.health.hediffSet.HasHediff(h);

        public static bool IgnoresZeroGSickness(Pawn p) => Has(p, CatalogDefOf.Rimverse_VoidBorn) || HasHediff(p, CatalogDefOf.Rimverse_BalanceEar);
        public static bool IgnoresWarpSickness(Pawn p) => HasHediff(p, CatalogDefOf.Rimverse_BalanceEar);

        public static float BoneLossFactor(Pawn p)
        {
            if (HasHediff(p, CatalogDefOf.Rimverse_ReinforcedBones)) return 0f;
            return HasHediff(p, CatalogDefOf.Rimverse_BoneStabilized) ? 0.25f : 1f;
        }

        public static float CrushBonus(Pawn p) => HasHediff(p, CatalogDefOf.Rimverse_ReinforcedBones) ? 0.5f : 0f;

        public static float CabinFeverFactor(Pawn p)
        {
            if (Has(p, CatalogDefOf.Rimverse_VoidBorn)) return 0f;
            float f = 1f;
            if (Has(p, CatalogDefOf.Rimverse_Grounded)) f *= 2f;
            if (Has(p, CatalogDefOf.Rimverse_Claustrophobic)) f *= 1.5f;
            if (Has(p, CatalogDefOf.Rimverse_SpaceLoner)) f *= 0.5f;
            return f;
        }
    }
}

namespace Rimverse
{
    public static class CatalogHealth
    {
        private static HediffDef decomp, frost, lung, skin, poison;

        private static void Init()
        {
            if (decomp != null) return;
            decomp = DefDatabase<HediffDef>.GetNamedSilentFail("Rimverse_Decompression");
            frost = DefDatabase<HediffDef>.GetNamedSilentFail("Rimverse_VacuumFrostbite");
            lung = DefDatabase<HediffDef>.GetNamedSilentFail("Rimverse_VacuumLung");
            skin = DefDatabase<HediffDef>.GetNamedSilentFail("Rimverse_SealedSkin");
            poison = DefDatabase<HediffDef>.GetNamedSilentFail("Rimverse_AirPoisoning");
        }

        public static float HypoxiaFactor(Pawn p)
        {
            Init();
            float f = lung != null && p.health.hediffSet.HasHediff(lung) ? 0.4f : 1f;
            foreach (Hediff h in p.health.hediffSet.hediffs)
                if (h is Hediff_AddedPart && h.Part?.def == BodyPartDefOf.Lung && h.def.addedPartProps != null && h.def.addedPartProps.betterThanNatural && h.def != lung) f *= 0.75f;
            return f;
        }

        public static void VacuumExposure(Pawn p, float seconds)
        {
            Init();
            if (SpaceMapUtility.VacuumProtection(p) >= 1f) return;
            if (skin != null && p.health.hediffSet.HasHediff(skin)) return;
            if (decomp != null) HealthUtility.AdjustSeverity(p, decomp, 0.01f * seconds);
            if (frost != null) HealthUtility.AdjustSeverity(p, frost, 0.006f * seconds);
        }

        public static void StaleAir(Pawn p, float seconds)
        {
            Init();
            if (poison != null) HealthUtility.AdjustSeverity(p, poison, 0.0004f * seconds);
        }
    }
}
