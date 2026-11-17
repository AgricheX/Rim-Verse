using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public class GameComponent_RimverseHud : GameComponent
    {
        private struct HudRow
        {
            public string label;
            public string value;
            public float fill;
            public int level;
            public string tip;
        }

        private const float Width = 228f;
        private const float RowHeight = 20f;
        private const float Pad = 8f;
        private static readonly Color Back = new Color(0.08f, 0.09f, 0.1f, 0.78f);
        private static readonly Color Edge = new Color(1f, 1f, 1f, 0.07f);
        private static readonly Color Muted = new Color(0.62f, 0.64f, 0.67f);
        private static readonly Color Accent = new Color(0.36f, 0.66f, 0.95f);
        private static readonly Color Warn = new Color(0.98f, 0.78f, 0.3f);
        private static readonly Color Crit = new Color(0.95f, 0.35f, 0.3f);
        private static readonly Color Track = new Color(1f, 1f, 1f, 0.08f);

        private readonly List<HudRow> rows = new List<HudRow>();
        private string title = "";
        private string subtitle = "";
        private int lastUpdate = -1;
        private bool collapsed;

        public GameComponent_RimverseHud(Game game)
        {
        }

        public static string Num(float v) => Mathf.Abs(v) >= 10000f ? v.ToString("N0") : v.ToString("0.#");

        public override void GameComponentOnGUI()
        {
            if (!QolFeatures.IsEnabled("Rimverse_Qol_Hud")) return;
            Map map = Find.CurrentMap;
            if (map == null || WorldRendererUtility.WorldRendered || Find.UIRoot == null) return;
            if (Time.frameCount - lastUpdate > 30)
            {
                lastUpdate = Time.frameCount;
                Build(map);
            }
            if (rows.Count == 0) return;
            float height = Pad * 2f + 22f + (collapsed ? 0f : rows.Count * RowHeight + 4f);
            float x = UI.screenWidth - Width - 10f;
            float y = 10f + RimverseMod.Settings.hudOffsetY;
            Rect r = new Rect(x, y, Width, height);
            Widgets.DrawBoxSolid(r, Back);
            GUI.color = Edge;
            Widgets.DrawBox(r);
            GUI.color = Color.white;
            Widgets.DrawBoxSolid(new Rect(r.x, r.y, 2f, r.height), Worst() == 2 ? Crit : Worst() == 1 ? Warn : Accent);
            Rect head = new Rect(r.x + Pad + 2f, r.y + Pad, r.width - Pad * 2f - 2f, 22f);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(head.x, head.y, head.width - 70f, head.height), title.Truncate(head.width - 70f));
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleRight;
            GUI.color = Muted;
            Widgets.Label(new Rect(head.xMax - 70f, head.y, 70f, head.height), subtitle);
            GUI.color = Color.white;
            if (Widgets.ButtonInvisible(head)) collapsed = !collapsed;
            TooltipHandler.TipRegion(head, "Rimverse_HudToggle".Translate());
            if (!collapsed)
            {
                float ry = head.yMax + 4f;
                foreach (HudRow row in rows)
                {
                    Rect rr = new Rect(head.x, ry, head.width, RowHeight);
                    Text.Anchor = TextAnchor.MiddleLeft;
                    GUI.color = Muted;
                    Widgets.Label(new Rect(rr.x, rr.y, rr.width * 0.45f, rr.height - 3f), row.label);
                    Text.Anchor = TextAnchor.MiddleRight;
                    GUI.color = row.level == 2 ? Crit : row.level == 1 ? Warn : Color.white;
                    Widgets.Label(new Rect(rr.x + rr.width * 0.4f, rr.y, rr.width * 0.6f, rr.height - 3f), row.value);
                    if (row.fill >= 0f)
                    {
                        Rect bar = new Rect(rr.x, rr.yMax - 3f, rr.width, 2f);
                        GUI.color = Color.white;
                        Widgets.DrawBoxSolid(bar, Track);
                        Widgets.DrawBoxSolid(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(row.fill), bar.height), row.level == 2 ? Crit : row.level == 1 ? Warn : Accent);
                    }
                    GUI.color = Color.white;
                    if (!row.tip.NullOrEmpty()) TooltipHandler.TipRegion(rr, row.tip);
                    ry += RowHeight;
                }
            }
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
        }

        private int Worst()
        {
            int w = 0;
            foreach (HudRow r in rows) if (r.level > w) w = r.level;
            return w;
        }

        private void Add(string label, string value, float fill, int level, string tip = null)
        {
            rows.Add(new HudRow { label = label, value = value, fill = fill, level = level, tip = tip });
        }

        private static int Low(float frac, float warn, float crit) => frac <= crit ? 2 : frac <= warn ? 1 : 0;

        private void Build(Map map)
        {
            rows.Clear();
            RimverseShip ship = ColonyUtility.PlayerShipOn(map);
            if (ship == null && RimverseMod.Settings.hudOnlyShip) return;
            title = ship != null ? ship.Label : map.Parent?.LabelCap ?? map.ToString();
            MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(map);
            subtitle = ship != null ? (loc != null && !loc.OnSurface ? "Rimverse_HudInSpace".Translate().ToString() : "Rimverse_HudLanded".Translate().ToString()) : "";
            string cut = loc?.SurfaceCutLabel;
            if (!cut.NullOrEmpty()) subtitle = subtitle.NullOrEmpty() ? cut : subtitle + " · " + cut;
            int colonists = Mathf.Max(1, map.mapPawns.FreeColonistsSpawnedCount);
            float days = map.resourceCounter.TotalHumanEdibleNutrition / (colonists * 1.6f);
            Add("Rimverse_HudFood".Translate(), "Rimverse_HudDays".Translate(days.ToString("0.#")), -1f, days < 2f ? 2 : days < 5f ? 1 : 0, "Rimverse_HudFoodTip".Translate(colonists));
            float stored = 0f, gain = 0f, cap = 0f;
            foreach (PowerNet net in map.powerNetManager.AllNetsListForReading)
            {
                stored += net.CurrentStoredEnergy();
                gain += net.CurrentEnergyGainRate() / CompPower.WattsToWattDaysPerTick;
                foreach (CompPowerBattery b in net.batteryComps) cap += b.Props.storedEnergyMax;
            }
            int powerLevel = gain < 0f && stored < -gain * 0.5f ? 2 : gain < 0f ? 1 : 0;
            Add("Rimverse_HudPower".Translate(), (gain >= 0f ? "+" : "") + Num(gain) + " W", cap > 0f ? stored / cap : -1f, powerLevel, "Rimverse_HudPowerTip".Translate(Num(stored), Num(cap)));
            if (ship != null)
            {
                Add("Rimverse_HudAir".Translate(), ship.AirFraction.ToStringPercent("0"), ship.AirFraction, Low(ship.AirFraction, 0.5f, 0.25f));
                Add("Rimverse_HudHull".Translate(), ship.Integrity.ToStringPercent("0"), ship.Integrity, Low(ship.Integrity, 0.6f, 0.3f));
                float heat = ship.heatCapacity <= 0f ? 0f : ship.state.heat / ship.heatCapacity;
                Add("Rimverse_HudHeat".Translate(), heat.ToStringPercent("0"), heat, heat >= 0.9f ? 2 : heat >= 0.7f ? 1 : 0);
                float fuel = ShipLaunchUtility.FuelAvailable(ship);
                Add("Rimverse_HudFuel".Translate(), Num(fuel), -1f, fuel <= 0f ? 2 : 0);
            }
            IntVec3 mouse = UI.MouseCell();
            float inside = mouse.InBounds(map) ? mouse.GetTemperature(map) : map.mapTemperature.OutdoorTemp;
            Add("Rimverse_HudTempShort".Translate(), inside.ToStringTemperature("F0") + "  /  " + map.mapTemperature.OutdoorTemp.ToStringTemperature("F0"), -1f, 0, "Rimverse_HudTempTip".Translate());
        }
    }

    [HarmonyPatch(typeof(QualityUtility), nameof(QualityUtility.GetLabel))]
    public static class Patch_QualityColors
    {
        public static void Postfix(QualityCategory cat, ref string __result)
        {
            if (!QolFeatures.IsEnabled("Rimverse_Qol_QualityColors")) return;
            __result = __result.Colorize(QualityColor(cat));
        }

        public static Color QualityColor(QualityCategory q)
        {
            switch (q)
            {
                case QualityCategory.Awful: return new Color(0.65f, 0.35f, 0.35f);
                case QualityCategory.Poor: return new Color(0.75f, 0.6f, 0.5f);
                case QualityCategory.Normal: return Color.white;
                case QualityCategory.Good: return new Color(0.5f, 0.9f, 0.5f);
                case QualityCategory.Excellent: return new Color(0.4f, 0.65f, 1f);
                case QualityCategory.Masterwork: return new Color(0.8f, 0.5f, 1f);
                default: return new Color(1f, 0.75f, 0.3f);
            }
        }
    }

    [HarmonyPatch(typeof(Trait), nameof(Trait.LabelCap), MethodType.Getter)]
    public static class Patch_TraitColors
    {
        private static readonly HashSet<string> Good = new HashSet<string> { "Kind", "Tough", "QuickSleeper", "GreatMemory", "FastLearner", "Sanguine", "Industriousness+", "Industriousness+2", "NaturalMood+", "NaturalMood+2", "Nerves+", "Nerves+2", "SpeedOffset+", "SpeedOffset+2", "Beauty+", "Beauty+2", "Immunity+", "PsychicSensitivity+", "Ascetic", "Bloodlust" };
        private static readonly HashSet<string> Bad = new HashSet<string> { "Pyromaniac", "Wimp", "Gourmand", "Abrasive", "SlowLearner", "Volatile", "Industriousness-", "Industriousness-2", "NaturalMood-", "NaturalMood-2", "Nerves-", "Nerves-2", "SpeedOffset-", "Beauty-", "Beauty-2", "Immunity-", "ChemicalInterest", "ChemicalFascination", "Jealous", "Greedy", "TorturedArtist", "BodyPurist" };

        public static void Postfix(Trait __instance, ref string __result)
        {
            if (__instance?.def == null || !QolFeatures.IsEnabled("Rimverse_Qol_TraitColors")) return;
            string key = __instance.def.defName;
            if (__instance.def.degreeDatas != null && __instance.def.degreeDatas.Count > 1 && __instance.Degree != 0) key += (__instance.Degree > 0 ? "+" : "-") + (Mathf.Abs(__instance.Degree) > 1 ? Mathf.Abs(__instance.Degree).ToString() : "");
            if (Good.Contains(key)) __result = __result.Colorize(new Color(0.55f, 0.95f, 0.55f));
            else if (Bad.Contains(key)) __result = __result.Colorize(new Color(1f, 0.55f, 0.5f));
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
    public static class Patch_DamageNumbers
    {
        public static void Postfix(Thing __instance, DamageWorker.DamageResult __result)
        {
            if (__result == null || __result.totalDamageDealt < 0.5f || !(__instance is Pawn p) || !p.Spawned || p.Map != Find.CurrentMap) return;
            if (!QolFeatures.IsEnabled("Rimverse_Qol_DamageNumbers")) return;
            MoteMaker.ThrowText(p.DrawPos + new Vector3(0f, 0f, 0.5f), p.Map, __result.totalDamageDealt.ToString("0"), p.Faction == Faction.OfPlayer ? new Color(1f, 0.4f, 0.4f) : new Color(1f, 0.85f, 0.4f), 1.5f);
        }
    }

    [HarmonyPatch(typeof(ThingWithComps), nameof(ThingWithComps.GetInspectString))]
    public static class Patch_ExtraInspect
    {
        public static void Postfix(ThingWithComps __instance, ref string __result)
        {
            if (__instance is Corpse corpse)
            {
                if (!QolFeatures.IsEnabled("Rimverse_Qol_KillerInfo")) return;
                string k = GameComponent_RimverseQolExtra.Instance?.KillerOf(corpse.InnerPawn);
                if (!k.NullOrEmpty()) __result = Append(__result, "Rimverse_KilledBy".Translate(k));
                return;
            }
            if (__instance.def.category != ThingCategory.Item || !QolFeatures.IsEnabled("Rimverse_Qol_ItemInspect")) return;
            float value = __instance.MarketValue * __instance.stackCount;
            float mass = __instance.GetStatValue(StatDefOf.Mass) * __instance.stackCount;
            __result = Append(__result, "Rimverse_ItemInspect".Translate(GameComponent_RimverseHud.Num(value), mass.ToStringMass()));
        }

        public static string Append(string a, string b) => a.NullOrEmpty() ? b : a + "\n" + b;
    }

    public class GameComponent_RimverseQolExtra : GameComponent
    {
        public Dictionary<int, string> killers = new Dictionary<int, string>();
        public Dictionary<int, IntVec3> positions = new Dictionary<int, IntVec3>();
        public Dictionary<int, int> lostWeapons = new Dictionary<int, int>();
        public HashSet<int> hunters = new HashSet<int>();
        public HashSet<int> pausedHarvest = new HashSet<int>();
        public Dictionary<int, StoragePriority> flicked = new Dictionary<int, StoragePriority>();
        private List<int> k1;
        private List<string> v1;
        private List<int> k2;
        private List<IntVec3> v2;
        private List<int> k3;
        private List<StoragePriority> v3;
        private List<int> hl;
        private List<int> ph;

        public static GameComponent_RimverseQolExtra Instance => Current.Game?.GetComponent<GameComponent_RimverseQolExtra>();

        public GameComponent_RimverseQolExtra(Game game)
        {
        }

        public string KillerOf(Pawn p) => p != null && killers.TryGetValue(p.thingIDNumber, out string s) ? s : null;

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref killers, "killers", LookMode.Value, LookMode.Value, ref k1, ref v1);
            Scribe_Collections.Look(ref positions, "positions", LookMode.Value, LookMode.Value, ref k2, ref v2);
            Scribe_Collections.Look(ref flicked, "flicked", LookMode.Value, LookMode.Value, ref k3, ref v3);
            hl = hunters.ToList();
            ph = pausedHarvest.ToList();
            Scribe_Collections.Look(ref hl, "hunters", LookMode.Value);
            Scribe_Collections.Look(ref ph, "pausedHarvest", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                killers = killers ?? new Dictionary<int, string>();
                positions = positions ?? new Dictionary<int, IntVec3>();
                flicked = flicked ?? new Dictionary<int, StoragePriority>();
                hunters = hl == null ? new HashSet<int>() : new HashSet<int>(hl);
                pausedHarvest = ph == null ? new HashSet<int>() : new HashSet<int>(ph);
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class Patch_RecordKiller
    {
        public static void Prefix(Pawn __instance, DamageInfo? dinfo, Hediff exactCulprit)
        {
            GameComponent_RimverseQolExtra g = GameComponent_RimverseQolExtra.Instance;
            if (g == null || __instance == null) return;
            string who = dinfo?.Instigator?.LabelShort;
            string how = exactCulprit?.LabelCap ?? dinfo?.Def?.label;
            if (who.NullOrEmpty() && how.NullOrEmpty()) return;
            g.killers[__instance.thingIDNumber] = who.NullOrEmpty() ? how : (how.NullOrEmpty() ? who : who + " (" + how + ")");
        }
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.AddHediff), new[] { typeof(Hediff), typeof(BodyPartRecord), typeof(DamageInfo?), typeof(DamageWorker.DamageResult) })]
    public static class Patch_LostPartMessage
    {
        public static void Postfix(Pawn ___pawn, Hediff hediff, DamageInfo? dinfo)
        {
            if (!(hediff is Hediff_MissingPart) || dinfo?.Instigator == null || hediff.Part == null) return;
            Pawn p = ___pawn;
            if (p == null || p.Faction != Faction.OfPlayer || !QolFeatures.IsEnabled("Rimverse_Qol_KillerInfo")) return;
            if (p.health.hediffSet.PartIsMissing(hediff.Part.parent ?? hediff.Part) && hediff.Part.parent != null) return;
            Messages.Message("Rimverse_MsgLostPart".Translate(p.LabelShort, hediff.Part.LabelCap, dinfo.Value.Instigator.LabelShort), p, MessageTypeDefOf.NegativeHealthEvent, false);
        }
    }

    [HarmonyPatch(typeof(Designator_Build), nameof(Designator_Build.Desc), MethodType.Getter)]
    public static class Patch_BuildMaterialCount
    {
        public static void Postfix(BuildableDef ___entDef, ThingDef ___stuffDef, ref string __result)
        {
            Map map = Find.CurrentMap;
            BuildableDef def = ___entDef;
            if (map == null || def == null || !QolFeatures.IsEnabled("Rimverse_Qol_MaterialCount")) return;
            List<string> parts = new List<string>();
            if (def.MadeFromStuff && ___stuffDef != null) parts.Add(___stuffDef.LabelCap + ": " + map.resourceCounter.GetCount(___stuffDef));
            if (def.CostList != null)
            {
                foreach (ThingDefCountClass c in def.CostList) parts.Add(c.thingDef.LabelCap + ": " + map.resourceCounter.GetCount(c.thingDef));
            }
            if (parts.Count > 0) __result += "\n\n" + "Rimverse_MaterialsHave".Translate(string.Join(", ", parts));
        }
    }

    public class Alert_DeepDrillEmpty : Alert
    {
        private readonly List<Thing> drills = new List<Thing>();

        public Alert_DeepDrillEmpty()
        {
            defaultLabel = "Rimverse_AlertDrillEmpty".Translate();
            defaultExplanation = "Rimverse_AlertDrillEmptyDesc".Translate();
        }

        public override AlertReport GetReport()
        {
            drills.Clear();
            if (!QolFeatures.IsEnabled("Rimverse_Qol_DeepDrillAlert")) return false;
            foreach (Map m in Find.Maps)
            {
                if (!m.IsPlayerHome || EndlessRoomsCompat.IsBackrooms(m)) continue;
                foreach (Building b in m.listerBuildings.allBuildingsColonist)
                {
                    CompDeepDrill d = b.TryGetComp<CompDeepDrill>();
                    if (d != null && !d.ValuableResourcesPresent()) drills.Add(b);
                }
            }
            return AlertReport.CulpritsAre(drills);
        }
    }

    public class Alert_LowStoredPower : Alert
    {
        private readonly List<Thing> batteries = new List<Thing>();

        public Alert_LowStoredPower()
        {
            defaultLabel = "Rimverse_AlertLowPower".Translate();
            defaultExplanation = "Rimverse_AlertLowPowerDesc".Translate();
            defaultPriority = AlertPriority.High;
        }

        public override AlertReport GetReport()
        {
            batteries.Clear();
            if (!QolFeatures.IsEnabled("Rimverse_Qol_PowerAlerts")) return false;
            foreach (Map m in Find.Maps)
            {
                if (!m.IsPlayerHome || EndlessRoomsCompat.IsBackrooms(m)) continue;
                foreach (PowerNet net in m.powerNetManager.AllNetsListForReading)
                {
                    if (net.batteryComps.Count == 0 || net.CurrentEnergyGainRate() >= 0f) continue;
                    float max = net.batteryComps.Sum(b => b.Props.storedEnergyMax);
                    if (max > 0f && net.CurrentStoredEnergy() / max < 0.15f) batteries.AddRange(net.batteryComps.Select(b => (Thing)b.parent));
                }
            }
            return AlertReport.CulpritsAre(batteries);
        }
    }

    public class MapComponent_RimverseQolDraw : MapComponent
    {
        public MapComponent_RimverseQolDraw(Map map) : base(map)
        {
        }

        public override void MapComponentUpdate()
        {
            if (Find.CurrentMap != map || !QolFeatures.IsEnabled("Rimverse_Qol_RangeRing")) return;
            foreach (object o in Find.Selector.SelectedObjectsListForReading)
            {
                if (!(o is Pawn p) || !p.Drafted || p.equipment?.Primary == null) continue;
                Verb v = p.equipment.PrimaryEq?.PrimaryVerb;
                if (v == null || v.verbProps.IsMeleeAttack) continue;
                float range = v.verbProps.range;
                if (range > 1.5f && range < GenRadial.MaxRadialPatternRadius) GenDraw.DrawRadiusRing(p.Position, range);
            }
        }
    }

    public class MainButtonWorker_RimverseQolTab : MainButtonWorker_ToggleTab
    {
        public override bool Visible => base.Visible && QolFeatures.IsEnabled(def.defName == "Rimverse_PowerTab" ? "Rimverse_Qol_PowerTab" : "Rimverse_Qol_PawnTable");
    }

    public class MainTabWindow_RimversePower : MainTabWindow
    {
        private Vector2 scroll;

        public override Vector2 RequestedTabSize => new Vector2(640f, 420f);

        public override void DoWindowContents(Rect inRect)
        {
            Map map = Find.CurrentMap;
            if (map == null) return;
            List<PowerNet> nets = map.powerNetManager.AllNetsListForReading.Where(n => n.powerComps.Count > 0 || n.batteryComps.Count > 0).ToList();
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), "Rimverse_PowerTabTitle".Translate(nets.Count));
            Text.Font = GameFont.Small;
            Rect outer = new Rect(inRect.x, inRect.y + 36f, inRect.width, inRect.height - 36f);
            Rect view = new Rect(0f, 0f, outer.width - 16f, nets.Count * 56f);
            Widgets.BeginScrollView(outer, ref scroll, view);
            float y = 0f;
            int i = 1;
            foreach (PowerNet net in nets)
            {
                float prod = 0f, use = 0f;
                foreach (CompPowerTrader c in net.powerComps)
                {
                    if (!c.PowerOn) continue;
                    if (c.PowerOutput > 0f) prod += c.PowerOutput;
                    else use -= c.PowerOutput;
                }
                float stored = net.CurrentStoredEnergy();
                float max = net.batteryComps.Sum(b => b.Props.storedEnergyMax);
                Rect row = new Rect(0f, y, view.width, 52f);
                if (i % 2 == 0) Widgets.DrawLightHighlight(row);
                Widgets.Label(new Rect(row.x + 4f, row.y + 2f, row.width, 24f), "Rimverse_PowerNetLine".Translate(i, net.powerComps.Count + net.batteryComps.Count, prod.ToString("0"), use.ToString("0"), (prod - use).ToString("0")));
                Widgets.Label(new Rect(row.x + 4f, row.y + 26f, row.width - 90f, 24f), "Rimverse_PowerNetStored".Translate(stored.ToString("0"), max.ToString("0")));
                Thing first = net.powerComps.FirstOrDefault()?.parent ?? net.batteryComps.FirstOrDefault()?.parent;
                if (first != null && Widgets.ButtonText(new Rect(row.xMax - 84f, row.y + 12f, 80f, 28f), "Rimverse_Show".Translate()))
                {
                    CameraJumper.TryJumpAndSelect(first);
                }
                y += 56f;
                i++;
            }
            Widgets.EndScrollView();
        }
    }

    public class MainTabWindow_RimversePawnTable : MainTabWindow
    {
        private static readonly List<string> columns = new List<string> { "Mood", "Health", "Shooting", "Melee", "Construction", "Intellectual" };
        private Vector2 scroll;

        public override Vector2 RequestedTabSize => new Vector2(Mathf.Min(UI.screenWidth - 40f, 180f + columns.Count * 100f + 40f), 520f);

        private static IEnumerable<string> AllColumns()
        {
            yield return "Mood";
            yield return "Health";
            yield return "Rest";
            yield return "Food";
            yield return "Joy";
            yield return "MoveSpeed";
            yield return "WorkSpeed";
            foreach (SkillDef s in DefDatabase<SkillDef>.AllDefsListForReading) yield return s.defName;
        }

        private static string Header(string c)
        {
            SkillDef s = DefDatabase<SkillDef>.GetNamedSilentFail(c);
            return s != null ? s.LabelCap.ToString() : ("Rimverse_Col_" + c).Translate().ToString();
        }

        private static string Value(Pawn p, string c)
        {
            switch (c)
            {
                case "Mood": return p.needs?.mood == null ? "-" : p.needs.mood.CurLevelPercentage.ToStringPercent();
                case "Health": return p.health.summaryHealth.SummaryHealthPercent.ToStringPercent();
                case "Rest": return p.needs?.rest == null ? "-" : p.needs.rest.CurLevelPercentage.ToStringPercent();
                case "Food": return p.needs?.food == null ? "-" : p.needs.food.CurLevelPercentage.ToStringPercent();
                case "Joy": return p.needs?.joy == null ? "-" : p.needs.joy.CurLevelPercentage.ToStringPercent();
                case "MoveSpeed": return p.GetStatValue(StatDefOf.MoveSpeed).ToString("0.0");
                case "WorkSpeed": return p.GetStatValue(StatDefOf.WorkSpeedGlobal).ToStringPercent();
            }
            SkillDef s = DefDatabase<SkillDef>.GetNamedSilentFail(c);
            SkillRecord r = s == null ? null : p.skills?.GetSkill(s);
            if (r == null) return "-";
            return r.TotallyDisabled ? "-" : r.Level + (r.passion == Passion.Major ? "++" : r.passion == Passion.Minor ? "+" : "");
        }

        public override void DoWindowContents(Rect inRect)
        {
            Map map = Find.CurrentMap;
            if (map == null) return;
            List<Pawn> pawns = map.mapPawns.FreeColonistsSpawned.ToList();
            float x = 180f;
            if (Widgets.ButtonText(new Rect(inRect.x, inRect.y, 170f, 28f), "Rimverse_AddColumn".Translate()))
            {
                Find.WindowStack.Add(new FloatMenu(AllColumns().Where(c => !columns.Contains(c)).Select(c => new FloatMenuOption(Header(c), () => columns.Add(c))).ToList()));
            }
            for (int i = 0; i < columns.Count; i++)
            {
                Rect h = new Rect(inRect.x + x + i * 100f, inRect.y, 96f, 28f);
                if (Widgets.ButtonText(h, Header(columns[i])))
                {
                    string c = columns[i];
                    Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
                    {
                        new FloatMenuOption("Rimverse_SortBy".Translate(), () => sortBy = c),
                        new FloatMenuOption("Rimverse_RemoveColumn".Translate(), () => columns.Remove(c))
                    }));
                }
            }
            if (!sortBy.NullOrEmpty()) pawns = pawns.OrderByDescending(p => SortKey(p, sortBy)).ToList();
            Rect outer = new Rect(inRect.x, inRect.y + 34f, inRect.width, inRect.height - 34f);
            Rect view = new Rect(0f, 0f, outer.width - 16f, pawns.Count * 28f);
            Widgets.BeginScrollView(outer, ref scroll, view);
            for (int j = 0; j < pawns.Count; j++)
            {
                Pawn p = pawns[j];
                Rect row = new Rect(0f, j * 28f, view.width, 28f);
                if (j % 2 == 0) Widgets.DrawLightHighlight(row);
                if (Widgets.ButtonInvisible(new Rect(0f, row.y, 176f, 28f))) CameraJumper.TryJumpAndSelect(p);
                Widgets.Label(new Rect(4f, row.y + 3f, 172f, 24f), p.LabelShortCap);
                for (int i = 0; i < columns.Count; i++) Widgets.Label(new Rect(x + i * 100f, row.y + 3f, 96f, 24f), Value(p, columns[i]));
            }
            Widgets.EndScrollView();
        }

        private static string sortBy;

        private static float SortKey(Pawn p, string c)
        {
            SkillDef s = DefDatabase<SkillDef>.GetNamedSilentFail(c);
            if (s != null) return p.skills?.GetSkill(s)?.Level ?? -1;
            switch (c)
            {
                case "Mood": return p.needs?.mood?.CurLevel ?? 0f;
                case "Health": return p.health.summaryHealth.SummaryHealthPercent;
                case "Rest": return p.needs?.rest?.CurLevel ?? 0f;
                case "Food": return p.needs?.food?.CurLevel ?? 0f;
                case "Joy": return p.needs?.joy?.CurLevel ?? 0f;
                case "MoveSpeed": return p.GetStatValue(StatDefOf.MoveSpeed);
                case "WorkSpeed": return p.GetStatValue(StatDefOf.WorkSpeedGlobal);
            }
            return 0f;
        }
    }

    public class ITab_RimverseGear : ITab
    {
        private Vector2 scroll;

        public ITab_RimverseGear()
        {
            size = new Vector2(460f, 480f);
            labelKey = "Rimverse_TabGear";
        }

        public override bool IsVisible => SelPawn != null && (SelPawn.apparel != null || SelPawn.equipment != null) && QolFeatures.IsEnabled("Rimverse_Qol_GearTab");

        protected override void FillTab()
        {
            Pawn p = SelPawn;
            Rect r = new Rect(0f, 0f, size.x, size.y).ContractedBy(10f);
            List<string> lines = new List<string>();
            lines.Add("Rimverse_GearArmor".Translate(p.GetStatValue(StatDefOf.ArmorRating_Sharp).ToStringPercent(), p.GetStatValue(StatDefOf.ArmorRating_Blunt).ToStringPercent(), p.GetStatValue(StatDefOf.ArmorRating_Heat).ToStringPercent()));
            lines.Add("Rimverse_GearInsulation".Translate(p.GetStatValue(StatDefOf.ComfyTemperatureMin).ToStringTemperature("F0"), p.GetStatValue(StatDefOf.ComfyTemperatureMax).ToStringTemperature("F0")));
            lines.Add("Rimverse_GearVacuum".Translate(SpaceMapUtility.VacuumProtection(p).ToStringPercent()));
            float carried = MassUtility.GearAndInventoryMass(p);
            lines.Add("Rimverse_GearMass".Translate(carried.ToStringMass(), MassUtility.Capacity(p).ToStringMass()));
            lines.Add("");
            if (p.equipment?.Primary != null)
            {
                ThingWithComps w = p.equipment.Primary;
                Verb v = p.equipment.PrimaryEq?.PrimaryVerb;
                string extra = v != null && !v.verbProps.IsMeleeAttack ? "Rimverse_GearRange".Translate(v.verbProps.range.ToString("0")).ToString() : "";
                lines.Add(w.LabelCap + " " + extra);
            }
            if (p.apparel != null) foreach (Apparel a in p.apparel.WornApparel) lines.Add("· " + a.LabelCap + " (" + a.HitPoints + "/" + a.MaxHitPoints + ")");
            if (p.inventory != null && p.inventory.innerContainer.Count > 0)
            {
                lines.Add("");
                lines.Add("Rimverse_GearInventory".Translate());
                foreach (Thing t in p.inventory.innerContainer) lines.Add("· " + t.LabelCap);
            }
            string text = string.Join("\n", lines);
            Rect view = new Rect(0f, 0f, r.width - 16f, Text.CalcHeight(text, r.width - 16f) + 10f);
            Widgets.BeginScrollView(r, ref scroll, view);
            Widgets.Label(view, text);
            Widgets.EndScrollView();
        }
    }

    public class Window_RimverseSearch : Window
    {
        private string query = "";
        private Vector2 scroll;
        private List<(string label, GlobalTargetInfo target)> results = new List<(string, GlobalTargetInfo)>();
        private string last;

        public override Vector2 InitialSize => new Vector2(460f, 520f);

        public Window_RimverseSearch()
        {
            doCloseX = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = false;
            preventCameraMotion = false;
        }

        private void Refresh()
        {
            results.Clear();
            last = query;
            if (query.Length < 2) return;
            string q = query.ToLowerInvariant();
            Map map = Find.CurrentMap;
            if (map != null)
            {
                foreach (Thing t in map.listerThings.AllThings)
                {
                    if (results.Count >= 300) break;
                    if (t.def.category != ThingCategory.Item && t.def.category != ThingCategory.Pawn && t.def.category != ThingCategory.Building) continue;
                    if (t.Position.Fogged(map)) continue;
                    if (t.LabelNoCount.ToLowerInvariant().Contains(q)) results.Add((t.LabelCap.ToString(), new GlobalTargetInfo(t)));
                }
            }
            GameComponent_Universe u = GameComponent_Universe.Instance;
            StarSystemData home = u?.HomeSystem;
            if (home != null)
            {
                foreach (BodyData b in home.bodies)
                {
                    if (b.name != null && b.name.ToLowerInvariant().Contains(q)) results.Add(("Rimverse_SearchBody".Translate(b.name, home.name).ToString(), GlobalTargetInfo.Invalid));
                }
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            GUI.SetNextControlName("RimverseSearch");
            query = Widgets.TextField(new Rect(inRect.x, inRect.y, inRect.width, 30f), query);
            if (Event.current.type == EventType.Layout) GUI.FocusControl("RimverseSearch");
            if (query != last) Refresh();
            Rect outer = new Rect(inRect.x, inRect.y + 36f, inRect.width, inRect.height - 36f);
            Rect view = new Rect(0f, 0f, outer.width - 16f, results.Count * 26f);
            Widgets.BeginScrollView(outer, ref scroll, view);
            for (int i = 0; i < results.Count; i++)
            {
                Rect row = new Rect(0f, i * 26f, view.width, 26f);
                if (i % 2 == 0) Widgets.DrawLightHighlight(row);
                Widgets.Label(row.ContractedBy(2f), results[i].label);
                if (results[i].target.IsValid && Widgets.ButtonInvisible(row)) CameraJumper.TryJumpAndSelect(results[i].target);
            }
            Widgets.EndScrollView();
        }
    }

    public class Window_RimverseMinimap : Window
    {
        private Texture2D tex;
        private Map map;
        private int lastFrame = -999;

        public override Vector2 InitialSize => new Vector2(280f, 300f);

        protected override float Margin => 6f;

        public Window_RimverseMinimap()
        {
            doCloseX = true;
            draggable = true;
            preventCameraMotion = false;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = false;
            focusWhenOpened = false;
            layer = WindowLayer.GameUI;
        }

        private void Rebuild()
        {
            map = Find.CurrentMap;
            if (map == null) return;
            int w = map.Size.x, h = map.Size.z;
            if (tex == null || tex.width != w || tex.height != h)
            {
                tex = new Texture2D(w, h, TextureFormat.RGB24, false) { filterMode = FilterMode.Point };
            }
            Color[] px = new Color[w * h];
            foreach (IntVec3 c in map.AllCells)
            {
                Color col;
                if (c.Fogged(map)) col = new Color(0.08f, 0.08f, 0.08f);
                else
                {
                    Building b = c.GetEdifice(map);
                    if (b != null) col = b.Faction == Faction.OfPlayer ? new Color(0.55f, 0.6f, 0.7f) : new Color(0.35f, 0.33f, 0.3f);
                    else
                    {
                        TerrainDef t = c.GetTerrain(map);
                        col = t.IsWater ? new Color(0.2f, 0.35f, 0.6f) : t.fertility > 0.5f ? new Color(0.3f, 0.45f, 0.25f) : new Color(0.45f, 0.42f, 0.36f);
                    }
                }
                px[c.z * w + c.x] = col;
            }
            foreach (Pawn p in map.mapPawns.AllPawnsSpawned)
            {
                if (p.Position.Fogged(map)) continue;
                Color col = p.Faction == Faction.OfPlayer ? Color.green : p.HostileTo(Faction.OfPlayer) ? Color.red : Color.yellow;
                IntVec3 c = p.Position;
                for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    int x = c.x + dx, z = c.z + dz;
                    if (x >= 0 && z >= 0 && x < w && z < h) px[z * w + x] = col;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (Find.CurrentMap != map || Time.frameCount - lastFrame > 120)
            {
                lastFrame = Time.frameCount;
                Rebuild();
            }
            if (tex == null || map == null) return;
            Rect r = new Rect(inRect.x, inRect.y + 18f, inRect.width, inRect.height - 18f);
            GUI.DrawTexture(r, tex, ScaleMode.StretchToFill);
            if (Event.current.type == EventType.MouseDown && Mouse.IsOver(r))
            {
                Vector2 m = Event.current.mousePosition;
                float fx = (m.x - r.x) / r.width, fz = 1f - (m.y - r.y) / r.height;
                IntVec3 target = new IntVec3(Mathf.RoundToInt(fx * map.Size.x), 0, Mathf.RoundToInt(fz * map.Size.z));
                if (target.InBounds(map)) Find.CameraDriver.JumpToCurrentMapLoc(target);
                Event.current.Use();
            }
        }
    }

    [StaticConstructorOnStartup]
    public static class QolKeys
    {
        public static void Check()
        {
            if (Find.CurrentMap == null || Find.WindowStack == null) return;
            KeyBindingDef search = DefDatabase<KeyBindingDef>.GetNamedSilentFail("Rimverse_Search");
            KeyBindingDef mini = DefDatabase<KeyBindingDef>.GetNamedSilentFail("Rimverse_Minimap");
            if (QolKeyChords.Pressed(search) && QolFeatures.IsEnabled("Rimverse_Qol_Search"))
            {
                if (!Find.WindowStack.IsOpen<Window_RimverseSearch>()) Find.WindowStack.Add(new Window_RimverseSearch());
                Event.current.Use();
            }
            if (QolKeyChords.Pressed(mini) && QolFeatures.IsEnabled("Rimverse_Qol_Minimap"))
            {
                Window_RimverseMinimap w = Find.WindowStack.WindowOfType<Window_RimverseMinimap>();
                if (w != null) w.Close();
                else Find.WindowStack.Add(new Window_RimverseMinimap());
                Event.current.Use();
            }
        }
    }

    public class GameComponent_RimverseKeys : GameComponent
    {
        public GameComponent_RimverseKeys(Game game)
        {
        }

        public override void GameComponentOnGUI()
        {
            if (Event.current.type == EventType.KeyDown) QolKeys.Check();
        }
    }
}
