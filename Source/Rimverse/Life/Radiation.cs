using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    [DefOf]
    public static class RadiationDefOf
    {
        public static StatDef Rimverse_RadiationResistance;
        public static HediffDef Rimverse_RadiationSickness;

        static RadiationDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(RadiationDefOf));
        }
    }

    public class GameComponent_Radiation : GameComponent
    {
        public const int Interval = 2500;
        public const float SpacePerHour = 0.012f;
        public const float AirlessPerHour = 0.008f;
        public const float ThinPerHour = 0.003f;

        public GameComponent_Radiation(Game game)
        {
        }

        public static float StarFactor(MapComponent_ShipLocation loc)
        {
            StarSystemData sys = loc?.System;
            if (sys == null) return 1f;
            BodyData b = loc.Body;
            float d = b == null ? 1f : Mathf.Max(0.05f, sys.OrbitOf(b));
            float flux = sys.TotalLuminosity / (d * d);
            float f = Mathf.Clamp(Mathf.Sqrt(Mathf.Max(flux, 0f)), 0.2f, 4f);
            StarType t = sys.Primary?.type ?? StarType.Yellow;
            if (t == StarType.Pulsar) f = Mathf.Max(f, 3f) * 1.5f;
            else if (t == StarType.Neutron) f = Mathf.Max(f, 2f);
            return f;
        }

        public static float MapDosePerHour(Map map)
        {
            if (SpaceMapUtility.IsRimverseSpace(map))
            {
                MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(map);
                if (loc == null || loc.OnSurface)
                {
                    AtmosphereType a = SpaceMapUtility.OutsideAtmosphere(map);
                    if (a == AtmosphereType.None) return AirlessPerHour * StarFactor(loc);
                    if (a == AtmosphereType.Thin) return ThinPerHour * StarFactor(loc) + BiomeFeatures.ExtraRadiationPerHour(map);
                    return BiomeFeatures.ExtraRadiationPerHour(map);
                }
                return SpacePerHour * StarFactor(loc);
            }
            if (map.IsPocketMap)
            {
                MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(map);
                if (loc == null) return 0f;
                AtmosphereType a = SpaceMapUtility.OutsideAtmosphere(map);
                if (a == AtmosphereType.None) return AirlessPerHour * StarFactor(loc);
                if (a == AtmosphereType.Thin) return ThinPerHour * StarFactor(loc);
            }
            return 0f;
        }

        public static float Resistance(Pawn p)
        {
            float r = p.GetStatValue(RadiationDefOf.Rimverse_RadiationResistance);
            CompVacuumSuit suit = VacuumSuitUtility.WornSuit(p, out bool helmet);
            if (suit != null) r += 0.4f;
            if (helmet) r += 0.15f;
            if (p.Faction == Faction.OfPlayer && ResearchGate.Done("Rimverse_RadiationShielding")) r += 0.2f;
            return Mathf.Clamp01(r);
        }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % Interval != 97) return;
            float hours = Interval / (float)RimverseUnits.TicksPerHour;
            foreach (Map map in Find.Maps)
            {
                float dose = MapDosePerHour(map);
                if (dose <= 0f) continue;
                foreach (Pawn p in map.mapPawns.AllPawnsSpawned.ToList())
                {
                    if (p.Dead || !p.RaceProps.IsFlesh) continue;
                    if (p.Position.Roofed(map)) continue;
                    float take = dose * hours * (1f - Resistance(p));
                    if (take <= 0.0001f) continue;
                    HealthUtility.AdjustSeverity(p, RadiationDefOf.Rimverse_RadiationSickness, take);
                }
            }
        }
    }
}
