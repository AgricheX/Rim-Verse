using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Rimverse
{
    public static class CompatGroupTweaks
    {
        public static float CrowdedCellsPerCrew = 16f;

        private static bool From(Def d, CompatGroup g) => d?.modContentPack != null && CompatRegistry.Known.TryGetValue(d.modContentPack.PackageId.ToLowerInvariant(), out CompatGroup k) && k == g;

        public static void Apply()
        {
            if (CompatRegistry.AnyActive(CompatGroup.ShipVersions)) ShipVersions();
            if (CompatRegistry.AnyActive(CompatGroup.Weapons)) Weapons();
            if (CompatRegistry.AnyActive(CompatGroup.AnomalyExtras)) AnomalyExtras();
            if (CompatRegistry.AnyActive(CompatGroup.Psycasts)) Psycasts();
            if (ModCompat.IsActive("lucifer.realisticrooms"))
            {
                CrowdedCellsPerCrew = 10f;
                RimverseLog.Message("Realistic Rooms: crowded ship threshold lowered to 10 cells per crew member.");
            }
        }

        private static void ShipVersions()
        {
            int n = 0;
            foreach (ThingDef d in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (d.category != ThingCategory.Building || !From(d, CompatGroup.ShipVersions)) continue;
                if (d.comps != null && d.comps.Any(c => c is CompProperties_ShipPart)) continue;
                ShipPartRole role;
                if (d.thingClass != null && typeof(Building_PlantGrower).IsAssignableFrom(d.thingClass)) role = ShipPartRole.Hydroponics;
                else if (d.building?.fixedStorageSettings != null || (d.thingClass != null && typeof(Building_Storage).IsAssignableFrom(d.thingClass))) role = ShipPartRole.Cargo;
                else continue;
                if (d.comps == null) d.comps = new List<CompProperties>();
                int cells = d.size.x * d.size.z;
                d.comps.Add(new CompProperties_ShipPart { role = role, mass = cells * (role == ShipPartRole.Cargo ? 0.5f : 1f), cargoTons = role == ShipPartRole.Cargo ? cells * 2f : 0f });
                n++;
            }
            if (n > 0) RimverseLog.Message("Storage and hydroponics from ship mods count as ship parts: " + n);
        }

        private static void Weapons()
        {
            int n = 0;
            foreach (ThingDef w in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (!w.IsRangedWeapon || !From(w, CompatGroup.Weapons) || w.Verbs == null) continue;
                foreach (VerbProperties v in w.Verbs)
                {
                    ThingDef proj = v.defaultProjectile;
                    if (proj?.projectile == null || proj.HasModExtension<HullRiskExtension>()) continue;
                    float chance = proj.projectile.explosionRadius > 0f || (proj.projectile.damageDef?.isExplosive ?? false) ? 0.25f : proj.projectile.GetDamageAmount(w, null) >= 20 ? 0.05f : 0f;
                    if (chance <= 0f) continue;
                    if (proj.modExtensions == null) proj.modExtensions = new List<DefModExtension>();
                    proj.modExtensions.Add(new HullRiskExtension { breachChance = chance });
                    n++;
                }
            }
            if (n > 0) RimverseLog.Message("Heavy and explosive rounds from weapon mods can breach the hull: " + n);
        }

        private static void AnomalyExtras()
        {
            StatDef seal = RimverseDefOf.Rimverse_VacuumSeal;
            if (seal == null) return;
            int n = 0;
            foreach (ThingDef d in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (d.race == null || !d.race.IsAnomalyEntity || !From(d, CompatGroup.AnomalyExtras)) continue;
                if (d.statBases == null) d.statBases = new List<StatModifier>();
                if (d.statBases.Any(s => s.stat == seal)) continue;
                d.statBases.Add(new StatModifier { stat = seal, value = 1f });
                n++;
            }
            if (n > 0) RimverseLog.Message("Entities from Anomaly mods survive vacuum like the base entities: " + n);
        }

        private static void Psycasts()
        {
            HediffDef flow = FlowUtility.Sensitive;
            StatDef gain = DefDatabase<StatDef>.GetNamedSilentFail("MeditationFocusGain");
            if (flow?.stages == null || gain == null) return;
            foreach (HediffStage st in flow.stages)
            {
                if (st.statOffsets == null) st.statOffsets = new List<StatModifier>();
                if (st.statOffsets.Any(s => s.stat == gain)) continue;
                st.statOffsets.Add(new StatModifier { stat = gain, value = 0.1f });
            }
            RimverseLog.Message("Psycast mods: Flow-sensitive pawns regain psyfocus faster.");
        }
    }
}
