using System.Collections.Generic;
using System.Text;
using RimWorld;
using Verse;

namespace Rimverse
{
    public class CompShipPart : ThingComp
    {
        public int shipId = -1;
        public bool heatShutdown;

        private CompPowerTrader power;
        private CompFlickable flick;
        private CompRefuelable fuel;
        private CompBreakdownable breakdown;

        public CompProperties_ShipPart Props => (CompProperties_ShipPart)props;

        public ShipPartRole Role => Props.role;

        public bool PoweredOk => power == null || power.PowerOn;

        public bool Operating
        {
            get
            {
                if (!parent.Spawned || heatShutdown) return false;
                if (flick != null && !flick.SwitchIsOn) return false;
                if (breakdown != null && breakdown.BrokenDown) return false;
                if (!PoweredOk) return false;
                if (fuel != null && !fuel.HasFuel) return false;
                return true;
            }
        }

        public float Efficiency
        {
            get
            {
                if (!Operating) return 0f;
                if (parent.def.useHitPoints && parent.HitPoints < parent.MaxHitPoints * 0.5f) return 0.5f;
                return 1f;
            }
        }

        public float CurrentHullPoints
        {
            get
            {
                if (Props.hullPoints <= 0f) return 0f;
                if (!parent.def.useHitPoints || parent.MaxHitPoints <= 0) return Props.hullPoints;
                return Props.hullPoints * parent.HitPoints / parent.MaxHitPoints;
            }
        }

        public MapComponent_RimverseShips ShipsComp => parent.MapHeld?.GetComponent<MapComponent_RimverseShips>();

        public RimverseShip Ship => shipId < 0 ? null : ShipsComp?.ShipById(shipId);

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            power = parent.TryGetComp<CompPowerTrader>();
            flick = parent.TryGetComp<CompFlickable>();
            fuel = parent.TryGetComp<CompRefuelable>();
            breakdown = parent.TryGetComp<CompBreakdownable>();
            MapComponent_RimverseShips comp = ShipsComp;
            if (comp != null)
            {
                comp.Dirty();
                if (!respawningAfterLoad && Props.hullPoints > 0f)
                {
                    comp.NotifyHullRebuilt(parent.Position, Props.hullPoints);
                }
            }
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            base.PostDeSpawn(map, mode);
            map?.GetComponent<MapComponent_RimverseShips>()?.Dirty();
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            if (previousMap == null) return;
            MapComponent_RimverseShips comp = previousMap.GetComponent<MapComponent_RimverseShips>();
            if (comp == null) return;
            comp.Dirty();
            if (mode == DestroyMode.KillFinalize && Props.hullPoints > 0f && shipId >= 0)
            {
                comp.NotifyHullLost(shipId, parent.Position, Props.hullPoints);
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo g in base.CompGetGizmosExtra())
            {
                yield return g;
            }
            if (Role != ShipPartRole.Bridge || parent.Faction != Faction.OfPlayer || !parent.Spawned) yield break;
            RimverseShip ship = Ship;
            if (ship == null || ship.bridge != parent) yield break;
            yield return new Command_Action
            {
                defaultLabel = "Rimverse_CmdSystemMap".Translate(),
                defaultDesc = "Rimverse_CmdSystemMapDesc".Translate(),
                icon = RimverseTex.ShipPanel,
                action = () => Find.WindowStack.Add(new Window_SystemMap(parent.Map))
            };
            yield return new Command_Action
            {
                defaultLabel = "Rimverse_CmdGalaxyMap".Translate(),
                defaultDesc = "Rimverse_CmdGalaxyMapDesc".Translate(),
                icon = RimverseTex.ShipPanel,
                action = () => Find.WindowStack.Add(new Window_GalaxyMap(parent.Map))
            };
            foreach (Gizmo g in ShipCombatUtility.Gizmos(ship)) yield return g;
            foreach (Gizmo g in GateUtility.Gizmos(ship)) yield return g;
            foreach (Gizmo g in AnomalyContent.BridgeGizmos(ship)) yield return g;
            foreach (Gizmo g in OdysseyCompat.BridgeGizmos(ship)) yield return g;
            if (SpaceMapUtility.IsOrbit(parent.Map))
            {
                BodyData orbitBody = MapComponent_ShipLocation.For(parent.Map)?.Body;
                if (orbitBody != null && !orbitBody.homeWorld && orbitBody.Landable)
                {
                    Command_Action found = new Command_Action
                    {
                        defaultLabel = "Rimverse_CmdFoundColony".Translate(),
                        defaultDesc = "Rimverse_CmdFoundColonyDesc".Translate(),
                        icon = RimverseTex.Launch,
                        action = () => Find.WindowStack.Add(new Dialog_FoundColony(parent.Map, orbitBody))
                    };
                    AcceptanceReport rep = ColonyUtility.CanFound(parent.Map);
                    if (!rep.Accepted) found.Disable(rep.Reason);
                    yield return found;
                }
            }
            if (SpaceMapUtility.IsRimverseSpace(parent.Map) && SpaceMapUtility.IsOrbit(parent.Map))
            {
                Command_Action land = new Command_Action
                {
                    defaultLabel = "Rimverse_CmdLand".Translate(),
                    defaultDesc = "Rimverse_CmdLandDesc".Translate(ShipLaunchUtility.LandingFuelCost(ship, ShipLaunchUtility.LandingMap(parent.Map, out _)).ToString("0")),
                    icon = RimverseTex.Land,
                    action = () => ShipLaunchUtility.TryLand(ship)
                };
                yield return land;
            }
            else if (!SpaceMapUtility.IsOrbit(parent.Map))
            {
                Command_Action launch = new Command_Action
                {
                    defaultLabel = "Rimverse_CmdLaunch".Translate(),
                    defaultDesc = "Rimverse_CmdLaunchDesc".Translate(ShipLaunchUtility.LaunchFuelCost(ship).ToString("0")),
                    icon = RimverseTex.Launch,
                    action = () => ShipLaunchUtility.TryLaunchToOrbit(ship)
                };
                yield return launch;
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref heatShutdown, "heatShutdown", false);
        }

        public override string CompInspectStringExtra()
        {
            StringBuilder sb = new StringBuilder();
            RimverseShip ship = Ship;
            if (ship != null)
            {
                sb.Append("Rimverse_PartOfShip".Translate(ship.Label));
            }
            else if (Props.role != ShipPartRole.HullWall && Props.role != ShipPartRole.Window)
            {
                sb.Append("Rimverse_NotPartOfShip".Translate());
            }
            if (heatShutdown)
            {
                if (sb.Length > 0) sb.AppendLine();
                sb.Append("Rimverse_HeatShutdown".Translate());
            }
            return sb.ToString();
        }
    }
}
