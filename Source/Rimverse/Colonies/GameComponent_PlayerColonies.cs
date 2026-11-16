using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public class GameComponent_PlayerColonies : GameComponent
    {
        public List<PlayerColony> colonies = new List<PlayerColony>();
        public int treasury;
        public int nextId = 1;

        public static GameComponent_PlayerColonies Instance => Current.Game?.GetComponent<GameComponent_PlayerColonies>();

        public GameComponent_PlayerColonies(Game game)
        {
        }

        public PlayerColony ColonyAt(string systemKey, int body) => colonies.FirstOrDefault(c => c.systemKey == systemKey && c.bodyIndex == body);

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (now % RimverseUnits.TicksPerHour != 911 || colonies.Count == 0) return;
            int limit = ColonyUtility.ColonyLimit();
            foreach (PlayerColony c in colonies.ToList())
            {
                ColonySituations.Consider(c);
                if (now >= c.nextStepTick)
                {
                    c.nextStepTick += RimverseUnits.TicksPerQuadrum;
                    Step(c, limit);
                }
            }
        }

        private void Step(PlayerColony c, int limit)
        {
            float hazard = c.Hazard;
            Pawn admin = c.Administrator;
            int social = admin?.skills.GetSkill(SkillDefOf.Social).Level ?? 0;
            if (GameComponent_ColonyExtras.HasAI(c)) social = Mathf.Max(social, 16);
            StationBonus sb = StationLinkUtility.For(c.systemKey);
            GameComponent_PlayerEmpire emp = GameComponent_PlayerEmpire.Instance;
            EmpireEffects ef = emp != null ? emp.ColonyEffects(c) : new EmpireEffects();
            float target = 50f + social + sb.stability + ef.stability;
            if (c.captured > 0) target -= 10f;
            if (c.tithe == TitheMode.Silver) target -= 5f;
            if (c.tithe == TitheMode.Food) target += 3f;
            if (colonies.Count > limit) target -= 2f * (colonies.Count - limit);
            if (hazard > 1.5f) target -= 10f;
            float food = (hazard <= 1.3f ? 10f : 0f) + industriesReady(c, ColonyIndustry.Agriculture) * 20f + sb.food + ef.food + (c.surface ? 15f : 0f);
            bool starving = c.population > food && food < c.population * 0.8f;
            if (starving) target -= 10f;
            c.stability = Mathf.Clamp(Mathf.Lerp(c.stability, target, 0.3f), 0f, 100f);
            float growth = starving ? -0.05f : 0.05f + ef.growth * 0.05f + (c.tithe == TitheMode.Food ? 0.02f : 0f);
            c.population = Mathf.Clamp(c.population * (1f + growth) + (starving ? 0f : 1f), 1f, Mathf.Max(c.Capacity + sb.capacity + ef.capacity, c.settlers.Count));
            float upkeep = c.industries.Where(b => b.Ready).Sum(b => ColonyUtility.Upkeep(b.type)) * hazard;
            upkeep += c.facilities.Where(f => f.Ready && f.def != null).Sum(f => f.def.upkeep) * hazard;
            float income = c.population * 10f * (c.stability / 50f) * (1f + social * 0.01f);
            if (c.Has(ColonyIndustry.TradePort)) income *= 1.2f;
            income *= 1f + sb.income;
            income *= Mathf.Max(0.1f, 1f + ef.income);
            income *= Mathf.Max(0.5f, GalacticCouncil.TariffFactor);
            if (c.tithe == TitheMode.Silver) income *= 1.15f;
            else if (c.tithe == TitheMode.Goods) income *= 0.6f;
            else if (c.tithe == TitheMode.Food) income *= 0.8f;
            c.lastIncome = income - upkeep;
            treasury = Mathf.Max(0, treasury + Mathf.RoundToInt(c.lastIncome));
            BodyData b = c.Body;
            int size = b?.sizeClass ?? 30;
            int prodFactor = c.stability >= 20f && c.tithe != TitheMode.Silver ? (c.tithe == TitheMode.Goods ? 2 : 1) : 0;
            if (c.tithe == TitheMode.Silver && c.stability >= 20f) treasury += Mathf.RoundToInt(c.population * 2f);
            foreach (ColonyFacility f in c.facilities) if (f.Ready && f.def != null) foreach (ThingDefCountClass o in f.def.output) if (o.thingDef != null) c.AddStock(o.thingDef.defName, o.count * prodFactor);
            c.AddStock("Steel", industriesReady(c, ColonyIndustry.Mining) * 40 * size / 20 * prodFactor);
            c.AddStock("Chemfuel", (industriesReady(c, ColonyIndustry.GasHarvest) * 60 + industriesReady(c, ColonyIndustry.Refinery) * 40) * prodFactor);
            c.AddStock("Rimverse_WarpMatter", industriesReady(c, ColonyIndustry.Refinery) * 10 * prodFactor);
            c.AddStock("ComponentIndustrial", industriesReady(c, ColonyIndustry.Factory) * 10 * prodFactor);
            c.AddStock("Steel", sb.steel * prodFactor);
            c.AddStock("Chemfuel", sb.chemfuel * prodFactor);
            c.AddStock("ComponentIndustrial", sb.components * prodFactor);
            c.AddStock("ComponentSpacer", industriesReady(c, ColonyIndustry.Factory) * 2 * prodFactor);
            Civilization owner = GameComponent_Civilizations.Instance?.generated == true ? GameComponent_Civilizations.Instance.OwnerOf(c.SystemId) : null;
            c.attention += (c.population * 0.5f + c.Wealth / 10000f + (owner != null ? 30f : 0f)) * sb.attention * Mathf.Max(0.2f, 1f + ef.attention);
            c.strength = 10f + sb.defense + industriesReady(c, ColonyIndustry.MilitaryBase) * 50f + c.population * 0.2f + ef.defense + (emp != null ? EmpireDiplomacy.AllyDefense(emp, c.systemKey) : 0f);
            if (owner != null && !owner.dead) GameComponent_Civilizations.Instance.ChangePlayerOpinion(owner, -2f + sb.opinion);
            if (!c.warned && c.attention >= ColonyUtility.WarnAttention)
            {
                c.warned = true;
                Find.LetterStack.ReceiveLetter("Rimverse_LetterColonyWarnLabel".Translate(), "Rimverse_LetterColonyWarn".Translate(c.name), LetterDefOf.ThreatSmall);
            }
            if (c.attention >= ColonyUtility.AttackAttention)
            {
                c.attention = 0f;
                c.warned = false;
                Attack(c, admin);
            }
        }

        private static int industriesReady(PlayerColony c, ColonyIndustry t) => c.industries.Count(b => b.type == t && b.Ready);

        private void Attack(PlayerColony c, Pawn admin)
        {
            Faction pirates = ShipCombatUtility.PirateFaction(GameComponent_Civilizations.Instance);
            Map map = ColonyUtility.PlayerMapInSystem(c.systemKey);
            if (map != null && pirates != null)
            {
                RimverseShip ship = ColonyUtility.PlayerShipOn(map);
                if (ship != null && SpaceMapUtility.IsOrbit(map) && RimverseMod.Settings.shipCombatEnabled && ShipCombatUtility.StartEncounter(map, ship, pirates, c.Wealth / 1000f + 20f, TechLevel.Spacer, -2)) return;
            }
            GameComponent_PlayerEmpire emp = GameComponent_PlayerEmpire.Instance;
            float defense = 10f + StationLinkUtility.For(c.systemKey).defense + industriesReady(c, ColonyIndustry.MilitaryBase) * 50f + (admin?.skills.GetSkill(SkillDefOf.Shooting).Level ?? 0) + c.population * 0.2f;
            if (emp != null) defense += emp.ColonyEffects(c).defense + EmpireDiplomacy.AllyDefense(emp, c.systemKey);
            float threat = (20f + c.Wealth / 1000f) * GalacticCouncil.PirateFactor;
            bool held = Rand.Chance(defense / (defense + threat));
            if (held)
            {
                c.stability = Mathf.Max(0f, c.stability - 5f);
                Find.LetterStack.ReceiveLetter("Rimverse_LetterColonyHeldLabel".Translate(), "Rimverse_LetterColonyHeld".Translate(c.name), LetterDefOf.NeutralEvent);
                return;
            }
            c.population *= 0.7f;
            c.stability = Mathf.Max(0f, c.stability - 20f);
            ColonyBuild lost = c.industries.Where(b => b.Ready).RandomElementWithFallback();
            if (lost != null) c.industries.Remove(lost);
            c.stock.Clear();
            Find.LetterStack.ReceiveLetter("Rimverse_LetterColonyRaidedLabel".Translate(), "Rimverse_LetterColonyRaided".Translate(c.name, lost != null ? ("Rimverse_Industry_" + lost.type).Translate().ToString() : "-"), LetterDefOf.NegativeEvent);
        }

        public bool TryBuild(PlayerColony c, ColonyIndustry t)
        {
            int cost = ColonyUtility.Cost(t);
            if (treasury < cost) return false;
            treasury -= cost;
            c.industries.Add(new ColonyBuild { type = t, readyTick = Find.TickManager.TicksGame + ColonyUtility.Days(t) * RimverseUnits.TicksPerDay });
            return true;
        }

        public AcceptanceReport CanBuildFacility(PlayerColony c, ColonyFacilityDef d)
        {
            if (d.research != null && !d.research.IsFinished) return "Rimverse_ColonyNeedResearch".Translate(d.research.LabelCap);
            if (c.Tier < d.minTier) return "Rimverse_FacilityNeedTier".Translate(("Rimverse_Tier_" + d.minTier).Translate());
            if (d.surfaceOnly && !c.surface) return "Rimverse_FacilitySurfaceOnly".Translate();
            if (d.spaceOnly && c.surface) return "Rimverse_FacilitySpaceOnly".Translate();
            if (c.FacilityCount(d) >= d.maxCount) return "Rimverse_FacilityMax".Translate();
            if (treasury < d.costSilver) return "Rimverse_NotEnoughTreasury".Translate(d.costSilver);
            return true;
        }

        public bool TryBuildFacility(PlayerColony c, ColonyFacilityDef d)
        {
            AcceptanceReport r = CanBuildFacility(c, d);
            if (!r.Accepted)
            {
                if (!r.Reason.NullOrEmpty()) Messages.Message(r.Reason, MessageTypeDefOf.RejectInput, false);
                return false;
            }
            treasury -= d.costSilver;
            c.facilities.Add(new ColonyFacility { def = d, readyTick = Find.TickManager.TicksGame + d.days * RimverseUnits.TicksPerDay });
            return true;
        }

        public void Collect(PlayerColony c, Map map)
        {
            List<Thing> things = new List<Thing>();
            foreach (KeyValuePair<string, int> kv in c.stock)
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(kv.Key);
                if (def != null) ColonyUtility.AddStacks(things, def, kv.Value);
            }
            c.stock.Clear();
            ColonyUtility.Deliver(map, things);
        }

        public void Recall(PlayerColony c, Map map)
        {
            List<Pawn> pawns = c.settlers.Where(p => p != null && !p.Dead).ToList();
            foreach (Pawn p in pawns) if (Find.WorldPawns.Contains(p)) Find.WorldPawns.RemovePawn(p);
            ColonyUtility.Deliver(map, pawns.Cast<Thing>().ToList());
            ColonyUtility.Deliver(map, c.stock.SelectMany(kv => { List<Thing> l = new List<Thing>(); ColonyUtility.AddStacks(l, DefDatabase<ThingDef>.GetNamedSilentFail(kv.Key), kv.Value); return l; }).ToList());
            colonies.Remove(c);
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref colonies, "colonies", LookMode.Deep);
            Scribe_Values.Look(ref treasury, "treasury");
            Scribe_Values.Look(ref nextId, "nextId", 1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && colonies == null) colonies = new List<PlayerColony>();
        }
    }
}
