using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Rimverse
{
    public static class QolWork5
    {
        public static int MaxBills() => QolFeatures.IsEnabled("Rimverse_Qol_NoMaxBills") ? 999 : 15;

        public static IEnumerable<CodeInstruction> ReplaceFifteen(IEnumerable<CodeInstruction> code)
        {
            MethodInfo m = AccessTools.Method(typeof(QolWork5), nameof(MaxBills));
            foreach (CodeInstruction c in code)
            {
                if ((c.opcode == OpCodes.Ldc_I4_S || c.opcode == OpCodes.Ldc_I4) && System.Convert.ToInt32(c.operand) == 15)
                {
                    CodeInstruction n = new CodeInstruction(OpCodes.Call, m);
                    n.labels.AddRange(c.labels);
                    n.blocks.AddRange(c.blocks);
                    yield return n;
                }
                else yield return c;
            }
        }

        public static bool IsCookingBill(Job job)
        {
            RecipeDef r = job?.bill?.recipe;
            return r != null && r.products != null && r.products.Any(p => p.thingDef != null && p.thingDef.IsNutritionGivingIngestible);
        }

        public static readonly HashSet<int> treatFirst = new HashSet<int>();
    }

    [HarmonyPatch(typeof(BillStack), nameof(BillStack.AddBill))]
    public static class Patch_QolNoMaxBillsAdd
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code) => QolWork5.ReplaceFifteen(code);
    }

    [HarmonyPatch(typeof(ITab_Bills), "FillTab")]
    public static class Patch_QolNoMaxBillsTab
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code) => QolWork5.ReplaceFifteen(code);
    }

    [HarmonyPatch(typeof(WorkGiver_Scanner), "get_Prioritized")]
    public static class Patch_QolPrioritized
    {
        public static void Postfix(WorkGiver_Scanner __instance, ref bool __result)
        {
            if (__result) return;
            if (__instance is WorkGiver_CleanFilth && QolFeatures.IsEnabled("Rimverse_Qol_CleaningPriority")) __result = true;
            else if (__instance is WorkGiver_TendOther && QolFeatures.IsEnabled("Rimverse_Qol_TreatFirst") && QolWork5.treatFirst.Count > 0) __result = true;
        }
    }

    [HarmonyPatch(typeof(WorkGiver_Scanner), nameof(WorkGiver_Scanner.GetPriority), new[] { typeof(Pawn), typeof(TargetInfo) })]
    public static class Patch_QolPriority
    {
        public static void Postfix(WorkGiver_Scanner __instance, Pawn pawn, TargetInfo t, ref float __result)
        {
            if (__instance is WorkGiver_CleanFilth && QolFeatures.IsEnabled("Rimverse_Qol_CleaningPriority") && t.HasThing && t.Thing.Spawned)
            {
                Room r = t.Thing.GetRoom();
                float bonus = 0f;
                if (r != null && r.Role != null)
                {
                    string role = r.Role.defName;
                    if (role == "Kitchen" || role == "Hospital") bonus = 1000f;
                    else if (role == "DiningRoom" || role == "RecRoom") bonus = 300f;
                }
                __result += bonus - t.Thing.Position.DistanceTo(pawn.Position);
            }
            else if (__instance is WorkGiver_TendOther && QolFeatures.IsEnabled("Rimverse_Qol_TreatFirst") && t.Thing is Pawn p)
            {
                __result += QolWork5.treatFirst.Contains(p.thingIDNumber) ? 1000f : 0f;
                __result -= t.Thing.Position.DistanceTo(pawn.Position);
            }
        }
    }

    [HarmonyPatch(typeof(WorkGiver_ConstructAffectFloor), nameof(WorkGiver_ConstructAffectFloor.HasJobOnCell))]
    public static class Patch_QolSmoothWallsFirst
    {
        public static void Postfix(WorkGiver_ConstructAffectFloor __instance, Pawn pawn, IntVec3 c, ref bool __result)
        {
            if (!__result || !(__instance is WorkGiver_ConstructSmoothFloor) || !QolFeatures.IsEnabled("Rimverse_Qol_SmartConstruction")) return;
            Room room = c.GetRoom(pawn.Map);
            if (room == null || room.TouchesMapEdge || room.CellCount > 400) return;
            foreach (Designation d in pawn.Map.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.SmoothWall))
            {
                IntVec3 w = d.target.Cell;
                foreach (IntVec3 n in GenAdj.CellsAdjacentCardinal(w, Rot4.North, IntVec2.One))
                {
                    if (n.InBounds(pawn.Map) && n.GetRoom(pawn.Map) == room)
                    {
                        __result = false;
                        return;
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(WorkGiver_DoBill), nameof(WorkGiver_DoBill.JobOnThing))]
    public static class Patch_QolCleanBeforeCooking
    {
        public static void Postfix(Pawn pawn, Thing thing, ref Job __result)
        {
            if (__result == null || !QolFeatures.IsEnabled("Rimverse_Qol_CommonSense") || !QolWork5.IsCookingBill(__result)) return;
            if (pawn.WorkTypeIsDisabled(WorkTypeDefOf.Cleaning)) return;
            Room room = thing.GetRoom();
            if (room == null || room.TouchesMapEdge || room.IsPrisonCell) return;
            List<Thing> filth = room.ContainedAndAdjacentThings.Where(x => x is Filth && x.Spawned && x.GetRoom() == room && pawn.CanReserve(x)).Take(12).ToList();
            if (filth.Count < 3) return;
            Job clean = JobMaker.MakeJob(JobDefOf.Clean);
            foreach (Thing f in filth) clean.AddQueuedTarget(TargetIndex.A, f);
            __result = clean;
        }
    }

    public class GameComponent_QolResearch : GameComponent
    {
        public GameComponent_QolResearch(Game game)
        {
        }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % 2500 != 321 || !QolFeatures.IsEnabled("Rimverse_Qol_AutoResearch")) return;
            if (Find.ResearchManager.GetProject() != null) return;
            if (!PawnsFinder.AllMaps_FreeColonistsSpawned.Any(p => !p.WorkTypeIsDisabled(WorkTypeDefOf.Research))) return;
            List<ResearchProjectDef> open = DefDatabase<ResearchProjectDef>.AllDefsListForReading.Where(r => r.CanStartNow && !r.IsFinished && !r.IsHidden && r.knowledgeCategory == null).OrderBy(r => r.CostApparent).Take(3).ToList();
            if (open.Count == 0) return;
            ResearchProjectDef pick = open.RandomElement();
            Find.ResearchManager.SetCurrentProject(pick);
            Messages.Message("Rimverse_QolAutoResearch".Translate(pick.LabelCap), MessageTypeDefOf.NeutralEvent, false);
        }
    }

    public class FloatMenuOptionProvider_QolMovePatient : FloatMenuOptionProvider
    {
        protected override bool Drafted => true;

        protected override bool Undrafted => true;

        protected override bool Multiselect => false;

        protected override bool RequiresManipulation => true;

        public override IEnumerable<FloatMenuOption> GetOptionsFor(Pawn clickedPawn, FloatMenuContext context)
        {
            if (!QolFeatures.IsEnabled("Rimverse_Qol_MovePatient")) yield break;
            Pawn carrier = context.FirstSelectedPawn;
            if (carrier == null || clickedPawn == carrier || !clickedPawn.Downed || !clickedPawn.InBed()) yield break;
            if (clickedPawn.Faction != Faction.OfPlayer && !clickedPawn.IsPrisonerOfColony) yield break;
            Building_Bed current = clickedPawn.CurrentBed();
            List<Building_Bed> beds = clickedPawn.Map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>()
                .Where(b => b != current && !b.AnyOccupants && b.Medical == current?.Medical && RestUtility.IsValidBedFor(b, clickedPawn, carrier, false, true))
                .OrderBy(b => b.Position.DistanceToSquared(carrier.Position)).Take(5).ToList();
            foreach (Building_Bed bed in beds)
            {
                Building_Bed target = bed;
                yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("Rimverse_QolMovePatient".Translate(clickedPawn.LabelShort, target.LabelCap, target.Position.ToString()), delegate
                {
                    Job job = JobMaker.MakeJob(JobDefOf.Rescue, clickedPawn, target);
                    job.count = 1;
                    carrier.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                }), carrier, clickedPawn);
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    public static class Patch_QolTreatFirstGizmo
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> values, Pawn __instance)
        {
            foreach (Gizmo g in values) yield return g;
            if (!QolFeatures.IsEnabled("Rimverse_Qol_TreatFirst") || !__instance.Spawned || !(__instance.Faction == Faction.OfPlayer || __instance.IsPrisonerOfColony)) yield break;
            if (!__instance.health.HasHediffsNeedingTend() && !QolWork5.treatFirst.Contains(__instance.thingIDNumber)) yield break;
            Pawn p = __instance;
            yield return new Command_Toggle
            {
                defaultLabel = "Rimverse_QolTreatFirst".Translate(),
                defaultDesc = "Rimverse_QolTreatFirstDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/ForPrisoners", false) ?? TexButton.Plus,
                isActive = () => QolWork5.treatFirst.Contains(p.thingIDNumber),
                toggleAction = () =>
                {
                    if (!QolWork5.treatFirst.Remove(p.thingIDNumber)) QolWork5.treatFirst.Add(p.thingIDNumber);
                }
            };
        }
    }

    [HarmonyPatch(typeof(Building_PlantGrower), nameof(Building_PlantGrower.GetGizmos))]
    public static class Patch_QolLinkedPlanters
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> values, Building_PlantGrower __instance)
        {
            foreach (Gizmo g in values) yield return g;
            if (!QolFeatures.IsEnabled("Rimverse_Qol_LinkedPlanters") || __instance.Faction != Faction.OfPlayer) yield break;
            Building_PlantGrower b = __instance;
            yield return new Command_Action
            {
                defaultLabel = "Rimverse_QolLinkPlanters".Translate(),
                defaultDesc = "Rimverse_QolLinkPlantersDesc".Translate(),
                icon = b.GetPlantDefToGrow()?.uiIcon ?? TexButton.Plus,
                action = () =>
                {
                    int n = 0;
                    foreach (Building_PlantGrower o in Connected(b))
                    {
                        if (o == b) continue;
                        o.SetPlantDefToGrow(b.GetPlantDefToGrow());
                        n++;
                    }
                    Messages.Message("Rimverse_QolLinkPlantersDone".Translate(n), b, MessageTypeDefOf.NeutralEvent, false);
                }
            };
        }

        public static IEnumerable<Building_PlantGrower> Connected(Building_PlantGrower start)
        {
            HashSet<Building_PlantGrower> seen = new HashSet<Building_PlantGrower> { start };
            Queue<Building_PlantGrower> q = new Queue<Building_PlantGrower>();
            q.Enqueue(start);
            while (q.Count > 0 && seen.Count < 200)
            {
                Building_PlantGrower cur = q.Dequeue();
                foreach (IntVec3 c in GenAdj.CellsAdjacentCardinal(cur))
                {
                    if (!c.InBounds(cur.Map)) continue;
                    if (c.GetEdifice(cur.Map) is Building_PlantGrower o && o.def == start.def && seen.Add(o)) q.Enqueue(o);
                }
            }
            return seen;
        }
    }

    [HarmonyPatch(typeof(Building), nameof(Building.GetGizmos))]
    public static class Patch_QolQuickStockpile
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> values, Building __instance)
        {
            foreach (Gizmo g in values) yield return g;
            if (!QolFeatures.IsEnabled("Rimverse_Qol_QuickStockpile") || __instance.Faction != Faction.OfPlayer || !__instance.Spawned) yield break;
            CompShipPart part = __instance.TryGetComp<CompShipPart>();
            if (part == null || part.Role != ShipPartRole.Cargo) yield break;
            Building b = __instance;
            yield return new Command_Action
            {
                defaultLabel = "Rimverse_QolQuickStockpile".Translate(),
                defaultDesc = "Rimverse_QolQuickStockpileDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Designators/ZoneCreate_Stockpile", false) ?? TexButton.Plus,
                action = () => MakeStockpile(b)
            };
        }

        public static void MakeStockpile(Building b)
        {
            Map map = b.Map;
            Room room = b.GetRoom();
            if (room == null || room.TouchesMapEdge || room.CellCount > 900)
            {
                Messages.Message("Rimverse_QolQuickStockpileNoRoom".Translate(), b, MessageTypeDefOf.RejectInput, false);
                return;
            }
            Zone_Stockpile z = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, map.zoneManager);
            map.zoneManager.RegisterZone(z);
            int n = 0;
            foreach (IntVec3 c in room.Cells)
            {
                if (map.zoneManager.ZoneAt(c) != null || !c.Standable(map)) continue;
                z.AddCell(c);
                n++;
            }
            if (n == 0) z.Delete();
            else Messages.Message("Rimverse_QolQuickStockpileDone".Translate(n), b, MessageTypeDefOf.PositiveEvent, false);
        }
    }
}
