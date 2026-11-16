using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Rimverse
{
    public static class ExternalCompat
    {
        private static bool Any(params string[] ids) => ids.Any(ModCompat.IsActive);

        public static bool LocalFlight => Any("nuanki.localflight", "salvador143.shuttledock");
        public static bool HardVacuum => Any("petetimessix.hardvacuum");
        public static bool TreesInSpace => Any("masstell.treesinspace");
        public static bool FreeLanding => Any("nep.landonanything", "exploriters.siiftun1857.launchintoemptyspace");
        public static bool LayeredOrbit => Any("mrhydralisk.layeredatmosphereorbit");
        public static bool AutoRepair => Any("lecht.autorepairon", "gunseeker.repairstation", "zaryuarm.mod");
        public static bool AutoParking => Any("rabiosus.vfautoparking");
        public static bool GravshipThreats => Any("asunib.gravshipfleet", "sk.gravshipraids");
        public static bool TheForce => Any("lee.theforce.standalone");
        public static bool TheForceLightsaber => Any("lee.theforce.lightsaber");
        public static bool RimGram => Any("we1tall.rimgram");
        public static bool RimHud => Any("jaxe.rimhud");
        public static bool MeleeAnimation => Any("co.uk.epicguru.meleeanimation");
        public static bool Rimazon => Any("pc1001.punkyroo.rimazon");
        public static bool BetterMoney => Any("sylant.silver");
        public static bool VisitSettlements => Any("ninagoblin.visitsettlements", "alt4s.visitsettlements");
        public static bool EmpireMod => Any("matathias.empire");
        public static bool WorldDomination => Any("tsa.worlddomination", "tsa.worlddominationexperimental");
        public static bool VanillaPsycasts => Any("vanillaexpanded.vpsycastse");

        public static readonly string[] MapGenMods = new[]
        {
            "cain.mineshaft", "chuiu.greenworld", "koth.realisticplanets1.6", "m00nl1ght.geologicallandforms", "m00nl1ght.mappreview",
            "oblitus.mylittleplanet", "sarg.alphaprefabs", "trigger.cellprefabs", "zal.prefabs", "park.sundial", "park.desrtstorm",
            "evileyeguy.custombaseframework", "we1tall.depths"
        }.Concat(CompatRegistry.Known.Where(kv => kv.Value == CompatGroup.MapGen).Select(kv => kv.Key)).Distinct().ToArray();

        public static readonly string[] OrbitalTraderMods = new[]
        {
            "veltaris.mechcrate", "teiwaz.gtgtradercore", "teiwaz.aot", "teiwaz.oot", "shunter.bettertradersguild", "arakos.callatrader.acat"
        }.Concat(CompatRegistry.Known.Where(kv => kv.Value == CompatGroup.OrbitalTraders).Select(kv => kv.Key)).Distinct().ToArray();

        public static IEnumerable<string> ActiveIds(IEnumerable<string> ids) => ids.Where(ModCompat.IsActive);
    }
}
