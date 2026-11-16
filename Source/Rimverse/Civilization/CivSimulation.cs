using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public static class CivSimulation
    {
        public static float Mod(Civilization c, CivStat s) => 1f + c.Modifier(s) / 100f;

        public static float FleetSpeedLyPerDay(Civilization c)
        {
            if (c.tech < TechLevel.Spacer) return 0f;
            if (c.stage >= 12) return 20f;
            if (c.stage == 11) return 10f;
            if (c.stage == 10) return 4f;
            if (c.stage == 9) return 2f;
            return 0f;
        }

        public static float MilitaryCapacity(Civilization c)
        {
            float pop = Mathf.Max(0.01f, c.Population);
            float techFactor = c.tech >= TechLevel.Ultra ? 2f : c.tech >= TechLevel.Spacer ? 1f : 0.3f;
            return Mathf.Sqrt(pop) * 10f * Mod(c, CivStat.Military) * techFactor * (0.7f + 0.6f * c.sizeClass / 100f);
        }

        public static float FleetPower(GameComponent_Civilizations gc, Civilization c)
        {
            float total = 0f;
            foreach (Fleet f in gc.fleets) if (f.civId == c.id) total += f.power;
            return total;
        }

        public static float DefensePower(Civilization c, string systemKey)
        {
            float pop = 0f;
            foreach (CivPlanet p in c.planets) if (p.systemKey == systemKey) pop += p.population;
            if (systemKey == c.capitalKey) pop += c.frontierPopulation * 0.1f;
            return Mathf.Sqrt(Mathf.Max(0f, pop)) * 5f * Mod(c, CivStat.Military);
        }

        private static bool AtWar(Civilization c)
        {
            if (c.playerState == DiploState.War && !c.Hostile) return true;
            foreach (CivRelation r in c.relations) if (r.state == DiploState.War) return true;
            return false;
        }

        public static void Step(GameComponent_Civilizations gc, Civilization c, float years)
        {
            if (years <= 0f) return;
            int seedTick = c.lastSimTick;
            UniverseRand r = new UniverseRand(UniverseRand.Hash(GameComponent_Universe.Instance.universeSeed, c.id, seedTick, 900));
            Grow(c, years);
            Produce(c, years);
            UpdateMoods(c, years);
            UpdateStability(c, years);
            UpdateIdeologies(c, years, r);
            UpdateLoyalty(gc, c);
            UpdateRelations(gc, c, years);
            ChooseGoal(c, r);
            Act(gc, c, r, years);
            PoliticalEvents(gc, c, r, years);
            CivPreSpace.TechRevolution(gc, c, r, years);
            CivLegitimacy.Tick(gc, c, r, years);
            CivColonyReactions.Tick(gc, c, r, years);
            StageChange(gc, c, years);
        }

        private static void Grow(Civilization c, float years)
        {
            float rate = 0.02f * years * (c.stability / 50f) * Mod(c, CivStat.Growth);
            bool famine = c.Stock(CivResource.Food) <= 0f;
            if (famine) rate = -0.03f * years;
            foreach (CivPlanet p in c.planets) p.population = Mathf.Clamp(p.population * (1f + rate), 0.01f, Mathf.Max(p.capacity, 0.01f));
            float cap = CivilizationGenerator.StagePopMin[Mathf.Clamp(c.stage + 1, 1, 14)] * 1.5f;
            c.frontierPopulation = Mathf.Clamp(c.frontierPopulation * (1f + rate), 0f, Mathf.Max(cap, c.frontierPopulation));
        }

        private static void Produce(Civilization c, float years)
        {
            float pop = c.Population;
            float prod = pop * years * Mod(c, CivStat.Production);
            float food = pop * years * Mod(c, CivStat.Growth) * 1.05f - pop * years;
            c.AddStock(CivResource.Food, food);
            c.AddStock(CivResource.Metal, prod * 0.4f);
            c.AddStock(CivResource.Fuel, prod * 0.2f);
            if (c.stage >= 9) c.AddStock(CivResource.WarpMatter, prod * 0.05f);
            c.AddStock(CivResource.Components, prod * 0.1f * Mod(c, CivStat.Research));
            float luxury = c.origin != null && c.origin.defName == "Rimverse_Policy_OriginGlitterworld" ? 0.15f : 0.05f;
            c.AddStock(CivResource.Luxury, prod * luxury);
            c.treasury += pop * years * 40f * Mod(c, CivStat.Trade) * (c.stability / 50f);
        }

        private static void UpdateMoods(Civilization c, float years)
        {
            foreach (InterestGroup g in Enum.GetValues(typeof(InterestGroup)))
            {
                float target = 50f;
                foreach (PolicyDef p in c.Policies)
                {
                    if (p.favoredBy.Contains(g)) target += 15f;
                    if (p.opposedBy.Contains(g)) target -= 15f;
                }
                if (g == InterestGroup.Military && AtWar(c)) target += 5f;
                if (g == InterestGroup.Merchants && AtWar(c)) target -= 10f;
                if (g == InterestGroup.Workers && c.Stock(CivResource.Food) <= 0f) target -= 25f;
                float cur = c.GroupMood(g);
                c.groupMood[g] = Mathf.Clamp(Mathf.MoveTowards(cur, target, 10f * years), 0f, 100f);
            }
        }

        private static void UpdateStability(Civilization c, float years)
        {
            float target = 50f + Mathf.Clamp(c.Modifier(CivStat.Stability), -20f, 20f);
            foreach (KeyValuePair<InterestGroup, float> kv in c.groupMood)
            {
                if (kv.Value > 60f) target += 5f;
                else if (kv.Value < 35f) target -= 10f;
            }
            if (AtWar(c)) target -= 10f;
            if (c.Stock(CivResource.Food) <= 0f) target -= 20f;
            target -= c.warWeariness * 0.1f;
            c.stability = Mathf.Clamp(Mathf.MoveTowards(c.stability, Mathf.Clamp(target, 0f, 100f), 10f * years), 0f, 100f);
            if (c.stability > 40f) c.stableYears++;
            else c.stableYears = 0;
            if (c.stability < 20f) c.lowStabilityYears++;
            else c.lowStabilityYears = 0;
            if (AtWar(c)) c.warWeariness = Mathf.Min(100f, c.warWeariness + 10f * years);
            else c.warWeariness = Mathf.Max(0f, c.warWeariness - 15f * years);
        }

        private static void UpdateIdeologies(Civilization c, float years, UniverseRand r)
        {
            if (c.ideology == null) return;
            string own = c.ideology.defName;
            if (!c.ideologySupport.ContainsKey(own)) c.ideologySupport[own] = 50f;
            float favor = 0f;
            int n = 0;
            foreach (InterestGroup g in c.ideology.favoredBy)
            {
                favor += c.GroupMood(g) - 50f;
                n++;
            }
            float delta = n > 0 ? favor / n / 5f : 0f;
            delta += (c.stability - 50f) / 20f;
            delta += r.Range(-2f, 2f);
            delta = Mathf.Clamp(delta, -5f, 5f) * years;
            c.ideologySupport[own] = Mathf.Clamp(c.ideologySupport[own] + delta, 1f, 99f);
            float othersTotal = c.ideologySupport.Where(kv => kv.Key != own).Sum(kv => kv.Value);
            float remaining = 100f - c.ideologySupport[own];
            if (othersTotal <= 0.01f)
            {
                PolicyDef other = CivilizationGenerator.Pick(r, PolicyGroup.Ideology, c.tech, c.stage, false);
                if (other != null && other.defName != own) c.ideologySupport[other.defName] = remaining;
                return;
            }
            foreach (string k in c.ideologySupport.Keys.ToList())
            {
                if (k == own) continue;
                c.ideologySupport[k] = c.ideologySupport[k] / othersTotal * remaining;
            }
        }

        private static void UpdateLoyalty(GameComponent_Civilizations gc, Civilization c)
        {
            float speed = Mathf.Max(0.5f, FleetSpeedLyPerDay(c));
            GameComponent_Universe u = GameComponent_Universe.Instance;
            foreach (CivPlanet p in c.planets)
            {
                double d = p.systemKey == c.capitalKey ? 0.0 : u.DistanceLy(c.Capital, StarSystemId.FromKey(p.systemKey));
                float days = (float)(d / speed);
                p.loyalty = Mathf.Clamp(50f + c.Modifier(CivStat.Loyalty) + (c.stability - 50f) * 0.5f - days / 2f, 0f, 100f);
            }
        }

        private static void UpdateRelations(GameComponent_Civilizations gc, Civilization c, float years)
        {
            int now = Find.TickManager.TicksGame;
            foreach (CivRelation rel in c.relations)
            {
                rel.opinion = Mathf.MoveTowards(rel.opinion, rel.baseOpinion, 1f * years);
                if (rel.state == DiploState.Truce && now - rel.sinceTick > 2 * RimverseUnits.TicksPerYear)
                {
                    Civilization o = gc.Civ(rel.other);
                    if (o != null) gc.SetState(c, o, DiploState.Peace);
                }
            }
            if (!c.Hostile)
            {
                float playerBase = 0f;
                c.playerOpinion = Mathf.MoveTowards(c.playerOpinion, playerBase, 1f * years);
                if (c.playerState == DiploState.Truce && now - c.playerStateSince > 2 * RimverseUnits.TicksPerYear) gc.SetPlayerState(c, DiploState.Peace);
            }
        }

        private static void ChooseGoal(Civilization c, UniverseRand r)
        {
            AiProfile prof = c.government?.profile ?? AiProfile.Cautious;
            CivGoal[] goals = { CivGoal.Expand, CivGoal.Arm, CivGoal.Colonize, CivGoal.Research, CivGoal.Trade, CivGoal.Defend };
            float[] w = { 1f, 1f, 1f, 1f, 1f, 1f };
            switch (prof)
            {
                case AiProfile.Warmonger: w[0] += 2f; w[1] += 3f; break;
                case AiProfile.Aggressive: w[0] += 1f; w[1] += 2f; break;
                case AiProfile.Mercantile: w[4] += 3f; w[2] += 1f; break;
                case AiProfile.Expansionist: w[2] += 3f; w[0] += 2f; break;
                default: w[5] += 2f; w[3] += 2f; break;
            }
            if (c.ideology?.profile == AiProfile.Warmonger) w[1] += 1f;
            if (c.stability < 30f) w[5] += 3f;
            c.goal = r.Weighted(goals, w);
        }

        private static void Act(GameComponent_Civilizations gc, Civilization c, UniverseRand r, float years)
        {
            float power = FleetPower(gc, c);
            float cap = MilitaryCapacity(c);
            if ((c.goal == CivGoal.Arm || power < cap * 0.4f) && power < cap && c.Stock(CivResource.Metal) > 5f)
            {
                float build = Mathf.Min(cap - power, cap * 0.3f * years + 1f);
                c.AddStock(CivResource.Metal, -build * 0.5f);
                Fleet f = new Fleet { id = gc.NextFleetId(), civId = c.id, mission = FleetMission.Patrol, systemKey = c.capitalKey, power = build };
                MergeOrAdd(gc, f);
            }
            if (c.goal == CivGoal.Colonize && c.stage >= 9 && c.Stock(CivResource.Metal) > 20f && r.Chance(0.5f * years + 0.1f)) Colonize(gc, c, r);
            if (c.goal == CivGoal.Expand || c.goal == CivGoal.Arm) ConsiderWar(gc, c, r);
            ConsiderPeace(gc, c, r);
            ConsiderPlayerRaid(gc, c, r, years);
            CivTrade.Launch(gc, c, r, years);
            CivTrade.Competition(gc, c, r, years);
        }

        private static void MergeOrAdd(GameComponent_Civilizations gc, Fleet f)
        {
            foreach (Fleet o in gc.fleets)
            {
                if (o.civId == f.civId && !o.Moving && o.mission == FleetMission.Patrol && o.systemKey == f.systemKey)
                {
                    o.power += f.power;
                    return;
                }
            }
            gc.fleets.Add(f);
        }

        private static void Colonize(GameComponent_Civilizations gc, Civilization c, UniverseRand r)
        {
            GameComponent_Universe u = GameComponent_Universe.Instance;
            StarSystemData target = null;
            float radius = Mathf.Max(c.InfluenceRadiusLy, c.stage >= 10 ? 10f : 0f);
            if (radius > 0f)
            {
                List<StarEntry> stars = GalaxyQuery.StarsInRadius(u, c.galaxy, gc.CapitalPosition(c), Mathf.Min(radius, 40f));
                for (int i = 0; i < 6 && stars.Count > 0; i++)
                {
                    StarEntry e = stars[r.RangeInclusive(0, stars.Count - 1)];
                    if (u.IsHome(e.id)) continue;
                    Civilization owner = gc.OwnerOf(e.id);
                    if (owner != null && owner != c) continue;
                    target = u.GetSystem(e.id);
                    break;
                }
            }
            if (target == null) target = u.GetSystem(c.Capital);
            if (target == null) return;
            BodyData b = CivilizationGenerator.BestBody(target, c.planets.Where(p => p.systemKey == target.id.Key).Select(p => p.body));
            if (b == null) return;
            c.planets.Add(new CivPlanet { systemKey = target.id.Key, body = b.index, capacity = Mathf.Max(1f, b.sizeClass) * CivilizationGenerator.Habitability(b) * 10f, population = 0.2f });
            c.AddStock(CivResource.Metal, -20f);
            if (c.frontierPopulation > 0.5f) c.frontierPopulation -= 0.2f;
            gc.InvalidateTerritory();
        }

        private static void ConsiderWar(GameComponent_Civilizations gc, Civilization c, UniverseRand r)
        {
            if (c.NoDiplomacy || c.warWeariness > 40f) return;
            float myPower = FleetPower(gc, c);
            foreach (CivRelation rel in c.relations)
            {
                if (rel.state == DiploState.War || rel.state >= DiploState.NonAggression) continue;
                if (rel.opinion > -50f) continue;
                Civilization o = gc.Civ(rel.other);
                if (o == null || o.dead) continue;
                if (myPower < FleetPower(gc, o) * 1.3f) continue;
                if (!r.Chance(0.3f)) continue;
                gc.SetState(c, o, DiploState.War);
                LaunchFleet(gc, c, o.capitalKey, o.id, FleetMission.War, 0.6f);
                if (c.known || o.known) Find.LetterStack.ReceiveLetter("Rimverse_LetterWarLabel".Translate(), "Rimverse_LetterWar".Translate(c.ShownName, o.ShownName), LetterDefOf.NeutralEvent);
                return;
            }
        }

        private static void ConsiderPeace(GameComponent_Civilizations gc, Civilization c, UniverseRand r)
        {
            if (c.warWeariness < 60f || c.NoDiplomacy) return;
            foreach (CivRelation rel in c.relations)
            {
                if (rel.state != DiploState.War) continue;
                Civilization o = gc.Civ(rel.other);
                if (o == null || o.Hostile) continue;
                gc.SetState(c, o, DiploState.Truce);
                c.warWeariness *= 0.5f;
                if (c.known || o.known) Find.LetterStack.ReceiveLetter("Rimverse_LetterPeaceLabel".Translate(), "Rimverse_LetterPeace".Translate(c.ShownName, o.ShownName), LetterDefOf.NeutralEvent);
                return;
            }
        }

        public static Fleet LaunchFleet(GameComponent_Civilizations gc, Civilization c, string destKey, int targetCiv, FleetMission mission, float fraction)
        {
            float speed = FleetSpeedLyPerDay(c);
            if (speed <= 0f) return null;
            Fleet source = null;
            foreach (Fleet f in gc.fleets)
            {
                if (f.civId == c.id && !f.Moving && f.mission == FleetMission.Patrol && (source == null || f.power > source.power)) source = f;
            }
            if (source == null || source.power < 1f) return null;
            float power = source.power * fraction;
            source.power -= power;
            GameComponent_Universe u = GameComponent_Universe.Instance;
            double ly = u.DistanceLy(StarSystemId.FromKey(source.systemKey), StarSystemId.FromKey(destKey));
            if (double.IsInfinity(ly)) return null;
            int now = Find.TickManager.TicksGame;
            Fleet n = new Fleet
            {
                id = gc.NextFleetId(),
                civId = c.id,
                mission = mission,
                systemKey = source.systemKey,
                destKey = destKey,
                departTick = now,
                arriveTick = now + Mathf.Max(RimverseUnits.TicksPerHour * 6, RimverseUnits.TicksFromDays((float)(ly / speed))),
                power = power,
                targetCiv = targetCiv
            };
            gc.fleets.Add(n);
            return n;
        }

        private static void ConsiderPlayerRaid(GameComponent_Civilizations gc, Civilization c, UniverseRand r, float years)
        {
            if (c.playerState != DiploState.War) return;
            foreach (Fleet f in gc.fleets) if (f.civId == c.id && f.Moving && f.TargetsPlayer) return;
            double d = gc.DistanceToPlayer(c, out StarSystemId target);
            float reach = c.lore == LoreKind.Pirates ? 60f : c.InfluenceRadiusLy + 40f;
            if (!target.Valid || d > reach) return;
            float chance = (c.lore == LoreKind.Pirates ? 0.5f : 0.25f) * years * RimverseMod.Settings.threatMultiplier;
            if (c.goal == CivGoal.Arm || c.goal == CivGoal.Expand) chance *= 1.5f;
            chance *= c.lore == LoreKind.Pirates || c.Hostile ? GalacticCouncil.PirateFactor : GalacticCouncil.WarFactor;
            if (GameComponent_PlayerEmpire.Instance != null && GameComponent_PlayerEmpire.Instance.allyDefense.Count > 0) chance *= 0.8f;
            if (!r.Chance(Mathf.Clamp01(chance))) return;
            Fleet raid = LaunchFleet(gc, c, target.Key, Civilization.PlayerId, FleetMission.Raid, 0.3f);
            if (raid == null) return;
            if (c.known)
            {
                float days = RimverseUnits.DaysFromTicks(raid.arriveTick - raid.departTick);
                Find.LetterStack.ReceiveLetter("Rimverse_LetterFleetIncomingLabel".Translate(), "Rimverse_LetterFleetIncoming".Translate(c.Name, days.ToString("0.#")), LetterDefOf.ThreatBig);
            }
        }

        private static void PoliticalEvents(GameComponent_Civilizations gc, Civilization c, UniverseRand r, float years)
        {
            if (c.NoDiplomacy) return;
            int now = Find.TickManager.TicksGame;
            string own = c.ideology?.defName;
            float support = own != null && c.ideologySupport.TryGetValue(own, out float v) ? v : 50f;
            if (c.government != null && c.government.elections && c.nextElectionTick > 0 && now >= c.nextElectionTick)
            {
                c.nextElectionTick = now + r.RangeInclusive(4, 8) * RimverseUnits.TicksPerYear;
                KeyValuePair<string, float> top = c.ideologySupport.OrderByDescending(kv => kv.Value).FirstOrDefault();
                PolicyDef winner = top.Key != null ? DefDatabase<PolicyDef>.GetNamedSilentFail(top.Key) : null;
                if (winner != null && winner != c.ideology)
                {
                    c.ideology = winner;
                    Notify(c, "Rimverse_LetterElectionChange", winner.label);
                }
                return;
            }
            if (support < 25f && c.GroupMood(InterestGroup.Military) < 40f && r.Chance(0.4f * years))
            {
                PolicyDef gov = CivilizationGenerator.Pick(r, PolicyGroup.Government, c.tech, c.stage, false, p => p.profile == AiProfile.Warmonger || p.profile == AiProfile.Aggressive ? 4f : 0.3f);
                if (gov != null) c.government = gov;
                c.stability = Mathf.Max(0f, c.stability - 15f);
                c.nextElectionTick = gov != null && gov.elections ? now + 4 * RimverseUnits.TicksPerYear : -1;
                KeyValuePair<string, float> top = c.ideologySupport.OrderByDescending(kv => kv.Value).FirstOrDefault();
                PolicyDef ideo = top.Key != null ? DefDatabase<PolicyDef>.GetNamedSilentFail(top.Key) : null;
                if (ideo != null) c.ideology = ideo;
                Notify(c, "Rimverse_LetterCoup", c.government?.label ?? "?");
                return;
            }
            if (support < 40f && r.Chance(0.3f * years))
            {
                PolicyDef eco = CivilizationGenerator.Pick(r, PolicyGroup.Economy, c.tech, c.stage, false);
                if (eco != null && eco != c.economy)
                {
                    c.economy = eco;
                    c.ideologySupport[own] = support + 10f;
                    Notify(c, "Rimverse_LetterReform", eco.label);
                }
            }
            if (c.lowStabilityYears >= 2 && c.Population > 2f && gc.civilizations.Count < GameComponent_Civilizations.MaxCivilizations && r.Chance(0.5f))
            {
                Split(gc, c, r, true);
                return;
            }
            CivPlanet rebel = c.planets.FirstOrDefault(p => p.systemKey != c.capitalKey && p.loyalty < 20f);
            if (rebel != null && r.Chance(0.3f * years) && gc.civilizations.Count < GameComponent_Civilizations.MaxCivilizations)
            {
                Secede(gc, c, rebel, r);
                return;
            }
            c.leaderAge += Mathf.Max(1, Mathf.RoundToInt(years));
            float deathChance = c.leaderAge > 60 ? 0.05f + (c.leaderAge - 60) * 0.02f : 0.01f;
            if (c.government != null && c.government.succession != Succession.None && r.Chance(deathChance * years))
            {
                c.leaderAge = r.RangeInclusive(25, 55);
                if (c.government.succession == Succession.Appointed && r.Chance(0.3f))
                {
                    c.stability = Mathf.Max(0f, c.stability - 10f);
                    Notify(c, "Rimverse_LetterSuccessionCrisis", "");
                }
                else if (c.government.succession == Succession.Elective) c.nextElectionTick = now;
            }
        }

        private static void Notify(Civilization c, string key, string arg)
        {
            if (!c.known) return;
            Find.LetterStack.ReceiveLetter((key + "Label").Translate(), key.Translate(c.Name, arg), LetterDefOf.NeutralEvent);
        }

        private static Civilization NewFrom(GameComponent_Civilizations gc, Civilization parent, UniverseRand r, string capitalKey)
        {
            Civilization n = new Civilization
            {
                id = gc.NextCivId(),
                root = CivilizationGenerator.RandomRoot(r),
                stage = Mathf.Max(8, parent.stage - 1),
                sizeClass = Mathf.Max(1, parent.sizeClass / 2),
                tech = parent.tech,
                capitalKey = capitalKey,
                galaxy = parent.galaxy,
                xeno = parent.xeno,
                logistics = parent.logistics,
                origin = CivilizationGenerator.Named("Rimverse_Policy_OriginFallenEmpire") ?? parent.origin,
                known = parent.known,
                stability = 40f,
                lastSimTick = parent.lastSimTick
            };
            n.color = Color.HSVToRGB(r.Range(0f, 1f), 0.7f, 0.9f);
            CivilizationGenerator.FillPolicies(n, r);
            if (n.ideology != null) n.ideologySupport[n.ideology.defName] = 60f;
            foreach (InterestGroup g in Enum.GetValues(typeof(InterestGroup))) n.groupMood[g] = 50f;
            return n;
        }

        private static void Split(GameComponent_Civilizations gc, Civilization c, UniverseRand r, bool civilWar)
        {
            List<CivPlanet> outer = c.planets.Where(p => p.systemKey != c.capitalKey).ToList();
            string capital = outer.Count > 0 ? outer[r.RangeInclusive(0, outer.Count - 1)].systemKey : null;
            if (capital == null)
            {
                Fleet any = gc.fleets.FirstOrDefault(f => f.civId == c.id);
                if (any == null) return;
                capital = c.capitalKey;
            }
            Civilization n = NewFrom(gc, c, r, capital);
            foreach (CivPlanet p in outer.Where((p, i) => i % 2 == 0 || p.systemKey == capital).ToList())
            {
                c.planets.Remove(p);
                n.planets.Add(p);
            }
            n.frontierPopulation = c.frontierPopulation * 0.5f;
            c.frontierPopulation *= 0.5f;
            foreach (Fleet f in gc.fleets.Where(f => f.civId == c.id).ToList())
            {
                Fleet half = new Fleet { id = gc.NextFleetId(), civId = n.id, mission = FleetMission.Patrol, systemKey = capital, power = f.power * 0.5f };
                f.power *= 0.5f;
                gc.fleets.Add(half);
            }
            for (int i = 0; i < c.stock.Count; i++) n.AddStock((CivResource)i, c.stock[i] * 0.4f);
            gc.civilizations.Add(n);
            gc.SetState(c, n, DiploState.War);
            CivRelation a = c.RelationWith(n.id, true), b = n.RelationWith(c.id, true);
            a.opinion = b.opinion = a.baseOpinion = b.baseOpinion = -70f;
            c.lowStabilityYears = 0;
            c.stability = Mathf.Min(100f, c.stability + 15f);
            gc.InvalidateTerritory();
            if (c.known) Find.LetterStack.ReceiveLetter("Rimverse_LetterCivilWarLabel".Translate(), "Rimverse_LetterCivilWar".Translate(c.Name, n.Name), LetterDefOf.NeutralEvent);
        }

        private static void Secede(GameComponent_Civilizations gc, Civilization c, CivPlanet planet, UniverseRand r)
        {
            Civilization n = NewFrom(gc, c, r, planet.systemKey);
            n.stage = Mathf.Min(n.stage, 9);
            c.planets.Remove(planet);
            n.planets.Add(planet);
            gc.civilizations.Add(n);
            CivRelation a = c.RelationWith(n.id, true), b = n.RelationWith(c.id, true);
            a.opinion = b.opinion = a.baseOpinion = b.baseOpinion = -40f;
            gc.SetState(c, n, r.Chance(0.5f) ? DiploState.War : DiploState.Peace);
            gc.InvalidateTerritory();
            if (c.known) Find.LetterStack.ReceiveLetter("Rimverse_LetterSecessionLabel".Translate(), "Rimverse_LetterSecession".Translate(c.Name, n.Name), LetterDefOf.NeutralEvent);
        }

        private static readonly TechLevel[] StageTech = { TechLevel.Neolithic, TechLevel.Neolithic, TechLevel.Neolithic, TechLevel.Medieval, TechLevel.Medieval, TechLevel.Medieval, TechLevel.Industrial, TechLevel.Industrial, TechLevel.Industrial, TechLevel.Spacer, TechLevel.Spacer, TechLevel.Spacer, TechLevel.Spacer, TechLevel.Ultra, TechLevel.Ultra };

        private static void StageChange(GameComponent_Civilizations gc, Civilization c, float years)
        {
            if (c.stage < 13 && c.stableYears >= 5)
            {
                int next = c.stage + 1;
                if (c.tech >= StageTech[next] && c.Population >= CivilizationGenerator.StagePopMin[next])
                {
                    c.stage = next;
                    c.stableYears = 0;
                    gc.InvalidateTerritory();
                    Notify(c, "Rimverse_LetterStageUp", ("Rimverse_Stage_" + next).Translate());
                    return;
                }
            }
            if (c.stage > 1 && c.Population < CivilizationGenerator.StagePopMin[c.stage] * 0.3f)
            {
                c.stage--;
                gc.InvalidateTerritory();
                Notify(c, "Rimverse_LetterStageDown", ("Rimverse_Stage_" + c.stage).Translate());
            }
            if (c.Population < 0.05f && c.planets.Count == 0)
            {
                c.dead = true;
                gc.fleets.RemoveAll(f => f.civId == c.id);
                gc.InvalidateTerritory();
                Notify(c, "Rimverse_LetterCivFallen", "");
            }
        }
    }
}
