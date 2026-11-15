using RimWorld;
using Verse;

namespace Rimverse
{
    [DefOf]
    public static class RimverseDefOf
    {
        public static StatDef Rimverse_VacuumSeal;
        public static StatDef Rimverse_PilotingOffset;
        public static HediffDef Rimverse_LaunchSickness;
        public static HediffDef Rimverse_Hypoxia;
        public static TerrainDef Rimverse_Vacuum;
        public static TerrainDef Rimverse_HullFloor;
        public static BiomeDef Rimverse_Orbit;
        public static MapGeneratorDef Rimverse_OrbitMap;
        public static MapGeneratorDef Rimverse_OrbitPocket;
        public static MapGeneratorDef Rimverse_AirlessBodyMap;
        public static MapGeneratorDef Rimverse_HostileBodyMap;
        public static FactionDef Rimverse_SpaceCiv;
        public static FactionDef Rimverse_SpacePirates;
        public static TraderKindDef Rimverse_CivSpaceTrader;
        public static TerrainDef Rimverse_Regolith;
        public static BiomeDef Rimverse_Airless;
        public static WorldObjectDef Rimverse_ShipInOrbit;
        public static PlanetLayerDef Rimverse_OrbitLayer;
        public static ResearchProjectDef Rimverse_ShipBasics;

        static RimverseDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(RimverseDefOf));
        }
    }
}
