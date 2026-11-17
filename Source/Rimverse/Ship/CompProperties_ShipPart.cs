using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Rimverse
{
    public class CompProperties_ShipPart : CompProperties
    {
        public ShipPartRole role = ShipPartRole.Other;
        public float mass;
        public float heatPerSecond;
        public bool heatOnlyWhenThrusting;
        public float heatRemovalPerSecond;
        public float heatCapacity;
        public float thrust;
        public float dodgeBonus;
        public float airPerHour;
        public float airCapacity;
        public float fuelCapacity;
        public float warpCapacity;
        public float hullPoints;
        public int maxCells;
        public int extraCells;
        public float cargoTons;
        public int gravityCells;
        public float sensorSensitivity;
        public float signatureFactor = 1f;
        public float signatureAdd;
        public bool lifeSupport;
        public bool essential;
        public int warpRangeLy;
        public int warpCooldownHours;
        public float warpMaxMass;
        public float aiPilotLevel;

        public CompProperties_ShipPart()
        {
            compClass = typeof(CompShipPart);
        }

        public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
        {
            foreach (StatDrawEntry entry in base.SpecialDisplayStats(req))
            {
                yield return entry;
            }
            StatCategoryDef cat = StatCategoryDefOf.Building;
            yield return new StatDrawEntry(cat, "Rimverse_Stat_Role".Translate(), ("Rimverse_Role_" + role).Translate(), "Rimverse_Stat_Role_Desc".Translate(), 5000);
            if (mass > 0f) yield return new StatDrawEntry(cat, "Rimverse_Stat_Mass".Translate(), mass.ToString("0.##") + " t", "Rimverse_Stat_Mass_Desc".Translate(), 4990);
            if (thrust > 0f) yield return new StatDrawEntry(cat, "Rimverse_Stat_Thrust".Translate(), thrust.ToString("0"), "Rimverse_Stat_Thrust_Desc".Translate(), 4980);
            if (heatPerSecond > 0f) yield return new StatDrawEntry(cat, "Rimverse_Stat_Heat".Translate(), "+" + heatPerSecond.ToString("0.##") + " UC/s", "Rimverse_Stat_Heat_Desc".Translate(), 4970);
            if (heatRemovalPerSecond > 0f) yield return new StatDrawEntry(cat, "Rimverse_Stat_HeatRemoval".Translate(), "-" + heatRemovalPerSecond.ToString("0.##") + " UC/s", "Rimverse_Stat_HeatRemoval_Desc".Translate(), 4960);
            if (heatCapacity > 0f) yield return new StatDrawEntry(cat, "Rimverse_Stat_HeatCapacity".Translate(), "+" + heatCapacity.ToString("0") + " UC", "Rimverse_Stat_HeatCapacity_Desc".Translate(), 4950);
            if (airPerHour > 0f) yield return new StatDrawEntry(cat, "Rimverse_Stat_AirPerHour".Translate(), "+" + airPerHour.ToString("0.##") + " APH/h", "Rimverse_Stat_AirPerHour_Desc".Translate(), 4940);
            if (airCapacity > 0f) yield return new StatDrawEntry(cat, "Rimverse_Stat_AirCapacity".Translate(), airCapacity.ToString("0") + " APH", "Rimverse_Stat_AirCapacity_Desc".Translate(), 4930);
            if (fuelCapacity > 0f) yield return new StatDrawEntry(cat, "Rimverse_Stat_FuelCapacity".Translate(), fuelCapacity.ToString("0"), "Rimverse_Stat_FuelCapacity_Desc".Translate(), 4920);
            if (hullPoints > 0f) yield return new StatDrawEntry(cat, "Rimverse_Stat_HullPoints".Translate(), hullPoints.ToString("0"), "Rimverse_Stat_HullPoints_Desc".Translate(), 4910);
            if (maxCells > 0) yield return new StatDrawEntry(cat, "Rimverse_Stat_MaxCells".Translate(), maxCells.ToString(), "Rimverse_Stat_MaxCells_Desc".Translate(), 4900);
            if (extraCells > 0) yield return new StatDrawEntry(cat, "Rimverse_Stat_ExtraCells".Translate(), "+" + extraCells, "Rimverse_Stat_ExtraCells_Desc".Translate(), 4890);
            if (cargoTons > 0f) yield return new StatDrawEntry(cat, "Rimverse_Stat_Cargo".Translate(), "+" + cargoTons.ToString("0") + " t", "Rimverse_Stat_Cargo_Desc".Translate(), 4880);
            if (sensorSensitivity > 0f) yield return new StatDrawEntry(cat, "Rimverse_Stat_Sensor".Translate(), sensorSensitivity.ToString("0"), "Rimverse_Stat_Sensor_Desc".Translate(), 4865);
            if (warpRangeLy > 0) yield return new StatDrawEntry(cat, "Rimverse_Stat_WarpRange".Translate(), "Rimverse_UnitLy".Translate(warpRangeLy), "Rimverse_Stat_WarpRange_Desc".Translate(), 4860);
            if (warpMaxMass > 0f) yield return new StatDrawEntry(cat, "Rimverse_Stat_WarpMaxMass".Translate(), warpMaxMass.ToString("0") + " t", "Rimverse_Stat_WarpMaxMass_Desc".Translate(), 4855);
            if (warpCooldownHours > 0) yield return new StatDrawEntry(cat, "Rimverse_Stat_WarpCooldown".Translate(), warpCooldownHours + " h", "Rimverse_Stat_WarpCooldown_Desc".Translate(), 4850);
            if (warpCapacity > 0f) yield return new StatDrawEntry(cat, "Rimverse_Stat_WarpCapacity".Translate(), warpCapacity.ToString("0"), "Rimverse_Stat_WarpCapacity_Desc".Translate(), 4845);
            if (aiPilotLevel > 0f) yield return new StatDrawEntry(cat, "Rimverse_Stat_AIPilot".Translate(), aiPilotLevel.ToString("0"), "Rimverse_Stat_AIPilot_Desc".Translate(), 4842);
            if (signatureFactor < 1f) yield return new StatDrawEntry(cat, "Rimverse_Stat_Signature".Translate(), "x" + signatureFactor.ToString("0.##"), "Rimverse_Stat_Signature_Desc".Translate(), 4841);
            if (signatureAdd > 0f) yield return new StatDrawEntry(cat, "Rimverse_Stat_Signature".Translate(), "+" + signatureAdd.ToStringPercent(), "Rimverse_Stat_SignatureAdd_Desc".Translate(), 4840);
            if (dodgeBonus > 0f) yield return new StatDrawEntry(cat, "Rimverse_Stat_Dodge".Translate(), "+" + dodgeBonus.ToStringPercent(), "Rimverse_Stat_Dodge_Desc".Translate(), 4870);
        }
    }
}
