using UnityEngine;
using Verse;

namespace Rimverse
{
    public static class GravityUtility
    {
        public static bool ArtificialGravity(RimverseShip ship) => ship != null && ship.gravityCells > 0 && ship.gravityCells >= ship.cells.Count;

        public static float GravityAt(Map map, IntVec3 cell)
        {
            if (map == null) return 1f;
            float g = ShipLaunchUtility.Gravity(map);
            MapComponent_RimverseShips comp = map.GetComponent<MapComponent_RimverseShips>();
            RimverseShip ship = comp != null && cell.IsValid ? comp.ShipAt(cell) : null;
            if (ArtificialGravity(ship)) return Mathf.Max(1f, g);
            return g;
        }

        public static float GravityAt(Pawn pawn) => GravityAt(pawn?.MapHeld, pawn?.PositionHeld ?? IntVec3.Invalid);
    }
}
