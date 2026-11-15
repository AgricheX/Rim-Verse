using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public static class EmpireWar
    {
        public const float WarNoClaimCost = 30f;

        public static float GoalScore(WarGoal g)
        {
            switch (g)
            {
                case WarGoal.Conquest: return 40f;
                case WarGoal.Subjugation: return 60f;
                case WarGoal.Plunder: return 25f;
                default: return 20f;
            }
        }

        public static AcceptanceReport CanClaim(GameComponent_PlayerEmpire e, Civilization c, CivPlanet p)
        {
            if (c == null || p == null) return false;
            if (!c.Reveals(CivKnowledge.Known)) return "Rimverse_ClaimNeedIntel".Translate();
            if (e.claims.Any(x => x.civId == c.id && x.systemKey == p.systemKey && x.body == p.body)) return "Rimverse_ClaimAlready".Translate();
            if (e.influence < GameComponent_PlayerEmpire.ClaimCost) return "Rimverse_NotEnoughInfluence".Translate(GameComponent_PlayerEmpire.ClaimCost);
            return true;
        }

        public static void Claim(GameComponent_PlayerEmpire e, Civilization c, CivPlanet p)
        {
            AcceptanceReport r = CanClaim(e, c, p);
            if (!r.Accepted)
            {
                if (!r.Reason.NullOrEmpty()) Messages.Message(r.Reason, MessageTypeDefOf.RejectInput, false);
                return;
            }
            e.influence -= GameComponent_PlayerEmpire.ClaimCost;
            e.claims.Add(new EmpireClaim { civId = c.id, systemKey = p.systemKey, body = p.body });
            GameComponent_Civilizations.Instance.ChangePlayerOpinion(c, -5f);
        }

        public static AcceptanceReport CanDeclare(GameComponent_PlayerEmpire e, Civilization c, WarGoal goal)
        {
            if (c == null || c.NoDiplomacy || c.Hostile) return false;
            if (c.playerState == DiploState.War) return "Rimverse_AlreadyAtWar".Translate();
            if (e.InFederation(c)) return "Rimverse_WarFederationMember".Translate();
            if (goal == WarGoal.Conquest && !e.claims.Any(x => x.civId == c.id)) return "Rimverse_WarNeedClaim".Translate();
            float cost = e.claims.Any(x => x.civId == c.id) ? 0f : WarNoClaimCost;
            if (e.influence < cost) return "Rimverse_NotEnoughInfluence".Translate(cost);
            return true;
        }

        public static void Declare(GameComponent_PlayerEmpire e, Civilization c, WarGoal goal)
        {
            AcceptanceReport r = CanDeclare(e, c, goal);
            if (!r.Accepted)
            {
                if (!r.Reason.NullOrEmpty()) Messages.Message(r.Reason, MessageTypeDefOf.RejectInput, false);
                return;
            }
            GameComponent_Civilizations gc = GameComponent_Civilizations.Instance;
            if (!e.claims.Any(x => x.civId == c.id)) e.influence -= WarNoClaimCost;
            gc.EnsureFaction(c);
            gc.SetPlayerState(c, DiploState.War);
            gc.ChangePlayerOpinion(c, -40f);
            e.federation.Remove(c.id);
            WarData w = e.WarWith(c, true);
            w.goal = goal;
            foreach (Civilization o in gc.Alive.Where(o => o != c && !o.Hostile))
            {
                CivRelation rel = o.RelationWith(c.id, false);
                if (rel != null && rel.state == DiploState.Alliance) gc.ChangePlayerOpinion(o, -20f);
                else if (rel != null && rel.state == DiploState.War) gc.ChangePlayerOpinion(o, 5f);
                else gc.ChangePlayerOpinion(o, -3f);
            }
            Find.LetterStack.ReceiveLetter("Rimverse_LetterWarDeclaredLabel".Translate(), "Rimverse_LetterWarDeclared".Translate(c.ShownName, ("Rimverse_WarGoal_" + goal).Translate()), LetterDefOf.ThreatBig);
        }

        public static float AcceptChance(GameComponent_PlayerEmpire e, Civilization c, WarData w)
        {
            float need = GoalScore(w.goal);
            float s = w.score + c.warWeariness * 0.3f;
            if (s >= need) return 1f;
            return Mathf.Clamp01((s - need * 0.5f) / need);
        }

        public static void ProposePeace(GameComponent_PlayerEmpire e, Civilization c, bool enforceGoal)
        {
            WarData w = e.WarWith(c, false);
            if (w == null) return;
            if (enforceGoal)
            {
                if (!Rand.Chance(AcceptChance(e, c, w)))
                {
                    Messages.Message("Rimverse_PeaceRejected".Translate(c.ShownName), MessageTypeDefOf.RejectInput, false);
                    w.exhaustion = Mathf.Min(100f, w.exhaustion + 3f);
                    return;
                }
                MakePeace(e, c, w, true);
                return;
            }
            if (w.score < -30f && !Rand.Chance(0.5f + w.exhaustion / 200f))
            {
                Messages.Message("Rimverse_PeaceRejected".Translate(c.ShownName), MessageTypeDefOf.RejectInput, false);
                return;
            }
            MakePeace(e, c, w, false);
        }

        public static void ForcePeace(GameComponent_PlayerEmpire e, Civilization c, WarData w)
        {
            MakePeace(e, c, w, w.score >= GoalScore(w.goal));
        }

        private static void MakePeace(GameComponent_PlayerEmpire e, Civilization c, WarData w, bool goalMet)
        {
            GameComponent_Civilizations gc = GameComponent_Civilizations.Instance;
            GameComponent_PlayerColonies pc = GameComponent_PlayerColonies.Instance;
            e.wars.Remove(w);
            string terms;
            if (goalMet)
            {
                switch (w.goal)
                {
                    case WarGoal.Conquest:
                        int taken = 0;
                        foreach (EmpireClaim cl in e.claims.Where(x => x.civId == c.id).ToList())
                        {
                            CivPlanet p = c.planets.FirstOrDefault(x => x.systemKey == cl.systemKey && x.body == cl.body);
                            if (p != null && EmpireFleetLogic.TakePlanet(c, p, false) != null) taken++;
                            e.claims.Remove(cl);
                        }
                        terms = "Rimverse_PeaceTermsConquest".Translate(taken);
                        break;
                    case WarGoal.Subjugation:
                        gc.SetPlayerState(c, DiploState.Vassal);
                        terms = "Rimverse_PeaceTermsVassal".Translate();
                        break;
                    case WarGoal.Plunder:
                        int silver = Mathf.RoundToInt(c.treasury * 0.3f) + 2000;
                        c.treasury = Mathf.Max(0f, c.treasury - silver);
                        if (pc != null) pc.treasury += silver;
                        terms = "Rimverse_PeaceTermsPlunder".Translate(silver);
                        break;
                    default:
                        e.influence = Mathf.Min(GameComponent_PlayerEmpire.InfluenceCap, e.influence + 40f);
                        c.stability = Mathf.Max(0f, c.stability - 15f);
                        terms = "Rimverse_PeaceTermsHumiliation".Translate();
                        break;
                }
            }
            else if (w.score <= -40f)
            {
                int pay = pc != null ? Mathf.Min(pc.treasury, 1500) : 0;
                if (pc != null) pc.treasury -= pay;
                e.influence = Mathf.Max(0f, e.influence - 20f);
                terms = "Rimverse_PeaceTermsLost".Translate(pay);
            }
            else terms = "Rimverse_PeaceTermsWhite".Translate();
            if (c.playerState == DiploState.War) gc.SetPlayerState(c, DiploState.Truce);
            c.warWeariness *= 0.5f;
            e.claims.RemoveAll(x => x.civId == c.id && w.goal != WarGoal.Conquest);
            Find.LetterStack.ReceiveLetter("Rimverse_LetterPeaceLabel".Translate(), "Rimverse_LetterEmpirePeace".Translate(c.ShownName, terms), LetterDefOf.NeutralEvent);
        }
    }
}
