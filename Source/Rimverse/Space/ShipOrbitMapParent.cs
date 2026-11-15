using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Rimverse
{
    public class ShipOrbitMapParent : MapParent
    {
        public PlanetTile surfaceTile = PlanetTile.Invalid;
        public string shipName;

        public override string Label => shipName.NullOrEmpty() ? base.Label : shipName;

        public override MapGeneratorDef MapGeneratorDef => RimverseDefOf.Rimverse_OrbitMap;

        public override bool ShouldRemoveMapNow(out bool alsoRemoveWorldObject)
        {
            alsoRemoveWorldObject = true;
            if (!HasMap) return true;
            if (Map.mapPawns.AnyPawnBlockingMapRemoval) return false;
            MapComponent_RimverseShips ships = Map.GetComponent<MapComponent_RimverseShips>();
            if (ships != null && ships.Ships.Any(s => s.bridge != null && s.bridge.Faction == Faction.OfPlayer)) return false;
            return true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref surfaceTile, "surfaceTile", PlanetTile.Invalid);
            Scribe_Values.Look(ref shipName, "shipName");
        }
    }
}
