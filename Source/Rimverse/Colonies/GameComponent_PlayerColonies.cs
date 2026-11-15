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
            float target = 50f + social;
            if (colonies.Count > limit) target -= 2f * (colonies.Count - limit);
            if (hazard > 1.5f) target -= 10f;
            float food = (hazard <= 1.3f ? 10f : 0f) + industriesReady(c, ColonyIndustry.Agriculture) * 20f;
            bool starving = c.population > food && food < c.population * 0.8f;
            if (starving) target -= 10f;
            c.stability = Mathf.Clamp(Mathf.Lerp(c.stability, target, 0.3f), 0f, 100f);
            float growth = starving ? -0.05f : 0.05f;
            c.population = Mathf.Clamp(c.population * (1f + growth) + (starving ? 0f : 1f), 1f, Mathf.Max(c.Capacity, c.settlers.Count));
            float upkeep = c.industries.Where(b => b.Ready).Sum(b => ColonyUtility.Upkeep(b.type)) * hazard;
            float income = c.population * 10f * (c.stability / 50f) * (1f + social * 0.01f);
            if (c.Has(ColonyIndustry.TradePort)) income *= 1.2f;
            c.lastIncome = income - upkeep;
            treasury = Mathf.Max(0, treasury + Mathf.RoundToInt(c.lastIncome));
            BodyData b = c.Body;
            int size = b?.sizeClass ?? 30;
            int prodFactor = c.stability >= 20f ? 1 : 0;
            c.AddStock("Steel", industriesReady(c, ColonyIndustry.Mining) * 40 * size / 20 * prodFactor);
            c.AddStock("Chemfuel", (industriesReady(c, ColonyIndustry.GasHarvest) * 60 + industriesReady(c, ColonyIndustry.Refinery) * 40) * prodFactor);
            c.AddStock("Rimverse_WarpMatter", industriesReady(c, ColonyIndustry.Refinery) * 10 * prodFactor);
            c.AddStock("ComponentIndustrial", industriesReady(c, ColonyIndustry.Factory) * 10 * prodFactor);
            c.AddStock("ComponentSpacer", industriesReady(c, ColonyIndustry.Factory) * 2 * prodFactor);
            Civilization owner = GameComponent_Civilizations.Instance?.generated == true ? GameComponent_Civilizations.Instance.OwnerOf(c.SystemId) : null;
            c.attention += c.population * 0.5f + c.Wealth / 10000f + (owner != null ? 30f : 0f);
            if (owner != null && !owner.dead) GameComponent_Civilizations.Instance.ChangePlayerOpinion(owner, -2f);
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
            float defense = 10f + industriesReady(c, ColonyIndustry.MilitaryBase) * 50f + (admin?.skills.GetSkill(SkillDefOf.Shooting).Level ?? 0) + c.population * 0.2f;
            float threat = 20f + c.Wealth / 1000f;
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
