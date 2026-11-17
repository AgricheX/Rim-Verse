using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Rimverse
{
    [StaticConstructorOnStartup]
    public static class CompatPatch13
    {
        public static readonly bool Allies;
        public static readonly bool NoForcedSlowdown;
        private static readonly string[] sealedWords = { "hazmat", "cbrn", "mopp", "gasmask", "respirator", "nbc" };

        static CompatPatch13()
        {
            Allies = CompatRegistry.AnyActive(CompatGroup.Allies);
            NoForcedSlowdown = ModCompat.IsActive("thegoosebehindtheslaughter.noforcedslowdown");
            if (Allies) RimverseLog.Message("Allies mods: goodwill they spend on Rimverse civilizations now changes the civilization's opinion of you too.");
            if (NoForcedSlowdown) RimverseLog.Message("No Forced Slowdown: threats do not auto-pause while a ship is traveling.");
            if (ModCompat.IsActive("cp.rimmunation.2.clothing")) RimmuNation();
        }

        private static void RimmuNation()
        {
            StatDef seal = RimverseDefOf.Rimverse_VacuumSeal;
            if (seal == null) return;
            int n = 0;
            foreach (ThingDef d in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (!d.IsApparel || d.modContentPack == null || d.modContentPack.PackageId.ToLowerInvariant() != "cp.rimmunation.2.clothing") continue;
                string name = d.defName.ToLowerInvariant();
                if (!sealedWords.Any(w => name.Contains(w))) continue;
                if (d.equippedStatOffsets == null) d.equippedStatOffsets = new List<StatModifier>();
                if (d.equippedStatOffsets.Any(s => s.stat == seal)) continue;
                d.equippedStatOffsets.Add(new StatModifier { stat = seal, value = 0.1f });
                n++;
            }
            if (n > 0) RimverseLog.Message("Rimmu-Nation clothing: gas masks and hazmat gear slow vacuum exposure a little: " + n);
        }

        public static bool AnyPlayerShipTraveling()
        {
            List<Map> maps = Find.Maps;
            for (int i = 0; i < maps.Count; i++)
            {
                MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(maps[i]);
                if (loc != null && loc.Traveling) return true;
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(Faction), nameof(Faction.TryAffectGoodwillWith))]
    public static class Patch_AlliesGoodwillSync
    {
        public static void Postfix(Faction __instance, Faction other, bool __result)
        {
            if (!__result || !CompatPatch13.Allies || other == null || !other.IsPlayer) return;
            Civilization c = Currencies.CivOf(__instance);
            if (c == null) return;
            c.playerOpinion = __instance.GoodwillWith(other);
        }
    }
}
