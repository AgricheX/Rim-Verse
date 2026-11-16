using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public static class RimGramBridge
    {
        private static bool init;
        private static bool broken;
        private static MethodInfo factionPost;
        private static MethodInfo strangerPost;
        private static MethodInfo colonistPost;
        private static MethodInfo publish;
        private static MethodInfo change;
        private static PropertyInfo ready;
        private static PropertyInfo value;

        private static void Init()
        {
            if (init) return;
            init = true;
            if (!ExternalCompat.RimGram) return;
            try
            {
                Type feed = GenTypes.GetTypeInAnyAssembly("RimGram.Feed");
                Type official = GenTypes.GetTypeInAnyAssembly("RimGram.Official");
                Type rep = GenTypes.GetTypeInAnyAssembly("RimGram.Reputation");
                if (feed == null) return;
                BindingFlags f = BindingFlags.Public | BindingFlags.Static;
                factionPost = feed.GetMethod("FactionPost", f);
                strangerPost = feed.GetMethod("StrangerPost", f);
                colonistPost = feed.GetMethod("ColonistPost", f);
                ready = feed.GetProperty("Ready", f);
                publish = official?.GetMethod("Publish", f);
                change = rep?.GetMethod("Change", f);
                value = rep?.GetProperty("Value", f);
            }
            catch (Exception ex)
            {
                broken = true;
                Log.Warning("[Rimverse] RimGram bridge: " + ex.Message);
            }
        }

        public static bool Present
        {
            get
            {
                Init();
                return !broken && factionPost != null && strangerPost != null;
            }
        }

        public static bool Ready
        {
            get
            {
                if (!Present) return false;
                try
                {
                    return ready == null || (bool)ready.GetValue(null);
                }
                catch
                {
                    return false;
                }
            }
        }

        private static object[] Args(MethodInfo m, params object[] given)
        {
            ParameterInfo[] ps = m.GetParameters();
            object[] a = new object[ps.Length];
            for (int i = 0; i < ps.Length; i++)
            {
                if (i < given.Length && given[i] != null)
                {
                    a[i] = given[i];
                    continue;
                }
                Type t = ps[i].ParameterType;
                object d = ps[i].HasDefaultValue ? ps[i].DefaultValue : null;
                if (t.IsEnum) a[i] = Enum.ToObject(t, d ?? 0);
                else if (d == null && t.IsValueType) a[i] = Activator.CreateInstance(t);
                else a[i] = d;
            }
            return a;
        }

        private static bool Call(MethodInfo m, params object[] given)
        {
            if (m == null || !Ready) return false;
            try
            {
                return m.Invoke(null, Args(m, given)) != null;
            }
            catch (Exception ex)
            {
                broken = true;
                Log.Warning("[Rimverse] RimGram bridge disabled: " + (ex.InnerException ?? ex).Message);
                return false;
            }
        }

        public static bool FactionPost(Faction f, string category, float chance, Faction target = null, string extra = null, string readerThought = null)
        {
            if (f == null) return false;
            return Call(factionPost, f, category, chance, null, target, extra, readerThought, null, true);
        }

        public static bool StrangerPost(string author, string category, string extra = null) => Call(strangerPost, author, category, extra);

        public static bool ColonistPost(Pawn p, string category, float chance, Faction faction = null, string extra = null) => p != null && Call(colonistPost, p, category, chance, null, faction, extra);

        public static bool Official(string category, string text)
        {
            if (publish == null || text.NullOrEmpty()) return false;
            return Call(publish, category, text, 300, 9000);
        }

        public static int Reputation
        {
            get
            {
                if (!Present || value == null) return 0;
                try
                {
                    return (int)value.GetValue(null);
                }
                catch
                {
                    return 0;
                }
            }
        }

        public static void ChangeReputation(int delta) => Call(change, delta);
    }

    public class RimGramPending : IExposable
    {
        public int kind;
        public int civId = -1;
        public int pawnId = -1;
        public string author;
        public string category;
        public string extra;

        public void ExposeData()
        {
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref civId, "civId", -1);
            Scribe_Values.Look(ref pawnId, "pawnId", -1);
            Scribe_Values.Look(ref author, "author");
            Scribe_Values.Look(ref category, "category");
            Scribe_Values.Look(ref extra, "extra");
        }
    }

    public class GameComponent_RimGramRimverse : GameComponent
    {
        private bool baseline;
        private HashSet<int> known = new HashSet<int>();
        private HashSet<int> rumored = new HashSet<int>();
        private HashSet<int> dead = new HashSet<int>();
        private HashSet<string> civWars = new HashSet<string>();
        private HashSet<int> playerWars = new HashSet<int>();
        private HashSet<int> colonies = new HashSet<int>();
        private HashSet<string> anomalies = new HashSet<string>();
        private HashSet<string> resolutions = new HashSet<string>();
        private HashSet<string> systems = new HashSet<string>();
        private HashSet<int> traveling = new HashSet<int>();
        private HashSet<int> flow = new HashSet<int>();
        private List<RimGramPending> queue = new List<RimGramPending>();
        private int lastRep;
        private int nextChatter;

        private static readonly string[] Strangers = { "Miner", "Trader", "Pilgrim", "Hunter", "Pilot", "Smuggler" };

        public GameComponent_RimGramRimverse(Game game)
        {
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref baseline, "baseline");
            Scribe_Collections.Look(ref known, "known", LookMode.Value);
            Scribe_Collections.Look(ref rumored, "rumored", LookMode.Value);
            Scribe_Collections.Look(ref dead, "dead", LookMode.Value);
            Scribe_Collections.Look(ref civWars, "civWars", LookMode.Value);
            Scribe_Collections.Look(ref playerWars, "playerWars", LookMode.Value);
            Scribe_Collections.Look(ref colonies, "colonies", LookMode.Value);
            Scribe_Collections.Look(ref anomalies, "anomalies", LookMode.Value);
            Scribe_Collections.Look(ref resolutions, "resolutions", LookMode.Value);
            Scribe_Collections.Look(ref systems, "systems", LookMode.Value);
            Scribe_Collections.Look(ref traveling, "traveling", LookMode.Value);
            Scribe_Collections.Look(ref flow, "flow", LookMode.Value);
            Scribe_Collections.Look(ref queue, "queue", LookMode.Deep);
            Scribe_Values.Look(ref lastRep, "lastRep");
            Scribe_Values.Look(ref nextChatter, "nextChatter");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (known == null) known = new HashSet<int>();
                if (rumored == null) rumored = new HashSet<int>();
                if (dead == null) dead = new HashSet<int>();
                if (civWars == null) civWars = new HashSet<string>();
                if (playerWars == null) playerWars = new HashSet<int>();
                if (colonies == null) colonies = new HashSet<int>();
                if (anomalies == null) anomalies = new HashSet<string>();
                if (resolutions == null) resolutions = new HashSet<string>();
                if (systems == null) systems = new HashSet<string>();
                if (traveling == null) traveling = new HashSet<int>();
                if (flow == null) flow = new HashSet<int>();
                if (queue == null) queue = new List<RimGramPending>();
            }
        }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % 1250 != 77) return;
            if (!RimGramBridge.Ready) return;
            Poll(!baseline);
            baseline = true;
        }

        private static string T(string key) => ("Rimverse_RG_" + key).Translate().ToString();

        private static string Voice(Civilization c)
        {
            if (c.faction != null && c.playerState == DiploState.War) return "Pirate";
            string g = c.government?.defName ?? "";
            string i = c.ideology?.defName ?? "";
            if (g.Contains("Hive") || g.Contains("Consciousness") || g.Contains("AI") || g.Contains("Web") || g.Contains("Omega") || g.Contains("Technocracy")) return "Hive";
            if (g.Contains("Theocracy") || i.Contains("Religious")) return "Holy";
            if (g.Contains("Merchant") || g.Contains("Corporat")) return "Merchant";
            if (g.Contains("Junta") || g.Contains("OneParty")) return "Martial";
            if (c.tech >= TechLevel.Ultra && !g.Contains("Monarchy")) return "Glitter";
            if (g.Contains("Monarchy") || g.Contains("Empire")) return "Imperial";
            if (g.Contains("Republic") || g.Contains("Democracy") || g.Contains("Federation")) return "Republic";
            return "Frontier";
        }

        private bool OutOfSignal()
        {
            if (CommRelay.AnyOn()) return false;
            bool any = false;
            foreach (Map m in Find.Maps)
            {
                if (m.mapPawns.FreeColonistsSpawnedCount == 0) continue;
                MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(m);
                if (loc == null || !loc.Warping) return false;
                any = true;
            }
            return any;
        }

        private void Civ(Civilization c, string category, string extra = null, Civilization target = null)
        {
            if (c?.faction == null) return;
            Enqueue(new RimGramPending { kind = 0, civId = c.id, category = category, extra = extra, pawnId = target?.id ?? -1 });
        }

        private void Stranger(string category, string extra = null)
        {
            Enqueue(new RimGramPending { kind = 1, author = T("Stranger_" + Strangers.RandomElement()), category = category, extra = extra });
        }

        private void News(string category, string extra = null)
        {
            Enqueue(new RimGramPending { kind = 1, author = T("StarNetwork"), category = category, extra = extra });
        }

        private void Colonist(Pawn p, string category, string extra = null)
        {
            if (p == null) return;
            Enqueue(new RimGramPending { kind = 2, pawnId = p.thingIDNumber, category = category, extra = extra });
        }

        private void Enqueue(RimGramPending p)
        {
            if (OutOfSignal())
            {
                if (queue.Count < 12) queue.Add(p);
                return;
            }
            Send(p);
        }

        private static void Send(RimGramPending p)
        {
            GameComponent_Civilizations gc = GameComponent_Civilizations.Instance;
            switch (p.kind)
            {
                case 0:
                    Civilization c = gc?.Civ(p.civId);
                    if (c?.faction == null) return;
                    Civilization t = p.pawnId >= 0 ? gc.Civ(p.pawnId) : null;
                    RimGramBridge.FactionPost(c.faction, p.category, 1f, t?.faction, p.extra);
                    break;
                case 1:
                    RimGramBridge.StrangerPost(p.author, p.category, p.extra);
                    break;
                case 2:
                    Pawn pawn = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists.FirstOrDefault(x => x.thingIDNumber == p.pawnId);
                    RimGramBridge.ColonistPost(pawn, p.category, 1f, null, p.extra);
                    break;
            }
        }

        private static Pawn RandomColonist(bool inSpace)
        {
            return PawnsFinder.AllMaps_FreeColonistsSpawned.Where(p => p.ageTracker.AgeBiologicalYears >= 13 && (!inSpace || MapComponent_ShipLocation.For(p.Map) is MapComponent_ShipLocation l && !l.OnSurface)).RandomElementWithFallback();
        }

        private void Poll(bool silent)
        {
            GameComponent_Civilizations gc = GameComponent_Civilizations.Instance;
            GameComponent_PlayerColonies pc = GameComponent_PlayerColonies.Instance;
            GameComponent_PlayerEmpire e = GameComponent_PlayerEmpire.Instance;
            int now = Find.TickManager.TicksGame;

            if (!silent && queue.Count > 0 && !OutOfSignal())
            {
                RimGramBridge.Official("RvSignal", "Rimverse_RG_SignalBack".Translate(queue.Count).ToString());
                foreach (RimGramPending p in queue) Send(p);
                queue.Clear();
            }

            foreach (Map m in Find.Maps)
            {
                MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(m);
                if (loc == null || m.mapPawns.FreeColonistsSpawnedCount == 0) continue;
                bool tr = loc.Traveling;
                if (tr && traveling.Add(m.uniqueID))
                {
                    if (!silent) Colonist(RandomColonist(true), "RvLaunch", loc.Warping ? EmpireFleetLogic.SystemName(loc.destinationSystemKey) : null);
                }
                else if (!tr && traveling.Remove(m.uniqueID) && !silent) Colonist(RandomColonist(true), "RvArrive", loc.System?.name);
                if (!tr && !loc.systemKey.NullOrEmpty() && systems.Add(loc.systemKey) && !silent)
                {
                    Colonist(RandomColonist(false), "RvNewSystem", loc.System?.name);
                    if (Rand.Chance(0.5f)) Stranger("RvStrangerWelcome", loc.System?.name);
                }
            }

            if (gc != null)
            {
                foreach (Civilization c in gc.civilizations)
                {
                    if (c.dead)
                    {
                        if (dead.Add(c.id) && !silent)
                        {
                            if (c.known) News("RvCivFall", c.Name);
                            else if (c.rumored) News("RvRumorFall");
                        }
                        continue;
                    }
                    if (c.known && known.Add(c.id) && !silent)
                    {
                        Civ(c, "RvFirstContact_" + Voice(c));
                        RimGramBridge.ChangeReputation(1);
                    }
                    if (c.rumored && !c.known && rumored.Add(c.id) && !silent) Stranger("RvRumorCiv");
                    bool atWar = c.playerState == DiploState.War;
                    if (atWar && playerWars.Add(c.id) && !silent)
                    {
                        if (c.known) Civ(c, "RvWarOnPlayer");
                        News("RvPlayerWar", c.known ? c.Name : T("UnknownPower"));
                    }
                    else if (!atWar && playerWars.Remove(c.id) && !silent && c.known) Civ(c, "RvPeaceWithPlayer");
                    foreach (CivRelation r in c.relations)
                    {
                        if (r.other < c.id) continue;
                        string key = c.id + "-" + r.other;
                        Civilization o = gc.Civ(r.other);
                        if (r.state == DiploState.War)
                        {
                            if (civWars.Add(key) && !silent)
                            {
                                if (c.known && o != null && o.known)
                                {
                                    News("RvCivWar", c.Name + " × " + o.Name);
                                    Civ(c, "RvCivWarDeclare", null, o);
                                }
                                else if (c.known || o != null && o.known) News("RvRumorWar", c.known ? c.Name : o.Name);
                            }
                        }
                        else if (civWars.Remove(key) && !silent && c.known && o != null && o.known) News("RvCivPeace", c.Name + " × " + o.Name);
                    }
                }
            }

            if (pc != null)
            {
                foreach (PlayerColony col in pc.colonies)
                {
                    if (!colonies.Add(col.id) || silent) continue;
                    if (col.captured > 0) News("RvColonyTaken", col.name);
                    else Colonist(RandomColonist(false), "RvColonyFounded", col.name);
                }
            }

            if (e != null)
            {
                foreach (FoundAnomaly a in e.anomalies)
                {
                    if (a.def == null) continue;
                    string key = a.def.defName + "@" + a.systemKey + "#" + a.body;
                    if (anomalies.Add(key) && !silent) Colonist(RandomColonist(false), "RvAnomaly", a.def.label);
                    if (a.done && anomalies.Add(key + "!") && !silent) RimGramBridge.Official("RvScience", "Rimverse_RG_AnomalySolved".Translate(Faction.OfPlayer.Name, a.def.label).ToString());
                }
                foreach (ActiveResolution r in e.resolutions)
                {
                    if (r.def == null) continue;
                    if (resolutions.Add(r.def.defName + ":" + r.endTick) && !silent) News("RvCouncil", r.def.label);
                }
            }

            if (FlowUtility.Enabled)
            {
                foreach (Pawn p in PawnsFinder.AllMaps_FreeColonistsSpawned)
                {
                    if (!FlowUtility.IsSensitive(p) || !flow.Add(p.thingIDNumber) || silent) continue;
                    Colonist(p, "RvFlow_" + FlowUtility.Side(p));
                }
            }

            int rep = RimGramBridge.Reputation;
            if (gc != null && rep != lastRep && now % 60000 < 1250)
            {
                float delta = Mathf.Clamp((rep - lastRep) * 0.2f, -3f, 3f);
                foreach (Civilization c in gc.Known) c.playerOpinion = Mathf.Clamp(c.playerOpinion + delta, -100f, 100f);
                lastRep = rep;
            }

            if (silent)
            {
                nextChatter = now + 60000;
                return;
            }
            if (now >= nextChatter)
            {
                nextChatter = now + Mathf.RoundToInt(60000 * Rand.Range(1f, 2.5f));
                Chatter(gc);
            }
        }

        private void Chatter(GameComponent_Civilizations gc)
        {
            float roll = Rand.Value;
            if (roll < 0.4f)
            {
                Pawn p = RandomColonist(true);
                if (p != null) Colonist(p, "RvSpaceLife");
                else Stranger("RvStrangerChatter");
            }
            else if (roll < 0.75f && gc != null)
            {
                Civilization c = gc.Known.Where(x => x.faction != null).RandomElementWithFallback();
                if (c != null) Civ(c, "RvVoice_" + Voice(c));
                else Stranger("RvStrangerChatter");
            }
            else Stranger("RvStrangerChatter");
        }
    }
}
