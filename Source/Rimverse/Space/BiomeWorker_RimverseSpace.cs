using RimWorld;
using RimWorld.Planet;

namespace Rimverse
{
    public class BiomeWorker_RimverseSpace : BiomeWorker
    {
        public override float GetScore(BiomeDef biome, Tile tile, PlanetTile planetTile) => -100f;
    }
}
