using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Rimverse
{
    public class IngestionOutcomeDoer_ReduceHediffs : IngestionOutcomeDoer
    {
        public List<HediffDef> hediffs = new List<HediffDef>();
        public float amount = 0.5f;

        protected override void DoIngestionOutcomeSpecial(Pawn pawn, Thing ingested, int ingestedCount)
        {
            if (pawn?.health?.hediffSet == null) return;
            foreach (HediffDef def in hediffs)
            {
                Hediff h = pawn.health.hediffSet.GetFirstHediffOfDef(def);
                if (h == null) continue;
                h.Severity -= amount;
                if (h.Severity <= 0.001f) pawn.health.RemoveHediff(h);
            }
        }
    }
}
