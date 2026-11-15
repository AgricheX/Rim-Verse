using RimWorld;
using Verse;

namespace Rimverse
{
    public class ThoughtWorker_FearOfSky : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (!p.Spawned || p.Map == null) return ThoughtState.Inactive;
            if (SpaceMapUtility.IsSpace(p.Map) || SpaceMapUtility.IsHostileSurface(p.Map)) return ThoughtState.Inactive;
            if (p.Position.Roofed(p.Map)) return ThoughtState.Inactive;
            LineageExtension ext = LineageUtility.ExtensionOf(p);
            return ext != null && ext.fearOfSky ? ThoughtState.ActiveAtStage(0) : ThoughtState.Inactive;
        }
    }
}
