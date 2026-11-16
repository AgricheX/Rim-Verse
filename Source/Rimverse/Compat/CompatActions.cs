using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Vehicles;
using Verse;

namespace Rimverse
{
    public static class CompatActions
    {
        private static bool From(Def d, CompatGroup g) => d?.modContentPack != null && CompatRegistry.Known.TryGetValue(d.modContentPack.PackageId.ToLowerInvariant(), out CompatGroup k) && k == g;

        public static void ForeignVehicles()
        {
            int fighters = 0;
            foreach (VehicleDef v in DefDatabase<VehicleDef>.AllDefsListForReading)
            {
                if (!From(v, CompatGroup.Vehicles) || v.type != VehicleType.Air) continue;
                if (v.comps == null) v.comps = new List<CompProperties>();
                CompProperties_SpaceCraft sc = v.comps.OfType<CompProperties_SpaceCraft>().FirstOrDefault();
                if (sc == null)
                {
                    sc = new CompProperties_SpaceCraft { role = CraftRole.Shuttle, fuelPerTrip = 12f };
                    v.comps.Add(sc);
                }
                if (sc.role == CraftRole.Shuttle && v.comps.Any(c => c is CompProperties_VehicleTurrets))
                {
                    sc.role = CraftRole.Fighter;
                    sc.interceptChance = 0.05f;
                    fighters++;
                }
            }
            if (fighters > 0) RimverseLog.Message("Armed aircraft from vehicle mods can fly as fighters: " + fighters);
        }

        public static void MarkForeignFactions()
        {
            int n = DefDatabase<FactionDef>.AllDefsListForReading.Count(f => From(f, CompatGroup.Factions) && !f.hidden && !f.isPlayer);
            if (n > 0) RimverseLog.Message("Factions from other mods can stand in for star civilizations: " + n);
        }

        public static bool TryLinkForeignFaction(Civilization c)
        {
            if (c == null || c.lore == LoreKind.Mechanoids || !CompatRegistry.AnyActive(CompatGroup.Factions)) return false;
            GameComponent_Civilizations gc = GameComponent_Civilizations.Instance;
            HashSet<Faction> used = new HashSet<Faction>(gc?.civilizations?.Where(o => o.faction != null).Select(o => o.faction) ?? Enumerable.Empty<Faction>());
            Faction f = Find.FactionManager.AllFactionsListForReading.FirstOrDefault(x => !x.IsPlayer && !x.Hidden && !x.defeated && !used.Contains(x) && From(x.def, CompatGroup.Factions) && x.def.techLevel >= TechLevel.Industrial && (x.HostileTo(Faction.OfPlayer) == c.Hostile));
            if (f == null) return false;
            c.faction = f;
            c.playerOpinion = f.GoodwillWith(Faction.OfPlayer);
            return true;
        }

        public static bool HarRace(Pawn p) => p != null && p.def != ThingDefOf.Human && CompatRegistry.AnyActive(CompatGroup.Races);
    }
}
