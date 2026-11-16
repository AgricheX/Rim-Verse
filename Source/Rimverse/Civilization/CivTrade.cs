using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public class StockGenerator_BuyDrugs : StockGenerator
    {
        public override IEnumerable<Thing> GenerateThings(PlanetTile forTile, Faction faction = null)
        {
            yield break;
        }

        public override bool HandlesThingDef(ThingDef thingDef) => thingDef.IsDrug;
    }

    public class CompUseEffect_RimverseContract : CompUseEffect
    {
        public override void DoEffect(Pawn usedBy)
        {
            base.DoEffect(usedBy);
            Map map = usedBy.MapHeld;
            float points = map != null ? StorytellerUtility.DefaultThreatPointsNow(map) : 500f;
            List<QuestScriptDef> roots = DefDatabase<QuestScriptDef>.AllDefsListForReading.Where(q => q.defName.StartsWith("Rimverse_Contract_") && map != null && q.CanRun(points, map)).ToList();
            if (roots.Count == 0)
            {
                Messages.Message("Rimverse_ContractNone".Translate(), usedBy, MessageTypeDefOf.RejectInput, false);
                return;
            }
            Quest q = QuestUtility.GenerateQuestAndMakeAvailable(roots.RandomElementByWeight(r => r.rootSelectionWeight), points);
            QuestUtility.SendLetterQuestAvailable(q);
        }
    }

    public static class RimverseTraders
    {
        private static readonly string[] Kinds = { "LightFreighter", "BulkFreighter", "Convoy", "Fuel", "Parts", "Shipyard", "Arms", "Data", "Exotic", "BlackMarket", "ContractBroker", "Salvage" };

        public static bool IsBlackMarket(TraderKindDef d) => d != null && d.defName == "Rimverse_Trader_BlackMarket";

        private static float Weight(Civilization c, string kind, bool station)
        {
            AiProfile prof = c?.government?.profile ?? AiProfile.Cautious;
            string ideo = c?.ideology?.defName ?? "";
            switch (kind)
            {
                case "LightFreighter": return 2f;
                case "BulkFreighter": return c != null && c.stage >= 9 ? 1.2f : 0.3f;
                case "Convoy": return prof == AiProfile.Mercantile ? 1.2f : 0.4f;
                case "Fuel": return 1f;
                case "Parts": return 1f;
                case "Shipyard": return station ? 2f : 0.6f;
                case "Arms": return prof == AiProfile.Warmonger || prof == AiProfile.Aggressive || ideo.Contains("Militarism") ? 1.4f : 0.5f;
                case "Data": return ideo.Contains("TechProgressivism") || ideo.Contains("Cosmism") ? 1.4f : 0.6f;
                case "Exotic": return c != null && c.lore == LoreKind.Glitterworld ? 1.4f : 0.6f;
                case "BlackMarket": return c != null && c.lore == LoreKind.Pirates ? 3f : ideo.Contains("Legalism") || ideo.Contains("Authoritarianism") ? 0f : 0.35f;
                case "ContractBroker": return 0.7f;
                case "Salvage": return c != null && c.lore == LoreKind.Pirates ? 1.5f : 0.5f;
            }
            return 0f;
        }

        public static TraderKindDef Pick(Civilization c, bool station = false)
        {
            if (Rand.Chance(0.3f)) return RimverseDefOf.Rimverse_CivSpaceTrader;
            string kind = Kinds.RandomElementByWeight(k => Weight(c, k, station));
            return DefDatabase<TraderKindDef>.GetNamedSilentFail("Rimverse_Trader_" + kind) ?? RimverseDefOf.Rimverse_CivSpaceTrader;
        }

        public static TraderKindDef ForFreighter(Fleet f)
        {
            string kind = f.power >= 6f ? "Convoy" : f.power >= 3f ? "BulkFreighter" : "LightFreighter";
            return DefDatabase<TraderKindDef>.GetNamedSilentFail("Rimverse_Trader_" + kind) ?? RimverseDefOf.Rimverse_CivSpaceTrader;
        }
    }

    public static class CivTrade
    {
        public static int ActiveFreighters(GameComponent_Civilizations gc, Civilization c) => gc.fleets.Count(f => f.civId == c.id && f.mission == FleetMission.Trade);

        public static void Launch(GameComponent_Civilizations gc, Civilization c, UniverseRand r, float years)
        {
            if (c.dead || c.capitalKey.NullOrEmpty() || c.stage < 8) return;
            AiProfile prof = c.government?.profile ?? AiProfile.Cautious;
            float chance = (c.goal == CivGoal.Trade ? 0.8f : 0.3f) * (prof == AiProfile.Mercantile ? 1.5f : 1f) * Mathf.Max(0.2f, years * 4f);
            if (!r.Chance(Mathf.Clamp01(chance))) return;
            if (ActiveFreighters(gc, c) >= 2 + c.planets.Count / 4) return;
            List<string> dests = new List<string>();
            foreach (CivPlanet p in c.planets) if (p.systemKey != c.capitalKey && !dests.Contains(p.systemKey)) dests.Add(p.systemKey);
            foreach (CivRelation rel in c.relations)
            {
                if (rel.state < DiploState.TradeAgreement) continue;
                Civilization o = gc.Civ(rel.other);
                if (o != null && !o.dead && !o.capitalKey.NullOrEmpty() && gc.DistanceLy(c, o) < 120.0) dests.Add(o.capitalKey);
            }
            if (dests.Count == 0) return;
            string dest = dests[r.RangeInclusive(0, dests.Count - 1)];
            float speed = Mathf.Max(0.5f, CivSimulation.FleetSpeedLyPerDay(c));
            double ly = GameComponent_Universe.Instance.DistanceLy(c.Capital, StarSystemId.FromKey(dest));
            if (double.IsInfinity(ly)) return;
            int now = Find.TickManager.TicksGame;
            float size = 1f + r.RangeInclusive(0, 2) + (prof == AiProfile.Mercantile ? 3f : 0f);
            gc.fleets.Add(new Fleet
            {
                id = gc.NextFleetId(),
                civId = c.id,
                mission = FleetMission.Trade,
                systemKey = c.capitalKey,
                destKey = dest,
                departTick = now,
                arriveTick = now + Mathf.Max(RimverseUnits.TicksPerHour * 6, RimverseUnits.TicksFromDays((float)(ly / speed))),
                power = size,
                cargo = size * 50f * (1f + c.stage * 0.1f),
                targetCiv = -2
            });
            c.AddStock(CivResource.Luxury, -Mathf.Min(c.Stock(CivResource.Luxury), size));
        }

        public static void Arrive(GameComponent_Civilizations gc, Civilization owner, Fleet f)
        {
            Civilization pirate = gc.Alive.Where(x => x.lore == LoreKind.Pirates && x.id != owner.id && gc.DistanceLy(x, StarSystemId.FromKey(f.systemKey)) < 40.0).FirstOrDefault();
            if (pirate != null && Rand.Chance(0.15f))
            {
                pirate.AddStock(CivResource.Luxury, f.cargo * 0.02f);
                if (owner.known) Messages.Message("Rimverse_MsgFreighterRaided".Translate(owner.ShownName, ContractUtility.SystemName(f.systemKey)), MessageTypeDefOf.NeutralEvent, false);
                f.power = 0f;
                return;
            }
            Civilization local = gc.OwnerOf(StarSystemId.FromKey(f.systemKey)) ?? owner;
            owner.AddStock(CivResource.Luxury, f.cargo * 0.03f);
            if (local != owner)
            {
                local.AddStock(CivResource.Luxury, f.cargo * 0.015f);
                CivRelation rel = owner.RelationWith(local.id, false);
                if (rel != null) rel.opinion = Mathf.Min(100f, rel.opinion + 1f);
            }
            GameComponent_RimverseBank bank = GameComponent_RimverseBank.Instance;
            if (bank != null)
            {
                bank.Shift(local, "Manufactured", -0.04f);
                bank.Shift(local, "ResourcesRaw", -0.03f);
                bank.Shift(local, "Foods", -0.03f);
            }
            Map map = FleetLogic.PlayerMapIn(f.systemKey);
            if (map != null && !owner.NoDiplomacy && owner.playerState != DiploState.War && !GameComponent_RimverseEvents.Embargoed(owner))
            {
                Faction fac = gc.EnsureFaction(owner);
                if (fac != null && !fac.HostileTo(Faction.OfPlayer) && map.passingShipManager.passingShips.Count < 5)
                {
                    TradeShip ts = new TradeShip(RimverseTraders.ForFreighter(f), fac) { name = owner.Name };
                    map.passingShipManager.AddShip(ts);
                    Messages.Message("Rimverse_MsgFreighterHere".Translate(owner.ShownName, ts.def.label), MessageTypeDefOf.PositiveEvent, false);
                }
            }
            f.cargo = 0f;
            FleetLogic.SendHome(gc, owner, f);
        }

        public static void Competition(GameComponent_Civilizations gc, Civilization c, UniverseRand r, float years)
        {
            if (c.dead || c.NoDiplomacy || c.goal != CivGoal.Trade || c.playerState >= DiploState.Alliance || GameComponent_RimverseEvents.Embargoed(c)) return;
            List<PlayerColony> cols = GameComponent_PlayerColonies.Instance?.colonies;
            if (cols == null) return;
            foreach (PlayerColony col in cols)
            {
                if (!(col.Has(ColonyIndustry.Mining) || col.Has(ColonyIndustry.TradePort))) continue;
                if (gc.DistanceLy(c, col.SystemId) > c.InfluenceRadiusLy + 10f) continue;
                if (!r.Chance(Mathf.Clamp01(0.08f * years * 4f))) continue;
                GameComponent_RimverseEvents ev = GameComponent_RimverseEvents.Instance;
                if (ev == null) return;
                ev.embargoUntil[c.id] = Find.TickManager.TicksGame + 20 * GenDate.TicksPerDay;
                gc.ChangePlayerOpinion(c, -8f);
                if (c.known) Find.LetterStack.ReceiveLetter("Rimverse_LetterTradeWarLabel".Translate(), "Rimverse_LetterTradeWar".Translate(c.ShownName, col.name), LetterDefOf.NegativeEvent);
                return;
            }
        }

        public static void Disrupt(GameComponent_Civilizations gc, string systemKey, float fraction)
        {
            foreach (Fleet f in gc.fleets.Where(x => x.mission == FleetMission.Trade && (x.systemKey == systemKey || x.destKey == systemKey)).ToList())
                if (Rand.Chance(fraction)) f.power = 0f;
        }
    }

    public static class PlanetMarket
    {
        public static float Factor(Civilization c, ThingDef d, Map map)
        {
            MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(map);
            if (c == null || d == null || loc == null || loc.systemKey.NullOrEmpty()) return 1f;
            string cat = GameComponent_RimverseBank.Category(d);
            int h = Gen.HashCombineInt(Gen.HashCombineInt(loc.systemKey.GetHashCode(), loc.bodyIndex), cat.GetHashCode());
            float local = ((h & 0xffff) / 65535f - 0.5f) * 0.3f;
            double ly = c.capitalKey.NullOrEmpty() ? 0.0 : GameComponent_Universe.Instance.DistanceLy(c.Capital, loc.SystemId);
            float far = double.IsInfinity(ly) ? 0.25f : Mathf.Min(0.25f, (float)ly * 0.01f);
            return 1f + local + far;
        }
    }

    [HarmonyPatch(typeof(Tradeable), nameof(Tradeable.GetPriceFor))]
    public static class Patch_PlanetMarketPrice
    {
        public static void Postfix(Tradeable __instance, TradeAction action, ref float __result)
        {
            ThingDef d = __instance.ThingDef;
            if (d == null || __instance.IsCurrency || Currencies.IsCurrency(d)) return;
            ITrader trader = TradeSession.trader;
            if (trader == null) return;
            if (RimverseTraders.IsBlackMarket(trader.TraderKind) && d.IsDrug && action == TradeAction.PlayerSells) __result *= 2.5f;
            Civilization c = Currencies.CivOf(trader.Faction);
            Map map = TradeSession.playerNegotiator?.MapHeld;
            if (c != null && map != null) __result *= PlanetMarket.Factor(c, d, map);
        }
    }
}
