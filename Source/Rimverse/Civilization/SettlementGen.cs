using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace Rimverse
{
    public enum LotKind
    {
        Residential,
        Market,
        Park,
        Farm,
        Mess,
        Medical,
        Industrial,
        Storage,
        Admin,
        Research,
        Barracks,
        Shield,
        Mine,
        Plaza
    }

    public static class SettlementGen
    {
        private static GenStepDef def;

        public static GenStepDef Def => def ?? (def = DefDatabase<GenStepDef>.GetNamedSilentFail("Rimverse_SurfaceSettlement"));

        public static Civilization CivOn(string systemKey, int body, out CivPlanet planet)
        {
            planet = null;
            GameComponent_Civilizations gc = GameComponent_Civilizations.Instance;
            if (gc == null) return null;
            foreach (Civilization c in gc.Alive)
            {
                CivPlanet p = c.planets.FirstOrDefault(x => x.systemKey == systemKey && x.body == body);
                if (p != null)
                {
                    planet = p;
                    return c;
                }
            }
            return null;
        }

        public static PlayerColony ColonyOn(string systemKey, int body) => GameComponent_PlayerColonies.Instance?.ColonyAt(systemKey, body);

        public static List<GenStepWithParams> ExtraFor(StarSystemId sys, BodyData body)
        {
            List<GenStepWithParams> list = new List<GenStepWithParams>();
            if (Def == null || body == null) return list;
            if (CivOn(sys.Key, body.index, out _) != null || ColonyOn(sys.Key, body.index) != null) list.Add(new GenStepWithParams(Def, default(GenStepParams)));
            return list;
        }
    }

    public class GenStep_SurfaceSettlement : GenStep
    {
        public override int SeedPart => 772013449;

        private Map map;
        private Faction faction;
        private TechLevel tech;
        private System.Random rand;
        private ThingDef wallStuff;
        private TerrainDef road;
        private TerrainDef sidewalk;
        private ThingDef light;
        private bool colony;
        private Building_TradeTerminal terminal;
        private readonly List<CellRect> lots = new List<CellRect>();

        private static ThingDef Named(string n) => DefDatabase<ThingDef>.GetNamedSilentFail(n);

        private static TerrainDef T(params string[] names)
        {
            foreach (string n in names)
            {
                TerrainDef d = DefDatabase<TerrainDef>.GetNamedSilentFail(n);
                if (d != null) return d;
            }
            return null;
        }

        public override void Generate(Map map, GenStepParams parms)
        {
            this.map = map;
            BodyData body = BodyGenContext.current;
            string key = BodyGenContext.systemKey;
            if (body == null || key.NullOrEmpty()) return;
            Civilization civ = SettlementGen.CivOn(key, body.index, out CivPlanet planet);
            PlayerColony pc = civ == null ? SettlementGen.ColonyOn(key, body.index) : null;
            if (civ == null && pc == null) return;
            colony = pc != null;
            rand = new System.Random(key.GetHashCode() ^ body.index * 7919);
            faction = colony ? Faction.OfPlayer : GameComponent_Civilizations.Instance.EnsureFaction(civ);
            tech = colony ? TechLevel.Spacer : civ.tech;
            wallStuff = tech >= TechLevel.Ultra ? ThingDefOf.Plasteel : tech >= TechLevel.Spacer ? ThingDefOf.Steel : (Named("BlocksGranite") ?? ThingDefOf.Steel);
            road = T(tech >= TechLevel.Spacer ? "PavedTile" : "Concrete", "Concrete");
            sidewalk = T(tech >= TechLevel.Ultra ? "SterileTile" : "Concrete", "Concrete");
            light = Named("Rimverse_MegaLightPanel");
            float density;
            bool capital = !colony && civ.capitalKey == key;
            if (!colony) MapComponent_CityHorizon.Mark(map, civ.tech, capital);
            if (colony) density = Mathf.Clamp(0.25f + 0.04f * pc.facilities.Count, 0.25f, 0.7f);
            else
            {
                float fill = planet != null && planet.capacity > 0f ? planet.population / planet.capacity : 0.5f;
                density = Mathf.Clamp(0.3f + 0.5f * fill + (capital ? 0.3f : 0f), 0.3f, 0.97f);
            }
            int side = Mathf.RoundToInt(Mathf.Min(map.Size.x, map.Size.z) * Mathf.Sqrt(density));
            side = Mathf.Clamp(side, 40, Mathf.Min(map.Size.x, map.Size.z) - 12);
            CellRect area = CellRect.CenteredOn(map.Center, side / 2).ClipInsideMap(map).ContractedBy(2);
            Clear(area);
            Streets(area);
            List<LotKind> wanted = colony ? ColonyLots(pc) : CityLots(civ, capital);
            int i = 0;
            foreach (CellRect lot in lots.OrderBy(l => l.CenterCell.DistanceToSquared(map.Center)))
            {
                LotKind k = colony ? (i < wanted.Count ? wanted[i] : LotKind.Plaza) : wanted[rand.Next(wanted.Count)];
                bool built = !colony || i >= pc.facilities.Count || pc.facilities[i].Ready;
                if (!colony && capital && i == 0) k = LotKind.Admin;
                if (!colony && i == 1) k = LotKind.Market;
                Lot(lot, k, built);
                i++;
            }
            if (!colony) Populate(civ, area, capital);
            MapGenerator.PlayerStartSpot = CellFinder.TryFindRandomCellNear(new IntVec3(area.maxX + 6 < map.Size.x ? area.maxX + 6 : area.minX - 6, 0, map.Center.z), map, 10, c => c.Standable(map), out IntVec3 spot) ? spot : map.Center;
        }

        private List<LotKind> CityLots(Civilization civ, bool capital)
        {
            List<LotKind> l = new List<LotKind>();
            void Add(LotKind k, int n) { for (int i = 0; i < n; i++) l.Add(k); }
            Add(LotKind.Residential, 8);
            Add(LotKind.Market, 2);
            Add(LotKind.Mess, 2);
            Add(LotKind.Storage, 2);
            Add(LotKind.Industrial, tech <= TechLevel.Industrial ? 4 : 2);
            Add(LotKind.Park, tech >= TechLevel.Ultra ? 4 : 1);
            Add(LotKind.Medical, 1);
            Add(LotKind.Research, tech >= TechLevel.Spacer ? 2 : 0);
            Add(LotKind.Barracks, civ.Hostile ? 3 : 1);
            if (capital) Add(LotKind.Plaza, 2);
            return l;
        }

        private static List<LotKind> ColonyLots(PlayerColony pc)
        {
            List<LotKind> l = new List<LotKind>();
            foreach (ColonyFacility f in pc.facilities)
            {
                string n = f.def.defName;
                if (n.Contains("Habitat")) l.Add(LotKind.Residential);
                else if (n.Contains("Garrison") || n.Contains("Citadel")) l.Add(LotKind.Barracks);
                else if (n.Contains("Shield")) l.Add(LotKind.Shield);
                else if (n.Contains("Admin")) l.Add(LotKind.Admin);
                else if (n.Contains("Institute")) l.Add(LotKind.Research);
                else if (n.Contains("Trade")) l.Add(LotKind.Market);
                else if (n.Contains("Hydro") || n.Contains("Farmland")) l.Add(LotKind.Farm);
                else if (n.Contains("Medical")) l.Add(LotKind.Medical);
                else if (n.Contains("Entertainment")) l.Add(LotKind.Mess);
                else if (n.Contains("Sensor")) l.Add(LotKind.Research);
                else if (n.Contains("Mine") || n.Contains("Lumber")) l.Add(LotKind.Mine);
                else l.Add(LotKind.Industrial);
            }
            if (l.Count == 0) l.Add(LotKind.Residential);
            return l;
        }

        private void Clear(CellRect area)
        {
            foreach (IntVec3 c in area.ExpandedBy(2))
            {
                if (!c.InBounds(map)) continue;
                foreach (Thing t in c.GetThingList(map).ToList())
                {
                    if (t is Pawn || !t.def.destroyable) continue;
                    if (t.def.category == ThingCategory.Plant || t.def.category == ThingCategory.Building || t.def.category == ThingCategory.Item) t.Destroy(DestroyMode.Vanish);
                }
                map.roofGrid.SetRoof(c, null);
                TerrainDef ter = c.GetTerrain(map);
                if (ter.passability == Traversability.Impassable || ter.IsWater && !ter.IsRiver) map.terrainGrid.SetTerrain(c, T("Soil", "Gravel"));
            }
        }

        private void Streets(CellRect area)
        {
            const int period = 22;
            const int width = 3;
            for (int x = area.minX; x <= area.maxX; x++)
            {
                for (int z = area.minZ; z <= area.maxZ; z++)
                {
                    int lx = (x - area.minX) % period, lz = (z - area.minZ) % period;
                    bool street = lx < width || lz < width || x > area.maxX - width || z > area.maxZ - width;
                    if (!street) continue;
                    IntVec3 c = new IntVec3(x, 0, z);
                    map.terrainGrid.SetTerrain(c, road);
                    if ((lx == 1 && lz % 7 == 3 || lz == 1 && lx % 7 == 3) && light != null) ShipBlueprintBuilder.Spawn(map, light, c, null, Rot4.North, null);
                }
            }
            for (int x = area.minX + width; x < area.maxX - width; x += period)
            {
                for (int z = area.minZ + width; z < area.maxZ - width; z += period)
                {
                    int x2 = Mathf.Min(x + period - width - 1, area.maxX - width);
                    int z2 = Mathf.Min(z + period - width - 1, area.maxZ - width);
                    if (x2 - x < 8 || z2 - z < 8) continue;
                    CellRect block = CellRect.FromLimits(x, z, x2, z2);
                    foreach (IntVec3 c in block.EdgeCells) map.terrainGrid.SetTerrain(c, sidewalk);
                    CellRect inner = block.ContractedBy(1);
                    int split = rand.Next(3);
                    if (split == 0 || inner.Width < 14) lots.Add(inner);
                    else if (split == 1)
                    {
                        int mx = inner.minX + inner.Width / 2;
                        lots.Add(CellRect.FromLimits(inner.minX, inner.minZ, mx - 1, inner.maxZ));
                        lots.Add(CellRect.FromLimits(mx + 1, inner.minZ, inner.maxX, inner.maxZ));
                    }
                    else
                    {
                        int mx = inner.minX + inner.Width / 2, mz = inner.minZ + inner.Height / 2;
                        lots.Add(CellRect.FromLimits(inner.minX, inner.minZ, mx - 1, mz - 1));
                        lots.Add(CellRect.FromLimits(mx + 1, inner.minZ, inner.maxX, mz - 1));
                        lots.Add(CellRect.FromLimits(inner.minX, mz + 1, mx - 1, inner.maxZ));
                        lots.Add(CellRect.FromLimits(mx + 1, mz + 1, inner.maxX, inner.maxZ));
                    }
                }
            }
        }

        private TerrainDef FloorFor(LotKind k)
        {
            switch (k)
            {
                case LotKind.Residential: return tech >= TechLevel.Spacer ? T("CarpetCream", "CarpetRed", "WoodPlankFloor") : T("WoodPlankFloor", "Concrete");
                case LotKind.Medical:
                case LotKind.Research: return T("SterileTile", "Concrete");
                case LotKind.Admin: return tech >= TechLevel.Ultra ? T("SilverTile", "SterileTile") : T("CarpetRed", "PavedTile");
                case LotKind.Mess:
                case LotKind.Market: return T("PavedTile", "Concrete");
                case LotKind.Industrial:
                case LotKind.Storage:
                case LotKind.Barracks:
                case LotKind.Mine: return T("MetalTile", "Concrete");
                default: return T("Concrete");
            }
        }

        private Thing Put(ThingDef d, IntVec3 c, Rot4 rot, ThingDef stuff = null, Faction f = null)
        {
            if (d == null || !c.InBounds(map)) return null;
            CellRect occ = GenAdj.OccupiedRect(c, rot, d.size);
            if (!occ.InBounds(map)) return null;
            foreach (IntVec3 o in occ)
            {
                if (o.GetEdifice(map) != null || o.GetTerrain(map).IsWater) return null;
            }
            if (d.hasInteractionCell)
            {
                IntVec3 ic = ThingUtility.InteractionCellWhenAt(d, c, rot, map);
                if (!ic.InBounds(map) || ic.GetEdifice(map) != null) return null;
            }
            return ShipBlueprintBuilder.Spawn(map, d, c, f ?? (d.CanHaveFaction ? faction : null), rot, d.MadeFromStuff ? stuff ?? wallStuff : null);
        }

        private void Lot(CellRect r, LotKind k, bool built)
        {
            if (r.Width < 5 || r.Height < 5) return;
            if (k == LotKind.Park || k == LotKind.Plaza)
            {
                foreach (IntVec3 c in r)
                {
                    if (k == LotKind.Plaza)
                    {
                        map.terrainGrid.SetTerrain(c, sidewalk);
                        continue;
                    }
                    map.terrainGrid.SetTerrain(c, T("Soil"));
                    int v = rand.Next(100);
                    if (v < 7) Put(Named(v < 4 ? "Plant_TreeOak" : "Plant_TreePoplar"), c, Rot4.North)?.Let(t => ((Plant)t).Growth = 1f);
                    else if (v < 60) Put(ThingDefOf.Plant_Grass, c, Rot4.North)?.Let(t => ((Plant)t).Growth = 1f);
                }
                if (k == LotKind.Plaza) Put(Named("Rimverse_MegaLightPanel"), r.CenterCell, Rot4.North);
                return;
            }
            if (k == LotKind.Farm)
            {
                foreach (IntVec3 c in r)
                {
                    map.terrainGrid.SetTerrain(c, T("SoilRich", "Soil"));
                    if (built && (c.x - r.minX) % 4 != 0) Put(Named(tech >= TechLevel.Spacer ? "Plant_Rice" : "Plant_Potato"), c, Rot4.North)?.Let(t => ((Plant)t).Growth = Rand.Range(0.2f, 0.9f));
                }
                return;
            }
            TerrainDef floor = FloorFor(k);
            foreach (IntVec3 c in r) map.terrainGrid.SetTerrain(c, floor);
            if (!built)
            {
                foreach (IntVec3 c in r.EdgeCells) if ((c.x + c.z) % 3 == 0) Put(ThingDefOf.Wall, c, Rot4.North);
                return;
            }
            foreach (IntVec3 c in r.EdgeCells) Put(ThingDefOf.Wall, c, Rot4.North);
            foreach (IntVec3 c in r) map.roofGrid.SetRoof(c, RoofDefOf.RoofConstructed);
            foreach (IntVec3 dc in DoorCells(r)) DoorAt(dc);
            CellRect inner = r.ContractedBy(1);
            for (int x = inner.minX + 2; x <= inner.maxX; x += 5)
            {
                for (int z = inner.minZ + 2; z <= inner.maxZ; z += 5) Put(light, new IntVec3(x, 0, z), Rot4.North, null, null);
            }
            switch (k)
            {
                case LotKind.Residential:
                case LotKind.Barracks:
                    for (int x = inner.minX; x <= inner.maxX; x += 2)
                    {
                        if (k == LotKind.Residential && (x - inner.minX) % 6 == 4) continue;
                        Put(Named("Bed"), new IntVec3(x, 0, inner.maxZ - 1), Rot4.South);
                    }
                    Put(Named("Table2x2c"), new IntVec3(inner.CenterCell.x, 0, inner.minZ + 1), Rot4.North);
                    Put(Named("Dresser"), new IntVec3(inner.minX, 0, inner.minZ + 1), Rot4.East);
                    break;
                case LotKind.Market:
                    for (int x = inner.minX + 1; x <= inner.maxX - 1; x += 3)
                    {
                        for (int z = inner.minZ + 3; z <= inner.maxZ - 1; z += 3) Put(Named("Shelf"), new IntVec3(x, 0, z), Rot4.North);
                    }
                    if (!colony && terminal == null) terminal = Put(Named("Rimverse_TradeTerminal"), new IntVec3(inner.CenterCell.x, 0, inner.minZ + 1), Rot4.North) as Building_TradeTerminal;
                    break;
                case LotKind.Mess:
                    for (int x = inner.minX + 2; x <= inner.maxX - 2; x += 4)
                    {
                        for (int z = inner.minZ + 2; z <= inner.maxZ - 2; z += 4)
                        {
                            if (Put(Named("Table2x2c"), new IntVec3(x, 0, z), Rot4.North) == null) continue;
                            Put(Named("DiningChair"), new IntVec3(x - 1, 0, z), Rot4.East);
                            Put(Named("DiningChair"), new IntVec3(x + 2, 0, z), Rot4.West);
                        }
                    }
                    break;
                case LotKind.Medical:
                    for (int x = inner.minX + 1; x <= inner.maxX - 1; x += 3) Put(Named("HospitalBed"), new IntVec3(x, 0, inner.maxZ - 1), Rot4.South);
                    Put(Named("VitalsMonitor"), new IntVec3(inner.minX, 0, inner.minZ), Rot4.North);
                    break;
                case LotKind.Industrial:
                case LotKind.Mine:
                    Put(Named("TableMachining"), new IntVec3(inner.minX + 2, 0, inner.maxZ - 1), Rot4.South);
                    Put(Named("FabricationBench"), new IntVec3(inner.maxX - 2, 0, inner.maxZ - 1), Rot4.South);
                    Put(Named("ElectricSmelter"), new IntVec3(inner.CenterCell.x, 0, inner.minZ + 1), Rot4.North);
                    break;
                case LotKind.Storage:
                    for (int x = inner.minX; x <= inner.maxX - 1; x += 3)
                    {
                        for (int z = inner.minZ; z <= inner.maxZ - 1; z += 3) Put(Named("Shelf"), new IntVec3(x, 0, z), Rot4.North);
                    }
                    if (!colony) AnomalyContent.Loot(map, inner.Cells.InRandomOrder().Take(10).ToList(), 400f);
                    break;
                case LotKind.Admin:
                    Put(Named("CommsConsole"), new IntVec3(inner.CenterCell.x, 0, inner.maxZ - 1), Rot4.South);
                    Put(Named("Table2x2c"), inner.CenterCell, Rot4.North);
                    break;
                case LotKind.Research:
                    Put(Named("HiTechResearchBench") ?? Named("SimpleResearchBench"), new IntVec3(inner.CenterCell.x, 0, inner.maxZ - 1), Rot4.South);
                    Put(Named("Rimverse_Telescope"), new IntVec3(inner.minX + 1, 0, inner.minZ + 1), Rot4.North);
                    break;
                case LotKind.Shield:
                    Put(Named("Rimverse_ShieldGenerator"), inner.CenterCell, Rot4.North);
                    break;
            }
        }

        private IEnumerable<IntVec3> DoorCells(CellRect r)
        {
            IntVec3[] options = { new IntVec3(r.CenterCell.x, 0, r.minZ), new IntVec3(r.CenterCell.x, 0, r.maxZ), new IntVec3(r.minX, 0, r.CenterCell.z), new IntVec3(r.maxX, 0, r.CenterCell.z) };
            IntVec3[] outward = { IntVec3.South, IntVec3.North, IntVec3.West, IntVec3.East };
            int made = 0;
            for (int i = 0; i < 4 && made < 2; i++)
            {
                IntVec3 o = options[i] + outward[i] * 2;
                if (!o.InBounds(map) || o.GetTerrain(map) != road && o.GetTerrain(map) != sidewalk) continue;
                made++;
                yield return options[i];
            }
            if (made == 0) yield return options[0];
        }

        private void DoorAt(IntVec3 c)
        {
            Building e = c.GetEdifice(map);
            if (e != null) e.Destroy(DestroyMode.Vanish);
            ShipBlueprintBuilder.Spawn(map, ThingDefOf.Door, c, faction, Rot4.North, wallStuff);
        }

        private void Populate(Civilization civ, CellRect area, bool capital)
        {
            if (faction == null) return;
            List<Pawn> pawns = new List<Pawn>();
            try
            {
                pawns.AddRange(PawnGroupMakerUtility.GeneratePawns(new PawnGroupMakerParms
                {
                    groupKind = PawnGroupKindDefOf.Settlement,
                    tile = map.Tile,
                    faction = faction,
                    points = capital ? 2500f : 1200f
                }, false));
            }
            catch (System.Exception e)
            {
                RimverseLog.Warning("City population failed: " + e.Message);
            }
            if (pawns.Count == 0 && faction.def.basicMemberKind != null)
            {
                for (int i = 0; i < 6; i++) pawns.Add(PawnGenerator.GeneratePawn(faction.def.basicMemberKind, faction));
            }
            foreach (Pawn p in pawns)
            {
                if (CellFinder.TryFindRandomCellInsideWith(area, c => c.Standable(map) && !c.Fogged(map), out IntVec3 c2)) GenSpawn.Spawn(p, c2, map);
                else GenSpawn.Spawn(p, area.CenterCell, map);
            }
            if (pawns.Count > 0) LordMaker.MakeNewLord(faction, new LordJob_DefendBase(faction, area.CenterCell, 60000), map, pawns);
            if (terminal != null && !faction.HostileTo(Faction.OfPlayer))
            {
                TradeShip ts = new TradeShip(RimverseTraders.Pick(civ), faction) { name = civ.Name };
                map.passingShipManager.AddShip(ts);
                terminal.traderLoadId = ts.GetUniqueLoadID();
            }
        }
    }
}
