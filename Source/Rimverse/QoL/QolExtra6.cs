using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Rimverse
{
    public class TransferableComparer_BestSkill : TransferableComparer
    {
        public static int Best(Transferable t)
        {
            if (!(t.AnyThing is Pawn p) || p.skills == null) return -1;
            int best = 0;
            foreach (SkillRecord s in p.skills.skills) if (!s.TotallyDisabled && s.Level > best) best = s.Level;
            return best;
        }

        public override int Compare(Transferable lhs, Transferable rhs)
        {
            int c = Best(rhs).CompareTo(Best(lhs));
            return c != 0 ? c : string.Compare(lhs.Label, rhs.Label, StringComparison.Ordinal);
        }
    }

    [HarmonyPatch(typeof(Settlement), nameof(Settlement.GetGizmos))]
    public static class Patch_QolSettlementStock
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> gizmos, Settlement __instance)
        {
            foreach (Gizmo g in gizmos) yield return g;
            if (!QolFeatures.IsEnabled("Rimverse_Qol_RemoteStock") || __instance.Faction == null || __instance.Faction.IsPlayer || __instance.trader == null) yield break;
            Settlement s = __instance;
            yield return new Command_Action
            {
                defaultLabel = "Rimverse_QolRemoteStock".Translate(),
                defaultDesc = "Rimverse_QolRemoteStockDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/Trade", false) ?? BaseContent.BadTex,
                action = delegate { Find.WindowStack.Add(new Dialog_MessageBox(QolWork6.StockText(s))); }
            };
        }
    }

    public static class QolWork6
    {
        public static string StockText(Settlement s)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Rimverse_QolRemoteStockTitle".Translate(s.LabelCap, s.Faction.Name, s.Faction.PlayerGoodwill.ToString()));
            sb.AppendLine();
            List<Thing> stock = s.trader.StockListForReading;
            foreach (IGrouping<ThingDef, Thing> g in stock.GroupBy(t => t.def).OrderByDescending(g => g.Sum(t => t.MarketValue * t.stackCount)).Take(40))
            {
                int count = g.Sum(t => t.stackCount);
                sb.AppendLine("- " + g.Key.LabelCap + " x" + count + " (" + g.First().MarketValue.ToStringMoney() + ")");
            }
            if (stock.Count == 0) sb.AppendLine("Rimverse_QolRemoteStockEmpty".Translate());
            return sb.ToString();
        }

        public static void ExportSettings()
        {
            string path = Path.Combine(GenFilePaths.ConfigFolderPath, "Rimverse_ExportedSettings.xml");
            RimverseSettings settings = RimverseMod.Settings;
            Scribe.saver.InitSaving(path, "SettingsBlock");
            try
            {
                Scribe_Deep.Look(ref settings, "ModSettings");
            }
            finally
            {
                Scribe.saver.FinalizeSaving();
            }
            Messages.Message("Rimverse_QolSettingsExported".Translate(path), MessageTypeDefOf.NeutralEvent, false);
        }

        public static void ImportSettings()
        {
            string path = Path.Combine(GenFilePaths.ConfigFolderPath, "Rimverse_ExportedSettings.xml");
            if (!File.Exists(path))
            {
                Messages.Message("Rimverse_QolSettingsNoFile".Translate(path), MessageTypeDefOf.RejectInput, false);
                return;
            }
            RimverseSettings loaded = null;
            Scribe.loader.InitLoading(path);
            try
            {
                Scribe_Deep.Look(ref loaded, "ModSettings");
                Scribe.loader.FinalizeLoading();
            }
            catch (Exception e)
            {
                Scribe.ForceStop();
                RimverseLog.Warning("Could not import settings: " + e.Message);
                return;
            }
            if (loaded == null) return;
            foreach (FieldInfo f in typeof(RimverseSettings).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                f.SetValue(RimverseMod.Settings, f.GetValue(loaded));
            }
            RimverseMod.Instance.WriteSettings();
            Messages.Message("Rimverse_QolSettingsImported".Translate(), MessageTypeDefOf.PositiveEvent, false);
        }

        public static void QuickSave()
        {
            if (Current.ProgramState != ProgramState.Playing || Find.World == null) return;
            string colony = Faction.OfPlayer?.Name ?? "Rimverse";
            string name = GenText.SanitizeFilename(colony + " - " + GenDate.DateFullStringAt(Find.TickManager.TicksAbs, Find.WorldGrid.LongLatOf(Find.CurrentMap?.Tile ?? 0)).Replace(",", ""));
            LongEventHandler.QueueLongEvent(delegate { GameDataSaveLoader.SaveGame(name); }, "SavingLongEvent", false, null);
            Messages.Message("Rimverse_QolQuickSaved".Translate(name), MessageTypeDefOf.SilentInput, false);
        }
    }

    [HarmonyPatch(typeof(Page_SelectScenario), nameof(Page_SelectScenario.DoWindowContents))]
    public static class Patch_QolRandomScenario
    {
        public static void Postfix(Page_SelectScenario __instance, Rect rect)
        {
            if (!QolFeatures.IsEnabled("Rimverse_Qol_RandomScenario")) return;
            Rect b = new Rect(rect.xMax - 200f, rect.y, 200f, 32f);
            if (Widgets.ButtonText(b, "Rimverse_QolRandomScenario".Translate()))
            {
                Scenario pick = ScenarioLister.AllScenarios().Where(x => x.showInUI).RandomElementWithFallback();
                if (pick != null) Traverse.Create(__instance).Field("curScen").SetValue(pick);
            }
        }
    }

    [HarmonyPatch(typeof(Projectile), "CanHit")]
    public static class Patch_QolFriendlyFire
    {
        public static void Postfix(Projectile __instance, Thing thing, Thing ___launcher, LocalTargetInfo ___intendedTarget, ref bool __result)
        {
            if (!__result || !(thing is Pawn p) || ___launcher == null || p.Faction == null || p.Faction != ___launcher.Faction) return;
            if (___intendedTarget.Thing == thing || !QolFeatures.IsEnabled("Rimverse_Qol_FairFriendlyFire")) return;
            float chance = 0.7f;
            if (___launcher is Pawn shooter && shooter.skills != null)
            {
                chance = Mathf.Clamp(0.5f + shooter.skills.GetSkill(SkillDefOf.Shooting).Level * 0.025f, 0.5f, 0.95f);
            }
            if (Rand.ChanceSeeded(chance, Gen.HashCombineInt(__instance.thingIDNumber, thing.thingIDNumber))) __result = false;
        }
    }

    public class MapComponent_NoTurretFire : MapComponent
    {
        public HashSet<IntVec3> cells = new HashSet<IntVec3>();

        public MapComponent_NoTurretFire(Map map) : base(map)
        {
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref cells, "rimverseNoTurretFire", LookMode.Value);
            if (cells == null) cells = new HashSet<IntVec3>();
        }

        public static bool Blocked(Map map, IntVec3 c)
        {
            if (map == null) return false;
            MapComponent_NoTurretFire comp = map.GetComponent<MapComponent_NoTurretFire>();
            return comp != null && comp.cells.Contains(c);
        }
    }

    public class Designator_RimverseNoTurretFire : Designator_Cells
    {
        private readonly bool add;

        public override DrawStyleCategoryDef DrawStyleCategory => DrawStyleCategoryDefOf.Areas;

        public override bool DragDrawMeasurements => true;

        public override bool Visible => QolFeatures.IsEnabled("Rimverse_Qol_TurretExclusion");

        public Designator_RimverseNoTurretFire() : this(true)
        {
        }

        public Designator_RimverseNoTurretFire(bool add)
        {
            this.add = add;
            defaultLabel = (add ? "Rimverse_DesNoTurretFire" : "Rimverse_DesNoTurretFireClear").Translate();
            defaultDesc = "Rimverse_DesNoTurretFireDesc".Translate();
            icon = ContentFinder<Texture2D>.Get(add ? "UI/Designators/HomeAreaOn" : "UI/Designators/HomeAreaOff", false);
            useMouseIcon = true;
            soundDragSustain = SoundDefOf.Designate_DragAreaAdd;
            soundDragChanged = SoundDefOf.Designate_DragZone_Changed;
            soundSucceeded = SoundDefOf.Designate_ZoneAdd;
        }

        private MapComponent_NoTurretFire Comp => Map.GetComponent<MapComponent_NoTurretFire>();

        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            if (!c.InBounds(Map) || Comp == null) return false;
            return add != Comp.cells.Contains(c);
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            if (add) Comp.cells.Add(c);
            else Comp.cells.Remove(c);
        }

        public override void SelectedUpdate()
        {
            GenUI.RenderMouseoverBracket();
            if (Comp != null && Comp.cells.Count > 0) GenDraw.DrawFieldEdges(Comp.cells.ToList(), new Color(1f, 0.35f, 0.2f));
        }
    }

    public class Designator_RimverseNoTurretFireClear : Designator_RimverseNoTurretFire
    {
        public Designator_RimverseNoTurretFireClear() : base(false)
        {
        }
    }

    [HarmonyPatch(typeof(Building_TurretGun), "IsValidTarget")]
    public static class Patch_QolTurretExclusion
    {
        public static void Postfix(Building_TurretGun __instance, Thing t, ref bool __result)
        {
            if (!__result || __instance.Faction != Faction.OfPlayer || !QolFeatures.IsEnabled("Rimverse_Qol_TurretExclusion")) return;
            if (MapComponent_NoTurretFire.Blocked(__instance.Map, t.Position)) __result = false;
        }
    }

    public class GameComponent_QolExtra6 : GameComponent
    {
        private HashSet<int> swapped = new HashSet<int>();
        private Dictionary<int, string> customNames = new Dictionary<int, string>();

        public static GameComponent_QolExtra6 Instance => Current.Game?.GetComponent<GameComponent_QolExtra6>();

        public GameComponent_QolExtra6(Game game)
        {
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref swapped, "rimverseSwapped", LookMode.Value);
            Scribe_Collections.Look(ref customNames, "rimverseCustomNames", LookMode.Value, LookMode.Value);
            if (swapped == null) swapped = new HashSet<int>();
            if (customNames == null) customNames = new Dictionary<int, string>();
        }

        public string NameOf(Thing t)
        {
            return customNames.TryGetValue(t.thingIDNumber, out string n) ? n : null;
        }

        public void SetName(Thing t, string name)
        {
            if (name.NullOrEmpty()) customNames.Remove(t.thingIDNumber);
            else customNames[t.thingIDNumber] = name;
        }

        public override void GameComponentOnGUI()
        {
            if (Event.current.type == EventType.KeyDown && RimverseDefOf6.Rimverse_QuickSave != null && RimverseDefOf6.Rimverse_QuickSave.KeyDownEvent && QolFeatures.IsEnabled("Rimverse_Qol_QuickSave"))
            {
                Event.current.Use();
                QolWork6.QuickSave();
            }
        }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % 30 != 7 || !QolFeatures.IsEnabled("Rimverse_Qol_Sidearms")) return;
            foreach (Pawn p in PawnsFinder.AllMaps_FreeColonistsSpawned)
            {
                if (p.Downed || p.equipment == null || p.inventory == null || p.InMentalState) continue;
                ThingWithComps primary = p.equipment.Primary;
                bool enemyAdjacent = GenAdj.CellsAdjacent8Way(p).Any(c => c.InBounds(p.Map) && c.GetFirstPawn(p.Map) is Pawn e && e.HostileTo(p) && !e.Downed);
                if (enemyAdjacent && (primary == null || primary.def.IsRangedWeapon))
                {
                    ThingWithComps melee = p.inventory.innerContainer.OfType<ThingWithComps>().Where(t => t.def.IsMeleeWeapon).OrderByDescending(t => t.GetStatValue(StatDefOf.MeleeWeapon_AverageDPS)).FirstOrDefault();
                    if (melee != null && Swap(p, primary, melee)) swapped.Add(p.thingIDNumber);
                }
                else if (!enemyAdjacent && swapped.Contains(p.thingIDNumber) && primary != null && primary.def.IsMeleeWeapon)
                {
                    bool near = p.Map.attackTargetsCache.GetPotentialTargetsFor(p).Any(t => t.Thing.Position.DistanceTo(p.Position) < 4.5f);
                    if (near) continue;
                    ThingWithComps ranged = p.inventory.innerContainer.OfType<ThingWithComps>().FirstOrDefault(t => t.def.IsRangedWeapon);
                    if (ranged != null) Swap(p, primary, ranged);
                    swapped.Remove(p.thingIDNumber);
                }
            }
        }

        private static bool Swap(Pawn p, ThingWithComps current, ThingWithComps next)
        {
            if (current != null && !p.equipment.TryTransferEquipmentToContainer(current, p.inventory.innerContainer)) return false;
            if (!p.inventory.innerContainer.Remove(next)) return false;
            p.equipment.AddEquipment(next);
            if (p.CurJob != null && p.CurJob.def == JobDefOf.AttackStatic) p.jobs.EndCurrentJob(JobCondition.InterruptForced);
            return true;
        }
    }

    [DefOf]
    public static class RimverseDefOf6
    {
        public static KeyBindingDef Rimverse_QuickSave;

        static RimverseDefOf6()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(RimverseDefOf6));
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.LabelNoCount), MethodType.Getter)]
    public static class Patch_QolCustomName
    {
        public static void Postfix(Thing __instance, ref string __result)
        {
            if (!(__instance is Building) || Current.ProgramState != ProgramState.Playing) return;
            string n = GameComponent_QolExtra6.Instance?.NameOf(__instance);
            if (n != null) __result = n;
        }
    }

    [HarmonyPatch(typeof(Building), nameof(Building.GetGizmos))]
    public static class Patch_QolRenameBuilding
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> gizmos, Building __instance)
        {
            foreach (Gizmo g in gizmos) yield return g;
            if (__instance.Faction != Faction.OfPlayer || !QolFeatures.IsEnabled("Rimverse_Qol_Rename")) yield break;
            Building b = __instance;
            yield return new Command_Action
            {
                defaultLabel = "Rimverse_QolRename".Translate(),
                defaultDesc = "Rimverse_QolRenameDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Buttons/Rename", false) ?? BaseContent.BadTex,
                action = delegate { Find.WindowStack.Add(new Dialog_RimverseRename(b)); }
            };
        }
    }

    public class Dialog_RimverseRename : Window
    {
        private readonly Thing thing;
        private string text;

        public override Vector2 InitialSize => new Vector2(400f, 170f);

        public Dialog_RimverseRename(Thing thing)
        {
            this.thing = thing;
            text = GameComponent_QolExtra6.Instance?.NameOf(thing) ?? thing.def.label;
            forcePause = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Widgets.Label(new Rect(0f, 0f, inRect.width, 30f), "Rimverse_QolRename".Translate());
            text = Widgets.TextField(new Rect(0f, 36f, inRect.width, 32f), text);
            if (Widgets.ButtonText(new Rect(0f, inRect.height - 35f, inRect.width / 2f - 5f, 35f), "Rimverse_QolRenameReset".Translate()))
            {
                GameComponent_QolExtra6.Instance?.SetName(thing, null);
                Close();
            }
            if (Widgets.ButtonText(new Rect(inRect.width / 2f + 5f, inRect.height - 35f, inRect.width / 2f - 5f, 35f), "OK".Translate()) || (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return))
            {
                GameComponent_QolExtra6.Instance?.SetName(thing, text.NullOrEmpty() ? null : text.Trim());
                Close();
            }
        }
    }
}
