using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public static class GalacticCouncil
    {
        public const int VotingDays = 15;
        public const float LobbyCost = 15f;

        public static float Factor(System.Func<ResolutionDef, float> f)
        {
            GameComponent_PlayerEmpire e = GameComponent_PlayerEmpire.Instance;
            if (e == null) return 1f;
            float v = 1f;
            foreach (ActiveResolution r in e.resolutions) if (r.def != null) v *= f(r.def);
            return v;
        }

        public static float PirateFactor => Factor(d => d.pirateFactor);

        public static float WarFactor => Factor(d => d.warFactor);

        public static float TariffFactor => Factor(d => d.tariffFactor);

        public static bool Sanctioned(Civilization c)
        {
            GameComponent_PlayerEmpire e = GameComponent_PlayerEmpire.Instance;
            return e != null && c != null && e.resolutions.Any(r => r.targetCiv == c.id && r.def != null && r.def.targetsCiv);
        }

        public static IEnumerable<Civilization> Members(GameComponent_Civilizations gc) => gc.Known.Where(c => !c.NoDiplomacy && !c.Hostile);

        public static void Step(GameComponent_PlayerEmpire e)
        {
            GameComponent_Civilizations gc = GameComponent_Civilizations.Instance;
            if (gc == null || !e.CouncilExists || e.proposal != null) return;
            List<ResolutionDef> defs = DefDatabase<ResolutionDef>.AllDefsListForReading.Where(d => !e.resolutions.Any(r => r.def == d)).ToList();
            if (defs.Count == 0) return;
            ResolutionDef pick = defs.RandomElement();
            int target = -1;
            if (pick.targetsCiv)
            {
                Civilization t = Members(gc).Concat(gc.Known.Where(c => c.Hostile && c.lore != LoreKind.Mechanoids)).OrderBy(c => -CivSimulation.FleetPower(gc, c) * Rand.Range(0.5f, 1.5f)).FirstOrDefault();
                if (t == null) return;
                target = t.id;
            }
            Propose(e, pick, target, false);
        }

        public static void Propose(GameComponent_PlayerEmpire e, ResolutionDef d, int target, bool byPlayer)
        {
            e.proposal = d;
            e.proposalTarget = target;
            e.playerVote = byPlayer ? 1 : 0;
            e.councilProposalTick = Find.TickManager.TicksGame + RimverseUnits.TicksPerDay * VotingDays;
            Find.LetterStack.ReceiveLetter("Rimverse_LetterCouncilLabel".Translate(), "Rimverse_LetterCouncil".Translate(Describe(d, target), VotingDays), LetterDefOf.NeutralEvent);
        }

        public static AcceptanceReport CanPlayerPropose(GameComponent_PlayerEmpire e, ResolutionDef d)
        {
            if (!e.CouncilExists) return "Rimverse_CouncilNone".Translate();
            if (e.proposal != null) return "Rimverse_CouncilBusy".Translate();
            if (e.resolutions.Any(r => r.def == d)) return "Rimverse_CouncilActive".Translate();
            if (e.influence < 40f) return "Rimverse_NotEnoughInfluence".Translate(40);
            return true;
        }

        public static void PlayerPropose(GameComponent_PlayerEmpire e, ResolutionDef d, int target)
        {
            AcceptanceReport r = CanPlayerPropose(e, d);
            if (!r.Accepted)
            {
                if (!r.Reason.NullOrEmpty()) Messages.Message(r.Reason, MessageTypeDefOf.RejectInput, false);
                return;
            }
            e.influence -= 40f;
            Propose(e, d, target, true);
        }

        public static string Describe(ResolutionDef d, int target)
        {
            if (d == null) return "-";
            if (!d.targetsCiv) return d.LabelCap;
            Civilization c = GameComponent_Civilizations.Instance?.Civ(target);
            return d.LabelCap + ": " + (c?.ShownName ?? "?");
        }

        public static float CivVote(Civilization c, ResolutionDef d, int target)
        {
            if (d.targetsCiv)
            {
                if (c.id == target) return -1f;
                CivRelation r = c.RelationWith(target, false);
                if (r == null) return 0.2f;
                if (r.state == DiploState.War) return 1f;
                if (r.state == DiploState.Alliance) return -1f;
                return r.opinion < 0f ? 0.6f : r.opinion > 30f ? -0.6f : 0f;
            }
            float v = 0f;
            AiProfile p = c.government?.profile ?? AiProfile.Cautious;
            if (d.favoredByPeaceful) v += p == AiProfile.Cautious || p == AiProfile.Mercantile ? 0.6f : -0.3f;
            if (d.favoredByMerchants) v += p == AiProfile.Mercantile ? 0.8f : c.HasCivic("Rimverse_Policy_CivicPirates") ? -1f : 0f;
            if (d.favoredByMilitary) v += p == AiProfile.Warmonger || p == AiProfile.Aggressive ? 0.8f : -0.4f;
            v += c.playerOpinion / 200f * (GameComponent_PlayerEmpire.Instance?.playerVote ?? 0);
            return Mathf.Clamp(v, -1f, 1f);
        }

        public static float CivWeight(Civilization c) => 1f + Mathf.Sqrt(Mathf.Max(0f, c.Population));

        public static float PlayerWeight(GameComponent_PlayerEmpire e)
        {
            GameComponent_PlayerColonies pc = GameComponent_PlayerColonies.Instance;
            float pop = pc?.colonies.Sum(c => c.population) ?? 0f;
            return 1f + Mathf.Sqrt(pop) * 0.5f + e.federation.Count;
        }

        public static void Tally(GameComponent_PlayerEmpire e, out float yes, out float no)
        {
            yes = no = 0f;
            GameComponent_Civilizations gc = GameComponent_Civilizations.Instance;
            if (gc == null || e.proposal == null) return;
            foreach (Civilization c in Members(gc))
            {
                float v = CivVote(c, e.proposal, e.proposalTarget);
                if (e.InFederation(c) && e.playerVote != 0) v = e.playerVote;
                if (v > 0.1f) yes += CivWeight(c) * v;
                else if (v < -0.1f) no += CivWeight(c) * -v;
            }
            if (e.playerVote > 0) yes += PlayerWeight(e) * e.playerVote;
            else if (e.playerVote < 0) no += PlayerWeight(e) * -e.playerVote;
        }

        public static void Lobby(GameComponent_PlayerEmpire e, int dir)
        {
            if (e.proposal == null || e.influence < LobbyCost) return;
            e.influence -= LobbyCost;
            e.playerVote = Mathf.Clamp(e.playerVote + dir, -4, 4);
        }

        public static void Resolve(GameComponent_PlayerEmpire e)
        {
            ResolutionDef d = e.proposal;
            int target = e.proposalTarget;
            Tally(e, out float yes, out float no);
            e.proposal = null;
            e.proposalTarget = -1;
            e.playerVote = 0;
            e.councilProposalTick = -1;
            if (d == null) return;
            bool passed = yes > no;
            if (passed)
            {
                e.resolutions.Add(new ActiveResolution { def = d, targetCiv = d.targetsCiv ? target : -1, endTick = Find.TickManager.TicksGame + Mathf.RoundToInt(d.durationYears * RimverseUnits.TicksPerYear) });
                Civilization t = d.targetsCiv ? GameComponent_Civilizations.Instance?.Civ(target) : null;
                if (t != null)
                {
                    t.stability = Mathf.Max(0f, t.stability + d.effects.stability);
                    t.treasury *= Mathf.Clamp01(1f + d.effects.income);
                }
            }
            Find.LetterStack.ReceiveLetter("Rimverse_LetterCouncilResultLabel".Translate(), (passed ? "Rimverse_LetterCouncilPassed" : "Rimverse_LetterCouncilFailed").Translate(Describe(d, target), yes.ToString("0"), no.ToString("0")), passed ? LetterDefOf.PositiveEvent : LetterDefOf.NeutralEvent);
        }
    }
}
