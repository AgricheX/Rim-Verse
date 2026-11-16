using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public enum DroneMode
    {
        Clean,
        Haul,
        Mine
    }

    public class CompProperties_DroneBay : CompProperties
    {
        public DroneMode mode = DroneMode.Clean;
        public int intervalTicks = 500;
        public int perInterval = 3;
        public float radius = 20f;

        public CompProperties_DroneBay()
        {
            compClass = typeof(CompDroneBay);
        }
    }

    public class CompDroneBay : ThingComp
    {
        private int done;
        private CompPowerTrader power;

        public CompProperties_DroneBay Props => (CompProperties_DroneBay)props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            power = parent.GetComp<CompPowerTrader>();
        }

        public override void CompTick()
        {
            base.CompTick();
            if (!parent.IsHashIntervalTick(Props.intervalTicks)) return;
            if (power != null && !power.PowerOn) return;
            Map map = parent.Map;
            if (map == null) return;
            switch (Props.mode)
            {
                case DroneMode.Clean: Clean(map); break;
                case DroneMode.Haul: Haul(map); break;
                case DroneMode.Mine: Mine(map); break;
            }
        }

        private IEnumerable<IntVec3> Cells(Map map) => GenRadial.RadialCellsAround(parent.Position, Props.radius, true).Where(c => c.InBounds(map));

        private int Budget => Mathf.Max(1, Mathf.RoundToInt(Props.perInterval * DroneWorkshopUtility.Factor(parent)));

        private void Clean(Map map)
        {
            int left = Budget;
            foreach (IntVec3 c in Cells(map))
            {
                List<Thing> things = c.GetThingList(map);
                for (int i = things.Count - 1; i >= 0 && left > 0; i--)
                {
                    if (things[i] is Filth f && !f.Destroyed)
                    {
                        f.Destroy();
                        done++;
                        left--;
                    }
                }
                if (left <= 0) return;
            }
        }

        private void Haul(Map map)
        {
            int left = Budget;
            foreach (IntVec3 c in Cells(map).ToList())
            {
                List<Thing> things = c.GetThingList(map);
                for (int i = things.Count - 1; i >= 0 && left > 0; i--)
                {
                    Thing t = things[i];
                    if (t.def.category != ThingCategory.Item || t.IsForbidden(Faction.OfPlayer) || t.IsInValidStorage()) continue;
                    if (!StoreUtility.TryFindBestBetterStoreCellFor(t, null, map, StoreUtility.CurrentStoragePriorityOf(t), Faction.OfPlayer, out IntVec3 dest)) continue;
                    if (dest.DistanceTo(parent.Position) > Props.radius) continue;
                    Thing moved = t;
                    moved.DeSpawn();
                    GenPlace.TryPlaceThing(moved, dest, map, ThingPlaceMode.Direct);
                    if (!moved.Spawned) GenPlace.TryPlaceThing(moved, dest, map, ThingPlaceMode.Near);
                    done++;
                    left--;
                }
                if (left <= 0) return;
            }
        }

        private void Mine(Map map)
        {
            foreach (IntVec3 c in Cells(map))
            {
                if (map.designationManager.DesignationAt(c, DesignationDefOf.Mine) == null) continue;
                Building b = c.GetEdifice(map);
                if (b == null || !b.def.mineable) continue;
                b.TakeDamage(new DamageInfo(DamageDefOf.Mining, 40 * Budget));
                if (b.Destroyed) done++;
                return;
            }
        }

        public override string CompInspectStringExtra() => ("Rimverse_DroneBay_" + Props.mode).Translate(Props.radius.ToString("0"), done);

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref done, "done");
        }
    }

    public class CompProperties_HeatDump : CompProperties
    {
        public float heat = 50f;
        public float fuelPerUse = 10f;

        public CompProperties_HeatDump()
        {
            compClass = typeof(CompHeatDump);
        }
    }

    public class CompHeatDump : ThingComp
    {
        public CompProperties_HeatDump Props => (CompProperties_HeatDump)props;

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo g in base.CompGetGizmosExtra()) yield return g;
            if (parent.Faction != Faction.OfPlayer) yield break;
            CompRefuelable fuel = parent.GetComp<CompRefuelable>();
            Command_Action a = new Command_Action
            {
                defaultLabel = "Rimverse_HeatDump".Translate(),
                defaultDesc = "Rimverse_HeatDumpDesc".Translate(Props.heat.ToString("0"), Props.fuelPerUse.ToString("0")),
                icon = RimverseTex.HeatDump,
                action = Dump
            };
            if (fuel != null && fuel.Fuel < Props.fuelPerUse) a.Disable("Rimverse_HeatDumpNoFuel".Translate());
            yield return a;
        }

        private void Dump()
        {
            RimverseShip ship = parent.Map?.GetComponent<MapComponent_RimverseShips>()?.ShipAt(parent.Position);
            if (ship?.state == null) return;
            parent.GetComp<CompRefuelable>()?.ConsumeFuel(Props.fuelPerUse);
            ship.state.heat = Mathf.Max(0f, ship.state.heat - Props.heat);
            FleckMaker.ThrowSmoke(parent.DrawPos, parent.Map, 3f);
        }
    }

    public class CompProperties_FoamEmitter : CompProperties
    {
        public int intervalTicks = 250;
        public float radius = 6f;
        public int cooldownTicks = 2500;

        public CompProperties_FoamEmitter()
        {
            compClass = typeof(CompFoamEmitter);
        }
    }

    public class CompFoamEmitter : ThingComp
    {
        private int nextReady;

        public CompProperties_FoamEmitter Props => (CompProperties_FoamEmitter)props;

        public override void CompTick()
        {
            base.CompTick();
            if (!parent.IsHashIntervalTick(Props.intervalTicks) || Find.TickManager.TicksGame < nextReady) return;
            CompPowerTrader power = parent.GetComp<CompPowerTrader>();
            if (power != null && !power.PowerOn) return;
            Map map = parent.Map;
            Room room = parent.GetRoom();
            bool fire = GenRadial.RadialCellsAround(parent.Position, Props.radius, true).Any(c => c.InBounds(map) && c.GetFirstThing<Fire>(map) != null && (room == null || c.GetRoom(map) == room));
            if (!fire) return;
            GenExplosion.DoExplosion(parent.Position, map, Props.radius, DamageDefOf.Extinguish, parent, postExplosionSpawnThingDef: ThingDefOf.Filth_FireFoam, postExplosionSpawnChance: 1f, postExplosionSpawnThingCount: 3, applyDamageToExplosionCellsNeighbors: true);
            nextReady = Find.TickManager.TicksGame + Props.cooldownTicks;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref nextReady, "nextReady");
        }
    }

    public static class CombatVentUtility
    {
        private static readonly Dictionary<int, int> vented = new Dictionary<int, int>();

        public static bool IsVented(Room room)
        {
            if (room == null) return false;
            if (!vented.TryGetValue(room.ID, out int until)) return false;
            if (Find.TickManager.TicksGame > until)
            {
                vented.Remove(room.ID);
                return false;
            }
            return true;
        }

        public static void Vent(Room room, int ticks)
        {
            if (room != null) vented[room.ID] = Find.TickManager.TicksGame + ticks;
        }
    }

    public class CompProperties_CombatVent : CompProperties
    {
        public int durationTicks = 2500;

        public CompProperties_CombatVent()
        {
            compClass = typeof(CompCombatVent);
        }
    }

    public class CompCombatVent : ThingComp
    {
        public CompProperties_CombatVent Props => (CompProperties_CombatVent)props;

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo g in base.CompGetGizmosExtra()) yield return g;
            if (parent.Faction != Faction.OfPlayer) yield break;
            yield return new Command_Action
            {
                defaultLabel = "Rimverse_CombatVent".Translate(),
                defaultDesc = "Rimverse_CombatVentDesc".Translate(),
                icon = RimverseTex.Vent,
                action = () =>
                {
                    Room room = parent.GetRoom();
                    CombatVentUtility.Vent(room, Props.durationTicks);
                    Messages.Message("Rimverse_CombatVentMsg".Translate(), parent, MessageTypeDefOf.CautionInput, false);
                }
            };
        }
    }

    public class CompProperties_SecurityCentral : CompProperties
    {
        public CompProperties_SecurityCentral()
        {
            compClass = typeof(CompSecurityCentral);
        }
    }

    public class CompSecurityCentral : ThingComp
    {
        private bool lockdown;

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo g in base.CompGetGizmosExtra()) yield return g;
            if (parent.Faction != Faction.OfPlayer) yield break;
            yield return new Command_Toggle
            {
                defaultLabel = "Rimverse_Lockdown".Translate(),
                defaultDesc = "Rimverse_LockdownDesc".Translate(),
                icon = RimverseTex.Lockdown,
                isActive = () => lockdown,
                toggleAction = () =>
                {
                    lockdown = !lockdown;
                    foreach (Building_Door d in parent.Map.listerBuildings.AllBuildingsColonistOfClass<Building_Door>())
                    {
                        d.SetForbidden(lockdown, false);
                    }
                }
            };
        }

        public override string CompInspectStringExtra()
        {
            Map map = parent.Map;
            if (map == null) return null;
            int hostiles = map.mapPawns.AllPawnsSpawned.Count(p => p.HostileTo(Faction.OfPlayer) && !p.Downed);
            return "Rimverse_SecurityInspect".Translate(hostiles);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref lockdown, "lockdown");
        }
    }

    public class CompProperties_RadiationLeak : CompProperties
    {
        public float radius = 6f;
        public float severityPerHour = 0.02f;
        public float hpFraction = 0.5f;

        public CompProperties_RadiationLeak()
        {
            compClass = typeof(CompRadiationLeak);
        }
    }

    public class CompRadiationLeak : ThingComp
    {
        public CompProperties_RadiationLeak Props => (CompProperties_RadiationLeak)props;

        public override void CompTickRare()
        {
            base.CompTickRare();
            if (!parent.Spawned || parent.HitPoints > parent.MaxHitPoints * Props.hpFraction) return;
            HediffDef rad = DefDatabase<HediffDef>.GetNamedSilentFail("Rimverse_RadiationSickness");
            if (rad == null) return;
            float sev = Props.severityPerHour * 250f / RimverseUnits.TicksPerHour;
            foreach (Pawn p in parent.Map.mapPawns.AllPawnsSpawned.ToList())
            {
                if (p.Position.DistanceTo(parent.Position) > Props.radius || !p.RaceProps.IsFlesh) continue;
                HealthUtility.AdjustSeverity(p, rad, sev * (1f - GameComponent_Radiation.Resistance(p)));
            }
        }

        public override string CompInspectStringExtra() => parent.HitPoints <= parent.MaxHitPoints * Props.hpFraction ? "Rimverse_RadiationLeaking".Translate().ToString() : null;
    }

    public class CompProperties_Megastructure : CompProperties
    {
        public float researchPerDay;
        public int silverPerDay;
        public float traderMtbDays;
        public ThingDef producedThing;
        public int producedPerDay;
        public bool megastructure;
        public MegastructureKind kind;
        public float stageCostFactor = 0.5f;

        public CompProperties_Megastructure()
        {
            compClass = typeof(CompMegastructure);
        }
    }

    public class CompMegastructure : ThingComp
    {
        private float carry;
        private int dataId = -1;

        public CompProperties_Megastructure Props => (CompProperties_Megastructure)props;

        public MegastructureData Data
        {
            get
            {
                if (!Props.megastructure) return null;
                GameComponent_Megastructures gc = GameComponent_Megastructures.Instance;
                if (gc == null) return null;
                MegastructureData d = dataId >= 0 ? gc.ById(dataId) : null;
                if (d == null && parent.Spawned) d = Link(gc);
                return d;
            }
        }

        public float OutputFactor
        {
            get
            {
                if (!Props.megastructure) return 1f;
                MegastructureData d = Data;
                return d == null ? 0f : d.Output;
            }
        }

        private MegastructureData Link(GameComponent_Megastructures gc)
        {
            MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(parent.Map);
            if (loc == null || loc.systemKey.NullOrEmpty()) return null;
            MegastructureData d = gc.Find(loc.systemKey, loc.bodyIndex, Props.kind, true);
            if (d == null) d = gc.Register(Props.kind, loc.systemKey, loc.bodyIndex, true, int.MinValue, 0);
            d.nodeId = parent.thingIDNumber;
            dataId = d.id;
            return d;
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad && Props.megastructure) _ = Data;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref carry, "carry");
            Scribe_Values.Look(ref dataId, "megaId", -1);
        }

        public List<ThingDefCountClass> StageCost(MegastructureData d)
        {
            List<ThingDefCountClass> list = new List<ThingDefCountClass>();
            if (parent.def.costList == null || d == null) return list;
            float f = Props.stageCostFactor * (1f + 0.25f * d.stage) * (ResearchGate.Done("Rimverse_MegaLogistics") ? 0.75f : 1f);
            foreach (ThingDefCountClass c in parent.def.costList) list.Add(new ThingDefCountClass(c.thingDef, Mathf.Max(1, Mathf.RoundToInt(c.count * f))));
            return list;
        }

        public override void CompTickRare()
        {
            base.CompTickRare();
            if (!parent.Spawned) return;
            CompPowerTrader power = parent.GetComp<CompPowerTrader>();
            bool powered = power == null || power.PowerOn;
            if (Props.megastructure)
            {
                MegastructureData d = Data;
                if (d != null && d.supplied && !d.Complete && powered && GameComponent_Megastructures.Instance.Advance(d, 250))
                {
                    Find.LetterStack.ReceiveLetter("Rimverse_LetterMegaStage".Translate(d.KindLabel), "Rimverse_LetterMegaStageDesc".Translate(d.Label, d.StageLabel, d.stage, MegastructureData.Stages), d.Complete ? LetterDefOf.PositiveEvent : LetterDefOf.NeutralEvent, parent);
                    IdeoEvents.OnMegastructureStage(parent.Map, d);
                }
            }
            if (!powered) return;
            float factor = OutputFactor;
            if (factor <= 0f) return;
            float frac = 250f / 60000f;
            if (Props.researchPerDay > 0f && Find.ResearchManager.GetProject() != null) Find.ResearchManager.ResearchPerformed(Props.researchPerDay * factor * frac, null);
            if (Props.silverPerDay > 0 || (Props.producedThing != null && Props.producedPerDay > 0))
            {
                carry += frac;
                if (carry >= 1f)
                {
                    carry -= 1f;
                    if (Props.silverPerDay > 0) Spawn(ThingDefOf.Silver, Mathf.RoundToInt(Props.silverPerDay * factor));
                    if (Props.producedThing != null) Spawn(Props.producedThing, Mathf.Max(1, Mathf.RoundToInt(Props.producedPerDay * factor)));
                }
            }
            if (Props.traderMtbDays > 0f && Rand.MTBEventOccurs(Props.traderMtbDays / factor, 60000f, 250f))
            {
                IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.Misc, parent.Map);
                IncidentDefOf.OrbitalTraderArrival.Worker.TryExecute(parms);
            }
        }

        private void Spawn(ThingDef def, int count)
        {
            if (count <= 0) return;
            Thing t = ThingMaker.MakeThing(def);
            t.stackCount = count;
            GenPlace.TryPlaceThing(t, parent.InteractionCell.IsValid && parent.def.hasInteractionCell ? parent.InteractionCell : parent.Position, parent.Map, ThingPlaceMode.Near);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo g in base.CompGetGizmosExtra()) yield return g;
            if (!Props.megastructure || parent.Faction != Faction.OfPlayer) yield break;
            MegastructureData d = Data;
            if (d == null || d.Complete) yield break;
            List<ThingDefCountClass> cost = StageCost(d);
            string costText = string.Join(", ", cost.Select(c => c.Label));
            Command_Action supply = new Command_Action
            {
                defaultLabel = "Rimverse_CmdMegaSupply".Translate(("Rimverse_MegaStage_" + (d.stage + 1)).Translate()),
                defaultDesc = "Rimverse_CmdMegaSupplyDesc".Translate(costText, d.StageDays.ToString("0")),
                icon = RimverseTex.ShipPanel,
                action = () =>
                {
                    foreach (ThingDefCountClass c in cost)
                    {
                        if (ColonyUtility.CountOnMap(parent.Map, x => x == c.thingDef) < c.count) return;
                    }
                    foreach (ThingDefCountClass c in cost) ColonyUtility.Consume(parent.Map, x => x == c.thingDef, c.count);
                    d.supplied = true;
                    Messages.Message("Rimverse_MsgMegaSupplied".Translate(d.Label), parent, MessageTypeDefOf.PositiveEvent, false);
                }
            };
            if (d.supplied) supply.Disable("Rimverse_MegaAlreadySupplied".Translate());
            else
            {
                List<string> missing = cost.Where(c => ColonyUtility.CountOnMap(parent.Map, x => x == c.thingDef) < c.count).Select(c => c.Label).ToList();
                if (missing.Count > 0) supply.Disable("Rimverse_MegaMissing".Translate(string.Join(", ", missing)));
            }
            yield return supply;
            if (DebugSettings.ShowDevGizmos)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEV: next stage",
                    action = () =>
                    {
                        d.supplied = true;
                        GameComponent_Megastructures.Instance.Advance(d, 999999999);
                    }
                };
            }
        }

        public override string CompInspectStringExtra()
        {
            List<string> parts = new List<string>();
            MegastructureData d = Props.megastructure ? Data : null;
            if (d != null)
            {
                parts.Add("Rimverse_MegaInspectStage".Translate(d.StageLabel, d.stage, MegastructureData.Stages, d.Completion.ToStringPercent()));
                if (!d.Complete) parts.Add(d.supplied ? "Rimverse_MegaInspectWorking".Translate(d.work.ToStringPercent(), d.StageDays.ToString("0")).ToString() : "Rimverse_MegaInspectNeedsSupply".Translate().ToString());
            }
            float f = OutputFactor;
            if (Props.researchPerDay > 0f) parts.Add("Rimverse_MegaResearch".Translate((Props.researchPerDay * f).ToString("0")));
            if (Props.silverPerDay > 0) parts.Add("Rimverse_MegaSilver".Translate(Mathf.RoundToInt(Props.silverPerDay * f)));
            if (Props.producedThing != null) parts.Add("Rimverse_MegaProduce".Translate(Mathf.RoundToInt(Props.producedPerDay * f), Props.producedThing.label));
            if (Props.traderMtbDays > 0f && f > 0f) parts.Add("Rimverse_MegaTraders".Translate((Props.traderMtbDays / f).ToString("0")));
            return parts.Count == 0 ? null : string.Join("\n", parts);
        }
    }

    public class CompPowerPlantMegastructure : CompPowerPlant
    {
        protected override float DesiredPowerOutput
        {
            get
            {
                CompMegastructure m = parent.GetComp<CompMegastructure>();
                return base.DesiredPowerOutput * (m == null ? 1f : m.OutputFactor);
            }
        }
    }
}
