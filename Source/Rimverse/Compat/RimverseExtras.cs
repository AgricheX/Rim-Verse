using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace Rimverse
{
    public class GameComponent_RimverseExtras : GameComponent
    {
        public Dictionary<int, int> merit = new Dictionary<int, int>();
        public Dictionary<int, int> jamUntil = new Dictionary<int, int>();
        public int escortUntil = -1;
        public Pawn noble;
        public int nobleLeaveTick = -1;
        public int defenseMapId = -1;
        public int defenseUntil = -1;
        public List<Pawn> deserters = new List<Pawn>();
        public Dictionary<string, int> counters = new Dictionary<string, int>();

        public static GameComponent_RimverseExtras Instance => Current.Game?.GetComponent<GameComponent_RimverseExtras>();

        public GameComponent_RimverseExtras(Game game)
        {
        }

        public static bool EscortActive => Instance != null && Find.TickManager.TicksGame < Instance.escortUntil;

        public static float ExtraJam(RimverseShip ship)
        {
            GameComponent_RimverseExtras x = Instance;
            if (x == null || ship == null) return 0f;
            return x.jamUntil.TryGetValue(ship.id, out int until) && Find.TickManager.TicksGame < until ? 0.3f : 0f;
        }

        public int Count(string key) => counters.TryGetValue(key, out int v) ? v : 0;

        public void Add(string key, int amount = 1) => counters[key] = Count(key) + amount;

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (now % 250 != 0) return;
            if (noble != null && nobleLeaveTick > 0)
            {
                if (noble.Dead || noble.Downed || noble.Destroyed)
                {
                    RoyalQuests.NobleFailed(noble);
                    noble = null;
                    nobleLeaveTick = -1;
                }
                else if (now >= nobleLeaveTick)
                {
                    RoyalQuests.NobleDone(noble);
                    noble = null;
                    nobleLeaveTick = -1;
                }
            }
            if (deserters.Count > 0)
            {
                deserters.RemoveAll(p => p == null);
                if (deserters.All(p => p.Dead || p.Downed || !p.Spawned || p.Faction == Faction.OfPlayer))
                {
                    deserters.Clear();
                    RoyalQuests.DesertersDone();
                }
            }
            if (defenseMapId >= 0 && now > defenseUntil)
            {
                defenseMapId = -1;
            }
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref merit, "merit", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref jamUntil, "jamUntil", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref counters, "counters", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref escortUntil, "escortUntil", -1);
            Scribe_References.Look(ref noble, "noble");
            Scribe_Values.Look(ref nobleLeaveTick, "nobleLeaveTick", -1);
            Scribe_Values.Look(ref defenseMapId, "defenseMapId", -1);
            Scribe_Values.Look(ref defenseUntil, "defenseUntil", -1);
            Scribe_Collections.Look(ref deserters, "deserters", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (merit == null) merit = new Dictionary<int, int>();
                if (jamUntil == null) jamUntil = new Dictionary<int, int>();
                if (counters == null) counters = new Dictionary<string, int>();
                if (deserters == null) deserters = new List<Pawn>();
            }
        }
    }

    public static class RimverseEvents
    {
        public static void OnShipVictory(Map map, RimverseShip player, bool captured = false)
        {
            IdeoEvents.OnShipVictory(map, captured);
            NavalRanks.OnVictory(map, player);
            RoyalQuests.OnVictory(map);
            GameComponent_RimverseExtras.Instance?.Add("shipVictories");
        }
    }

    public static class NavalRanks
    {
        public static readonly int[] Thresholds = { 1, 3, 6, 10, 15, 25 };

        public static int Merit(Pawn p) => p != null && GameComponent_RimverseExtras.Instance != null && GameComponent_RimverseExtras.Instance.merit.TryGetValue(p.thingIDNumber, out int v) ? v : 0;

        public static int RankOf(int merit)
        {
            int r = 0;
            for (int i = 0; i < Thresholds.Length; i++) if (merit >= Thresholds[i]) r = i + 1;
            return r;
        }

        public static int Rank(Pawn p) => RankOf(Merit(p));

        public static string RankLabel(Pawn p)
        {
            int r = Rank(p);
            return r <= 0 ? null : ("Rimverse_NavalRank_" + r).Translate().ToString();
        }

        public static void OnVictory(Map map, RimverseShip player)
        {
            GameComponent_RimverseExtras x = GameComponent_RimverseExtras.Instance;
            if (x == null || map == null) return;
            ThoughtDef promoted = DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimverse_NavalPromotion");
            foreach (Pawn p in map.mapPawns.FreeColonistsSpawned.ToList())
            {
                int before = RankOf(Merit(p));
                x.merit[p.thingIDNumber] = Merit(p) + 1;
                int after = RankOf(Merit(p));
                if (after <= before) continue;
                if (promoted != null) p.needs?.mood?.thoughts?.memories?.TryGainMemory(promoted);
                Faction empire = ModCompat.Royalty ? Faction.OfEmpire : null;
                if (empire != null && p.royalty != null && !empire.HostileTo(Faction.OfPlayer)) p.royalty.GainFavor(empire, after);
                Find.LetterStack.ReceiveLetter("Rimverse_LetterPromotionLabel".Translate(), "Rimverse_LetterPromotion".Translate(p.LabelShort, ("Rimverse_NavalRank_" + after).Translate()), LetterDefOf.PositiveEvent, p);
            }
        }
    }

    public static class RoyalQuests
    {
        public static Faction Empire => ModCompat.Royalty ? Faction.OfEmpire : null;

        public static bool EmpireFriendly => Empire != null && !Empire.defeated && !Empire.HostileTo(Faction.OfPlayer);

        public static Pawn FavorTarget(Map map)
        {
            IEnumerable<Pawn> pool = map != null ? map.mapPawns.FreeColonistsSpawned : PawnsFinder.AllMaps_FreeColonists;
            Pawn best = pool.Where(p => p.royalty != null).OrderByDescending(p => p.royalty.MostSeniorTitle?.def.seniority ?? -1).FirstOrDefault();
            return best ?? PawnsFinder.AllMaps_FreeColonists.FirstOrDefault(p => p.royalty != null);
        }

        public static void Reward(Map map, int favor, int goodwill, string letterKey)
        {
            Faction e = Empire;
            if (e == null) return;
            Pawn p = FavorTarget(map);
            if (p != null && favor > 0) p.royalty.GainFavor(e, favor);
            if (goodwill != 0) e.TryAffectGoodwillWith(Faction.OfPlayer, goodwill, false, true);
            Find.LetterStack.ReceiveLetter((letterKey + "Label").Translate(), letterKey.Translate(p?.LabelShort ?? "?", favor), LetterDefOf.PositiveEvent, p);
        }

        public static void NobleDone(Pawn noble)
        {
            Map map = noble.MapHeld;
            if (noble.Spawned)
            {
                noble.GetLord()?.RemovePawn(noble);
                noble.DeSpawn();
            }
            if (!noble.Destroyed) noble.Destroy();
            Reward(map, 6, 10, "Rimverse_LetterNobleDone");
        }

        public static void NobleFailed(Pawn noble)
        {
            Empire?.TryAffectGoodwillWith(Faction.OfPlayer, -20, false, true);
            Find.LetterStack.ReceiveLetter("Rimverse_LetterNobleFailedLabel".Translate(), "Rimverse_LetterNobleFailed".Translate(noble.LabelShort), LetterDefOf.NegativeEvent);
        }

        public static void DesertersDone()
        {
            Reward(null, 8, 5, "Rimverse_LetterDesertersDone");
        }

        public static void OnVictory(Map map)
        {
            GameComponent_RimverseExtras x = GameComponent_RimverseExtras.Instance;
            if (x == null || map == null || x.defenseMapId != map.uniqueID) return;
            x.defenseMapId = -1;
            Reward(map, 10, 15, "Rimverse_LetterStellarchSaved");
        }
    }
}
