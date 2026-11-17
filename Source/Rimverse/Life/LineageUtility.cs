using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public static class LineageUtility
    {
        public static Hediff LineageHediff(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return null;
            foreach (Hediff h in pawn.health.hediffSet.hediffs)
            {
                if (h.def.HasModExtension<LineageExtension>()) return h;
            }
            return null;
        }

        public static LineageExtension ExtensionOf(Pawn pawn) => LineageHediff(pawn)?.def.GetModExtension<LineageExtension>();

        public static string ForHome(BodyData body, LoreKind lore, float roll)
        {
            if (lore == LoreKind.Mechanoids) return null;
            if (lore == LoreKind.Pirates && roll < 0.5f) return "Rimverse_Lineage_VoidBorn";
            if (body == null || body.homeWorld) return null;
            if (body.gravity >= 1.3f) return "Rimverse_Lineage_Gravitated";
            if (body.gravity <= 0.6f) return "Rimverse_Lineage_Slender";
            if (body.type == BodyType.Ocean) return "Rimverse_Lineage_Abyssal";
            if (body.temperatureC <= -15f) return "Rimverse_Lineage_Boreal";
            if (body.temperatureC >= 40f) return "Rimverse_Lineage_Solar";
            if (body.atmosphere == AtmosphereType.Toxic || body.atmosphere == AtmosphereType.Thin) return roll < 0.6f ? "Rimverse_Lineage_Rad" : null;
            if (roll < 0.15f) return "Rimverse_Lineage_Horned";
            return null;
        }

        public static void Apply(Pawn pawn, HediffDef def)
        {
            if (pawn == null || def == null || !pawn.RaceProps.Humanlike) return;
            if (LineageHediff(pawn) != null) return;
            pawn.health.AddHediff(def);
            ApplyLooks(pawn, def);
        }

        public static void ApplyLooks(Pawn pawn, HediffDef def)
        {
            LineageExtension ext = def?.GetModExtension<LineageExtension>();
            if (ext == null || pawn.story == null) return;
            if (ext.skinColor.HasValue)
            {
                Color c = ext.skinColor.Value;
                float j = ext.skinColorJitter;
                pawn.story.skinColorOverride = new Color(Mathf.Clamp01(c.r + Rand.Range(-j, j)), Mathf.Clamp01(c.g + Rand.Range(-j, j)), Mathf.Clamp01(c.b + Rand.Range(-j, j)));
            }
            if (pawn.DevelopmentalStage.Adult())
            {
                BodyTypeDef bt = pawn.gender == Gender.Female ? ext.bodyTypeFemale ?? ext.bodyTypeMale : ext.bodyTypeMale ?? ext.bodyTypeFemale;
                if (bt != null) pawn.story.bodyType = bt;
            }
            pawn.Drawer?.renderer?.SetAllGraphicsDirty();
        }

        public static void TryApplyFromFaction(Pawn pawn)
        {
            if (!RimverseMod.Settings.lineagesEnabled || pawn?.Faction == null || !pawn.RaceProps.Humanlike) return;
            if (CompatActions.HarRace(pawn)) return;
            GameComponent_Civilizations gc = GameComponent_Civilizations.Instance;
            if (gc?.civilizations == null) return;
            Civilization civ = gc.civilizations.FirstOrDefault(c => c.faction == pawn.Faction);
            if (civ == null) return;
            if (civ.lineage.NullOrEmpty())
            {
                CivXenotypes.TryApply(pawn, civ);
                return;
            }
            if (!Rand.Chance(0.7f)) return;
            HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(civ.lineage);
            if (BiotechCompat.TryXenotype(pawn, civ.lineage))
            {
                ApplyLooks(pawn, def);
            }
            else
            {
                Apply(pawn, def);
            }
            WearGarb(pawn, def);
        }

        public static void WearGarb(Pawn pawn, HediffDef def)
        {
            ThingDef garb = def?.GetModExtension<LineageExtension>()?.apparel;
            if (garb == null || pawn.apparel == null || !pawn.DevelopmentalStage.Adult() || !Rand.Chance(0.5f)) return;
            if (!ApparelUtility.HasPartsToWear(pawn, garb)) return;
            ThingDef stuff = GenStuff.RandomStuffFor(garb);
            Apparel a = (Apparel)ThingMaker.MakeThing(garb, stuff);
            PawnGenerator.PostProcessGeneratedGear(a, pawn);
            pawn.apparel.Wear(a, false);
        }
    }
}
