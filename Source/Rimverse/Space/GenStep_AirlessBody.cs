using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public class GenStep_AirlessBody : GenStep
    {
        private static readonly string[] Ores = { "MineableSteel", "MineableSteel", "MineableSteel", "MineableComponentsIndustrial", "MineablePlasteel", "MineableSilver", "MineableGold", "MineableUranium", "MineableJade", "Rimverse_MineableIce", "Rimverse_MineablePrecursorWreck" };
        private static readonly float[] OreWeights = { 30f, 0f, 0f, 10f, 8f, 10f, 3f, 5f, 2f, 14f, 1.5f };

        public override int SeedPart => 911834227;

        public override void Generate(Map map, GenStepParams parms)
        {
            BodyData body = BodyGenContext.current;
            bool asteroid = body == null || body.type == BodyType.Asteroid || body.type == BodyType.Belt;
            TerrainDef ground = RimverseDefOf.Rimverse_Regolith;
            TerrainDef vacuum = RimverseDefOf.Rimverse_Vacuum;
            ThingDef rock = DefDatabase<ThingDef>.GetNamedSilentFail("Granite") ?? ThingDefOf.Granite;
            IntVec3 center = map.Center;
            float radius = Mathf.Min(map.Size.x, map.Size.z) * 0.42f;
            float seedX = Rand.Range(0f, 1000f);
            float seedZ = Rand.Range(0f, 1000f);
            foreach (IntVec3 c in map.AllCells)
            {
                float dist = (c - center).LengthHorizontal;
                float edge = radius * (0.85f + 0.3f * Mathf.PerlinNoise(seedX + c.x * 0.05f, seedZ + c.z * 0.05f));
                if (asteroid && dist > edge)
                {
                    map.terrainGrid.SetTerrain(c, vacuum);
                    continue;
                }
                map.terrainGrid.SetTerrain(c, ground);
                float n = Mathf.PerlinNoise(seedX + c.x * 0.08f, seedZ + c.z * 0.08f);
                float threshold = asteroid ? Mathf.Lerp(0.42f, 0.75f, dist / Mathf.Max(edge, 1f)) : 0.68f;
                if (n > threshold && dist > 8f)
                {
                    ThingDef def = rock;
                    float oreRoll = Mathf.PerlinNoise(seedZ + c.x * 0.2f, seedX + c.z * 0.2f);
                    if (oreRoll > 0.72f) def = PickOre();
                    if (def != null) GenSpawn.Spawn(def, c, map);
                }
            }
            for (int i = 0; i < 6; i++)
            {
                IntVec3 cell = CellFinder.RandomClosewalkCellNear(center, map, (int)(radius * 0.8f));
                ThingDef chunk = DefDatabase<ThingDef>.GetNamedSilentFail("ChunkGranite");
                if (chunk != null && cell.Standable(map)) GenSpawn.Spawn(chunk, cell, map);
            }
            MapGenerator.PlayerStartSpot = center;
        }

        private static ThingDef PickOre()
        {
            List<ThingDef> defs = new List<ThingDef>();
            List<float> weights = new List<float>();
            for (int i = 0; i < Ores.Length; i++)
            {
                ThingDef d = DefDatabase<ThingDef>.GetNamedSilentFail(Ores[i]);
                if (d == null || OreWeights[i] <= 0f) continue;
                defs.Add(d);
                weights.Add(OreWeights[i]);
            }
            if (defs.Count == 0) return null;
            float total = 0f;
            foreach (float w in weights) total += w;
            float roll = Rand.Value * total;
            for (int i = 0; i < defs.Count; i++)
            {
                roll -= weights[i];
                if (roll <= 0f) return defs[i];
            }
            return defs[defs.Count - 1];
        }
    }
}
