using System.Collections.Generic;
using System.Text;
using RimWorld;
using Verse;

namespace Rimverse
{
    public class CompProperties_SensorVisor : CompProperties
    {
        public CompProperties_SensorVisor()
        {
            compClass = typeof(CompSensorVisor);
        }
    }

    public class CompSensorVisor : ThingComp
    {
        public override IEnumerable<Gizmo> CompGetWornGizmosExtra()
        {
            foreach (Gizmo g in base.CompGetWornGizmosExtra()) yield return g;
            if (!(parent is Apparel a) || a.Wearer == null || a.Wearer.Faction != Faction.OfPlayer) yield break;
            Pawn wearer = a.Wearer;
            yield return new Command_Action
            {
                defaultLabel = "Rimverse_SensorVisorScan".Translate(),
                defaultDesc = "Rimverse_SensorVisorScanDesc".Translate(),
                icon = parent.def.uiIcon,
                action = delegate { Find.WindowStack.Add(new Dialog_MessageBox(Report(wearer.MapHeld))); }
            };
        }

        public static StarSystemData SystemOf(Map map)
        {
            MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(map);
            GameComponent_Universe u = GameComponent_Universe.Instance;
            if (u == null) return null;
            if (loc != null && !loc.systemKey.NullOrEmpty()) return loc.System;
            return u.HomeSystem;
        }

        public static string Report(Map map)
        {
            StarSystemData sys = SystemOf(map);
            if (sys == null) return "Rimverse_SensorVisorNothing".Translate();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Rimverse_SensorVisorSystem".Translate(sys.name, sys.sizeClass));
            MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(map);
            int here = loc?.bodyIndex ?? -1;
            foreach (BodyData b in sys.bodies)
            {
                string line = "Rimverse_SensorVisorBody".Translate(b.name, ("Rimverse_BodyType_" + b.type).Translate(), b.sizeClass);
                if (b.index == here) line += " " + "Rimverse_SensorVisorHere".Translate();
                sb.AppendLine(line);
            }
            return sb.ToString().TrimEndNewlines();
        }
    }
}
