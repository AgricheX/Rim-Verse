using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Rimverse
{
    [StaticConstructorOnStartup]
    public static class QolBigMaps
    {
        static QolBigMaps()
        {
            if (!QolFeatures.IsEnabled("Rimverse_Qol_BigMaps")) return;
            FieldInfo f = typeof(Dialog_AdvancedGameConfig).GetField("MapSizes", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (f == null || !(f.GetValue(null) is int[] cur)) return;
            int[] extra = { 350, 400, 450, 500 };
            int[] merged = cur.Concat(extra).Distinct().OrderBy(x => x).ToArray();
            try
            {
                f.SetValue(null, merged);
            }
            catch (System.Exception ex)
            {
                RimverseLog.Warning("Big map sizes: " + ex.Message);
            }
        }
    }

    [HarmonyPatch(typeof(GenRecipe), nameof(GenRecipe.MakeRecipeProducts))]
    public static class Patch_CremateStrip
    {
        public static void Prefix(RecipeDef recipeDef, Pawn worker, List<Thing> ingredients)
        {
            if (recipeDef == null || worker == null || ingredients == null || worker.Map == null) return;
            if (!recipeDef.defName.Contains("Cremate")) return;
            if (!QolFeatures.IsEnabled("Rimverse_Qol_CremateStrip")) return;
            foreach (Thing t in ingredients)
            {
                if (!(t is Corpse c) || c.InnerPawn?.apparel == null) continue;
                Pawn p = c.InnerPawn;
                foreach (Apparel a in p.apparel.WornApparel.ToList())
                {
                    if (a.WornByCorpse && a.HitPoints < a.MaxHitPoints * 0.2f) continue;
                    p.apparel.Remove(a);
                    GenPlace.TryPlaceThing(a, worker.Position, worker.Map, ThingPlaceMode.Near);
                }
                if (p.equipment != null)
                {
                    foreach (ThingWithComps eq in p.equipment.AllEquipmentListForReading.ToList())
                    {
                        p.equipment.Remove(eq);
                        GenPlace.TryPlaceThing(eq, worker.Position, worker.Map, ThingPlaceMode.Near);
                    }
                }
            }
        }
    }
}
