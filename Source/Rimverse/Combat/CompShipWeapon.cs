using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public enum CombatBand
    {
        Far,
        Medium,
        Near,
        Boarding
    }

    public class CompProperties_ShipWeapon : CompProperties
    {
        public CombatBand band = CombatBand.Medium;
        public ThingDef projectile;
        public int burst = 1;
        public float damage;
        public float cooldownSeconds = 8f;
        public float heatPerShot = 1f;
        public float ammoPerShot;
        public float shieldMultiplier = 1f;
        public bool onlyShields;
        public float interceptChance;
        public bool tractor;
        public bool missile;
        public float jamming;

        public CompProperties_ShipWeapon()
        {
            compClass = typeof(CompShipWeapon);
        }

        public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
        {
            foreach (StatDrawEntry e in base.SpecialDisplayStats(req)) yield return e;
            StatCategoryDef cat = StatCategoryDefOf.Weapon;
            yield return new StatDrawEntry(cat, "Rimverse_Stat_WeaponBand".Translate(), ("Rimverse_Band_" + band).Translate(), "Rimverse_Stat_WeaponBand_Desc".Translate(), 5000);
            if (damage > 0f) yield return new StatDrawEntry(cat, "Rimverse_Stat_WeaponDamage".Translate(), burst > 1 ? damage.ToString("0") + " × " + burst : damage.ToString("0"), "Rimverse_Stat_WeaponDamage_Desc".Translate(), 4990);
            yield return new StatDrawEntry(cat, "Rimverse_Stat_WeaponCooldown".Translate(), cooldownSeconds.ToString("0.#") + " s", "Rimverse_Stat_WeaponCooldown_Desc".Translate(), 4980);
            if (heatPerShot > 0f) yield return new StatDrawEntry(cat, "Rimverse_Stat_WeaponHeat".Translate(), heatPerShot.ToString("0.#") + " UC", "Rimverse_Stat_WeaponHeat_Desc".Translate(), 4970);
            if (interceptChance > 0f) yield return new StatDrawEntry(cat, "Rimverse_Stat_WeaponIntercept".Translate(), interceptChance.ToStringPercent(), "Rimverse_Stat_WeaponIntercept_Desc".Translate(), 4960);
        }
    }

    public class CompShipWeapon : ThingComp
    {
        public int nextFireTick;

        public CompProperties_ShipWeapon Props => (CompProperties_ShipWeapon)props;

        public bool Ready
        {
            get
            {
                if (!parent.Spawned) return false;
                CompPowerTrader power = parent.GetComp<CompPowerTrader>();
                if (power != null && !power.PowerOn) return false;
                CompShipPart part = parent.GetComp<CompShipPart>();
                if (part != null && !part.Operating) return false;
                CompBreakdownable bd = parent.GetComp<CompBreakdownable>();
                if (bd != null && bd.BrokenDown) return false;
                if (Props.ammoPerShot > 0f)
                {
                    CompRefuelable ammo = parent.GetComp<CompRefuelable>();
                    if (ammo != null && ammo.Fuel < Props.ammoPerShot) return false;
                }
                return Find.TickManager.TicksGame >= nextFireTick;
            }
        }

        public bool Active
        {
            get
            {
                if (!parent.Spawned) return false;
                CompPowerTrader power = parent.GetComp<CompPowerTrader>();
                if (power != null && !power.PowerOn) return false;
                CompShipPart part = parent.GetComp<CompShipPart>();
                return part == null || part.Operating;
            }
        }

        public void ConsumeShot()
        {
            nextFireTick = Find.TickManager.TicksGame + Mathf.RoundToInt(Props.cooldownSeconds * 60f);
            if (Props.ammoPerShot > 0f) parent.GetComp<CompRefuelable>()?.ConsumeFuel(Props.ammoPerShot);
        }

        public override string CompInspectStringExtra()
        {
            int left = nextFireTick - Find.TickManager.TicksGame;
            string band = ("Rimverse_Band_" + Props.band).Translate();
            return left > 0 ? "Rimverse_WeaponReloading".Translate(band, (left / 60f).ToString("0.#")) : "Rimverse_WeaponReady".Translate(band);
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref nextFireTick, "nextFireTick");
        }
    }

    public class CompProperties_ShipShield : CompProperties
    {
        public float maxHitPoints = 300f;
        public float regenPerSecond = 3f;

        public CompProperties_ShipShield()
        {
            compClass = typeof(CompShipShield);
        }

        public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
        {
            foreach (StatDrawEntry e in base.SpecialDisplayStats(req)) yield return e;
            yield return new StatDrawEntry(StatCategoryDefOf.Building, "Rimverse_Stat_ShieldHP".Translate(), maxHitPoints.ToString("0"), "Rimverse_Stat_ShieldHP_Desc".Translate(), 5000);
            yield return new StatDrawEntry(StatCategoryDefOf.Building, "Rimverse_Stat_ShieldRegen".Translate(), regenPerSecond.ToString("0.#") + "/s", "Rimverse_Stat_ShieldRegen_Desc".Translate(), 4990);
        }
    }

    public class CompShipShield : ThingComp
    {
        public float hitPoints;

        public CompProperties_ShipShield Props => (CompProperties_ShipShield)props;

        public bool Online
        {
            get
            {
                if (!parent.Spawned) return false;
                CompPowerTrader power = parent.GetComp<CompPowerTrader>();
                if (power != null && !power.PowerOn) return false;
                CompShipPart part = parent.GetComp<CompShipPart>();
                return part == null || part.Operating;
            }
        }

        public override void CompTick()
        {
            if (!parent.IsHashIntervalTick(60)) return;
            if (Online) hitPoints = Mathf.Min(Props.maxHitPoints, hitPoints + Props.regenPerSecond);
            else hitPoints = Mathf.Max(0f, hitPoints - Props.maxHitPoints * 0.1f);
        }

        public float Absorb(float amount)
        {
            if (!Online || hitPoints <= 0f) return amount;
            float take = Mathf.Min(hitPoints, amount);
            hitPoints -= take;
            return amount - take;
        }

        public override string CompInspectStringExtra()
        {
            return "Rimverse_ShieldStatus".Translate(hitPoints.ToString("0"), Props.maxHitPoints.ToString("0"));
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref hitPoints, "hitPoints");
        }
    }
}
