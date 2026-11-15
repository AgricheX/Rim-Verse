using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public static class ShipCrewUtility
    {
        public static float PilotLevel(Pawn pawn)
        {
            if (pawn?.skills == null) return 0f;
            float intellectual = pawn.skills.GetSkill(SkillDefOf.Intellectual).Level;
            float shooting = pawn.skills.GetSkill(SkillDefOf.Shooting).Level;
            float level = intellectual * 0.6f + shooting * 0.4f + pawn.GetStatValue(RimverseDefOf.Rimverse_PilotingOffset);
            float consciousness = pawn.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness);
            return Mathf.Clamp(level * Mathf.Min(1f, consciousness), 0f, 25f);
        }

        public static bool CanPilot(Pawn pawn)
        {
            return pawn != null && pawn.Spawned && !pawn.Dead && !pawn.Downed && pawn.IsColonist && !pawn.InMentalState && pawn.Awake()
                   && !pawn.WorkTypeIsDisabled(WorkTypeDefOf.Research);
        }

        public static List<Pawn> Crew(RimverseShip ship)
        {
            return ship.map.mapPawns.AllPawnsSpawned.Where(p => ship.cells.Contains(p.Position)).ToList();
        }

        public static void PilotAndCopilot(RimverseShip ship, out Pawn pilot, out Pawn copilot)
        {
            List<Pawn> candidates = Crew(ship).Where(CanPilot).OrderByDescending(PilotLevel).ToList();
            pilot = candidates.Count > 0 ? candidates[0] : null;
            copilot = candidates.Count > 1 ? candidates[1] : null;
        }

        public static float Dodge(RimverseShip ship, Pawn pilot)
        {
            if (ship.mass <= 0f) return 0.05f;
            float level = pilot == null ? 0f : PilotLevel(pilot);
            float dodge = 0.3f * (ship.thrust / ship.mass) * (1f + 0.03f * (level - 10f)) + ship.dodgeBonus;
            return Mathf.Clamp(dodge, 0.05f, 0.9f);
        }
    }
}
