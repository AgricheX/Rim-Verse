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
        private static readonly int[] WeaponCounts = { 1, 2, 3, 6, 6, 5, 14, 20, 18 };
        private static readonly int[] CrewCaps = { 1, 4, 6, 20, 16, 40, 80, 120, 100 };
        private static readonly int[] SizeClasses = { 1, 1, 2, 3, 3, 4, 4, 5, 5 };
        private static readonly int[] Thrusters = { 1, 1, 2, 3, 3, 4, 5, 6, 0 };

        private static readonly string[] IndustrialWeapons = { "Rimverse_Autocannon", "Rimverse_HullCannon", "Rimverse_MissileLauncher", "Rimverse_FlakBattery", "Rimverse_MineLayer" };
        private static readonly string[] SpacerWeapons = { "Rimverse_Autocannon", "Rimverse_HullCannon", "Rimverse_MissileLauncher", "Rimverse_LaserLance", "Rimverse_Railgun", "Rimverse_IonCannon", "Rimverse_BeamLaser", "Rimverse_RailBattery", "Rimverse_PulseMissileLauncher" };
        private static readonly string[] UltraWeapons = { "Rimverse_LaserLance", "Rimverse_PlasmaCannon", "Rimverse_Railgun", "Rimverse_MissileLauncher", "Rimverse_IonCannon", "Rimverse_PlasmaMortar", "Rimverse_TorpedoTube", "Rimverse_BeamLaser" };

        private static ThingDef Named(string defName) => DefDatabase<ThingDef>.GetNamedSilentFail(defName);

        public static int TierFor(float power) => power < 12f ? 0 : power < 25f ? 1 : power < 60f ? 2 : power < 100f ? 3 : power < 150f ? 4 : power < 400f ? 6 : 7;

        public static int MaxTierFor(Civilization civ, Faction faction)
        {
            if (faction != null && (faction.def.permanentEnemy || faction.def.defName.Contains("Pirate"))) return 5;
            if (civ == null) return 7;
            if (civ.stage <= 9) return 3;
            if (civ.stage == 10) return 4;
            if (civ.stage == 11) return 6;
            return 8;
        }

        public static int ClassFor(float power, Civilization civ, Faction faction)
        {
            int tier = TierFor(power);
            if (tier == 6 && Rand.Chance(0.3f)) tier = 5;
            if (tier >= 6 && faction != null && !faction.def.permanentEnemy && Rand.Chance(0.15f)) tier = 8;
            return Mathf.Min(tier, MaxTierFor(civ, faction));
        }

        public static BlueprintStyle StyleFor(Faction faction, TechLevel tech)
        {
            if (faction == null) return BlueprintStyle.Civil;
            if (faction == Faction.OfMechanoids) return BlueprintStyle.Mechanoid;
            if (faction.def.defName.Contains("Empire")) return BlueprintStyle.Imperial;
            if (faction.def.permanentEnemy || faction.def.defName.Contains("Pirate")) return BlueprintStyle.Pirate;
            if (tech >= TechLevel.Spacer) return BlueprintStyle.Military;
            return BlueprintStyle.Civil;
        }

        public static BlueprintRequest RequestFor(int tier, TechLevel tech, Faction faction, float power)
        {
            BlueprintStyle style = StyleFor(faction, tech);
            BlueprintRequest req = new BlueprintRequest
            {
                style = style,
                sizeClass = SizeClasses[tier],
                armored = tier >= 6 && tech >= TechLevel.Spacer || tier == 8,
                seed = Rand.Int
            };
            ThingDef bridgeDef = Named(tier >= 6 ? "Rimverse_BridgeCapital" : tier >= 3 ? "Rimverse_BridgeCommand" : "Rimverse_BridgeSmall") ?? Named("Rimverse_BridgeSmall");
            List<ThingDef> weapons = PickWeapons(tier, tech, faction);
            float powerNeed = Consumption(bridgeDef) + weapons.Sum(Consumption) + Consumption(Named("Rimverse_AirGenerator"));
            bool shield = tier >= 3 && tech >= TechLevel.Spacer;
            if (shield) powerNeed += Consumption(Named("Rimverse_ShieldGenerator"));
            int cells = Mathf.CeilToInt(powerNeed / 1000f) + 1;
            if (bridgeDef != null) req.Add(bridgeDef.defName);
            if (Thrusters[tier] > 0) req.Add(tier >= 6 && tech >= TechLevel.Spacer ? "Rimverse_FusionThruster" : "Rimverse_ChemThruster", Thrusters[tier]);
            if (shield && (tier >= 7 || tier == 8)) req.Add("Rimverse_ShieldGenerator");
            if (tier == 5) req.Add("Rimverse_Hangar", 2);
            if ((tier == 6 || tier == 7) && tech >= TechLevel.Spacer) req.Add("Rimverse_SpinalCannon");
            if (tier == 8) req.Add("Rimverse_SecurityCentral");
            req.Add("Rimverse_FuelCell", cells);
            req.Add("Rimverse_AirGenerator").Add("Rimverse_AirTank");
            if (shield) req.Add("Rimverse_ShieldGenerator");
            foreach (ThingDef w in weapons) req.Add(w.defName);
            EnemyRoster.Flavor(req, tier, tech);
            if (tier >= 1) req.Add("Rimverse_ChemTank");
            req.Add("Rimverse_RadiatorFixed", Mathf.Min(10, tier + 2));
            if (tier != 8) req.Add("Rimverse_ManeuverThruster", Mathf.Clamp(tier + 1, 1, 6));
            if (tier >= 1) req.Add("Rimverse_Decoy");
            if (tier >= 2) req.Add("Rimverse_ReactiveArmor");
            if (tier >= 2 && tech >= TechLevel.Spacer) req.Add("Rimverse_EmergencyHeatSink");
            if (tier >= 3 && tech >= TechLevel.Spacer && Rand.Chance(0.5f)) req.Add("Rimverse_SignatureCloak");
            if (style != BlueprintStyle.Mechanoid)
            {
                req.Add("Bed", Mathf.Clamp(CrewCaps[tier] / 2, 2, 20));
                if (tier >= 1) req.Add("Rimverse_CargoBay");
            }
            return req;
        }

        public static RimverseShip Generate(Map map, RimverseShip player, Faction faction, float power, TechLevel tech, out int crewCount)
        {
            crewCount = 0;
            if (map == null || player == null || faction == null) return null;
            Civilization civ = GameComponent_Civilizations.Instance?.generated == true ? Currencies.CivOf(faction) : null;
            int tier = ClassFor(power, civ, faction);
            ShipBlueprint bp = null;
            CellRect rect = default;
            for (; tier >= 0; tier = tier == 8 ? 6 : tier == 6 ? 4 : tier - 1)
            {
                bp = ShipBlueprintGenerator.Generate(RequestFor(tier, tech, faction, power));
                if (bp != null && TryFindRect(map, player, bp.width, bp.height, out rect)) break;
                bp = null;
            }
            if (bp == null) return null;
            BlueprintBuildResult res = ShipBlueprintBuilder.Build(map, rect, bp, new BlueprintBuildOptions { faction = faction, clearArea = false });
            if (res == null) return null;
            Building bridge = res.bridge;
            if (bridge == null)
            {
                ShipBlueprintBuilder.Clear(map, rect);
                return null;
            }
            MapComponent_RimverseShips ships = map.GetComponent<MapComponent_RimverseShips>();
            ships.Dirty();
            RimverseShip ship = ships.ShipById(bridge.thingIDNumber);
            if (ship == null)
            {
                ShipBlueprintBuilder.Clear(map, rect);
                return null;
            }
            ship.state.airStock = ship.airCapacity;
            ship.state.name = "Rimverse_EnemyShipName".Translate(faction.Name, ("Rimverse_EnemyClass_" + tier).Translate());
            List<Pawn> crew = GenerateCrew(map, faction, power, tier, res.freeCells.Count);
            EnemyRoster.Name(ship, faction, crew.FirstOrDefault(), tier);
            int i = 0;
            foreach (Pawn p in crew)
            {
                RoomRole role = i++ % 3 == 0 ? RoomRole.Bridge : i % 3 == 1 ? RoomRole.Quarters : RoomRole.Corridor;
                IntVec3 cell = res.RandomCellIn(role, bridge.InteractionCell);
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
                ThingDef pd = Named(Rand.Chance(0.5f) ? "Rimverse_PointLaser" : "Rimverse_FlakBattery");
                if (pd != null) list.Add(pd);
            }
            if (tier >= 1 && faction != null && faction.def.permanentEnemy)
            {
                ThingDef tractor = Named(Rand.Chance(0.5f) ? "Rimverse_TractorBeam" : "Rimverse_TowHarpoon");
                if (tractor != null && Rand.Chance(0.5f)) list.Add(tractor);
            }
            if (tier >= 2 && tech >= TechLevel.Spacer && Rand.Chance(0.4f))
            {
                ThingDef jam = Named("Rimverse_Jammer");
                if (jam != null) list.Add(jam);
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
                points = Mathf.Clamp(power * 6f, 150f, 12000f),
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
            if (SpaceMapUtility.IsSpace(map)) foreach (Pawn cp in crew) if (Rand.Chance(0.6f)) SpaceSuits.Equip(cp);
            while (crew.Count > cap)
            {
                Pawn extra = crew[crew.Count - 1];
                crew.RemoveAt(crew.Count - 1);
                Find.WorldPawns.PassToWorld(extra, PawnDiscardDecideMode.Discard);
            }
            return crew;
        }

        internal static bool TryFindRect(Map map, RimverseShip player, int w, int h, out CellRect result, int[] gaps = null)
        {
            CellRect pb = ShipMover.Bounds(player);
            IntVec3 pc = pb.CenterCell;
            foreach (int gap in gaps ?? new[] { 35, 25, 18, 12 })
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
