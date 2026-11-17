using RimWorld;
using Verse;

namespace Rimverse
{
    public class CompProperties_BreedingStall : CompProperties
    {
        public float radius = 3f;
        public float maxBodySize = 0.6f;
        public float bonus = 0.3f;

        public CompProperties_BreedingStall()
        {
            compClass = typeof(CompBreedingStall);
        }
    }

    public class CompBreedingStall : ThingComp
    {
        private static readonly HarmonyLib.AccessTools.FieldRef<CompEggLayer, float> eggProgress = HarmonyLib.AccessTools.FieldRefAccess<CompEggLayer, float>("eggProgress");
        private static readonly HarmonyLib.AccessTools.FieldRef<CompHasGatherableBodyResource, float> fullness = HarmonyLib.AccessTools.FieldRefAccess<CompHasGatherableBodyResource, float>("fullness");

        public CompProperties_BreedingStall Props => (CompProperties_BreedingStall)props;

        public override void CompTickRare()
        {
            if (!parent.Spawned) return;
            Room room = parent.GetRoom();
            if (room == null) return;
            foreach (Thing t in GenRadial.RadialDistinctThingsAround(parent.Position, parent.Map, Props.radius, true))
            {
                if (!(t is Pawn p) || !p.RaceProps.Animal || p.Faction != Faction.OfPlayer || p.BodySize > Props.maxBodySize || p.GetRoom() != room) continue;
                CompEggLayer egg = p.TryGetComp<CompEggLayer>();
                if (egg != null && egg.Props.eggLayIntervalDays > 0f && (!egg.Props.eggLayFemaleOnly || p.gender == Gender.Female) && p.ageTracker.CurLifeStage.milkable)
                {
                    float cap = egg.FullyFertilized ? 1f : egg.Props.eggProgressUnfertilizedMax;
                    if (eggProgress(egg) < cap) eggProgress(egg) = UnityEngine.Mathf.Min(cap, eggProgress(egg) + Props.bonus * 250f / (egg.Props.eggLayIntervalDays * 60000f));
                }
                CompMilkable milk = p.TryGetComp<CompMilkable>();
                if (milk != null && milk.Props.milkIntervalDays > 0) Grow(milk, milk.Props.milkIntervalDays);
                CompShearable wool = p.TryGetComp<CompShearable>();
                if (wool != null && wool.Props.shearIntervalDays > 0) Grow(wool, wool.Props.shearIntervalDays);
            }
        }

        private void Grow(CompHasGatherableBodyResource c, float days)
        {
            if (!c.ActiveAndFull) fullness(c) = UnityEngine.Mathf.Min(1f, fullness(c) + Props.bonus * 250f / (days * 60000f));
        }
    }
}
