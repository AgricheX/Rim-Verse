using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public static class CivilizationGenerator
    {
        private static readonly string[] SylA = { "Tes", "Var", "Kor", "Ael", "Dra", "Mir", "Sol", "Ith", "Bel", "Zan", "Oro", "Vel", "Cyr", "Hal", "Nem", "Quor", "Ras", "Ul", "Ves", "Yth" };
        private static readonly string[] SylB = { "sar", "dun", "ith", "ora", "eth", "an", "ix", "umar", "ova", "ell", "or", "adan", "ys", "ek", "aris", "ont" };
        private static readonly string[] SylC = { "", "", "", "ia", "ar", "on", "is", "ea" };

        private static readonly int[] StageMin = { 0, 20, 15, 8, 3, 1 };
        private static readonly int[] StageMax = { 0, 60, 40, 20, 8, 3 };

        public static readonly float[] StagePopMin = { 0f, 0f, 0.01f, 0.05f, 0.2f, 1f, 10f, 50f, 100f, 200f, 1000f, 10000f, 200000f, 5000000f, 5000000f };

        public static string RandomRoot(UniverseRand r)
        {
            return SylA[r.RangeInclusive(0, SylA.Length - 1)] + SylB[r.RangeInclusive(0, SylB.Length - 1)] + SylC[r.RangeInclusive(0, SylC.Length - 1)];
        }

        public static float Habitability(BodyData b)
        {
            if (!b.Landable) return 0f;
            float h;
            switch (b.atmosphere)
            {
                case AtmosphereType.Breathable: h = 1f; break;
                case AtmosphereType.Thin:
                case AtmosphereType.Dense: h = 0.4f; break;
                case AtmosphereType.Toxic: h = 0.2f; break;
                default: h = 0.1f; break;
            }
            float t = Mathf.Abs(b.temperatureC - 15f);
            h *= Mathf.Clamp01(1.2f - t / 60f);
            h *= Mathf.Clamp01(1.3f - Mathf.Abs(b.gravity - 1f));
            return Mathf.Max(0.02f, h);
        }

        public static BodyData BestBody(StarSystemData sys, IEnumerable<int> exclude = null)
        {
            HashSet<int> ex = exclude != null ? new HashSet<int>(exclude) : new HashSet<int>();
            BodyData best = null;
            float bestScore = -1f;
            foreach (BodyData b in sys.bodies)
            {
                if (b.homeWorld || !b.Landable || ex.Contains(b.index)) continue;
                float s = Habitability(b) * Mathf.Max(5, b.sizeClass);
                if (s > bestScore)
                {
                    bestScore = s;
                    best = b;
                }
            }
            return best;
        }

        public static CivPlanet MakePlanet(StarSystemData sys, BodyData b, float fill)
        {
            float cap = Mathf.Max(1f, b.sizeClass) * Habitability(b) * 10f;
            return new CivPlanet { systemKey = sys.id.Key, body = b.index, capacity = cap, population = cap * fill };
        }

        public static StarEntry FindStarNear(GameComponent_Universe u, int galaxy, Vec3d pos, HashSet<string> taken)
        {
            GalaxyData g = u.Galaxy(galaxy);
            DistrictId d0 = DistrictId.FromPosition(galaxy, pos);
            StarEntry best = null;
            double bestDist = double.MaxValue;
            for (int r = 0; r <= 2 && best == null; r++)
            {
                for (long x = d0.GlobalX - r; x <= d0.GlobalX + r; x++)
                for (long y = d0.GlobalY - r; y <= d0.GlobalY + r; y++)
                for (long z = d0.GlobalZ - r; z <= d0.GlobalZ + r; z++)
                {
                    foreach (StarEntry e in GalaxyQuery.StarsInDistrict(u, g, DistrictId.FromGlobal(galaxy, x, y, z)))
                    {
                        if (u.IsHome(e.id) || taken.Contains(e.id.Key)) continue;
                        double dist = Vec3d.Distance(e.position, pos);
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            best = e;
                        }
                    }
                }
            }
            return best;
        }

        public static PolicyDef Pick(UniverseRand r, PolicyGroup group, TechLevel tech, int stage, bool allowLore, Func<PolicyDef, float> extraWeight = null)
        {
            List<PolicyDef> list = DefDatabase<PolicyDef>.AllDefsListForReading.Where(p => p.group == group && p.Allowed(tech, stage) && (allowLore || !p.loreOnly)).ToList();
            if (list.Count == 0) list = DefDatabase<PolicyDef>.AllDefsListForReading.Where(p => p.group == group && !p.loreOnly).ToList();
            if (list.Count == 0) return null;
            float[] w = list.Select(p => p.weight * (extraWeight != null ? extraWeight(p) : 1f)).ToArray();
            return r.Weighted(list.ToArray(), w);
        }

        public static PolicyDef Named(string defName) => DefDatabase<PolicyDef>.GetNamedSilentFail(defName);

        public static TechLevel TechFor(UniverseRand r, int stage)
        {
            if (stage <= 8) return r.Chance(0.6f) ? TechLevel.Industrial : TechLevel.Spacer;
            if (stage <= 11) return r.Chance(0.1f) ? TechLevel.Ultra : TechLevel.Spacer;
            if (stage == 12) return r.Chance(0.3f) ? TechLevel.Ultra : TechLevel.Spacer;
            return TechLevel.Ultra;
        }

        public static void FillPolicies(Civilization c, UniverseRand r)
        {
            if (c.government == null) c.government = Pick(r, PolicyGroup.Government, c.tech, c.stage, false);
            if (c.ideology == null) c.ideology = Pick(r, PolicyGroup.Ideology, c.tech, c.stage, false, p => p.profile == c.government?.profile ? 3f : 1f);
            if (c.economy == null) c.economy = Pick(r, PolicyGroup.Economy, c.tech, c.stage, false);
            if (c.xeno == null) c.xeno = Pick(r, PolicyGroup.Xeno, c.tech, c.stage, false);
            if (c.logistics == null) c.logistics = Named("Rimverse_Policy_LogisticsWarp");
            if (c.origin == null) c.origin = Pick(r, PolicyGroup.Origin, c.tech, c.stage, false);
            int civicCount = r.Chance(0.5f) ? 2 : 3;
            int guard = 0;
            while (c.civics.Count < civicCount && guard++ < 20)
            {
                PolicyDef civic = Pick(r, PolicyGroup.Civic, c.tech, c.stage, false, p => p.defName == "Rimverse_Policy_CivicPirates" ? 0.05f : 1f);
                if (civic != null && !c.civics.Contains(civic)) c.civics.Add(civic);
            }
        }

        public static void InitState(Civilization c, UniverseRand r, GameComponent_Universe u)
        {
            float h = r.Range(0f, 1f);
            c.color = Color.HSVToRGB(h, r.Range(0.45f, 0.85f), r.Range(0.7f, 1f));
            c.stability = r.Range(40f, 70f);
            c.leaderAge = r.RangeInclusive(30, 70);
            StarSystemData sys = u.GetSystem(c.Capital);
            BodyData capital = BestBody(sys);
            if (capital != null) c.planets.Add(MakePlanet(sys, capital, r.Range(0.6f, 0.95f)));
            c.lineage = LineageUtility.ForHome(capital, c.lore, r.Range(0f, 1f));
            if (c.stage >= 9 && sys != null)
            {
                int extra = Mathf.Clamp(c.stage - 8, 0, 4);
                for (int i = 0; i < extra; i++)
                {
                    BodyData b = BestBody(sys, c.planets.Select(p => p.body));
                    if (b == null) break;
                    c.planets.Add(MakePlanet(sys, b, r.Range(0.2f, 0.6f)));
                }
            }
            float target = StagePopMin[Mathf.Clamp(c.stage, 1, 14)] * r.Range(1.2f, 3f);
            c.frontierPopulation = Mathf.Max(0f, target - c.Population);
            float pop = c.Population;
            foreach (CivResource res in Enum.GetValues(typeof(CivResource))) c.AddStock(res, pop * r.Range(1f, 3f));
            c.treasury = pop * 200f;
            if (c.ideology != null) c.ideologySupport[c.ideology.defName] = r.Range(45f, 70f);
            float rest = 100f - (c.ideology != null ? c.ideologySupport[c.ideology.defName] : 0f);
            for (int i = 0; i < 2; i++)
            {
                PolicyDef other = Pick(r, PolicyGroup.Ideology, c.tech, c.stage, false);
                if (other == null || c.ideologySupport.ContainsKey(other.defName)) continue;
                float share = i == 0 ? rest * r.Range(0.4f, 0.7f) : rest;
                c.ideologySupport[other.defName] = share;
                rest -= share;
            }
            foreach (InterestGroup g in Enum.GetValues(typeof(InterestGroup))) c.groupMood[g] = 50f;
            c.lastSimTick = Find.TickManager?.TicksGame ?? 0;
            if (c.government != null && c.government.elections) c.nextElectionTick = c.lastSimTick + r.RangeInclusive(1, 8) * RimverseUnits.TicksPerYear;
            if (c.Hostile)
            {
                c.playerOpinion = -80f;
                c.playerState = DiploState.War;
            }
        }

        private static Civilization Place(GameComponent_Civilizations civs, GameComponent_Universe u, UniverseRand r, Vec3d pos, int stage, HashSet<string> taken)
        {
            StarEntry star = FindStarNear(u, 0, pos, taken);
            if (star == null) return null;
            taken.Add(star.id.Key);
            Civilization c = new Civilization
            {
                id = civs.NextCivId(),
                root = RandomRoot(r),
                stage = stage,
                sizeClass = r.SkewedSizeClass(1, 100, 1.8f),
                capitalKey = star.id.Key,
                galaxy = 0
            };
            c.tech = TechFor(r, stage);
            return c;
        }

        private static Vec3d AroundHome(GameComponent_Universe u, UniverseRand r, double minLy, double maxLy)
        {
            Vec3d home = u.PositionOf(u.HomeSystemId);
            double a = r.ValueDouble * Math.PI * 2.0;
            double d = minLy + (maxLy - minLy) * r.ValueDouble;
            return home + new Vec3d(Math.Cos(a) * d, (r.ValueDouble - 0.5) * Math.Min(d, 60.0) * 0.4, Math.Sin(a) * d);
        }

        public static void GenerateAll(GameComponent_Civilizations civs)
        {
            GameComponent_Universe u = GameComponent_Universe.Instance;
            if (u == null) return;
            u.EnsureInitialized();
            UniverseRand r = new UniverseRand(UniverseRand.Hash(u.universeSeed, 700));
            HashSet<string> taken = new HashSet<string>();
            GalaxyData g = u.Galaxy(0);
            AddLore(civs, u, r, taken);
            float scale = g.sizeClass / 50f * RimverseMod.Settings.civilizationDensity;
            int total = civs.civilizations.Count;
            for (int stageIdx = 1; stageIdx <= 5; stageIdx++)
            {
                int stage = 7 + stageIdx;
                int count = Mathf.RoundToInt(r.RangeInclusive(StageMin[stageIdx], StageMax[stageIdx]) * scale);
                for (int i = 0; i < count && total < GameComponent_Civilizations.MaxCivilizations; i++)
                {
                    Vec3d pos;
                    if (r.Chance(0.35f)) pos = AroundHome(u, r, 20, 600);
                    else
                    {
                        double a = r.ValueDouble * Math.PI * 2.0;
                        double rad = g.radiusLy * Math.Sqrt(r.ValueDouble) * 0.95;
                        pos = new Vec3d(Math.Cos(a) * rad, (r.ValueDouble - 0.5) * g.thicknessLy * 0.5, Math.Sin(a) * rad);
                    }
                    Civilization c = Place(civs, u, r, pos, stage, taken);
                    if (c == null) continue;
                    FillPolicies(c, r);
                    InitState(c, r, u);
                    civs.civilizations.Add(c);
                    total++;
                }
            }
            civs.InitRelations();
        }

        private static void AddLore(GameComponent_Civilizations civs, GameComponent_Universe u, UniverseRand r, HashSet<string> taken)
        {
            Civilization empire = Place(civs, u, r, AroundHome(u, r, 300, 700), 12, taken);
            if (empire != null)
            {
                empire.root = "Sophiamunda";
                empire.lore = LoreKind.Empire;
                empire.sizeClass = Math.Max(70, empire.sizeClass);
                empire.tech = TechLevel.Ultra;
                empire.government = Named("Rimverse_Policy_StellarEmpire");
                empire.ideology = Named("Rimverse_Policy_Legalism");
                empire.economy = Named("Rimverse_Policy_Tributary");
                empire.origin = Named("Rimverse_Policy_OriginFallenEmpire");
                empire.xeno = Named("Rimverse_Policy_XenoNeutral");
                AddCivic(empire, "Rimverse_Policy_CivicNobleHouses");
                FinishLore(civs, u, r, empire);
            }
            Civilization glitter = Place(civs, u, r, AroundHome(u, r, 60, 150), 10, taken);
            if (glitter != null)
            {
                glitter.lore = LoreKind.Glitterworld;
                glitter.tech = TechLevel.Ultra;
                glitter.government = Named("Rimverse_Policy_PlanetaryFederation");
                glitter.ideology = Named("Rimverse_Policy_Humanism");
                glitter.origin = Named("Rimverse_Policy_OriginGlitterworld");
                glitter.xeno = Named("Rimverse_Policy_Xenophilia");
                AddCivic(glitter, "Rimverse_Policy_CivicScholars");
                FinishLore(civs, u, r, glitter);
            }
            Civilization urb = Place(civs, u, r, AroundHome(u, r, 40, 120), 10, taken);
            if (urb != null)
            {
                urb.lore = LoreKind.Urbworld;
                urb.government = Named("Rimverse_Policy_Corporatocracy");
                urb.economy = Named("Rimverse_Policy_LaissezFaire");
                urb.origin = Named("Rimverse_Policy_OriginUrbworld");
                AddCivic(urb, "Rimverse_Policy_CivicTraders");
                AddCivic(urb, "Rimverse_Policy_CivicMercenaries");
                FinishLore(civs, u, r, urb);
            }
            Civilization pirates = Place(civs, u, r, AroundHome(u, r, 12, 35), 9, taken);
            if (pirates != null)
            {
                pirates.lore = LoreKind.Pirates;
                pirates.government = Named("Rimverse_Policy_MilitaryJunta");
                pirates.ideology = Named("Rimverse_Policy_Militarism");
                pirates.origin = Named("Rimverse_Policy_OriginVoidborn");
                AddCivic(pirates, "Rimverse_Policy_CivicPirates");
                AddCivic(pirates, "Rimverse_Policy_CivicWarriorCulture");
                FinishLore(civs, u, r, pirates);
            }
            Civilization mechs = Place(civs, u, r, AroundHome(u, r, 150, 400), 11, taken);
            if (mechs != null)
            {
                mechs.root = "Synthesis";
                mechs.lore = LoreKind.Mechanoids;
                mechs.tech = TechLevel.Ultra;
                mechs.government = Named("Rimverse_Policy_HiveMind");
                mechs.ideology = Named("Rimverse_Policy_Transhumanism");
                mechs.economy = Named("Rimverse_Policy_Planned");
                mechs.origin = Named("Rimverse_Policy_OriginMechanoid");
                mechs.xeno = Named("Rimverse_Policy_Xenophobia");
                AddCivic(mechs, "Rimverse_Policy_CivicAIServants");
                AddCivic(mechs, "Rimverse_Policy_CivicShipwrights");
                FinishLore(civs, u, r, mechs);
            }
            Civilization neighbor = Place(civs, u, r, AroundHome(u, r, 6, 20), 9, taken);
            if (neighbor != null)
            {
                neighbor.lore = LoreKind.Neighbor;
                neighbor.xeno = Named("Rimverse_Policy_XenoNeutral");
                FinishLore(civs, u, r, neighbor);
            }
        }

        public static Civilization CreateCrisis(GameComponent_Civilizations civs, CrisisKind kind)
        {
            GameComponent_Universe u = GameComponent_Universe.Instance;
            UniverseRand r = new UniverseRand(UniverseRand.Hash(u.universeSeed, 7300, Find.TickManager.TicksGame));
            HashSet<string> taken = new HashSet<string>(civs.civilizations.Select(x => x.capitalKey));
            bool invasion = kind == CrisisKind.Invasion;
            Civilization c = Place(civs, u, r, AroundHome(u, r, invasion ? 900 : 400, invasion ? 1500 : 900), 13, taken);
            if (c == null) return null;
            c.sizeClass = 100;
            c.tech = invasion ? TechLevel.Ultra : TechLevel.Archotech;
            if (kind == CrisisKind.MechanoidSwarm) c.root = "Swarm";
            else if (kind == CrisisKind.ArchotechAwakening) c.root = "Ascendant";
            else c.root = "Outsider";
            if (invasion)
            {
                c.lore = LoreKind.None;
                c.government = Named("Rimverse_Policy_MilitaryJunta");
                c.ideology = Named("Rimverse_Policy_Militarism");
                AddCivic(c, "Rimverse_Policy_CivicPirates");
                AddCivic(c, "Rimverse_Policy_CivicWarriorCulture");
            }
            else
            {
                c.lore = LoreKind.Mechanoids;
                c.government = Named("Rimverse_Policy_HiveMind");
                c.ideology = Named("Rimverse_Policy_Transhumanism");
                c.economy = Named("Rimverse_Policy_Planned");
                c.xeno = Named("Rimverse_Policy_Xenophobia");
                AddCivic(c, "Rimverse_Policy_CivicShipwrights");
            }
            FinishLore(civs, u, r, c);
            return c;
        }

        private static void AddCivic(Civilization c, string defName)
        {
            PolicyDef p = Named(defName);
            if (p != null && !c.civics.Contains(p)) c.civics.Add(p);
        }

        private static void FinishLore(GameComponent_Civilizations civs, GameComponent_Universe u, UniverseRand r, Civilization c)
        {
            FillPolicies(c, r);
            InitState(c, r, u);
            civs.civilizations.Add(c);
        }
    }
}
