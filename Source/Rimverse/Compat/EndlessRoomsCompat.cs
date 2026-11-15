using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld.Planet;
using Verse;

namespace Rimverse
{
    public static class EndlessRoomsCompat
    {
        private static bool resolved;
        private static MethodInfo isTile;
        private static MethodInfo drop;
        private static MethodInfo banish;
        private static MethodInfo pull;
        private static MethodInfo yellowMap;
        public const string RoomsId = "arsiy.rooms";
        private static readonly Dictionary<int, bool> mapCache = new Dictionary<int, bool>();

        public static bool Active => ModCompat.EndlessRooms && Resolve() && isTile != null;

        private static bool Resolve()
        {
            if (resolved) return isTile != null;
            resolved = true;
            try
            {
                Type util = AccessTools.TypeByName("arsiy.Rooms.YellowRoomsUtility");
                if (util != null) yellowMap = AccessTools.Method(util, "IsYellowRoomsMap", new[] { typeof(Map) });
                Type api = AccessTools.TypeByName("EndlessRooms.EndlessRoomsApi");
                if (api != null)
                {
                    isTile = AccessTools.Method(api, "IsBackroomsTile", new[] { typeof(PlanetTile) });
                    drop = AccessTools.Method(api, "Drop", new[] { typeof(Pawn) });
                    banish = AccessTools.Method(api, "Banish", new[] { typeof(Pawn) });
                    pull = AccessTools.Method(api, "Pull", new[] { typeof(Pawn), typeof(Map), typeof(IntVec3) });
                }
                if (isTile == null)
                {
                    Type hidden = AccessTools.TypeByName("EndlessRooms.RoomsHidden");
                    if (hidden != null) isTile = AccessTools.Method(hidden, "IsHiddenTile", new[] { typeof(PlanetTile) });
                }
                Type transit = AccessTools.TypeByName("EndlessRooms.RoomsTransit");
                if (transit != null)
                {
                    if (drop == null) drop = AccessTools.Method(transit, "BeginDrop", new[] { typeof(Pawn) });
                    if (banish == null) banish = AccessTools.Method(transit, "BeginBanish", new[] { typeof(Pawn) });
                    if (pull == null) pull = AccessTools.Method(transit, "BeginPull", new[] { typeof(Pawn), typeof(Map), typeof(IntVec3) });
                }
            }
            catch (Exception ex)
            {
                RimverseLog.Warning("Endless Rooms: " + ex.Message);
            }
            return isTile != null || yellowMap != null;
        }

        public static bool IsBackrooms(Map map)
        {
            if (map == null || !(ModCompat.EndlessRooms || ModCompat.IsActive(RoomsId)) || !Resolve()) return false;
            if (mapCache.TryGetValue(map.uniqueID, out bool v)) return v;
            try
            {
                v = isTile != null && (bool)isTile.Invoke(null, new object[] { map.Tile });
                if (!v && yellowMap != null) v = (bool)yellowMap.Invoke(null, new object[] { map });
            }
            catch
            {
                v = false;
            }
            mapCache[map.uniqueID] = v;
            return v;
        }

        public static void Forget(Map map)
        {
            if (map != null) mapCache.Remove(map.uniqueID);
        }

        private static bool Call(MethodInfo m, params object[] args)
        {
            if (m == null) return false;
            try
            {
                object r = m.Invoke(null, args);
                return !(r is bool b) || b;
            }
            catch (Exception ex)
            {
                RimverseLog.Warning("Endless Rooms: " + ex.Message);
                return false;
            }
        }

        public static bool Drop(Pawn pawn) => Active && pawn != null && !IsBackrooms(pawn.Map) && Call(drop, pawn);

        public static bool Banish(Pawn pawn) => Active && pawn != null && !IsBackrooms(pawn.Map) && Call(banish, pawn);

        public static bool Pull(Pawn pawn, Map dest, IntVec3 near) => Active && pawn != null && dest != null && Call(pull, pawn, dest, near);

        public static List<Pawn> LostColonists()
        {
            List<Pawn> list = new List<Pawn>();
            if (!Active) return list;
            foreach (Map m in Find.Maps)
            {
                if (IsBackrooms(m)) list.AddRange(m.mapPawns.FreeColonistsSpawned);
            }
            return list;
        }

        public static bool AnyLost => LostColonists().Any();
    }
}
