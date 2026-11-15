using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public enum AllyRequest
    {
        JointAttack,
        Defense,
        Supplies
    }

    public static class EmpireDiplomacy
    {
        public static float CivPower(GameComponent_Civilizations gc, Civilization c) => CivSimulation.FleetPower(gc, c) + CivSimulation.MilitaryCapacity(c) * 0.5f;

        public static bool IsAlly(Civilization c) => c != null && (c.playerState == DiploState.Alliance || c.playerState == DiploState.Federation || c.playerState == DiploState.Vassal);

        public static float RequestOpinion(AllyRequest r) => r == AllyRequest.JointAttack ? 15f : 10f;

        public static float RequestInfluence(AllyRequest r) => r == AllyRequest.JointAttack ? 20f : r == AllyRequest.Defense ? 10f : 5f;

        public static AcceptanceReport CanRequest(GameComponent_PlayerEmpire e, Civilization c, AllyRequest r)
        {
            if (c == null || c.dead) return false;
            if (r == AllyRequest.Supplies ? c.playerState < DiploState.TradeAgreement : !IsAlly(c)) return "Rimverse_RequestNeedAlly".Translate();
            if (c.playerOpinion < RequestOpinion(r)) return "Rimverse_RequestLowOpinion".Translate();
            if (e.influence < RequestInfluence(r)) return "Rimverse_NotEnoughInfluence".Translate(RequestInfluence(r));
            if (r == AllyRequest.Defense && e.allyDefense.ContainsKey(c.id)) return "Rimverse_RequestActive".Translate();
            if (r == AllyRequest.JointAttack && !e.wars.Any()) return "Rimverse_RequestNoWar".Translate();
            return true;
        }

        public static void Request(GameComponent_PlayerEmpire e, Civilization c, AllyRequest r, Map map)
        {
            AcceptanceReport ok = CanRequest(e, c, r);
            if (!ok.Accepted)
            {
                if (!ok.Reason.NullOrEmpty()) Messages.Message(ok.Reason, MessageTypeDefOf.RejectInput, false);
                return;
            }
            GameComponent_Civilizations gc = GameComponent_Civilizations.Instance;
            e.influence -= RequestInfluence(r);
            gc.ChangePlayerOpinion(c, -RequestOpinion(r));
            switch (r)
            {
                case AllyRequest.JointAttack:
                    Civilization enemy = e.wars.Select(w => gc.Civ(w.civId)).Where(x => x != null && !x.dead).OrderBy(x => gc.DistanceLy(c, x)).FirstOrDefault();
                    if (enemy == null) return;
                    gc.SetState(c, enemy, DiploState.War);
                    string dest = enemy.planets.OrderBy(p => p.population).FirstOrDefault()?.systemKey ?? enemy.capitalKey;
                    Fleet f = CivSimulation.LaunchFleet(gc, c, dest, enemy.id, FleetMission.War, 0.5f);
                    Messages.Message(f != null ? "Rimverse_MsgAllyAttack".Translate(c.ShownName, enemy.ShownName) : "Rimverse_MsgAllyNoFleet".Translate(c.ShownName), f != null ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NeutralEvent, false);
                    WarData w0 = e.WarWith(enemy, false);
                    if (w0 != null && f != null) w0.score = Mathf.Min(100f, w0.score + 5f);
                    break;
                case AllyRequest.Defense:
                    e.allyDefense[c.id] = Find.TickManager.TicksGame + RimverseUnits.TicksPerYear;
                    Messages.Message("Rimverse_MsgAllyDefense".Translate(c.ShownName), MessageTypeDefOf.PositiveEvent, false);
                    break;
                default:
                    List<Thing> things = new List<Thing>();
                    ColonyUtility.AddStacks(things, ThingDefOf.Steel, 200);
                    ColonyUtility.AddStacks(things, ThingDefOf.ComponentIndustrial, 8);
                    ColonyUtility.AddStacks(things, ThingDefOf.Chemfuel, 100);
                    ColonyUtility.AddStacks(things, ThingDefOf.MealSurvivalPack, 20);
                    Map m = map ?? Find.AnyPlayerHomeMap;
                    if (m != null) ColonyUtility.Deliver(m, things);
                    Messages.Message("Rimverse_MsgAllySupplies".Translate(c.ShownName), MessageTypeDefOf.PositiveEvent, false);
                    break;
            }
        }

        public static float AllyDefense(GameComponent_PlayerEmpire e, string systemKey)
        {
            GameComponent_Civilizations gc = GameComponent_Civilizations.Instance;
            if (gc == null) return 0f;
            float d = 0f;
            foreach (int id in e.allyDefense.Keys)
            {
                Civilization c = gc.Civ(id);
                if (c == null || c.dead) continue;
                double ly = gc.DistanceLy(c, StarSystemId.FromKey(systemKey));
                if (ly <= c.InfluenceRadiusLy + 100.0) d += CivPower(gc, c) * 0.15f;
            }
            foreach (int id in e.federation)
            {
                Civilization c = gc.Civ(id);
                if (c != null && !c.dead) d += CivPower(gc, c) * 0.05f;
            }
            return d;
        }

        public static AcceptanceReport CanFoundFederation(GameComponent_PlayerEmpire e, Civilization c)
        {
            if (!e.federationName.NullOrEmpty()) return "Rimverse_FederationExists".Translate();
            if (c == null || c.playerState != DiploState.Alliance) return "Rimverse_RequestNeedAlly".Translate();
            if (c.playerOpinion < 40f) return "Rimverse_RequestLowOpinion".Translate();
            if (e.influence < GameComponent_PlayerEmpire.FederationCost) return "Rimverse_NotEnoughInfluence".Translate(GameComponent_PlayerEmpire.FederationCost);
            return true;
        }

        public static void FoundFederation(GameComponent_PlayerEmpire e, Civilization c, string name)
        {
            AcceptanceReport r = CanFoundFederation(e, c);
            if (!r.Accepted)
            {
                if (!r.Reason.NullOrEmpty()) Messages.Message(r.Reason, MessageTypeDefOf.RejectInput, false);
                return;
            }
            e.influence -= GameComponent_PlayerEmpire.FederationCost;
            e.federationName = name.NullOrEmpty() ? "Rimverse_FederationDefaultName".Translate().ToString() : name;
            Join(e, c);
            Find.LetterStack.ReceiveLetter("Rimverse_LetterFederationLabel".Translate(), "Rimverse_LetterFederation".Translate(e.federationName, c.ShownName), LetterDefOf.PositiveEvent);
        }

        public static AcceptanceReport CanInvite(GameComponent_PlayerEmpire e, Civilization c)
        {
            if (e.federationName.NullOrEmpty()) return "Rimverse_FederationNone".Translate();
            if (c == null || e.InFederation(c)) return false;
            if (c.playerState != DiploState.Alliance) return "Rimverse_RequestNeedAlly".Translate();
            if (c.playerOpinion < 50f) return "Rimverse_RequestLowOpinion".Translate();
            if (e.influence < 20f) return "Rimverse_NotEnoughInfluence".Translate(20);
            return true;
        }

        public static void Invite(GameComponent_PlayerEmpire e, Civilization c)
        {
            AcceptanceReport r = CanInvite(e, c);
            if (!r.Accepted)
            {
                if (!r.Reason.NullOrEmpty()) Messages.Message(r.Reason, MessageTypeDefOf.RejectInput, false);
                return;
            }
            e.influence -= 20f;
            Join(e, c);
            Messages.Message("Rimverse_MsgFederationJoined".Translate(c.ShownName, e.federationName), MessageTypeDefOf.PositiveEvent, false);
        }

        private static void Join(GameComponent_PlayerEmpire e, Civilization c)
        {
            GameComponent_Civilizations gc = GameComponent_Civilizations.Instance;
            if (!e.federation.Contains(c.id)) e.federation.Add(c.id);
            gc.SetPlayerState(c, DiploState.Federation);
            foreach (int id in e.federation)
            {
                Civilization o = gc.Civ(id);
                if (o != null && o != c) gc.SetState(c, o, DiploState.Alliance);
            }
        }

        public static void Leave(GameComponent_PlayerEmpire e, Civilization c)
        {
            GameComponent_Civilizations gc = GameComponent_Civilizations.Instance;
            e.federation.Remove(c.id);
            if (c.playerState == DiploState.Federation) gc.SetPlayerState(c, DiploState.Alliance);
            gc.ChangePlayerOpinion(c, -20f);
            if (e.federation.Count == 0) e.federationName = null;
        }

        public static void ReleaseVassal(GameComponent_PlayerEmpire e, Civilization c)
        {
            GameComponent_Civilizations gc = GameComponent_Civilizations.Instance;
            gc.SetPlayerState(c, DiploState.Alliance);
            gc.ChangePlayerOpinion(c, 30f);
        }

        public static void BalanceOfPower(GameComponent_PlayerEmpire e, GameComponent_Civilizations gc)
        {
            GameComponent_PlayerColonies pc = GameComponent_PlayerColonies.Instance;
            foreach (Civilization v in gc.Alive.Where(x => x.playerState == DiploState.Vassal).ToList())
            {
                int tribute = Mathf.RoundToInt(v.treasury * 0.05f + v.Population * 2f);
                v.treasury = Mathf.Max(0f, v.treasury - tribute);
                if (pc != null) pc.treasury += tribute;
                gc.ChangePlayerOpinion(v, -2f);
                if (v.playerOpinion < -40f && Rand.Chance(0.3f))
                {
                    gc.SetPlayerState(v, DiploState.War);
                    e.WarWith(v, true).goal = WarGoal.Subjugation;
                    Find.LetterStack.ReceiveLetter("Rimverse_LetterVassalRevoltLabel".Translate(), "Rimverse_LetterVassalRevolt".Translate(v.ShownName), LetterDefOf.ThreatBig);
                }
            }
            if (pc == null || pc.colonies.Count < 5) return;
            float mine = e.PlayerPower();
            List<Civilization> rivals = gc.Known.Where(x => !x.Hostile && !x.NoDiplomacy && !IsAlly(x)).ToList();
            if (rivals.Count == 0) return;
            float top = rivals.Max(x => CivPower(gc, x));
            if (mine < top * 1.5f) return;
            List<Civilization> worried = rivals.Where(x => x.playerOpinion < 30f).ToList();
            foreach (Civilization c in worried)
            {
                gc.ChangePlayerOpinion(c, -4f);
                foreach (Civilization o in worried) if (o != c) { CivRelation r = c.RelationWith(o.id, true); r.opinion = Mathf.Clamp(r.opinion + 5f, -100f, 100f); }
            }
            if (worried.Count >= 2 && Rand.Chance(0.15f))
            {
                Civilization lead = worried.OrderBy(x => x.playerOpinion).First();
                if (lead.playerState != DiploState.War)
                {
                    gc.SetPlayerState(lead, DiploState.War);
                    e.WarWith(lead, true);
                    Find.LetterStack.ReceiveLetter("Rimverse_LetterCoalitionLabel".Translate(), "Rimverse_LetterCoalition".Translate(lead.ShownName, worried.Count), LetterDefOf.ThreatBig);
                }
            }
        }
    }
}
