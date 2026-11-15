using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public class GameComponent_PlayerEmpire : GameComponent
    {
        public const float InfluenceCap = 300f;
        public const float ReformCost = 50f;
        public const float ClaimCost = 25f;
        public const float FederationCost = 60f;

        public float influence = 20f;
        public PolicyDef government;
        public PolicyDef ideology;
        public PolicyDef economy;
        public PolicyDef xeno;
        public List<PolicyDef> civics = new List<PolicyDef>();
        public bool governmentChosen;
        public int lastReformTick = -999999;
        public List<ActiveEdict> edicts = new List<ActiveEdict>();
        public List<SectorGovernor> governors = new List<SectorGovernor>();
        public List<PlayerFleet> fleets = new List<PlayerFleet>();
        public int nextFleetId = 1;
        public List<WarData> wars = new List<WarData>();
        public List<EmpireClaim> claims = new List<EmpireClaim>();
        public string federationName;
        public List<int> federation = new List<int>();
        public int councilProposalTick = -1;
        public ResolutionDef proposal;
        public int proposalTarget = -1;
        public int playerVote;
        public List<ActiveResolution> resolutions = new List<ActiveResolution>();
        public List<FoundAnomaly> anomalies = new List<FoundAnomaly>();
        public Dictionary<int, int> allyDefense = new Dictionary<int, int>();
        public int nextBalanceTick;
        public int nextCouncilTick;
        public float researchBank;

        public static GameComponent_PlayerEmpire Instance => Current.Game?.GetComponent<GameComponent_PlayerEmpire>();

        public GameComponent_PlayerEmpire(Game game)
        {
        }

        public IEnumerable<PolicyDef> Policies
        {
            get
            {
                if (government != null) yield return government;
                if (ideology != null) yield return ideology;
                if (economy != null) yield return economy;
                if (xeno != null) yield return xeno;
                foreach (PolicyDef c in civics) if (c != null) yield return c;
            }
        }

        public float PolicyStat(CivStat s) => Policies.Sum(p => p.Effect(s));

        public bool CouncilExists
        {
            get
            {
                GameComponent_Civilizations gc = GameComponent_Civilizations.Instance;
                return gc != null && gc.Alive.Count(c => c.known && !c.NoDiplomacy && !c.Hostile) >= 3;
            }
        }

        public bool InFederation(Civilization c) => c != null && federation.Contains(c.id);

        public WarData WarWith(Civilization c, bool create)
        {
            if (c == null) return null;
            WarData w = wars.FirstOrDefault(x => x.civId == c.id);
            if (w == null && create)
            {
                w = new WarData { civId = c.id, startTick = Find.TickManager.TicksGame, goal = claims.Any(x => x.civId == c.id) ? WarGoal.Conquest : WarGoal.Humiliation };
                wars.Add(w);
            }
            return w;
        }

        public Pawn GovernorOf(string systemKey) => governors.FirstOrDefault(g => g.systemKey == systemKey)?.pawn;

        public void SetGovernor(string systemKey, Pawn p)
        {
            governors.RemoveAll(g => g.systemKey == systemKey || g.pawn == p);
            if (p != null) governors.Add(new SectorGovernor { systemKey = systemKey, pawn = p });
        }

        public EmpireEffects GlobalEffects()
        {
            EmpireEffects e = new EmpireEffects();
            e.stability += PolicyStat(CivStat.Stability) * 0.5f + PolicyStat(CivStat.Loyalty) * 0.3f;
            e.income += (PolicyStat(CivStat.Production) + PolicyStat(CivStat.Trade) * 0.5f) / 100f;
            e.growth += PolicyStat(CivStat.Growth) / 100f;
            e.defense += PolicyStat(CivStat.Military) * 0.5f;
            e.research += PolicyStat(CivStat.Research) * 2f;
            e.influence += PolicyStat(CivStat.Culture) / 10f;
            foreach (ActiveEdict ed in edicts) e.Add(ed.def?.effects);
            foreach (ActiveResolution r in resolutions) if (r.targetCiv < 0) e.Add(r.def?.effects);
            if (federation.Count > 0)
            {
                e.research += 10f;
                e.defense += 5f * federation.Count;
            }
            return e;
        }

        public EmpireEffects ColonyEffects(PlayerColony c)
        {
            EmpireEffects e = GlobalEffects();
            foreach (ColonyFacility f in c.facilities) if (f.Ready && f.def != null) e.Add(f.def.effects);
            Pawn gov = GovernorOf(c.systemKey);
            if (gov != null && !gov.Dead && gov.skills != null)
            {
                e.stability += gov.skills.GetSkill(SkillDefOf.Social).Level * 0.5f;
                e.income += gov.skills.GetSkill(SkillDefOf.Intellectual).Level / 100f;
                e.defense += gov.skills.GetSkill(SkillDefOf.Shooting).Level;
            }
            foreach (PlayerFleet f in fleets) if (!f.Moving && f.systemKey == c.systemKey && (f.mission == PlayerFleetMission.Defend || f.mission == PlayerFleetMission.Idle)) e.defense += f.Power * 0.5f;
            return e;
        }

        public float PlayerPower()
        {
            float p = fleets.Sum(f => f.Power);
            GameComponent_PlayerColonies pc = GameComponent_PlayerColonies.Instance;
            if (pc != null) p += pc.colonies.Sum(c => c.strength * 0.5f + c.population * 0.2f);
            return p;
        }

        public float InfluencePerDay
        {
            get
            {
                float v = 0.1f + GlobalEffects().influence / 10f;
                GameComponent_PlayerColonies pc = GameComponent_PlayerColonies.Instance;
                if (pc != null)
                {
                    v += pc.colonies.Count * 0.03f;
                    foreach (PlayerColony c in pc.colonies) foreach (ColonyFacility f in c.facilities) if (f.Ready && f.def != null) v += f.def.effects.influence / 10f;
                }
                v += federation.Count * 0.05f;
                return Mathf.Max(0f, v);
            }
        }

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (now % RimverseUnits.TicksPerHour != 1337) return;
            float days = RimverseUnits.TicksPerHour / (float)RimverseUnits.TicksPerDay;
            influence = Mathf.Min(InfluenceCap, influence + InfluencePerDay * days);
            edicts.RemoveAll(e => e.def == null || now >= e.endTick);
            resolutions.RemoveAll(r => r.def == null || now >= r.endTick);
            governors.RemoveAll(g => g.pawn == null || g.pawn.Dead);
            EmpireFleetLogic.Tick(this, now);
            EmpireAnomalies.Tick(this, now);
            if (now % RimverseUnits.TicksPerDay < RimverseUnits.TicksPerHour)
            {
                DailyStep(now);
            }
            if (now >= nextBalanceTick)
            {
                nextBalanceTick = now + RimverseUnits.TicksPerQuadrum;
                QuadrumStep();
            }
            if (now >= nextCouncilTick)
            {
                nextCouncilTick = now + RimverseUnits.TicksPerYear / 2;
                GalacticCouncil.Step(this);
            }
            if (proposal != null && councilProposalTick > 0 && now >= councilProposalTick) GalacticCouncil.Resolve(this);
        }

        private void DailyStep(int now)
        {
            float research = GlobalEffects().research / 15f;
            if (research > 0f && Find.ResearchManager.GetProject() != null)
            {
                researchBank += research;
                if (researchBank >= 1f)
                {
                    Find.ResearchManager.AddProgress(Find.ResearchManager.GetProject(), Mathf.Floor(researchBank));
                    researchBank -= Mathf.Floor(researchBank);
                }
            }
        }

        private void QuadrumStep()
        {
            GameComponent_PlayerColonies pc = GameComponent_PlayerColonies.Instance;
            GameComponent_Civilizations gc = GameComponent_Civilizations.Instance;
            float upkeepMult = Mathf.Max(0.2f, 1f + GlobalEffects().fleetUpkeep);
            int upkeep = Mathf.RoundToInt(fleets.Sum(f => f.Upkeep) * upkeepMult);
            if (pc != null && upkeep > 0)
            {
                if (pc.treasury >= upkeep) pc.treasury -= upkeep;
                else
                {
                    pc.treasury = 0;
                    foreach (PlayerFleet f in fleets) f.condition = Mathf.Max(0.3f, f.condition - 0.15f);
                    Find.LetterStack.ReceiveLetter("Rimverse_LetterFleetUnpaidLabel".Translate(), "Rimverse_LetterFleetUnpaid".Translate(upkeep), LetterDefOf.NegativeEvent);
                }
            }
            foreach (PlayerFleet f in fleets) if (!f.Moving && f.condition < 1f && EmpireFleetLogic.HasShipyard(f.systemKey)) f.condition = Mathf.Min(1f, f.condition + 0.25f);
            if (gc == null) return;
            foreach (WarData w in wars.ToList())
            {
                Civilization c = gc.Civ(w.civId);
                if (c == null || c.dead || c.playerState != DiploState.War)
                {
                    wars.Remove(w);
                    continue;
                }
                w.exhaustion = Mathf.Min(100f, w.exhaustion + 4f);
                if (w.exhaustion >= 100f) EmpireWar.ForcePeace(this, c, w);
            }
            foreach (Civilization c in gc.Alive.Where(c => c.playerState == DiploState.War && !c.Hostile)) WarWith(c, true);
            foreach (int id in federation.ToList())
            {
                Civilization m = gc.Civ(id);
                if (m == null || m.dead || m.playerState == DiploState.War)
                {
                    federation.Remove(id);
                    continue;
                }
                gc.ChangePlayerOpinion(m, 1.5f);
                foreach (int o in federation) if (o != id) { Civilization other = gc.Civ(o); if (other != null) { CivRelation r = m.RelationWith(o, true); r.opinion = Mathf.Clamp(r.opinion + 2f, -100f, 100f); } }
            }
            float op = GlobalEffects().opinion;
            if (op != 0f) foreach (Civilization c in gc.Known.Where(c => !c.Hostile && !c.NoDiplomacy)) gc.ChangePlayerOpinion(c, op);
            foreach (KeyValuePair<int, int> kv in allyDefense.ToList()) if (Find.TickManager.TicksGame >= kv.Value) allyDefense.Remove(kv.Key);
            EmpireDiplomacy.BalanceOfPower(this, gc);
        }

        public bool CanReform => Find.TickManager.TicksGame - lastReformTick >= RimverseUnits.TicksPerYear && influence >= ReformCost;

        public void ApplyGovernment(PolicyDef gov, PolicyDef ide, PolicyDef eco, PolicyDef xen, List<PolicyDef> civ)
        {
            if (governmentChosen)
            {
                influence -= ReformCost;
                lastReformTick = Find.TickManager.TicksGame;
            }
            government = gov;
            ideology = ide;
            economy = eco;
            xeno = xen;
            civics = civ.Where(c => c != null).Take(MaxCivics).ToList();
            governmentChosen = true;
        }

        public int MaxCivics
        {
            get
            {
                GameComponent_PlayerColonies pc = GameComponent_PlayerColonies.Instance;
                int n = 2;
                if (pc != null && pc.colonies.Count >= 3) n++;
                if (pc != null && pc.colonies.Count >= 8) n++;
                return n;
            }
        }

        public bool TryEdict(EdictDef d)
        {
            GameComponent_PlayerColonies pc = GameComponent_PlayerColonies.Instance;
            if (d == null || pc == null || edicts.Any(e => e.def == d)) return false;
            if (pc.treasury < d.costSilver || influence < d.costInfluence) return false;
            if (d.research != null && !d.research.IsFinished) return false;
            if (d.war && wars.Count == 0) return false;
            pc.treasury -= d.costSilver;
            influence -= d.costInfluence;
            edicts.Add(new ActiveEdict { def = d, endTick = Find.TickManager.TicksGame + RimverseUnits.TicksFromDays(d.durationDays) });
            return true;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref influence, "influence", 20f);
            Scribe_Defs.Look(ref government, "government");
            Scribe_Defs.Look(ref ideology, "ideology");
            Scribe_Defs.Look(ref economy, "economy");
            Scribe_Defs.Look(ref xeno, "xeno");
            Scribe_Collections.Look(ref civics, "civics", LookMode.Def);
            Scribe_Values.Look(ref governmentChosen, "governmentChosen");
            Scribe_Values.Look(ref lastReformTick, "lastReformTick", -999999);
            Scribe_Collections.Look(ref edicts, "edicts", LookMode.Deep);
            Scribe_Collections.Look(ref governors, "governors", LookMode.Deep);
            Scribe_Collections.Look(ref fleets, "fleets", LookMode.Deep);
            Scribe_Values.Look(ref nextFleetId, "nextFleetId", 1);
            Scribe_Collections.Look(ref wars, "wars", LookMode.Deep);
            Scribe_Collections.Look(ref claims, "claims", LookMode.Deep);
            Scribe_Values.Look(ref federationName, "federationName");
            Scribe_Collections.Look(ref federation, "federation", LookMode.Value);
            Scribe_Values.Look(ref councilProposalTick, "councilProposalTick", -1);
            Scribe_Defs.Look(ref proposal, "proposal");
            Scribe_Values.Look(ref proposalTarget, "proposalTarget", -1);
            Scribe_Values.Look(ref playerVote, "playerVote");
            Scribe_Collections.Look(ref resolutions, "resolutions", LookMode.Deep);
            Scribe_Collections.Look(ref anomalies, "anomalies", LookMode.Deep);
            Scribe_Collections.Look(ref allyDefense, "allyDefense", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref nextBalanceTick, "nextBalanceTick");
            Scribe_Values.Look(ref nextCouncilTick, "nextCouncilTick");
            Scribe_Values.Look(ref researchBank, "researchBank");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (civics == null) civics = new List<PolicyDef>();
                civics.RemoveAll(c => c == null);
                if (edicts == null) edicts = new List<ActiveEdict>();
                if (governors == null) governors = new List<SectorGovernor>();
                if (fleets == null) fleets = new List<PlayerFleet>();
                if (wars == null) wars = new List<WarData>();
                if (claims == null) claims = new List<EmpireClaim>();
                if (federation == null) federation = new List<int>();
                if (resolutions == null) resolutions = new List<ActiveResolution>();
                if (anomalies == null) anomalies = new List<FoundAnomaly>();
                if (allyDefense == null) allyDefense = new Dictionary<int, int>();
                anomalies.RemoveAll(a => a.def == null);
            }
        }
    }
}
