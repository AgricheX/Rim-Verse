using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public enum ColonyIndustry
    {
        Agriculture,
        Mining,
        GasHarvest,
        Refinery,
        TradePort,
        Factory,
        MilitaryBase,
        Shipyard
    }

    public class ColonyBuild : IExposable
    {
        public ColonyIndustry type;
        public int readyTick;

        public bool Ready => Find.TickManager.TicksGame >= readyTick;

        public void ExposeData()
        {
            Scribe_Values.Look(ref type, "type");
            Scribe_Values.Look(ref readyTick, "readyTick");
        }
    }

    public class PlayerColony : IExposable
    {
        public int id;
        public string name;
        public string systemKey;
        public int bodyIndex;
        public int foundedTick;
        public float population;
        public float stability = 50f;
        public float attention;
        public bool warned;
        public int nextStepTick;
        public float lastIncome;
        public List<ColonyBuild> industries = new List<ColonyBuild>();
        public List<Pawn> settlers = new List<Pawn>();
        public Dictionary<string, int> stock = new Dictionary<string, int>();

        public StarSystemId SystemId => StarSystemId.FromKey(systemKey);

        public BodyData Body => GameComponent_Universe.Instance?.GetSystem(SystemId)?.Body(bodyIndex);

        public StarSystemData System => GameComponent_Universe.Instance?.GetSystem(SystemId);

        public bool Has(ColonyIndustry t) => industries.Any(b => b.type == t && b.Ready);

        public bool Building(ColonyIndustry t) => industries.Any(b => b.type == t);

        public Pawn Administrator => settlers.Where(p => p != null && !p.Dead && p.skills != null).OrderByDescending(p => p.skills.GetSkill(SkillDefOf.Social).Level).FirstOrDefault();

        public float Hazard => ColonyUtility.Hazard(Body);

        public float Capacity
        {
            get
            {
                BodyData b = Body;
                float size = b != null ? b.sizeClass : 30f;
                return (20f + size * 5f) / Mathf.Max(1f, Hazard);
            }
        }

        public float Wealth => industries.Sum(i => ColonyUtility.Cost(i.type)) + population * 100f;

        public void AddStock(string defName, int count)
        {
            if (count <= 0) return;
            stock.TryGetValue(defName, out int cur);
            stock[defName] = cur + count;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref systemKey, "systemKey");
            Scribe_Values.Look(ref bodyIndex, "bodyIndex");
            Scribe_Values.Look(ref foundedTick, "foundedTick");
            Scribe_Values.Look(ref population, "population");
            Scribe_Values.Look(ref stability, "stability", 50f);
            Scribe_Values.Look(ref attention, "attention");
            Scribe_Values.Look(ref warned, "warned");
            Scribe_Values.Look(ref nextStepTick, "nextStepTick");
            Scribe_Values.Look(ref lastIncome, "lastIncome");
            Scribe_Collections.Look(ref industries, "industries", LookMode.Deep);
            Scribe_Collections.Look(ref settlers, "settlers", LookMode.Reference);
            Scribe_Collections.Look(ref stock, "stock", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (industries == null) industries = new List<ColonyBuild>();
                if (settlers == null) settlers = new List<Pawn>();
                settlers.RemoveAll(p => p == null);
                if (stock == null) stock = new Dictionary<string, int>();
            }
        }
    }
}
