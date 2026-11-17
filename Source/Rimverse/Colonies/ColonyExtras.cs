using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public static class PlayerStage
    {
        private static int cached = -1;
        private static int cachedTick = -99999;

        public static int Current
        {
            get
            {
                int now = Find.TickManager?.TicksGame ?? 0;
                if (cached > 0 && now - cachedTick < 2500 && now >= cachedTick) return cached;
                cachedTick = now;
                cached = Compute();
                return cached;
            }
        }

        public static string Label => ("Rimverse_Stage_" + Current).Translate();

        private static int Compute()
        {
            int stage = 3;
            if (Find.Maps.Any(m => SpaceMapUtility.IsRimverseSpace(m) && m.mapPawns.FreeColonistsSpawnedCount > 0)) stage = 6;
            GameComponent_PlayerColonies pc = GameComponent_PlayerColonies.Instance;
            if (pc != null && pc.colonies.Count > 0)
            {
                stage = Mathf.Max(stage, 7);
                string home = GameComponent_Universe.Instance?.HomeSystemId.Key;
                HashSet<string> systems = new HashSet<string>(pc.colonies.Select(c => c.systemKey));
                if (pc.colonies.Any(c => c.systemKey == home)) stage = Mathf.Max(stage, 8);
                if (pc.colonies.GroupBy(c => c.systemKey).Any(g => g.Count() >= 2)) stage = Mathf.Max(stage, 9);
                if (systems.Count >= 2) stage = Mathf.Max(stage, 10);
                if (systems.Count >= 5) stage = Mathf.Max(stage, 11);
                if (systems.Count >= 12) stage = Mathf.Max(stage, 12);
                if (systems.Count >= 30) stage = Mathf.Max(stage, 13);
                if (systems.Count >= 80) stage = 14;
            }
            GameComponent_Megastructures mg = GameComponent_Megastructures.Instance;
            int done = mg?.all.Count(m => m.player && m.Complete && !m.IsBase) ?? 0;
            if (done >= 1) stage = Mathf.Max(stage, 10);
            if (done >= 3) stage = Mathf.Max(stage, 12);
            if (GameComponent_Endgame.Instance?.activatedGates.Count > 0) stage = Mathf.Max(stage, 11);
            return Mathf.Clamp(stage, 1, 14);
        }
    }

    public class GameComponent_ColonyExtras : GameComponent
    {
        public HashSet<int> aiAdmins = new HashSet<int>();

        public static GameComponent_ColonyExtras Instance => Verse.Current.Game?.GetComponent<GameComponent_ColonyExtras>();

        public GameComponent_ColonyExtras(Game game)
        {
        }

        public static bool HasAI(PlayerColony c) => Instance?.aiAdmins.Contains(c.id) == true;

        public static Thing FindCore(Map map) => map?.listerThings.ThingsOfDef(ThingDefOf.AIPersonaCore).FirstOrDefault(t => !t.IsForbidden(Faction.OfPlayer));

        public static bool TryInstallAI(PlayerColony c, Map map)
        {
            Thing core = FindCore(map);
            if (core == null || HasAI(c)) return false;
            core.SplitOff(1).Destroy();
            Instance.aiAdmins.Add(c.id);
            Messages.Message("Rimverse_MsgAIAdmin".Translate(c.name), MessageTypeDefOf.PositiveEvent, false);
            return true;
        }

        public override void GameComponentTick()
        {
            int tick = Find.TickManager.TicksGame;
            if (tick % 2500 != 1777) return;
            GameComponent_PlayerColonies pc = GameComponent_PlayerColonies.Instance;
            if (pc != null)
            {
                foreach (PlayerColony c in pc.colonies)
                {
                    if (!HasAI(c)) continue;
                    c.stability = Mathf.Min(100f, c.stability + 0.4f);
                    c.attention = Mathf.Max(0f, c.attention - 0.3f);
                }
                aiAdmins.RemoveWhere(id => !pc.colonies.Any(c => c.id == id));
            }
            foreach (Map map in Find.Maps)
            {
                MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(map);
                if (loc == null || loc.Traveling || loc.systemKey.NullOrEmpty() || !EmpireFleetLogic.HasShipyard(loc.systemKey)) continue;
                MapComponent_RimverseShips comp = map.GetComponent<MapComponent_RimverseShips>();
                if (comp == null) continue;
                foreach (RimverseShip s in comp.Ships)
                {
                    if (s.bridge == null || s.bridge.Faction != Faction.OfPlayer) continue;
                    RepairShip(map, s);
                }
            }
        }

        private static void RepairShip(Map map, RimverseShip s)
        {
            int fixedParts = 0;
            foreach (IntVec3 c in s.cells)
            {
                foreach (Thing t in c.GetThingList(map))
                {
                    if (!(t is Building b) || b.Faction != Faction.OfPlayer || !b.def.useHitPoints || b.HitPoints >= b.MaxHitPoints) continue;
                    b.HitPoints = Mathf.Min(b.MaxHitPoints, b.HitPoints + Mathf.Max(1, b.MaxHitPoints / 20));
                    fixedParts++;
                    if (fixedParts > 60) return;
                }
            }
            if (s.state.lostHullPoints > 0f) s.state.lostHullPoints = Mathf.Max(0f, s.state.lostHullPoints - 25f);
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref aiAdmins, "aiAdmins", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && aiAdmins == null) aiAdmins = new HashSet<int>();
        }
    }
}
