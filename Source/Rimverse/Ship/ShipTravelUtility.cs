using System.Collections.Generic;

namespace Rimverse
{
    public static class ShipTravelUtility
    {
        private static readonly HashSet<int> reservedStates = new HashSet<int>();

        public static bool IsStateReserved(int id) => reservedStates.Contains(id);

        public static void Reserve(int id) => reservedStates.Add(id);

        public static void Release(int id) => reservedStates.Remove(id);
    }
}
