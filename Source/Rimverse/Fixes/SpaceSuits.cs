using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Rimverse
{
    public static class SpaceSuits
    {
        public static void Equip(Pawn p)
        {
            if (p == null || p.Dead || p.apparel == null || !p.RaceProps.Humanlike) return;
            if (!SpaceMapUtility.Breathes(p)) return;
            VacuumSuitUtility.WornSuit(p, out bool helmet);
            if (VacuumSuitUtility.WornSuit(p, out _) != null && helmet) return;
            if (SpaceMapUtility.VacuumProtection(p) >= 1f) return;
            TechLevel tech = p.Faction?.def.techLevel ?? TechLevel.Industrial;
            string suit = tech >= TechLevel.Spacer ? (p.kindDef.combatPower >= 90f ? "Rimverse_Armor_SuitBoarding" : "Rimverse_SuitWork") : "Rimverse_SuitEmergency";
            Wear(p, DefDatabase<ThingDef>.GetNamedSilentFail(suit) ?? DefDatabase<ThingDef>.GetNamedSilentFail("Rimverse_SuitEmergency"));
            Wear(p, DefDatabase<ThingDef>.GetNamedSilentFail("Rimverse_HelmetVacuum"));
        }

        private static void Wear(Pawn p, ThingDef def)
        {
            if (def == null || !def.IsApparel) return;
            if (!ApparelUtility.HasPartsToWear(p, def)) return;
            ThingDef stuff = def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null;
            Apparel a = (Apparel)ThingMaker.MakeThing(def, stuff);
            foreach (Apparel worn in new List<Apparel>(p.apparel.WornApparel))
            {
                if (!ApparelUtility.CanWearTogether(def, worn.def, p.RaceProps.body)) p.apparel.Remove(worn);
            }
            p.apparel.Wear(a, false, true);
            CompVacuumSuit c = a.TryGetComp<CompVacuumSuit>();
            if (c != null) c.Air = c.Props.airHours;
        }

        public static void EquipAll(IEnumerable<Pawn> pawns)
        {
            foreach (Pawn p in pawns) Equip(p);
        }
    }

    [HarmonyPatch(typeof(PawnsArrivalModeWorker), nameof(PawnsArrivalModeWorker.Arrive))]
    public static class Patch_SpaceArrivalSuits
    {
        public static void Prefix(List<Pawn> pawns, IncidentParms parms)
        {
            if (parms?.target is Map map && SpaceMapUtility.IsSpace(map) && pawns != null) SpaceSuits.EquipAll(pawns);
        }
    }
}
