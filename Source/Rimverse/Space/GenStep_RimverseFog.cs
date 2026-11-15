using Verse;

namespace Rimverse
{
    public class GenStep_RimverseFog : GenStep
    {
        public override int SeedPart => 61942377;

        public override void Generate(Map map, GenStepParams parms)
        {
            map.fogGrid.Refog(CellRect.WholeMap(map));
            IntVec3 root = MapGenerator.PlayerStartSpotValid ? MapGenerator.PlayerStartSpot : IntVec3.Invalid;
            if (!root.IsValid || !root.InBounds(map) || !root.Standable(map))
            {
                if (!CellFinder.TryFindRandomEdgeCellWith(c => c.Standable(map), map, 0f, out root) && !CellFinderLoose.TryGetRandomCellWith(c => c.Standable(map), map, 2000, out root))
                {
                    root = map.Center;
                }
            }
            FloodFillerFog.FloodUnfog(root, map);
            if (MapGenerator.rootsToUnfog != null)
            {
                foreach (IntVec3 c in MapGenerator.rootsToUnfog)
                {
                    if (c.InBounds(map)) FloodFillerFog.FloodUnfog(c, map);
                }
            }
        }
    }
}
