using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public static class LifeAboard
    {
        public const int IntervalTicks = RimverseUnits.TicksPerHour;
        public const float ZeroG = 0.1f;
        public const float BoneLossPerHour = 1f / 120f;
        public const float BoneRecoveryPerHour = 2f / 24f;
        public const float AdaptationDecayPerHour = 1f / 240f;
        public const float CabinFeverPerHour = 1f / (30f * 24f);
        public const float CabinFeverRecoveryPerHour = 1f / 12f;
        public const float SkyLongingPerHour = 1f / (365f * 24f);

        private static readonly List<Pawn> tmp = new List<Pawn>();

        public static void Tick(Map map, MapComponent_RimverseShips comp)
        {
            if (!RimverseMod.Settings.lifeAboardEnabled || map == null) return;
            bool confined = SpaceMapUtility.IsSpace(map) || SpaceMapUtility.IsHostileSurface(map);
            bool tracked = confined || SpaceMapUtility.IsRimverseSpace(map) || MapComponent_ShipLocation.For(map)?.Body != null;
            tmp.Clear();
            tmp.AddRange(map.mapPawns.AllPawnsSpawned);
            foreach (Pawn p in tmp)
            {
                if (p.Dead || !p.RaceProps.Humanlike || p.health?.hediffSet == null) continue;
                if (!tracked && !HasAny(p)) continue;
                TickPawn(p, map, comp, confined);
            }
            tmp.Clear();
        }

        private static bool HasAny(Pawn p)
        {
            HediffSet s = p.health.hediffSet;
            return s.HasHediff(LifeDefOf.Rimverse_BoneLoss) || s.HasHediff(LifeDefOf.Rimverse_ZeroGAdaptation) || s.HasHediff(LifeDefOf.Rimverse_CabinFever) || s.HasHediff(LifeDefOf.Rimverse_GravityCrush) || s.HasHediff(LifeDefOf.Rimverse_LowGravityClumsy) || s.HasHediff(LifeDefOf.Rimverse_LowGravityStride) || s.HasHediff(LifeDefOf.Rimverse_DryClimate);
        }

        private static void TickPawn(Pawn p, Map map, MapComponent_RimverseShips comp, bool confined)
        {
            LineageExtension lin = LineageUtility.ExtensionOf(p);
            float g = GravityUtility.GravityAt(p);
            HediffSet set = p.health.hediffSet;
            if (g < ZeroG)
            {
                if ((lin == null || !lin.zeroGAdapted) && !GeneSpace.Any(p, e => e.zeroGAdapted))
                {
                    Hediff adapt = set.GetFirstHediffOfDef(LifeDefOf.Rimverse_ZeroGAdaptation);
                    if (adapt == null)
                    {
                        if (!CatalogTraits.IgnoresZeroGSickness(p)) p.health.AddHediff(LifeDefOf.Rimverse_ZeroGSickness);
                        adapt = p.health.AddHediff(LifeDefOf.Rimverse_ZeroGAdaptation);
                    }
                    adapt.Severity = 1f;
                    float boneFactor = CatalogTraits.BoneLossFactor(p);
                    if (boneFactor > 0f)
                    {
                        HealthUtility.AdjustSeverity(p, LifeDefOf.Rimverse_BoneLoss, BoneLossPerHour * boneFactor);
                        HealthUtility.AdjustSeverity(p, CatalogDefOf.Rimverse_MuscleAtrophy, BoneLossPerHour * 0.5f * boneFactor);
                    }
                }
            }
            else
            {
                Adjust(p, LifeDefOf.Rimverse_ZeroGAdaptation, -AdaptationDecayPerHour);
                Adjust(p, LifeDefOf.Rimverse_BoneLoss, -BoneRecoveryPerHour);
                Adjust(p, CatalogDefOf.Rimverse_MuscleAtrophy, -BoneRecoveryPerHour);
            }
            float limit = (lin?.crushLimit ?? 1.3f) + GeneSpace.Sum(p, e => e.crushBonus) + CatalogTraits.CrushBonus(p);
            if (g > limit)
            {
                Hediff crush = set.GetFirstHediffOfDef(LifeDefOf.Rimverse_GravityCrush) ?? p.health.AddHediff(LifeDefOf.Rimverse_GravityCrush);
                crush.Severity = Mathf.Max(0.01f, (g - limit) / 0.3f);
            }
            else Remove(p, LifeDefOf.Rimverse_GravityCrush);
            Toggle(p, LifeDefOf.Rimverse_LowGravityClumsy, lin != null && lin.lowGravityClumsy && g < 0.8f && !GeneSpace.Any(p, e => e.noClumsy));
            Toggle(p, LifeDefOf.Rimverse_LowGravityStride, lin != null && lin.lowGravityStride && g < 0.6f);
            Toggle(p, LifeDefOf.Rimverse_DryClimate, lin != null && lin.dryClimatePenalty && !confined && map.TileInfo != null && map.TileInfo.rainfall < 400f);
            if (confined)
            {
                float rate = CabinFeverPerHour * (lin?.cabinFeverFactor ?? 1f) * GeneSpace.Factor(p, e => e.cabinFeverFactor) * CatalogTraits.CabinFeverFactor(p);
                RimverseShip ship = comp?.ShipAt(p.Position);
                if (ship != null && (ship.HasRole(ShipPartRole.Garden) || ship.HasRole(ShipPartRole.Hydroponics) || ship.HasRole(ShipPartRole.Window))) rate *= 0.5f;
                if (rate > 0f) HealthUtility.AdjustSeverity(p, LifeDefOf.Rimverse_CabinFever, rate);
                if (!CatalogTraits.Has(p, CatalogDefOf.Rimverse_VoidBorn)) HealthUtility.AdjustSeverity(p, CatalogDefOf.Rimverse_SkyLonging, SkyLongingPerHour * (CatalogTraits.Has(p, CatalogDefOf.Rimverse_Grounded) ? 3f : 1f));
            }
            else
            {
                Adjust(p, LifeDefOf.Rimverse_CabinFever, -CabinFeverRecoveryPerHour);
                Adjust(p, CatalogDefOf.Rimverse_SkyLonging, -CabinFeverRecoveryPerHour);
            }
        }

        private static void Adjust(Pawn p, HediffDef def, float amount)
        {
            Hediff h = p.health.hediffSet.GetFirstHediffOfDef(def);
            if (h == null) return;
            h.Severity += amount;
            if (h.Severity <= 0f) p.health.RemoveHediff(h);
        }

        private static void Remove(Pawn p, HediffDef def)
        {
            Hediff h = p.health.hediffSet.GetFirstHediffOfDef(def);
            if (h != null) p.health.RemoveHediff(h);
        }

        private static void Toggle(Pawn p, HediffDef def, bool on)
        {
            Hediff h = p.health.hediffSet.GetFirstHediffOfDef(def);
            if (on && h == null) p.health.AddHediff(def);
            else if (!on && h != null) p.health.RemoveHediff(h);
        }

        public static void WarpSickness(Map map)
        {
            if (!RimverseMod.Settings.lifeAboardEnabled || map == null) return;
            foreach (Pawn p in map.mapPawns.AllPawnsSpawned)
            {
                if (p.Dead || !p.RaceProps.Humanlike || p.health?.hediffSet == null || CatalogTraits.IgnoresWarpSickness(p)) continue;
                Hediff h = p.health.hediffSet.GetFirstHediffOfDef(LifeDefOf.Rimverse_WarpSickness) ?? p.health.AddHediff(LifeDefOf.Rimverse_WarpSickness);
                h.Severity = 1f;
            }
        }
    }
}
