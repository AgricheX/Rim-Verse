using RimWorld;
using Verse;

namespace Rimverse
{
    public class SpaceBiomeExtension : DefModExtension
    {
        public float gravity = 0f;
    }

    public static class SpaceMapUtility
    {
        public static bool IsRimverseSpace(Map map) => map?.Biome != null && map.Biome.HasModExtension<SpaceBiomeExtension>();

        public static bool IsOdysseyVacuum(Map map) => map?.Biome != null && map.Biome.inVacuum && !IsRimverseSpace(map);

        public static bool IsSpace(Map map) => IsRimverseSpace(map) || IsOdysseyVacuum(map);

        public static bool RimverseHandlesAir(Map map) => IsRimverseSpace(map) || IsHostileSurface(map);

        public static AtmosphereType OutsideAtmosphere(Map map)
        {
            if (map == null) return AtmosphereType.Breathable;
            if (IsSpace(map)) return AtmosphereType.None;
            MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(map);
            return loc?.OutsideAtmosphere ?? AtmosphereType.Breathable;
        }

        public static bool IsHostileSurface(Map map)
        {
            if (map == null || !map.IsPocketMap || IsSpace(map)) return false;
            AtmosphereType a = OutsideAtmosphere(map);
            return a == AtmosphereType.Thin || a == AtmosphereType.Toxic || a == AtmosphereType.Dense;
        }

        public static bool IsOrbit(Map map)
        {
            if (!IsSpace(map)) return false;
            MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(map);
            return loc == null || !loc.OnSurface;
        }

        private static StatDef vacuumResistance;
        private static bool vacuumResistanceSearched;

        public static float VacuumProtection(Pawn pawn)
        {
            float value = pawn.GetStatValue(RimverseDefOf.Rimverse_VacuumSeal);
            if (!vacuumResistanceSearched)
            {
                vacuumResistanceSearched = true;
                vacuumResistance = DefDatabase<StatDef>.GetNamedSilentFail("VacuumResistance");
            }
            if (vacuumResistance != null)
            {
                StatPart_RimverseSuit.suppress = true;
                float other;
                try
                {
                    other = pawn.GetStatValue(vacuumResistance, true, -1);
                }
                finally
                {
                    StatPart_RimverseSuit.suppress = false;
                }
                if (other > value) value = other;
            }
            return value;
        }

        public static bool Breathes(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || !pawn.Spawned) return false;
            if (pawn.RaceProps.IsFlesh == false) return false;
            if (pawn.health?.hediffSet == null) return false;
            if (pawn.kindDef?.GetModExtension<AlienFaunaExtension>()?.vacuum == true) return false;
            return true;
        }
    }
}
