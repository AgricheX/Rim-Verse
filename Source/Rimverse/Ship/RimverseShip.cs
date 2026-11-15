using System.Collections.Generic;
using Verse;

namespace Rimverse
{
    public class RimverseShip
    {
        public int id;
        public Map map;
        public Building bridge;
        public ShipState state;
        public HashSet<IntVec3> cells = new HashSet<IntVec3>();
        public List<CompShipPart> parts = new List<CompShipPart>();
        public List<Room> rooms = new List<Room>();
        public int cellLimit;
        public bool overLimit;
        public bool multipleBridges;

        public float mass;
        public float hullMax;
        public float hullCurrent;
        public float thrust;
        public float dodgeBonus;
        public float heatCapacity;
        public float heatGeneration;
        public float heatRemoval;
        public float airCapacity;
        public float airGeneration;
        public float fuelCapacity;
        public float warpCapacity;
        public int warpRangeLy;
        public float warpMaxMass;
        public int warpCooldownHours;
        public float cargoTons;
        public int gravityCells;
        public float sensorSensitivity;
        public int sealedCells;
        public int crewCount;
        public float aiPilotLevel;
        public float signature = 1f;
        public ShipClass shipClass;

        public string Label => state?.name ?? bridge?.LabelShort ?? "?";

        public float Integrity => hullMax <= 0f ? 1f : hullCurrent / (hullMax + state.lostHullPoints);

        public float AirFraction => airCapacity <= 0f ? 0f : state.airStock / airCapacity;

        public float Signature => UnityEngine.Mathf.Clamp(signature * (1f + 0.5f * HeatFraction), 0.15f, 1.5f);

        public float HeatFraction => heatCapacity <= 0f ? 0f : state.heat / heatCapacity;

        public bool HasRole(ShipPartRole role)
        {
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i].Role == role) return true;
            }
            return false;
        }

        public int CountRole(ShipPartRole role, bool operatingOnly = false)
        {
            int n = 0;
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i].Role == role && (!operatingOnly || parts[i].Operating)) n++;
            }
            return n;
        }

        public IEnumerable<CompShipPart> PartsWithRole(ShipPartRole role)
        {
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i].Role == role) yield return parts[i];
            }
        }

        public bool Contains(IntVec3 c) => cells.Contains(c);
    }
}
