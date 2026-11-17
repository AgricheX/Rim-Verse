using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public class CompProperties_BladeCrystal : CompProperties
    {
        public CompProperties_BladeCrystal()
        {
            compClass = typeof(CompBladeCrystal);
        }
    }

    public class CompBladeCrystal : ThingComp
    {
        public static readonly Color[] Colors =
        {
            new Color(0.35f, 0.6f, 1f),
            new Color(0.35f, 1f, 0.45f),
            new Color(0.7f, 0.4f, 1f),
            new Color(1f, 0.75f, 0.3f),
            new Color(0.95f, 0.95f, 1f),
            new Color(1f, 0.25f, 0.2f)
        };

        public int crystal = -1;

        public override void PostPostMake()
        {
            base.PostPostMake();
            if (crystal < 0) crystal = Rand.RangeInclusive(0, Colors.Length - 2);
            Apply();
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            Apply();
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref crystal, "rimverseCrystal", -1);
        }

        public override void Notify_Equipped(Pawn pawn)
        {
            base.Notify_Equipped(pawn);
            if (FlowUtility.Enabled && FlowUtility.Side(pawn) == FlowSide.Rupture && crystal != Colors.Length - 1)
            {
                crystal = Colors.Length - 1;
                Apply();
            }
        }

        private void Apply()
        {
            if (crystal < 0 || crystal >= Colors.Length) return;
            CompColorable c = parent.TryGetComp<CompColorable>();
            if (c != null && (!c.Active || c.Color != Colors[crystal])) c.SetColor(Colors[crystal]);
        }

        public override string TransformLabel(string label)
        {
            if (crystal < 0) return label;
            return label + " (" + ("Rimverse_BladeCrystal" + crystal).Translate() + ")";
        }
    }
}
