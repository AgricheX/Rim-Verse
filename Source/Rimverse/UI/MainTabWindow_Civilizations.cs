using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public class MainTabWindow_Civilizations : MainTabWindow
    {
        private int selectedId = -1;
        private Vector2 listScroll;
        private Vector2 infoScroll;
        private float infoHeight = 800f;

        public override Vector2 RequestedTabSize => new Vector2(1010f, 680f);

        private static GameComponent_Civilizations GC => GameComponent_Civilizations.Instance;

        public override void DoWindowContents(Rect inRect)
        {
            GameComponent_Civilizations gc = GC;
            if (gc == null || !RimverseMod.Settings.civilizationSimulation)
            {
                Widgets.Label(inRect, "Rimverse_CivDisabled".Translate());
                return;
            }
            gc.EnsureGenerated();
            List<Civilization> known = gc.Known.OrderBy(c => gc.DistanceToPlayer(c, out _)).ToList();
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 34f), "Rimverse_CivTabTitle".Translate(known.Count, gc.Alive.Count()));
            Text.Font = GameFont.Small;
            Rect body = new Rect(inRect.x, inRect.y + 38f, inRect.width, inRect.height - 38f);
            Rect listRect = body.LeftPart(0.34f).ContractedBy(4f);
            Rect infoRect = body.RightPart(0.66f).ContractedBy(4f);
            DrawList(listRect, known, gc);
            Civilization sel = known.FirstOrDefault(c => c.id == selectedId);
            if (sel == null)
            {
                Widgets.Label(infoRect, known.Count == 0 ? "Rimverse_CivNoneKnown".Translate() : "Rimverse_CivSelect".Translate());
                return;
            }
            DrawInfo(infoRect, sel, gc);
        }

        private void DrawList(Rect rect, List<Civilization> known, GameComponent_Civilizations gc)
        {
            Widgets.DrawMenuSection(rect);
            Rect view = new Rect(0f, 0f, rect.width - 16f, known.Count * 52f);
            Widgets.BeginScrollView(rect.ContractedBy(2f), ref listScroll, view);
            float y = 0f;
            foreach (Civilization c in known)
            {
                Rect row = new Rect(0f, y, view.width, 50f);
                if (c.id == selectedId) Widgets.DrawHighlightSelected(row);
                else if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);
                Widgets.DrawBoxSolid(new Rect(row.x + 4f, row.y + 8f, 6f, 34f), c.color);
                Widgets.Label(new Rect(row.x + 16f, row.y + 3f, row.width - 20f, 24f), c.Name);
                GUI.color = Color.gray;
                Text.Font = GameFont.Tiny;
                double d = gc.DistanceToPlayer(c, out _);
                Widgets.Label(new Rect(row.x + 16f, row.y + 26f, row.width - 20f, 22f), ("Rimverse_Stage_" + c.stage).Translate() + " · " + ("Rimverse_Diplo_" + c.playerState).Translate() + " · " + d.ToString("0") + " ly");
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
                if (Widgets.ButtonInvisible(row)) selectedId = c.id;
                y += 52f;
            }
            Widgets.EndScrollView();
        }

        private void DrawInfo(Rect rect, Civilization c, GameComponent_Civilizations gc)
        {
            Rect view = new Rect(0f, 0f, rect.width - 16f, infoHeight);
            Widgets.BeginScrollView(rect, ref infoScroll, view);
            Listing_Standard l = new Listing_Standard();
            l.Begin(view);
            Text.Font = GameFont.Medium;
            GUI.color = c.color;
            l.Label(c.Name);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            l.Label("Rimverse_CivStageLine".Translate(("Rimverse_Stage_" + c.stage).Translate(), c.sizeClass, c.tech.ToStringHuman()));
            l.Label("Rimverse_CivPopulation".Translate(PopText(c.Population), c.planets.Count));
            l.Label("Rimverse_CivStability".Translate(c.stability.ToString("0"), c.warWeariness.ToString("0")));
            l.Label("Rimverse_CivFleetPower".Translate(CivSimulation.FleetPower(gc, c).ToString("0")));
            StarSystemData cap = GameComponent_Universe.Instance.GetSystem(c.Capital);
            l.Label("Rimverse_CivCapital".Translate(cap?.name ?? "?", gc.DistanceToPlayer(c, out _).ToString("0")));
            if (c.InfluenceRadiusLy > 0f) l.Label("Rimverse_CivReach".Translate(c.InfluenceRadiusLy.ToString("0")));
            l.Label("Rimverse_CivGoal".Translate(("Rimverse_Goal_" + c.goal).Translate()));
            HediffDef lineage = c.lineage.NullOrEmpty() ? null : DefDatabase<HediffDef>.GetNamedSilentFail(c.lineage);
            l.Label(lineage != null ? "Rimverse_CivLineage".Translate(lineage.label) : "Rimverse_CivLineageNone".Translate());
            l.GapLine();
            PolicyLine(l, "Rimverse_PolicyGroup_Government", c.government);
            PolicyLine(l, "Rimverse_PolicyGroup_Ideology", c.ideology);
            PolicyLine(l, "Rimverse_PolicyGroup_Economy", c.economy);
            PolicyLine(l, "Rimverse_PolicyGroup_Xeno", c.xeno);
            PolicyLine(l, "Rimverse_PolicyGroup_Origin", c.origin);
            foreach (PolicyDef civic in c.civics) PolicyLine(l, "Rimverse_PolicyGroup_Civic", civic);
            if (c.ideologySupport.Count > 0)
            {
                l.Label("Rimverse_CivIdeologySupport".Translate() + " " + string.Join(", ", c.ideologySupport.OrderByDescending(kv => kv.Value).Select(kv => (DefDatabase<PolicyDef>.GetNamedSilentFail(kv.Key)?.label ?? kv.Key) + " " + kv.Value.ToString("0") + "%")));
            }
            l.Label("Rimverse_CivGroups".Translate() + " " + string.Join(", ", c.groupMood.Select(kv => ("Rimverse_Group_" + kv.Key).Translate().ToString() + " " + kv.Value.ToString("0"))));
            l.GapLine();
            l.Label("Rimverse_CivPlayerRelation".Translate(("Rimverse_Diplo_" + c.playerState).Translate(), c.playerOpinion.ToString("0")));
            if (!c.NoDiplomacy && !c.Hostile) l.Label("Rimverse_CivTariff".Translate(CivDiplomacy.Tariff(c.playerState).ToStringPercent()));
            Map map = Find.CurrentMap;
            if (!c.NoDiplomacy && !c.Hostile)
            {
                AcceptanceReport trade = CivDiplomacy.CanTrade(c, map);
                if (trade.Accepted)
                {
                    if (l.ButtonText("Rimverse_CivHail".Translate())) CivDiplomacy.Hail(c, map);
                }
                else
                {
                    GUI.color = Color.gray;
                    l.Label("Rimverse_CivHailBlocked".Translate(trade.Reason));
                    GUI.color = Color.white;
                }
                foreach (PlayerProposal p in System.Enum.GetValues(typeof(PlayerProposal)))
                {
                    if (!CivDiplomacy.Available(c, p)) continue;
                    string label = ("Rimverse_Proposal_" + p).Translate();
                    if (p != PlayerProposal.DeclareWar) label += " (" + "Rimverse_ProposalNeeds".Translate(CivDiplomacy.Threshold(p).ToString("0")) + ")";
                    if (l.ButtonText(label))
                    {
                        if (p == PlayerProposal.DeclareWar)
                        {
                            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("Rimverse_ConfirmWar".Translate(c.Name), () => CivDiplomacy.Propose(c, p, map), true));
                        }
                        else CivDiplomacy.Propose(c, p, map);
                    }
                }
            }
            else
            {
                l.Label("Rimverse_CivNoTalk".Translate(c.Name));
            }
            l.GapLine();
            List<CivRelation> wars = c.relations.Where(r => r.state == DiploState.War).ToList();
            if (wars.Count > 0)
            {
                l.Label("Rimverse_CivWars".Translate(string.Join(", ", wars.Select(r => gc.Civ(r.other)).Where(o => o != null && o.known).Select(o => o.Name))));
            }
            l.End();
            infoHeight = l.CurHeight + 20f;
            Widgets.EndScrollView();
        }

        private static string PopText(float points)
        {
            if (points >= 1000f) return (points / 1000f).ToString("0.#") + " bi";
            if (points >= 1f) return points.ToString("0.#") + " mi";
            return (points * 1000f).ToString("0") + " mil";
        }

        private static void PolicyLine(Listing_Standard l, string groupKey, PolicyDef p)
        {
            if (p == null) return;
            Rect r = l.GetRect(Text.LineHeight);
            Widgets.Label(r, groupKey.Translate() + ": " + p.LabelCap);
            if (Mouse.IsOver(r))
            {
                Widgets.DrawHighlight(r);
                string tip = p.description.NullOrEmpty() ? "" : p.description + "\n\n";
                tip += string.Join("\n", p.EffectLines());
                if (p.succession != Succession.None) tip += "\n" + ("Rimverse_Succession_" + p.succession).Translate();
                if (p.elections) tip += "\n" + "Rimverse_HasElections".Translate();
                TooltipHandler.TipRegion(r, tip);
            }
        }
    }
}
