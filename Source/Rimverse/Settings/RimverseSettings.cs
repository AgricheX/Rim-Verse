using System.Collections.Generic;
using Verse;

namespace Rimverse
{
    public class RimverseSettings : ModSettings
    {
        public bool debugLogging;

        public float threatMultiplier = 1f;
        public float economyMultiplier = 1f;
        public float researchCostMultiplier = 1f;

        public int galaxyCap = 32;
        public float starDensityFactor = 1f;
        public bool civilizationSimulation = true;
        public float civilizationDensity = 1f;
        public bool endgameEnabled = true;
        public float crisisStartYears = 10f;
        public int civilizationTickInterval = RimverseUnits.TicksPerDay;

        public float airConsumptionMultiplier = 1f;
        public float fuelConsumptionMultiplier = 1f;
        public float breachDrainMultiplier = 1f;
        public bool shipCombatEnabled = true;
        public bool lineagesEnabled = true;
        public bool lifeAboardEnabled = true;
        public bool hudOnlyShip;
        public float hudOffsetY;

        public bool fluxoEnabled = true;
        public float fluxoSensitiveChance = 0.005f;

        public bool qolEnabled = true;
        public HashSet<string> disabledQol = new HashSet<string>();
        public HashSet<string> enabledQol = new HashSet<string>();

        public bool odysseyIntegration = true;
        public bool combatExtendedIntegration = true;
        public bool endlessRoomsEvents = true;

        public bool IsQolEnabled(QolFeatureDef def)
        {
            if (def.enabledByDefault)
            {
                return !disabledQol.Contains(def.defName);
            }
            return enabledQol.Contains(def.defName);
        }

        public void SetQolEnabled(QolFeatureDef def, bool value)
        {
            disabledQol.Remove(def.defName);
            enabledQol.Remove(def.defName);
            if (def.enabledByDefault && !value)
            {
                disabledQol.Add(def.defName);
            }
            else if (!def.enabledByDefault && value)
            {
                enabledQol.Add(def.defName);
            }
        }

        public void ResetToDefaults()
        {
            debugLogging = false;
            threatMultiplier = 1f;
            economyMultiplier = 1f;
            researchCostMultiplier = 1f;
            galaxyCap = 32;
            starDensityFactor = 1f;
            civilizationSimulation = true;
            civilizationDensity = 1f;
            endgameEnabled = true;
            crisisStartYears = 10f;
            civilizationTickInterval = RimverseUnits.TicksPerDay;
            airConsumptionMultiplier = 1f;
            fuelConsumptionMultiplier = 1f;
            breachDrainMultiplier = 1f;
            shipCombatEnabled = true;
            lineagesEnabled = true;
            lifeAboardEnabled = true;
            fluxoEnabled = true;
            fluxoSensitiveChance = 0.005f;
            qolEnabled = true;
            disabledQol.Clear();
            enabledQol.Clear();
            odysseyIntegration = true;
            combatExtendedIntegration = true;
            endlessRoomsEvents = true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref debugLogging, "debugLogging", false);
            Scribe_Values.Look(ref threatMultiplier, "threatMultiplier", 1f);
            Scribe_Values.Look(ref economyMultiplier, "economyMultiplier", 1f);
            Scribe_Values.Look(ref researchCostMultiplier, "researchCostMultiplier", 1f);
            Scribe_Values.Look(ref galaxyCap, "galaxyCap", 32);
            Scribe_Values.Look(ref starDensityFactor, "starDensityFactor", 1f);
            Scribe_Values.Look(ref civilizationSimulation, "civilizationSimulation", true);
            Scribe_Values.Look(ref civilizationDensity, "civilizationDensity", 1f);
            Scribe_Values.Look(ref endgameEnabled, "endgameEnabled", true);
            Scribe_Values.Look(ref crisisStartYears, "crisisStartYears", 10f);
            Scribe_Values.Look(ref civilizationTickInterval, "civilizationTickInterval", RimverseUnits.TicksPerDay);
            Scribe_Values.Look(ref airConsumptionMultiplier, "airConsumptionMultiplier", 1f);
            Scribe_Values.Look(ref fuelConsumptionMultiplier, "fuelConsumptionMultiplier", 1f);
            Scribe_Values.Look(ref breachDrainMultiplier, "breachDrainMultiplier", 1f);
            Scribe_Values.Look(ref shipCombatEnabled, "shipCombatEnabled", true);
            Scribe_Values.Look(ref lineagesEnabled, "lineagesEnabled", true);
            Scribe_Values.Look(ref lifeAboardEnabled, "lifeAboardEnabled", true);
            Scribe_Values.Look(ref hudOnlyShip, "hudOnlyShip", false);
            Scribe_Values.Look(ref hudOffsetY, "hudOffsetY", 0f);
            Scribe_Values.Look(ref fluxoEnabled, "fluxoEnabled", true);
            Scribe_Values.Look(ref fluxoSensitiveChance, "fluxoSensitiveChance", 0.005f);
            Scribe_Values.Look(ref qolEnabled, "qolEnabled", true);
            Scribe_Collections.Look(ref disabledQol, "disabledQol", LookMode.Value);
            Scribe_Collections.Look(ref enabledQol, "enabledQol", LookMode.Value);
            Scribe_Values.Look(ref odysseyIntegration, "odysseyIntegration", true);
            Scribe_Values.Look(ref combatExtendedIntegration, "combatExtendedIntegration", true);
            Scribe_Values.Look(ref endlessRoomsEvents, "endlessRoomsEvents", true);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (disabledQol == null) disabledQol = new HashSet<string>();
                if (enabledQol == null) enabledQol = new HashSet<string>();
            }
        }
    }
}
