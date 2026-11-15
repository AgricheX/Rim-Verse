using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public static class EmpireFleetLogic
    {
        public const int ShipyardBuildSlots = 3;

        public static bool HasShipyard(string systemKey)
        {
            if (systemKey.NullOrEmpty()) return false;
            GameComponent_PlayerColonies pc = GameComponent_PlayerColonies.Instance;
            if (pc != null)
            {
                foreach (PlayerColony c in pc.colonies)
                {
                    if (c.systemKey != systemKey) continue;
                    if (c.Has(ColonyIndustry.Shipyard)) return true;
                    if (c.facilities.Any(f => f.Ready && f.def != null && f.def.defName == "Rimverse_Facility_OrbitalShipyard")) return true;
                }
            }
            return FleetLogic.PlayerMapIn(systemKey) != null;
        }

        public static IEnumerable<string> ShipyardSystems()
        {
            HashSet<string> seen = new HashSet<string>();
            GameComponent_PlayerColonies pc = GameComponent_PlayerColonies.Instance;
            if (pc != null) foreach (PlayerColony c in pc.colonies) if (seen.Add(c.systemKey) && HasShipyard(c.systemKey)) yield return c.systemKey;
            foreach (Map m in Find.Maps)
            {
                MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(m);
                if (loc == null || loc.Traveling) continue;
                string k = loc.SystemId.Key;
                if (seen.Add(k) && HasShipyard(k)) yield return k;
            }
        }

        public static string SystemName(string key)
        {
            if (key.NullOrEmpty()) return "-";
            StarSystemData s = GameComponent_Universe.Instance?.GetSystem(StarSystemId.FromKey(key));
            return s?.name ?? key;
        }

        public static PlayerFleet CreateFleet(GameComponent_PlayerEmpire e, string systemKey)
        {
            PlayerFleet f = new PlayerFleet { id = e.nextFleetId++, systemKey = systemKey };
            f.name = "Rimverse_FleetDefaultName".Translate(f.id).ToString();
            e.fleets.Add(f);
            return f;
        }

        public static AcceptanceReport CanBuild(PlayerFleet f, FleetHullDef d, int n)
        {
            if (d.research != null && !d.research.IsFinished) return "Rimverse_ColonyNeedResearch".Translate(d.research.LabelCap);
            if (f.Moving) return "Rimverse_FleetMoving".Translate();
            if (!HasShipyard(f.systemKey)) return "Rimverse_FleetNoShipyard".Translate();
            GameComponent_PlayerColonies pc = GameComponent_PlayerColonies.Instance;
            if (pc == null || pc.treasury < d.costSilver * n) return "Rimverse_NotEnoughTreasury".Translate(d.costSilver * n);
            if (f.orders.Count >= ShipyardBuildSlots) return "Rimverse_FleetQueueFull".Translate();
            return true;
        }

        public static bool BuildUnits(PlayerFleet f, FleetHullDef d, int n)
        {
            AcceptanceReport r = CanBuild(f, d, n);
            if (!r.Accepted)
            {
                Messages.Message(r.Reason, MessageTypeDefOf.RejectInput, false);
                return false;
            }
            GameComponent_PlayerColonies.Instance.treasury -= d.costSilver * n;
            int start = f.orders.Count == 0 ? Find.TickManager.TicksGame : f.orders.Max(o => o.readyTick);
            f.orders.Add(new FleetOrder { def = d, count = n, readyTick = start + RimverseUnits.TicksFromDays(d.days * Mathf.Sqrt(n)) });
            return true;
        }

        public static float TravelDays(PlayerFleet f, string destKey)
        {
            if (f.Speed <= 0f) return -1f;
            double ly = GameComponent_Universe.Instance.DistanceLy(StarSystemId.FromKey(f.systemKey), StarSystemId.FromKey(destKey));
            if (double.IsInfinity(ly) || double.IsNaN(ly)) return -1f;
            return Mathf.Max(0.25f, (float)(ly / f.Speed));
        }

        public static bool SendFleet(PlayerFleet f, string destKey, PlayerFleetMission mission, int targetCiv)
        {
            if (f.Empty || f.Moving || destKey.NullOrEmpty()) return false;
            if (destKey == f.systemKey)
            {
                f.mission = mission;
                f.targetCiv = targetCiv;
                if (mission != PlayerFleetMission.Idle && mission != PlayerFleetMission.Defend) Arrive(GameComponent_PlayerEmpire.Instance, f);
                return true;
            }
            float days = TravelDays(f, destKey);
            if (days < 0f)
            {
                Messages.Message("Rimverse_FleetCannotReach".Translate(), MessageTypeDefOf.RejectInput, false);
                return false;
            }
            f.destKey = destKey;
            f.mission = mission;
            f.targetCiv = targetCiv;
            f.arriveTick = Find.TickManager.TicksGame + RimverseUnits.TicksFromDays(days);
            return true;
        }

        public static void Tick(GameComponent_PlayerEmpire e, int now)
        {
            foreach (PlayerFleet f in e.fleets.ToList())
            {
                foreach (FleetOrder o in f.orders.Where(o => now >= o.readyTick).ToList())
                {
                    f.orders.Remove(o);
                    if (o.def == null) continue;
                    f.AddUnits(o.def, o.count);
                    Messages.Message("Rimverse_MsgFleetUnitsReady".Translate(o.count, o.def.LabelCap, f.name), MessageTypeDefOf.PositiveEvent, false);
                }
                if (f.Moving && now >= f.arriveTick)
                {
                    f.systemKey = f.destKey;
                    f.destKey = null;
                    f.arriveTick = -1;
                    Arrive(e, f);
                }
                if (!f.Moving && f.mission == PlayerFleetMission.Blockade && now % RimverseUnits.TicksPerDay < RimverseUnits.TicksPerHour) BlockadeDay(e, f);
                if (f.Empty && f.orders.Count == 0)
                {
                    e.fleets.Remove(f);
                    Messages.Message("Rimverse_MsgFleetLost".Translate(f.name), MessageTypeDefOf.NegativeEvent, false);
                }
            }
        }

        public static Civilization TargetOf(PlayerFleet f)
        {
            GameComponent_Civilizations gc = GameComponent_Civilizations.Instance;
            if (gc == null) return null;
            Civilization owner = gc.OwnerOf(StarSystemId.FromKey(f.systemKey));
            if (owner != null && !owner.dead) return owner;
            Civilization t = gc.Civ(f.targetCiv);
            if (t != null && !t.dead && gc.fleets.Any(x => x.civId == t.id && !x.Moving && x.systemKey == f.systemKey)) return t;
            return null;
        }

        public static bool AtWarWith(Civilization c) => c != null && (c.Hostile || c.playerState == DiploState.War);

        public static float EnemyPower(Civilization c, string systemKey)
        {
            GameComponent_Civilizations gc = GameComponent_Civilizations.Instance;
            float p = CivSimulation.DefensePower(c, systemKey);
            if (gc != null) p += gc.fleets.Where(x => x.civId == c.id && !x.Moving && x.systemKey == systemKey).Sum(x => x.power * x.morale);
            return p;
        }

        private static void Arrive(GameComponent_PlayerEmpire e, PlayerFleet f)
        {
            Messages.Message("Rimverse_MsgFleetArrived".Translate(f.name, SystemName(f.systemKey)), MessageTypeDefOf.NeutralEvent, false);
            if (f.mission == PlayerFleetMission.Idle || f.mission == PlayerFleetMission.Defend) return;
            Civilization target = TargetOf(f);
            if (target == null)
            {
                Messages.Message("Rimverse_FleetNoTarget".Translate(f.name), MessageTypeDefOf.RejectInput, false);
                f.mission = PlayerFleetMission.Defend;
                return;
            }
            if (!AtWarWith(target))
            {
                Messages.Message("Rimverse_FleetNotAtWar".Translate(target.ShownName), MessageTypeDefOf.RejectInput, false);
                f.mission = PlayerFleetMission.Defend;
                return;
            }
            if (f.mission == PlayerFleetMission.Blockade)
            {
                Messages.Message("Rimverse_MsgBlockadeStarted".Translate(f.name, target.ShownName), MessageTypeDefOf.NeutralEvent, false);
                return;
            }
            Map map = FleetLogic.PlayerMapIn(f.systemKey);
            RimverseShip ship = map != null && SpaceMapUtility.IsOrbit(map) ? ColonyUtility.PlayerShipOn(map) : null;
            MapComponent_ShipCombat combat = map != null ? MapComponent_ShipCombat.For(map) : null;
            if (ship != null && combat != null && !combat.InCombat && RimverseMod.Settings.shipCombatEnabled)
            {
                PlayerFleet fleet = f;
                Civilization civ = target;
                Find.WindowStack.Add(new Dialog_MessageBox("Rimverse_FleetBattleChoice".Translate(f.name, target.ShownName), "Rimverse_FleetCommand".Translate(), () => CommandBattle(e, fleet, civ, map, ship), "Rimverse_FleetAutoResolve".Translate(), () => AutoBattle(e, fleet, civ)));
                return;
            }
            AutoBattle(e, f, target);
        }

        private static void CommandBattle(GameComponent_PlayerEmpire e, PlayerFleet f, Civilization target, Map map, RimverseShip ship)
        {
            GameComponent_Civilizations gc = GameComponent_Civilizations.Instance;
            Faction faction = gc.EnsureFaction(target);
            float enemy = EnemyPower(target, f.systemKey);
            float left = Mathf.Max(5f, enemy - f.Power * 0.6f);
            if (faction == null || !ShipCombatUtility.StartEncounter(map, ship, faction, left, target.tech, target.id))
            {
                AutoBattle(e, f, target);
                return;
            }
            ApplyLosses(f, Rand.Range(0.05f, 0.2f));
            DamageCiv(gc, target, f.systemKey, 0.6f);
            Score(e, target, 6f);
        }

        public static void AutoBattle(GameComponent_PlayerEmpire e, PlayerFleet f, Civilization target)
        {
            GameComponent_Civilizations gc = GameComponent_Civilizations.Instance;
            float enemy = EnemyPower(target, f.systemKey);
            float atk = f.Power * (1f + e.GlobalEffects().defense / 200f);
            bool win = Rand.Chance(atk / Mathf.Max(1f, atk + enemy));
            float ratio = enemy / Mathf.Max(1f, atk);
            if (!win)
            {
                ApplyLosses(f, Mathf.Clamp(Rand.Range(0.3f, 0.6f) * Mathf.Max(0.5f, ratio), 0.2f, 0.9f));
                f.mission = PlayerFleetMission.Defend;
                Score(e, target, -8f);
                Find.LetterStack.ReceiveLetter("Rimverse_LetterFleetDefeatLabel".Translate(), "Rimverse_LetterFleetDefeat".Translate(f.name, target.ShownName, SystemName(f.systemKey)), LetterDefOf.NegativeEvent);
                return;
            }
            ApplyLosses(f, Mathf.Clamp(Rand.Range(0.05f, 0.3f) * ratio, 0f, 0.6f));
            string result;
            switch (f.mission)
            {
                case PlayerFleetMission.Raid:
                    int loot = Mathf.RoundToInt(Mathf.Min(target.treasury * 0.1f, 500f + enemy * 30f) + enemy * 10f);
                    target.treasury = Mathf.Max(0f, target.treasury - loot);
                    GameComponent_PlayerColonies.Instance.treasury += loot;
                    DamageCiv(gc, target, f.systemKey, 0.3f);
                    Score(e, target, 5f);
                    result = "Rimverse_FleetResultRaid".Translate(loot);
                    break;
                case PlayerFleetMission.Capture:
                    result = Capture(e, f, target);
                    break;
                default:
                    DamageCiv(gc, target, f.systemKey, 0.8f);
                    Score(e, target, 10f);
                    result = "Rimverse_FleetResultAttack".Translate();
                    break;
            }
            if (f.mission != PlayerFleetMission.Blockade) f.mission = PlayerFleetMission.Defend;
            Find.LetterStack.ReceiveLetter("Rimverse_LetterFleetVictoryLabel".Translate(), "Rimverse_LetterFleetVictory".Translate(f.name, target.ShownName, SystemName(f.systemKey), result), LetterDefOf.PositiveEvent);
        }

        private static string Capture(GameComponent_PlayerEmpire e, PlayerFleet f, Civilization target)
        {
            GameComponent_Civilizations gc = GameComponent_Civilizations.Instance;
            DamageCiv(gc, target, f.systemKey, 0.8f);
            CivPlanet planet = target.planets.Where(p => p.systemKey == f.systemKey).OrderBy(p => p.population).FirstOrDefault();
            if (planet == null)
            {
                Score(e, target, 8f);
                return "Rimverse_FleetResultNoPlanet".Translate();
            }
            float garrison = CivSimulation.DefensePower(target, f.systemKey) * 0.5f;
            if (f.Ground < garrison)
            {
                Score(e, target, 6f);
                return "Rimverse_FleetResultNoGround".Translate(f.Ground.ToString("0"), garrison.ToString("0"));
            }
            if (planet.systemKey == target.capitalKey && target.planets.Count > 1)
            {
                Score(e, target, 8f);
                return "Rimverse_FleetResultCapital".Translate();
            }
            PlayerColony c = TakePlanet(target, planet, true);
            Score(e, target, 20f);
            return "Rimverse_FleetResultCaptured".Translate(c?.name ?? "?");
        }

        public static PlayerColony TakePlanet(Civilization owner, CivPlanet planet, bool byForce)
        {
            GameComponent_PlayerColonies pc = GameComponent_PlayerColonies.Instance;
            GameComponent_Civilizations gc = GameComponent_Civilizations.Instance;
            if (pc == null || planet == null) return null;
            owner.planets.Remove(planet);
            gc?.InvalidateTerritory();
            PlayerColony existing = pc.ColonyAt(planet.systemKey, planet.body);
            if (existing != null) return existing;
            StarSystemData sys = GameComponent_Universe.Instance.GetSystem(StarSystemId.FromKey(planet.systemKey));
            BodyData b = sys?.Body(planet.body);
            PlayerColony c = new PlayerColony
            {
                id = pc.nextId++,
                name = b?.name ?? sys?.name ?? "?",
                systemKey = planet.systemKey,
                bodyIndex = planet.body,
                foundedTick = Find.TickManager.TicksGame,
                population = Mathf.Clamp(planet.population * 4f, 20f, 400f),
                stability = byForce ? 20f : 45f,
                captured = owner.id + 1,
                nextStepTick = Find.TickManager.TicksGame + RimverseUnits.TicksPerQuadrum,
                nextSituationTick = Find.TickManager.TicksGame + RimverseUnits.TicksPerQuadrum
            };
            pc.colonies.Add(c);
            if (owner.planets.Count == 0 && owner.frontierPopulation < 1f && !owner.dead)
            {
                owner.dead = true;
                Find.LetterStack.ReceiveLetter("Rimverse_LetterCivFallenLabel".Translate(), "Rimverse_LetterCivFallen".Translate(owner.ShownName), LetterDefOf.PositiveEvent);
            }
            return c;
        }

        private static void BlockadeDay(GameComponent_PlayerEmpire e, PlayerFleet f)
        {
            Civilization target = TargetOf(f);
            if (target == null || !AtWarWith(target))
            {
                f.mission = PlayerFleetMission.Defend;
                return;
            }
            target.treasury = Mathf.Max(0f, target.treasury * 0.99f);
            foreach (CivPlanet p in target.planets) if (p.systemKey == f.systemKey) p.loyalty = Mathf.Max(0f, p.loyalty - 0.5f);
            target.AddStock(CivResource.Food, -1f);
            Score(e, target, 0.5f);
            WarData w = e.WarWith(target, false);
            if (w != null) w.exhaustion = Mathf.Max(0f, w.exhaustion - 0.2f);
        }

        public static void ApplyLosses(PlayerFleet f, float fraction)
        {
            if (fraction <= 0f) return;
            foreach (KeyValuePair<FleetHullDef, int> kv in f.Units.ToList())
            {
                float lost = kv.Value * fraction;
                int n = Mathf.FloorToInt(lost) + (Rand.Chance(lost - Mathf.Floor(lost)) ? 1 : 0);
                if (n > 0) f.AddUnits(kv.Key, -n);
            }
            f.condition = Mathf.Max(0.3f, f.condition - fraction * 0.5f);
        }

        public static void DamageCiv(GameComponent_Civilizations gc, Civilization c, string systemKey, float strength)
        {
            foreach (Fleet o in gc.fleets.Where(x => x.civId == c.id && !x.Moving && x.systemKey == systemKey)) o.power *= 1f - Mathf.Clamp01(Rand.Range(0.5f, 1f) * strength);
            foreach (CivPlanet p in c.planets) if (p.systemKey == systemKey) p.population *= 1f - 0.1f * strength;
            c.warWeariness = Mathf.Min(100f, c.warWeariness + 10f * strength);
            gc.fleets.RemoveAll(x => x.power < 0.5f);
        }

        public static void Score(GameComponent_PlayerEmpire e, Civilization c, float amount)
        {
            if (c == null || c.Hostile) return;
            WarData w = e.WarWith(c, c.playerState == DiploState.War);
            if (w != null) w.score = Mathf.Clamp(w.score + amount, -100f, 100f);
        }
    }
}
