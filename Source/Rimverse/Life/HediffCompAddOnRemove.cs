using Verse;

namespace Rimverse
{
    public class HediffCompProperties_AddOnRemove : HediffCompProperties
    {
        public HediffDef hediff;

        public HediffCompProperties_AddOnRemove()
        {
            compClass = typeof(HediffComp_AddOnRemove);
        }
    }

    public class HediffComp_AddOnRemove : HediffComp
    {
        public HediffCompProperties_AddOnRemove Props => (HediffCompProperties_AddOnRemove)props;

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            Pawn p = Pawn;
            if (Props.hediff == null || p == null || p.Dead || p.health == null) return;
            if (!p.health.hediffSet.HasHediff(Props.hediff)) p.health.AddHediff(Props.hediff);
        }
    }
}
