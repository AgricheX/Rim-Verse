using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Rimverse
{
    public static class PrisonerTransfer
    {
        public static bool HasShip(Map map) => map?.GetComponent<MapComponent_RimverseShips>()?.Ships.Count > 0;

        public static bool CanMove(Pawn p) => p != null && p.Spawned && p.IsPrisonerOfColony && !p.Dead && HasShip(p.Map);

        public static IEnumerable<Map> Targets(Map from)
        {
            MapComponent_ShipLocation a = MapComponent_ShipLocation.For(from);
            if (a == null || a.Traveling) yield break;
            foreach (Map m in Find.Maps)
            {
                if (m == from || !m.IsPlayerHome || !HasShip(m)) continue;
                MapComponent_ShipLocation b = MapComponent_ShipLocation.For(m);
                if (b == null || b.Traveling || b.systemKey != a.systemKey) continue;
                yield return m;
            }
        }

        public static void Move(Pawn p, Map to)
        {
            IntVec3 cell = IntVec3.Invalid;
            Building_Bed bed = to.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>().FirstOrDefault(b => b.ForPrisoners && b.AnyUnownedSleepingSlot);
            if (bed != null) cell = bed.Position;
            if (!cell.IsValid)
            {
                RimverseShip ship = to.GetComponent<MapComponent_RimverseShips>().Ships[0];
                IntVec3 near = ship.bridge != null && ship.bridge.Spawned ? ship.bridge.Position : ship.cells.First();
                if (!CellFinder.TryFindRandomReachableNearbyCell(near, to, 8, TraverseParms.For(TraverseMode.PassDoors), c => c.Standable(to), null, out cell)) cell = near;
            }
            if (p.Spawned) p.DeSpawn();
            GenSpawn.Spawn(p, cell, to);
            p.jobs?.StopAll();
            if (bed != null) p.ownership?.ClaimBedIfNonMedical(bed);
            Messages.Message("Rimverse_MsgPrisonerMoved".Translate(p.LabelShort, to.Parent?.LabelCap ?? to.ToString()), p, MessageTypeDefOf.NeutralEvent, false);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    public static class Patch_PrisonerTransferGizmo
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> values, Pawn __instance)
        {
            foreach (Gizmo g in values) yield return g;
            if (!PrisonerTransfer.CanMove(__instance)) yield break;
            List<Map> targets = PrisonerTransfer.Targets(__instance.Map).ToList();
            Pawn p = __instance;
            Command_Action cmd = new Command_Action
            {
                defaultLabel = "Rimverse_CmdMovePrisoner".Translate(),
                defaultDesc = "Rimverse_CmdMovePrisonerDesc".Translate(),
                icon = RimverseTex.Board,
                action = delegate
                {
                    List<FloatMenuOption> opts = targets.Select(m => new FloatMenuOption(m.Parent?.LabelCap ?? m.ToString(), () => PrisonerTransfer.Move(p, m))).ToList();
                    Find.WindowStack.Add(new FloatMenu(opts));
                }
            };
            if (targets.Count == 0) cmd.Disable("Rimverse_CmdMovePrisonerNone".Translate());
            yield return cmd;
        }
    }
}
