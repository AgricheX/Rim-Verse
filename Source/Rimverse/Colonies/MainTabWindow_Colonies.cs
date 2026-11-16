using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public class MainTabWindow_Colonies : MainTabWindow
    {
        private int selected = -1;
        private Vector2 listScroll;

        public override Vector2 RequestedTabSize => new Vector2(980f, 620f);

        public override void DoWindowContents(Rect inRect)
        {
            GameComponent_PlayerColonies gc = GameComponent_PlayerColonies.Instance;
            if (gc == null) return;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 34f), "Rimverse_ColoniesTitle".Translate(gc.colonies.Count, ColonyUtility.ColonyLimit(), gc.treasury.ToString("N0")));
            Text.Font = GameFont.Small;
            Rect body = new Rect(inRect.x, inRect.y + 40f, inRect.width, inRect.height - 40f);
            Rect left = body.LeftPart(0.32f).ContractedBy(4f);
            Rect right = body.RightPart(0.68f).ContractedBy(4f);
            Widgets.DrawMenuSection(left);
            Rect view = new Rect(0f, 0f, left.width - 16f, gc.colonies.Count * 48f);
            Widgets.BeginScrollView(left.ContractedBy(2f), ref listScroll, view);
            float y = 0f;
            foreach (PlayerColony c in gc.colonies)
            {
                Rect row = new Rect(0f, y, view.width, 46f);
                if (c.id == selected) Widgets.DrawHighlightSelected(row);
                else if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);
                Widgets.Label(new Rect(6f, y + 2f, row.width - 8f, 22f), c.name);
                GUI.color = Color.gray;
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(6f, y + 24f, row.width - 8f, 20f), (c.System?.name ?? "?") + " · " + "Rimverse_ColonyPopShort".Translate(c.population.ToString("0")));
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
                if (Widgets.ButtonInvisible(row)) selected = c.id;
                y += 48f;
            }
            Widgets.EndScrollView();
            PlayerColony sel = gc.colonies.FirstOrDefault(c => c.id == selected);
            if (sel == null)
            {
                Widgets.Label(right, gc.colonies.Count == 0 ? "Rimverse_ColoniesNone".Translate() : "Rimverse_ColoniesSelect".Translate());
                return;
            }
            DrawColony(right, sel, gc);
        }

        private void DrawColony(Rect rect, PlayerColony c, GameComponent_PlayerColonies gc)
        {
            Listing_Standard l = new Listing_Standard();
            l.Begin(rect);
            Text.Font = GameFont.Medium;
            l.Label(c.name + " · " + c.TierLabel);
            Text.Font = GameFont.Small;
            if (l.ButtonText("Rimverse_ManageColony".Translate())) Find.WindowStack.Add(new Dialog_ColonyManage(c));
            Pawn admin = c.Administrator;
            l.Label("Rimverse_ColonyLine1".Translate(c.System?.name ?? "?", c.population.ToString("0"), c.Capacity.ToString("0"), c.Hazard.ToStringPercent()));
            l.Label("Rimverse_ColonyLine2".Translate(c.stability.ToString("0"), c.lastIncome.ToString("N0"), c.attention.ToString("0"), ColonyUtility.AttackAttention.ToString("0")));
            l.Label("Rimverse_ColonyAdmin".Translate(admin != null ? admin.LabelShortCap : "-", c.settlers.Count));
            if (GameComponent_ColonyExtras.HasAI(c)) l.Label("Rimverse_ColonyAIAdmin".Translate());
            else if (GameComponent_ColonyExtras.FindCore(Find.CurrentMap) != null && l.ButtonText("Rimverse_ColonyInstallAI".Translate())) GameComponent_ColonyExtras.TryInstallAI(c, Find.CurrentMap);
            l.Label("Rimverse_ColonyNextStep".Translate(RimverseUnits.DaysFromTicks(c.nextStepTick - Find.TickManager.TicksGame).ToString("0")));
            l.GapLine();
            l.Label("Rimverse_ColonyIndustries".Translate());
            foreach (ColonyBuild b in c.industries)
            {
                string state = b.Ready ? "Rimverse_IndustryReady".Translate().ToString() : "Rimverse_IndustryBuilding".Translate(RimverseUnits.DaysFromTicks(b.readyTick - Find.TickManager.TicksGame).ToString("0")).ToString();
                l.Label("  " + ("Rimverse_Industry_" + b.type).Translate() + " · " + state);
            }
            l.Gap(6f);
            foreach (ColonyIndustry t in System.Enum.GetValues(typeof(ColonyIndustry)))
            {
                if (t == ColonyIndustry.Agriculture && c.Body != null && c.Body.atmosphere != AtmosphereType.Breathable && c.Body.atmosphere != AtmosphereType.Thin) continue;
                if (t == ColonyIndustry.GasHarvest && (c.System == null || !c.System.bodies.Any(b => b.type == BodyType.GasGiant))) continue;
                Rect r = l.GetRect(28f);
                string label = "Rimverse_BuildIndustry".Translate(("Rimverse_Industry_" + t).Translate(), ColonyUtility.Cost(t).ToString("N0"), ColonyUtility.Days(t), ColonyUtility.Upkeep(t).ToString("0"));
                bool afford = gc.treasury >= ColonyUtility.Cost(t);
                if (!afford) GUI.color = Color.gray;
                if (Widgets.ButtonText(r, label) && afford) gc.TryBuild(c, t);
                GUI.color = Color.white;
                TooltipHandler.TipRegion(r, ("Rimverse_IndustryDesc_" + t).Translate());
                l.Gap(2f);
            }
            l.GapLine();
            string stock = c.stock.Count == 0 ? "-" : string.Join(", ", c.stock.Select(kv => (DefDatabase<ThingDef>.GetNamedSilentFail(kv.Key)?.label ?? kv.Key) + " ×" + kv.Value));
            l.Label("Rimverse_ColonyStock".Translate(stock));
            Map map = c.surface ? null : ColonyUtility.PlayerMapInSystem(c.systemKey);
            if (c.surface)
            {
                GUI.color = Color.gray;
                l.Label("Rimverse_ColonySurfaceCollect".Translate());
                GUI.color = Color.white;
            }
            else if (map == null)
            {
                GUI.color = Color.gray;
                l.Label("Rimverse_ColonyNeedPresence".Translate());
                GUI.color = Color.white;
            }
            else
            {
                if (c.stock.Count > 0 && l.ButtonText("Rimverse_ColonyCollect".Translate())) gc.Collect(c, map);
                int money = ColonyUtility.MoneyOnMap(map);
                if (money > 0 && l.ButtonText("Rimverse_ColonyDeposit".Translate(money.ToString("N0")))) gc.treasury += ColonyUtility.DepositAll(map);
                if (gc.treasury >= 1000 && l.ButtonText("Rimverse_ColonyWithdraw".Translate("1.000")))
                {
                    gc.treasury -= 1000;
                    ColonyUtility.Withdraw(map, 1000);
                }
                if (l.ButtonText("Rimverse_ColonyRecall".Translate()))
                {
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("Rimverse_ColonyRecallConfirm".Translate(c.name), () => gc.Recall(c, map), true));
                }
            }
            l.End();
        }
    }
}
