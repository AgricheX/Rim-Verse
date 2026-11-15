using System.Linq;
using RimWorld;
using Verse;

namespace Rimverse
{
    public class IncidentWorker_ShipAmbush : IncidentWorker
    {
        private static RimverseShip PlayerShip(Map map)
        {
            if (map == null || GameComponent_RimverseExtras.EscortActive || !SpaceMapUtility.IsRimverseSpace(map) || !SpaceMapUtility.IsOrbit(map)) return null;
            MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(map);
            if (loc == null || loc.Traveling) return null;
            return map.GetComponent<MapComponent_RimverseShips>()?.Ships.FirstOrDefault(s => s.bridge != null && s.bridge.Faction == Faction.OfPlayer);
        }

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!RimverseMod.Settings.shipCombatEnabled) return false;
            Map map = parms.target as Map;
            MapComponent_ShipCombat comp = MapComponent_ShipCombat.For(map);
            if (comp == null || comp.InCombat) return false;
            return PlayerShip(map) != null;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = parms.target as Map;
            RimverseShip ship = PlayerShip(map);
            if (ship == null) return false;
            Faction f = parms.faction != null && parms.faction.HostileTo(Faction.OfPlayer) ? parms.faction : ShipCombatUtility.PirateFaction(GameComponent_Civilizations.Instance);
            if (f == null) return false;
            float points = parms.points > 0f ? parms.points : StorytellerUtility.DefaultThreatPointsNow(map);
            if (!Rand.Chance(UnityEngine.Mathf.Lerp(0.3f, 1f, UnityEngine.Mathf.Clamp01(ship.Signature))))
            {
                Messages.Message("Rimverse_MsgSignatureHid".Translate(ship.Label), ship.bridge, MessageTypeDefOf.PositiveEvent, false);
                return true;
            }
            TechLevel tech = f.def.techLevel < TechLevel.Industrial ? TechLevel.Industrial : f.def.techLevel;
            return ShipCombatUtility.StartEncounter(map, ship, f, points / 8f, tech, -2);
        }
    }
}
