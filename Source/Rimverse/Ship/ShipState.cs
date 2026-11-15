using System.Collections.Generic;
using Verse;

namespace Rimverse
{
    public class ShipState : IExposable
    {
        public int shipId;
        public string name;
        public float airStock;
        public float heat;
        public float lostHullPoints;
        public List<IntVec3> lostHullCells = new List<IntVec3>();
        public Dictionary<int, float> roomAir = new Dictionary<int, float>();
        public int lastBreachTick = -99999;
        public int lastFireTick = -99999;
        public int lastShutdownTick = -99999;
        public bool thrusting;
        public bool warping;
        public int lastWarpTick = -9999999;

        public void ExposeData()
        {
            Scribe_Values.Look(ref shipId, "shipId");
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref airStock, "airStock");
            Scribe_Values.Look(ref heat, "heat");
            Scribe_Values.Look(ref lostHullPoints, "lostHullPoints");
            Scribe_Collections.Look(ref lostHullCells, "lostHullCells", LookMode.Value);
            Scribe_Values.Look(ref lastBreachTick, "lastBreachTick", -99999);
            Scribe_Values.Look(ref lastFireTick, "lastFireTick", -99999);
            Scribe_Values.Look(ref lastShutdownTick, "lastShutdownTick", -99999);
            Scribe_Values.Look(ref thrusting, "thrusting");
            Scribe_Values.Look(ref warping, "warping");
            Scribe_Values.Look(ref lastWarpTick, "lastWarpTick", -9999999);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (lostHullCells == null) lostHullCells = new List<IntVec3>();
                if (roomAir == null) roomAir = new Dictionary<int, float>();
            }
        }
    }
}
