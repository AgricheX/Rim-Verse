using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public enum MegastructureKind
    {
        OrbitalRing,
        StellarCollector,
        GiantHabitat,
        DeepObservatory,
        WormholeStabilizer,
        Gate,
        RingHabitat,
        Citadel,
        PirateBase,
        CivStation
    }

    public class MegastructureData : IExposable
    {
        public const int Stages = 4;

        public int id;
        public MegastructureKind kind;
        public string systemKey;
        public int body = -1;
        public int stage;
        public float work;
        public bool supplied;
        public bool player;
        public int civId = int.MinValue;
        public int colonyId = -1;
        public bool ruin;
        public int seed;
        public int nodeId = -1;
        public float angle;

        public StarSystemId System => StarSystemId.FromKey(systemKey);

        public bool Complete => stage >= Stages;

        public float Completion => Mathf.Clamp01((stage + (Complete ? 0f : work)) / Stages);

        public float Output => Mathf.Clamp01((float)stage / Stages);

        public bool Visitable => stage >= 1;

        public bool IsBase => kind == MegastructureKind.PirateBase || kind == MegastructureKind.CivStation;

        public bool AroundStar => kind == MegastructureKind.StellarCollector || kind == MegastructureKind.Gate && body < 0;

        public string KindLabel => ("Rimverse_Mega_" + kind).Translate();

        public string StageLabel => ("Rimverse_MegaStage_" + Mathf.Clamp(stage, 0, Stages)).Translate();

        public Civilization Civ => civId == int.MinValue ? null : GameComponent_Civilizations.Instance?.Civ(civId);

        public string OwnerLabel
        {
            get
            {
                if (player) return Faction.OfPlayer.Name;
                Civilization c = Civ;
                if (c != null) return c.ShownName;
                return ruin ? "Rimverse_MegaOwnerPrecursor".Translate().ToString() : "Rimverse_MegaOwnerNone".Translate().ToString();
            }
        }

        public string Label
        {
            get
            {
                StarSystemData sys = GameComponent_Universe.Instance?.GetSystem(System);
                string where = sys == null ? systemKey : body >= 0 && sys.Body(body) != null ? sys.Body(body).name : sys.name;
                return "Rimverse_MegaLabel".Translate(KindLabel, where);
            }
        }

        public int SectionCount
        {
            get
            {
                switch (kind)
                {
                    case MegastructureKind.OrbitalRing: return 4096;
                    case MegastructureKind.RingHabitat: return 2048;
                    case MegastructureKind.StellarCollector: return 1024;
                    case MegastructureKind.GiantHabitat: return 512;
                    case MegastructureKind.Citadel: return 64;
                    case MegastructureKind.Gate: return 96;
                    case MegastructureKind.PirateBase:
                    case MegastructureKind.CivStation: return 1;
                    default: return 24;
                }
            }
        }

        public float StageDays
        {
            get
            {
                float baseDays;
                switch (kind)
                {
                    case MegastructureKind.OrbitalRing: baseDays = 30f; break;
                    case MegastructureKind.GiantHabitat: baseDays = 25f; break;
                    case MegastructureKind.StellarCollector: baseDays = 22f; break;
                    case MegastructureKind.Gate: baseDays = 40f; break;
                    default: baseDays = 15f; break;
                }
                return baseDays * (1f + 0.35f * stage);
            }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref systemKey, "systemKey");
            Scribe_Values.Look(ref body, "body", -1);
            Scribe_Values.Look(ref stage, "stage");
            Scribe_Values.Look(ref work, "work");
            Scribe_Values.Look(ref supplied, "supplied");
            Scribe_Values.Look(ref player, "player");
            Scribe_Values.Look(ref civId, "civId", int.MinValue);
            Scribe_Values.Look(ref colonyId, "colonyId", -1);
            Scribe_Values.Look(ref ruin, "ruin");
            Scribe_Values.Look(ref seed, "seed");
            Scribe_Values.Look(ref nodeId, "nodeId", -1);
            Scribe_Values.Look(ref angle, "angle");
        }
    }

    public class GameComponent_Megastructures : GameComponent
    {
        public const int TickInterval = 2500;

        public List<MegastructureData> all = new List<MegastructureData>();
        public HashSet<string> seeded = new HashSet<string>();
        public int nextId = 1;

        public static GameComponent_Megastructures Instance => Current.Game?.GetComponent<GameComponent_Megastructures>();

        public GameComponent_Megastructures(Game game)
        {
        }

        public MegastructureData ById(int id) => all.FirstOrDefault(m => m.id == id);

        public List<MegastructureData> In(string systemKey)
        {
            EnsureSeeded(systemKey);
            return all.Where(m => m.systemKey == systemKey).ToList();
        }

        public IEnumerable<MegastructureData> At(string systemKey, int body)
        {
            EnsureSeeded(systemKey);
            return all.Where(m => m.systemKey == systemKey && (m.body == body || m.AroundStar));
        }

        public MegastructureData Find(string systemKey, int body, MegastructureKind kind, bool player)
        {
            return all.FirstOrDefault(m => m.systemKey == systemKey && m.kind == kind && m.player == player && (m.body == body || m.AroundStar && body < 0 || kind == MegastructureKind.StellarCollector));
        }

        public MegastructureData Register(MegastructureKind kind, string systemKey, int body, bool player, int civId, int stage)
        {
            MegastructureData m = new MegastructureData
            {
                id = nextId++,
                kind = kind,
                systemKey = systemKey,
                body = kind == MegastructureKind.StellarCollector ? -1 : body,
                player = player,
                civId = civId,
                stage = stage,
                seed = Gen.HashCombineInt(systemKey.GetHashCode(), (int)kind * 7919 + body),
                angle = Rand.Range(0f, 360f)
            };
            all.Add(m);
            MegastructureRender.Version++;
            return m;
        }

        public void EnsureSeeded(string systemKey)
        {
            if (systemKey.NullOrEmpty() || seeded.Contains(systemKey)) return;
            GameComponent_Universe u = GameComponent_Universe.Instance;
            if (u == null) return;
            seeded.Add(systemKey);
            StarSystemId id = StarSystemId.FromKey(systemKey);
            if (GateUtility.IsGate(id) && !all.Any(m => m.systemKey == systemKey && m.kind == MegastructureKind.Gate))
            {
                MegastructureData g = Register(MegastructureKind.Gate, systemKey, -1, false, int.MinValue, MegastructureData.Stages);
                g.ruin = true;
                g.angle = (systemKey.GetHashCode() & 0xffff) / 65535f * 360f;
            }
            SpaceBases.Seed(this, id, systemKey);
            GameComponent_Civilizations civs = GameComponent_Civilizations.Instance;
            if (civs == null) return;
            foreach (Civilization c in civs.civilizations)
            {
                if (c.dead || c.capitalKey != systemKey || c.stage < 11) continue;
                CivPlanet cap = c.planets.FirstOrDefault(p => p.systemKey == systemKey);
                int body = cap?.body ?? -1;
                UniverseRand r = new UniverseRand(UniverseRand.Hash(u.universeSeed, 960, c.id));
                List<MegastructureKind> pool = new List<MegastructureKind> { MegastructureKind.OrbitalRing, MegastructureKind.GiantHabitat, MegastructureKind.StellarCollector, MegastructureKind.DeepObservatory, MegastructureKind.Citadel };
                int count = Mathf.Clamp(c.stage - 10, 1, 4);
                for (int i = 0; i < count && pool.Count > 0; i++)
                {
                    MegastructureKind k = pool[r.RangeInclusive(0, pool.Count - 1)];
                    pool.Remove(k);
                    int stage = i == count - 1 ? r.RangeInclusive(1, MegastructureData.Stages - 1) : MegastructureData.Stages;
                    MegastructureData m = Register(k, systemKey, body, false, c.id, stage);
                    m.work = stage < MegastructureData.Stages ? r.Range(0f, 0.9f) : 0f;
                    m.angle = r.Range(0f, 360f);
                }
            }
        }

        public override void GameComponentTick()
        {
            int tick = Verse.Find.TickManager.TicksGame;
            if (tick % TickInterval != 123) return;
            foreach (MegastructureData m in all)
            {
                if (m.player || m.Complete || m.ruin || m.IsBase) continue;
                if (m.colonyId >= 0) continue;
                Civilization c = m.Civ;
                if (c == null || c.dead) continue;
                Advance(m, TickInterval);
            }
            if (tick % (TickInterval * 4) == 123) SyncColonies();
        }

        public bool Advance(MegastructureData m, int ticks)
        {
            if (m.Complete) return false;
            m.work += ticks / (m.StageDays * GenDate.TicksPerDay);
            if (m.work < 1f) return false;
            m.work = 0f;
            m.stage++;
            m.supplied = false;
            MegastructureRender.Version++;
            return true;
        }

        public void SyncColonies()
        {
            GameComponent_PlayerColonies pc = GameComponent_PlayerColonies.Instance;
            if (pc == null) return;
            foreach (PlayerColony c in pc.colonies)
            {
                foreach (ColonyFacility f in c.facilities)
                {
                    MegastructureKind? k = f.def.defName == "Rimverse_Facility_RingHabitat" ? MegastructureKind.RingHabitat : f.def.defName == "Rimverse_Facility_Citadel" ? (MegastructureKind?)MegastructureKind.Citadel : null;
                    if (k == null) continue;
                    MegastructureData m = all.FirstOrDefault(x => x.colonyId == c.id && x.kind == k.Value);
                    int stage = f.Ready ? MegastructureData.Stages : 1;
                    if (m == null)
                    {
                        m = Register(k.Value, c.systemKey, c.bodyIndex, true, int.MinValue, stage);
                        m.colonyId = c.id;
                    }
                    else if (m.stage != stage)
                    {
                        m.stage = stage;
                        MegastructureRender.Version++;
                    }
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref all, "megastructures", LookMode.Deep);
            Scribe_Collections.Look(ref seeded, "seeded", LookMode.Value);
            Scribe_Values.Look(ref nextId, "nextId", 1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (all == null) all = new List<MegastructureData>();
                if (seeded == null) seeded = new HashSet<string>();
                MegastructureRender.Version++;
            }
        }
    }
}
