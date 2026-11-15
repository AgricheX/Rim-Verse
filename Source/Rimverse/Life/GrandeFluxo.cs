using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public enum FlowSide
    {
        None,
        Harmony,
        Rupture
    }

    public enum FlowMode
    {
        Push,
        Sense,
        Heal,
        Calm,
        Choke,
        Storm
    }

    public static class FlowUtility
    {
        private static HediffDef sensitive;
        private static HediffDef harmony;
        private static HediffDef rupture;

        public static HediffDef Sensitive => sensitive ?? (sensitive = DefDatabase<HediffDef>.GetNamedSilentFail("Rimverse_FlowSensitive"));

        public static HediffDef HarmonyDef => harmony ?? (harmony = DefDatabase<HediffDef>.GetNamedSilentFail("Rimverse_FlowHarmony"));

        public static HediffDef RuptureDef => rupture ?? (rupture = DefDatabase<HediffDef>.GetNamedSilentFail("Rimverse_FlowRupture"));

        public static bool Enabled => RimverseMod.Settings.fluxoEnabled && Sensitive != null;

        public static bool IsSensitive(Pawn p) => p?.health?.hediffSet != null && Sensitive != null && p.health.hediffSet.HasHediff(Sensitive);

        public static FlowSide Side(Pawn p)
        {
            if (p?.health?.hediffSet == null) return FlowSide.None;
            if (HarmonyDef != null && p.health.hediffSet.HasHediff(HarmonyDef)) return FlowSide.Harmony;
            if (RuptureDef != null && p.health.hediffSet.HasHediff(RuptureDef)) return FlowSide.Rupture;
            return FlowSide.None;
        }

        public static FlowSide PickSide(Pawn p)
        {
            FlowSide forced = FlowForceBridge.SideFromForce(p);
            if (forced != FlowSide.None) return forced;
            if (p.story?.traits != null)
            {
                if (p.story.traits.HasTrait(TraitDefOf.Kind)) return FlowSide.Harmony;
                if (p.story.traits.HasTrait(TraitDefOf.Psychopath) || p.story.traits.HasTrait(TraitDefOf.Bloodlust)) return FlowSide.Rupture;
            }
            return Rand.Chance(0.6f) ? FlowSide.Harmony : FlowSide.Rupture;
        }

        public static void Awaken(Pawn p, FlowSide side, bool letter)
        {
            if (!Enabled || p?.health == null || !p.RaceProps.Humanlike) return;
            if (!IsSensitive(p)) p.health.AddHediff(Sensitive);
            if (Side(p) == FlowSide.None)
            {
                if (side == FlowSide.None) side = PickSide(p);
                HediffDef d = side == FlowSide.Harmony ? HarmonyDef : RuptureDef;
                if (d != null) p.health.AddHediff(d);
            }
            if (letter && p.Faction == Faction.OfPlayer)
            {
                Find.LetterStack.ReceiveLetter("Rimverse_LetterFlowAwakenLabel".Translate(), "Rimverse_LetterFlowAwaken".Translate(p.Named("PAWN"), ("Rimverse_FlowSide_" + Side(p)).Translate()), LetterDefOf.PositiveEvent, p);
            }
        }

        public static void OnGenerated(Pawn p)
        {
            if (!Enabled || p == null || !p.RaceProps.Humanlike || IsSensitive(p)) return;
            if (FlowForceBridge.HasForceSensitivity(p) || Rand.Chance(RimverseMod.Settings.fluxoSensitiveChance)) Awaken(p, FlowSide.None, false);
        }

        public static float Power(Pawn caster)
        {
            float p = 1f;
            if (caster == null) return p;
            p += caster.GetStatValue(StatDefOf.PsychicSensitivity) - 1f;
            p += FlowForceBridge.ForceDegree(caster) * 0.15f;
            return Mathf.Clamp(p, 0.5f, 2.5f);
        }
    }

    public class CompProperties_FlowEffect : CompProperties_AbilityEffect
    {
        public FlowMode mode;
        public float amount = 10f;

        public CompProperties_FlowEffect()
        {
            compClass = typeof(CompAbilityEffect_Flow);
        }
    }

    public class CompAbilityEffect_Flow : CompAbilityEffect
    {
        public new CompProperties_FlowEffect Props => (CompProperties_FlowEffect)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            Map map = caster?.Map;
            if (map == null) return;
            float power = FlowUtility.Power(caster) * Props.amount;
            Pawn tp = target.Pawn;
            switch (Props.mode)
            {
                case FlowMode.Push:
                    if (tp == null) return;
                    IntVec3 dir = tp.Position - caster.Position;
                    IntVec3 step = new IntVec3(System.Math.Sign(dir.x), 0, System.Math.Sign(dir.z));
                    IntVec3 to = tp.Position;
                    for (int i = 0; i < Mathf.RoundToInt(power); i++)
                    {
                        IntVec3 n = to + step;
                        if (!n.InBounds(map) || !n.Standable(map)) break;
                        to = n;
                    }
                    if (to != tp.Position)
                    {
                        tp.Position = to;
                        tp.Notify_Teleported(true, false);
                    }
                    tp.stances?.stunner?.StunFor(90, caster, false);
                    FleckMaker.ThrowDustPuff(tp.DrawPos, map, 1.5f);
                    break;
                case FlowMode.Sense:
                    IntVec3 c = target.Cell;
                    foreach (IntVec3 cell in GenRadial.RadialCellsAround(c, Mathf.Min(40f, power), true)) if (cell.InBounds(map)) map.fogGrid.Unfog(cell);
                    FleckMaker.Static(c, map, FleckDefOf.PsycastAreaEffect, Mathf.Min(40f, power));
                    break;
                case FlowMode.Heal:
                    if (tp == null) return;
                    float left = power;
                    foreach (Hediff_Injury h in tp.health.hediffSet.hediffs.OfType<Hediff_Injury>().OrderByDescending(h => h.Severity).ToList())
                    {
                        if (left <= 0f) break;
                        float a = Mathf.Min(left, h.Severity);
                        h.Heal(a);
                        left -= a;
                    }
                    FleckMaker.ThrowMetaIcon(tp.Position, map, FleckDefOf.HealingCross);
                    break;
                case FlowMode.Calm:
                    if (tp == null) return;
                    if (tp.InMentalState) tp.MentalState.RecoverFromState();
                    tp.needs?.mood?.thoughts?.memories?.TryGainMemory(ThoughtDef.Named("Rimverse_FlowCalmed"));
                    FleckMaker.ThrowMetaIcon(tp.Position, map, FleckDefOf.Heart);
                    break;
                case FlowMode.Choke:
                    if (tp == null) return;
                    tp.TakeDamage(new DamageInfo(DamageDefOf.Blunt, power, 0.5f, -1f, caster));
                    tp.stances?.stunner?.StunFor(150, caster, false);
                    break;
                case FlowMode.Storm:
                    foreach (Pawn p in GenRadial.RadialDistinctThingsAround(target.Cell, map, 2.5f, true).OfType<Pawn>().ToList())
                    {
                        if (p == caster) continue;
                        p.TakeDamage(new DamageInfo(DamageDefOf.Burn, power, 0.3f, -1f, caster));
                        p.stances?.stunner?.StunFor(90, caster, false);
                    }
                    FleckMaker.ThrowLightningGlow(target.CenterVector3, map, 2f);
                    break;
            }
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            if ((Props.mode == FlowMode.Heal || Props.mode == FlowMode.Calm || Props.mode == FlowMode.Push || Props.mode == FlowMode.Choke) && target.Pawn == null) return false;
            return base.CanApplyOn(target, dest);
        }
    }

    public class CompProperties_UseEffectAwakenFlow : CompProperties_UseEffect
    {
        public CompProperties_UseEffectAwakenFlow()
        {
            compClass = typeof(CompUseEffect_AwakenFlow);
        }
    }

    public class CompUseEffect_AwakenFlow : CompUseEffect
    {
        public override AcceptanceReport CanBeUsedBy(Pawn p)
        {
            if (!FlowUtility.Enabled) return "Rimverse_FlowDisabled".Translate();
            if (!p.RaceProps.Humanlike) return false;
            if (FlowUtility.IsSensitive(p)) return "Rimverse_FlowAlready".Translate(p.Named("PAWN"));
            return base.CanBeUsedBy(p);
        }

        public override void DoEffect(Pawn usedBy)
        {
            base.DoEffect(usedBy);
            FlowUtility.Awaken(usedBy, FlowSide.None, true);
        }
    }

    public static class FlowForceBridge
    {
        private static TraitDef forceTrait;
        private static TraitDef lightTrait;
        private static TraitDef darkTrait;
        private static bool searched;

        public static bool Active => ExternalCompat.TheForce && ExternalCompat.TheForceLightsaber;

        private static void Find()
        {
            if (searched) return;
            searched = true;
            forceTrait = DefDatabase<TraitDef>.GetNamedSilentFail("Force_NeutralSensitivity");
            lightTrait = DefDatabase<TraitDef>.GetNamedSilentFail("Force_LightAffinity");
            darkTrait = DefDatabase<TraitDef>.GetNamedSilentFail("Force_DarkAffinity");
        }

        public static bool HasForceSensitivity(Pawn p)
        {
            if (!Active || p?.story?.traits == null) return false;
            Find();
            return forceTrait != null && p.story.traits.HasTrait(forceTrait);
        }

        public static int ForceDegree(Pawn p)
        {
            if (!HasForceSensitivity(p)) return 0;
            return p.story.traits.DegreeOfTrait(forceTrait) + 1;
        }

        public static FlowSide SideFromForce(Pawn p)
        {
            if (!Active || p?.story?.traits == null) return FlowSide.None;
            Find();
            if (lightTrait != null && p.story.traits.HasTrait(lightTrait)) return FlowSide.Harmony;
            if (darkTrait != null && p.story.traits.HasTrait(darkTrait)) return FlowSide.Rupture;
            return FlowSide.None;
        }

        public static void Sync(Pawn p)
        {
            if (!Active || !FlowUtility.Enabled || p?.story?.traits == null || !p.RaceProps.Humanlike) return;
            Find();
            if (HasForceSensitivity(p) && !FlowUtility.IsSensitive(p)) FlowUtility.Awaken(p, SideFromForce(p), p.Faction == Faction.OfPlayer && p.Spawned);
            else if (FlowUtility.IsSensitive(p) && forceTrait != null && !p.story.traits.HasTrait(forceTrait))
            {
                p.story.traits.GainTrait(new Trait(forceTrait, 0, true));
                FlowSide side = FlowUtility.Side(p);
                TraitDef aff = side == FlowSide.Harmony ? lightTrait : side == FlowSide.Rupture ? darkTrait : null;
                if (aff != null && !p.story.traits.HasTrait(aff) && aff.degreeDatas.Count > 0) p.story.traits.GainTrait(new Trait(aff, aff.degreeDatas.OrderBy(d => System.Math.Abs(d.degree)).First().degree, true));
            }
        }
    }

    public class GameComponent_GrandeFluxo : GameComponent
    {
        public GameComponent_GrandeFluxo(Game game)
        {
        }

        public override void GameComponentTick()
        {
            if (!FlowForceBridge.Active || Find.TickManager.TicksGame % RimverseUnits.TicksPerDay != 4321) return;
            foreach (Pawn p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_Colonists.ToList()) FlowForceBridge.Sync(p);
        }
    }
}
