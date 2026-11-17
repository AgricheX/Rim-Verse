using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    [HarmonyPatch(typeof(Need_Mood), nameof(Need_Mood.MoodString), MethodType.Getter)]
    public static class Patch_QolMoodColor
    {
        public static void Postfix(Need_Mood __instance, Pawn ___pawn, ref string __result)
        {
            if (__result.NullOrEmpty() || ___pawn?.mindState?.mentalBreaker == null || !QolFeatures.IsEnabled("Rimverse_Qol_MoodColors")) return;
            float m = __instance.CurLevel;
            Color c;
            if (m < ___pawn.mindState.mentalBreaker.BreakThresholdMinor) c = ColorLibrary.RedReadable;
            else if (m < 0.5f) c = new Color(1f, 0.85f, 0.35f);
            else if (m >= 0.75f) c = new Color(0.45f, 0.9f, 0.45f);
            else return;
            __result = __result.Colorize(c);
        }
    }

    public class TransferableComparer_ValuePerMass : TransferableComparer
    {
        private static float Ratio(Transferable t)
        {
            Thing a = t.AnyThing;
            if (a == null) return 0f;
            float mass = a.GetStatValue(StatDefOf.Mass);
            return mass <= 0.001f ? a.MarketValue * 1000f : a.MarketValue / mass;
        }

        public override int Compare(Transferable lhs, Transferable rhs)
        {
            return Ratio(rhs).CompareTo(Ratio(lhs));
        }
    }

    public enum KeyChordModifier
    {
        None,
        Control,
        Shift,
        Alt
    }

    public class KeyChordExtension : DefModExtension
    {
        public KeyChordModifier modifier = KeyChordModifier.None;
    }

    public static class QolKeyChords
    {
        public static bool Pressed(KeyBindingDef def)
        {
            if (def == null || !def.KeyDownEvent) return false;
            KeyChordExtension ext = def.GetModExtension<KeyChordExtension>();
            if (ext == null || ext.modifier == KeyChordModifier.None) return true;
            Event e = Event.current;
            switch (ext.modifier)
            {
                case KeyChordModifier.Control: return e.control || e.command;
                case KeyChordModifier.Shift: return e.shift;
                case KeyChordModifier.Alt: return e.alt;
            }
            return true;
        }
    }

    public class FloatMenuOptionProvider_QolBuildFromInventory : FloatMenuOptionProvider
    {
        protected override bool Drafted => true;

        protected override bool Undrafted => true;

        protected override bool Multiselect => false;

        protected override bool RequiresManipulation => true;

        protected override FloatMenuOption GetSingleOptionFor(Thing clickedThing, FloatMenuContext context)
        {
            if (!(clickedThing is IConstructible c) || clickedThing.Faction != Faction.OfPlayer || !QolFeatures.IsEnabled("Rimverse_Qol_BuildFromInventory")) return null;
            Pawn pawn = context.FirstSelectedPawn;
            if (pawn?.inventory == null || pawn.inventory.innerContainer.Count == 0) return null;
            int total = 0;
            foreach (ThingDefCountClass need in c.TotalMaterialCost())
            {
                if (c.ThingCountNeeded(need.thingDef) <= 0) continue;
                total += pawn.inventory.innerContainer.TotalStackCountOfDef(need.thingDef);
            }
            if (total == 0 || !pawn.CanReach(clickedThing, Verse.AI.PathEndMode.Touch, Danger.Deadly)) return null;
            Thing target = clickedThing;
            return new FloatMenuOption("Rimverse_QolBuildFromInventory".Translate(), delegate
            {
                Verse.AI.Job job = JobMaker.MakeJob(QolJobDefOf9.Rimverse_DropForBuild, target);
                pawn.jobs.TryTakeOrderedJob(job, Verse.AI.JobTag.Misc);
            });
        }
    }

    [DefOf]
    public static class QolJobDefOf9
    {
        public static JobDef Rimverse_DropForBuild;

        static QolJobDefOf9()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(QolJobDefOf9));
        }
    }

    public class JobDriver_RimverseDropForBuild : Verse.AI.JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override System.Collections.Generic.IEnumerable<Verse.AI.Toil> MakeNewToils()
        {
            yield return Verse.AI.Toils_Goto.GotoThing(Verse.AI.TargetIndex.A, Verse.AI.PathEndMode.Touch);
            Verse.AI.Toil drop = ToilMaker.MakeToil("RimverseDropForBuild");
            drop.initAction = delegate
            {
                Thing t = job.GetTarget(Verse.AI.TargetIndex.A).Thing;
                if (!(t is IConstructible c) || !t.Spawned) return;
                foreach (ThingDefCountClass need in c.TotalMaterialCost())
                {
                    int left = c.ThingCountNeeded(need.thingDef);
                    for (int i = pawn.inventory.innerContainer.Count - 1; i >= 0 && left > 0; i--)
                    {
                        Thing it = pawn.inventory.innerContainer[i];
                        if (it.def != need.thingDef) continue;
                        int n = Mathf.Min(left, it.stackCount);
                        if (pawn.inventory.innerContainer.TryDrop(it, pawn.Position, pawn.Map, ThingPlaceMode.Near, n, out Thing dropped) && dropped != null)
                        {
                            left -= dropped.stackCount;
                            dropped.SetForbidden(false, false);
                        }
                    }
                }
            };
            drop.defaultCompleteMode = Verse.AI.ToilCompleteMode.Instant;
            yield return drop;
        }
    }
}
