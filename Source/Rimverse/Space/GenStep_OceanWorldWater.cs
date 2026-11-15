using RimWorld;
using Verse;

namespace Rimverse
{
    public class GenStep_OceanWorldWater : GenStep
    {
        public override int SeedPart => 772093415;

        public override void Generate(Map map, GenStepParams parms)
        {
            BodyData body = BodyGenContext.current ?? MapComponent_ShipLocation.For(map)?.Body;
            if (body == null) return;
            float share = body.type == BodyType.Ocean ? 0.7f : body.water > 0.6f ? (body.water - 0.6f) * 1.5f : 0f;
            if (share <= 0f) return;
            MapGenFloatGrid elev = MapGenerator.Elevation;
            if (elev == null) return;
            bool frozen = body.temperatureC < -5f;
            TerrainDef deep = frozen ? TerrainDefOf.Ice : TerrainDefOf.WaterOceanDeep;
            TerrainDef shallow = frozen ? TerrainDefOf.Ice : TerrainDefOf.WaterOceanShallow;
            float deepLine = share * 0.8f;
            foreach (IntVec3 c in map.AllCells)
            {
                if (c.GetEdifice(map) != null) continue;
                float e = elev[c];
                if (e < deepLine) map.terrainGrid.SetTerrain(c, deep);
                else if (e < share) map.terrainGrid.SetTerrain(c, shallow);
            }
        }
    }
}
