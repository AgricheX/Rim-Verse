using Verse;
namespace Rimverse
{
    public static class ShipClassifier
    {
        public const int CommandBridgeCells = 1200;
        public const int CapitalBridgeCells = 4000;
        public const float ArmoredWallPoints = 150f;
        public const int HeavyWarpRange = 20;

        public static ShipClass Classify(RimverseShip ship)
        {
            if (ship.bridge == null) return ShipClass.None;
            int bridgeCells = ship.bridge.TryGetComp<CompShipPart>()?.Props.maxCells ?? 0;
            bool hasThruster = ship.HasRole(ShipPartRole.Thruster);
            bool hasWarp = ship.HasRole(ShipPartRole.WarpDrive);
            if (!hasThruster && !hasWarp) return ShipClass.Station;
            int weapons = ship.CountRole(ShipPartRole.Weapon);
            bool heavyWarp = false;
            foreach (CompShipPart warp in ship.PartsWithRole(ShipPartRole.WarpDrive))
            {
                if (warp.Props.warpRangeLy >= HeavyWarpRange) heavyWarp = true;
            }
            if (bridgeCells >= CapitalBridgeCells && heavyWarp && weapons >= 6) return ShipClass.Capital;
            if (bridgeCells >= CommandBridgeCells && ship.HasRole(ShipPartRole.Shield) && weapons >= 2 && ArmoredWallShare(ship) >= 0.3f) return ShipClass.Military;
            if (ship.HasRole(ShipPartRole.AirGenerator) || ship.HasRole(ShipPartRole.Garden)) return ShipClass.Civil;
            return ShipClass.Tin;
        }

        public static float ArmoredWallShare(RimverseShip ship)
        {
            int walls = 0;
            int armored = 0;
            foreach (CompShipPart wall in ship.PartsWithRole(ShipPartRole.HullWall))
            {
                walls++;
                if (wall.Props.hullPoints >= ArmoredWallPoints) armored++;
            }
            return walls == 0 ? 0f : armored / (float)walls;
        }

        public static bool CanInterstellarWarp(ShipClass c) => c == ShipClass.Civil || c == ShipClass.Military || c == ShipClass.Capital;

        public static bool CanUseHyperlanes(ShipClass c) => c == ShipClass.Capital;
    }
}
