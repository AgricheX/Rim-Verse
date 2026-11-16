using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public class CompProperties_EnergyCell : CompProperties
    {
        public int shots = 30;
        public float heatPerShot;
        public float heatMax = 100f;

        public CompProperties_EnergyCell()
        {
            compClass = typeof(CompEnergyCell);
        }
    }

    public class CompEnergyCell : ThingComp
    {
        public float charge = -1f;
        public float heat;
        public bool venting;

        public CompProperties_EnergyCell Props => (CompProperties_EnergyCell)props;

        public static bool Enabled => !CombatExtendedCompat.Active;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (charge < 0f) charge = Props.shots;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref charge, "rvCharge", -1f);
            Scribe_Values.Look(ref heat, "rvHeat");
            Scribe_Values.Look(ref venting, "rvVenting");
        }

        public bool CanFire => charge != 0f && !venting;

        public void Fired()
        {
            if (charge < 0f) charge = Props.shots;
            charge = Mathf.Max(0f, charge - 1f);
            if (Props.heatPerShot <= 0f) return;
            heat += Props.heatPerShot;
            if (heat >= Props.heatMax)
            {
                venting = true;
                Pawn p = Holder;
                if (p != null && p.Faction == Faction.OfPlayer) Messages.Message("Rimverse_WeaponOverheat".Translate(parent.LabelShort, p.LabelShort), p, MessageTypeDefOf.NeutralEvent, false);
            }
        }

        public Pawn Holder => (parent.ParentHolder as Pawn_EquipmentTracker)?.pawn;

        public void Tick250()
        {
            if (charge < 0f) charge = Props.shots;
            heat = Mathf.Max(0f, heat - Props.heatMax * 0.08f);
            if (venting && heat <= Props.heatMax * 0.25f) venting = false;
            if (charge >= Props.shots) return;
            Thing at = (Thing)Holder ?? parent;
            if (!at.Spawned) return;
            Map map = at.Map;
            bool powered = GenRadial.RadialDistinctThingsAround(at.Position, map, 3f, true).Any(t => t.TryGetComp<CompPowerTrader>() is CompPowerTrader pt && pt.PowerOn && t.Faction == at.Faction);
            if (powered) charge = Mathf.Min(Props.shots, charge + Props.shots * 0.1f);
        }

        public override void CompTick()
        {
            base.CompTick();
            if (parent.IsHashIntervalTick(250)) Tick250();
        }

        public override string CompInspectStringExtra()
        {
            if (!Enabled) return null;
            string s = "Rimverse_WeaponCharge".Translate(Mathf.CeilToInt(Mathf.Max(0f, charge)), Props.shots);
            if (Props.heatPerShot > 0f) s += "\n" + "Rimverse_WeaponHeat".Translate((heat / Props.heatMax).ToStringPercent()).ToString() + (venting ? " (" + "Rimverse_WeaponVenting".Translate().ToString() + ")" : "");
            return s;
        }

        public override string TransformLabel(string label)
        {
            if (!Enabled || charge < 0f) return label;
            return label + " (" + Mathf.CeilToInt(charge) + "/" + Props.shots + ")";
        }

        public static CompEnergyCell Of(Verb v) => v?.EquipmentSource?.GetComp<CompEnergyCell>();
    }

    [HarmonyPatch(typeof(Pawn_EquipmentTracker), nameof(Pawn_EquipmentTracker.EquipmentTrackerTick))]
    public static class Patch_EnergyCellEquippedTick
    {
        public static void Postfix(Pawn_EquipmentTracker __instance)
        {
            ThingWithComps eq = __instance.Primary;
            if (eq == null || !__instance.pawn.IsHashIntervalTick(250)) return;
            eq.GetComp<CompEnergyCell>()?.Tick250();
        }
    }

    [HarmonyPatch(typeof(Verb), nameof(Verb.Available))]
    public static class Patch_EnergyCellAvailable
    {
        public static void Postfix(Verb __instance, ref bool __result)
        {
            if (!__result || !CompEnergyCell.Enabled) return;
            CompEnergyCell c = CompEnergyCell.Of(__instance);
            if (c != null && !c.CanFire) __result = false;
        }
    }

    [HarmonyPatch(typeof(Verb_Shoot), "TryCastShot")]
    public static class Patch_EnergyCellShot
    {
        public static bool Prefix(Verb_Shoot __instance, ref bool __result)
        {
            if (!CompEnergyCell.Enabled) return true;
            CompEnergyCell c = CompEnergyCell.Of(__instance);
            if (c == null || c.CanFire) return true;
            __result = false;
            return false;
        }

        public static void Postfix(Verb_Shoot __instance, bool __result)
        {
            if (!__result || !CompEnergyCell.Enabled) return;
            CompEnergyCell.Of(__instance)?.Fired();
        }
    }
}
