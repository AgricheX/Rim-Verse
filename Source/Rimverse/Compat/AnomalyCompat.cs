using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Rimverse
{
    public static class AnomalyCompat
    {
        private static readonly string[] Followups = { "SightstealerArrival", "ShamblerSwarm", "FrenziedAnimals", "VoidCuriosity", "UnnaturalCorpseArrival", "HarbingerTreeSpawn" };

        public static IncidentDef Find(string name) => DefDatabase<IncidentDef>.GetNamedSilentFail(name);

        public static List<IncidentDef> FollowupIncidents()
        {
            return Followups.Select(Find).Where(d => d != null).ToList();
        }

        public static bool ShipMap(Map map)
        {
            if (!ModCompat.Anomaly || map == null || !map.IsPlayerHome) return false;
            return SpaceMapUtility.IsSpace(map);
        }
    }

    public class IncidentWorker_VoiceOnRadio : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return AnomalyCompat.ShipMap(parms.target as Map) && AnomalyCompat.FollowupIncidents().Count > 0;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            List<IncidentDef> list = AnomalyCompat.FollowupIncidents();
            if (list.Count == 0) return false;
            IncidentDef next = list.RandomElement();
            IncidentParms p = StorytellerUtility.DefaultParmsNow(next.category, map);
            p.forced = true;
            int delay = Rand.RangeInclusive(2500 * 6, 2500 * 24);
            Find.Storyteller.incidentQueue.Add(next, Verse.Find.TickManager.TicksGame + delay, p);
            SendStandardLetter("Rimverse_LetterVoiceRadioLabel".Translate(), "Rimverse_LetterVoiceRadio".Translate(), LetterDefOf.ThreatSmall, parms, LookTargets.Invalid);
            return true;
        }
    }

    public class IncidentWorker_UnlistedCrewmate : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = parms.target as Map;
            IncidentDef creep = AnomalyCompat.Find("CreepJoinerJoin");
            return AnomalyCompat.ShipMap(map) && creep != null && map.mapPawns.FreeColonistsSpawnedCount >= 3;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            IncidentDef creep = AnomalyCompat.Find("CreepJoinerJoin");
            if (creep == null) return false;
            IncidentParms p = StorytellerUtility.DefaultParmsNow(creep.category, map);
            p.forced = true;
            if (!creep.Worker.TryExecute(p)) return false;
            Messages.Message("Rimverse_MsgUnlistedCrewmate".Translate(), MessageTypeDefOf.NeutralEvent, false);
            return true;
        }
    }
}
