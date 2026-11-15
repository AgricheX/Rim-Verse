using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public static class EmpireAnomalies
    {
        public const float FindChance = 0.06f;

        public static void OnScanned(StarSystemId id, int body)
        {
            GameComponent_PlayerEmpire e = GameComponent_PlayerEmpire.Instance;
            if (e == null || Current.ProgramState != ProgramState.Playing) return;
            string key = id.Key;
            if (e.anomalies.Any(a => a.systemKey == key && a.body == body)) return;
            UniverseRand r = new UniverseRand(UniverseRand.Hash(GameComponent_Universe.Instance.universeSeed, id.GetHashCode(), body, 4242));
            if (!r.Chance(FindChance)) return;
            List<SpaceAnomalyDef> defs = DefDatabase<SpaceAnomalyDef>.AllDefsListForReading.Where(d => d.minTech <= TechLevel.Spacer).ToList();
            if (defs.Count == 0) return;
            SpaceAnomalyDef pick = defs.RandomElementByWeight(d => d.commonality);
            e.anomalies.Add(new FoundAnomaly { def = pick, systemKey = key, body = body });
            BodyData b = GameComponent_Universe.Instance.GetSystem(id)?.Body(body);
            Find.LetterStack.ReceiveLetter("Rimverse_LetterAnomalyLabel".Translate(), "Rimverse_LetterAnomaly".Translate(pick.LabelCap, b?.name ?? EmpireFleetLogic.SystemName(key), pick.description), LetterDefOf.PositiveEvent);
        }

        public static Map MapFor(FoundAnomaly a) => FleetLogic.PlayerMapIn(a.systemKey);

        public static PlayerColony ColonyFor(FoundAnomaly a) => GameComponent_PlayerColonies.Instance?.colonies.FirstOrDefault(c => c.systemKey == a.systemKey);

        public static Pawn BestResearcher(Map map)
        {
            if (map == null) return null;
            return map.mapPawns.FreeColonistsSpawned.Where(p => p.skills != null && !p.skills.GetSkill(SkillDefOf.Intellectual).TotallyDisabled).OrderByDescending(p => p.skills.GetSkill(SkillDefOf.Intellectual).Level).FirstOrDefault();
        }

        public static AcceptanceReport CanStudy(FoundAnomaly a)
        {
            if (a.done || a.Working) return false;
            if (a.def.needsColony && ColonyFor(a) == null) return "Rimverse_AnomalyNeedColony".Translate();
            if (!a.def.needsColony && MapFor(a) == null && ColonyFor(a) == null) return "Rimverse_AnomalyNeedPresence".Translate();
            return true;
        }

        public static void Study(FoundAnomaly a)
        {
            AcceptanceReport r = CanStudy(a);
            if (!r.Accepted)
            {
                if (!r.Reason.NullOrEmpty()) Messages.Message(r.Reason, MessageTypeDefOf.RejectInput, false);
                return;
            }
            Pawn p = BestResearcher(MapFor(a));
            int skill = p?.skills.GetSkill(SkillDefOf.Intellectual).Level ?? 4;
            float days = a.def.researchDays / (1f + skill / 10f);
            GameComponent_PlayerEmpire e = GameComponent_PlayerEmpire.Instance;
            if (e != null) days /= 1f + Mathf.Max(0f, e.GlobalEffects().research) / 100f;
            a.workEndTick = Find.TickManager.TicksGame + RimverseUnits.TicksFromDays(Mathf.Max(0.5f, days));
        }

        public static void Tick(GameComponent_PlayerEmpire e, int now)
        {
            foreach (FoundAnomaly a in e.anomalies)
            {
                if (!a.Working || now < a.workEndTick) continue;
                a.workEndTick = -1;
                a.chapter++;
                if (a.def.dangerChance > 0f && Rand.Chance(a.def.dangerChance)) Danger(a);
                if (a.chapter >= a.def.chapters)
                {
                    a.done = true;
                    Reward(e, a);
                }
                else
                {
                    string text = ("Rimverse_AnomalyChapter_" + a.def.defName + "_" + a.chapter).CanTranslate() ? ("Rimverse_AnomalyChapter_" + a.def.defName + "_" + a.chapter).Translate().ToString() : "Rimverse_AnomalyChapterGeneric".Translate().ToString();
                    Find.LetterStack.ReceiveLetter("Rimverse_LetterAnomalyChapterLabel".Translate(a.def.LabelCap, a.chapter, a.def.chapters), text, LetterDefOf.NeutralEvent);
                }
            }
        }

        private static void Danger(FoundAnomaly a)
        {
            Map map = MapFor(a);
            PlayerColony c = ColonyFor(a);
            if (map != null && Rand.Chance(0.5f))
            {
                IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatSmall, map);
                parms.forced = true;
                parms.points = Mathf.Max(100f, parms.points * 0.6f);
                IncidentDef inc = DefDatabase<IncidentDef>.GetNamedSilentFail("ShortCircuit") ?? IncidentDefOf.RaidEnemy;
                if (inc == IncidentDefOf.RaidEnemy) parms.faction = ShipCombatUtility.PirateFaction(GameComponent_Civilizations.Instance);
                if (inc.Worker.CanFireNow(parms)) inc.Worker.TryExecute(parms);
            }
            else if (c != null)
            {
                c.population *= 0.95f;
                c.stability = Mathf.Max(0f, c.stability - 8f);
            }
            Find.LetterStack.ReceiveLetter("Rimverse_LetterAnomalyDangerLabel".Translate(), "Rimverse_LetterAnomalyDanger".Translate(a.def.LabelCap), LetterDefOf.NegativeEvent);
        }

        private static void Reward(GameComponent_PlayerEmpire e, FoundAnomaly a)
        {
            SpaceAnomalyDef d = a.def;
            if (d.rewardResearch > 0 && Find.ResearchManager.GetProject() != null) Find.ResearchManager.AddProgress(Find.ResearchManager.GetProject(), d.rewardResearch);
            GameComponent_PlayerColonies pc = GameComponent_PlayerColonies.Instance;
            if (d.rewardSilver > 0 && pc != null) pc.treasury += d.rewardSilver;
            if (d.rewardInfluence > 0f) e.influence = Mathf.Min(GameComponent_PlayerEmpire.InfluenceCap, e.influence + d.rewardInfluence);
            if (d.rewardItems.Count > 0)
            {
                Map map = MapFor(a);
                PlayerColony c = ColonyFor(a);
                List<Thing> things = new List<Thing>();
                foreach (ThingDefCountClass t in d.rewardItems)
                {
                    if (t.thingDef == null) continue;
                    if (map == null && c != null) c.AddStock(t.thingDef.defName, t.count);
                    else ColonyUtility.AddStacks(things, t.thingDef, t.count);
                }
                if (things.Count > 0)
                {
                    Map target = map ?? Find.AnyPlayerHomeMap;
                    if (target != null) ColonyUtility.Deliver(target, things);
                }
            }
            string key = "Rimverse_AnomalyEnd_" + d.defName;
            string text = key.CanTranslate() ? key.Translate().ToString() : "Rimverse_AnomalyEndGeneric".Translate().ToString();
            Find.LetterStack.ReceiveLetter("Rimverse_LetterAnomalyDoneLabel".Translate(d.LabelCap), text + "\n\n" + "Rimverse_AnomalyRewards".Translate(d.rewardResearch, d.rewardSilver, d.rewardInfluence.ToString("0")), LetterDefOf.PositiveEvent);
        }
    }
}
