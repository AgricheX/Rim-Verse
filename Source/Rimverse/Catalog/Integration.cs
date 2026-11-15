using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Vehicles;
using Verse;

namespace Rimverse
{
    public enum CraftRole
    {
        Shuttle,
        Fighter,
        Bomber
    }

    public class CompProperties_SpaceCraft : CompProperties
    {
        public CraftRole role = CraftRole.Shuttle;
        public ThingDef projectile;
        public float damage;
        public int shots = 1;
        public float cooldownSeconds = 10f;
        public float interceptChance;
        public float fuelPerTrip = 10f;

        public CompProperties_SpaceCraft()
        {
            compClass = typeof(CompSpaceCraft);
        }
    }

    public class CompSpaceCraft : ThingComp
    {
        public int nextShotTick;

        public CompProperties_SpaceCraft Props => (CompProperties_SpaceCraft)props;

        public VehiclePawn Vehicle => parent as VehiclePawn;

        public bool Crewed => Vehicle != null && Vehicle.AllPawnsAboard.Any(p => !p.Dead && !p.Downed);

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo g in base.CompGetGizmosExtra()) yield return g;
            VehiclePawn v = Vehicle;
            if (v == null || !v.Spawned || v.Faction != Faction.OfPlayer) yield break;
            Command_Action fly = new Command_Action
            {
                defaultLabel = "Rimverse_CraftFly".Translate(),
                defaultDesc = "Rimverse_CraftFlyDesc".Translate(Props.fuelPerTrip.ToString("0")),
                icon = RimverseTex.Launch,
                action = OpenMenu
            };
            string reason = CannotFly();
            if (reason != null) fly.Disable(reason);
            yield return fly;
        }

        private string CannotFly()
        {
            VehiclePawn v = Vehicle;
            if (!Crewed) return "Rimverse_CraftNoCrew".Translate();
            CompFueledTravel fuel = v.CompFueledTravel;
            if (fuel != null && fuel.Fuel < Props.fuelPerTrip) return "Rimverse_CraftNoFuel".Translate();
            if (SpaceMapUtility.IsSpace(v.Map) && HangarUtility.HangarOf(v) == null) return "Rimverse_CraftNotInHangar".Translate();
            if (Destinations().Count == 0) return "Rimverse_CraftNoDestination".Translate();
            return null;
        }

        private List<Map> Destinations()
        {
            List<Map> list = new List<Map>();
            Map here = parent.Map;
            MapComponent_ShipLocation loc = here == null ? null : MapComponent_ShipLocation.For(here);
            if (loc == null || loc.Traveling) return list;
            string key = loc.SystemId.Key;
            foreach (Map m in Find.Maps)
            {
                if (m == here) continue;
                MapComponent_ShipLocation other = MapComponent_ShipLocation.For(m);
                if (other == null || other.Traveling || other.SystemId.Key != key) continue;
                if (!m.IsPlayerHome && !m.mapPawns.AnyColonistSpawned) continue;
                list.Add(m);
            }
            return list;
        }

        private void OpenMenu()
        {
            List<FloatMenuOption> opts = new List<FloatMenuOption>();
            foreach (Map m in Destinations())
            {
                Map dest = m;
                string label = dest.Parent?.LabelCap ?? dest.ToString();
                opts.Add(new FloatMenuOption(label, () => FlyTo(dest)));
            }
            if (opts.Count > 0) Find.WindowStack.Add(new FloatMenu(opts));
        }

        public void FlyTo(Map dest)
        {
            VehiclePawn v = Vehicle;
            if (v == null || !v.Spawned) return;
            if (!HangarUtility.TryFindLanding(v, dest, out IntVec3 cell))
            {
                Messages.Message("Rimverse_CraftNoLanding".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            v.CompFueledTravel?.ConsumeFuel(Props.fuelPerTrip);
            Map from = v.Map;
            FleckMaker.ThrowSmoke(v.DrawPos, from, 3f);
            Rot4 rot = v.Rotation;
            v.DeSpawn();
            GenSpawn.Spawn(v, cell, dest, rot);
            FleckMaker.ThrowDustPuff(cell, dest, 2f);
            Messages.Message("Rimverse_CraftArrived".Translate(v.LabelShort, dest.Parent?.LabelCap ?? ""), new TargetInfo(cell, dest), MessageTypeDefOf.PositiveEvent, false);
        }

        public override string CompInspectStringExtra()
        {
            if (Props.role == CraftRole.Shuttle) return null;
            CompHangar h = HangarUtility.HangarOf(Vehicle);
            return h != null && Crewed ? "Rimverse_CraftReady".Translate().ToString() : null;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref nextShotTick, "nextShotTick");
        }
    }

    public class CompProperties_Hangar : CompProperties
    {
        public int slots = 2;
        public float radius = 4f;
        public float repairPerRare = 6f;

        public CompProperties_Hangar()
        {
            compClass = typeof(CompHangar);
        }
    }

    public class CompHangar : ThingComp
    {
        public CompProperties_Hangar Props => (CompProperties_Hangar)props;

        public bool Active
        {
            get
            {
                if (!parent.Spawned) return false;
                CompShipPart part = parent.GetComp<CompShipPart>();
                if (part != null) return part.Operating;
                CompPowerTrader p = parent.GetComp<CompPowerTrader>();
                return p == null || p.PowerOn;
            }
        }

        public bool Covers(Thing t) => t.Spawned && t.Map == parent.Map && t.Position.InHorDistOf(parent.TrueCenter().ToIntVec3(), Props.radius + Mathf.Max(parent.def.size.x, parent.def.size.z) * 0.5f);

        public List<VehiclePawn> Parked()
        {
            List<VehiclePawn> list = new List<VehiclePawn>();
            if (!parent.Spawned) return list;
            foreach (Pawn p in parent.Map.mapPawns.AllPawnsSpawned)
            {
                if (p is VehiclePawn v && v.Faction == Faction.OfPlayer && Covers(v)) list.Add(v);
                if (list.Count >= Props.slots) break;
            }
            return list;
        }

        public override void CompTickRare()
        {
            base.CompTickRare();
            if (!Active) return;
            foreach (VehiclePawn v in Parked())
            {
                VehicleComponent worst = v.statHandler?.components?.Where(c => c.HealthPercent < 1f).OrderBy(c => c.HealthPercent).FirstOrDefault();
                worst?.HealComponent(Props.repairPerRare);
                CompFueledTravel fuel = v.CompFueledTravel;
                if (fuel == null || fuel.FullTank || fuel.Props.fuelType == null) continue;
                Thing stock = GenRadial.RadialDistinctThingsAround(parent.Position, parent.Map, Props.radius + 6f, true).FirstOrDefault(t => t.def == fuel.Props.fuelType && !t.IsForbidden(Faction.OfPlayer));
                if (stock == null) continue;
                int take = Mathf.Min(stock.stackCount, Mathf.CeilToInt(fuel.TargetFuelLevel - fuel.Fuel), 10);
                if (take <= 0) continue;
                stock.SplitOff(take).Destroy();
                fuel.Refuel(take);
            }
        }

        public override string CompInspectStringExtra()
        {
            List<VehiclePawn> parked = Parked();
            int fighters = parked.Count(v => v.GetComp<CompSpaceCraft>() is CompSpaceCraft c && c.Props.role != CraftRole.Shuttle && c.Crewed);
            return "Rimverse_HangarInspect".Translate(parked.Count, Props.slots, fighters);
        }
    }

    public static class HangarUtility
    {
        public static IEnumerable<CompHangar> Hangars(RimverseShip ship)
        {
            if (ship == null) yield break;
            foreach (CompShipPart p in ship.parts)
            {
                CompHangar h = p.parent.GetComp<CompHangar>();
                if (h != null) yield return h;
            }
        }

        public static CompHangar HangarOf(VehiclePawn v)
        {
            if (v == null || !v.Spawned) return null;
            foreach (Building b in v.Map.listerBuildings.allBuildingsColonist)
            {
                CompHangar h = b.GetComp<CompHangar>();
                if (h != null && h.Covers(v)) return h;
            }
            return null;
        }

        public static List<CompSpaceCraft> ReadyCraft(RimverseShip ship)
        {
            List<CompSpaceCraft> list = new List<CompSpaceCraft>();
            foreach (CompHangar h in Hangars(ship))
            {
                if (!h.Active) continue;
                foreach (VehiclePawn v in h.Parked())
                {
                    CompSpaceCraft c = v.GetComp<CompSpaceCraft>();
                    if (c != null && c.Props.role != CraftRole.Shuttle && c.Crewed) list.Add(c);
                }
            }
            return list;
        }

        public static float FighterIntercept(RimverseShip target)
        {
            if (target?.bridge == null || target.bridge.Faction != Faction.OfPlayer) return 0f;
            float chance = 0f;
            foreach (CompSpaceCraft c in ReadyCraft(target)) chance += c.Props.interceptChance;
            return Mathf.Min(0.6f, chance);
        }

        public static void FighterSorties(Map map, ShipEncounter e, RimverseShip player, RimverseShip enemy)
        {
            if (player == null || enemy == null) return;
            int tick = Find.TickManager.TicksGame;
            float dodge = ShipCombatUtility.Dodge(enemy);
            float jam = ShipCombatUtility.Jamming(enemy);
            foreach (CompSpaceCraft c in ReadyCraft(player))
            {
                CompProperties_SpaceCraft p = c.Props;
                if (p.projectile == null || tick < c.nextShotTick) continue;
                if (p.role == CraftRole.Fighter && e.band == CombatBand.Far) continue;
                c.nextShotTick = tick + Mathf.RoundToInt(p.cooldownSeconds * 60f);
                float hit = Mathf.Clamp01(0.65f * (1f - dodge) * (1f - jam));
                for (int i = 0; i < p.shots; i++)
                {
                    IntVec3 aim = ShipCombatUtility.RandomCell(enemy);
                    if (!aim.IsValid) return;
                    float remaining = p.damage;
                    foreach (CompShipShield s in ShipCombatUtility.Shields(enemy))
                    {
                        if (remaining <= 0f) break;
                        remaining = s.Absorb(remaining);
                    }
                    if (remaining <= 0.01f) continue;
                    if (!Rand.Chance(hit)) continue;
                    ShipCombatUtility.Launch(map, p.projectile, c.parent, player, aim);
                }
            }
        }

        public static bool TryFindLanding(VehiclePawn v, Map dest, out IntVec3 cell)
        {
            IntVec2 size = v.def.size;
            System.Func<IntVec3, bool> ok = c =>
            {
                CellRect r = GenAdj.OccupiedRect(c, v.Rotation, size);
                foreach (IntVec3 x in r)
                {
                    if (!x.InBounds(dest) || !x.Standable(dest) || x.GetEdifice(dest) != null || x.GetFirstPawn(dest) != null) return false;
                }
                return true;
            };
            RimverseShip ship = ColonyUtility.PlayerShipOn(dest);
            foreach (CompHangar h in Hangars(ship))
            {
                if (!h.Active || h.Parked().Count >= h.Props.slots) continue;
                IntVec3 center = h.parent.Position;
                foreach (IntVec3 c in GenRadial.RadialCellsAround(center, h.Props.radius + 2f, true))
                {
                    if (ok(c))
                    {
                        cell = c;
                        return true;
                    }
                }
            }
            IntVec3 spot = DropCellFinder.TradeDropSpot(dest);
            foreach (IntVec3 c in GenRadial.RadialCellsAround(spot, 25f, true))
            {
                if (ok(c))
                {
                    cell = c;
                    return true;
                }
            }
            cell = IntVec3.Invalid;
            return false;
        }
    }

    public class CompProperties_DockingPort : CompProperties
    {
        public float traderMtbDays = 10f;

        public CompProperties_DockingPort()
        {
            compClass = typeof(CompDockingPort);
        }
    }

    public class CompDockingPort : ThingComp
    {
        public CompProperties_DockingPort Props => (CompProperties_DockingPort)props;

        public override void CompTickRare()
        {
            base.CompTickRare();
            if (!parent.Spawned || parent.Faction != Faction.OfPlayer) return;
            CompPowerTrader power = parent.GetComp<CompPowerTrader>();
            if (power != null && !power.PowerOn) return;
            Map map = parent.Map;
            if (!SpaceMapUtility.IsSpace(map) && !SpaceMapUtility.IsOrbit(map)) return;
            if (!Rand.MTBEventOccurs(Props.traderMtbDays, 60000f, 250f)) return;
            if (map.passingShipManager.passingShips.Count >= 5) return;
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.Misc, map);
            parms.traderKind = DefDatabase<TraderKindDef>.GetNamedSilentFail("Rimverse_SpaceGoodsTrader");
            IncidentDefOf.OrbitalTraderArrival.Worker.TryExecute(parms);
        }

        public override string CompInspectStringExtra() => "Rimverse_DockInspect".Translate(Props.traderMtbDays.ToString("0"));
    }

    public class CompProperties_StatusPanel : CompProperties
    {
        public CompProperties_StatusPanel()
        {
            compClass = typeof(CompStatusPanel);
        }
    }

    public class CompStatusPanel : ThingComp
    {
        private RimverseShip Ship()
        {
            MapComponent_RimverseShips comp = parent.Map?.GetComponent<MapComponent_RimverseShips>();
            if (comp == null) return null;
            RimverseShip s = comp.ShipAt(parent.Position);
            if (s != null) return s;
            foreach (IntVec3 c in GenAdj.CellsAdjacent8Way(parent))
            {
                if (!c.InBounds(parent.Map)) continue;
                s = comp.ShipAt(c);
                if (s != null) return s;
            }
            return null;
        }

        public override string CompInspectStringExtra()
        {
            if (!parent.Spawned) return null;
            CompPowerTrader power = parent.GetComp<CompPowerTrader>();
            if (power != null && !power.PowerOn) return null;
            RimverseShip s = Ship();
            if (s?.state == null) return "Rimverse_PanelNoShip".Translate();
            return "Rimverse_PanelInspect".Translate(s.Label, s.Integrity.ToStringPercent(), s.AirFraction.ToStringPercent(), s.HeatFraction.ToStringPercent(), ShipLaunchUtility.FuelAvailable(s).ToString("0"), s.crewCount) + "\n" + "Rimverse_ShipSignature".Translate(s.Signature.ToStringPercent());
        }
    }

    public class CompProperties_DroneWorkshop : CompProperties
    {
        public float radius = 30f;
        public float factor = 1.5f;

        public CompProperties_DroneWorkshop()
        {
            compClass = typeof(CompDroneWorkshop);
        }
    }

    public class CompDroneWorkshop : ThingComp
    {
        public CompProperties_DroneWorkshop Props => (CompProperties_DroneWorkshop)props;

        public bool Active
        {
            get
            {
                if (!parent.Spawned) return false;
                CompPowerTrader p = parent.GetComp<CompPowerTrader>();
                return p == null || p.PowerOn;
            }
        }

        public override string CompInspectStringExtra() => "Rimverse_DroneWorkshopInspect".Translate(Props.radius.ToString("0"), (Props.factor - 1f).ToStringPercent());
    }

    public static class DroneWorkshopUtility
    {
        public static float Factor(Thing bay)
        {
            if (bay?.Map == null) return 1f;
            float best = 1f;
            foreach (Building b in bay.Map.listerBuildings.allBuildingsColonist)
            {
                if (b == bay) continue;
                CompDroneWorkshop w = b.GetComp<CompDroneWorkshop>();
                if (w == null || !w.Active || !b.Position.InHorDistOf(bay.Position, w.Props.radius)) continue;
                if (w.Props.factor > best) best = w.Props.factor;
            }
            return best;
        }
    }

    public class CompProperties_StationLink : CompProperties
    {
        public int colonyLimit;
        public float colonyStability;
        public int colonySteel;
        public int colonyChemfuel;
        public int colonyComponents;
        public float colonyIncomeFactor;
        public float colonyDefense;
        public float colonyCapacity;
        public float colonyFood;
        public float civOpinion;
        public float attentionFactor = 1f;

        public CompProperties_StationLink()
        {
            compClass = typeof(CompStationLink);
        }
    }

    public class CompStationLink : ThingComp
    {
        public CompProperties_StationLink Props => (CompProperties_StationLink)props;

        public bool Active
        {
            get
            {
                if (!parent.Spawned || parent.Faction != Faction.OfPlayer) return false;
                CompPowerTrader p = parent.GetComp<CompPowerTrader>();
                return p == null || p.PowerOn;
            }
        }

        public override string CompInspectStringExtra()
        {
            MapComponent_ShipLocation loc = parent.Map == null ? null : MapComponent_ShipLocation.For(parent.Map);
            int n = 0;
            if (loc != null && GameComponent_PlayerColonies.Instance != null) n = GameComponent_PlayerColonies.Instance.colonies.Count(c => c.systemKey == loc.SystemId.Key);
            List<string> parts = new List<string> { "Rimverse_StationLinked".Translate(n) };
            CompProperties_StationLink p = Props;
            if (p.colonyIncomeFactor != 0f) parts.Add("Rimverse_LinkIncome".Translate(p.colonyIncomeFactor.ToStringPercent()));
            if (p.colonyStability != 0f) parts.Add("Rimverse_LinkStability".Translate(p.colonyStability.ToString("0")));
            if (p.colonyDefense != 0f) parts.Add("Rimverse_LinkDefense".Translate(p.colonyDefense.ToString("0")));
            if (p.colonyCapacity != 0f) parts.Add("Rimverse_LinkCapacity".Translate(p.colonyCapacity.ToString("0")));
            if (p.colonySteel + p.colonyChemfuel + p.colonyComponents > 0) parts.Add("Rimverse_LinkGoods".Translate(p.colonySteel, p.colonyChemfuel, p.colonyComponents));
            if (p.colonyLimit != 0) parts.Add("Rimverse_LinkLimit".Translate(p.colonyLimit));
            if (p.civOpinion != 0f) parts.Add("Rimverse_LinkOpinion".Translate(p.civOpinion.ToString("+0;-0")));
            return string.Join("\n", parts);
        }
    }

    public class StationBonus
    {
        public int limit;
        public float stability;
        public int steel;
        public int chemfuel;
        public int components;
        public float income;
        public float defense;
        public float capacity;
        public float food;
        public float opinion;
        public float attention = 1f;
    }

    public static class StationLinkUtility
    {
        public static IEnumerable<CompStationLink> Links(Map map)
        {
            foreach (Building b in map.listerBuildings.allBuildingsColonist)
            {
                CompStationLink l = b.GetComp<CompStationLink>();
                if (l != null && l.Active) yield return l;
            }
        }

        public static StationBonus For(string systemKey)
        {
            StationBonus s = new StationBonus();
            foreach (Map m in Find.Maps)
            {
                MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(m);
                if (loc == null || loc.Traveling || loc.SystemId.Key != systemKey) continue;
                foreach (CompStationLink l in Links(m))
                {
                    CompProperties_StationLink p = l.Props;
                    s.stability += p.colonyStability;
                    s.steel += p.colonySteel;
                    s.chemfuel += p.colonyChemfuel;
                    s.components += p.colonyComponents;
                    s.income += p.colonyIncomeFactor;
                    s.defense += p.colonyDefense;
                    s.capacity += p.colonyCapacity;
                    s.food += p.colonyFood;
                    s.opinion += p.civOpinion;
                    s.attention *= p.attentionFactor;
                }
            }
            return s;
        }

        public static int ExtraColonyLimit()
        {
            int n = 0;
            foreach (Map m in Find.Maps)
            {
                foreach (CompStationLink l in Links(m)) n += l.Props.colonyLimit;
            }
            return n;
        }
    }

    public class StudyExtension : DefModExtension
    {
        public float research = 500f;
    }

    public class RecipeWorker_Study : RecipeWorker
    {
        public override void Notify_IterationCompleted(Pawn billDoer, List<Thing> ingredients)
        {
            base.Notify_IterationCompleted(billDoer, ingredients);
            float amount = recipe.GetModExtension<StudyExtension>()?.research ?? 0f;
            ResearchProjectDef proj = Find.ResearchManager.GetProject();
            if (proj == null || amount <= 0f)
            {
                Messages.Message("Rimverse_StudyNoProject".Translate(), billDoer, MessageTypeDefOf.RejectInput, false);
                return;
            }
            Find.ResearchManager.AddProgress(proj, amount, billDoer);
            Messages.Message("Rimverse_StudyDone".Translate(amount.ToString("0"), proj.LabelCap), billDoer, MessageTypeDefOf.PositiveEvent, false);
        }
    }
}
