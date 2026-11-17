using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public static class Currencies
    {
        public static readonly string[] Order = { "Silver", "Rimverse_OrbitalCredit", "Rimverse_StellarMark", "Rimverse_SectorCrown", "Rimverse_GalacticStandard" };

        public static IEnumerable<ThingDef> All
        {
            get
            {
                foreach (string n in Order)
                {
                    ThingDef d = DefDatabase<ThingDef>.GetNamedSilentFail(n);
                    if (d != null) yield return d;
                }
            }
        }

        public static bool IsCurrency(ThingDef d) => d != null && Order.Contains(d.defName);

        public static ThingDef Preferred(Civilization c)
        {
            string n = c.stage >= 14 ? "Rimverse_GalacticStandard" : c.stage >= 12 ? "Rimverse_SectorCrown" : c.stage >= 10 ? "Rimverse_StellarMark" : c.stage >= 7 ? "Rimverse_OrbitalCredit" : "Silver";
            return DefDatabase<ThingDef>.GetNamedSilentFail(n) ?? ThingDefOf.Silver;
        }

        public static float Fee(Civilization c, ThingDef currency)
        {
            if (currency == Preferred(c)) return 0f;
            return c.playerState >= DiploState.TradeAgreement ? 0.01f : 0.04f;
        }

        public static Civilization CivOf(Faction f)
        {
            if (f == null) return null;
            return GameComponent_Civilizations.Instance?.civilizations.FirstOrDefault(c => c.faction == f && !c.dead);
        }
    }

    public class GameComponent_RimverseBank : GameComponent
    {
        public const float InterestPerQuadrum = 0.015f;
        public Dictionary<int, float> accounts = new Dictionary<int, float>();
        public Dictionary<string, float> supply = new Dictionary<string, float>();
        public int lastInterestTick;

        public static GameComponent_RimverseBank Instance => Current.Game?.GetComponent<GameComponent_RimverseBank>();

        public GameComponent_RimverseBank(Game game)
        {
        }

        public float Balance(Civilization c) => accounts.TryGetValue(c.id, out float v) ? v : 0f;

        public void Add(Civilization c, float value)
        {
            accounts[c.id] = Mathf.Max(0f, Balance(c) + value);
        }

        public static string Category(ThingDef d)
        {
            ThingCategoryDef cat = d.FirstThingCategory;
            while (cat?.parent != null && cat.parent != ThingCategoryDefOf.Root) cat = cat.parent;
            return cat?.defName ?? "Misc";
        }

        private static float Baseline(Civilization c, string cat)
        {
            int h = Gen.HashCombineInt(c.id * 7919, cat.GetHashCode());
            return ((h & 0xffff) / 65535f - 0.5f) * 0.6f;
        }

        public float Supply(Civilization c, string cat)
        {
            string key = c.id + ":" + cat;
            if (!supply.TryGetValue(key, out float v))
            {
                v = Baseline(c, cat);
                supply[key] = v;
            }
            return v;
        }

        public void Shift(Civilization c, string cat, float delta)
        {
            supply[c.id + ":" + cat] = Mathf.Clamp(Supply(c, cat) + delta, -1f, 1f);
        }

        public float PriceFactor(Civilization c, ThingDef d) => Mathf.Clamp(1f - 0.3f * Supply(c, Category(d)), 0.6f, 1.4f);

        public override void GameComponentTick()
        {
            int tick = Find.TickManager.TicksGame;
            if (tick % GenDate.TicksPerDay == 333 && supply.Count > 0)
            {
                GameComponent_Civilizations gc = GameComponent_Civilizations.Instance;
                foreach (string key in supply.Keys.ToList())
                {
                    int sep = key.IndexOf(':');
                    Civilization c = gc?.Civ(int.Parse(key.Substring(0, sep)));
                    if (c == null || c.dead) { supply.Remove(key); continue; }
                    float b = Baseline(c, key.Substring(sep + 1));
                    supply[key] = Mathf.Lerp(supply[key], b, 0.05f);
                }
            }
            if (tick - lastInterestTick >= GenDate.TicksPerQuadrum)
            {
                lastInterestTick = tick;
                GameComponent_Civilizations gc = GameComponent_Civilizations.Instance;
                foreach (int id in accounts.Keys.ToList())
                {
                    Civilization c = gc?.Civ(id);
                    if (c == null || c.dead || c.playerState == DiploState.War) continue;
                    accounts[id] *= 1f + InterestPerQuadrum;
                }
            }
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref accounts, "accounts", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref supply, "supply", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref lastInterestTick, "lastInterestTick");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (accounts == null) accounts = new Dictionary<int, float>();
                if (supply == null) supply = new Dictionary<string, float>();
            }
        }
    }

    public class Window_Bank : Window
    {
        private readonly Civilization civ;
        private readonly Map map;
        private readonly IntVec3 at;
        private Vector2 scroll;

        public Window_Bank(Civilization civ, Map map, IntVec3 at)
        {
            this.civ = civ;
            this.map = map;
            this.at = at;
            doCloseX = true;
            forcePause = true;
            absorbInputAroundWindow = true;
        }

        public override Vector2 InitialSize => new Vector2(620f, 560f);

        private List<Thing> Held(ThingDef d) => Patch_TradeWithoutBeacon.SellableNear(map, at).Where(t => t.def == d).ToList();

        public override void DoWindowContents(Rect inRect)
        {
            GameComponent_RimverseBank bank = GameComponent_RimverseBank.Instance;
            if (bank == null) return;
            Listing_Standard l = new Listing_Standard();
            Rect view = new Rect(0f, 0f, inRect.width - 16f, 900f);
            Widgets.BeginScrollView(inRect, ref scroll, view);
            l.Begin(view);
            Text.Font = GameFont.Medium;
            l.Label("Rimverse_BankTitle".Translate(civ.ShownName));
            Text.Font = GameFont.Small;
            float balance = bank.Balance(civ);
            l.Label("Rimverse_BankBalance".Translate(balance.ToString("N0")));
            l.Label("Rimverse_BankInterest".Translate((GameComponent_RimverseBank.InterestPerQuadrum).ToStringPercent()));
            if (civ.playerState == DiploState.War) l.Label("Rimverse_BankFrozen".Translate());
            ThingDef pref = Currencies.Preferred(civ);
            l.Label("Rimverse_BankPreferred".Translate(pref.LabelCap));
            l.GapLine();
            bool frozen = civ.playerState == DiploState.War;
            foreach (ThingDef cur in Currencies.All)
            {
                float unit = cur.BaseMarketValue;
                float fee = Currencies.Fee(civ, cur);
                List<Thing> held = Held(cur);
                int count = held.Sum(t => t.stackCount);
                Rect row = l.GetRect(30f);
                Widgets.ThingIcon(row.LeftPartPixels(28f), cur);
                Widgets.Label(new Rect(row.x + 34f, row.y + 4f, 230f, 24f), cur.LabelCap + " (" + unit.ToString("0") + ") ×" + count);
                Rect b1 = new Rect(row.x + 270f, row.y, 140f, 28f);
                Rect b2 = new Rect(row.x + 416f, row.y, 140f, 28f);
                if (!frozen && count > 0 && Widgets.ButtonText(b1, "Rimverse_BankDeposit".Translate()))
                {
                    float value = 0f;
                    foreach (Thing t in held)
                    {
                        value += t.stackCount * unit;
                        t.Destroy();
                    }
                    bank.Add(civ, value * (1f - fee));
                }
                int canTake = Mathf.FloorToInt(balance / (unit * (1f + fee)));
                if (!frozen && canTake > 0 && Widgets.ButtonText(b2, "Rimverse_BankWithdraw".Translate(Mathf.Min(canTake, cur.stackLimit * 4))))
                {
                    int n = Mathf.Min(canTake, cur.stackLimit * 4);
                    bank.Add(civ, -n * unit * (1f + fee));
                    int left = n;
                    while (left > 0)
                    {
                        Thing t = ThingMaker.MakeThing(cur);
                        t.stackCount = Mathf.Min(left, cur.stackLimit);
                        left -= t.stackCount;
                        GenPlace.TryPlaceThing(t, at, map, ThingPlaceMode.Near);
                    }
                }
                if (fee > 0f) TooltipHandler.TipRegion(row, "Rimverse_BankFee".Translate(fee.ToStringPercent()));
            }
            l.GapLine();
            if (!ResearchGate.Done("Rimverse_MarketAnalytics"))
            {
                l.Label(ResearchGate.Need("Rimverse_MarketAnalytics"));
                l.End();
                Widgets.EndScrollView();
                return;
            }
            l.Label("Rimverse_MarketTitle".Translate());
            List<KeyValuePair<string, float>> cats = new List<KeyValuePair<string, float>>();
            foreach (ThingCategoryDef cat in ThingCategoryDefOf.Root.childCategories) cats.Add(new KeyValuePair<string, float>(cat.defName, bank.Supply(civ, cat.defName)));
            foreach (KeyValuePair<string, float> kv in cats.OrderBy(k => k.Value))
            {
                ThingCategoryDef cd = DefDatabase<ThingCategoryDef>.GetNamedSilentFail(kv.Key);
                float f = Mathf.Clamp(1f - 0.3f * kv.Value, 0.6f, 1.4f);
                GUI.color = f > 1.05f ? new Color(0.6f, 1f, 0.6f) : f < 0.95f ? new Color(1f, 0.6f, 0.55f) : Color.white;
                l.Label("  " + (cd?.LabelCap.ToString() ?? kv.Key) + ": " + "Rimverse_MarketFactor".Translate(f.ToStringPercent()));
            }
            GUI.color = Color.white;
            l.End();
            Widgets.EndScrollView();
        }
    }

    [HarmonyPatch(typeof(Tradeable), nameof(Tradeable.GetPriceFor))]
    public static class Patch_MarketPrice
    {
        public static void Postfix(Tradeable __instance, ref float __result)
        {
            ThingDef d = __instance.ThingDef;
            if (d == null || Currencies.IsCurrency(d) || __instance.IsCurrency) return;
            Civilization c = Currencies.CivOf(TradeSession.trader?.Faction);
            GameComponent_RimverseBank bank = GameComponent_RimverseBank.Instance;
            if (c == null || bank == null) return;
            __result *= bank.PriceFactor(c, d);
        }
    }

    [HarmonyPatch(typeof(Tradeable), nameof(Tradeable.ResolveTrade))]
    public static class Patch_MarketShift
    {
        public static void Prefix(Tradeable __instance)
        {
            ThingDef d = __instance.ThingDef;
            if (d == null || __instance.IsCurrency || Currencies.IsCurrency(d)) return;
            Civilization c = Currencies.CivOf(TradeSession.trader?.Faction);
            GameComponent_RimverseBank bank = GameComponent_RimverseBank.Instance;
            if (c == null || bank == null) return;
            float value = d.BaseMarketValue;
            float scale = 4000f + c.Population * 0.5f;
            if (__instance.ActionToDo == TradeAction.PlayerSells) bank.Shift(c, GameComponent_RimverseBank.Category(d), __instance.CountToTransferToDestination * value / scale);
            else if (__instance.ActionToDo == TradeAction.PlayerBuys) bank.Shift(c, GameComponent_RimverseBank.Category(d), -__instance.CountToTransferToSource * value / scale);
        }
    }
}
