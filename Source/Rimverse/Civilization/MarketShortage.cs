using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public static class MarketShortage
    {
        public const float ShortageSupply = -0.6f;

        public static bool Short(Civilization c, ThingDef d)
        {
            GameComponent_RimverseBank bank = GameComponent_RimverseBank.Instance;
            if (bank == null || c == null || d == null || Currencies.IsCurrency(d) || d == ThingDefOf.Silver) return false;
            return bank.Supply(c, GameComponent_RimverseBank.Category(d)) <= ShortageSupply;
        }

        public static List<string> ShortCategories(Civilization c)
        {
            List<string> list = new List<string>();
            GameComponent_RimverseBank bank = GameComponent_RimverseBank.Instance;
            if (bank == null || c == null) return list;
            foreach (ThingCategoryDef cat in ThingCategoryDefOf.Root.childCategories)
            {
                if (bank.Supply(c, cat.defName) <= ShortageSupply) list.Add(cat.LabelCap);
            }
            return list;
        }

        public static int Strip(ThingOwner owner, Faction f)
        {
            Civilization c = Currencies.CivOf(f);
            if (c == null || owner == null) return 0;
            int n = 0;
            foreach (Thing t in owner.ToList())
            {
                if (!Short(c, t.def)) continue;
                owner.Remove(t);
                t.Destroy();
                n++;
            }
            return n;
        }
    }

    [HarmonyPatch(typeof(TradeShip), nameof(TradeShip.GenerateThings))]
    public static class Patch_TradeShipShortage
    {
        public static void Postfix(TradeShip __instance, ThingOwner ___things)
        {
            MarketShortage.Strip(___things, __instance.Faction);
        }
    }

    [HarmonyPatch(typeof(Settlement_TraderTracker), "RegenerateStock")]
    public static class Patch_SettlementShortage
    {
        public static void Postfix(Settlement_TraderTracker __instance, ThingOwner<Thing> ___stock)
        {
            MarketShortage.Strip(___stock, __instance.settlement?.Faction);
        }
    }

    [HarmonyPatch(typeof(Dialog_Trade), nameof(Dialog_Trade.DoWindowContents))]
    public static class Patch_TradeShortageLabel
    {
        public static void Postfix(Rect inRect)
        {
            Civilization c = Currencies.CivOf(TradeSession.trader?.Faction);
            if (c == null) return;
            List<string> cats = MarketShortage.ShortCategories(c);
            if (cats.Count == 0) return;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperRight;
            GUI.color = new Color(1f, 0.7f, 0.4f);
            Widgets.Label(new Rect(inRect.xMax - 420f, inRect.y + 18f, 420f, 20f), "Rimverse_MarketShortage".Translate(string.Join(", ", cats)));
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }
    }
}
