using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Rimverse
{
    [DefOf]
    public static class QolJobDefOf
    {
        public static JobDef Rimverse_MultiHaul;

        static QolJobDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(QolJobDefOf));
        }
    }

    [HarmonyPatch(typeof(WorkGiver_HaulGeneral), nameof(WorkGiver_HaulGeneral.JobOnThing))]
    public static class Patch_QolMultiHaul
    {
        public static void Postfix(Pawn pawn, Thing t, bool forced, ref Job __result)
        {
            if (__result == null || __result.def != JobDefOf.HaulToCell || forced || pawn.Drafted || !QolFeatures.IsEnabled("Rimverse_Qol_MultiHaul")) return;
            if (pawn.inventory == null || !pawn.RaceProps.Humanlike) return;
            Map map = pawn.Map;
            float free = MassUtility.FreeSpace(pawn);
            float firstMass = t.GetStatValue(StatDefOf.Mass) * t.stackCount;
            if (free < firstMass) return;
            free -= firstMass;
            List<Thing> extra = new List<Thing>();
            foreach (Thing o in map.listerHaulables.ThingsPotentiallyNeedingHauling())
            {
                if (extra.Count >= 6) break;
                if (o == t || !o.Spawned || o.def.category != ThingCategory.Item || o.Position.DistanceToSquared(t.Position) > 144) continue;
                if (o.IsForbidden(pawn) || !pawn.CanReserve(o) || !HaulAIUtility.PawnCanAutomaticallyHaulFast(pawn, o, false)) continue;
                float m = o.GetStatValue(StatDefOf.Mass) * o.stackCount;
                if (m > free) continue;
                if (!StoreUtility.TryFindBestBetterStoreCellFor(o, pawn, map, StoreUtility.CurrentStoragePriorityOf(o), pawn.Faction, out _, false)) continue;
                free -= m;
                extra.Add(o);
            }
            if (extra.Count == 0) return;
            Job job = JobMaker.MakeJob(QolJobDefOf.Rimverse_MultiHaul);
            job.targetQueueA = new List<LocalTargetInfo> { t };
            job.countQueue = new List<int> { t.stackCount };
            foreach (Thing o in extra)
            {
                job.targetQueueA.Add(o);
                job.countQueue.Add(o.stackCount);
            }
            __result = job;
        }
    }

    public class JobDriver_RimverseMultiHaul : JobDriver
    {
        private List<Thing> carried = new List<Thing>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref carried, "rimverseCarried", LookMode.Reference);
            if (carried == null) carried = new List<Thing>();
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            pawn.ReserveAsManyAsPossible(job.GetTargetQueue(TargetIndex.A), job);
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Toil extract = Toils_JobTransforms.ExtractNextTargetFromQueue(TargetIndex.A);
            yield return extract;
            Toil next = ToilMaker.MakeToil("RimverseMultiHaulNext");
            Toil gotoThing = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch).JumpIfDespawnedOrNullOrForbidden(TargetIndex.A, next);
            yield return gotoThing;
            Toil take = ToilMaker.MakeToil("RimverseMultiHaulTake");
            take.initAction = delegate
            {
                Thing thing = job.GetTarget(TargetIndex.A).Thing;
                if (thing == null || !thing.Spawned) return;
                int count = Mathf.Min(thing.stackCount, MassUtility.CountToPickUpUntilOverEncumbered(pawn, thing));
                if (count <= 0) return;
                Thing split = thing.SplitOff(count);
                if (pawn.inventory.innerContainer.TryAdd(split, true))
                {
                    if (!carried.Contains(split)) carried.Add(split);
                }
                else if (!split.Spawned)
                {
                    GenPlace.TryPlaceThing(split, pawn.Position, pawn.Map, ThingPlaceMode.Near);
                }
            };
            yield return take;
            yield return next;
            yield return Toils_Jump.JumpIf(extract, () => !job.GetTargetQueue(TargetIndex.A).NullOrEmpty());
            Toil pick = ToilMaker.MakeToil("RimverseMultiHaulPick");
            Toil done = ToilMaker.MakeToil("RimverseMultiHaulDone");
            pick.initAction = delegate
            {
                carried.RemoveAll(c => c == null || c.Destroyed || !pawn.inventory.innerContainer.Contains(c));
                if (carried.Count == 0)
                {
                    pawn.jobs.curDriver.JumpToToil(done);
                    return;
                }
                Thing c0 = carried[0];
                if (StoreUtility.TryFindBestBetterStoreCellFor(c0, pawn, pawn.Map, StoragePriority.Unstored, pawn.Faction, out IntVec3 cell)) job.SetTarget(TargetIndex.B, cell);
                else job.SetTarget(TargetIndex.B, pawn.Position);
            };
            yield return pick;
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.ClosestTouch);
            Toil drop = ToilMaker.MakeToil("RimverseMultiHaulDrop");
            drop.initAction = delegate
            {
                if (carried.Count == 0) return;
                Thing c0 = carried[0];
                carried.RemoveAt(0);
                if (!pawn.inventory.innerContainer.Contains(c0)) return;
                IntVec3 cell = job.GetTarget(TargetIndex.B).Cell;
                if (!pawn.inventory.innerContainer.TryDrop(c0, cell, pawn.Map, ThingPlaceMode.Direct, out Thing _)) pawn.inventory.innerContainer.TryDrop(c0, pawn.Position, pawn.Map, ThingPlaceMode.Near, out Thing _);
            };
            yield return drop;
            yield return Toils_Jump.Jump(pick);
            yield return done;
        }
    }

    public class FloatMenuOptionProvider_QolPutHere : FloatMenuOptionProvider
    {
        protected override bool Drafted => true;

        protected override bool Undrafted => true;

        protected override bool Multiselect => false;

        protected override bool RequiresManipulation => true;

        protected override FloatMenuOption GetSingleOptionFor(Thing clickedThing, FloatMenuContext context)
        {
            if (!QolFeatures.IsEnabled("Rimverse_Qol_PutHere") || clickedThing == null || clickedThing.def.category != ThingCategory.Item || !clickedThing.def.EverHaulable) return null;
            Pawn pawn = context.FirstSelectedPawn;
            if (pawn == null || !pawn.CanReserveAndReach(clickedThing, PathEndMode.ClosestTouch, Danger.Deadly)) return null;
            Thing thing = clickedThing;
            return new FloatMenuOption("Rimverse_QolPutHere".Translate(thing.LabelShort), delegate
            {
                Find.Targeter.BeginTargeting(new TargetingParameters { canTargetLocations = true, canTargetPawns = false, canTargetBuildings = false, canTargetItems = false }, delegate (LocalTargetInfo target)
                {
                    if (!target.Cell.IsValid || !target.Cell.Standable(pawn.Map)) return;
                    Job job = HaulAIUtility.HaulToCellStorageJob(pawn, thing, target.Cell, false);
                    if (job != null) pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                });
            });
        }
    }

    [HarmonyPatch(typeof(Dialog_FormCaravan), nameof(Dialog_FormCaravan.DoWindowContents))]
    public static class Patch_QolInstantCaravan
    {
        private static readonly MethodInfo reformMethod = AccessTools.Method(typeof(Dialog_FormCaravan), "TryReformCaravan");

        public static void Postfix(Dialog_FormCaravan __instance, Rect inRect, Map ___map, bool ___reform)
        {
            if (___reform || ___map == null || !QolFeatures.IsEnabled("Rimverse_Qol_InstantCaravan")) return;
            if (GenHostility.AnyHostileActiveThreatToPlayer(___map, true)) return;
            Rect b = new Rect(inRect.x, inRect.yMax - 38f, 200f, 38f);
            TooltipHandler.TipRegion(b, "Rimverse_QolInstantCaravanDesc".Translate());
            if (Widgets.ButtonText(b, "Rimverse_QolInstantCaravan".Translate()))
            {
                if ((bool)reformMethod.Invoke(__instance, null)) __instance.Close(false);
            }
        }
    }
}
