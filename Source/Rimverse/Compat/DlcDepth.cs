using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Rimverse
{
    public static class ShipAIUtility
    {
        public static bool AICoreOn(Map map)
        {
            if (map == null) return false;
            ThingDef core = DefDatabase<ThingDef>.GetNamedSilentFail("Rimverse_AICore");
            return core != null && map.listerBuildings.ColonistsHaveBuilding(core);
        }
    }

    public class ThoughtWorker_Precept_ShipAIPresent : ThoughtWorker_Precept
    {
        protected override ThoughtState ShouldHaveThought(Pawn p) => p.Spawned && ShipAIUtility.AICoreOn(p.Map);
    }

    public class ThoughtWorker_Precept_ShipAIAbsentInSpace : ThoughtWorker_Precept
    {
        protected override ThoughtState ShouldHaveThought(Pawn p) => SpaceLifeUtility.InSpace(p) && !ShipAIUtility.AICoreOn(p.Map);
    }

    [HarmonyPatch(typeof(TradeDeal), nameof(TradeDeal.TryExecute))]
    public static class Patch_DataTradeIdeo
    {
        public static void Prefix(TradeDeal __instance, out int __state)
        {
            __state = 0;
            if (!ModCompat.Ideology) return;
            foreach (Tradeable t in __instance.AllTradeables)
            {
                if (t.ThingDef != null && t.ThingDef.defName == "Rimverse_StellarData" && t.ActionToDo == TradeAction.PlayerSells) __state += t.CountToTransferToDestination;
            }
        }

        public static void Postfix(bool __result, int __state)
        {
            if (!__result || __state <= 0) return;
            Map map = TradeSession.playerNegotiator?.MapHeld;
            IdeoEvents.Give(map, "Rimverse_DataTrade_Forbidden", "Rimverse_DataTrade_Betrayal");
            IdeoEvents.Give(map, "Rimverse_DataTrade_Exalted", "Rimverse_DataTrade_Shared");
        }
    }

    public static class CivXenotypes
    {
        public static XenotypeDef For(Civilization c)
        {
            if (!ModCompat.Biotech || c == null || c.NoDiplomacy) return null;
            XenotypeDef byLineage = BiotechCompat.XenotypeFor(c.lineage);
            if (byLineage != null) return byLineage;
            string ideo = c.ideology?.defName ?? "";
            string gov = c.government?.defName ?? "";
            string name = null;
            int h = System.Math.Abs(c.id * 7919 + 13) % 100;
            if (c.stage <= 2) name = "Neanderthal";
            else if (ideo.Contains("Militarism") || gov.Contains("Junta")) name = h < 60 ? "Hussar" : null;
            else if (ideo.Contains("TechProgressivism") || ideo.Contains("Transhumanism")) name = h < 55 ? "Genie" : null;
            else if (ideo.Contains("Environmentalism")) name = h < 50 ? "Yttakin" : (h < 70 ? "Dirtmole" : null);
            else if (c.lore == LoreKind.Pirates) name = h < 50 ? "Waster" : (h < 65 ? "Impid" : null);
            else if (ideo.Contains("Liberalism") || ideo.Contains("Hedonism")) name = h < 40 ? "Highmate" : null;
            else if (h < 15) name = "Pigskin";
            else if (h < 25) name = "Impid";
            return name != null ? DefDatabase<XenotypeDef>.GetNamedSilentFail(name) : null;
        }

        public static bool TryApply(Pawn pawn, Civilization c)
        {
            XenotypeDef x = For(c);
            if (x == null || pawn?.genes == null) return false;
            if (pawn.genes.Xenotype != null && pawn.genes.Xenotype != XenotypeDefOf.Baseliner) return false;
            if (!Rand.Chance(0.6f)) return false;
            pawn.genes.SetXenotype(x);
            return true;
        }
    }

    public class CompUseEffect_GravshipRetrofit : CompUseEffect
    {
        public override AcceptanceReport CanBeUsedBy(Pawn p)
        {
            if (!ModCompat.Odyssey) return "Rimverse_RetrofitNoOdyssey".Translate();
            if (p.Map == null || SubstructureCells(p.Map).Count == 0) return "Rimverse_RetrofitNoGravship".Translate();
            return base.CanBeUsedBy(p);
        }

        private static List<IntVec3> SubstructureCells(Map map)
        {
            List<IntVec3> list = new List<IntVec3>();
            foreach (IntVec3 c in map.AllCells)
            {
                TerrainDef f = map.terrainGrid.FoundationAt(c);
                if (f != null && f.defName.Contains("Substructure")) list.Add(c);
            }
            return list;
        }

        public override void DoEffect(Pawn usedBy)
        {
            base.DoEffect(usedBy);
            Map map = usedBy.Map;
            TerrainDef hull = DefDatabase<TerrainDef>.GetNamedSilentFail("Rimverse_HullFloor");
            if (map == null || hull == null) return;
            List<IntVec3> cells = SubstructureCells(map);
            foreach (IntVec3 c in cells)
            {
                TerrainDef top = map.terrainGrid.TopTerrainAt(c);
                map.terrainGrid.SetFoundation(c, hull);
                if (top != null && top != hull && !top.defName.Contains("Substructure")) map.terrainGrid.SetTerrain(c, top);
            }
            ThingDef bridge = DefDatabase<ThingDef>.GetNamedSilentFail("Rimverse_BridgeSmall");
            if (bridge != null && ColonyUtility.PlayerShipOn(map) == null)
            {
                Thing b = ThingMaker.MakeThing(bridge, GenStuff.DefaultStuffFor(bridge));
                b.SetFactionDirect(Faction.OfPlayer);
                GenPlace.TryPlaceThing(b.MakeMinified(), usedBy.Position, map, ThingPlaceMode.Near);
            }
            map.GetComponent<MapComponent_RimverseShips>()?.Dirty();
            Find.LetterStack.ReceiveLetter("Rimverse_RetrofitDoneLabel".Translate(), "Rimverse_RetrofitDoneText".Translate(cells.Count), LetterDefOf.PositiveEvent, new TargetInfo(usedBy.Position, map));
        }
    }

    public class ThoughtWorker_NobleShipDemands : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (!ModCompat.Royalty || p.royalty == null || !SpaceLifeUtility.InSpace(p)) return ThoughtState.Inactive;
            RoyalTitle t = p.royalty.MostSeniorTitle;
            if (t == null || t.def.seniority < 200) return ThoughtState.Inactive;
            if (ColonyUtility.PlayerShipOn(p.Map) == null) return ThoughtState.Inactive;
            int score = 0;
            foreach (string n in new[] { "Rimverse_CommandThrone", "Rimverse_NobleBunk", "Rimverse_StarWindow" })
            {
                ThingDef d = DefDatabase<ThingDef>.GetNamedSilentFail(n);
                if (d != null && p.Map.listerBuildings.ColonistsHaveBuilding(d)) score++;
            }
            int need = t.def.seniority >= 500 ? 3 : 2;
            if (score >= need) return ThoughtState.ActiveAtStage(1);
            return ThoughtState.ActiveAtStage(0);
        }
    }
}

namespace Rimverse
{
    [HarmonyPatch]
    public static class Patch_ProjectileCE_Gravity
    {
        private static List<System.Reflection.MethodBase> Targets()
        {
            List<System.Reflection.MethodBase> list = new List<System.Reflection.MethodBase>();
            System.Type t = CombatExtendedCompat.ProjectileCEType;
            if (t == null) return list;
            foreach (string n in new[] { "GravityFactor", "Gravity" })
            {
                System.Reflection.PropertyInfo p = AccessTools.Property(t, n);
                System.Reflection.MethodInfo g = p?.GetGetMethod(true);
                if (g != null && g.ReturnType == typeof(float) && !g.IsAbstract) list.Add(g);
            }
            return list;
        }

        public static bool Prepare() => CombatExtendedCompat.Active && Targets().Count > 0;

        public static IEnumerable<System.Reflection.MethodBase> TargetMethods() => Targets();

        public static void Postfix(Thing __instance, ref float __result)
        {
            if (__instance?.Map == null) return;
            float g = GravityUtility.GravityAt(__instance.Map, __instance.Position);
            if (g == 1f) return;
            __result *= UnityEngine.Mathf.Clamp(g, 0.05f, 3f);
        }
    }
}
