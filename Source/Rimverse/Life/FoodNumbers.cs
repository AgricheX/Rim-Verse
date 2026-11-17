using RimWorld;
using Verse;

namespace Rimverse
{
    [StaticConstructorOnStartup]
    public static class FoodNumbers
    {
        static FoodNumbers()
        {
            SetRot("Rimverse_ExoticPreserve", 60f);
        }

        private static void SetRot(string defName, float days)
        {
            ThingDef d = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            CompProperties_Rottable rot = d?.GetCompProperties<CompProperties_Rottable>();
            if (rot == null) return;
            CompProperties_Rottable copy = new CompProperties_Rottable { daysToRotStart = days, rotDestroys = rot.rotDestroys, rotDamagePerDay = rot.rotDamagePerDay, daysToDessicated = rot.daysToDessicated, dessicatedDamagePerDay = rot.dessicatedDamagePerDay, disableIfHatcher = rot.disableIfHatcher };
            int i = d.comps.IndexOf(rot);
            if (i >= 0) d.comps[i] = copy;
        }
    }
}
