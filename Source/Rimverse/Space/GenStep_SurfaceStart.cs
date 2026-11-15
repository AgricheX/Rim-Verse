using Verse;

namespace Rimverse
{
    public class GenStep_SurfaceStart : GenStep
    {
        public override int SeedPart => 418877311;

        public override void Generate(Map map, GenStepParams parms)
        {
            IntVec3 spot = map.Center;
            if (!CellFinder.TryFindRandomCellNear(map.Center, map, 30, c => c.Standable(map) && !c.Fogged(map), out spot)) spot = map.Center;
            MapGenerator.PlayerStartSpot = spot;
        }
    }
}
