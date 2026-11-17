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

        public bool spaceBackground = true;
        public float backgroundStarFactor = 1f;
        public bool megastructureVisuals = true;
        public bool warpStreaks = true;
        public bool pauseUnseenMaps;
        public bool orbitalTraffic = true;
        public bool cityHorizon = true;

        public string PerfKey => (spaceBackground ? 1 : 0) + ":" + backgroundStarFactor.ToString("0.00") + ":" + (megastructureVisuals ? 1 : 0) + ":" + (warpStreaks ? 1 : 0) + ":" + (orbitalTraffic ? 1 : 0) + ":" + (cityHorizon ? 1 : 0);

        public bool odysseyIntegration = true;
        public bool combatExtendedIntegration = true;
        public bool endlessRoomsEvents = true;
        public bool compatShipVersions = true;
        public bool compatWeapons = true;
        public bool compatAnomalyExtras = true;
        public bool compatPsycasts = true;
        public bool compatRealisticRooms = true;
        public bool compatAlliesGoodwill = true;
        public bool compatNoForcedSlowdown = false;
        public bool compatRimmuNation = true;

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
            spaceBackground = true;
            backgroundStarFactor = 1f;
            megastructureVisuals = true;
            warpStreaks = true;
            pauseUnseenMaps = false;
            orbitalTraffic = true;
            cityHorizon = true;
            odysseyIntegration = true;
            combatExtendedIntegration = true;
            endlessRoomsEvents = true;
            compatShipVersions = true;
            compatWeapons = true;
            compatAnomalyExtras = true;
            compatPsycasts = true;
            compatRealisticRooms = true;
            compatAlliesGoodwill = true;
            compatNoForcedSlowdown = false;
            compatRimmuNation = true;
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
            Scribe_Values.Look(ref spaceBackground, "spaceBackground", true);
            Scribe_Values.Look(ref backgroundStarFactor, "backgroundStarFactor", 1f);
            Scribe_Values.Look(ref megastructureVisuals, "megastructureVisuals", true);
            Scribe_Values.Look(ref warpStreaks, "warpStreaks", true);
            Scribe_Values.Look(ref pauseUnseenMaps, "pauseUnseenMaps", false);
            Scribe_Values.Look(ref orbitalTraffic, "orbitalTraffic", true);
            Scribe_Values.Look(ref cityHorizon, "cityHorizon", true);
            Scribe_Values.Look(ref odysseyIntegration, "odysseyIntegration", true);
            Scribe_Values.Look(ref combatExtendedIntegration, "combatExtendedIntegration", true);
            Scribe_Values.Look(ref endlessRoomsEvents, "endlessRoomsEvents", true);
            Scribe_Values.Look(ref compatShipVersions, "compatShipVersions", true);
            Scribe_Values.Look(ref compatWeapons, "compatWeapons", true);
            Scribe_Values.Look(ref compatAnomalyExtras, "compatAnomalyExtras", true);
            Scribe_Values.Look(ref compatPsycasts, "compatPsycasts", true);
            Scribe_Values.Look(ref compatRealisticRooms, "compatRealisticRooms", true);
            Scribe_Values.Look(ref compatAlliesGoodwill, "compatAlliesGoodwill", true);
            Scribe_Values.Look(ref compatNoForcedSlowdown, "compatNoForcedSlowdown", false);
            Scribe_Values.Look(ref compatRimmuNation, "compatRimmuNation", true);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (disabledQol == null) disabledQol = new HashSet<string>();
                if (enabledQol == null) enabledQol = new HashSet<string>();
            }
        }
    }
}
