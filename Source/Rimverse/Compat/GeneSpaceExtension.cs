using Verse;

namespace Rimverse
{
    public class GeneSpaceExtension : DefModExtension
    {
        public float airFactor = 1f;
        public float hypoxiaFactor = 1f;
        public float cabinFeverFactor = 1f;
        public float crushBonus;
        public bool zeroGAdapted;
        public bool noClumsy;
        public bool fearOfSky;
        public bool dryClimatePenalty;
        public float waterSpeedFactor = 1f;
    }

    public static class GeneSpace
    {
        public static float Factor(Pawn p, System.Func<GeneSpaceExtension, float> f)
        {
            if (!ModCompat.Biotech || p?.genes == null) return 1f;
            float r = 1f;
            var list = p.genes.GenesListForReading;
            for (int i = 0; i < list.Count; i++)
            {
                if (!list[i].Active) continue;
                GeneSpaceExtension e = list[i].def.GetModExtension<GeneSpaceExtension>();
                if (e != null) r *= f(e);
            }
            return r;
        }

        public static float Sum(Pawn p, System.Func<GeneSpaceExtension, float> f)
        {
            if (!ModCompat.Biotech || p?.genes == null) return 0f;
            float r = 0f;
            var list = p.genes.GenesListForReading;
            for (int i = 0; i < list.Count; i++)
            {
                if (!list[i].Active) continue;
                GeneSpaceExtension e = list[i].def.GetModExtension<GeneSpaceExtension>();
                if (e != null) r += f(e);
            }
            return r;
        }

        public static bool Any(Pawn p, System.Func<GeneSpaceExtension, bool> f)
        {
            if (!ModCompat.Biotech || p?.genes == null) return false;
            var list = p.genes.GenesListForReading;
            for (int i = 0; i < list.Count; i++)
            {
                if (!list[i].Active) continue;
                GeneSpaceExtension e = list[i].def.GetModExtension<GeneSpaceExtension>();
                if (e != null && f(e)) return true;
            }
            return false;
        }
    }
}
