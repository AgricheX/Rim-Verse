using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public class CompProperties_MaintenanceDrones : CompProperties
    {
        public int intervalTicks = 2500;
        public int repairPerInterval = 80;
        public float radius = 30f;
        public bool repairVehicles;

        public CompProperties_MaintenanceDrones()
        {
            compClass = typeof(CompMaintenanceDrones);
        }
    }

    public class CompMaintenanceDrones : ThingComp
    {
        private int repairedTotal;

        public CompProperties_MaintenanceDrones Props => (CompProperties_MaintenanceDrones)props;

        public override void CompTick()
        {
            if (!parent.Spawned || !parent.IsHashIntervalTick(Props.intervalTicks)) return;
            CompPowerTrader power = parent.TryGetComp<CompPowerTrader>();
            if (power != null && !power.PowerOn) return;
            Map map = parent.Map;
            RimverseShip ship = map.GetComponent<MapComponent_RimverseShips>()?.Ships.FirstOrDefault(s => s.Contains(parent.Position));
            int budget = Mathf.RoundToInt(Props.repairPerInterval * DroneWorkshopUtility.Factor(parent));
            IEnumerable<Building> targets = map.listerBuildings.allBuildingsColonist.Where(b => b.def.useHitPoints && b.HitPoints < b.MaxHitPoints && (ship != null ? ship.Contains(b.Position) : b.Position.InHorDistOf(parent.Position, Props.radius))).OrderBy(b => b.HitPoints / (float)b.MaxHitPoints).ToList();
            foreach (Building b in targets)
            {
                if (budget <= 0) break;
                int amount = Mathf.Min(budget, b.MaxHitPoints - b.HitPoints);
                b.HitPoints += amount;
                budget -= amount;
                repairedTotal += amount;
                map.listerBuildingsRepairable.Notify_BuildingRepaired(b);
                FleckMaker.ThrowMicroSparks(b.DrawPos, map);
            }
            if (Props.repairVehicles && budget > 0)
            {
                foreach (Pawn p in map.mapPawns.AllPawnsSpawned.ToList())
                {
                    if (budget <= 0) break;
                    if (!(p is Vehicles.VehiclePawn v) || v.Faction != Faction.OfPlayer || v.statHandler == null || !v.statHandler.NeedsRepairs) continue;
                    if (ship != null ? !ship.Contains(v.Position) : !v.Position.InHorDistOf(parent.Position, Props.radius)) continue;
                    foreach (Vehicles.VehicleComponent c in v.statHandler.components)
                    {
                        if (budget <= 0) break;
                        float miss = c.MaxHealth - c.Health;
                        if (miss <= 0f) continue;
                        int amount = Mathf.Min(budget, Mathf.CeilToInt(miss));
                        c.HealComponent(amount);
                        budget -= amount;
                        repairedTotal += amount;
                    }
                    FleckMaker.ThrowMicroSparks(v.DrawPos, map);
                }
            }
        }

        public override string CompInspectStringExtra() => "Rimverse_DronesInspect".Translate(Props.repairPerInterval, repairedTotal);

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref repairedTotal, "repairedTotal");
        }
    }

    public class CompProperties_AutoGalley : CompProperties
    {
        public int intervalTicks = 1250;
        public float radius = 6f;
        public ThingDef meal;
        public float nutritionPerMeal = 0.5f;

        public CompProperties_AutoGalley()
        {
            compClass = typeof(CompAutoGalley);
        }
    }

    public class CompAutoGalley : ThingComp
    {
        public bool active = true;

        public CompProperties_AutoGalley Props => (CompProperties_AutoGalley)props;

        private static bool IsRaw(Thing t)
        {
            IngestibleProperties ing = t.def.ingestible;
            if (ing == null || t.def.IsDrug || t.def.IsCorpse) return false;
            if (ing.preferability != FoodPreferability.RawBad && ing.preferability != FoodPreferability.RawTasty) return false;
            return t.GetStatValue(StatDefOf.Nutrition) > 0f;
        }

        public override void CompTick()
        {
            if (!active || !parent.Spawned || !parent.IsHashIntervalTick(Props.intervalTicks)) return;
            CompPowerTrader power = parent.TryGetComp<CompPowerTrader>();
            if (power != null && !power.PowerOn) return;
            Map map = parent.Map;
            ThingDef meal = Props.meal ?? ThingDefOf.MealSimple;
            List<Thing> raw = GenRadial.RadialDistinctThingsAround(parent.Position, map, Props.radius, true).Where(t => t.def.category == ThingCategory.Item && IsRaw(t) && !t.IsForbidden(Faction.OfPlayer)).ToList();
            float available = raw.Sum(t => t.GetStatValue(StatDefOf.Nutrition) * t.stackCount);
            if (available < Props.nutritionPerMeal) return;
            float need = Props.nutritionPerMeal;
            foreach (Thing t in raw)
            {
                if (need <= 0f) break;
                float per = t.GetStatValue(StatDefOf.Nutrition);
                int count = Mathf.Min(t.stackCount, Mathf.CeilToInt(need / per));
                need -= per * count;
                if (count >= t.stackCount) t.Destroy();
                else t.SplitOff(count).Destroy();
            }
            Thing made = ThingMaker.MakeThing(meal);
            IntVec3 at = parent.def.hasInteractionCell ? parent.InteractionCell : parent.Position;
            GenPlace.TryPlaceThing(made, at, map, ThingPlaceMode.Near);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            yield return new Command_Toggle
            {
                defaultLabel = "Rimverse_GalleyToggle".Translate(),
                defaultDesc = "Rimverse_GalleyToggleDesc".Translate(),
                icon = ThingDefOf.MealSimple.uiIcon,
                isActive = () => active,
                toggleAction = () => active = !active
            };
        }

        public override string CompInspectStringExtra() => "Rimverse_GalleyInspect".Translate(Props.radius.ToString("0"));

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref active, "active", true);
        }
    }
}
