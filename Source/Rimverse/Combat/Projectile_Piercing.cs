using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public class Projectile_Piercing : Bullet
    {
        public const int MaxPierce = 3;
        public const float Falloff = 0.6f;
        public int pierced;
        public float factor = 1f;

        public override int DamageAmount => Mathf.Max(1, Mathf.RoundToInt(base.DamageAmount * factor));

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Map map = Map;
            IntVec3 cell = Position;
            Vector3 dir = (destination - origin).Yto0();
            Thing shooter = launcher;
            Thing gear = equipment;
            base.Impact(hitThing, blockedByShield);
            if (blockedByShield || hitThing == null || pierced >= MaxPierce || map == null || dir.sqrMagnitude < 0.01f) return;
            if (hitThing is Pawn && Rand.Chance(0.5f)) return;
            dir.Normalize();
            Vector3 start = cell.ToVector3Shifted() + dir * 1.1f;
            IntVec3 next = (cell.ToVector3Shifted() + dir * 14f).ToIntVec3();
            next = next.ClampInsideMap(map);
            IntVec3 startCell = start.ToIntVec3();
            if (!startCell.InBounds(map) || next == startCell) return;
            Projectile_Piercing p = (Projectile_Piercing)GenSpawn.Spawn(def, startCell, map);
            p.pierced = pierced + 1;
            p.factor = factor * Falloff;
            p.Launch(shooter, start, new LocalTargetInfo(next), new LocalTargetInfo(next), ProjectileHitFlags.All, false, gear);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref pierced, "pierced");
            Scribe_Values.Look(ref factor, "factor", 1f);
        }
    }
}
