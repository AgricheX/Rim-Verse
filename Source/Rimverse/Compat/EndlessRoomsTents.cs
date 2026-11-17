using HarmonyLib;
using RimWorld;
using Verse;

namespace Rimverse
{
    [HarmonyPatch(typeof(Toils_LayDown), "ApplyBedThoughts")]
    public static class Patch_EndlessRoomsTentCabin
    {
        public static bool IsTent(ThingDef d)
        {
            if (d?.modContentPack == null || !d.IsBed) return false;
            if (!d.modContentPack.PackageId.ToLowerInvariant().Contains("endlessrooms")) return false;
            return d.defName.ToLowerInvariant().Contains("tent") || (d.label ?? "").ToLowerInvariant().Contains("tent");
        }

        public static void Postfix(Pawn actor, Building_Bed bed)
        {
            if (bed == null || actor?.needs?.mood == null || bed != actor.ownership?.OwnedBed || !IsTent(bed.def)) return;
            if (!bed.Spawned || !SpaceMapUtility.IsSpace(bed.Map) || actor.story?.traits == null || actor.story.traits.HasTrait(TraitDefOf.Ascetic)) return;
            MemoryThoughtHandler mem = actor.needs.mood.thoughts.memories;
            mem.RemoveMemoriesOfDef(ThoughtDefOf.SleptInBarracks);
            mem.RemoveMemoriesOfDef(ThoughtDefOf.SleptOutside);
            mem.RemoveMemoriesOfDef(ThoughtDefOf.SleptInBedroom);
            var stages = ThoughtDefOf.SleptInBedroom.stages;
            for (int i = 0; i < stages.Count; i++)
            {
                if (stages[i] == null || stages[i].baseMoodEffect < 0f) continue;
                mem.TryGainMemory(ThoughtMaker.MakeThought(ThoughtDefOf.SleptInBedroom, i));
                break;
            }
        }
    }
}
