using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Rimverse
{
    public class EmpireEffects
    {
        public float stability;
        public float income;
        public float growth;
        public float defense;
        public float attention;
        public float research;
        public float influence;
        public float capacity;
        public float food;
        public float fleetUpkeep;
        public float opinion;

        public void Add(EmpireEffects o, float mult = 1f)
        {
            if (o == null) return;
            stability += o.stability * mult;
            income += o.income * mult;
            growth += o.growth * mult;
            defense += o.defense * mult;
            attention += o.attention * mult;
            research += o.research * mult;
            influence += o.influence * mult;
            capacity += o.capacity * mult;
            food += o.food * mult;
            fleetUpkeep += o.fleetUpkeep * mult;
            opinion += o.opinion * mult;
        }

        public IEnumerable<string> Lines()
        {
            if (stability != 0f) yield return "Rimverse_Eff_Stability".Translate(stability.ToString("+0;-0"));
            if (income != 0f) yield return "Rimverse_Eff_Income".Translate(income.ToStringPercentSigned());
            if (growth != 0f) yield return "Rimverse_Eff_Growth".Translate(growth.ToStringPercentSigned());
            if (defense != 0f) yield return "Rimverse_Eff_Defense".Translate(defense.ToString("+0;-0"));
            if (attention != 0f) yield return "Rimverse_Eff_Attention".Translate(attention.ToStringPercentSigned());
            if (research != 0f) yield return "Rimverse_Eff_Research".Translate(research.ToString("+0;-0"));
            if (influence != 0f) yield return "Rimverse_Eff_Influence".Translate(influence.ToString("+0.#;-0.#"));
            if (capacity != 0f) yield return "Rimverse_Eff_Capacity".Translate(capacity.ToString("+0;-0"));
            if (food != 0f) yield return "Rimverse_Eff_Food".Translate(food.ToString("+0;-0"));
            if (fleetUpkeep != 0f) yield return "Rimverse_Eff_FleetUpkeep".Translate(fleetUpkeep.ToStringPercentSigned());
            if (opinion != 0f) yield return "Rimverse_Eff_Opinion".Translate(opinion.ToString("+0.#;-0.#"));
        }
    }

    public class EdictDef : Def
    {
        public int costSilver;
        public float costInfluence;
        public float durationDays = 60f;
        public EmpireEffects effects = new EmpireEffects();
        public ResearchProjectDef research;
        public bool war;
        public int order;
    }

    public class ColonyFacilityDef : Def
    {
        public int costSilver = 1000;
        public int days = 10;
        public float upkeep = 20f;
        public int minTier;
        public int maxCount = 1;
        public bool surfaceOnly;
        public bool spaceOnly;
        public EmpireEffects effects = new EmpireEffects();
        public List<ThingDefCountClass> output = new List<ThingDefCountClass>();
        public ResearchProjectDef research;
        public int order;
    }

    public class ResolutionDef : Def
    {
        public bool targetsCiv;
        public EmpireEffects effects = new EmpireEffects();
        public float tariffFactor = 1f;
        public float pirateFactor = 1f;
        public float warFactor = 1f;
        public bool favoredByPeaceful = true;
        public bool favoredByMerchants;
        public bool favoredByMilitary;
        public float durationYears = 5f;
    }

    public class SpaceAnomalyDef : Def
    {
        public float commonality = 1f;
        public float researchDays = 5f;
        public int chapters = 1;
        public bool needsColony;
        public int rewardResearch;
        public int rewardSilver;
        public float rewardInfluence;
        public List<ThingDefCountClass> rewardItems = new List<ThingDefCountClass>();
        public float dangerChance;
        public TechLevel minTech = TechLevel.Industrial;
    }

    public class SituationOption
    {
        public string key;
        public int costSilver;
        public float costInfluence;
        public float stability;
        public float population;
        public float attention;
        public int silver;
        public float influence;
        public string stockDef;
        public int stockCount;
        public float successChance = 1f;
    }

    public class ColonySituationDef : Def
    {
        public float commonality = 1f;
        public float minStability;
        public float maxStability = 100f;
        public float minPopulation;
        public float minHazard;
        public bool surfaceOnly;
        public bool spaceOnly;
        public List<SituationOption> options = new List<SituationOption>();
    }

    public class FleetHullDef : Def
    {
        public float power = 10f;
        public float ground;
        public int costSilver = 1000;
        public float upkeep = 10f;
        public int days = 5;
        public float speedLyPerDay = 4f;
        public bool squad;
        public ResearchProjectDef research;
        public int order;
    }
}
