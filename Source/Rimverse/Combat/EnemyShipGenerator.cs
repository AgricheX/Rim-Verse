using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace Rimverse
{
    public static class EnemyShipGenerator
    {
        private static readonly int[] Widths = { 11, 15, 21, 27 };
        private static readonly int[] Heights = { 9, 9, 13, 15 };
        private static readonly int[] WeaponCounts = { 1, 2, 4, 6 };
        private static readonly int[] CrewCaps = { 4, 6, 10, 16 };

        private static readonly string[] IndustrialWeapons = { "Rimverse_Autocannon", "Rimverse_HullCannon", "Rimverse_MissileLauncher" };
        private static readonly string[] SpacerWeapons = { "Rimverse_Autocannon", "Rimverse_HullCannon", "Rimverse_MissileLauncher", "Rimverse_LaserLance", "Rimverse_Railgun", "Rimverse_IonCannon" };
        private static readonly string[] UltraWeapons = { "Rimverse_LaserLance", "Rimverse_PlasmaCannon", "Rimverse_Railgun", "Rimverse_MissileLauncher", "Rimverse_IonCannon" };

        private static ThingDef Named(string defName) => DefDatabase<ThingDef>.GetNamedSilentFail(defName);

        public static int TierFor(float power) => power < 25f ? 0 : power < 60f ? 1 : power < 150f ? 2 : 3;

        public static RimverseShip Generate(Map map, RimverseShip player, Faction faction, float power, TechLevel tech, out int crewCount)
        {
            crewCount = 0;
            if (map == null || player == null || faction == null) return null;
            int tier = TierFor(power);
            if (!TryFindRect(map, player, Widths[tier], Heights[tier], out CellRect rect)) return null;
            TerrainDef floor = DefDatabase<TerrainDef>.GetNamedSilentFail("Rimverse_HullFloorVacuum");
            ThingDef wall = Named(tier >= 3 && tech >= TechLevel.Spacer ? "Rimverse_HullWallArmored" : "Rimverse_HullWall");
            if (floor == null || wall == null) return null;
            foreach (IntVec3 c in rect) map.terrainGrid.SetFoundation(c, floor);
            CellRect inner = rect.ContractedBy(1);
            foreach (IntVec3 c in rect.EdgeCells) Spawn(map, wall, c, faction);
            foreach (IntVec3 c in inner) map.roofGrid.SetRoof(c, RoofDefOf.RoofConstructed);
            int corridorZ = inner.minZ + inner.Height / 2;
            HashSet<IntVec3> used = new HashSet<IntVec3>();
            ThingDef bridgeDef = Named(tier >= 2 ? "Rimverse_BridgeCommand" : "Rimverse_BridgeSmall");
            Building bridge = Place(map, bridgeDef, inner, corridorZ, used, faction) as Building;
            if (bridge == null)
            {
                bridgeDef = Named("Rimverse_BridgeSmall");
                bridge = Place(map, bridgeDef, inner, corridorZ, used, faction) as Building;
            }
            if (bridge == null)
            {
                Clear(map, rect);
                return null;
            }
            List<ThingDef> weapons = PickWeapons(tier, tech, faction);
            float powerNeed = Consumption(bridgeDef) + weapons.Sum(Consumption) + Consumption(Named("Rimverse_AirGenerator"));
            bool shield = tier >= 2 && tech >= TechLevel.Spacer;
            if (shield) powerNeed += Consumption(Named("Rimverse_ShieldGenerator"));
            int cells = Mathf.CeilToInt(powerNeed / 1000f) + 1;
            List<ThingDef> order = new List<ThingDef>();
            for (int i = 0; i <= tier; i++) order.Add(Named("Rimverse_ChemThruster"));
            for (int i = 0; i < cells; i++) order.Add(Named("Rimverse_FuelCell"));
            order.Add(Named("Rimverse_AirGenerator"));
            order.Add(Named("Rimverse_AirTank"));
            if (shield) order.Add(Named("Rimverse_ShieldGenerator"));
            order.AddRange(weapons);
            if (tier >= 1) order.Add(Named("Rimverse_ChemTank"));
            for (int i = 0; i < tier + 2; i++) order.Add(Named("Rimverse_RadiatorFixed"));
            for (int i = 0; i < Mathf.Min(4, tier + 1); i++) order.Add(Named("Rimverse_ManeuverThruster"));
            foreach (ThingDef def in order)
            {
                if (def == null) continue;
                Place(map, def, inner, corridorZ, used, faction);
            }
            ThingDef conduit = ThingDefOf.PowerConduit;
            foreach (IntVec3 c in inner)
            {
                if (used.Contains(c)) continue;
                Spawn(map, conduit, c, faction);
            }
            MapComponent_RimverseShips ships = map.GetComponent<MapComponent_RimverseShips>();
            ships.Dirty();
            RimverseShip ship = ships.ShipById(bridge.thingIDNumber);
            if (ship == null)
            {
                Clear(map, rect);
                return null;
            }
            ship.state.airStock = ship.airCapacity;
            ship.state.name = "Rimverse_EnemyShipName".Translate(faction.Name, ("Rimverse_EnemyTier_" + tier).Translate());
            List<IntVec3> free = inner.Cells.Where(c => !used.Contains(c) && c.Standable(map)).ToList();
            List<Pawn> crew = GenerateCrew(map, faction, power, tier, free.Count);
            foreach (Pawn p in crew)
            {
                IntVec3 cell = free.Count > 0 ? free.RandomElement() : bridge.InteractionCell;
                GenSpawn.Spawn(p, cell, map);
            }
            if (crew.Count > 0)
            {
                LordMaker.MakeNewLord(faction, new LordJob_DefendPoint(bridge.Position, 6f, 10f, false, false), map, crew);
            }
            crewCount = crew.Count;
            return ship;
        }

        private static float Consumption(ThingDef def)
        {
            CompProperties_Power p = def?.GetCompProperties<CompProperties_Power>();
            if (p == null || p.compClass != typeof(CompPowerTrader)) return 0f;
            return Mathf.Max(0f, p.PowerConsumption);
        }

        private static List<ThingDef> PickWeapons(int tier, TechLevel tech, Faction faction)
        {
            string[] pool = tech >= TechLevel.Ultra ? UltraWeapons : tech >= TechLevel.Spacer ? SpacerWeapons : IndustrialWeapons;
            List<ThingDef> list = new List<ThingDef>();
            List<ThingDef> options = pool.Select(Named).Where(d => d != null).ToList();
            if (options.Count == 0) return list;
            for (int i = 0; i < WeaponCounts[tier]; i++) list.Add(options.RandomElement());
            if (tier >= 1 && tech >= TechLevel.Spacer)
            {
                ThingDef pd = Named("Rimverse_PointLaser");
                if (pd != null) list.Add(pd);
            }
            if (tier >= 1 && faction != null && faction.def.permanentEnemy)
            {
                ThingDef tractor = Named("Rimverse_TractorBeam");
                if (tractor != null && Rand.Chance(0.5f)) list.Add(tractor);
            }
            return list;
        }

        private static List<Pawn> GenerateCrew(Map map, Faction faction, float power, int tier, int freeCells)
        {
            int cap = Mathf.Min(CrewCaps[tier], Mathf.Max(1, freeCells / 2));
            List<Pawn> crew = new List<Pawn>();
            PawnGroupMakerParms parms = new PawnGroupMakerParms
            {
                groupKind = PawnGroupKindDefOf.Combat,
                tile = map.Tile,
                faction = faction,
                points = Mathf.Clamp(power * 6f, 150f, 4000f),
                generateFightersOnly = true
            };
            try
            {
                crew.AddRange(PawnGroupMakerUtility.GeneratePawns(parms, false));
            }
            catch (System.Exception ex)
            {
                RimverseLog.Warning("Enemy crew generation failed: " + ex.Message);
            }
            if (crew.Count == 0 && faction.def.basicMemberKind != null)
            {
                for (int i = 0; i < tier + 2; i++) crew.Add(PawnGenerator.GeneratePawn(faction.def.basicMemberKind, faction));
            }
            while (crew.Count > cap)
            {
                Pawn extra = crew[crew.Count - 1];
                crew.RemoveAt(crew.Count - 1);
                Find.WorldPawns.PassToWorld(extra, PawnDiscardDecideMode.Discard);
            }
            return crew;
        }

        private static Thing Spawn(Map map, ThingDef def, IntVec3 c, Faction faction)
        {
            Thing t = ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
            if (def.CanHaveFaction) t.SetFaction(faction);
            return GenSpawn.Spawn(t, c, map, Rot4.North);
        }

        private static Thing Place(Map map, ThingDef def, CellRect inner, int corridorZ, HashSet<IntVec3> used, Faction faction)
        {
            if (def == null) return null;
            foreach (IntVec3 center in Candidates(inner))
            {
                CellRect occ = GenAdj.OccupiedRect(center, Rot4.North, def.size);
                if (!Fits(occ, inner, corridorZ, used)) continue;
                Thing t = Spawn(map, def, center, faction);
                foreach (IntVec3 c in occ) used.Add(c);
                Prepare(t);
                return t;
            }
            return null;
        }

        private static IEnumerable<IntVec3> Candidates(CellRect inner)
        {
            for (int x = inner.minX; x <= inner.maxX; x++)
            {
                for (int z = inner.minZ; z <= inner.maxZ; z++) yield return new IntVec3(x, 0, z);
            }
        }

        private static bool Fits(CellRect occ, CellRect inner, int corridorZ, HashSet<IntVec3> used)
        {
            if (occ.minX < inner.minX || occ.maxX > inner.maxX || occ.minZ < inner.minZ || occ.maxZ > inner.maxZ) return false;
            if (occ.minZ <= corridorZ && occ.maxZ >= corridorZ) return false;
            foreach (IntVec3 c in occ) if (used.Contains(c)) return false;
            return true;
        }

        private static void Prepare(Thing t)
        {
            CompRefuelable fuel = t.TryGetComp<CompRefuelable>();
            if (fuel != null)
            {
                float missing = fuel.Props.fuelCapacity - fuel.Fuel;
                if (missing > 0f) fuel.Refuel(missing);
            }
            CompShipShield shield = t.TryGetComp<CompShipShield>();
            if (shield != null) shield.hitPoints = shield.Props.maxHitPoints;
            CompFlickable flick = t.TryGetComp<CompFlickable>();
            if (flick != null) flick.SwitchIsOn = true;
        }

        private static void Clear(Map map, CellRect rect)
        {
            foreach (IntVec3 c in rect)
            {
                foreach (Thing t in c.GetThingList(map).ToList())
                {
                    if (t.def.destroyable && !(t is Pawn)) t.Destroy(DestroyMode.Vanish);
                }
                map.roofGrid.SetRoof(c, null);
                if (map.terrainGrid.FoundationAt(c) != null) map.terrainGrid.RemoveFoundation(c, false);
            }
            map.GetComponent<MapComponent_RimverseShips>()?.Dirty();
        }

        internal static bool TryFindRect(Map map, RimverseShip player, int w, int h, out CellRect result)
        {
            CellRect pb = ShipMover.Bounds(player);
            IntVec3 pc = pb.CenterCell;
            foreach (int gap in new[] { 35, 25, 18, 12 })
            {
                List<IntVec3> origins = new List<IntVec3>
                {
                    new IntVec3(pb.maxX + gap, 0, pc.z - h / 2),
                    new IntVec3(pb.minX - gap - w + 1, 0, pc.z - h / 2),
                    new IntVec3(pc.x - w / 2, 0, pb.maxZ + gap),
                    new IntVec3(pc.x - w / 2, 0, pb.minZ - gap - h + 1)
                };
                foreach (IntVec3 o in origins.InRandomOrder())
                {
                    CellRect r = new CellRect(o.x, o.z, w, h);
                    if (AreaFree(map, r))
                    {
                        result = r;
                        return true;
                    }
                }
            }
            result = default;
            return false;
        }

        private static bool AreaFree(Map map, CellRect r)
        {
            foreach (IntVec3 c in r.ExpandedBy(2))
            {
                if (!c.InBounds(map) || c.InNoBuildEdgeArea(map)) return false;
                if (map.terrainGrid.FoundationAt(c) != null) return false;
                if (!HullUtility.IsVacuumTerrain(map, c)) return false;
                if (c.GetFirstBuilding(map) != null || c.GetFirstPawn(map) != null) return false;
            }
            return true;
        }
    }
}
