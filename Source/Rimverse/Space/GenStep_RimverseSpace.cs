using Verse;

namespace Rimverse
{
    public class GenStep_RimverseSpace : GenStep
    {
        public override int SeedPart => 734118205;

        public override void Generate(Map map, GenStepParams parms)
        {
            TerrainDef vacuum = RimverseDefOf.Rimverse_Vacuum;
            foreach (IntVec3 c in map.AllCells)
            {
                map.terrainGrid.SetTerrain(c, vacuum);
            }
            MapGenerator.PlayerStartSpot = map.Center;
        }
    }
}
