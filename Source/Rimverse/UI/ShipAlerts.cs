using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Rimverse
{
    public abstract class Alert_RimverseShip : Alert
    {
        private readonly List<GlobalTargetInfo> targets = new List<GlobalTargetInfo>();
        private readonly List<string> names = new List<string>();

        protected abstract bool Matches(RimverseShip ship);

        protected abstract string ExplanationKey { get; }

        public override AlertReport GetReport()
        {
            targets.Clear();
            names.Clear();
            List<Map> maps = Find.Maps;
            for (int i = 0; i < maps.Count; i++)
            {
                MapComponent_RimverseShips comp = maps[i].GetComponent<MapComponent_RimverseShips>();
                if (comp == null) continue;
                IReadOnlyList<RimverseShip> ships = comp.Ships;
                for (int j = 0; j < ships.Count; j++)
                {
                    RimverseShip ship = ships[j];
                    if (ship.bridge == null || ship.bridge.Faction != Faction.OfPlayer || !Matches(ship)) continue;
                    targets.Add(ship.bridge);
                    names.Add(ship.Label);
                }
            }
            return AlertReport.CulpritsAre(targets);
        }

        public override TaggedString GetExplanation() => ExplanationKey.Translate(string.Join(", ", names));
    }

    public class Alert_ShipAirLow : Alert_RimverseShip
    {
        public Alert_ShipAirLow()
        {
            defaultLabel = "Rimverse_AlertAirLow".Translate();
            defaultPriority = AlertPriority.Critical;
        }

        protected override string ExplanationKey => "Rimverse_AlertAirLowDesc";

        protected override bool Matches(RimverseShip ship) => SpaceMapUtility.RimverseHandlesAir(ship.map) && ship.crewCount > 0 && ship.AirFraction < 0.3f;
    }

    public class Alert_ShipHeatHigh : Alert_RimverseShip
    {
        public Alert_ShipHeatHigh()
        {
            defaultLabel = "Rimverse_AlertHeatHigh".Translate();
            defaultPriority = AlertPriority.High;
        }

        protected override string ExplanationKey => "Rimverse_AlertHeatHighDesc";

        protected override bool Matches(RimverseShip ship) => ship.HeatFraction >= 0.7f;
    }

    public class Alert_ShipIntegrityLow : Alert_RimverseShip
    {
        public Alert_ShipIntegrityLow()
        {
            defaultLabel = "Rimverse_AlertIntegrityLow".Translate();
            defaultPriority = AlertPriority.High;
        }

        protected override string ExplanationKey => "Rimverse_AlertIntegrityLowDesc";

        protected override bool Matches(RimverseShip ship) => ship.Integrity < 0.6f;
    }

    public class Alert_ShipOverLimit : Alert_RimverseShip
    {
        public Alert_ShipOverLimit()
        {
            defaultLabel = "Rimverse_AlertOverLimit".Translate();
            defaultPriority = AlertPriority.Medium;
        }

        protected override string ExplanationKey => "Rimverse_AlertOverLimitDesc";

        protected override bool Matches(RimverseShip ship) => ship.overLimit || ship.multipleBridges;
    }
}
