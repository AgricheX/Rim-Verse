using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public class MainTabWindow_Empire : MainTabWindow
    {
        private enum Tab
        {
            Government,
            Edicts,
            Fleets,
            Diplomacy,
            Council,
            Anomalies
        }

        private Tab tab;
        private Vector2 scroll;
        private Vector2 scroll2;
        private float height = 1000f;
        private int selectedFleet = -1;
        private PolicyDef pGov;
        private PolicyDef pIde;
        private PolicyDef pEco;
        private PolicyDef pXen;
        private List<PolicyDef> pCiv;

        public override Vector2 RequestedTabSize => new Vector2(1010f, 680f);

        private static GameComponent_PlayerEmpire E => GameComponent_PlayerEmpire.Instance;

        private static GameComponent_Civilizations GC => GameComponent_Civilizations.Instance;

        private static GameComponent_PlayerColonies PC => GameComponent_PlayerColonies.Instance;

        public override void PreOpen()
        {
            base.PreOpen();
            GameComponent_PlayerEmpire e = E;
            if (e == null) return;
            pGov = e.government;
            pIde = e.ideology;
            pEco = e.economy;
            pXen = e.xeno;
            pCiv = e.civics.ToList();
        }

        public override void DoWindowContents(Rect inRect)
        {
            GameComponent_PlayerEmpire e = E;
            if (e == null || PC == null) return;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 34f), "Rimverse_EmpireTitle".Translate(e.influence.ToString("0"), GameComponent_PlayerEmpire.InfluenceCap.ToString("0"), e.InfluencePerDay.ToString("0.00"), PC.treasury.ToString("N0")));
            Text.Font = GameFont.Small;
            List<TabRecord> tabs = new List<TabRecord>();
            foreach (Tab t in System.Enum.GetValues(typeof(Tab)))
            {
                Tab tt = t;
                tabs.Add(new TabRecord(("Rimverse_EmpireTab_" + t).Translate(), () => { tab = tt; scroll = Vector2.zero; }, tab == t));
            }
            Rect body = new Rect(inRect.x, inRect.y + 70f, inRect.width, inRect.height - 70f);
            Widgets.DrawMenuSection(body);
            TabDrawer.DrawTabs(body, tabs);
            Rect inner = body.ContractedBy(10f);
            switch (tab)
            {
                case Tab.Government: Scrolled(inner, l => DrawGovernment(l, e)); break;
                case Tab.Edicts: Scrolled(inner, l => DrawEdicts(l, e)); break;
                case Tab.Fleets: DrawFleets(inner, e); break;
                case Tab.Diplomacy: Scrolled(inner, l => DrawDiplomacy(l, e)); break;
                case Tab.Council: Scrolled(inner, l => DrawCouncil(l, e)); break;
                default: Scrolled(inner, l => DrawAnomalies(l, e)); break;
            }
        }

        private void Scrolled(Rect rect, System.Action<Listing_Standard> draw)
        {
            Rect view = new Rect(0f, 0f, rect.width - 16f, height);
            Widgets.BeginScrollView(rect, ref scroll, view);
            Listing_Standard l = new Listing_Standard();
            l.Begin(view);
            draw(l);
            height = Mathf.Max(rect.height, l.CurHeight + 20f);
            l.End();
            Widgets.EndScrollView();
        }

        private static void Header(Listing_Standard l, string text)
        {
            Text.Font = GameFont.Medium;
            l.Label(text);
            Text.Font = GameFont.Small;
        }

        private static void Gray(Listing_Standard l, string text)
        {
            GUI.color = Color.gray;
            l.Label(text);
            GUI.color = Color.white;
        }

        private static IEnumerable<PolicyDef> Choices(PolicyGroup g) => DefDatabase<PolicyDef>.AllDefsListForReading.Where(p => p.group == g && !p.loreOnly);

        private void PolicyRow(Listing_Standard l, string label, PolicyDef cur, PolicyGroup g, System.Action<PolicyDef> set)
        {
            Rect r = l.GetRect(28f);
            Widgets.Label(r.LeftPart(0.3f), label);
            if (Widgets.ButtonText(r.RightPart(0.7f), cur?.LabelCap ?? "Rimverse_None".Translate().ToString()))
            {
                List<FloatMenuOption> opts = Choices(g).Select(p => new FloatMenuOption(p.LabelCap, () => set(p))).ToList();
                if (opts.Count > 0) Find.WindowStack.Add(new FloatMenu(opts));
            }
            if (cur != null) TooltipHandler.TipRegion(r, cur.description);
            l.Gap(2f);
        }

        private void DrawGovernment(Listing_Standard l, GameComponent_PlayerEmpire e)
        {
            Header(l, "Rimverse_EmpireGovHeader".Translate());
            Gray(l, e.governmentChosen ? "Rimverse_EmpireReformInfo".Translate(GameComponent_PlayerEmpire.ReformCost.ToString("0")) : "Rimverse_EmpireFirstGov".Translate());
            PolicyRow(l, "Rimverse_PolicyGroup_Government".Translate(), pGov, PolicyGroup.Government, p => pGov = p);
            PolicyRow(l, "Rimverse_PolicyGroup_Ideology".Translate(), pIde, PolicyGroup.Ideology, p => pIde = p);
            PolicyRow(l, "Rimverse_PolicyGroup_Economy".Translate(), pEco, PolicyGroup.Economy, p => pEco = p);
            PolicyRow(l, "Rimverse_PolicyGroup_Xeno".Translate(), pXen, PolicyGroup.Xeno, p => pXen = p);
            for (int i = 0; i < e.MaxCivics; i++)
            {
                int idx = i;
                PolicyDef cur = idx < pCiv.Count ? pCiv[idx] : null;
                PolicyRow(l, "Rimverse_EmpireCivicSlot".Translate(i + 1), cur, PolicyGroup.Civic, p =>
                {
                    if (pCiv.Contains(p)) return;
                    while (pCiv.Count <= idx) pCiv.Add(null);
                    pCiv[idx] = p;
                });
            }
            bool changed = pGov != e.government || pIde != e.ideology || pEco != e.economy || pXen != e.xeno || !pCiv.Where(c => c != null).SequenceEqual(e.civics);
            bool can = !e.governmentChosen || e.CanReform;
            if (changed)
            {
                if (!can) GUI.color = Color.gray;
                if (l.ButtonText("Rimverse_EmpireApplyGov".Translate()) && can) e.ApplyGovernment(pGov, pIde, pEco, pXen, pCiv);
                GUI.color = Color.white;
            }
            l.GapLine();
            Header(l, "Rimverse_EmpireEffectsHeader".Translate());
            List<string> lines = e.GlobalEffects().Lines().ToList();
            l.Label(lines.Count == 0 ? "-" : string.Join("\n", lines));
            l.GapLine();
            Header(l, "Rimverse_EmpireSectorsHeader".Translate());
            foreach (IGrouping<string, PlayerColony> g in PC.colonies.GroupBy(c => c.systemKey))
            {
                Pawn gov = e.GovernorOf(g.Key);
                Rect r = l.GetRect(26f);
                Widgets.Label(r.LeftPart(0.6f), EmpireFleetLogic.SystemName(g.Key) + " · " + "Rimverse_EmpireSectorColonies".Translate(g.Count(), g.Sum(c => c.population).ToString("0")));
                if (Widgets.ButtonText(r.RightPart(0.38f), gov != null ? gov.LabelShortCap : "Rimverse_EmpireAssignGovernor".Translate().ToString()))
                {
                    string key = g.Key;
                    List<FloatMenuOption> opts = new List<FloatMenuOption> { new FloatMenuOption("Rimverse_None".Translate(), () => e.SetGovernor(key, null)) };
                    foreach (Pawn p in g.SelectMany(Dialog_ColonyManage.GovernorCandidates).Distinct())
                    {
                        Pawn pawn = p;
                        opts.Add(new FloatMenuOption(p.LabelShortCap, () => e.SetGovernor(key, pawn)));
                    }
                    Find.WindowStack.Add(new FloatMenu(opts));
                }
            }
            if (PC.colonies.Count == 0) Gray(l, "Rimverse_ColoniesNone".Translate());
        }

        private void DrawEdicts(Listing_Standard l, GameComponent_PlayerEmpire e)
        {
            Header(l, "Rimverse_EmpireEdictsHeader".Translate());
            int now = Find.TickManager.TicksGame;
            foreach (EdictDef d in DefDatabase<EdictDef>.AllDefsListForReading.OrderBy(d => d.order))
            {
                ActiveEdict a = e.edicts.FirstOrDefault(x => x.def == d);
                Rect r = l.GetRect(30f);
                string label = d.LabelCap + " · " + "Rimverse_EdictCost".Translate(d.costSilver.ToString("N0"), d.costInfluence.ToString("0"), d.durationDays.ToString("0"));
                if (a != null)
                {
                    Widgets.DrawHighlightSelected(r);
                    Widgets.Label(r.ContractedBy(4f, 0f), label + " · " + "Rimverse_EdictActive".Translate(RimverseUnits.DaysFromTicks(a.endTick - now).ToString("0")));
                }
                else
                {
                    bool locked = d.research != null && !d.research.IsFinished;
                    bool ok = !locked && PC.treasury >= d.costSilver && e.influence >= d.costInfluence;
                    if (!ok) GUI.color = Color.gray;
                    if (Widgets.ButtonText(r, label) && ok) e.TryEdict(d);
                    GUI.color = Color.white;
                }
                TooltipHandler.TipRegion(r, d.description + "\n\n" + string.Join("\n", d.effects.Lines()) + (d.research != null ? "\n\n" + "Rimverse_NeedsResearch".Translate(d.research.LabelCap) : ""));
                l.Gap(3f);
            }
        }

        private static IEnumerable<string> Destinations()
        {
            HashSet<string> seen = new HashSet<string>();
            foreach (PlayerColony c in PC.colonies) if (seen.Add(c.systemKey)) yield return c.systemKey;
            foreach (Map m in Find.Maps)
            {
                MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(m);
                if (loc != null && !loc.Traveling && seen.Add(loc.SystemId.Key)) yield return loc.SystemId.Key;
            }
            GameComponent_Civilizations gc = GC;
            if (gc == null) yield break;
            foreach (Civilization c in gc.Known)
            {
                if (!c.Reveals(CivKnowledge.Contact)) continue;
                if (!c.capitalKey.NullOrEmpty() && c.Reveals(CivKnowledge.Known) && seen.Add(c.capitalKey)) yield return c.capitalKey;
                foreach (CivPlanet p in c.planets) if (seen.Add(p.systemKey)) yield return p.systemKey;
            }
        }

        private static string DestLabel(string key)
        {
            string s = EmpireFleetLogic.SystemName(key);
            Civilization owner = GC?.OwnerOf(StarSystemId.FromKey(key));
            if (owner != null) s += " (" + owner.ShownName + ")";
            return s;
        }

        private void DrawFleets(Rect rect, GameComponent_PlayerEmpire e)
        {
            Rect left = rect.LeftPart(0.32f);
            Rect right = rect.RightPart(0.66f);
            Rect btn = new Rect(left.x, left.y, left.width, 30f);
            List<string> yards = EmpireFleetLogic.ShipyardSystems().ToList();
            if (yards.Count == 0) GUI.color = Color.gray;
            if (Widgets.ButtonText(btn, "Rimverse_FleetNew".Translate()))
            {
                if (yards.Count == 0) Messages.Message("Rimverse_FleetNoShipyard".Translate(), MessageTypeDefOf.RejectInput, false);
                else Find.WindowStack.Add(new FloatMenu(yards.Select(k => new FloatMenuOption(EmpireFleetLogic.SystemName(k), () => selectedFleet = EmpireFleetLogic.CreateFleet(e, k).id)).ToList()));
            }
            GUI.color = Color.white;
            Rect list = new Rect(left.x, left.y + 36f, left.width, left.height - 36f);
            Widgets.DrawMenuSection(list);
            Rect view = new Rect(0f, 0f, list.width - 16f, e.fleets.Count * 48f);
            Widgets.BeginScrollView(list.ContractedBy(2f), ref scroll2, view);
            float y = 0f;
            foreach (PlayerFleet f in e.fleets)
            {
                Rect row = new Rect(0f, y, view.width, 46f);
                if (f.id == selectedFleet) Widgets.DrawHighlightSelected(row);
                else if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);
                Widgets.Label(new Rect(6f, y + 2f, row.width - 8f, 22f), f.name);
                GUI.color = Color.gray;
                Text.Font = GameFont.Tiny;
                string where = f.Moving ? "Rimverse_FleetEnRoute".Translate(EmpireFleetLogic.SystemName(f.destKey), RimverseUnits.DaysFromTicks(f.arriveTick - Find.TickManager.TicksGame).ToString("0.#")).ToString() : EmpireFleetLogic.SystemName(f.systemKey);
                Widgets.Label(new Rect(6f, y + 24f, row.width - 8f, 20f), where + " · " + "Rimverse_FleetPowerShort".Translate(f.Power.ToString("0")));
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
                if (Widgets.ButtonInvisible(row)) selectedFleet = f.id;
                y += 48f;
            }
            Widgets.EndScrollView();
            PlayerFleet sel = e.fleets.FirstOrDefault(f => f.id == selectedFleet);
            if (sel == null)
            {
                Widgets.Label(right, e.fleets.Count == 0 ? "Rimverse_FleetNone".Translate() : "Rimverse_FleetSelect".Translate());
                return;
            }
            Scrolled(right, l => DrawFleet(l, e, sel));
        }

        private void DrawFleet(Listing_Standard l, GameComponent_PlayerEmpire e, PlayerFleet f)
        {
            Header(l, f.name);
            l.Label("Rimverse_FleetLine".Translate(f.Power.ToString("0"), f.Ground.ToString("0"), f.Speed.ToString("0.#"), f.condition.ToStringPercent(), f.Upkeep.ToString("0"), ("Rimverse_FleetMission_" + f.mission).Translate()));
            l.Label("Rimverse_FleetLocation".Translate(f.Moving ? "Rimverse_FleetEnRoute".Translate(EmpireFleetLogic.SystemName(f.destKey), RimverseUnits.DaysFromTicks(f.arriveTick - Find.TickManager.TicksGame).ToString("0.#")).ToString() : EmpireFleetLogic.SystemName(f.systemKey)));
            l.GapLine();
            foreach (KeyValuePair<FleetHullDef, int> kv in f.Units) l.Label("  " + kv.Key.LabelCap + " ×" + kv.Value);
            foreach (FleetOrder o in f.orders) Gray(l, "  " + "Rimverse_FleetOrder".Translate(o.def?.LabelCap ?? "?", o.count, RimverseUnits.DaysFromTicks(o.readyTick - Find.TickManager.TicksGame).ToString("0")));
            l.GapLine();
            Rect r = l.GetRect(30f);
            if (Widgets.ButtonText(r.LeftHalf().ContractedBy(2f, 0f), "Rimverse_FleetBuild".Translate()))
            {
                List<FloatMenuOption> opts = new List<FloatMenuOption>();
                foreach (FleetHullDef d in DefDatabase<FleetHullDef>.AllDefsListForReading.OrderBy(d => d.order))
                {
                    FleetHullDef def = d;
                    foreach (int n in new[] { 1, 5 })
                    {
                        int count = n;
                        AcceptanceReport ok = EmpireFleetLogic.CanBuild(f, d, n);
                        string label = "Rimverse_FleetBuildOption".Translate(d.LabelCap, n, (d.costSilver * n).ToString("N0"), d.power.ToString("0"), d.ground.ToString("0"));
                        opts.Add(ok.Accepted ? new FloatMenuOption(label, () => EmpireFleetLogic.BuildUnits(f, def, count)) : new FloatMenuOption(label + " (" + ok.Reason + ")", null));
                    }
                }
                if (opts.Count > 0) Find.WindowStack.Add(new FloatMenu(opts));
            }
            if (Widgets.ButtonText(r.RightHalf().ContractedBy(2f, 0f), "Rimverse_FleetSend".Translate()) && !f.Moving && !f.Empty)
            {
                List<FloatMenuOption> opts = new List<FloatMenuOption>();
                foreach (string k in Destinations())
                {
                    string key = k;
                    float days = key == f.systemKey ? 0f : EmpireFleetLogic.TravelDays(f, key);
                    if (days < 0f) continue;
                    opts.Add(new FloatMenuOption(DestLabel(key) + " · " + "Rimverse_DaysShort".Translate(days.ToString("0.#")), () => ChooseMission(f, key)));
                }
                if (opts.Count > 0) Find.WindowStack.Add(new FloatMenu(opts));
            }
            l.Gap(4f);
            Rect r2 = l.GetRect(30f);
            if (Widgets.ButtonText(r2.LeftHalf().ContractedBy(2f, 0f), "Rimverse_FleetRename".Translate())) Find.WindowStack.Add(new Dialog_RenameFleet(f));
            if (Widgets.ButtonText(r2.RightHalf().ContractedBy(2f, 0f), "Rimverse_FleetDisband".Translate()))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("Rimverse_FleetDisbandConfirm".Translate(f.name), () =>
                {
                    e.fleets.Remove(f);
                    selectedFleet = -1;
                }, true));
            }
        }

        private static void ChooseMission(PlayerFleet f, string key)
        {
            List<FloatMenuOption> opts = new List<FloatMenuOption>();
            foreach (PlayerFleetMission m in System.Enum.GetValues(typeof(PlayerFleetMission)))
            {
                PlayerFleetMission mission = m;
                opts.Add(new FloatMenuOption(("Rimverse_FleetMission_" + m).Translate() + " — " + ("Rimverse_FleetMissionDesc_" + m).Translate(), () =>
                {
                    Civilization owner = GC?.OwnerOf(StarSystemId.FromKey(key));
                    EmpireFleetLogic.SendFleet(f, key, mission, owner?.id ?? -1);
                }));
            }
            Find.WindowStack.Add(new FloatMenu(opts));
        }

        private void DrawDiplomacy(Listing_Standard l, GameComponent_PlayerEmpire e)
        {
            GameComponent_Civilizations gc = GC;
            if (gc == null) return;
            Header(l, "Rimverse_EmpireWarsHeader".Translate());
            if (e.wars.Count == 0) Gray(l, "Rimverse_EmpireNoWars".Translate());
            foreach (WarData w in e.wars.ToList())
            {
                Civilization c = gc.Civ(w.civId);
                if (c == null) continue;
                l.Label("Rimverse_WarLine".Translate(c.ShownName, ("Rimverse_WarGoal_" + w.goal).Translate(), w.score.ToString("+0;-0"), EmpireWar.GoalScore(w.goal).ToString("0"), w.exhaustion.ToString("0")));
                Rect r = l.GetRect(28f);
                if (Widgets.ButtonText(r.LeftHalf().ContractedBy(2f, 0f), "Rimverse_PeaceDemand".Translate(EmpireWar.AcceptChance(e, c, w).ToStringPercent()))) EmpireWar.ProposePeace(e, c, true);
                if (Widgets.ButtonText(r.RightHalf().ContractedBy(2f, 0f), "Rimverse_PeaceWhite".Translate())) EmpireWar.ProposePeace(e, c, false);
                l.Gap(4f);
            }
            l.GapLine();
            Header(l, "Rimverse_EmpireFederationHeader".Translate());
            if (e.federationName.NullOrEmpty()) Gray(l, "Rimverse_FederationNone".Translate());
            else l.Label("Rimverse_FederationLine".Translate(e.federationName, string.Join(", ", e.federation.Select(id => gc.Civ(id)?.ShownName ?? "?"))));
            l.GapLine();
            Header(l, "Rimverse_EmpireCivsHeader".Translate());
            foreach (Civilization c in gc.Known.Where(c => !c.NoDiplomacy).OrderBy(c => gc.DistanceToPlayer(c, out _)))
            {
                Civilization civ = c;
                Rect head = l.GetRect(24f);
                Widgets.DrawBoxSolid(new Rect(head.x, head.y + 4f, 6f, 16f), c.color);
                Widgets.Label(new Rect(head.x + 12f, head.y, head.width - 12f, 24f), c.ShownName + " · " + ("Rimverse_Diplo_" + c.playerState).Translate() + " · " + "Rimverse_OpinionShort".Translate(c.playerOpinion.ToString("0")) + (e.claims.Any(x => x.civId == c.id) ? " · " + "Rimverse_ClaimsShort".Translate(e.claims.Count(x => x.civId == c.id)).ToString() : "") + (GalacticCouncil.Sanctioned(c) ? " · " + "Rimverse_Sanctioned".Translate().ToString() : ""));
                Rect r = l.GetRect(28f);
                float w4 = r.width / 4f;
                if (Widgets.ButtonText(new Rect(r.x, r.y, w4 - 4f, r.height), "Rimverse_ClaimButton".Translate()))
                {
                    List<FloatMenuOption> opts = new List<FloatMenuOption>();
                    foreach (CivPlanet p in c.planets)
                    {
                        CivPlanet planet = p;
                        AcceptanceReport ok = EmpireWar.CanClaim(e, civ, p);
                        string label = EmpireFleetLogic.SystemName(p.systemKey) + " #" + p.body;
                        opts.Add(ok.Accepted ? new FloatMenuOption(label + " (" + GameComponent_PlayerEmpire.ClaimCost.ToString("0") + ")", () => EmpireWar.Claim(e, civ, planet)) : new FloatMenuOption(label + (ok.Reason.NullOrEmpty() ? "" : " (" + ok.Reason + ")"), null));
                    }
                    if (opts.Count > 0) Find.WindowStack.Add(new FloatMenu(opts));
                }
                if (c.playerState != DiploState.War && Widgets.ButtonText(new Rect(r.x + w4, r.y, w4 - 4f, r.height), "Rimverse_WarButton".Translate()))
                {
                    List<FloatMenuOption> opts = new List<FloatMenuOption>();
                    foreach (WarGoal g in System.Enum.GetValues(typeof(WarGoal)))
                    {
                        WarGoal goal = g;
                        AcceptanceReport ok = EmpireWar.CanDeclare(e, civ, g);
                        string label = ("Rimverse_WarGoal_" + g).Translate() + " — " + ("Rimverse_WarGoalDesc_" + g).Translate();
                        opts.Add(ok.Accepted ? new FloatMenuOption(label, () => Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("Rimverse_WarConfirm".Translate(civ.ShownName), () => EmpireWar.Declare(e, civ, goal), true))) : new FloatMenuOption(label + (ok.Reason.NullOrEmpty() ? "" : " (" + ok.Reason + ")"), null));
                    }
                    Find.WindowStack.Add(new FloatMenu(opts));
                }
                if (Widgets.ButtonText(new Rect(r.x + w4 * 2f, r.y, w4 - 4f, r.height), "Rimverse_RequestButton".Translate()))
                {
                    List<FloatMenuOption> opts = new List<FloatMenuOption>();
                    foreach (AllyRequest q in System.Enum.GetValues(typeof(AllyRequest)))
                    {
                        AllyRequest req = q;
                        AcceptanceReport ok = EmpireDiplomacy.CanRequest(e, civ, q);
                        string label = ("Rimverse_Request_" + q).Translate(EmpireDiplomacy.RequestInfluence(q).ToString("0"), EmpireDiplomacy.RequestOpinion(q).ToString("0"));
                        opts.Add(ok.Accepted ? new FloatMenuOption(label, () => EmpireDiplomacy.Request(e, civ, req, Find.CurrentMap)) : new FloatMenuOption(label + (ok.Reason.NullOrEmpty() ? "" : " (" + ok.Reason + ")"), null));
                    }
                    Find.WindowStack.Add(new FloatMenu(opts));
                }
                Rect fed = new Rect(r.x + w4 * 3f, r.y, w4 - 4f, r.height);
                if (c.playerState == DiploState.Vassal)
                {
                    if (Widgets.ButtonText(fed, "Rimverse_VassalRelease".Translate())) EmpireDiplomacy.ReleaseVassal(e, civ);
                }
                else if (e.InFederation(c))
                {
                    if (Widgets.ButtonText(fed, "Rimverse_FederationLeave".Translate())) EmpireDiplomacy.Leave(e, civ);
                }
                else if (e.federationName.NullOrEmpty())
                {
                    if (Widgets.ButtonText(fed, "Rimverse_FederationFound".Translate()))
                    {
                        AcceptanceReport ok = EmpireDiplomacy.CanFoundFederation(e, civ);
                        if (ok.Accepted) Find.WindowStack.Add(new Dialog_NameFederation(civ));
                        else if (!ok.Reason.NullOrEmpty()) Messages.Message(ok.Reason, MessageTypeDefOf.RejectInput, false);
                    }
                }
                else if (Widgets.ButtonText(fed, "Rimverse_FederationInvite".Translate())) EmpireDiplomacy.Invite(e, civ);
                l.Gap(6f);
            }
        }

        private void DrawCouncil(Listing_Standard l, GameComponent_PlayerEmpire e)
        {
            GameComponent_Civilizations gc = GC;
            Header(l, "Rimverse_EmpireCouncilHeader".Translate());
            if (!e.CouncilExists)
            {
                Gray(l, "Rimverse_CouncilNone".Translate());
                return;
            }
            Gray(l, "Rimverse_CouncilInfo".Translate(GalacticCouncil.Members(gc).Count(), GalacticCouncil.PlayerWeight(e).ToString("0.#")));
            if (e.proposal != null)
            {
                GalacticCouncil.Tally(e, out float yes, out float no);
                l.Label("Rimverse_CouncilProposal".Translate(GalacticCouncil.Describe(e.proposal, e.proposalTarget), RimverseUnits.DaysFromTicks(e.councilProposalTick - Find.TickManager.TicksGame).ToString("0")));
                Gray(l, e.proposal.description + "\n" + string.Join("\n", e.proposal.effects.Lines()));
                l.Label("Rimverse_CouncilTally".Translate(yes.ToString("0"), no.ToString("0"), e.playerVote.ToString("+0;-0;0")));
                Rect r = l.GetRect(30f);
                if (Widgets.ButtonText(r.LeftHalf().ContractedBy(2f, 0f), "Rimverse_CouncilVoteYes".Translate(GalacticCouncil.LobbyCost.ToString("0")))) GalacticCouncil.Lobby(e, 1);
                if (Widgets.ButtonText(r.RightHalf().ContractedBy(2f, 0f), "Rimverse_CouncilVoteNo".Translate(GalacticCouncil.LobbyCost.ToString("0")))) GalacticCouncil.Lobby(e, -1);
            }
            else if (l.ButtonText("Rimverse_CouncilPropose".Translate()))
            {
                List<FloatMenuOption> opts = new List<FloatMenuOption>();
                foreach (ResolutionDef d in DefDatabase<ResolutionDef>.AllDefsListForReading)
                {
                    ResolutionDef def = d;
                    AcceptanceReport ok = GalacticCouncil.CanPlayerPropose(e, d);
                    if (!ok.Accepted)
                    {
                        opts.Add(new FloatMenuOption(d.LabelCap + (ok.Reason.NullOrEmpty() ? "" : " (" + ok.Reason + ")"), null));
                        continue;
                    }
                    if (!d.targetsCiv) opts.Add(new FloatMenuOption(d.LabelCap, () => GalacticCouncil.PlayerPropose(e, def, -1)));
                    else opts.Add(new FloatMenuOption(d.LabelCap + "...", () => Find.WindowStack.Add(new FloatMenu(gc.Known.Where(c => !c.NoDiplomacy).Select(c => new FloatMenuOption(c.ShownName, () => GalacticCouncil.PlayerPropose(e, def, c.id))).ToList()))));
                }
                Find.WindowStack.Add(new FloatMenu(opts));
            }
            l.GapLine();
            Header(l, "Rimverse_CouncilActiveHeader".Translate());
            if (e.resolutions.Count == 0) Gray(l, "-");
            foreach (ActiveResolution r in e.resolutions)
            {
                Rect row = l.GetRect(24f);
                Widgets.Label(row, GalacticCouncil.Describe(r.def, r.targetCiv) + " · " + "Rimverse_YearsLeft".Translate((RimverseUnits.DaysFromTicks(r.endTick - Find.TickManager.TicksGame) / RimverseUnits.DaysPerYear).ToString("0.#")));
                TooltipHandler.TipRegion(row, r.def.description + "\n" + string.Join("\n", r.def.effects.Lines()));
            }
        }

        private void DrawAnomalies(Listing_Standard l, GameComponent_PlayerEmpire e)
        {
            Header(l, "Rimverse_EmpireAnomaliesHeader".Translate());
            Gray(l, "Rimverse_AnomalyInfo".Translate());
            if (e.anomalies.Count == 0) Gray(l, "Rimverse_AnomalyNone".Translate());
            foreach (FoundAnomaly a in e.anomalies.OrderBy(a => a.done))
            {
                Rect r = l.GetRect(30f);
                string state = a.done ? "Rimverse_AnomalyDone".Translate().ToString() : a.Working ? "Rimverse_AnomalyWorking".Translate(RimverseUnits.DaysFromTicks(a.workEndTick - Find.TickManager.TicksGame).ToString("0.#")).ToString() : "Rimverse_AnomalyChapterShort".Translate(a.chapter + 1, a.def.chapters).ToString();
                Widgets.Label(r.LeftPart(0.68f), a.def.LabelCap + " · " + EmpireFleetLogic.SystemName(a.systemKey) + " · " + state);
                TooltipHandler.TipRegion(r, a.def.description);
                if (!a.done && !a.Working)
                {
                    AcceptanceReport ok = EmpireAnomalies.CanStudy(a);
                    if (!ok.Accepted) GUI.color = Color.gray;
                    if (Widgets.ButtonText(r.RightPart(0.3f), "Rimverse_AnomalyStudy".Translate()))
                    {
                        if (ok.Accepted) EmpireAnomalies.Study(a);
                        else if (!ok.Reason.NullOrEmpty()) Messages.Message(ok.Reason, MessageTypeDefOf.RejectInput, false);
                    }
                    GUI.color = Color.white;
                }
                l.Gap(2f);
            }
        }
    }

    public class Dialog_RenameFleet : Dialog_Rename<PlayerFleetRenamable>
    {
        public Dialog_RenameFleet(PlayerFleet f) : base(new PlayerFleetRenamable(f))
        {
        }
    }

    public class PlayerFleetRenamable : IRenameable
    {
        private readonly PlayerFleet fleet;

        public PlayerFleetRenamable(PlayerFleet f)
        {
            fleet = f;
        }

        public string RenamableLabel
        {
            get => fleet.name;
            set => fleet.name = value;
        }

        public string BaseLabel => fleet.name;

        public string InspectLabel => fleet.name;
    }

    public class Dialog_NameFederation : Window
    {
        private readonly Civilization civ;
        private string name;

        public override Vector2 InitialSize => new Vector2(420f, 170f);

        public Dialog_NameFederation(Civilization civ)
        {
            this.civ = civ;
            name = "Rimverse_FederationDefaultName".Translate();
            doCloseX = true;
            absorbInputAroundWindow = true;
            forcePause = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 30f), "Rimverse_FederationNamePrompt".Translate());
            name = Widgets.TextField(new Rect(inRect.x, inRect.y + 36f, inRect.width, 32f), name);
            if (Widgets.ButtonText(new Rect(inRect.xMax - 160f, inRect.yMax - 36f, 160f, 34f), "OK".Translate()))
            {
                EmpireDiplomacy.FoundFederation(GameComponent_PlayerEmpire.Instance, civ, name);
                Close();
            }
        }
    }
}
