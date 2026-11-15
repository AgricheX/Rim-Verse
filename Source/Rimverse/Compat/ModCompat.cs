using System.Collections.Generic;
using Verse;

namespace Rimverse
{
    [StaticConstructorOnStartup]
    public static class ModCompat
    {
        public const string HarmonyId = "brrainz.harmony";
        public const string VehicleFrameworkId = "SmashPhil.VehicleFramework";
        public const string CombatExtendedId = "CETeam.CombatExtended";
        public const string EndlessRoomsId = "agriche.endlessrooms";
        public const string SaveOurShip2Id = "kentington.saveourship2";
        public const string GravshipExpanded1Id = "vanillaexpanded.gravship";
        public const string GravshipExpanded2Id = "vanillaexpanded.gravship2";

        public static readonly bool Royalty;
        public static readonly bool Ideology;
        public static readonly bool Biotech;
        public static readonly bool Anomaly;
        public static readonly bool Odyssey;
        public static readonly bool VehicleFramework;
        public static readonly bool CombatExtended;
        public static readonly bool EndlessRooms;
        public static readonly bool SaveOurShip2;
        public static readonly bool GravshipExpanded1;
        public static readonly bool GravshipExpanded2;

        private static readonly Dictionary<string, bool> cache = new Dictionary<string, bool>();

        static ModCompat()
        {
            Royalty = ModsConfig.RoyaltyActive;
            Ideology = ModsConfig.IdeologyActive;
            Biotech = ModsConfig.BiotechActive;
            Anomaly = ModsConfig.AnomalyActive;
            Odyssey = ModsConfig.OdysseyActive;
            VehicleFramework = IsActive(VehicleFrameworkId);
            CombatExtended = IsActive(CombatExtendedId);
            EndlessRooms = IsActive(EndlessRoomsId);
            SaveOurShip2 = IsActive(SaveOurShip2Id);
            GravshipExpanded1 = IsActive(GravshipExpanded1Id);
            GravshipExpanded2 = IsActive(GravshipExpanded2Id);
            RimverseLog.Message(Summary());
        }

        public static bool IsActive(string packageId)
        {
            if (packageId.NullOrEmpty())
            {
                return false;
            }
            string key = packageId.ToLowerInvariant();
            if (!cache.TryGetValue(key, out bool active))
            {
                active = ModsConfig.IsActive(key) || ModsConfig.IsActive(key + "_steam") || ModsConfig.IsActive(key + "_copy");
                cache[key] = active;
            }
            return active;
        }

        public static bool AnyActive(List<string> packageIds)
        {
            if (packageIds == null)
            {
                return false;
            }
            for (int i = 0; i < packageIds.Count; i++)
            {
                if (IsActive(packageIds[i]))
                {
                    return true;
                }
            }
            return false;
        }

        public static string Summary(bool translated = false)
        {
            List<string> parts = new List<string>();
            if (Royalty) parts.Add("Royalty");
            if (Ideology) parts.Add("Ideology");
            if (Biotech) parts.Add("Biotech");
            if (Anomaly) parts.Add("Anomaly");
            if (Odyssey) parts.Add("Odyssey");
            if (CombatExtended) parts.Add("Combat Extended");
            if (EndlessRooms) parts.Add("Endless Rooms");
            if (SaveOurShip2) parts.Add("Save Our Ship 2");
            if (GravshipExpanded1) parts.Add("Vanilla Gravship Expanded 1");
            if (GravshipExpanded2) parts.Add("Vanilla Gravship Expanded 2");
            if (translated) return parts.Count == 0 ? "Rimverse_ActiveIntegrationsNone".Translate().ToString() : "Rimverse_ActiveIntegrations".Translate(string.Join(", ", parts)).ToString();
            return parts.Count == 0 ? "Active integrations: none." : "Active integrations: " + string.Join(", ", parts) + ".";
        }
    }
}
