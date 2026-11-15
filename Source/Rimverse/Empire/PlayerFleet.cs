using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public enum PlayerFleetMission
    {
        Idle,
        Defend,
        Attack,
        Raid,
        Capture,
        Blockade
    }

    public class PlayerFleet : IExposable
    {
        public int id;
        public string name;
        public string systemKey;
        public string destKey;
        public int arriveTick = -1;
        public PlayerFleetMission mission = PlayerFleetMission.Idle;
        public int targetCiv = -1;
        public float condition = 1f;
        public Dictionary<string, int> units = new Dictionary<string, int>();
        public List<FleetOrder> orders = new List<FleetOrder>();

        public bool Moving => !destKey.NullOrEmpty() && arriveTick > 0;

        public IEnumerable<KeyValuePair<FleetHullDef, int>> Units
        {
            get
            {
                foreach (KeyValuePair<string, int> kv in units)
                {
                    FleetHullDef d = DefDatabase<FleetHullDef>.GetNamedSilentFail(kv.Key);
                    if (d != null && kv.Value > 0) yield return new KeyValuePair<FleetHullDef, int>(d, kv.Value);
                }
            }
        }

        public int Count(FleetHullDef d) => units.TryGetValue(d.defName, out int n) ? n : 0;

        public void AddUnits(FleetHullDef d, int n)
        {
            int cur = Count(d) + n;
            if (cur <= 0) units.Remove(d.defName);
            else units[d.defName] = cur;
        }

        public float Power => Units.Sum(kv => kv.Key.power * kv.Value) * condition;

        public float Ground => Units.Sum(kv => kv.Key.ground * kv.Value) * condition;

        public float Upkeep => Units.Sum(kv => kv.Key.upkeep * kv.Value);

        public bool Empty => !Units.Any();

        public float Speed
        {
            get
            {
                List<FleetHullDef> hulls = Units.Where(kv => !kv.Key.squad).Select(kv => kv.Key).ToList();
                return hulls.Count == 0 ? 0f : hulls.Min(h => h.speedLyPerDay);
            }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref systemKey, "systemKey");
            Scribe_Values.Look(ref destKey, "destKey");
            Scribe_Values.Look(ref arriveTick, "arriveTick", -1);
            Scribe_Values.Look(ref mission, "mission", PlayerFleetMission.Idle);
            Scribe_Values.Look(ref targetCiv, "targetCiv", -1);
            Scribe_Values.Look(ref condition, "condition", 1f);
            Scribe_Collections.Look(ref units, "units", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref orders, "orders", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (units == null) units = new Dictionary<string, int>();
                if (orders == null) orders = new List<FleetOrder>();
            }
        }
    }

    public class FleetOrder : IExposable
    {
        public FleetHullDef def;
        public int count;
        public int readyTick;

        public void ExposeData()
        {
            Scribe_Defs.Look(ref def, "def");
            Scribe_Values.Look(ref count, "count");
            Scribe_Values.Look(ref readyTick, "readyTick");
        }
    }

    public enum WarGoal
    {
        Conquest,
        Humiliation,
        Subjugation,
        Plunder
    }

    public class WarData : IExposable
    {
        public int civId;
        public WarGoal goal = WarGoal.Humiliation;
        public float score;
        public float exhaustion;
        public int startTick;

        public void ExposeData()
        {
            Scribe_Values.Look(ref civId, "civId");
            Scribe_Values.Look(ref goal, "goal", WarGoal.Humiliation);
            Scribe_Values.Look(ref score, "score");
            Scribe_Values.Look(ref exhaustion, "exhaustion");
            Scribe_Values.Look(ref startTick, "startTick");
        }
    }

    public class EmpireClaim : IExposable
    {
        public int civId;
        public string systemKey;
        public int body;

        public void ExposeData()
        {
            Scribe_Values.Look(ref civId, "civId");
            Scribe_Values.Look(ref systemKey, "systemKey");
            Scribe_Values.Look(ref body, "body");
        }
    }

    public class ActiveEdict : IExposable
    {
        public EdictDef def;
        public int endTick;

        public void ExposeData()
        {
            Scribe_Defs.Look(ref def, "def");
            Scribe_Values.Look(ref endTick, "endTick");
        }
    }

    public class SectorGovernor : IExposable
    {
        public string systemKey;
        public Pawn pawn;

        public void ExposeData()
        {
            Scribe_Values.Look(ref systemKey, "systemKey");
            Scribe_References.Look(ref pawn, "pawn");
        }
    }

    public class ActiveResolution : IExposable
    {
        public ResolutionDef def;
        public int targetCiv = -1;
        public int endTick;

        public void ExposeData()
        {
            Scribe_Defs.Look(ref def, "def");
            Scribe_Values.Look(ref targetCiv, "targetCiv", -1);
            Scribe_Values.Look(ref endTick, "endTick");
        }
    }

    public class FoundAnomaly : IExposable
    {
        public SpaceAnomalyDef def;
        public string systemKey;
        public int body;
        public int chapter;
        public int workEndTick = -1;
        public bool done;

        public bool Working => workEndTick > 0 && !done;

        public void ExposeData()
        {
            Scribe_Defs.Look(ref def, "def");
            Scribe_Values.Look(ref systemKey, "systemKey");
            Scribe_Values.Look(ref body, "body");
            Scribe_Values.Look(ref chapter, "chapter");
            Scribe_Values.Look(ref workEndTick, "workEndTick", -1);
            Scribe_Values.Look(ref done, "done");
        }
    }
}
