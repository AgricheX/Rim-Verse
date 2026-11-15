using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Rimverse
{
    public class CompProperties_ColdStorage : CompProperties
    {
        public CompProperties_ColdStorage()
        {
            compClass = typeof(CompColdStorage);
        }
    }

    public class CompColdStorage : ThingComp
    {
        public override void CompTick()
        {
            if (!parent.IsHashIntervalTick(250) || !parent.Spawned) return;
            CompPowerTrader power = parent.TryGetComp<CompPowerTrader>();
            if (power != null && !power.PowerOn) return;
            foreach (IntVec3 c in parent.OccupiedRect())
            {
                List<Thing> list = c.GetThingList(parent.Map);
                for (int i = 0; i < list.Count; i++)
                {
                    CompRottable r = list[i].TryGetComp<CompRottable>();
                    if (r != null) r.RotProgress = Mathf.Max(0f, r.RotProgress - 250f);
                }
            }
        }
    }

    public class AutoHydroExtension : DefModExtension
    {
        public bool autoSow;
        public bool autoHarvest;
        public bool deliver;
        public bool sealedPods;
    }

    public class Building_RimverseHydroponics : Building_PlantGrower
    {
        public bool neededMode;

        private AutoHydroExtension Ext => def.GetModExtension<AutoHydroExtension>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref neededMode, "neededMode", false);
        }

        public override void TickRare()
        {
            base.TickRare();
            AutoHydroExtension ext = Ext;
            if (ext == null || !Spawned) return;
            CompPowerTrader power = GetComp<CompPowerTrader>();
            if (power != null && !power.PowerOn) return;
            if (!ext.sealedPods && GravityUtility.GravityAt(Map, Position) < LifeAboard.ZeroG) return;
            ThingDef plantDef = neededMode ? PickNeeded() ?? GetPlantDefToGrow() : GetPlantDefToGrow();
            foreach (IntVec3 c in this.OccupiedRect())
            {
                Plant p = c.GetPlant(Map);
                if (p == null)
                {
                    if (ext.autoSow && plantDef != null && c.GetFirstItem(Map) == null)
                    {
                        Plant sown = (Plant)GenSpawn.Spawn(ThingMaker.MakeThing(plantDef), c, Map);
                        sown.Growth = Plant.BaseSownGrowthPercent;
                        sown.sown = true;
                    }
                    continue;
                }
                if (!ext.autoHarvest || !p.HarvestableNow || p.def.plant.harvestedThingDef == null) continue;
                int y = p.YieldNow();
                if (y > 0)
                {
                    Thing prod = ThingMaker.MakeThing(p.def.plant.harvestedThingDef);
                    prod.stackCount = y;
                    if (ext.deliver && StoreUtility.TryFindBestBetterStoreCellFor(prod, null, Map, StoragePriority.Unstored, Faction.OfPlayer, out IntVec3 dest, false))
                    {
                        GenPlace.TryPlaceThing(prod, dest, Map, ThingPlaceMode.Near);
                    }
                    else GenPlace.TryPlaceThing(prod, InteractionCellOrNear(), Map, ThingPlaceMode.Near);
                }
                p.Destroy();
            }
        }

        private IntVec3 InteractionCellOrNear() => def.hasInteractionCell ? InteractionCell : Position;

        private ThingDef PickNeeded()
        {
            ThingDef best = null;
            int bestCount = int.MaxValue;
            foreach (ThingDef d in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (d.plant == null || d.plant.harvestedThingDef == null || d.plant.sowTags == null || !d.plant.sowTags.Contains(def.building.sowTag)) continue;
                if (d.plant.sowResearchPrerequisites != null && d.plant.sowResearchPrerequisites.Any(r => !r.IsFinished)) continue;
                if (!d.plant.harvestedThingDef.IsNutritionGivingIngestible && !d.plant.harvestedThingDef.IsMedicine) continue;
                int count = Map.resourceCounter.GetCount(d.plant.harvestedThingDef);
                if (count < bestCount)
                {
                    bestCount = count;
                    best = d;
                }
            }
            return best;
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo g in base.GetGizmos()) yield return g;
            if (Ext == null || !Ext.autoSow) yield break;
            yield return new Command_Toggle
            {
                defaultLabel = "Rimverse_CmdNeededMode".Translate(),
                defaultDesc = "Rimverse_CmdNeededModeDesc".Translate(),
                icon = RimverseTex.ShipPanel,
                isActive = () => neededMode,
                toggleAction = () => neededMode = !neededMode
            };
        }

        public override string GetInspectString()
        {
            string s = base.GetInspectString();
            AutoHydroExtension ext = Ext;
            if (ext == null) return s;
            string line = ext.autoHarvest ? "Rimverse_HydroAuto".Translate() : "Rimverse_HydroSemi".Translate();
            if (neededMode) line += " " + "Rimverse_HydroNeeded".Translate();
            if (!ext.sealedPods && Spawned && GravityUtility.GravityAt(Map, Position) < LifeAboard.ZeroG) line += "\n" + "Rimverse_HydroNeedsGravity".Translate();
            return s.NullOrEmpty() ? line : s + "\n" + line;
        }
    }

    public class CompProperties_Arsenal : CompProperties
    {
        public float radius = 20f;

        public CompProperties_Arsenal()
        {
            compClass = typeof(CompArsenal);
        }
    }

    public class CompArsenal : ThingComp
    {
        private List<Pawn> armed = new List<Pawn>();
        private bool alert;

        public CompProperties_Arsenal Props => (CompProperties_Arsenal)props;

        public override void PostExposeData()
        {
            Scribe_Collections.Look(ref armed, "armed", LookMode.Reference);
            Scribe_Values.Look(ref alert, "alert", false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) armed = armed?.Where(p => p != null).ToList() ?? new List<Pawn>();
        }

        public override void CompTick()
        {
            if (!parent.IsHashIntervalTick(250) || !parent.Spawned) return;
            Map map = parent.Map;
            bool threat = GenHostility.AnyHostileActiveThreatToPlayer(map);
            if (threat)
            {
                alert = true;
                List<Thing> weapons = parent.OccupiedRect().Cells.SelectMany(c => c.GetThingList(map)).Where(t => t.def.IsWeapon && !t.IsForbidden(Faction.OfPlayer)).ToList();
                if (weapons.Count == 0) return;
                foreach (Pawn p in map.mapPawns.FreeColonistsSpawned)
                {
                    if (weapons.Count == 0) break;
                    if (p.Downed || p.Drafted || p.InMentalState || p.equipment == null || p.equipment.Primary != null || p.WorkTagIsDisabled(WorkTags.Violent)) continue;
                    if (p.Position.DistanceTo(parent.Position) > Props.radius || p.CurJobDef == JobDefOf.Equip) continue;
                    Thing w = weapons.Where(x => p.CanReserveAndReach(x, PathEndMode.ClosestTouch, Danger.Deadly)).OrderByDescending(x => x.MarketValue).FirstOrDefault();
                    if (w == null) continue;
                    weapons.Remove(w);
                    p.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Equip, w), JobCondition.InterruptForced);
                    armed.Add(p);
                }
            }
            else if (alert)
            {
                alert = false;
                foreach (Pawn p in armed)
                {
                    if (p == null || !p.Spawned || p.Map != map || p.Drafted || p.equipment?.Primary == null) continue;
                    p.equipment.TryDropEquipment(p.equipment.Primary, out _, p.Position, false);
                }
                armed.Clear();
            }
        }

        public override string CompInspectStringExtra() => alert ? "Rimverse_ArsenalAlert".Translate(armed.Count) : "Rimverse_ArsenalReady".Translate();
    }

    public class CompProperties_AutoMedPod : CompProperties
    {
        public float quality = 0.7f;
        public int intervalTicks = 2500;

        public CompProperties_AutoMedPod()
        {
            compClass = typeof(CompAutoMedPod);
        }
    }

    public class CompAutoMedPod : ThingComp
    {
        public CompProperties_AutoMedPod Props => (CompProperties_AutoMedPod)props;

        public override void CompTick()
        {
            if (!parent.IsHashIntervalTick(Props.intervalTicks) || !(parent is Building_Bed bed) || !parent.Spawned) return;
            CompPowerTrader power = parent.TryGetComp<CompPowerTrader>();
            if (power != null && !power.PowerOn) return;
            foreach (Pawn p in bed.CurOccupants.ToList())
            {
                int i = 0;
                foreach (Hediff h in p.health.hediffSet.hediffs.Where(x => x.TendableNow()).ToList())
                {
                    h.Tended(Props.quality * Rand.Range(0.85f, 1.1f), 1f, i++);
                }
                if (i > 0) FleckMaker.ThrowMetaIcon(p.Position, p.Map, FleckDefOf.HealingCross);
            }
        }

        public override string CompInspectStringExtra() => "Rimverse_MedPodInspect".Translate(Props.quality.ToStringPercent());
    }
}
