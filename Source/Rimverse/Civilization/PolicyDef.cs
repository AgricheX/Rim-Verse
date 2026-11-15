using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Rimverse
{
    public enum PolicyGroup
    {
        Government,
        Ideology,
        Economy,
        Xeno,
        Logistics,
        Origin,
        Civic
    }

    public enum Succession
    {
        None,
        Appointed,
        Hereditary,
        Elective
    }

    public enum AiProfile
    {
        Warmonger,
        Aggressive,
        Cautious,
        Mercantile,
        Expansionist
    }

    public enum InterestGroup
    {
        Military,
        Merchants,
        Clergy,
        Scientists,
        Nobility,
        Workers,
        Machines
    }

    public enum CivStat
    {
        Production,
        Stability,
        Loyalty,
        Culture,
        Military,
        Research,
        Trade,
        Growth
    }

    public class PolicyDef : Def
    {
        public PolicyGroup group;
        public TechLevel minTech = TechLevel.Neolithic;
        public int minStage = 1;
        public int maxStage = 14;
        public Succession succession = Succession.None;
        public bool elections;
        public AiProfile profile = AiProfile.Cautious;
        public float production;
        public float stability;
        public float loyalty;
        public float culture;
        public float military;
        public float research;
        public float trade;
        public float growth;
        public float xeno;
        public float weight = 1f;
        public bool loreOnly;
        public List<InterestGroup> favoredBy = new List<InterestGroup>();
        public List<InterestGroup> opposedBy = new List<InterestGroup>();

        public float Effect(CivStat stat)
        {
            switch (stat)
            {
                case CivStat.Production: return production;
                case CivStat.Stability: return stability;
                case CivStat.Loyalty: return loyalty;
                case CivStat.Culture: return culture;
                case CivStat.Military: return military;
                case CivStat.Research: return research;
                case CivStat.Trade: return trade;
                default: return growth;
            }
        }

        public bool Allowed(TechLevel tech, int stage) => tech >= minTech && stage >= minStage && stage <= maxStage;

        public IEnumerable<string> EffectLines()
        {
            foreach (CivStat s in System.Enum.GetValues(typeof(CivStat)))
            {
                float v = Effect(s);
                if (v != 0f) yield return ("Rimverse_CivStat_" + s).Translate() + " " + (v > 0 ? "+" : "") + v.ToString("0");
            }
        }
    }
}
