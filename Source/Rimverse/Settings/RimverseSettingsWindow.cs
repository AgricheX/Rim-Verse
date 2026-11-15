using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public static class RimverseSettingsWindow
    {
        private enum Tab
        {
            General,
            Universe,
            Ship,
            Fluxo,
            Qol,
            Compat
        }

        private static Tab currentTab = Tab.General;
        private static Vector2 scroll;
        private static float lastHeight = 1200f;

        public static void Draw(Rect inRect, RimverseSettings s)
        {
            Rect tabRect = new Rect(inRect.x, inRect.y + 32f, inRect.width, inRect.height - 32f);
            List<TabRecord> tabs = new List<TabRecord>
            {
                MakeTab(Tab.General, "Rimverse_Tab_General"),
                MakeTab(Tab.Universe, "Rimverse_Tab_Universe"),
                MakeTab(Tab.Ship, "Rimverse_Tab_Ship"),
                MakeTab(Tab.Fluxo, "Rimverse_Tab_Fluxo"),
                MakeTab(Tab.Qol, "Rimverse_Tab_Qol"),
                MakeTab(Tab.Compat, "Rimverse_Tab_Compat")
            };
            Widgets.DrawMenuSection(tabRect);
            TabDrawer.DrawTabs(tabRect, tabs);
            Rect content = tabRect.ContractedBy(12f);
            Rect view = new Rect(0f, 0f, content.width - 16f, lastHeight);
            Widgets.BeginScrollView(content, ref scroll, view);
            Listing_Standard l = new Listing_Standard();
            l.Begin(view);
            switch (currentTab)
            {
                case Tab.General: DrawGeneral(l, s); break;
                case Tab.Universe: DrawUniverse(l, s); break;
                case Tab.Ship: DrawShip(l, s); break;
                case Tab.Fluxo: DrawFluxo(l, s); break;
                case Tab.Qol: DrawQol(l, s); break;
                case Tab.Compat: DrawCompat(l, s); break;
            }
            lastHeight = Mathf.Max(l.CurHeight + 24f, content.height);
            l.End();
            Widgets.EndScrollView();
        }

        private static TabRecord MakeTab(Tab tab, string key)
        {
            return new TabRecord(key.Translate(), () => { currentTab = tab; scroll = Vector2.zero; }, currentTab == tab);
        }

        private static void Slider(Listing_Standard l, string key, ref float value, float min, float max, bool percent = true)
        {
            string shown = percent ? value.ToStringPercent() : value.ToString("0.##");
            l.Label((string)(key.Translate() + ": " + shown), -1f, (TipSignal?)(string)((key + "_Desc").Translate()));
            value = l.Slider(value, min, max);
        }

        private static void IntSlider(Listing_Standard l, string key, ref int value, int min, int max)
        {
            l.Label((string)(key.Translate() + ": " + value), -1f, (TipSignal?)(string)((key + "_Desc").Translate()));
            value = Mathf.RoundToInt(l.Slider(value, min, max));
        }

        private static void Check(Listing_Standard l, string key, ref bool value, bool enabled = true)
        {
            if (enabled)
            {
                l.CheckboxLabeled(key.Translate(), ref value, (key + "_Desc").Translate());
            }
            else
            {
                bool shown = false;
                GUI.color = Color.gray;
                l.CheckboxLabeled(key.Translate() + " " + "Rimverse_ModInactive".Translate(), ref shown, (key + "_Desc").Translate());
                GUI.color = Color.white;
            }
        }

        private static void DrawGeneral(Listing_Standard l, RimverseSettings s)
        {
            Slider(l, "Rimverse_Threat", ref s.threatMultiplier, 0.1f, 3f);
            Slider(l, "Rimverse_Economy", ref s.economyMultiplier, 0.25f, 3f);
            Slider(l, "Rimverse_ResearchCost", ref s.researchCostMultiplier, 0.1f, 3f);
            l.GapLine();
            Check(l, "Rimverse_DebugLogging", ref s.debugLogging);
            l.Gap();
            if (l.ButtonText("Rimverse_ResetDefaults".Translate()))
            {
                s.ResetToDefaults();
            }
        }

        private static void DrawUniverse(Listing_Standard l, RimverseSettings s)
        {
            l.Label("Rimverse_NewGameOnly".Translate());
            l.GapLine();
            IntSlider(l, "Rimverse_GalaxyCount", ref s.galaxyCount, 1, 20);
            Slider(l, "Rimverse_StarDensity", ref s.starDensity, 0.02f, 1f);
            l.GapLine();
            Check(l, "Rimverse_CivSimulation", ref s.civilizationSimulation);
            Slider(l, "Rimverse_CivDensity", ref s.civilizationDensity, 0.2f, 2f);
            Check(l, "Rimverse_Endgame", ref s.endgameEnabled);
            if (s.endgameEnabled)
            {
                Slider(l, "Rimverse_CrisisYears", ref s.crisisStartYears, 3f, 40f, false);
                s.crisisStartYears = Mathf.Round(s.crisisStartYears);
            }
            float days = s.civilizationTickInterval / (float)RimverseUnits.TicksPerDay;
            l.Label((string)("Rimverse_CivTickInterval".Translate() + ": " + days.ToString("0.##")), -1f, (TipSignal?)(string)("Rimverse_CivTickInterval_Desc".Translate()));
            days = l.Slider(days, 0.25f, 5f);
            s.civilizationTickInterval = RimverseUnits.TicksFromDays(Mathf.Round(days * 4f) / 4f);
        }

        private static void DrawShip(Listing_Standard l, RimverseSettings s)
        {
            Slider(l, "Rimverse_AirConsumption", ref s.airConsumptionMultiplier, 0.1f, 3f);
            Slider(l, "Rimverse_FuelConsumption", ref s.fuelConsumptionMultiplier, 0.1f, 3f);
            Slider(l, "Rimverse_BreachDrain", ref s.breachDrainMultiplier, 0.1f, 3f);
            l.GapLine();
            Check(l, "Rimverse_ShipCombat", ref s.shipCombatEnabled);
            Check(l, "Rimverse_LifeAboard", ref s.lifeAboardEnabled);
            Check(l, "Rimverse_Lineages", ref s.lineagesEnabled);
            l.GapLine();
            Check(l, "Rimverse_HudOnlyShip", ref s.hudOnlyShip);
            l.Label((string)("Rimverse_HudOffset".Translate() + ": " + s.hudOffsetY.ToString("0")), -1f, (TipSignal?)(string)("Rimverse_HudOffset_Desc".Translate()));
            s.hudOffsetY = Mathf.Round(l.Slider(s.hudOffsetY, 0f, 600f));
        }

        private static void DrawFluxo(Listing_Standard l, RimverseSettings s)
        {
            Check(l, "Rimverse_FluxoEnabled", ref s.fluxoEnabled);
            if (s.fluxoEnabled)
            {
                float perThousand = s.fluxoSensitiveChance * 1000f;
                l.Label((string)("Rimverse_FluxoChance".Translate() + ": " + s.fluxoSensitiveChance.ToStringPercent("0.0#")), -1f, (TipSignal?)(string)("Rimverse_FluxoChance_Desc".Translate()));
                perThousand = l.Slider(perThousand, 0.5f, 50f);
                s.fluxoSensitiveChance = Mathf.Round(perThousand * 2f) / 2000f;
            }
        }

        private static void DrawQol(Listing_Standard l, RimverseSettings s)
        {
            Check(l, "Rimverse_QolEnabled", ref s.qolEnabled);
            if (!s.qolEnabled)
            {
                return;
            }
            l.GapLine();
            List<QolFeatureDef> defs = DefDatabase<QolFeatureDef>.AllDefsListForReading.OrderBy(d => d.category).ThenBy(d => d.label).ToList();
            if (defs.Count == 0)
            {
                l.Label("Rimverse_QolNone".Translate());
                return;
            }
            string lastCategory = null;
            foreach (QolFeatureDef def in defs)
            {
                if (def.category != lastCategory)
                {
                    lastCategory = def.category;
                    l.Gap(6f);
                    Text.Font = GameFont.Medium;
                    l.Label(("Rimverse_QolCategory_" + def.category).Translate());
                    Text.Font = GameFont.Small;
                }
                if (def.OriginalModActive)
                {
                    GUI.color = Color.gray;
                    l.Label((string)("  " + def.LabelCap + " " + "Rimverse_QolOriginalActive".Translate()), -1f, (TipSignal?)(string)(def.description));
                    GUI.color = Color.white;
                    continue;
                }
                bool value = s.IsQolEnabled(def);
                bool before = value;
                l.CheckboxLabeled("  " + def.LabelCap + (def.requiresRestart ? " " + (string)"Rimverse_RequiresRestart".Translate() : ""), ref value, def.description);
                if (value != before)
                {
                    s.SetQolEnabled(def, value);
                }
            }
        }

        private static void DrawCompat(Listing_Standard l, RimverseSettings s)
        {
            l.Label(ModCompat.Summary(true));
            l.GapLine();
            Check(l, "Rimverse_OdysseyIntegration", ref s.odysseyIntegration, ModCompat.Odyssey);
            Check(l, "Rimverse_CEIntegration", ref s.combatExtendedIntegration, ModCompat.CombatExtended);
            Check(l, "Rimverse_EndlessRoomsEvents", ref s.endlessRoomsEvents, ModCompat.EndlessRooms);
            Check(l, "Rimverse_FacialAnimationIntegration", ref s.facialAnimationIntegration, ModCompat.FacialAnimation);
            Check(l, "Rimverse_YayoIntegration", ref s.yayoAnimationIntegration, ModCompat.YayoAnimation);
        }
    }
}
