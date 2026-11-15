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

        /// <summary>Call before a projectile hits. Returns the hull's hit points to restore for frangible rounds, or -1.</summary>
        public static int BeforeImpact(ThingDef projectileDef, Thing hitThing)
        {
            HullRiskExtension ext = projectileDef?.GetModExtension<HullRiskExtension>();
            if (ext == null || !ext.frangible || !IsHull(hitThing)) return -1;
            return hitThing.HitPoints;
        }

        /// <summary>Call after a projectile hit: frangible rounds leave the hull intact, others may breach it.</summary>
        public static void AfterImpact(ThingDef projectileDef, Thing hitThing, int state)
        {
            HullRiskExtension ext = projectileDef?.GetModExtension<HullRiskExtension>();
            if (ext == null || hitThing == null) return;
            if (state > 0 && !hitThing.Destroyed)
            {
                hitThing.HitPoints = state;
                return;
            }
            if (ext.breachChance <= 0f || !IsHull(hitThing) || hitThing.Destroyed) return;
            if (!Rand.Chance(ext.breachChance * RimverseMod.Settings.breachDrainMultiplier)) return;
            Messages.Message("Rimverse_MsgWeaponBreach".Translate(), new TargetInfo(hitThing.Position, hitThing.Map), MessageTypeDefOf.NegativeEvent, false);
            hitThing.TakeDamage(new DamageInfo(DamageDefOf.Blunt, hitThing.HitPoints + 1));
        }
    }

    [HarmonyPatch(typeof(Projectile), "Impact")]
    public static class Patch_Projectile_Impact_HullRisk
    {
        public static void Prefix(Projectile __instance, Thing hitThing, ref int __state)
        {
            __state = HullRiskUtility.BeforeImpact(__instance.def, hitThing);
        }

        public static void Postfix(Projectile __instance, Thing hitThing, int __state)
        {
            HullRiskUtility.AfterImpact(__instance.def, hitThing, __state);
        }
    }
}
