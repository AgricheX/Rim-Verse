using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public enum DiploState
    {
        War,
        Truce,
        Peace,
        TradeAgreement,
        NonAggression,
        Alliance,
        Vassal,
        Federation
    }

    public enum CivGoal
    {
        Expand,
        Arm,
        Colonize,
        Research,
        Trade,
        Defend
    }

    public enum CivResource
    {
        Food,
        Metal,
        Fuel,
        WarpMatter,
        Components,
        Luxury
    }

    public enum LoreKind
    {
        None,
        Empire,
        Glitterworld,
        Urbworld,
        Pirates,
        Mechanoids,
        Neighbor
    }

    public class CivPlanet : IExposable
    {
        public string systemKey;
        public int body;
        public float population;
        public float capacity;
        public float loyalty = 50f;

        public void ExposeData()
        {
            Scribe_Values.Look(ref systemKey, "systemKey");
            Scribe_Values.Look(ref body, "body");
            Scribe_Values.Look(ref population, "population");
            Scribe_Values.Look(ref capacity, "capacity");
            Scribe_Values.Look(ref loyalty, "loyalty", 50f);
        }
    }

    public class CivRelation : IExposable
    {
        public int other;
        public float opinion;
        public float baseOpinion;
        public DiploState state = DiploState.Peace;
        public int sinceTick;

        public void ExposeData()
        {
            Scribe_Values.Look(ref other, "other");
            Scribe_Values.Look(ref opinion, "opinion");
            Scribe_Values.Look(ref baseOpinion, "baseOpinion");
            Scribe_Values.Look(ref state, "state", DiploState.Peace);
            Scribe_Values.Look(ref sinceTick, "sinceTick");
        }
    }

    public class Civilization : IExposable
    {
        public const int PlayerId = -1;

        public int id;
        public string root;
        public Color color = Color.white;
        public int stage = 9;
        public int sizeClass = 50;
        public TechLevel tech = TechLevel.Spacer;
        public PolicyDef government;
        public PolicyDef ideology;
        public PolicyDef economy;
        public PolicyDef xeno;
        public PolicyDef logistics;
        public PolicyDef origin;
        public List<PolicyDef> civics = new List<PolicyDef>();
        public string capitalKey;
        public int galaxy;
        public List<CivPlanet> planets = new List<CivPlanet>();
        public float frontierPopulation;
        public float stability = 50f;
        public float warWeariness;
        public float treasury;
        public List<float> stock = new List<float>();
        public Dictionary<string, float> ideologySupport = new Dictionary<string, float>();
        public Dictionary<InterestGroup, float> groupMood = new Dictionary<InterestGroup, float>();
        public CivGoal goal = CivGoal.Expand;
        public List<CivRelation> relations = new List<CivRelation>();
        public float playerOpinion;
        public DiploState playerState = DiploState.Peace;
        public int playerStateSince;
        public bool known;
        public Faction faction;
        public LoreKind lore = LoreKind.None;
        public string lineage;
        public int lastSimTick;
        public int nextElectionTick = -1;
        public int stableYears;
        public int lowStabilityYears;
        public bool dead;
        public int leaderAge = 40;
        public int lastPlayerProposalTick = -9999999;

        public bool Hostile => lore == LoreKind.Mechanoids || HasCivic("Rimverse_Policy_CivicPirates");

        public bool NoDiplomacy => lore == LoreKind.Mechanoids;

        public StarSystemId Capital => StarSystemId.FromKey(capitalKey);

        public IEnumerable<PolicyDef> Policies
        {
            get
            {
                if (government != null) yield return government;
                if (ideology != null) yield return ideology;
                if (economy != null) yield return economy;
                if (xeno != null) yield return xeno;
                if (logistics != null) yield return logistics;
                if (origin != null) yield return origin;
                foreach (PolicyDef c in civics) yield return c;
            }
        }

        public bool HasCivic(string defName)
        {
            foreach (PolicyDef c in civics) if (c.defName == defName) return true;
            return false;
        }

        public float Modifier(CivStat stat)
        {
            float total = 0f;
            foreach (PolicyDef p in Policies) total += p.Effect(stat);
            return total;
        }

        public float Population
        {
            get
            {
                float total = frontierPopulation;
                foreach (CivPlanet p in planets) total += p.population;
                return total;
            }
        }

        public float Stock(CivResource r)
        {
            int i = (int)r;
            return i < stock.Count ? stock[i] : 0f;
        }

        public void AddStock(CivResource r, float amount)
        {
            int i = (int)r;
            while (stock.Count <= i) stock.Add(0f);
            stock[i] = Mathf.Max(0f, stock[i] + amount);
        }

        public string FormKey
        {
            get
            {
                if (lore == LoreKind.Mechanoids) return "Hive";
                if (lore == LoreKind.Pirates || HasCivic("Rimverse_Policy_CivicPirates")) return "Pirates";
                string g = government?.defName ?? "";
                if (g.Contains("Tribal") || g.Contains("Chiefdom")) return "Tribe";
                if (g.Contains("Empire") || g.Contains("Absolute") || g.Contains("Feudal")) return "Empire";
                if (g.Contains("Monarchy")) return "Kingdom";
                if (g.Contains("Theocracy")) return "Theocracy";
                if (g.Contains("Corporat")) return "Corporation";
                if (g.Contains("Junta")) return "Junta";
                if (g.Contains("Federation") || g.Contains("Assembly") || g.Contains("WebAssembly")) return "Federation";
                if (g.Contains("Hive") || g.Contains("Distributed") || g.Contains("AITechnocracy") || g.Contains("Omega")) return "Collective";
                if (g.Contains("OneParty")) return "Union";
                if (g.Contains("Technocracy")) return "Directorate";
                return "Republic";
            }
        }

        public string Name => ("Rimverse_CivForm_" + FormKey).Translate(root);

        public float InfluenceRadiusLy
        {
            get
            {
                float scale = 0.6f + 0.4f * sizeClass / 100f;
                switch (stage)
                {
                    case 10: return 20f * scale;
                    case 11: return 60f * scale;
                    case 12: return 180f * scale;
                    case 13: return 2000f * scale;
                    case 14: return 6000f * scale;
                    default: return 0f;
                }
            }
        }

        public CivRelation RelationWith(int other, bool create)
        {
            foreach (CivRelation r in relations) if (r.other == other) return r;
            if (!create) return null;
            CivRelation n = new CivRelation { other = other };
            relations.Add(n);
            return n;
        }

        public float GroupMood(InterestGroup g) => groupMood.TryGetValue(g, out float v) ? v : 50f;

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref root, "root");
            Scribe_Values.Look(ref color, "color", Color.white);
            Scribe_Values.Look(ref stage, "stage", 9);
            Scribe_Values.Look(ref sizeClass, "sizeClass", 50);
            Scribe_Values.Look(ref tech, "tech", TechLevel.Spacer);
            Scribe_Defs.Look(ref government, "government");
            Scribe_Defs.Look(ref ideology, "ideology");
            Scribe_Defs.Look(ref economy, "economy");
            Scribe_Defs.Look(ref xeno, "xeno");
            Scribe_Defs.Look(ref logistics, "logistics");
            Scribe_Defs.Look(ref origin, "origin");
            Scribe_Collections.Look(ref civics, "civics", LookMode.Def);
            Scribe_Values.Look(ref capitalKey, "capitalKey");
            Scribe_Values.Look(ref galaxy, "galaxy");
            Scribe_Collections.Look(ref planets, "planets", LookMode.Deep);
            Scribe_Values.Look(ref frontierPopulation, "frontierPopulation");
            Scribe_Values.Look(ref stability, "stability", 50f);
            Scribe_Values.Look(ref warWeariness, "warWeariness");
            Scribe_Values.Look(ref treasury, "treasury");
            Scribe_Collections.Look(ref stock, "stock", LookMode.Value);
            Scribe_Collections.Look(ref ideologySupport, "ideologySupport", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref groupMood, "groupMood", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref goal, "goal");
            Scribe_Collections.Look(ref relations, "relations", LookMode.Deep);
            Scribe_Values.Look(ref playerOpinion, "playerOpinion");
            Scribe_Values.Look(ref playerState, "playerState", DiploState.Peace);
            Scribe_Values.Look(ref playerStateSince, "playerStateSince");
            Scribe_Values.Look(ref known, "known");
            Scribe_References.Look(ref faction, "faction");
            Scribe_Values.Look(ref lore, "lore");
            Scribe_Values.Look(ref lineage, "lineage");
            Scribe_Values.Look(ref lastSimTick, "lastSimTick");
            Scribe_Values.Look(ref nextElectionTick, "nextElectionTick", -1);
            Scribe_Values.Look(ref stableYears, "stableYears");
            Scribe_Values.Look(ref lowStabilityYears, "lowStabilityYears");
            Scribe_Values.Look(ref dead, "dead");
            Scribe_Values.Look(ref leaderAge, "leaderAge", 40);
            Scribe_Values.Look(ref lastPlayerProposalTick, "lastPlayerProposalTick", -9999999);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (civics == null) civics = new List<PolicyDef>();
                civics.RemoveAll(c => c == null);
                if (planets == null) planets = new List<CivPlanet>();
                if (stock == null) stock = new List<float>();
                if (ideologySupport == null) ideologySupport = new Dictionary<string, float>();
                if (groupMood == null) groupMood = new Dictionary<InterestGroup, float>();
                if (relations == null) relations = new List<CivRelation>();
            }
        }
    }
}
