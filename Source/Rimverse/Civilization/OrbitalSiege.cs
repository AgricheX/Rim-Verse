using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public static class OrbitalSiege
    {
        public const float MinPower = 6f;
        public const float Chance = 0.4f;
        public const float StrikeChancePerDay = 0.25f;

        public static bool TryBegin(GameComponent_Civilizations gc, Civilization owner, Fleet f)
        {
            if (owner.lore == LoreKind.Pirates || f.power < MinPower || owner.playerState != DiploState.War) return false;
            if (FleetLogic.PlayerMapIn(f.systemKey) == null || !Rand.Chance(Chance)) return false;
            if (gc.fleets.Any(o => o != f && o.mission == FleetMission.Siege && o.systemKey == f.systemKey)) return false;
            int now = Find.TickManager.TicksGame;
            f.mission = FleetMission.Siege;
            f.siegeUntil = now + Rand.RangeInclusive(6, 12) * RimverseUnits.TicksPerDay;
            Find.LetterStack.ReceiveLetter("Rimverse_LetterSiegeLabel".Translate(), "Rimverse_LetterSiege".Translate(owner.Name, RimverseUnits.DaysFromTicks(f.siegeUntil - now).ToString("0")), LetterDefOf.ThreatBig, new LookTargets(FleetLogic.PlayerMapIn(f.systemKey)?.Parent));
            return true;
        }

        public static bool Besieged(string systemKey)
        {
            GameComponent_Civilizations gc = GameComponent_Civilizations.Instance;
            return gc != null && !systemKey.NullOrEmpty() && gc.fleets.Any(f => f.mission == FleetMission.Siege && f.systemKey == systemKey && f.power >= 0.5f);
        }

        public static bool Besieged(Map map)
        {
            MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(map);
            string key = loc != null && !loc.systemKey.NullOrEmpty() ? loc.systemKey : GameComponent_Universe.Instance?.HomeSystemId.Key;
            return Besieged(key);
        }

        public static float DefenderPower(string systemKey)
        {
            GameComponent_PlayerEmpire e = GameComponent_PlayerEmpire.Instance;
            if (e == null) return 0f;
            return e.fleets.Where(p => !p.Moving && p.systemKey == systemKey).Sum(p => p.Power);
        }

        public static void Tick(GameComponent_Civilizations gc, int now)
        {
            if (now % RimverseUnits.TicksPerDay >= GameComponent_Civilizations.FleetTickInterval) return;
            foreach (Fleet f in gc.fleets.Where(x => x.mission == FleetMission.Siege && !x.Moving).ToList())
            {
                Civilization owner = gc.Civ(f.civId);
                if (owner == null || owner.dead)
                {
                    f.power = 0f;
                    continue;
                }
                float defense = DefenderPower(f.systemKey);
                bool peace = owner.playerState != DiploState.War;
                bool broken = defense > f.power * f.morale;
                if (broken)
                {
                    f.power *= Rand.Range(0.3f, 0.6f);
                    PlayerFleetDamage(f.systemKey, 0.15f);
                }
                if (peace || broken || now >= f.siegeUntil || FleetLogic.PlayerMapIn(f.systemKey) == null)
                {
                    End(gc, owner, f, broken);
                    continue;
                }
                CivTrade.Disrupt(gc, f.systemKey, 0.5f);
                if (Rand.Chance(StrikeChancePerDay)) FleetLogic.RaidPlayer(gc, owner, f);
                f.morale = Mathf.Max(0.3f, f.morale - 0.03f);
            }
        }

        private static void PlayerFleetDamage(string systemKey, float fraction)
        {
            GameComponent_PlayerEmpire e = GameComponent_PlayerEmpire.Instance;
            if (e == null) return;
            foreach (PlayerFleet p in e.fleets.Where(p => !p.Moving && p.systemKey == systemKey).ToList()) EmpireFleetLogic.ApplyLosses(p, fraction);
        }

        private static void End(GameComponent_Civilizations gc, Civilization owner, Fleet f, bool broken)
        {
            f.mission = FleetMission.Raid;
            f.siegeUntil = -1;
            Find.LetterStack.ReceiveLetter("Rimverse_LetterSiegeEndLabel".Translate(), (broken ? "Rimverse_LetterSiegeBroken" : "Rimverse_LetterSiegeLifted").Translate(owner.Name), broken ? LetterDefOf.PositiveEvent : LetterDefOf.NeutralEvent);
            FleetLogic.SendHome(gc, owner, f);
        }
    }

    [HarmonyPatch(typeof(IncidentWorker_OrbitalTraderArrival), "CanFireNowSub")]
    public static class Patch_SiegeBlocksTraders
    {
        public static void Postfix(IncidentParms parms, ref bool __result)
        {
            if (__result && parms?.target is Map map && OrbitalSiege.Besieged(map)) __result = false;
        }
    }
}
