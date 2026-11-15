using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public static class ColonySituations
    {
        public static void Consider(PlayerColony c)
        {
            int now = Find.TickManager.TicksGame;
            if (c.nextSituationTick <= 0)
            {
                c.nextSituationTick = now + RimverseUnits.TicksFromDays(30f);
                return;
            }
            if (now < c.nextSituationTick) return;
            c.nextSituationTick = now + RimverseUnits.TicksFromDays(Rand.Range(25f, 50f));
            if (!Rand.Chance(0.6f)) return;
            float hazard = c.Hazard;
            List<ColonySituationDef> defs = DefDatabase<ColonySituationDef>.AllDefsListForReading.Where(d => c.stability >= d.minStability && c.stability <= d.maxStability && c.population >= d.minPopulation && hazard >= d.minHazard && (!d.surfaceOnly || c.surface) && (!d.spaceOnly || !c.surface)).ToList();
            if (defs.Count == 0) return;
            ColonySituationDef pick = defs.RandomElementByWeight(d => d.commonality);
            ChoiceLetter_ColonySituation letter = (ChoiceLetter_ColonySituation)LetterMaker.MakeLetter("Rimverse_SituationLabel".Translate(c.name, pick.LabelCap), "Rimverse_SituationText".Translate(c.name, pick.description), RimverseEmpireDefOf.Rimverse_ColonySituation);
            letter.colonyId = c.id;
            letter.def2 = pick;
            Find.LetterStack.ReceiveLetter(letter);
        }

        public static PlayerColony Colony(int id) => GameComponent_PlayerColonies.Instance?.colonies.FirstOrDefault(c => c.id == id);

        public static string OptionLabel(SituationOption o)
        {
            string s = ("Rimverse_SitOpt_" + o.key).Translate();
            List<string> parts = new List<string>();
            if (o.costSilver > 0) parts.Add("Rimverse_SitCostSilver".Translate(o.costSilver));
            if (o.costInfluence > 0f) parts.Add("Rimverse_SitCostInfluence".Translate(o.costInfluence.ToString("0")));
            if (o.successChance < 1f) parts.Add("Rimverse_SitChance".Translate(o.successChance.ToStringPercent()));
            if (parts.Count > 0) s += " (" + string.Join(", ", parts) + ")";
            return s;
        }

        public static AcceptanceReport CanPick(SituationOption o)
        {
            GameComponent_PlayerColonies pc = GameComponent_PlayerColonies.Instance;
            GameComponent_PlayerEmpire e = GameComponent_PlayerEmpire.Instance;
            if (o.costSilver > 0 && (pc == null || pc.treasury < o.costSilver)) return "Rimverse_NotEnoughTreasury".Translate(o.costSilver);
            if (o.costInfluence > 0f && (e == null || e.influence < o.costInfluence)) return "Rimverse_NotEnoughInfluence".Translate(o.costInfluence);
            return true;
        }

        public static void Apply(PlayerColony c, SituationOption o)
        {
            GameComponent_PlayerColonies pc = GameComponent_PlayerColonies.Instance;
            GameComponent_PlayerEmpire e = GameComponent_PlayerEmpire.Instance;
            if (c == null || pc == null) return;
            pc.treasury -= o.costSilver;
            if (e != null) e.influence = Mathf.Max(0f, e.influence - o.costInfluence);
            bool ok = Rand.Chance(o.successChance);
            float mult = ok ? 1f : -0.5f;
            c.stability = Mathf.Clamp(c.stability + o.stability * mult, 0f, 100f);
            c.population = Mathf.Max(1f, c.population * (1f + o.population * mult));
            c.attention = Mathf.Max(0f, c.attention + o.attention);
            if (ok)
            {
                pc.treasury += o.silver;
                if (e != null) e.influence = Mathf.Min(GameComponent_PlayerEmpire.InfluenceCap, e.influence + o.influence);
                if (!o.stockDef.NullOrEmpty() && o.stockCount > 0) c.AddStock(o.stockDef, o.stockCount);
            }
            Messages.Message((ok ? "Rimverse_SitResultOk" : "Rimverse_SitResultFail").Translate(c.name), ok ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NegativeEvent, false);
        }
    }

    public class ChoiceLetter_ColonySituation : ChoiceLetter
    {
        public int colonyId;
        public ColonySituationDef def2;

        public override bool CanDismissWithRightClick => false;

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                if (ArchivedOnly || def2 == null)
                {
                    yield return Option_Close;
                    yield break;
                }
                PlayerColony c = ColonySituations.Colony(colonyId);
                if (c == null)
                {
                    yield return Option_Close;
                    yield break;
                }
                foreach (SituationOption o in def2.options)
                {
                    SituationOption opt = o;
                    DiaOption d = new DiaOption(ColonySituations.OptionLabel(o));
                    AcceptanceReport r = ColonySituations.CanPick(o);
                    if (!r.Accepted) d.Disable(r.Reason);
                    d.action = () =>
                    {
                        ColonySituations.Apply(ColonySituations.Colony(colonyId), opt);
                        Find.LetterStack.RemoveLetter(this);
                    };
                    d.resolveTree = true;
                    yield return d;
                }
                yield return Option_Postpone;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref colonyId, "colonyId");
            Scribe_Defs.Look(ref def2, "situation");
        }
    }

    [DefOf]
    public static class RimverseEmpireDefOf
    {
        public static LetterDef Rimverse_ColonySituation;
        public static WorldObjectDef Rimverse_PlayerOutpost;

        static RimverseEmpireDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(RimverseEmpireDefOf));
        }
    }
}
