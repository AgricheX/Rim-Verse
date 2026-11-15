using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public static class ColonyUtility
    {
        public const int MinSettlers = 2;
        public const int SteelCost = 300;
        public const int MealCost = 30;
        public const float WarnAttention = 500f;
        public const float AttackAttention = 600f;

        public static int Cost(ColonyIndustry t)
        {
            switch (t)
            {
                case ColonyIndustry.Agriculture: return 2000;
                case ColonyIndustry.Mining: return 3000;
                case ColonyIndustry.GasHarvest: return 4000;
                case ColonyIndustry.Refinery: return 4000;
                case ColonyIndustry.TradePort: return 5000;
                case ColonyIndustry.Factory: return 6000;
                case ColonyIndustry.MilitaryBase: return 8000;
                default: return 12000;
            }
        }

        public static int Days(ColonyIndustry t)
        {
            switch (t)
            {
                case ColonyIndustry.Agriculture: return 20;
                case ColonyIndustry.Factory: return 45;
                case ColonyIndustry.MilitaryBase: return 60;
                case ColonyIndustry.Shipyard: return 90;
                default: return 30;
            }
        }

        public static float Upkeep(ColonyIndustry t)
        {
            switch (t)
            {
                case ColonyIndustry.Agriculture: return 100f;
                case ColonyIndustry.Mining: return 150f;
                case ColonyIndustry.GasHarvest: return 200f;
                case ColonyIndustry.Refinery: return 200f;
                case ColonyIndustry.TradePort: return 150f;
                case ColonyIndustry.Factory: return 300f;
                case ColonyIndustry.MilitaryBase: return 400f;
                default: return 600f;
            }
        }

        public static float Hazard(BodyData b)
        {
            if (b == null) return 1f;
            float h = 1f + Mathf.Abs(b.gravity - 1f) / 0.2f * 0.1f;
            h += Mathf.Clamp01((Mathf.Abs(b.temperatureC - 15f) - 25f) / 100f) * 0.5f;
            switch (b.atmosphere)
            {
                case AtmosphereType.Thin: h += 0.25f; break;
                case AtmosphereType.Dense: h += 0.5f; break;
                case AtmosphereType.Toxic: h += 0.75f; break;
                case AtmosphereType.None: h += 0.5f; break;
            }
            if (b.atmosphere == AtmosphereType.Breathable) h += Mathf.Min(0.25f, GenStep_AlienFauna.SpeciesFor(b).Count * 0.025f);
            return h;
        }

        public static RimverseShip PlayerShipOn(Map map) => map?.GetComponent<MapComponent_RimverseShips>()?.Ships.FirstOrDefault(s => s.bridge != null && s.bridge.Faction == Faction.OfPlayer);

        public static AcceptanceReport CanFound(Map map)
        {
            ResearchProjectDef research = DefDatabase<ResearchProjectDef>.GetNamedSilentFail("Rimverse_ColonyAdministration");
            if (research != null && !research.IsFinished) return "Rimverse_ColonyNeedResearch".Translate(research.LabelCap);
            MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(map);
            if (loc == null || loc.Traveling) return "Rimverse_ColonyNeedOrbit".Translate();
            BodyData b = loc.Body;
            if (b == null || b.homeWorld || !b.Landable) return "Rimverse_ColonyBadBody".Translate();
            GameComponent_PlayerColonies gc = GameComponent_PlayerColonies.Instance;
            if (gc != null && gc.ColonyAt(loc.SystemId.Key, b.index) != null) return "Rimverse_ColonyAlreadyThere".Translate();
            if (GameComponent_Universe.Instance.GetScan(loc.SystemId, b.index) < ScanLevel.Exact) return "Rimverse_ColonyNeedScan".Translate();
            if (CountOnMap(map, t => t == ThingDefOf.Steel) < SteelCost) return "Rimverse_ColonyNeedSteel".Translate(SteelCost);
            if (CountOnMap(map, t => t.IsIngestible && t.ingestible.IsMeal) < MealCost) return "Rimverse_ColonyNeedMeals".Translate(MealCost);
            if (map.mapPawns.FreeColonistsSpawned.Count <= MinSettlers) return "Rimverse_ColonyNeedColonists".Translate(MinSettlers + 1);
            return true;
        }

        public static int CountOnMap(Map map, System.Func<ThingDef, bool> pred)
        {
            int n = 0;
            foreach (Thing t in map.listerThings.AllThings)
            {
                if (!t.Spawned || t.IsForbidden(Faction.OfPlayer) || !pred(t.def) || t.def.category != ThingCategory.Item) continue;
                n += t.stackCount;
            }
            return n;
        }

        public static bool Consume(Map map, System.Func<ThingDef, bool> pred, int count)
        {
            if (CountOnMap(map, pred) < count) return false;
            foreach (Thing t in map.listerThings.AllThings.Where(t => t.Spawned && !t.IsForbidden(Faction.OfPlayer) && t.def.category == ThingCategory.Item && pred(t.def)).ToList())
            {
                if (count <= 0) break;
                int take = Mathf.Min(count, t.stackCount);
                t.SplitOff(take).Destroy();
                count -= take;
            }
            return true;
        }

        public static void Found(Map map, BodyData body, List<Pawn> settlers)
        {
            GameComponent_PlayerColonies gc = GameComponent_PlayerColonies.Instance;
            MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(map);
            if (gc == null || loc == null || body == null || settlers.Count < MinSettlers) return;
            Consume(map, t => t == ThingDefOf.Steel, SteelCost);
            Consume(map, t => t.IsIngestible && t.ingestible.IsMeal, MealCost);
            PlayerColony c = new PlayerColony
            {
                id = gc.nextId++,
                name = body.name,
                systemKey = loc.SystemId.Key,
                bodyIndex = body.index,
                foundedTick = Find.TickManager.TicksGame,
                population = settlers.Count * 5f,
                nextStepTick = Find.TickManager.TicksGame + RimverseUnits.TicksPerQuadrum
            };
            foreach (Pawn p in settlers)
            {
                if (p.Spawned) p.DeSpawn();
                if (!Find.WorldPawns.Contains(p)) Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.KeepForever);
                c.settlers.Add(p);
            }
            gc.colonies.Add(c);
            Find.LetterStack.ReceiveLetter("Rimverse_LetterColonyFoundedLabel".Translate(), "Rimverse_LetterColonyFounded".Translate(c.name, settlers.Count), LetterDefOf.PositiveEvent);
        }

        public static Map PlayerMapInSystem(string systemKey) => FleetLogic.PlayerMapIn(systemKey);

        public static IntVec3 DeliverySpot(Map map)
        {
            RimverseShip ship = PlayerShipOn(map);
            if (ship != null) return ship.bridge.InteractionCell;
            return DropCellFinder.TradeDropSpot(map);
        }

        public static void Deliver(Map map, List<Thing> things)
        {
            if (things.Count == 0) return;
            IntVec3 spot = DeliverySpot(map);
            if (PlayerShipOn(map) != null || SpaceMapUtility.IsSpace(map))
            {
                foreach (Thing t in things) GenPlace.TryPlaceThing(t, spot, map, ThingPlaceMode.Near);
            }
            else DropPodUtility.DropThingsNear(spot, map, things, 110, false, false, true);
        }

        public static int MoneyOnMap(Map map)
        {
            return CountOnMap(map, t => t == ThingDefOf.Silver) + CountOnMap(map, t => t.defName == "Rimverse_OrbitalCredit") * 10 + CountOnMap(map, t => t.defName == "Rimverse_StellarMark") * 100;
        }

        public static int DepositAll(Map map)
        {
            int silver = CountOnMap(map, t => t == ThingDefOf.Silver);
            int credits = CountOnMap(map, t => t.defName == "Rimverse_OrbitalCredit");
            int marks = CountOnMap(map, t => t.defName == "Rimverse_StellarMark");
            Consume(map, t => t == ThingDefOf.Silver, silver);
            Consume(map, t => t.defName == "Rimverse_OrbitalCredit", credits);
            Consume(map, t => t.defName == "Rimverse_StellarMark", marks);
            return silver + credits * 10 + marks * 100;
        }

        public static void Withdraw(Map map, int amount)
        {
            List<Thing> things = new List<Thing>();
            ThingDef mark = DefDatabase<ThingDef>.GetNamedSilentFail("Rimverse_StellarMark");
            ThingDef credit = DefDatabase<ThingDef>.GetNamedSilentFail("Rimverse_OrbitalCredit");
            int marks = mark != null ? amount / 100 : 0;
            int rest = amount - marks * 100;
            int credits = credit != null ? rest / 10 : 0;
            int silver = rest - credits * 10;
            AddStacks(things, mark, marks);
            AddStacks(things, credit, credits);
            AddStacks(things, ThingDefOf.Silver, silver);
            Deliver(map, things);
        }

        public static void AddStacks(List<Thing> list, ThingDef def, int count)
        {
            if (def == null) return;
            while (count > 0)
            {
                Thing t = ThingMaker.MakeThing(def);
                t.stackCount = Mathf.Min(count, def.stackLimit);
                count -= t.stackCount;
                list.Add(t);
            }
        }

        public static int ColonyLimit()
        {
            Pawn leader = PawnsFinder.AllMaps_FreeColonists.Where(p => p.skills != null).OrderByDescending(p => p.skills.GetSkill(SkillDefOf.Social).Level).FirstOrDefault();
            int social = leader?.skills.GetSkill(SkillDefOf.Social).Level ?? 0;
            return 3 + social / 5;
        }
    }
}
