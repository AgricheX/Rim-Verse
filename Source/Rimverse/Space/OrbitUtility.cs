using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public static class OrbitUtility
    {
        public const float OrbitRadius = 130f;
        public const int OrbitSubdivisions = 5;

        public static bool UsingOdysseyOrbit => ModCompat.Odyssey && RimverseMod.Settings.odysseyIntegration && Find.WorldGrid.Orbit != null;

        public static PlanetLayer OrbitLayer(bool create)
        {
            WorldGrid grid = Find.WorldGrid;
            if (UsingOdysseyOrbit) return grid.Orbit;
            if (grid.TryGetFirstLayerOfDef(RimverseDefOf.Rimverse_OrbitLayer, out PlanetLayer layer)) return layer;
            if (!create) return null;
            layer = grid.RegisterPlanetLayer(RimverseDefOf.Rimverse_OrbitLayer, Vector3.zero, OrbitRadius, 180f, 300f, OrbitSubdivisions, 160f, 2.5f);
            WorldGenerator.GeneratePlanetLayer(layer, Find.World.info.seedString, Gen.HashCombineInt(Find.World.info.Seed, 734118205));
            PlanetLayer surface = grid.Surface;
            if (surface != null)
            {
                if (!surface.HasConnectionFromTo(layer)) surface.AddConnection(layer, 0f);
                if (!layer.HasConnectionFromTo(surface)) layer.AddConnection(surface, 0f);
            }
            RimverseLog.Message("Orbit layer created.");
            return layer;
        }

        public static PlanetTile OrbitTileAbove(PlanetTile surfaceTile)
        {
            PlanetLayer orbit = OrbitLayer(true);
            if (orbit == null || !surfaceTile.Valid) return PlanetTile.Invalid;
            Vector3 dir = surfaceTile.Layer.GetTileCenter(surfaceTile).normalized;
            List<(float, int)> best = new List<(float, int)>();
            for (int i = 0; i < orbit.TilesCount; i++)
            {
                float d = (orbit.GetTileCenter(i).normalized - dir).sqrMagnitude;
                best.Add((d, i));
            }
            best.Sort((a, b) => a.Item1.CompareTo(b.Item1));
            for (int i = 0; i < best.Count && i < 200; i++)
            {
                PlanetTile tile = orbit.PlanetTileForID(best[i].Item2);
                if (!Find.WorldObjects.AnyWorldObjectAt(tile)) return tile;
            }
            return best.Count > 0 ? orbit.PlanetTileForID(best[0].Item2) : PlanetTile.Invalid;
        }

        public static PlanetTile SurfaceTileBelow(PlanetTile orbitTile)
        {
            PlanetLayer surface = Find.WorldGrid.Surface;
            if (surface == null || !orbitTile.Valid) return PlanetTile.Invalid;
            Vector3 dir = orbitTile.Layer.GetTileCenter(orbitTile).normalized;
            float bestD = float.MaxValue;
            int bestId = -1;
            for (int i = 0; i < surface.TilesCount; i++)
            {
                float d = (surface.GetTileCenter(i).normalized - dir).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    bestId = i;
                }
            }
            return bestId < 0 ? PlanetTile.Invalid : surface.PlanetTileForID(bestId);
        }

        public static Map CreateOrbitMap(PlanetTile surfaceTile, string shipName, IntVec3 size)
        {
            PlanetTile tile = OrbitTileAbove(surfaceTile);
            if (!tile.Valid) return null;
            ShipOrbitMapParent parent = (ShipOrbitMapParent)WorldObjectMaker.MakeWorldObject(RimverseDefOf.Rimverse_ShipInOrbit);
            parent.Tile = tile;
            parent.surfaceTile = surfaceTile;
            parent.shipName = shipName;
            parent.SetFaction(Faction.OfPlayer);
            Find.WorldObjects.Add(parent);
            return MapGenerator.GenerateMap(size, parent, parent.MapGeneratorDef, parent.ExtraGenStepDefs, null, false);
        }
    }
}
