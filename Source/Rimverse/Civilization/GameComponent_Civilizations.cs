using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public class GameComponent_Civilizations : GameComponent
    {
        public const int MaxCivilizations = 320;
        public const double NearPlayerLy = 300.0;
        public const double RelationRangeLy = 500.0;
        public const int FleetTickInterval = 2500;
        public const int CoarseYears = 5;

        public List<Civilization> civilizations = new List<Civilization>();
        public List<Fleet> fleets = new List<Fleet>();
        private int nextCivId = 1;
        private int nextFleetId = 1;
        public bool generated;

        private readonly Dictionary<string, int> ownerCache = new Dictionary<string, int>();
        private readonly Dictionary<int, Vec3d> capitalPos = new Dictionary<int, Vec3d>();
        private readonly Dictionary<int, Civilization> byId = new Dictionary<int, Civilization>();
        private List<StarSystemId> playerSystemsCache;
        private int playerSystemsTick = -99999;

        public static GameComponent_Civilizations Instance => Current.Game?.GetComponent<GameComponent_Civilizations>();

        public GameComponent_Civilizations(Game game)
        {
        }

        public int NextCivId() => nextCivId++;

        public int NextFleetId() => nextFleetId++;

        public IEnumerable<Civilization> Alive => civilizations.Where(c => !c.dead);

        public IEnumerable<Civilization> Known => civilizations.Where(c => !c.dead && c.known);

        public Civilization Civ(int id)
        {
            if (byId.Count != civilizations.Count) RebuildIndex();
            return byId.TryGetValue(id, out Civilization c) ? c : null;
        }

        private void RebuildIndex()
        {
            byId.Clear();
            foreach (Civilization c in civilizations) byId[c.id] = c;
        }

        public void EnsureGenerated()
        {
            if (generated) return;
            generated = true;
            CivilizationGenerator.GenerateAll(this);
            RebuildIndex();
            InvalidateTerritory();
        }

        public void InvalidateTerritory()
        {
            ownerCache.Clear();
            capitalPos.Clear();
        }

        public Vec3d CapitalPosition(Civilization c)
        {
            if (capitalPos.TryGetValue(c.id, out Vec3d p)) return p;
            p = GameComponent_Universe.Instance.PositionOf(c.Capital);
            capitalPos[c.id] = p;
            return p;
        }

        public double DistanceLy(Civilization c, StarSystemId id)
        {
            if (id.galaxy != c.galaxy) return double.PositiveInfinity;
            return Vec3d.Distance(CapitalPosition(c), GameComponent_Universe.Instance.PositionOf(id));
        }

        public double DistanceLy(Civilization a, Civilization b)
        {
            if (a.galaxy != b.galaxy) return double.PositiveInfinity;
            return Vec3d.Distance(CapitalPosition(a), CapitalPosition(b));
        }

        public Civilization OwnerOf(StarSystemId id)
        {
            if (!id.Valid) return null;
            string key = id.Key;
            if (ownerCache.TryGetValue(key, out int cached)) return cached < 0 ? null : Civ(cached);
            if (ownerCache.Count > 20000) ownerCache.Clear();
            if (GameComponent_Universe.Instance.IsHome(id))
            {
                ownerCache[key] = -1;
                return null;
            }
            Civilization best = null;
            double bestScore = 0;
            Vec3d pos = GameComponent_Universe.Instance.PositionOf(id);
            foreach (Civilization c in civilizations)
            {
                if (c.dead || c.galaxy != id.galaxy) continue;
                if (c.capitalKey == key)
                {
                    best = c;
                    break;
                }
                bool planetHere = false;
                foreach (CivPlanet p in c.planets)
                {
                    if (p.systemKey == key)
                    {
                        planetHere = true;
                        break;
                    }
                }
                double d = Vec3d.Distance(CapitalPosition(c), pos);
                float r = c.InfluenceRadiusLy;
                if (!planetHere && d > r) continue;
                double score = (planetHere ? 1000.0 : 0.0) + (c.stage * 10 + c.sizeClass / 10.0) / (d + 1.0);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = c;
                }
            }
            ownerCache[key] = best?.id ?? -1;
            return best;
        }

        public List<StarSystemId> PlayerSystems()
        {
            int now = Find.TickManager.TicksGame;
            if (playerSystemsCache != null && now - playerSystemsTick < RimverseUnits.TicksPerHour) return playerSystemsCache;
            HashSet<string> keys = new HashSet<string>();
            List<StarSystemId> list = new List<StarSystemId>();
            foreach (Map m in Find.Maps)
            {
                if (!m.IsPlayerHome && !m.mapPawns.AnyColonistSpawned) continue;
                MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(m);
                if (loc == null) continue;
                StarSystemId id = loc.SystemId;
                if (id.Valid && keys.Add(id.Key)) list.Add(id);
            }
            GameComponent_Universe u = GameComponent_Universe.Instance;
            if (u != null && keys.Add(u.HomeSystemId.Key)) list.Add(u.HomeSystemId);
            playerSystemsCache = list;
            playerSystemsTick = now;
            return list;
        }

        public double DistanceToPlayer(Civilization c, out StarSystemId nearest)
        {
            double best = double.PositiveInfinity;
            nearest = StarSystemId.Invalid;
            foreach (StarSystemId id in PlayerSystems())
            {
                double d = DistanceLy(c, id);
                if (d < best)
                {
                    best = d;
                    nearest = id;
                }
            }
            return best;
        }

        public float BaseOpinion(Civilization a, Civilization b)
        {
            float o = 0f;
            if (a.ideology == b.ideology) o += 20f;
            if (a.government == b.government) o += 10f;
            if (a.xeno != null && a.xeno.xeno < 0) o -= 15f;
            if (b.xeno != null && b.xeno.xeno < 0) o -= 15f;
            if (a.xeno != null && b.xeno != null && a.xeno.xeno > 0 && b.xeno.xeno > 0) o += 10f;
            if (a.government?.profile == AiProfile.Warmonger || b.government?.profile == AiProfile.Warmonger) o -= 10f;
            UniverseRand r = new UniverseRand(UniverseRand.Hash(Math.Min(a.id, b.id), Math.Max(a.id, b.id), 77));
            o += r.Range(-20f, 20f);
            if (a.Hostile || b.Hostile) o = -80f;
            return Mathf.Clamp(o, -100f, 100f);
        }

        public void InitRelations()
        {
            List<Civilization> list = civilizations.Where(c => !c.dead).ToList();
            for (int i = 0; i < list.Count; i++)
            {
                for (int j = i + 1; j < list.Count; j++)
                {
                    Civilization a = list[i], b = list[j];
                    if (DistanceLy(a, b) > RelationRangeLy) continue;
                    float o = BaseOpinion(a, b);
                    CivRelation ra = a.RelationWith(b.id, true);
                    CivRelation rb = b.RelationWith(a.id, true);
                    ra.opinion = rb.opinion = ra.baseOpinion = rb.baseOpinion = o;
                    DiploState st = a.Hostile || b.Hostile ? DiploState.War : DiploState.Peace;
                    ra.state = rb.state = st;
                }
            }
        }

        public void SetState(Civilization a, Civilization b, DiploState state)
        {
            int now = Find.TickManager.TicksGame;
            CivRelation ra = a.RelationWith(b.id, true);
            CivRelation rb = b.RelationWith(a.id, true);
            ra.state = rb.state = state;
            ra.sinceTick = rb.sinceTick = now;
        }

        public void SetPlayerState(Civilization c, DiploState state)
        {
            c.playerState = state;
            c.playerStateSince = Find.TickManager.TicksGame;
            if (c.faction != null && !c.NoDiplomacy && !c.faction.def.permanentEnemy)
            {
                FactionRelationKind kind = state == DiploState.War ? FactionRelationKind.Hostile : state >= DiploState.Alliance ? FactionRelationKind.Ally : FactionRelationKind.Neutral;
                if (c.faction.RelationKindWith(Faction.OfPlayer) != kind) c.faction.SetRelationDirect(Faction.OfPlayer, kind, false);
            }
        }

        public void ChangePlayerOpinion(Civilization c, float delta)
        {
            c.playerOpinion = Mathf.Clamp(c.playerOpinion + delta, -100f, 100f);
            if (c.faction != null && !c.NoDiplomacy && !c.faction.def.permanentEnemy)
            {
                int target = Mathf.RoundToInt(c.playerOpinion);
                int cur = c.faction.GoodwillWith(Faction.OfPlayer);
                if (target != cur) c.faction.TryAffectGoodwillWith(Faction.OfPlayer, target - cur, false, false);
            }
        }

        public Faction EnsureFaction(Civilization c)
        {
            if (c.faction != null && Find.FactionManager.AllFactionsListForReading.Contains(c.faction)) return c.faction;
            if (c.lore == LoreKind.Mechanoids)
            {
                c.faction = Faction.OfMechanoids;
                return c.faction;
            }
            if (RoyaltyCompat.TryLinkEmpire(c))
            {
                FactionRelationKind rel = c.faction.RelationKindWith(Faction.OfPlayer);
                if (rel == FactionRelationKind.Hostile) c.playerState = DiploState.War;
                else if (rel == FactionRelationKind.Ally && c.playerState < DiploState.Alliance) c.playerState = DiploState.Alliance;
                else if (rel == FactionRelationKind.Neutral && c.playerState == DiploState.War) c.playerState = DiploState.Truce;
                c.playerStateSince = Find.TickManager.TicksGame;
                return c.faction;
            }
            if (CompatActions.TryLinkForeignFaction(c))
            {
                SetPlayerState(c, c.faction.HostileTo(Faction.OfPlayer) ? DiploState.War : c.playerState);
                return c.faction;
            }
            FactionDef def = CivPreSpace.FactionFor(c) ?? (c.Hostile ? RimverseDefOf.Rimverse_SpacePirates : RimverseDefOf.Rimverse_SpaceCiv);
            Faction f = FactionGenerator.NewGeneratedFaction(new FactionGeneratorParms(def));
            f.Name = c.Name;
            f.color = c.color;
            Find.FactionManager.Add(f);
            c.faction = f;
            CivIdeo.Apply(c, f);
            ChangePlayerOpinion(c, 0f);
            SetPlayerState(c, c.playerState);
            return f;
        }

        public void MakeContact(Civilization c, bool letter)
        {
            if (c == null || c.known || c.dead) return;
            c.known = true;
            c.rumored = true;
            c.AddIntel(Mathf.Max(0f, 20f - c.intel));
            EnsureFaction(c);
            if (letter)
            {
                Find.LetterStack.ReceiveLetter("Rimverse_LetterFirstContactLabel".Translate(c.Name), "Rimverse_LetterFirstContact".Translate(c.Name, ("Rimverse_Stage_" + c.stage).Translate(), c.government?.LabelCap ?? "?", c.ideology?.LabelCap ?? "?", c.tech.ToStringHuman()), c.Hostile ? LetterDefOf.ThreatSmall : LetterDefOf.PositiveEvent);
            }
        }

        public void Rumor(Civilization c, bool letter)
        {
            if (c == null || c.dead || c.known || c.rumored) return;
            c.rumored = true;
            if (letter && c.lore != LoreKind.Mechanoids)
            {
                Find.LetterStack.ReceiveLetter("Rimverse_LetterRumorLabel".Translate(), "Rimverse_LetterRumor".Translate(RumorDirection(c)), LetterDefOf.NeutralEvent);
            }
        }

        public string RumorDirection(Civilization c)
        {
            double d = DistanceToPlayer(c, out _);
            if (d < 50.0) return "Rimverse_RumorVeryClose".Translate();
            if (d < 150.0) return "Rimverse_RumorClose".Translate();
            return "Rimverse_RumorFar".Translate();
        }

        public void CheckContacts()
        {
            GameComponent_Universe u = GameComponent_Universe.Instance;
            if (u == null) return;
            float days = Mathf.Max(RimverseUnits.TicksPerHour, RimverseMod.Settings.civilizationTickInterval) / (float)RimverseUnits.TicksPerDay;
            HashSet<int> present = new HashSet<int>();
            foreach (StarSystemId id in PlayerSystems())
            {
                Civilization owner = OwnerOf(id);
                if (owner == null) continue;
                present.Add(owner.id);
                if (!owner.known) MakeContact(owner, true);
            }
            foreach (Civilization c in civilizations)
            {
                if (c.dead) continue;
                ScanLevel scan = u.GetSystemScan(c.Capital);
                if (!c.known)
                {
                    if (scan >= ScanLevel.Exact) MakeContact(c, true);
                    else if (scan >= ScanLevel.Range || (IsNearPlayer(c) && Rand.Chance(0.02f * days))) Rumor(c, true);
                    continue;
                }
                float gain = 0.05f;
                if (present.Contains(c.id)) gain += 1f;
                if (scan >= ScanLevel.Exact) gain += 0.3f;
                if (c.playerState >= DiploState.TradeAgreement) gain += 0.4f;
                if (c.playerState >= DiploState.Alliance) gain += 0.8f;
                if (IsNearPlayer(c)) gain += 0.15f;
                c.AddIntel(gain * days);
            }
        }

        public bool IsNearPlayer(Civilization c) => DistanceToPlayer(c, out _) <= NearPlayerLy + c.InfluenceRadiusLy;

        public override void GameComponentTick()
        {
            RimverseSettings s = RimverseMod.Settings;
            if (s == null || !s.civilizationSimulation) return;
            int tick = Find.TickManager.TicksGame;
            if (tick % FleetTickInterval == 123)
            {
                if (!generated) EnsureGenerated();
                FleetLogic.Tick(this);
            }
            int interval = Mathf.Max(RimverseUnits.TicksPerHour, s.civilizationTickInterval);
            if (tick % interval != 71) return;
            EnsureGenerated();
            CheckContacts();
            HashSet<int> playerGalaxies = new HashSet<int>();
            foreach (StarSystemId pid in PlayerSystems()) playerGalaxies.Add(pid.galaxy);
            if (playerGalaxies.Count == 0) playerGalaxies.Add(0);
            for (int i = 0; i < civilizations.Count; i++)
            {
                Civilization c = civilizations[i];
                if (c.dead) continue;
                int step = !playerGalaxies.Contains(c.galaxy) ? RimverseUnits.TicksPerYear * CoarseYears : IsNearPlayer(c) ? RimverseUnits.TicksPerQuadrum : RimverseUnits.TicksPerYear;
                int elapsed = tick - c.lastSimTick;
                if (elapsed < step) continue;
                int guard = 0;
                while (elapsed >= step && guard++ < 50)
                {
                    float years = Mathf.Min(elapsed, step) / (float)RimverseUnits.TicksPerYear;
                    CivSimulation.Step(this, c, years);
                    int used = Mathf.Min(elapsed, RimverseUnits.TicksPerYear);
                    c.lastSimTick += used;
                    elapsed -= used;
                }
                c.lastSimTick = tick;
            }
        }

        public override void FinalizeInit()
        {
            RebuildIndex();
            InvalidateTerritory();
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref civilizations, "civilizations", LookMode.Deep);
            Scribe_Collections.Look(ref fleets, "fleets", LookMode.Deep);
            Scribe_Values.Look(ref nextCivId, "nextCivId", 1);
            Scribe_Values.Look(ref nextFleetId, "nextFleetId", 1);
            Scribe_Values.Look(ref generated, "generated");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (civilizations == null) civilizations = new List<Civilization>();
                if (fleets == null) fleets = new List<Fleet>();
                RebuildIndex();
                InvalidateTerritory();
            }
        }
    }
}
