using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public static class ShipThoughts
    {
        public static ThoughtDef Def(string key) => DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimverse_Thought_" + key);

        public static void Give(Pawn p, string key)
        {
            ThoughtDef d = Def(key);
            if (d == null || p?.needs?.mood?.thoughts?.memories == null || p.Dead) return;
            p.needs.mood.thoughts.memories.TryGainMemory(d);
        }

        public static void GiveMap(Map map, string key, Pawn except = null)
        {
            if (map == null) return;
            foreach (Pawn p in map.mapPawns.FreeColonistsSpawned.ToList()) if (p != except) Give(p, key);
        }

        public static void GiveShip(RimverseShip ship, string key)
        {
            if (ship?.map == null) return;
            foreach (Pawn p in ship.map.mapPawns.FreeColonistsSpawned.ToList()) if (ship.cells.Contains(p.Position)) Give(p, key);
        }

        public static void Tale(string key, Pawn p)
        {
            TaleDef d = DefDatabase<TaleDef>.GetNamedSilentFail("Rimverse_Tale_" + key);
            if (d != null && p != null) TaleRecorder.RecordTale(d, p);
        }

        public static RimverseShip ShipOf(Pawn p)
        {
            if (p?.MapHeld == null) return null;
            MapComponent_RimverseShips comp = p.MapHeld.GetComponent<MapComponent_RimverseShips>();
            return comp?.ShipAt(p.PositionHeld);
        }

        public static bool InSpace(Pawn p) => p?.MapHeld != null && SpaceMapUtility.IsSpace(p.MapHeld);

        public static void VacuumFear(Pawn p)
        {
            if (p == null || CatalogTraits.Has(p, CatalogDefOf.Rimverse_VoidBorn)) return;
            Give(p, "VacuumFear");
        }

        public static void OnCryoArrived(Map map, float years)
        {
            if (map == null || years < 1f) return;
            foreach (Pawn p in map.mapPawns.FreeColonists.ToList())
            {
                Give(p, "WorldChanged");
                if (p.relations != null && p.relations.FamilyByBlood.Any(o => o != null && !o.Dead && o.MapHeld != map)) Give(p, "LeftFamily");
            }
        }
    }

    public class GameComponent_ShipThoughts : GameComponent
    {
        public HashSet<int> orbited = new HashSet<int>();
        public HashSet<string> worlds = new HashSet<string>();
        public Dictionary<int, int> awayFromHome = new Dictionary<int, int>();

        public static GameComponent_ShipThoughts Instance => Current.Game?.GetComponent<GameComponent_ShipThoughts>();

        public GameComponent_ShipThoughts(Game game)
        {
        }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % 2500 != 1234) return;
            GameComponent_Universe u = GameComponent_Universe.Instance;
            int now = Find.TickManager.TicksGame;
            foreach (Map map in Find.Maps)
            {
                MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(map);
                if (loc == null || loc.Traveling || loc.systemKey.NullOrEmpty()) continue;
                bool home = u != null && u.IsHome(loc.SystemId);
                foreach (Pawn p in map.mapPawns.FreeColonistsSpawned)
                {
                    int id = p.thingIDNumber;
                    if (loc.OnSurface)
                    {
                        string key = id + "|" + loc.systemKey + ":" + loc.bodyIndex;
                        if (!home && worlds.Add(key))
                        {
                            ShipThoughts.Give(p, "NewWorld");
                            ShipThoughts.Tale("NewWorld", p);
                        }
                    }
                    else if (SpaceMapUtility.IsSpace(map) && orbited.Add(id))
                    {
                        ShipThoughts.Give(p, "FirstOrbit");
                        ShipThoughts.Tale("FirstOrbit", p);
                    }
                    if (home)
                    {
                        if (awayFromHome.TryGetValue(id, out int since) && now - since > 30 * GenDate.TicksPerDay) ShipThoughts.Give(p, "HomeworldReturn");
                        awayFromHome.Remove(id);
                    }
                    else if (!awayFromHome.ContainsKey(id)) awayFromHome[id] = now;
                }
            }
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref orbited, "orbited", LookMode.Value);
            Scribe_Collections.Look(ref worlds, "worlds", LookMode.Value);
            Scribe_Collections.Look(ref awayFromHome, "awayFromHome", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (orbited == null) orbited = new HashSet<int>();
                if (worlds == null) worlds = new HashSet<string>();
                if (awayFromHome == null) awayFromHome = new Dictionary<int, int>();
            }
        }
    }

    public class ThoughtWorker_ShipConfined : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (!ShipThoughts.InSpace(p)) return false;
            RimverseShip s = ShipThoughts.ShipOf(p);
            if (s == null) return false;
            int crew = Mathf.Max(1, s.crewCount);
            return s.cells.Count / (float)crew < 16f;
        }
    }

    public class ThoughtWorker_ZeroGSleepNoBag : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            Building_Bed bed = p.ownership?.OwnedBed;
            if (bed == null || !bed.Spawned) return false;
            if (GravityUtility.GravityAt(bed.Map, bed.Position) >= LifeAboard.ZeroG) return false;
            return bed.def.defName != "Rimverse_ZeroGSleepingBag";
        }
    }

    public class ThoughtWorker_OwnCabinWindow : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            Building_Bed bed = p.ownership?.OwnedBed;
            if (bed == null || !bed.Spawned || ShipThoughts.ShipOf(p) == null && !ShipThoughts.InSpace(p)) return false;
            Room room = bed.GetRoom();
            if (room == null || room.PsychologicallyOutdoors || room.Owners.Count() != 1) return false;
            return room.ContainedAndAdjacentThings.Any(t => t.def.defName == "Rimverse_ObservationWindow" || t.def.defName == "Rimverse_HullViewport");
        }
    }

    public class ThoughtWorker_ShipTemperature : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            RimverseShip s = ShipThoughts.ShipOf(p);
            if (s == null || s.heatCapacity <= 0f) return false;
            return s.state.heat >= s.heatCapacity * 0.8f;
        }
    }

    public class ThoughtWorker_BeautifulShip : ThoughtWorker
    {
        private static readonly Dictionary<int, KeyValuePair<int, float>> cache = new Dictionary<int, KeyValuePair<int, float>>();

        private static float Impressiveness(RimverseShip s)
        {
            int id = s.bridge?.thingIDNumber ?? 0;
            int now = Find.TickManager.TicksGame;
            if (cache.TryGetValue(id, out KeyValuePair<int, float> c) && now - c.Key < 2500) return c.Value;
            HashSet<Room> rooms = new HashSet<Room>();
            foreach (IntVec3 cell in s.cells)
            {
                Room r = cell.GetRoom(s.map);
                if (r != null && !r.PsychologicallyOutdoors) rooms.Add(r);
            }
            float v = rooms.Count == 0 ? 0f : rooms.Average(r => r.GetStat(RoomStatDefOf.Impressiveness));
            cache[id] = new KeyValuePair<int, float>(now, v);
            return v;
        }

        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            RimverseShip s = ShipThoughts.ShipOf(p);
            if (s == null) return false;
            float v = Impressiveness(s);
            if (v >= 100f) return ThoughtState.ActiveAtStage(1);
            if (v >= 60f) return ThoughtState.ActiveAtStage(0);
            return false;
        }
    }

    public class ThoughtWorker_CrewUnited : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (!p.IsColonist || ShipThoughts.ShipOf(p) == null) return false;
            if (p.records.GetAsInt(RecordDefOf.TimeAsColonistOrColonyAnimal) < 60 * GenDate.TicksPerDay) return false;
            return p.MapHeld.mapPawns.FreeColonistsSpawnedCount >= 3;
        }
    }

    public class ThoughtWorker_ShipUniform : ThoughtWorker
    {
        private static readonly HashSet<string> Uniforms = new HashSet<string> { "Rimverse_Clothes_CrewJumpsuit", "Rimverse_Clothes_OfficerUniform", "Rimverse_Clothes_MarineFatigues", "Rimverse_Clothes_CaptainCoat", "Rimverse_Clothes_GalaUniform" };

        protected override ThoughtState CurrentStateInternal(Pawn p) => p.apparel != null && p.apparel.WornApparel.Any(a => Uniforms.Contains(a.def.defName));
    }

    public class ThoughtWorker_LongShift : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (p.needs?.rest == null || !p.Awake() || ShipThoughts.ShipOf(p) == null) return false;
            return p.needs.rest.CurLevel < 0.15f && p.CurJob != null && p.CurJob.workGiverDef != null;
        }
    }

    public class RecipeWorker_SpaceFuneral : RecipeWorker
    {
        public override void ConsumeIngredient(Thing ingredient, RecipeDef recipe, Map map)
        {
            Corpse c = ingredient as Corpse;
            Pawn dead = c?.InnerPawn;
            base.ConsumeIngredient(ingredient, recipe, map);
            if (dead == null || !dead.RaceProps.Humanlike) return;
            foreach (Pawn p in map.mapPawns.FreeColonistsSpawned) ShipThoughts.Give(p, "SpaceFuneral");
            Pawn host = map.mapPawns.FreeColonistsSpawned.FirstOrDefault();
            if (host != null) ShipThoughts.Tale("SpaceFuneral", host);
        }
    }

    public class CompProperties_Morgue : CompProperties
    {
        public CompProperties_Morgue()
        {
            compClass = typeof(CompMorgue);
        }
    }

    public class CompMorgue : ThingComp
    {
        public override void CompTickRare()
        {
            base.CompTickRare();
            if (!parent.Spawned) return;
            CompPowerTrader power = parent.GetComp<CompPowerTrader>();
            if (power != null && !power.PowerOn) return;
            foreach (IntVec3 c in parent.OccupiedRect())
                foreach (Thing t in c.GetThingList(parent.Map))
                {
                    CompRottable rot = (t as Corpse)?.GetComp<CompRottable>();
                    if (rot != null) rot.RotProgress = Mathf.Max(0f, rot.RotProgress - 250f);
                }
        }
    }

    [HarmonyPatch(typeof(Verb_Shoot), "TryCastShot")]
    public static class Patch_ZeroGRecoil
    {
        public static void Postfix(Verb_Shoot __instance, bool __result)
        {
            if (!__result || !(__instance.CasterPawn is Pawn p) || !p.Spawned || p.stances?.stagger == null) return;
            string proj = __instance.verbProps?.defaultProjectile?.defName ?? "";
            if (proj.Contains("Laser") || proj.Contains("Plasma") || proj.Contains("Ion") || proj.Contains("Beam")) return;
            if (GravityUtility.GravityAt(p) >= LifeAboard.ZeroG) return;
            if (p.apparel != null && p.apparel.WornApparel.Any(a => a.def.defName == "Rimverse_Gear_MagBoots")) return;
            if (Rand.Chance(0.12f)) p.stances.stagger.StaggerFor(45);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class Patch_CrewmateSpaced
    {
        public static void Prefix(Pawn __instance, out Map __state)
        {
            __state = null;
            if (__instance == null || !__instance.IsColonist || !__instance.Spawned) return;
            Map map = __instance.Map;
            if (!SpaceMapUtility.IsSpace(map)) return;
            Room room = __instance.GetRoom();
            MapComponent_RimverseShips comp = map.GetComponent<MapComponent_RimverseShips>();
            RimverseShip ship = comp?.ShipOfRoom(room);
            bool vacuum = ship == null ? !HabitatAir.Pressurized(map, room) : comp.RoomAir(room) < 0.05f;
            if (vacuum) __state = map;
        }

        public static void Postfix(Pawn __instance, Map __state)
        {
            if (__state != null && __instance.Dead) ShipThoughts.GiveMap(__state, "CrewmateSpaced", __instance);
        }
    }
}
