using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public class Dialog_ColonyManage : Window
    {
        private readonly PlayerColony colony;
        private Vector2 scroll;
        private float height = 900f;

        public override Vector2 InitialSize => new Vector2(720f, 640f);

        public Dialog_ColonyManage(PlayerColony colony)
        {
            this.colony = colony;
            doCloseX = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
        }

        public static IEnumerable<Pawn> GovernorCandidates(PlayerColony c)
        {
            foreach (Pawn p in c.settlers) if (p != null && !p.Dead) yield return p;
            foreach (Pawn p in PawnsFinder.AllMaps_FreeColonists) if (p.skills != null) yield return p;
        }

        public override void DoWindowContents(Rect inRect)
        {
            GameComponent_PlayerColonies pc = GameComponent_PlayerColonies.Instance;
            GameComponent_PlayerEmpire e = GameComponent_PlayerEmpire.Instance;
            if (pc == null || e == null || !pc.colonies.Contains(colony))
            {
                Close();
                return;
            }
            Rect view = new Rect(0f, 0f, inRect.width - 16f, height);
            Widgets.BeginScrollView(inRect, ref scroll, view);
            Listing_Standard l = new Listing_Standard();
            l.Begin(view);
            Text.Font = GameFont.Medium;
            l.Label(colony.name + " · " + colony.TierLabel);
            Text.Font = GameFont.Small;
            l.Label("Rimverse_ManageLine".Translate(colony.population.ToString("0"), colony.stability.ToString("0"), colony.lastIncome.ToString("N0"), colony.strength.ToString("0"), pc.treasury.ToString("N0")));
            if (colony.captured > 0)
            {
                GUI.color = new Color(1f, 0.8f, 0.4f);
                l.Label("Rimverse_ManageCaptured".Translate());
                GUI.color = Color.white;
            }
            l.GapLine();
            Rect tr = l.GetRect(28f);
            Widgets.Label(tr.LeftPart(0.35f), "Rimverse_ManageTithe".Translate());
            if (Widgets.ButtonText(tr.RightPart(0.65f), ("Rimverse_Tithe_" + colony.tithe).Translate()))
            {
                List<FloatMenuOption> opts = new List<FloatMenuOption>();
                foreach (TitheMode m in System.Enum.GetValues(typeof(TitheMode)))
                {
                    TitheMode mode = m;
                    opts.Add(new FloatMenuOption(("Rimverse_Tithe_" + m).Translate() + " — " + ("Rimverse_TitheDesc_" + m).Translate(), () => colony.tithe = mode));
                }
                Find.WindowStack.Add(new FloatMenu(opts));
            }
            l.Gap(4f);
            Pawn gov = e.GovernorOf(colony.systemKey);
            Rect gr = l.GetRect(28f);
            Widgets.Label(gr.LeftPart(0.35f), "Rimverse_ManageGovernor".Translate());
            if (Widgets.ButtonText(gr.RightPart(0.65f), gov != null ? gov.LabelShortCap : "Rimverse_None".Translate().ToString()))
            {
                List<FloatMenuOption> opts = new List<FloatMenuOption> { new FloatMenuOption("Rimverse_None".Translate(), () => e.SetGovernor(colony.systemKey, null)) };
                foreach (Pawn p in GovernorCandidates(colony).Distinct())
                {
                    Pawn pawn = p;
                    opts.Add(new FloatMenuOption(p.LabelShortCap + " (" + "Rimverse_GovernorSkills".Translate(p.skills.GetSkill(SkillDefOf.Social).Level, p.skills.GetSkill(SkillDefOf.Intellectual).Level, p.skills.GetSkill(SkillDefOf.Shooting).Level) + ")", () => e.SetGovernor(colony.systemKey, pawn)));
                }
                Find.WindowStack.Add(new FloatMenu(opts));
            }
            l.GapLine();
            l.Label("Rimverse_ManageEffects".Translate());
            EmpireEffects ef = e.ColonyEffects(colony);
            List<string> lines = ef.Lines().ToList();
            l.Label(lines.Count == 0 ? "-" : "  " + string.Join("\n  ", lines));
            l.GapLine();
            Text.Font = GameFont.Medium;
            l.Label("Rimverse_ManageFacilities".Translate());
            Text.Font = GameFont.Small;
            foreach (ColonyFacility f in colony.facilities.Where(f => f.def != null))
            {
                string state = f.Ready ? "Rimverse_IndustryReady".Translate().ToString() : "Rimverse_IndustryBuilding".Translate(RimverseUnits.DaysFromTicks(f.readyTick - Find.TickManager.TicksGame).ToString("0")).ToString();
                Rect r = l.GetRect(24f);
                Widgets.Label(r, "  " + f.def.LabelCap + " · " + state);
                TooltipHandler.TipRegion(r, f.def.description + "\n\n" + string.Join("\n", f.def.effects.Lines()));
            }
            l.Gap(6f);
            foreach (ColonyFacilityDef d in DefDatabase<ColonyFacilityDef>.AllDefsListForReading.OrderBy(d => d.order))
            {
                if (d.surfaceOnly && !colony.surface || d.spaceOnly && colony.surface) continue;
                AcceptanceReport ok = pc.CanBuildFacility(colony, d);
                Rect r = l.GetRect(28f);
                if (!ok.Accepted) GUI.color = Color.gray;
                if (Widgets.ButtonText(r, "Rimverse_BuildFacility".Translate(d.LabelCap, d.costSilver.ToString("N0"), d.days, d.upkeep.ToString("0"))))
                {
                    if (ok.Accepted) pc.TryBuildFacility(colony, d);
                    else if (!ok.Reason.NullOrEmpty()) Messages.Message(ok.Reason, MessageTypeDefOf.RejectInput, false);
                }
                GUI.color = Color.white;
                TooltipHandler.TipRegion(r, d.description + "\n\n" + string.Join("\n", d.effects.Lines()) + (d.output.Count > 0 ? "\n" + "Rimverse_FacilityOutput".Translate(string.Join(", ", d.output.Where(o => o.thingDef != null).Select(o => o.thingDef.label + " ×" + o.count))) : "") + (ok.Accepted ? "" : "\n\n" + ok.Reason));
                l.Gap(2f);
            }
            height = l.CurHeight + 20f;
            l.End();
            Widgets.EndScrollView();
        }
    }
}
