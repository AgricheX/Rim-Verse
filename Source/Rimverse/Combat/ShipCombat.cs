using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace Rimverse
{
    public enum CombatOrder
    {
        Approach,
        Hold,
        Flee,
        Board,
        KeepRange
    }

    public class ShipEncounter : IExposable
    {
        public int playerShipId;
        public int enemyShipId;
        public CombatBand band = CombatBand.Far;
        public float bandProgress;
        public CombatOrder playerOrder = CombatOrder.Hold;
        public CombatOrder enemyOrder = CombatOrder.Approach;
        public CombatBand enemyPreferred = CombatBand.Medium;
        public float playerFlee;
        public float enemyFlee;
        public int civId = -2;
        public Faction enemyFaction;
        public int startTick;
        public int lastBoardTick;
        public bool militarist;
        public int initialEnemyCrew;

        public void ExposeData()
        {
            Scribe_Values.Look(ref playerShipId, "playerShipId");
            Scribe_Values.Look(ref enemyShipId, "enemyShipId");
            Scribe_Values.Look(ref band, "band");
            Scribe_Values.Look(ref bandProgress, "bandProgress");
            Scribe_Values.Look(ref playerOrder, "playerOrder", CombatOrder.Hold);
            Scribe_Values.Look(ref enemyOrder, "enemyOrder", CombatOrder.Approach);
            Scribe_Values.Look(ref enemyPreferred, "enemyPreferred", CombatBand.Medium);
            Scribe_Values.Look(ref playerFlee, "playerFlee");
            Scribe_Values.Look(ref enemyFlee, "enemyFlee");
            Scribe_Values.Look(ref civId, "civId", -2);
            Scribe_References.Look(ref enemyFaction, "enemyFaction");
            Scribe_Values.Look(ref startTick, "startTick");
            Scribe_Values.Look(ref lastBoardTick, "lastBoardTick");
            Scribe_Values.Look(ref militarist, "militarist");
            Scribe_Values.Look(ref initialEnemyCrew, "initialEnemyCrew");
        }
    }

    [StaticConstructorOnStartup]
    public class MapComponent_ShipCombat : MapComponent
    {
        public List<ShipEncounter> encounters = new List<ShipEncounter>();
        public int lastAmbushCheckTick;

        public MapComponent_ShipCombat(Map map) : base(map)
        {
        }

        public static MapComponent_ShipCombat For(Map map) => map?.GetComponent<MapComponent_ShipCombat>();

        public ShipEncounter EncounterOf(RimverseShip ship)
        {
            if (ship == null) return null;
            foreach (ShipEncounter e in encounters) if (e.playerShipId == ship.id || e.enemyShipId == ship.id) return e;
            return null;
        }

        public bool InCombat => encounters.Count > 0;

        public override void MapComponentTick()
        {
            int tick = Find.TickManager.TicksGame;
            if (encounters.Count > 0 && tick % 60 == 17)
            {
                for (int i = encounters.Count - 1; i >= 0; i--)
                {
                    if (i >= encounters.Count) continue;
                    ShipCombatUtility.Process(this, encounters[i]);
                }
            }
            if (tick % RimverseUnits.TicksPerDay == 4321) ShipCombatUtility.MaybeAmbush(map, this);
        }

        public void End(ShipEncounter e)
        {
            encounters.Remove(e);
        }

        public override void MapComponentOnGUI()
        {
            if (encounters.Count == 0 || Find.CurrentMap != map || WorldRendererUtility.WorldSelected) return;
            ShipEncounter e = encounters[0];
            MapComponent_RimverseShips ships = map.GetComponent<MapComponent_RimverseShips>();
            RimverseShip enemy = ships?.ShipById(e.enemyShipId);
            RimverseShip mine = ships?.ShipById(e.playerShipId);
            Rect r = new Rect(UI.screenWidth / 2f - 260f, 6f, 520f, 64f);
            Widgets.DrawWindowBackground(r);
            Rect inner = r.ContractedBy(6f);
            Text.Font = GameFont.Small;
            string line1 = "Rimverse_CombatHud".Translate(("Rimverse_Band_" + e.band).Translate(), e.bandProgress.ToStringPercent(), ("Rimverse_Order_" + e.playerOrder).Translate(), ("Rimverse_Order_" + e.enemyOrder).Translate());
            string line2 = "Rimverse_CombatHud2".Translate(mine != null ? mine.Integrity.ToStringPercent() : "?", ShipCombatUtility.ShieldPoints(mine).ToString("0"), enemy != null ? enemy.Integrity.ToStringPercent() : "?", ShipCombatUtility.ShieldPoints(enemy).ToString("0"));
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 24f), line1);
            Widgets.Label(new Rect(inner.x, inner.y + 26f, inner.width, 24f), line2);
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref encounters, "encounters", LookMode.Deep);
            Scribe_Values.Look(ref lastAmbushCheckTick, "lastAmbushCheckTick");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && encounters == null) encounters = new List<ShipEncounter>();
        }
    }
}
