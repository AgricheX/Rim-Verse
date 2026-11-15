using Verse;

namespace Rimverse
{
    public class HullTerrainExtension : DefModExtension
    {
        public float hullPoints = 30f;
        public float mass = 0.5f;
    }

    public class VacuumTerrainExtension : DefModExtension
    {
    }

    public static class HullUtility
    {
        public static HullTerrainExtension HullAt(Map map, IntVec3 c)
        {
            TerrainDef foundation = map.terrainGrid.FoundationAt(c);
            if (foundation != null)
            {
                HullTerrainExtension ext = foundation.GetModExtension<HullTerrainExtension>();
                if (ext != null) return ext;
            }
            TerrainDef top = map.terrainGrid.TerrainAt(c);
            return top?.GetModExtension<HullTerrainExtension>();
        }

        public static bool IsHull(Map map, IntVec3 c) => HullAt(map, c) != null;

        public static bool IsVacuumTerrain(Map map, IntVec3 c)
        {
            TerrainDef t = map.terrainGrid.TerrainAt(c);
            return t != null && t.HasModExtension<VacuumTerrainExtension>();
        }
    }
}
