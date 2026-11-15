using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Vehicles;
using Verse;

namespace Rimverse
{
    [StaticConstructorOnStartup]
    public static class BetterRimCompat
    {
        private static readonly HashSet<string> SafePacks = new HashSet<string> { "ludeon.rimworld", "ludeon.rimworld.royalty", "ludeon.rimworld.ideology", "ludeon.rimworld.biotech", "ludeon.rimworld.anomaly", "ludeon.rimworld.odyssey", "agrichex.rimverse" };

        static BetterRimCompat()
        {
            try
            {
                SpaceCraftForForeignAircraft();
                CleanSpaceMapGenerators();
            }
            catch (System.Exception e)
            {
                RimverseLog.Warning("Better Rim compat setup failed: " + e.Message);
            }
        }

        private static bool Foreign(Def d) => d?.modContentPack != null && !SafePacks.Contains(d.modContentPack.PackageId.ToLowerInvariant());

        private static void SpaceCraftForForeignAircraft()
        {
            int n = 0;
            foreach (VehicleDef v in DefDatabase<VehicleDef>.AllDefsListForReading)
            {
                if (!Foreign(v) || v.type != VehicleType.Air) continue;
                if (v.comps == null) v.comps = new List<CompProperties>();
                if (v.comps.Any(c => c is CompProperties_SpaceCraft)) continue;
                v.comps.Add(new CompProperties_SpaceCraft { role = CraftRole.Shuttle, fuelPerTrip = 12f });
                n++;
            }
            if (n > 0) RimverseLog.Message("Space travel added to " + n + " aircraft from other mods.");
        }

        private static void CleanSpaceMapGenerators()
        {
            if (!ExternalCompat.ActiveIds(ExternalCompat.MapGenMods).Any()) return;
            int n = 0;
            foreach (MapGeneratorDef g in DefDatabase<MapGeneratorDef>.AllDefsListForReading)
            {
                if (!g.defName.StartsWith("Rimverse_") || g.genSteps == null) continue;
                n += g.genSteps.RemoveAll(s => Foreign(s));
            }
            if (n > 0) RimverseLog.Message("Kept " + n + " foreign map steps out of Rimverse space maps.");
        }

        public static TraderKindDef OrbitalTrader()
        {
            TraderKindDef own = DefDatabase<TraderKindDef>.GetNamedSilentFail("Rimverse_SpaceGoodsTrader");
            if (!ExternalCompat.ActiveIds(ExternalCompat.OrbitalTraderMods).Any() || Rand.Chance(0.5f)) return own;
            List<TraderKindDef> foreign = DefDatabase<TraderKindDef>.AllDefsListForReading.Where(t => t.orbital && Foreign(t) && t.commonality > 0f).ToList();
            return foreign.Count > 0 ? foreign.RandomElementByWeight(t => t.commonality) : own;
        }
    }
}
