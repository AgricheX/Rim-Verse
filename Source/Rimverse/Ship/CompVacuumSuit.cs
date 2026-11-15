using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public class CompProperties_VacuumSuit : CompProperties
    {
        public float airHours;
        public bool helmet;

        public CompProperties_VacuumSuit()
        {
            compClass = typeof(CompVacuumSuit);
        }
    }

    public class CompVacuumSuit : ThingComp
    {
        public float air = -1f;

        public CompProperties_VacuumSuit Props => (CompProperties_VacuumSuit)props;

        public float Air
        {
            get
            {
                if (air < 0f) air = Props.airHours;
                return air;
            }
            set => air = Mathf.Clamp(value, 0f, Props.airHours);
        }

        public override string CompInspectStringExtra()
        {
            if (Props.helmet) return null;
            return "Rimverse_SuitAir".Translate(Air.ToString("0.#"), Props.airHours.ToString("0.#"));
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref air, "air", -1f);
        }
    }

    public static class VacuumSuitUtility
    {
        public const float RefillHoursPerHour = 4f;
        public const float NoHelmetDrainFactor = 10f;

        public static CompVacuumSuit WornSuit(Pawn pawn, out bool hasHelmet)
        {
            hasHelmet = false;
            CompVacuumSuit suit = null;
            if (pawn.apparel == null) return null;
            foreach (Apparel a in pawn.apparel.WornApparel)
            {
                CompVacuumSuit c = a.TryGetComp<CompVacuumSuit>();
                if (c == null) continue;
                if (c.Props.helmet) hasHelmet = true;
                else suit = c;
            }
            return suit;
        }

        public static bool TryBreatheFromSuit(Pawn pawn, float seconds)
        {
            CompVacuumSuit suit = WornSuit(pawn, out bool helmet);
            if (suit == null || suit.Air <= 0f) return false;
            float hours = seconds / MapComponent_RimverseShips.SecondsPerHour * (helmet ? 1f : NoHelmetDrainFactor);
            suit.Air -= hours;
            return true;
        }

        public static void Refill(Pawn pawn, float seconds, RimverseShip ship)
        {
            CompVacuumSuit suit = WornSuit(pawn, out _);
            if (suit == null || suit.Air >= suit.Props.airHours) return;
            float hours = Mathf.Min(suit.Props.airHours - suit.Air, RefillHoursPerHour * seconds / MapComponent_RimverseShips.SecondsPerHour);
            if (ship != null)
            {
                hours = Mathf.Min(hours, ship.state.airStock);
                ship.state.airStock -= hours;
            }
            suit.Air += hours;
        }
    }
}
