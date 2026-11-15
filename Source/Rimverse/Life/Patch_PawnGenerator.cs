using HarmonyLib;
using Verse;

namespace Rimverse
{
    [HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), new[] { typeof(PawnGenerationRequest) })]
    public static class Patch_PawnGenerator_GeneratePawn
    {
        public static void Postfix(Pawn __result)
        {
            if (__result == null) return;
            try
            {
                LineageUtility.TryApplyFromFaction(__result);
            }
            catch (System.Exception ex)
            {
                RimverseLog.ErrorOnce("Lineage assignment failed: " + ex.Message, "RimverseLineageGen");
            }
        }
    }
}
