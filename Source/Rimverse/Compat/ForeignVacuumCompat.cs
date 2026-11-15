using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Rimverse
{
    public class StatPart_RimverseSuit : StatPart
    {
        public float suitOnly;
        public float full = 1f;

        public static bool suppress;

        private float ValueFor(Pawn pawn)
        {
            if (suppress || pawn?.apparel == null) return -1f;
            CompVacuumSuit suit = VacuumSuitUtility.WornSuit(pawn, out bool helmet);
            if (suit == null || suit.Air <= 0f) return -1f;
            return helmet ? full : suitOnly;
        }

        public override void TransformValue(StatRequest req, ref float val)
        {
            if (!(req.Thing is Pawn pawn)) return;
            float v = ValueFor(pawn);
            if (v > val) val = v;
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (!(req.Thing is Pawn pawn)) return null;
            float v = ValueFor(pawn);
            if (v <= 0f) return null;
            CompVacuumSuit suit = VacuumSuitUtility.WornSuit(pawn, out _);
            return "Rimverse_StatPartSuit".Translate(v.ToStringPercent(), suit.Air.ToString("0.#"));
        }
    }

    public static class ForeignVacuumCompat
    {
        private static readonly List<Pawn> tmpPawns = new List<Pawn>();
        private static BiomeDef sos2Space;
        private static bool sos2Searched;

        public static bool IsSos2Space(Map map)
        {
            if (!ModCompat.SaveOurShip2 || map?.Biome == null) return false;
            if (!sos2Searched)
            {
                sos2Searched = true;
                sos2Space = DefDatabase<BiomeDef>.GetNamedSilentFail("OuterSpaceBiome");
            }
            return sos2Space != null && map.Biome == sos2Space;
        }

        public static bool Applies(Map map)
        {
            if (SpaceMapUtility.RimverseHandlesAir(map)) return false;
            return SpaceMapUtility.IsOdysseyVacuum(map) || IsSos2Space(map);
        }

        private static bool Exposed(Pawn pawn, bool sos2)
        {
            Room room = pawn.GetRoom();
            if (room == null) return true;
            if (sos2) return room.UsesOutdoorTemperature || room.OpenRoofCount > 0;
            return room.Vacuum >= 0.5f;
        }

        public static void Tick(Map map, float seconds)
        {
            if (!Applies(map)) return;
            bool sos2 = IsSos2Space(map);
            tmpPawns.Clear();
            tmpPawns.AddRange(map.mapPawns.AllPawnsSpawned);
            for (int i = 0; i < tmpPawns.Count; i++)
            {
                Pawn pawn = tmpPawns[i];
                if (!SpaceMapUtility.Breathes(pawn) || pawn.apparel == null) continue;
                CompVacuumSuit suit = VacuumSuitUtility.WornSuit(pawn, out _);
                if (suit == null) continue;
                if (Exposed(pawn, sos2))
                {
                    if (OtherProtection(pawn, sos2) >= 1f) continue;
                    VacuumSuitUtility.TryBreatheFromSuit(pawn, seconds);
                }
                else
                {
                    VacuumSuitUtility.Refill(pawn, seconds, null);
                }
            }
            tmpPawns.Clear();
        }

        private static float OtherProtection(Pawn pawn, bool sos2)
        {
            StatDef stat = sos2 ? DefDatabase<StatDef>.GetNamedSilentFail("HypoxiaResistance") : DefDatabase<StatDef>.GetNamedSilentFail("VacuumResistance");
            if (stat == null) return 0f;
            StatPart_RimverseSuit.suppress = true;
            try
            {
                return pawn.GetStatValue(stat, true, -1);
            }
            finally
            {
                StatPart_RimverseSuit.suppress = false;
            }
        }
    }
}
