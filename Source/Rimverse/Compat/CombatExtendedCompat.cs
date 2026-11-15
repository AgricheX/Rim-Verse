using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace Rimverse
{
    /// <summary>
    /// Bridge to Combat Extended. Rimverse has no compile-time reference to CE: every CE type and method
    /// is looked up by name, so the assembly loads and runs normally when CE is not installed.
    /// </summary>
    public static class CombatExtendedCompat
    {
        /// <summary>Height (in cells) ship weapons fire from. Matches what vehicle turrets use.</summary>
        public const float ShipShotHeight = 1.5f;
        private const float TargetHeight = 0.5f;

        private static bool initialized;
        private static Type projectileCEType;
        private static Type projectilePropsCEType;
        private static MethodInfo launchProjectileCE;
        private static PropertyInfo trajectoryWorkerProp;
        private static MethodInfo shotAngleMethod;
        private static MethodInfo shotRotationMethod;

        public static bool Active => ModCompat.CombatExtended && ProjectileCEType != null;

        public static Type ProjectileCEType
        {
            get
            {
                Init();
                return projectileCEType;
            }
        }

        private static void Init()
        {
            if (initialized) return;
            initialized = true;
            if (!ModCompat.CombatExtended) return;
            try
            {
                projectileCEType = AccessTools.TypeByName("CombatExtended.ProjectileCE");
                projectilePropsCEType = AccessTools.TypeByName("CombatExtended.ProjectilePropertiesCE");
                Type utility = AccessTools.TypeByName("CombatExtended.CE_Utility");
                Type worker = AccessTools.TypeByName("CombatExtended.BaseTrajectoryWorker");
                if (utility != null)
                {
                    launchProjectileCE = AccessTools.Method(utility, "LaunchProjectileCE", new[]
                    {
                        typeof(ThingDef), typeof(Vector2), typeof(LocalTargetInfo), typeof(Thing),
                        typeof(float), typeof(float), typeof(float), typeof(float)
                    });
                }
                if (projectilePropsCEType != null)
                {
                    trajectoryWorkerProp = AccessTools.Property(projectilePropsCEType, "TrajectoryWorker");
                }
                if (worker != null && projectilePropsCEType != null)
                {
                    shotAngleMethod = AccessTools.Method(worker, "ShotAngle", new[] { projectilePropsCEType, typeof(Vector3), typeof(Vector3), typeof(float?) });
                    shotRotationMethod = AccessTools.Method(worker, "ShotRotation", new[] { projectilePropsCEType, typeof(Vector3), typeof(Vector3) });
                }
                if (projectileCEType == null || launchProjectileCE == null || trajectoryWorkerProp == null || shotAngleMethod == null || shotRotationMethod == null)
                {
                    RimverseLog.Warning("Combat Extended is active but some of its projectile API was not found. Ship weapons that use CE projectiles will not fire.");
                }
            }
            catch (Exception ex)
            {
                RimverseLog.Error("Combat Extended bridge failed to initialize: " + ex);
            }
        }

        public static bool IsCEProjectile(ThingDef def)
        {
            Type t = ProjectileCEType;
            return t != null && def?.thingClass != null && t.IsAssignableFrom(def.thingClass);
        }

        /// <summary>
        /// Fires a Combat Extended projectile from <paramref name="origin"/> at <paramref name="target"/>,
        /// computing the launch angle and heading the same way CE's own verbs do.
        /// </summary>
        public static bool TryLaunch(ThingDef projectileDef, Thing launcher, IntVec3 origin, IntVec3 target)
        {
            Init();
            if (launchProjectileCE == null || trajectoryWorkerProp == null || shotAngleMethod == null || shotRotationMethod == null) return false;
            object props = projectileDef.projectile;
            if (props == null || !projectilePropsCEType.IsInstanceOfType(props))
            {
                RimverseLog.ErrorOnce(projectileDef.defName + " uses a Combat Extended projectile class but not CombatExtended.ProjectilePropertiesCE.", "RimverseCEProps" + projectileDef.defName);
                return false;
            }
            try
            {
                Vector3 source = origin.ToVector3Shifted();
                source.y = ShipShotHeight;
                Vector3 dest = target.ToVector3Shifted();
                dest.y = TargetHeight;
                float speed = projectileDef.projectile.speed;
                object worker = trajectoryWorkerProp.GetValue(props, null);
                float angle = (float)shotAngleMethod.Invoke(worker, new object[] { props, source, dest, (float?)speed });
                float rotation = (float)shotRotationMethod.Invoke(worker, new object[] { props, source, dest });
                launchProjectileCE.Invoke(null, new object[]
                {
                    projectileDef, new Vector2(source.x, source.z), new LocalTargetInfo(target), launcher,
                    angle, rotation, ShipShotHeight, speed
                });
                return true;
            }
            catch (Exception ex)
            {
                RimverseLog.ErrorOnce("Could not launch Combat Extended projectile " + projectileDef.defName + ": " + ex, "RimverseCELaunch" + projectileDef.defName);
                return false;
            }
        }
    }

    /// <summary>
    /// Hull breach risk for Combat Extended projectiles. CE projectiles do not derive from vanilla
    /// <see cref="Projectile"/>, so <see cref="Patch_Projectile_Impact_HullRisk"/> never sees them.
    /// Every Impact(Thing) override in the ProjectileCE hierarchy is patched; overrides call base.Impact,
    /// so only the outermost call on a projectile applies the rules.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_ProjectileCE_Impact_HullRisk
    {
        [ThreadStatic]
        private static int depth;

        public static bool Prepare()
        {
            return CombatExtendedCompat.Active;
        }

        public static IEnumerable<MethodBase> TargetMethods()
        {
            Type baseType = CombatExtendedCompat.ProjectileCEType;
            if (baseType == null) yield break;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (Type t in AccessTools.AllTypes())
            {
                if (t == null || !baseType.IsAssignableFrom(t)) continue;
                MethodInfo m = t.GetMethod("Impact", flags, null, new[] { typeof(Thing) }, null);
                if (m != null && !m.IsAbstract) yield return m;
            }
        }

        public static void Prefix(Thing __instance, object[] __args, ref int __state)
        {
            depth++;
            __state = depth == 1 ? HullRiskUtility.BeforeImpact(__instance.def, __args[0] as Thing) : -1;
        }

        public static void Postfix(Thing __instance, object[] __args, int __state)
        {
            if (depth == 1) HullRiskUtility.AfterImpact(__instance.def, __args[0] as Thing, __state);
        }

        public static void Finalizer()
        {
            if (depth > 0) depth--;
        }
    }
}
