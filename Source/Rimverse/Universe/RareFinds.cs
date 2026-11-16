using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public class GameComponent_RareFinds : GameComponent
    {
        public const int RareClass = 95;
        public const float ResearchPerExactScan = 20f;
        public HashSet<string> seen = new HashSet<string>();
        public int recordsMade;

        public static GameComponent_RareFinds Instance => Current.Game?.GetComponent<GameComponent_RareFinds>();

        public GameComponent_RareFinds(Game game)
        {
        }

        public static void OnBodyScanned(StarSystemId id, int body, ScanLevel level)
        {
            GameComponent_RareFinds c = Instance;
            GameComponent_Universe u = GameComponent_Universe.Instance;
            if (c == null || u == null || Current.ProgramState != ProgramState.Playing) return;
            BodyData b = u.GetSystem(id)?.Body(body);
            if (b == null) return;
            if (level == ScanLevel.Exact)
            {
                if (Find.ResearchManager.GetProject() != null) Find.ResearchManager.AddProgress(Find.ResearchManager.GetProject(), ResearchPerExactScan * (1f + b.sizeClass / 100f));
                int records = 1 + b.sizeClass / 25 + (b.sizeClass >= RareClass ? 6 : 0);
                MakeRecords(id, records);
            }
            if (b.sizeClass >= RareClass && c.seen.Add("B:" + GameComponent_Universe.BodyKey(id, body))) Announce(b.name, b.sizeClass, id);
        }

        public static void OnSystemScanned(StarSystemId id, ScanLevel level)
        {
            GameComponent_RareFinds c = Instance;
            GameComponent_Universe u = GameComponent_Universe.Instance;
            if (c == null || u == null || level < ScanLevel.Range || Current.ProgramState != ProgramState.Playing) return;
            StarSystemData sys = u.GetSystem(id);
            if (sys == null) return;
            if (sys.sizeClass >= RareClass && c.seen.Add("S:" + id.Key)) Announce(sys.name, sys.sizeClass, id);
        }

        private static void Announce(string name, int sizeClass, StarSystemId id)
        {
            if (Find.ResearchManager.GetProject() != null) Find.ResearchManager.AddProgress(Find.ResearchManager.GetProject(), 50f * (sizeClass - RareClass + 1));
            MakeRecords(id, 10);
            Find.LetterStack.ReceiveLetter("Rimverse_RareFindLabel".Translate(), "Rimverse_RareFind".Translate(name, sizeClass), LetterDefOf.PositiveEvent);
        }

        public static void MakeRecords(StarSystemId id, int count)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail("Rimverse_StellarData");
            if (def == null || count <= 0) return;
            Map map = FleetLogic.PlayerMapIn(id.Key) ?? Find.AnyPlayerHomeMap;
            if (map == null) return;
            Thing t = ThingMaker.MakeThing(def);
            t.stackCount = Mathf.Min(count, def.stackLimit);
            IntVec3 at = DropCellFinder.TradeDropSpot(map);
            GenPlace.TryPlaceThing(t, at, map, ThingPlaceMode.Near);
            if (Instance != null) Instance.recordsMade += t.stackCount;
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref seen, "seen", LookMode.Value);
            Scribe_Values.Look(ref recordsMade, "recordsMade");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && seen == null) seen = new HashSet<string>();
        }
    }
}
