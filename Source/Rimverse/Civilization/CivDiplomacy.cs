using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public enum PlayerProposal
    {
        Truce,
        Peace,
        TradeAgreement,
        NonAggression,
        Alliance,
        DeclareWar
    }

    public static class CivDiplomacy
    {
        public const int ProposalCooldownTicks = RimverseUnits.TicksPerDay * 15;
        public const int TraderCooldownTicks = RimverseUnits.TicksPerDay * 7;

        public static Civilization CivAt(Map map)
        {
            GameComponent_Civilizations gc = GameComponent_Civilizations.Instance;
            MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(map);
            if (gc == null || loc == null || loc.Traveling) return null;
            return gc.OwnerOf(loc.SystemId);
        }

        public static Pawn Negotiator(Map map)
        {
            if (map == null) return null;
            return map.mapPawns.FreeColonistsSpawned.Where(p => !p.Downed && !p.skills.GetSkill(SkillDefOf.Social).TotallyDisabled && p.health.capacities.CapableOf(PawnCapacityDefOf.Talking)).OrderByDescending(p => p.skills.GetSkill(SkillDefOf.Social).Level).FirstOrDefault();
        }

        public static AcceptanceReport CanTrade(Civilization c, Map map)
        {
            if (c == null) return "Rimverse_NoCivHere".Translate();
            if (c.NoDiplomacy || c.Hostile) return "Rimverse_CivNoTalk".Translate(c.Name);
            if (c.playerState == DiploState.War) return "Rimverse_CivAtWar".Translate(c.Name);
            if (CivAt(map) != c) return "Rimverse_CivNotHere".Translate(c.Name);
            if (Negotiator(map) == null) return "Rimverse_NoNegotiator".Translate();
            return true;
        }

        public static void Hail(Civilization c, Map map)
        {
            AcceptanceReport rep = CanTrade(c, map);
            if (!rep.Accepted)
            {
                Messages.Message(rep.Reason, MessageTypeDefOf.RejectInput, false);
                return;
            }
            GameComponent_Civilizations gc = GameComponent_Civilizations.Instance;
            Faction f = gc.EnsureFaction(c);
            c.AddIntel(5f);
            Pawn negotiator = Negotiator(map);
            TradeShip existing = map.passingShipManager.passingShips.OfType<TradeShip>().FirstOrDefault(s => s.Faction == f && !s.Departed);
            if (existing == null)
            {
                existing = new TradeShip(RimverseDefOf.Rimverse_CivSpaceTrader, f);
                existing.name = c.Name;
                map.passingShipManager.AddShip(existing);
            }
            existing.TryOpenComms(negotiator);
        }

        public static float Threshold(PlayerProposal p)
        {
            switch (p)
            {
                case PlayerProposal.Truce: return -40f;
                case PlayerProposal.Peace: return 0f;
                case PlayerProposal.TradeAgreement: return 20f;
                case PlayerProposal.NonAggression: return 10f;
                case PlayerProposal.Alliance: return 60f;
                default: return -999f;
            }
        }

        public static bool Available(Civilization c, PlayerProposal p)
        {
            if (c.NoDiplomacy) return false;
            if (c.Hostile) return false;
            switch (p)
            {
                case PlayerProposal.Truce: return c.playerState == DiploState.War;
                case PlayerProposal.Peace: return c.playerState == DiploState.Truce;
                case PlayerProposal.TradeAgreement: return c.playerState == DiploState.Peace || c.playerState == DiploState.NonAggression;
                case PlayerProposal.NonAggression: return c.playerState == DiploState.Peace || c.playerState == DiploState.TradeAgreement;
                case PlayerProposal.Alliance: return c.playerState == DiploState.TradeAgreement || c.playerState == DiploState.NonAggression;
                default: return c.playerState != DiploState.War;
            }
        }

        public static float Bonus(Pawn negotiator)
        {
            if (negotiator == null) return 0f;
            return (negotiator.skills.GetSkill(SkillDefOf.Social).Level - 5) * 1.5f;
        }

        public static void Propose(Civilization c, PlayerProposal p, Map map)
        {
            GameComponent_Civilizations gc = GameComponent_Civilizations.Instance;
            int now = Find.TickManager.TicksGame;
            if (p != PlayerProposal.DeclareWar && now - c.lastPlayerProposalTick < ProposalCooldownTicks)
            {
                Messages.Message("Rimverse_ProposalCooldown".Translate(c.Name, RimverseUnits.DaysFromTicks(ProposalCooldownTicks - (now - c.lastPlayerProposalTick)).ToString("0")), MessageTypeDefOf.RejectInput, false);
                return;
            }
            gc.EnsureFaction(c);
            if (p == PlayerProposal.DeclareWar)
            {
                gc.SetPlayerState(c, DiploState.War);
                gc.ChangePlayerOpinion(c, -40f);
                Messages.Message("Rimverse_MsgWarDeclared".Translate(c.Name), MessageTypeDefOf.NegativeEvent, false);
                return;
            }
            c.lastPlayerProposalTick = now;
            float score = c.playerOpinion + Bonus(Negotiator(map));
            if (p == PlayerProposal.Truce) score += c.warWeariness * 0.5f;
            if (score >= Threshold(p))
            {
                DiploState state = p == PlayerProposal.Truce ? DiploState.Truce : p == PlayerProposal.Peace ? DiploState.Peace : p == PlayerProposal.TradeAgreement ? DiploState.TradeAgreement : p == PlayerProposal.NonAggression ? DiploState.NonAggression : DiploState.Alliance;
                gc.SetPlayerState(c, state);
                gc.ChangePlayerOpinion(c, 5f);
                Messages.Message("Rimverse_ProposalAccepted".Translate(c.Name, ("Rimverse_Diplo_" + state).Translate()), MessageTypeDefOf.PositiveEvent, false);
            }
            else
            {
                gc.ChangePlayerOpinion(c, -3f);
                Messages.Message("Rimverse_ProposalRefused".Translate(c.Name, Threshold(p).ToString("0"), score.ToString("0")), MessageTypeDefOf.RejectInput, false);
            }
        }

        public static float Tariff(DiploState s)
        {
            switch (s)
            {
                case DiploState.Alliance:
                case DiploState.Federation: return 0f;
                case DiploState.TradeAgreement: return 0.05f;
                case DiploState.Truce: return 0.3f;
                default: return 0.15f;
            }
        }
    }
}
