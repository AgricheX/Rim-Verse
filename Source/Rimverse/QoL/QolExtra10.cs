using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public class GameComponent_QolCallTrader : GameComponent
    {
        public const int CooldownTicks = 10 * GenDate.TicksPerDay;
        public const int Cost = 300;
        public int lastCall = -9999999;

        public GameComponent_QolCallTrader(Game game)
        {
        }

        public static GameComponent_QolCallTrader Instance => Current.Game?.GetComponent<GameComponent_QolCallTrader>();

        public int TicksLeft => Mathf.Max(0, lastCall + CooldownTicks - Find.TickManager.TicksGame);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lastCall, "lastCall", -9999999);
        }
    }

    [HarmonyPatch(typeof(Building_CommsConsole), nameof(Building_CommsConsole.GetGizmos))]
    public static class Patch_QolCallTrader
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> values, Building_CommsConsole __instance)
        {
            foreach (Gizmo g in values) yield return g;
            if (__instance.Faction != Faction.OfPlayer || !QolFeatures.IsEnabled("Rimverse_Qol_CallTrader")) yield break;
            GameComponent_QolCallTrader c = GameComponent_QolCallTrader.Instance;
            if (c == null) yield break;
            Map map = __instance.Map;
            Command_Action cmd = new Command_Action
            {
                defaultLabel = "Rimverse_QolCallTrader".Translate(),
                defaultDesc = "Rimverse_QolCallTraderDesc".Translate(GameComponent_QolCallTrader.Cost, 10),
                icon = ThingDefOf.Silver.uiIcon,
                action = delegate
                {
                    if (!ColonyUtility.Consume(map, d => d == ThingDefOf.Silver, GameComponent_QolCallTrader.Cost))
                    {
                        Messages.Message("Rimverse_QolCallTraderNoSilver".Translate(GameComponent_QolCallTrader.Cost), MessageTypeDefOf.RejectInput, false);
                        return;
                    }
                    IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.Misc, map);
                    parms.forced = true;
                    if (IncidentDefOf.OrbitalTraderArrival.Worker.TryExecute(parms)) c.lastCall = Find.TickManager.TicksGame;
                }
            };
            if (!__instance.CanUseCommsNow) cmd.Disable("Rimverse_QolCallTraderNoPower".Translate());
            else if (c.TicksLeft > 0) cmd.Disable("Rimverse_QolCallTraderWait".Translate(c.TicksLeft.ToStringTicksToPeriod()));
            else if (OrbitalSiege.Besieged(map)) cmd.Disable("Rimverse_QolCallTraderSiege".Translate());
            yield return cmd;
        }
    }

    [HarmonyPatch(typeof(Dialog_Trade), nameof(Dialog_Trade.DoWindowContents))]
    public static class Patch_QolGoodwillPreview
    {
        public static void Postfix(Rect inRect)
        {
            if (!QolFeatures.IsEnabled("Rimverse_Qol_GoodwillPreview")) return;
            Faction f = TradeSession.trader?.Faction;
            if (f == null || f.IsPlayer || f.def.permanentEnemy || TradeSession.deal == null) return;
            int now = f.GoodwillWith(Faction.OfPlayer);
            string text;
            if (TradeSession.giftMode)
            {
                int after = Mathf.Clamp(now + FactionGiftUtility.GetGoodwillChange(TradeSession.deal.AllTradeables, f), -100, 100);
                text = "Rimverse_QolGoodwillGift".Translate(now, after, Kind(after));
            }
            else text = "Rimverse_QolGoodwillNow".Translate(now, Kind(now));
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperRight;
            Widgets.Label(new Rect(inRect.xMax - 320f, inRect.y, 320f, 20f), text);
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }

        private static string Kind(int goodwill) => goodwill >= 75 ? "Ally".Translate().ToString() : goodwill <= -75 ? "Hostile".Translate().ToString() : "Neutral".Translate().ToString();
    }
}
