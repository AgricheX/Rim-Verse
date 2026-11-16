using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Rimverse
{
    public class AlienFaunaExtension : DefModExtension
    {
        public string archetype;
        public int herdMin = 1;
        public int herdMax = 3;
        public bool vacuum;
    }

    public class GenStep_AlienFauna : GenStep
    {
        public override int SeedPart => 482915377;

        public static List<PawnKindDef> Pool => DefDatabase<PawnKindDef>.AllDefs.Where(k => k.HasModExtension<AlienFaunaExtension>()).OrderBy(k => k.defName).ToList();

        public static List<PawnKindDef> SpeciesFor(BodyData body)
        {
            List<PawnKindDef> pool = Pool;
            List<PawnKindDef> result = new List<PawnKindDef>();
            if (body == null || body.homeWorld || pool.Count == 0) return result;
            string habitat = HabitatArchetype(body);
            foreach (PawnKindDef k in pool.Where(k => Special(k) && Arch(k) == habitat).ToList())
            {
                result.Add(k);
                pool.Remove(k);
            }
            pool.RemoveAll(k => Special(k) && Arch(k) != "Symbiont");
            float hab = CivilizationGenerator.Habitability(body);
            UniverseRand r = new UniverseRand(UniverseRand.Hash(GenText.StableStringHash(body.name ?? "body"), body.index, body.sizeClass, 7771));
            int count = hab < 0.3f ? r.RangeInclusive(1, 3) : hab < 0.6f ? r.RangeInclusive(3, 6) : r.RangeInclusive(6, 10);
            while (result.Count < count && pool.Count > 0)
            {
                int i = r.RangeInclusive(0, pool.Count - 1);
                result.Add(pool[i]);
                pool.RemoveAt(i);
            }
            return result;
        }

        private static readonly string[] SpecialArch = { "Swimmer", "FungalWalker", "Crystal", "Lava", "Symbiont" };

        private static string Arch(PawnKindDef k) => k.GetModExtension<AlienFaunaExtension>()?.archetype;

        private static bool Special(PawnKindDef k) => SpecialArch.Contains(Arch(k));

        public static string HabitatArchetype(BodyData body)
        {
            if (body.type == BodyType.Ocean || body.water > 0.6f) return "Swimmer";
            if (body.temperatureC >= 80f) return "Lava";
            if (body.flavor == BodyFlavor.Crystal) return "Crystal";
            if (body.flavor == BodyFlavor.Fungal) return "FungalWalker";
            return null;
        }

        public override void Generate(Map map, GenStepParams parms)
        {
            BodyData body = BodyGenContext.current;
            List<PawnKindDef> species = SpeciesFor(body);
            if (species.Count == 0) return;
            float areaFactor = map.Area / 62500f;
            foreach (PawnKindDef kind in species)
            {
                AlienFaunaExtension ext = kind.GetModExtension<AlienFaunaExtension>();
                int herds = System.Math.Max(1, (int)(Rand.RangeInclusive(1, 3) * areaFactor + 0.5f));
                for (int h = 0; h < herds; h++)
                {
                    if (!RCellFinder.TryFindRandomCellNearTheCenterOfTheMapWith(c => c.Standable(map) && !c.Fogged(map) && !c.Roofed(map), map, out IntVec3 center)) continue;
                    int n = Rand.RangeInclusive(ext.herdMin, ext.herdMax);
                    for (int i = 0; i < n; i++)
                    {
                        IntVec3 cell = CellFinder.RandomClosewalkCellNear(center, map, 6);
                        Pawn p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, null, PawnGenerationContext.NonPlayer, map.Tile));
                        GenSpawn.Spawn(p, cell, map);
                    }
                }
            }
        }
    }
}
