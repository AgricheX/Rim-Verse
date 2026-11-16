using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public static class ShipFracture
    {
        public const float FractureIntegrity = 0.1f;

        public static bool TryFracture(RimverseShip ship, int tick)
        {
            float integrity = ship.Integrity;
            if (integrity > FractureIntegrity || ship.map == null || ship.cells.Count < 30) return false;
            if (integrity > 0.001f && !Rand.Chance(0.25f)) return false;
            CellRect bounds = ShipMover.Bounds(ship);
            bool alongX = bounds.Width < bounds.Height;
            IntVec3 bridgeCell = ship.bridge != null ? ship.bridge.Position : bounds.CenterCell;
            int lo = alongX ? bounds.minZ + 2 : bounds.minX + 2;
            int hi = alongX ? bounds.maxZ - 2 : bounds.maxX - 2;
            if (hi - lo < 4) return false;
            int line = Rand.RangeInclusive(lo, hi);
            int bridgeLine = alongX ? bridgeCell.z : bridgeCell.x;
            if (Mathf.Abs(line - bridgeLine) < 3) line = bridgeLine + (line < bridgeLine ? -3 : 3);
            if (line < lo || line > hi) return false;
            Map map = ship.map;
            List<IntVec3> crack = new List<IntVec3>();
            foreach (IntVec3 c in ship.cells)
            {
                int v = alongX ? c.z : c.x;
                if (v == line || v == line + 1 && Rand.Chance(0.5f)) crack.Add(c);
            }
            if (crack.Count == 0) return false;
            foreach (IntVec3 c in crack)
            {
                foreach (Thing t in c.GetThingList(map).ToList())
                {
                    if (t is Pawn || t == ship.bridge || t.Destroyed) continue;
                    if (t.def.category == ThingCategory.Building || t.def.category == ThingCategory.Item && Rand.Chance(0.5f)) t.Destroy(DestroyMode.KillFinalize);
                }
                TerrainDef top = map.terrainGrid.TerrainAt(c);
                if (top?.GetModExtension<HullTerrainExtension>() != null && map.terrainGrid.CanRemoveTopLayerAt(c)) map.terrainGrid.RemoveTopLayer(c, false);
                if (Rand.Chance(0.35f)) FilthMaker.TryMakeFilth(c, map, ThingDefOf.Filth_RubbleBuilding);
            }
            map.GetComponent<MapComponent_RimverseShips>()?.Dirty();
            Find.LetterStack.ReceiveLetter("Rimverse_LetterShipFractureLabel".Translate(), "Rimverse_LetterShipFracture".Translate(ship.Label), LetterDefOf.ThreatBig, new TargetInfo(crack[crack.Count / 2], map));
            return true;
        }
    }

    public static class ThrusterFuel
    {
        public const float FusionGasPerFuel = 0.05f;

        public static float ChemShare(RimverseShip ship, out float fusionShare)
        {
            float chem = 0f, fusion = 0f, total = 0f;
            fusionShare = 0f;
            foreach (CompShipPart part in ship.PartsWithRole(ShipPartRole.Thruster))
            {
                float thrust = part.Props.thrust;
                if (thrust <= 0f) continue;
                total += thrust;
                CompRefuelable own = part.parent.TryGetComp<CompRefuelable>();
                CompPowerTrader power = part.parent.TryGetComp<CompPowerTrader>();
                if (own != null)
                {
                    if (own.Fuel > 0f) fusion += thrust;
                    else chem += thrust;
                }
                else if (power != null && power.Props.PowerConsumption > 0f) { }
                else chem += thrust;
            }
            if (total <= 0f) return 1f;
            fusionShare = fusion / total;
            return chem / total;
        }

        public static float Factor(RimverseShip ship) => ChemShare(ship, out _);

        public static void BurnFusionGas(RimverseShip ship, float baseFuel)
        {
            ChemShare(ship, out float fusionShare);
            float need = baseFuel * fusionShare * FusionGasPerFuel;
            foreach (CompShipPart part in ship.PartsWithRole(ShipPartRole.Thruster))
            {
                if (need <= 0f) return;
                CompRefuelable own = part.parent.TryGetComp<CompRefuelable>();
                if (own == null || own.Fuel <= 0f) continue;
                float take = Mathf.Min(own.Fuel, need);
                own.ConsumeFuel(take);
                need -= take;
            }
        }
    }
}
