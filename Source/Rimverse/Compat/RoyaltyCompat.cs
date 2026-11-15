using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Rimverse
{
    public class CompProperties_AbilitySenseSignatures : CompProperties_AbilityEffect
    {
        public float rangeLy = 12f;

        public CompProperties_AbilitySenseSignatures()
        {
            compClass = typeof(CompAbilityEffect_SenseSignatures);
        }
    }

    public class CompAbilityEffect_SenseSignatures : CompAbilityEffect
    {
        public new CompProperties_AbilitySenseSignatures Props => (CompProperties_AbilitySenseSignatures)props;

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Map map = parent.pawn?.Map;
            MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(map);
            if (loc == null || loc.systemKey.NullOrEmpty())
            {
                if (throwMessages) Messages.Message("Rimverse_PsyNeedSpace".Translate(), MessageTypeDefOf.RejectInput, false);
                return false;
            }
            return base.Valid(target, throwMessages);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            GameComponent_Universe u = GameComponent_Universe.Instance;
            MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(parent.pawn?.Map);
            if (u == null || loc == null || loc.systemKey.NullOrEmpty()) return;
            StarSystemId here = loc.SystemId;
            float range = Props.rangeLy * parent.pawn.GetStatValue(StatDefOf.PsychicSensitivity);
            u.SetSystemScan(here, ScanLevel.Exact);
            int count = 0;
            foreach (StarEntry e in GalaxyQuery.StarsInRadius(u, here.galaxy, u.PositionOf(here), range))
            {
                if (u.GetSystemScan(e.id) < ScanLevel.Range) u.SetSystemScan(e.id, ScanLevel.Range);
                count++;
            }
            Messages.Message("Rimverse_PsySensed".Translate(parent.pawn.LabelShort, count, range.ToString("0.#")), parent.pawn, MessageTypeDefOf.PositiveEvent, false);
        }
    }

    public class CompProperties_AbilityCalmCrew : CompProperties_AbilityEffect
    {
        public ThoughtDef thought;
        public float radius = 30f;

        public CompProperties_AbilityCalmCrew()
        {
            compClass = typeof(CompAbilityEffect_CalmCrew);
        }
    }

    public class CompAbilityEffect_CalmCrew : CompAbilityEffect
    {
        public new CompProperties_AbilityCalmCrew Props => (CompProperties_AbilityCalmCrew)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            if (caster?.Map == null || Props.thought == null) return;
            foreach (Pawn p in caster.Map.mapPawns.FreeColonistsSpawned)
            {
                if (p == caster || p.needs?.mood == null) continue;
                if (p.Position.DistanceTo(caster.Position) > Props.radius) continue;
                if (p.GetStatValue(StatDefOf.PsychicSensitivity) <= 0f) continue;
                p.needs.mood.thoughts.memories.TryGainMemory(Props.thought);
                if (p.InMentalState && p.MentalState.def.IsAggro == false && Rand.Chance(0.5f)) p.MentalState.RecoverFromState();
            }
        }
    }

    public static class RoyaltyCompat
    {
        public static bool TryLinkEmpire(Civilization c)
        {
            if (!ModCompat.Royalty || c == null || c.lore != LoreKind.Empire) return false;
            Faction empire = Faction.OfEmpire;
            if (empire == null || empire.defeated) return false;
            c.faction = empire;
            c.playerOpinion = empire.GoodwillWith(Faction.OfPlayer);
            return true;
        }
    }
}
