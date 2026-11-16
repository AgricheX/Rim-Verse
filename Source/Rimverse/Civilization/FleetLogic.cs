using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public static class FleetLogic
    {
        public static void Tick(GameComponent_Civilizations gc)
        {
            int now = Find.TickManager.TicksGame;
            List<Fleet> arrived = null;
            foreach (Fleet f in gc.fleets)
            {
                if (f.Moving && now >= f.arriveTick)
                {
                    if (arrived == null) arrived = new List<Fleet>();
                    arrived.Add(f);
                }
            }
            if (arrived == null) return;
            foreach (Fleet f in arrived) Arrive(gc, f);
            gc.fleets.RemoveAll(f => f.power < 0.5f);
        }

        private static void Arrive(GameComponent_Civilizations gc, Fleet f)
        {
            f.systemKey = f.destKey;
            f.destKey = null;
            f.arriveTick = -1;
            f.departTick = -1;
            Civilization owner = gc.Civ(f.civId);
            if (owner == null || owner.dead)
            {
                f.power = 0f;
                return;
            }
            if (f.returning && f.mission == FleetMission.Trade)
            {
                f.power = 0f;
                return;
            }
            if (f.mission == FleetMission.Trade)
            {
                CivTrade.Arrive(gc, owner, f);
                return;
            }
            if (f.returning)
            {
                f.returning = false;
                f.mission = FleetMission.Patrol;
                f.targetCiv = -2;
                foreach (Fleet o in gc.fleets)
                {
                    if (o != f && o.civId == f.civId && !o.Moving && o.mission == FleetMission.Patrol && o.systemKey == f.systemKey)
                    {
                        o.power += f.power;
                        f.power = 0f;
                        break;
                    }
                }
                return;
            }
            if (f.mission == FleetMission.Raid && f.TargetsPlayer)
            {
                RaidPlayer(gc, owner, f);
                SendHome(gc, owner, f);
                return;
            }
            if (f.mission == FleetMission.War)
            {
                Civilization target = gc.Civ(f.targetCiv);
                if (target != null && !target.dead)
                {
                    CivRelation rel = owner.RelationWith(target.id, false);
                    if (rel != null && rel.state == DiploState.War) Battle(gc, owner, f, target);
                }
                SendHome(gc, owner, f);
            }
        }

        public static void SendHome(GameComponent_Civilizations gc, Civilization c, Fleet f)
        {
            if (f.power < 0.5f || f.systemKey == c.capitalKey) return;
            float speed = Mathf.Max(0.5f, CivSimulation.FleetSpeedLyPerDay(c));
            double ly = GameComponent_Universe.Instance.DistanceLy(StarSystemId.FromKey(f.systemKey), c.Capital);
            if (double.IsInfinity(ly)) ly = 50.0;
            int now = Find.TickManager.TicksGame;
            f.destKey = c.capitalKey;
            f.departTick = now;
            f.arriveTick = now + Mathf.Max(RimverseUnits.TicksPerHour * 6, RimverseUnits.TicksFromDays((float)(ly / speed)));
            f.returning = true;
        }

        private static void Battle(GameComponent_Civilizations gc, Civilization attacker, Fleet f, Civilization defender)
        {
            List<Fleet> defenders = gc.fleets.Where(o => o.civId == defender.id && !o.Moving && o.systemKey == f.systemKey).ToList();
            float defPower = defenders.Sum(o => o.power) + CivSimulation.DefensePower(defender, f.systemKey);
            float atkPower = f.power * f.morale;
            float chance = atkPower / Mathf.Max(1f, atkPower + defPower);
            bool win = Rand.Chance(chance);
            float ratio = defPower / Mathf.Max(1f, atkPower);
            if (win)
            {
                f.power *= 1f - Mathf.Clamp(Rand.Range(0.2f, 0.5f) * ratio, 0.05f, 0.8f);
                foreach (Fleet o in defenders) o.power *= Rand.Range(0f, 0.4f);
                CivPlanet captured = defender.planets.FirstOrDefault(p => p.systemKey == f.systemKey && f.systemKey != defender.capitalKey);
                if (captured != null)
                {
                    defender.planets.Remove(captured);
                    captured.loyalty = 20f;
                    attacker.planets.Add(captured);
                    gc.InvalidateTerritory();
                }
                else
                {
                    foreach (CivPlanet p in defender.planets) if (p.systemKey == f.systemKey) p.population *= 0.7f;
                    defender.frontierPopulation *= 0.9f;
                }
                defender.warWeariness = Mathf.Min(100f, defender.warWeariness + 15f);
            }
            else
            {
                f.power *= Rand.Range(0f, 0.4f);
                foreach (Fleet o in defenders) o.power *= 1f - Mathf.Clamp(Rand.Range(0.1f, 0.4f) / Mathf.Max(0.2f, ratio), 0.05f, 0.7f);
                attacker.warWeariness = Mathf.Min(100f, attacker.warWeariness + 15f);
            }
            if (attacker.known || defender.known)
            {
                Messages.Message("Rimverse_MsgBattle".Translate(attacker.ShownName, defender.ShownName, win ? attacker.ShownName : defender.ShownName), MessageTypeDefOf.NeutralEvent, false);
            }
        }

        public static Map PlayerMapIn(string systemKey)
        {
            Map best = null;
            foreach (Map m in Find.Maps)
            {
                MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(m);
                if (loc == null || loc.Traveling || loc.SystemId.Key != systemKey) continue;
                if (!m.mapPawns.AnyColonistSpawned) continue;
                if (best == null || m.IsPlayerHome && !best.IsPlayerHome) best = m;
            }
            return best;
        }

        public static void RaidPlayer(GameComponent_Civilizations gc, Civilization c, Fleet f)
        {
            Map map = PlayerMapIn(f.systemKey);
            if (map == null) return;
            if (GameComponent_RimverseExtras.EscortActive && SpaceMapUtility.IsSpace(map)) return;
            Faction faction = gc.EnsureFaction(c);
            if (faction == null) return;
            if (!c.known) gc.MakeContact(c, false);
            if (RimverseMod.Settings.shipCombatEnabled && SpaceMapUtility.IsRimverseSpace(map) && SpaceMapUtility.IsOrbit(map))
            {
                MapComponent_ShipCombat combat = MapComponent_ShipCombat.For(map);
                RimverseShip target = map.GetComponent<MapComponent_RimverseShips>()?.Ships.FirstOrDefault(s => s.bridge != null && s.bridge.Faction == Faction.OfPlayer);
                if (combat != null && target != null && !combat.InCombat && ShipCombatUtility.StartEncounter(map, target, faction, f.power, c.tech, c.id))
                {
                    f.power *= 0.6f;
                    return;
                }
            }
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, map);
            parms.faction = faction;
            parms.forced = true;
            float baseline = StorytellerUtility.DefaultThreatPointsNow(map);
            parms.points = Mathf.Clamp(f.power * 8f, baseline * 0.5f, baseline * 2f);
            if (SpaceMapUtility.IsSpace(map)) parms.raidArrivalMode = PawnsArrivalModeDefOf.CenterDrop;
            IncidentDef def = IncidentDefOf.RaidEnemy;
            if (!def.Worker.TryExecute(parms)) return;
            f.power *= 0.6f;
        }
    }
}
