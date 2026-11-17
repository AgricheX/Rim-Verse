using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public class HediffCompProperties_PlanetStrain : HediffCompProperties
    {
        public float min = 0.6f;
        public float max = 1.6f;

        public HediffCompProperties_PlanetStrain()
        {
            compClass = typeof(HediffComp_PlanetStrain);
        }
    }

    public class HediffComp_PlanetStrain : HediffComp
    {
        public float factor = -1f;
        private float last = -1f;

        public HediffCompProperties_PlanetStrain Props => (HediffCompProperties_PlanetStrain)props;

        public static float FactorFor(Map map, float min, float max)
        {
            if (map == null) return 1f;
            GameComponent_Universe u = GameComponent_Universe.Instance;
            MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(map);
            long key = loc != null && loc.Body != null ? GenText.StableStringHash((loc.systemKey ?? "") + ":" + loc.bodyIndex) : map.Tile;
            UniverseRand r = new UniverseRand(UniverseRand.Hash(u?.universeSeed ?? 0, 7713, (int)key));
            return r.Range(min, max);
        }

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            if (factor < 0f) factor = FactorFor(Pawn.MapHeld, Props.min, Props.max);
            last = parent.Severity;
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (!Pawn.IsHashIntervalTick(250)) return;
            if (factor < 0f) factor = FactorFor(Pawn.MapHeld, Props.min, Props.max);
            float s = parent.Severity;
            if (last >= 0f && s > last && !Mathf.Approximately(factor, 1f)) severityAdjustment += (s - last) * (factor - 1f);
            last = s + Mathf.Max(0f, severityAdjustment);
        }

        public override string CompLabelInBracketsExtra => factor > 0f ? "Rimverse_PlagueStrain".Translate(factor.ToStringPercent()) : null;

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref factor, "rimverseStrain", -1f);
            Scribe_Values.Look(ref last, "rimverseStrainLast", -1f);
        }
    }
}
