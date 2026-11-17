using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Rimverse
{
    public class WorkSnapshot
    {
        public Dictionary<WorkTypeDef, int> priorities = new Dictionary<WorkTypeDef, int>();
        public List<TimeAssignmentDef> times;
        public Area area;

        public static WorkSnapshot Of(Pawn p)
        {
            WorkSnapshot s = new WorkSnapshot();
            if (p.workSettings != null && p.workSettings.EverWork)
            {
                foreach (WorkTypeDef w in DefDatabase<WorkTypeDef>.AllDefsListForReading) s.priorities[w] = p.workSettings.GetPriority(w);
            }
            if (p.timetable != null) s.times = new List<TimeAssignmentDef>(p.timetable.times);
            s.area = p.playerSettings?.AreaRestrictionInPawnCurrentMap;
            return s;
        }

        public void ApplyTo(Pawn p)
        {
            if (p.workSettings != null && p.workSettings.EverWork)
            {
                foreach (KeyValuePair<WorkTypeDef, int> kv in priorities)
                {
                    if (!p.WorkTypeIsDisabled(kv.Key)) p.workSettings.SetPriority(kv.Key, kv.Value);
                }
            }
            if (times != null && p.timetable != null) p.timetable.times = new List<TimeAssignmentDef>(times);
            if (p.playerSettings != null && p.playerSettings.SupportsAllowedAreas) p.playerSettings.AreaRestrictionInPawnCurrentMap = area;
        }
    }

    public static class QolWork
    {
        public static WorkSnapshot clipboard;
        public static readonly Dictionary<int, WorkSnapshot> beforeMode = new Dictionary<int, WorkSnapshot>();
        public static List<Bill> billClipboard;
        private static readonly string[] Emergency = { "Firefighter", "Patient", "Doctor", "PatientBedRest", "BasicWorker" };

        public static void SetMode(Pawn p, string mode)
        {
            if (p.workSettings == null || !p.workSettings.EverWork) return;
            if (mode == "Restore")
            {
                if (beforeMode.TryGetValue(p.thingIDNumber, out WorkSnapshot s)) s.ApplyTo(p);
                beforeMode.Remove(p.thingIDNumber);
                return;
            }
            if (!beforeMode.ContainsKey(p.thingIDNumber)) beforeMode[p.thingIDNumber] = WorkSnapshot.Of(p);
            foreach (WorkTypeDef w in DefDatabase<WorkTypeDef>.AllDefsListForReading)
            {
                if (p.WorkTypeIsDisabled(w)) continue;
                bool keep;
                switch (mode)
                {
                    case "Emergency": keep = Emergency.Contains(w.defName); break;
                    case "Repair": keep = Emergency.Contains(w.defName) || w.defName == "Construction" || w.defName == "Hauling"; break;
                    case "Haul": keep = Emergency.Contains(w.defName) || w.defName == "Hauling" || w.defName == "Cleaning"; break;
                    default: keep = true; break;
                }
                p.workSettings.SetPriority(w, keep ? Mathf.Max(1, p.workSettings.GetPriority(w) == 0 ? 2 : p.workSettings.GetPriority(w)) : 0);
            }
        }

        public static int ZoneKey(Zone z) => -(z.ID + 1);
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    public static class Patch_PawnQolGizmos
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            foreach (Gizmo g in __result) yield return g;
            Pawn p = __instance;
            if (p == null || !p.IsColonistPlayerControlled || p.Faction != Faction.OfPlayer) yield break;
            if (QolFeatures.IsEnabled("Rimverse_Qol_WorkProfiles") && !p.Drafted)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Rimverse_CmdCopyWork".Translate(),
                    defaultDesc = "Rimverse_CmdCopyWorkDesc".Translate(),
                    icon = RimverseTex.Copy,
                    action = () => QolWork.clipboard = WorkSnapshot.Of(p)
                };
                Command_Action paste = new Command_Action
                {
                    defaultLabel = "Rimverse_CmdPasteWork".Translate(),
                    defaultDesc = "Rimverse_CmdPasteWorkDesc".Translate(),
                    icon = RimverseTex.Paste,
                    action = () => QolWork.clipboard?.ApplyTo(p)
                };
                if (QolWork.clipboard == null) paste.Disable("Rimverse_NothingCopied".Translate());
                yield return paste;
            }
            if (QolFeatures.IsEnabled("Rimverse_Qol_WorkModes") && !p.Drafted)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Rimverse_CmdWorkMode".Translate(),
                    defaultDesc = "Rimverse_CmdWorkModeDesc".Translate(),
                    icon = RimverseTex.ShipPanel,
                    action = () =>
                    {
                        List<Pawn> sel = Find.Selector.SelectedPawns.Where(x => x.IsColonistPlayerControlled).ToList();
                        List<FloatMenuOption> opts = new List<FloatMenuOption>();
                        foreach (string m in new[] { "Emergency", "Repair", "Haul", "Restore" })
                        {
                            string mode = m;
                            opts.Add(new FloatMenuOption(("Rimverse_WorkMode_" + m).Translate(), () => { foreach (Pawn x in sel) QolWork.SetMode(x, mode); }));
                        }
                        Find.WindowStack.Add(new FloatMenu(opts));
                    }
                };
            }
            if (!p.Drafted) yield break;
            GameComponent_RimverseQolExtra ex = GameComponent_RimverseQolExtra.Instance;
            if (ex == null) yield break;
            if (QolFeatures.IsEnabled("Rimverse_Qol_SearchDestroy"))
            {
                bool on = ex.hunters.Contains(p.thingIDNumber);
                yield return new Command_Toggle
                {
                    defaultLabel = "Rimverse_CmdHunt".Translate(),
                    defaultDesc = "Rimverse_CmdHuntDesc".Translate(),
                    icon = TexCommand.Attack,
                    isActive = () => ex.hunters.Contains(p.thingIDNumber),
                    toggleAction = () =>
                    {
                        if (!ex.hunters.Remove(p.thingIDNumber)) ex.hunters.Add(p.thingIDNumber);
                    }
                };
            }
            if (QolFeatures.IsEnabled("Rimverse_Qol_DefensivePositions"))
            {
                yield return new Command_Action
                {
                    defaultLabel = "Rimverse_CmdSavePos".Translate(),
                    defaultDesc = "Rimverse_CmdSavePosDesc".Translate(),
                    icon = RimverseTex.Copy,
                    action = () => ex.positions[p.thingIDNumber] = p.Position
                };
                Command_Action go = new Command_Action
                {
                    defaultLabel = "Rimverse_CmdGoPos".Translate(),
                    defaultDesc = "Rimverse_CmdGoPosDesc".Translate(),
                    icon = TexCommand.SquadAttack,
                    action = () =>
                    {
                        if (!ex.positions.TryGetValue(p.thingIDNumber, out IntVec3 c) || !c.InBounds(p.Map)) return;
                        p.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Goto, c), JobTag.DraftedOrder);
                    }
                };
                if (!ex.positions.ContainsKey(p.thingIDNumber)) go.Disable("Rimverse_NoSavedPos".Translate());
                yield return go;
            }
        }
    }

    [HarmonyPatch(typeof(Building), nameof(Building.GetGizmos))]
    public static class Patch_BuildingQolGizmos
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Building __instance)
        {
            foreach (Gizmo g in __result) yield return g;
            Building b = __instance;
            if (b == null || b.Faction != Faction.OfPlayer) yield break;
            GameComponent_RimverseQolExtra ex = GameComponent_RimverseQolExtra.Instance;
            if (b is Building_WorkTable wt && QolFeatures.IsEnabled("Rimverse_Qol_BillCopy"))
            {
                yield return new Command_Action
                {
                    defaultLabel = "Rimverse_CmdCopyBills".Translate(),
                    defaultDesc = "Rimverse_CmdCopyBillsDesc".Translate(),
                    icon = RimverseTex.Copy,
                    action = () => QolWork.billClipboard = wt.BillStack.Bills.Select(x => x.Clone()).ToList()
                };
                Command_Action paste = new Command_Action
                {
                    defaultLabel = "Rimverse_CmdPasteBills".Translate(),
                    defaultDesc = "Rimverse_CmdPasteBillsDesc".Translate(),
                    icon = RimverseTex.Paste,
                    action = () =>
                    {
                        foreach (Bill bill in QolWork.billClipboard)
                        {
                            if (!wt.def.AllRecipes.Contains(bill.recipe) || wt.BillStack.Count >= BillStack.MaxCount) continue;
                            Bill c = bill.Clone();
                            c.InitializeAfterClone();
                            wt.BillStack.AddBill(c);
                        }
                    }
                };
                if (QolWork.billClipboard == null || QolWork.billClipboard.Count == 0) paste.Disable("Rimverse_NothingCopied".Translate());
                yield return paste;
            }
            if (b is Building_Bed bed && QolFeatures.IsEnabled("Rimverse_Qol_SelectBedOwner") && bed.OwnersForReading.Count > 0)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Rimverse_CmdSelectOwner".Translate(),
                    defaultDesc = "Rimverse_CmdSelectOwnerDesc".Translate(),
                    icon = TexCommand.SelectShelf,
                    action = () =>
                    {
                        Find.Selector.ClearSelection();
                        foreach (Pawn o in bed.OwnersForReading)
                        {
                            if (o.Spawned) Find.Selector.Select(o);
                        }
                    }
                };
            }
            if (b is Building_Storage st && ex != null && QolFeatures.IsEnabled("Rimverse_Qol_FlickStorage"))
            {
                yield return StorageToggle(st.settings, b.thingIDNumber, ex);
            }
            if (b is Building_PlantGrower && ex != null && QolFeatures.IsEnabled("Rimverse_Qol_ToggleHarvest"))
            {
                yield return HarvestToggle(b.thingIDNumber, ex);
            }
        }

        public static Command_Toggle StorageToggle(StorageSettings settings, int key, GameComponent_RimverseQolExtra ex)
        {
            return new Command_Toggle
            {
                defaultLabel = "Rimverse_CmdStorageOn".Translate(),
                defaultDesc = "Rimverse_CmdStorageOnDesc".Translate(),
                icon = TexCommand.ForbidOff,
                isActive = () => !ex.flicked.ContainsKey(key),
                toggleAction = () =>
                {
                    if (ex.flicked.TryGetValue(key, out StoragePriority prev))
                    {
                        settings.Priority = prev;
                        ex.flicked.Remove(key);
                    }
                    else
                    {
                        ex.flicked[key] = settings.Priority;
                        settings.Priority = StoragePriority.Unstored;
                    }
                }
            };
        }

        public static Command_Toggle HarvestToggle(int key, GameComponent_RimverseQolExtra ex)
        {
            return new Command_Toggle
            {
                defaultLabel = "Rimverse_CmdHarvestOn".Translate(),
                defaultDesc = "Rimverse_CmdHarvestOnDesc".Translate(),
                icon = TexCommand.ForbidOff,
                isActive = () => !ex.pausedHarvest.Contains(key),
                toggleAction = () =>
                {
                    if (!ex.pausedHarvest.Remove(key)) ex.pausedHarvest.Add(key);
                }
            };
        }
    }

    [HarmonyPatch(typeof(Zone), nameof(Zone.GetGizmos))]
    public static class Patch_ZoneQolGizmos
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Zone __instance)
        {
            foreach (Gizmo g in __result) yield return g;
            GameComponent_RimverseQolExtra ex = GameComponent_RimverseQolExtra.Instance;
            if (ex == null) yield break;
            if (__instance is Zone_Stockpile sp && QolFeatures.IsEnabled("Rimverse_Qol_FlickStorage")) yield return Patch_BuildingQolGizmos.StorageToggle(sp.settings, QolWork.ZoneKey(sp), ex);
            if (__instance is Zone_Growing && QolFeatures.IsEnabled("Rimverse_Qol_ToggleHarvest")) yield return Patch_BuildingQolGizmos.HarvestToggle(QolWork.ZoneKey(__instance), ex);
        }
    }

    [HarmonyPatch(typeof(WorkGiver_GrowerHarvest), nameof(WorkGiver_GrowerHarvest.HasJobOnCell))]
    public static class Patch_HarvestPaused
    {
        public static void Postfix(Pawn pawn, IntVec3 c, ref bool __result)
        {
            if (!__result) return;
            GameComponent_RimverseQolExtra ex = GameComponent_RimverseQolExtra.Instance;
            if (ex == null || ex.pausedHarvest.Count == 0 || pawn?.Map == null) return;
            Zone z = c.GetZone(pawn.Map);
            if (z != null && ex.pausedHarvest.Contains(QolWork.ZoneKey(z))) { __result = false; return; }
            Building_PlantGrower g = c.GetFirstThing<Building_PlantGrower>(pawn.Map);
            if (g != null && ex.pausedHarvest.Contains(g.thingIDNumber)) __result = false;
        }
    }

    [HarmonyPatch(typeof(LetterStack), nameof(LetterStack.ReceiveLetter), new[] { typeof(Letter), typeof(string), typeof(int), typeof(bool) })]
    public static class Patch_AutoPause
    {
        public static void Postfix(Letter let, int delayTicks)
        {
            if (let == null || delayTicks > 0 || Find.TickManager == null || Find.TickManager.Paused) return;
            if (let.def != LetterDefOf.ThreatBig && let.def != LetterDefOf.ThreatSmall) return;
            if (!QolFeatures.IsEnabled("Rimverse_Qol_AutoPause")) return;
            Find.TickManager.Pause();
        }
    }

    [HarmonyPatch(typeof(DropCellFinder), nameof(DropCellFinder.TradeDropSpot))]
    public static class Patch_TradeDropSpot
    {
        public static void Postfix(Map map, ref IntVec3 __result)
        {
            if (map == null || !QolFeatures.IsEnabled("Rimverse_Qol_TradeDropSpot")) return;
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail("Rimverse_TradeDropSpot");
            if (def == null) return;
            Thing spot = map.listerThings.ThingsOfDef(def).FirstOrDefault();
            if (spot != null) __result = spot.Position;
        }
    }

    [HarmonyPatch(typeof(HealthAIUtility), nameof(HealthAIUtility.FindBestMedicine))]
    public static class Patch_SmartMedicine
    {
        public static void Postfix(Pawn healer, Pawn patient, bool onlyUseInventory, ref Thing __result)
        {
            if (__result == null || onlyUseInventory || healer?.Map == null || patient == null) return;
            if (!QolFeatures.IsEnabled("Rimverse_Qol_SmartMedicine")) return;
            float potency = __result.def.GetStatValueAbstract(StatDefOf.MedicalPotency);
            if (potency <= 0.65f || !Minor(patient)) return;
            Thing cheaper = GenClosest.ClosestThingReachable(patient.PositionHeld, healer.Map, ThingRequest.ForGroup(ThingRequestGroup.Medicine), PathEndMode.ClosestTouch, TraverseParms.For(healer), 9999f, t =>
            {
                float pot = t.def.GetStatValueAbstract(StatDefOf.MedicalPotency);
                return pot < potency && pot > 0f && !t.IsForbidden(healer) && healer.CanReserve(t) && patient.playerSettings != null && t.def.GetStatValueAbstract(StatDefOf.MedicalPotency) <= potency;
            });
            if (cheaper != null) __result = cheaper;
        }

        private static bool Minor(Pawn p)
        {
            if (p.health.hediffSet.BleedRateTotal > 0.15f) return false;
            foreach (Hediff h in p.health.hediffSet.hediffs)
            {
                if (!h.TendableNow()) continue;
                if (h.def.lethalSeverity > 0f || h is Hediff_MissingPart) return false;
                if (h is Hediff_Injury inj && inj.Severity > 8f) return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Pawn_EquipmentTracker), nameof(Pawn_EquipmentTracker.TryDropEquipment))]
    public static class Patch_RememberDroppedWeapon
    {
        public static void Postfix(Pawn_EquipmentTracker __instance, ThingWithComps resultingEq, bool __result)
        {
            Pawn p = __instance.pawn;
            if (!__result || resultingEq == null || p == null || !p.Downed || p.Faction != Faction.OfPlayer) return;
            GameComponent_RimverseQolExtra ex = GameComponent_RimverseQolExtra.Instance;
            if (ex != null) ex.lostWeapons[p.thingIDNumber] = resultingEq.thingIDNumber;
        }
    }

    public class MapComponent_RimverseQolTick : MapComponent
    {
        public MapComponent_RimverseQolTick(Map map) : base(map)
        {
        }

        public override void MapComponentTick()
        {
            if (Find.TickManager.TicksGame % 120 != 0) return;
            GameComponent_RimverseQolExtra ex = GameComponent_RimverseQolExtra.Instance;
            if (ex == null) return;
            if (ex.hunters.Count > 0 && QolFeatures.IsEnabled("Rimverse_Qol_SearchDestroy")) Hunt(ex);
            if (ex.lostWeapons.Count > 0 && QolFeatures.IsEnabled("Rimverse_Qol_PickupWeapon")) Pickup(ex);
        }

        private void Hunt(GameComponent_RimverseQolExtra ex)
        {
            foreach (Pawn p in map.mapPawns.FreeColonistsSpawned)
            {
                if (!ex.hunters.Contains(p.thingIDNumber)) continue;
                if (!p.Drafted || p.Downed) { ex.hunters.Remove(p.thingIDNumber); break; }
                if (p.CurJobDef != JobDefOf.Wait_Combat && p.CurJobDef != JobDefOf.Wait && p.CurJob != null) continue;
                Pawn target = map.mapPawns.AllPawnsSpawned.Where(t => !t.Dead && !t.Downed && t.HostileTo(p) && !t.Position.Fogged(map) && p.CanReach(t, PathEndMode.Touch, Danger.Deadly)).OrderBy(t => t.Position.DistanceToSquared(p.Position)).FirstOrDefault();
                if (target == null) continue;
                bool melee = p.equipment?.Primary == null || p.equipment.PrimaryEq.PrimaryVerb.verbProps.IsMeleeAttack;
                Job job = JobMaker.MakeJob(melee ? JobDefOf.AttackMelee : JobDefOf.AttackStatic, target);
                p.jobs.TryTakeOrderedJob(job, JobTag.DraftedOrder);
            }
        }

        private void Pickup(GameComponent_RimverseQolExtra ex)
        {
            foreach (Pawn p in map.mapPawns.FreeColonistsSpawned)
            {
                if (!ex.lostWeapons.TryGetValue(p.thingIDNumber, out int id)) continue;
                if (p.Downed || p.InMentalState) continue;
                if (p.equipment?.Primary != null) { ex.lostWeapons.Remove(p.thingIDNumber); break; }
                Thing w = map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon).FirstOrDefault(t => t.thingIDNumber == id);
                ex.lostWeapons.Remove(p.thingIDNumber);
                if (w == null || !w.Spawned || w.Position.DistanceTo(p.Position) > 25f || !p.CanReserveAndReach(w, PathEndMode.ClosestTouch, Danger.Some)) break;
                Job job = JobMaker.MakeJob(JobDefOf.Equip, w);
                p.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                break;
            }
        }
    }

    public class Designator_RimverseHaulArea : Designator
    {
        public override DrawStyleCategoryDef DrawStyleCategory => DrawStyleCategoryDefOf.FilledRectangle;

        public override bool Visible => QolFeatures.IsEnabled("Rimverse_Qol_HaulArea");

        public Designator_RimverseHaulArea()
        {
            defaultLabel = "Rimverse_DesHaulArea".Translate();
            defaultDesc = "Rimverse_DesHaulAreaDesc".Translate();
            icon = ContentFinder<Texture2D>.Get("UI/Designators/Haul", false);
            useMouseIcon = true;
            soundSucceeded = SoundDefOf.Designate_Haul;
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            if (!c.InBounds(Map) || c.Fogged(Map)) return false;
            return c.GetThingList(Map).Any(t => CanDesignateThing(t).Accepted);
        }

        public override AcceptanceReport CanDesignateThing(Thing t)
        {
            return t.def.EverHaulable && t.def.category == ThingCategory.Item && Map.designationManager.DesignationOn(t, DesignationDefOf.Haul) == null && !t.IsInValidStorage();
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            foreach (Thing t in c.GetThingList(Map).ToList()) if (CanDesignateThing(t).Accepted) DesignateThing(t);
        }

        public override void DesignateThing(Thing t)
        {
            t.SetForbidden(false, false);
            Map.designationManager.AddDesignation(new Designation(t, DesignationDefOf.Haul));
        }
    }

    public class BlueprintEntry
    {
        public ThingDef def;
        public ThingDef stuff;
        public IntVec3 offset;
        public Rot4 rot;
    }

    public static class QolBlueprints
    {
        public static List<BlueprintEntry> copied = new List<BlueprintEntry>();
    }

    public class Designator_RimverseCopyBuildings : Designator
    {
        public override DrawStyleCategoryDef DrawStyleCategory => DrawStyleCategoryDefOf.FilledRectangle;

        public override bool Visible => QolFeatures.IsEnabled("Rimverse_Qol_Blueprints");

        public Designator_RimverseCopyBuildings()
        {
            defaultLabel = "Rimverse_DesCopy".Translate();
            defaultDesc = "Rimverse_DesCopyDesc".Translate();
            icon = RimverseTex.Copy;
            useMouseIcon = true;
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 c) => c.InBounds(Map);

        public override void DesignateMultiCell(IEnumerable<IntVec3> cells)
        {
            List<IntVec3> list = cells.ToList();
            if (list.Count == 0) return;
            IntVec3 min = new IntVec3(list.Min(c => c.x), 0, list.Min(c => c.z));
            HashSet<Thing> seen = new HashSet<Thing>();
            QolBlueprints.copied.Clear();
            foreach (IntVec3 c in list)
            {
                foreach (Thing t in c.GetThingList(Map))
                {
                    if (!(t is Building) || t.Faction != Faction.OfPlayer || !t.def.BuildableByPlayer || !seen.Add(t)) continue;
                    QolBlueprints.copied.Add(new BlueprintEntry { def = t.def, stuff = t.Stuff, offset = t.Position - min, rot = t.Rotation });
                }
            }
            Messages.Message("Rimverse_MsgCopied".Translate(QolBlueprints.copied.Count), MessageTypeDefOf.TaskCompletion, false);
        }
    }

    public class Designator_RimversePasteBuildings : Designator
    {
        public override bool Visible => QolFeatures.IsEnabled("Rimverse_Qol_Blueprints");

        public Designator_RimversePasteBuildings()
        {
            defaultLabel = "Rimverse_DesPaste".Translate();
            defaultDesc = "Rimverse_DesPasteDesc".Translate();
            icon = RimverseTex.Paste;
            useMouseIcon = true;
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            if (QolBlueprints.copied.Count == 0) return "Rimverse_NothingCopied".Translate();
            return c.InBounds(Map);
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            int placed = 0;
            foreach (BlueprintEntry e in QolBlueprints.copied)
            {
                IntVec3 at = c + e.offset;
                if (!at.InBounds(Map)) continue;
                if (!GenConstruct.CanPlaceBlueprintAt(e.def, at, e.rot, Map, false, null, null, e.stuff).Accepted) continue;
                GenConstruct.PlaceBlueprintForBuild(e.def, at, Map, e.rot, Faction.OfPlayer, e.stuff);
                placed++;
            }
            Messages.Message("Rimverse_MsgPasted".Translate(placed, QolBlueprints.copied.Count), MessageTypeDefOf.TaskCompletion, false);
        }

        public override void SelectedUpdate()
        {
            IntVec3 m = UI.MouseCell();
            foreach (BlueprintEntry e in QolBlueprints.copied)
            {
                IntVec3 at = m + e.offset;
                if (at.InBounds(Find.CurrentMap)) GenDraw.DrawFieldEdges(new List<IntVec3> { at });
            }
        }
    }

    [HarmonyPatch(typeof(LordJob_AssaultColony), nameof(LordJob_AssaultColony.CreateGraph))]
    public static class Patch_NeverFlee
    {
        public static void Prefix(Faction ___assaulterFaction, ref bool ___canTimeoutOrFlee)
        {
            if (___assaulterFaction != null && ___assaulterFaction.def.permanentEnemy && QolFeatures.IsEnabled("Rimverse_Qol_NeverFlee")) ___canTimeoutOrFlee = false;
        }
    }
}
