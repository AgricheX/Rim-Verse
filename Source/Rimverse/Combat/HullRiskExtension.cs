using HarmonyLib;
using RimWorld;
using Verse;

namespace Rimverse
{
    public class HullRiskExtension : DefModExtension
    {
        public float breachChance;
        public bool frangible;
    }

    public static class HullRiskUtility
    {
        public static bool IsHull(Thing t)
        {
            if (t == null || !t.Spawned) return false;
            CompShipPart part = t.TryGetComp<CompShipPart>();
            if (part == null) return false;
            ShipPartRole role = part.Props.role;
            return role == ShipPartRole.HullWall || role == ShipPartRole.Window;
        }
    }

    [HarmonyPatch(typeof(Projectile), "Impact")]
    public static class Patch_Projectile_Impact_HullRisk
    {
        public static void Prefix(Projectile __instance, Thing hitThing, ref int __state)
        {
            __state = -1;
            HullRiskExtension ext = __instance.def.GetModExtension<HullRiskExtension>();
            if (ext == null || !ext.frangible || !HullRiskUtility.IsHull(hitThing)) return;
            __state = hitThing.HitPoints;
        }

        public static void Postfix(Projectile __instance, Thing hitThing, int __state)
        {
            HullRiskExtension ext = __instance.def.GetModExtension<HullRiskExtension>();
            if (ext == null || hitThing == null) return;
            if (__state > 0 && !hitThing.Destroyed)
            {
                hitThing.HitPoints = __state;
                return;
            }
            if (ext.breachChance <= 0f || !HullRiskUtility.IsHull(hitThing) || hitThing.Destroyed) return;
            if (!Rand.Chance(ext.breachChance * RimverseMod.Settings.breachDrainMultiplier)) return;
            Messages.Message("Rimverse_MsgWeaponBreach".Translate(), new TargetInfo(hitThing.Position, hitThing.Map), MessageTypeDefOf.NegativeEvent, false);
            hitThing.TakeDamage(new DamageInfo(DamageDefOf.Blunt, hitThing.HitPoints + 1));
        }
    }
}
