using System.Linq;
using RimWorld;
using Verse;

namespace Rimverse
{
    public static class SpaceLifeUtility
    {
        public static bool InSpace(Pawn p)
        {
            if (p == null || !p.Spawned || p.Map == null) return false;
            if (SpaceMapUtility.IsSpace(p.Map)) return true;
            return HullUtility.IsHull(p.Map, p.Position);
        }

        public static bool OnGround(Pawn p)
        {
            if (p == null || !p.Spawned || p.Map == null) return false;
            return !InSpace(p);
        }
    }

    public class ThoughtWorker_Precept_InSpace : ThoughtWorker_Precept
    {
        protected override ThoughtState ShouldHaveThought(Pawn p) => SpaceLifeUtility.InSpace(p);
    }

    public class ThoughtWorker_Precept_OnGround : ThoughtWorker_Precept
    {
        protected override ThoughtState ShouldHaveThought(Pawn p) => SpaceLifeUtility.OnGround(p);
    }
}

namespace Rimverse
{
    public static class IdeoEvents
    {
        public static void Give(Map map, string precept, string thought)
        {
            if (!ModCompat.Ideology || map == null) return;
            PreceptDef pd = DefDatabase<PreceptDef>.GetNamedSilentFail(precept);
            ThoughtDef td = DefDatabase<ThoughtDef>.GetNamedSilentFail(thought);
            if (pd == null || td == null) return;
            foreach (Pawn p in map.mapPawns.FreeColonistsSpawned)
            {
                if (p.Ideo != null && p.Ideo.HasPrecept(pd)) p.needs?.mood?.thoughts?.memories?.TryGainMemory(td);
            }
        }

        public static void OnGateUsed(Map map)
        {
            Give(map, "Rimverse_GateUse_Sacred", "Rimverse_GateUse_Pilgrimage");
            Give(map, "Rimverse_GateUse_Feared", "Rimverse_GateUse_Dread");
        }

        public static void OnShipVictory(Map map, bool captured)
        {
            Give(map, "Rimverse_Salvage_Honorable", "Rimverse_Salvage_Spoils");
            Give(map, "Rimverse_Salvage_Disrespectful", "Rimverse_Salvage_Desecration");
            if (captured)
            {
                Give(map, "Rimverse_Piracy_Honorable", "Rimverse_Piracy_Prize");
                Give(map, "Rimverse_Piracy_Abhorrent", "Rimverse_Piracy_Shame");
            }
        }

        public static int DaysSinceNewScan()
        {
            GameComponent_RimverseExtras x = GameComponent_RimverseExtras.Instance;
            if (x == null) return 0;
            int last = x.Count("lastNewScanTick");
            return (Find.TickManager.TicksGame - last) / GenDate.TicksPerDay;
        }

        public static void NoteNewScan()
        {
            GameComponent_RimverseExtras x = GameComponent_RimverseExtras.Instance;
            if (x != null && Current.ProgramState == ProgramState.Playing) x.counters["lastNewScanTick"] = Find.TickManager.TicksGame;
        }
    }

    public class ThoughtWorker_Precept_ExplorationStagnant : ThoughtWorker_Precept
    {
        protected override ThoughtState ShouldHaveThought(Pawn p) => IdeoEvents.DaysSinceNewScan() > 15;
    }

    public class ThoughtWorker_Precept_ExploredRecently : ThoughtWorker_Precept
    {
        protected override ThoughtState ShouldHaveThought(Pawn p) => IdeoEvents.DaysSinceNewScan() <= 5;
    }

    public class ThoughtWorker_Precept_ManyContacts : ThoughtWorker_Precept
    {
        protected override ThoughtState ShouldHaveThought(Pawn p)
        {
            GameComponent_Civilizations gc = GameComponent_Civilizations.Instance;
            return gc?.civilizations != null && gc.civilizations.Count(c => c.known && !c.dead) >= 3;
        }
    }

    public class ThoughtWorker_Precept_ForeignTies : ThoughtWorker_Precept
    {
        protected override ThoughtState ShouldHaveThought(Pawn p)
        {
            GameComponent_Civilizations gc = GameComponent_Civilizations.Instance;
            return gc?.civilizations != null && gc.civilizations.Any(c => c.known && !c.dead && c.playerState >= DiploState.TradeAgreement);
        }
    }

    public class ThoughtWorker_Precept_CryoInUse : ThoughtWorker_Precept
    {
        protected override ThoughtState ShouldHaveThought(Pawn p)
        {
            if (p.Map == null) return false;
            return p.Map.listerBuildings.allBuildingsColonist.OfType<Building_CryptosleepCasket>().Any(c => c.HasAnyContents);
        }
    }

    public class ThoughtWorker_Precept_HasCryo : ThoughtWorker_Precept
    {
        protected override ThoughtState ShouldHaveThought(Pawn p)
        {
            if (p.Map == null || !SpaceMapUtility.IsSpace(p.Map)) return false;
            return p.Map.listerBuildings.allBuildingsColonist.OfType<Building_CryptosleepCasket>().Any();
        }
    }

    public class ThoughtWorker_Precept_SelfLineage : ThoughtWorker_Precept
    {
        protected override ThoughtState ShouldHaveThought(Pawn p) => LineageUtility.LineageHediff(p) != null || (p.genes != null && p.genes.GenesListForReading.Any(g => g.def.defName.StartsWith("Rimverse_Gene_")));
    }

    public class ThoughtWorker_Precept_LineageAround : ThoughtWorker_Precept
    {
        protected override ThoughtState ShouldHaveThought(Pawn p)
        {
            if (p.Map == null) return false;
            return p.Map.mapPawns.FreeColonistsSpawned.Any(o => o != p && LineageUtility.LineageHediff(o) != null);
        }
    }

    public class ThoughtWorker_Precept_ShipUnnamed : ThoughtWorker_Precept
    {
        protected override ThoughtState ShouldHaveThought(Pawn p)
        {
            RimverseShip s = ColonyUtility.PlayerShipOn(p.Map);
            return s != null && s.state != null && s.state.name.NullOrEmpty();
        }
    }

    public class ThoughtWorker_Precept_ShipNamed : ThoughtWorker_Precept
    {
        protected override ThoughtState ShouldHaveThought(Pawn p)
        {
            RimverseShip s = ColonyUtility.PlayerShipOn(p.Map);
            return s != null && s.state != null && !s.state.name.NullOrEmpty();
        }
    }

    public class ThoughtWorker_Precept_NoViewport : ThoughtWorker_Precept
    {
        protected override ThoughtState ShouldHaveThought(Pawn p)
        {
            Map m = p.Map;
            if (m == null || !SpaceMapUtility.IsSpace(m)) return false;
            return !m.listerBuildings.allBuildingsColonist.Any(b => b.def.defName == "Rimverse_HullViewport" || b.def.defName == "Rimverse_StarWindow");
        }
    }

    public class ThoughtWorker_Precept_HasViewport : ThoughtWorker_Precept
    {
        protected override ThoughtState ShouldHaveThought(Pawn p)
        {
            Map m = p.Map;
            if (m == null || !SpaceMapUtility.IsSpace(m)) return false;
            return m.listerBuildings.allBuildingsColonist.Any(b => b.def.defName == "Rimverse_HullViewport" || b.def.defName == "Rimverse_StarWindow");
        }
    }
}
