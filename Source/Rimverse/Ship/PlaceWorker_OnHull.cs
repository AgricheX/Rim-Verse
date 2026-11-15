using Verse;

namespace Rimverse
{
    public class PlaceWorker_OnHull : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            IntVec2 size = checkingDef.Size;
            foreach (IntVec3 c in GenAdj.OccupiedRect(loc, rot, size))
            {
                if (!c.InBounds(map) || !HullUtility.IsHull(map, c))
                {
                    return "Rimverse_MustBeOnHull".Translate();
                }
            }
            return true;
        }
    }
}
