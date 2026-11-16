using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Rimverse
{
    public class HediffCompProperties_Flow : HediffCompProperties
    {
        public HediffCompProperties_Flow()
        {
            compClass = typeof(HediffComp_Flow);
        }
    }

    public class HediffComp_Flow : HediffComp
    {
        public int sensitivity = -1;
        public int level = 1;
        public float xp;
        public float energy = -1f;
        public float alignment;
        public Pawn master;
        public Pawn apprentice;

        public static float XpFor(int lvl) => 150f * lvl * lvl;

        public int MaxLevel => sensitivity >= 90 ? 7 : sensitivity >= 60 ? 6 : sensitivity >= 45 ? 5 : sensitivity >= 30 ? 4 : 3;

        public float MaxEnergy => 20f * level * Mathf.Max(0.3f, 1f - 0.1f * Pawn.GetPsylinkLevel());

        public override void CompPostMake()
        {
            base.CompPostMake();
            Init();
        }

        public void Init()
        {
            if (sensitivity < 0) sensitivity = Mathf.Clamp(Mathf.RoundToInt(20f + 80f * Mathf.Pow(Rand.Value, 2.2f)), 20, 100);
            if (energy < 0f) energy = MaxEnergy;
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref sensitivity, "sensitivity", -1);
            Scribe_Values.Look(ref level, "level", 1);
            Scribe_Values.Look(ref xp, "xp");
            Scribe_Values.Look(ref energy, "energy", -1f);
            Scribe_Values.Look(ref alignment, "alignment");
            Scribe_References.Look(ref master, "master");
            Scribe_References.Look(ref apprentice, "apprentice");
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (!Pawn.IsHashIntervalTick(250)) return;
            Init();
            bool meditating = Pawn.CurJobDef == JobDefOf.Meditate;
            float perHour = meditating ? 20f : 5f;
            energy = Mathf.Min(MaxEnergy, energy + perHour * 250f / GenDate.TicksPerHour);
            if (meditating)
            {
                float gain = 2f;
                if (Pawn.Spawned && GenRadial.RadialDistinctThingsAround(Pawn.Position, Pawn.Map, 6f, true).Any(t => t.def.defName == "Rimverse_ResonanceShard" || t.def.defName == "Rimverse_FlowShrine")) gain *= 2f;
                GainXp(gain);
            }
            if (apprentice != null && (apprentice.Dead || apprentice.Destroyed || FlowDepth.Data(apprentice) == null)) apprentice = null;
            if (master != null && (master.Dead || master.Destroyed)) master = null;
            FlowDepth.SyncSide(Pawn, this);
        }

        public void GainXp(float amount)
        {
            if (master != null && master.Spawned && Pawn.Spawned && master.Map == Pawn.Map) amount *= 3f;
            xp += amount;
            while (level < MaxLevel && xp >= XpFor(level))
            {
                xp -= XpFor(level);
                level++;
                energy = MaxEnergy;
                if (Pawn.Faction == Faction.OfPlayer) Messages.Message("Rimverse_FlowLevelUp".Translate(Pawn.LabelShort, level), Pawn, MessageTypeDefOf.PositiveEvent, false);
            }
        }

        public override string CompLabelInBracketsExtra => "Rimverse_FlowBracket".Translate(level, Mathf.FloorToInt(energy), Mathf.FloorToInt(MaxEnergy));

        public override string CompTipStringExtra
        {
            get
            {
                string s = "Rimverse_FlowTip".Translate(sensitivity, level, MaxLevel, Mathf.FloorToInt(xp), Mathf.FloorToInt(XpFor(level)), Mathf.RoundToInt(alignment));
                if (master != null) s += "\n" + "Rimverse_FlowMasterIs".Translate(master.LabelShort);
                if (apprentice != null) s += "\n" + "Rimverse_FlowApprenticeIs".Translate(apprentice.LabelShort);
                return s;
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmos()
        {
            if (Pawn.Faction != Faction.OfPlayer || level < 4 || apprentice != null) yield break;
            Command_Action c = new Command_Action
            {
                defaultLabel = "Rimverse_FlowTakeApprentice".Translate(),
                defaultDesc = "Rimverse_FlowTakeApprenticeDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("Rimverse/UI/Flow/Sense", false) ?? BaseContent.BadTex,
                action = () =>
                {
                    List<FloatMenuOption> opts = new List<FloatMenuOption>();
                    foreach (Pawn p in Pawn.Map?.mapPawns.FreeColonistsSpawned ?? new List<Pawn>())
                    {
                        HediffComp_Flow d = FlowDepth.Data(p);
                        if (p == Pawn || d == null || d.master != null || d.level >= level) continue;
                        Pawn target = p;
                        opts.Add(new FloatMenuOption(p.LabelShortCap, () => FlowDepth.Bind(Pawn, target)));
                    }
                    if (opts.Count == 0) opts.Add(new FloatMenuOption("Rimverse_FlowNoApprentice".Translate(), null));
                    Find.WindowStack.Add(new FloatMenu(opts));
                }
            };
            yield return c;
        }
    }

    public static class FlowDepth
    {
        public static HediffComp_Flow Data(Pawn p)
        {
            if (p?.health?.hediffSet == null || FlowUtility.Sensitive == null) return null;
            Hediff h = p.health.hediffSet.GetFirstHediffOfDef(FlowUtility.Sensitive);
            return (h as HediffWithComps)?.TryGetComp<HediffComp_Flow>();
        }

        public static int Level(Pawn p) => Data(p)?.level ?? 0;

        public static void Bind(Pawn master, Pawn apprentice)
        {
            HediffComp_Flow m = Data(master);
            HediffComp_Flow a = Data(apprentice);
            if (m == null || a == null) return;
            m.apprentice = apprentice;
            a.master = master;
            PawnRelationDef rm = DefDatabase<PawnRelationDef>.GetNamedSilentFail("Rimverse_FlowMaster");
            PawnRelationDef ra = DefDatabase<PawnRelationDef>.GetNamedSilentFail("Rimverse_FlowApprentice");
            if (rm != null && !apprentice.relations.DirectRelationExists(rm, master)) apprentice.relations.AddDirectRelation(rm, master);
            if (ra != null && !master.relations.DirectRelationExists(ra, apprentice)) master.relations.AddDirectRelation(ra, apprentice);
            Messages.Message("Rimverse_FlowBound".Translate(master.LabelShort, apprentice.LabelShort), master, MessageTypeDefOf.PositiveEvent, false);
        }

        public static void SyncSide(Pawn p, HediffComp_Flow d)
        {
            FlowSide side = FlowUtility.Side(p);
            if (side == FlowSide.Harmony && d.alignment < -10f) Swap(p, FlowUtility.HarmonyDef, FlowUtility.RuptureDef);
            else if (side == FlowSide.Rupture && d.alignment > 10f) Swap(p, FlowUtility.RuptureDef, FlowUtility.HarmonyDef);
            Extreme(p, "Rimverse_FlowZenith", d.alignment > 50f);
            Extreme(p, "Rimverse_FlowAbyss", d.alignment < -50f);
        }

        private static void Swap(Pawn p, HediffDef from, HediffDef to)
        {
            Hediff h = from != null ? p.health.hediffSet.GetFirstHediffOfDef(from) : null;
            if (h != null) p.health.RemoveHediff(h);
            if (to != null && !p.health.hediffSet.HasHediff(to)) p.health.AddHediff(to);
            if (p.Faction == Faction.OfPlayer) Messages.Message("Rimverse_FlowSideChanged".Translate(p.LabelShort, ("Rimverse_FlowSide_" + FlowUtility.Side(p)).Translate()), p, MessageTypeDefOf.NeutralEvent, false);
        }

        private static void Extreme(Pawn p, string def, bool want)
        {
            HediffDef d = DefDatabase<HediffDef>.GetNamedSilentFail(def);
            if (d == null) return;
            Hediff h = p.health.hediffSet.GetFirstHediffOfDef(d);
            if (want && h == null) p.health.AddHediff(d);
            else if (!want && h != null) p.health.RemoveHediff(h);
        }

        public static bool Latent(Pawn p)
        {
            HediffDef d = DefDatabase<HediffDef>.GetNamedSilentFail("Rimverse_FlowLatent");
            return d != null && p?.health?.hediffSet != null && p.health.hediffSet.HasHediff(d);
        }

        public static void MakeLatent(Pawn p)
        {
            HediffDef d = DefDatabase<HediffDef>.GetNamedSilentFail("Rimverse_FlowLatent");
            if (d != null && !Latent(p) && !FlowUtility.IsSensitive(p)) p.health.AddHediff(d);
        }

        public static void AwakenLatent(Pawn p, string reason)
        {
            if (!Latent(p)) return;
            HediffDef d = DefDatabase<HediffDef>.GetNamedSilentFail("Rimverse_FlowLatent");
            p.health.RemoveHediff(p.health.hediffSet.GetFirstHediffOfDef(d));
            FlowUtility.Awaken(p, FlowSide.None, true);
            if (p.Faction == Faction.OfPlayer && !reason.NullOrEmpty()) Messages.Message(reason.Translate(p.LabelShort), p, MessageTypeDefOf.PositiveEvent, false);
        }

        public static float ColonyFlowSignature()
        {
            float s = 0f;
            foreach (Pawn p in PawnsFinder.AllMaps_FreeColonistsSpawned)
            {
                HediffComp_Flow d = Data(p);
                if (d != null) s += d.level * 500f;
            }
            return s;
        }

        public static bool HasFlowOrder(Civilization c, out bool rupture)
        {
            rupture = c.HasCivic("Rimverse_Policy_CivicRuptureLords");
            return rupture || c.HasCivic("Rimverse_Policy_CivicHarmonyGuardians");
        }
    }

    public class HediffCompProperties_FlowBarrier : HediffCompProperties
    {
        public float points = 150f;

        public HediffCompProperties_FlowBarrier()
        {
            compClass = typeof(HediffComp_FlowBarrier);
        }
    }

    public class HediffComp_FlowBarrier : HediffComp
    {
        public float left = -1f;

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref left, "left", -1f);
        }

        public bool Absorb(float amount)
        {
            if (left < 0f) left = ((HediffCompProperties_FlowBarrier)props).points;
            if (left <= 0f) return false;
            left -= amount;
            if (Pawn.Spawned) FleckMaker.Static(Pawn.TrueCenter(), Pawn.Map, FleckDefOf.ExplosionFlash, 0.6f);
            if (left <= 0f) parent.Severity = 0f;
            return true;
        }

        public override string CompLabelInBracketsExtra => left < 0f ? null : Mathf.CeilToInt(left).ToString();
    }

    public class HediffCompProperties_FlowDominated : HediffCompProperties
    {
        public HediffCompProperties_FlowDominated()
        {
            compClass = typeof(HediffComp_FlowDominated);
        }
    }

    public class HediffComp_FlowDominated : HediffComp
    {
        public Faction original;

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_References.Look(ref original, "original");
        }

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            if (Pawn.Dead || Pawn.Faction == original) return;
            Pawn.SetFaction(original);
            Pawn.jobs?.EndCurrentJob(JobCondition.InterruptForced);
        }
    }

    public class Gene_FlowSensitive : Gene
    {
        public override void PostAdd()
        {
            base.PostAdd();
            if (!FlowUtility.Enabled || pawn?.health == null) return;
            if (FlowUtility.IsSensitive(pawn))
            {
                HediffComp_Flow d = FlowDepth.Data(pawn);
                if (d != null) d.sensitivity = Mathf.Min(100, d.sensitivity + 30);
            }
            else FlowDepth.MakeLatent(pawn);
        }
    }

    public class FlowWeaponExtension : DefModExtension
    {
        public int minLevel;
    }

    [HarmonyPatch(typeof(EquipmentUtility), nameof(EquipmentUtility.CanEquip), new[] { typeof(Thing), typeof(Pawn), typeof(string), typeof(bool) }, new[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out, ArgumentType.Normal })]
    public static class Patch_FlowWeaponEquip
    {
        public static void Postfix(Thing thing, Pawn pawn, ref string cantReason, ref bool __result)
        {
            if (!__result || !FlowUtility.Enabled) return;
            FlowWeaponExtension ext = thing?.def?.GetModExtension<FlowWeaponExtension>();
            if (ext == null || FlowDepth.Level(pawn) >= ext.minLevel) return;
            __result = false;
            cantReason = "Rimverse_FlowWeaponLevel".Translate(ext.minLevel);
        }
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.PreApplyDamage))]
    public static class Patch_FlowDefense
    {
        public static void Postfix(Pawn_HealthTracker __instance, Pawn ___pawn, DamageInfo dinfo, ref bool absorbed)
        {
            if (absorbed || ___pawn == null || !___pawn.Spawned) return;
            HediffDef bd = DefDatabase<HediffDef>.GetNamedSilentFail("Rimverse_FlowBarrier");
            if (bd != null && ___pawn.health.hediffSet.GetFirstHediffOfDef(bd) is HediffWithComps bh && bh.TryGetComp<HediffComp_FlowBarrier>() is HediffComp_FlowBarrier barrier && barrier.Absorb(dinfo.Amount))
            {
                absorbed = true;
                return;
            }
            if (!FlowUtility.Enabled || !dinfo.Def.isRanged) return;
            ThingWithComps w = ___pawn.equipment?.Primary;
            if (w == null || !w.def.weaponTags.NotNullAndContains("Rimverse_BeamBlade")) return;
            HediffComp_Flow d = FlowDepth.Data(___pawn);
            if (d == null || d.level < 3 || d.energy < 2f) return;
            if (!Rand.Chance(Mathf.Min(0.6f, d.level * 0.08f))) return;
            d.energy -= 2f;
            absorbed = true;
            MoteMaker.ThrowText(___pawn.DrawPos, ___pawn.Map, "Rimverse_FlowDeflected".Translate(), 1.5f);
            FleckMaker.ThrowMicroSparks(___pawn.DrawPos, ___pawn.Map);
        }
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), "MakeDowned")]
    public static class Patch_FlowAwakenOnDowned
    {
        public static void Postfix(Pawn ___pawn)
        {
            if (FlowUtility.Enabled && ___pawn != null && FlowDepth.Latent(___pawn) && Rand.Chance(0.35f)) FlowDepth.AwakenLatent(___pawn, "Rimverse_FlowAwakenDanger");
        }
    }

    public class GameComponent_FlowOrders : GameComponent
    {
        public GameComponent_FlowOrders(Game game)
        {
        }

        public override void GameComponentTick()
        {
            if (!FlowUtility.Enabled || Find.TickManager.TicksGame % 2500 != 1777) return;
            foreach (Pawn p in PawnsFinder.AllMaps_FreeColonistsSpawned.ToList())
            {
                if (!FlowDepth.Latent(p)) continue;
                if (p.CurJobDef == JobDefOf.Meditate && GenRadial.RadialDistinctThingsAround(p.Position, p.Map, 5f, true).Any(t => t.def.defName == "Rimverse_ResonanceShard" || t.def.defName == "Rimverse_FlowShrine") && Rand.Chance(0.25f)) FlowDepth.AwakenLatent(p, "Rimverse_FlowAwakenMeditation");
                else if (p.Map.mapPawns.FreeColonistsSpawned.Any(m => FlowDepth.Level(m) >= 4) && Rand.Chance(0.02f)) FlowDepth.AwakenLatent(p, "Rimverse_FlowAwakenMaster");
            }
        }
    }

    public class IncidentWorker_FlowWanderer : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms) => FlowUtility.Enabled && parms.target is Map m && m.mapPawns.FreeColonistsSpawned.Any(p => FlowUtility.IsSensitive(p) || FlowDepth.Latent(p));

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            List<Pawn> taught = new List<Pawn>();
            foreach (Pawn p in map.mapPawns.FreeColonistsSpawned.ToList())
            {
                if (FlowDepth.Latent(p))
                {
                    FlowDepth.AwakenLatent(p, null);
                    taught.Add(p);
                    continue;
                }
                HediffComp_Flow d = FlowDepth.Data(p);
                if (d == null) continue;
                d.GainXp(HediffComp_Flow.XpFor(d.level) * 0.6f);
                taught.Add(p);
            }
            if (taught.Count == 0) return false;
            SendStandardLetter(def.letterLabel, "Rimverse_FlowWandererText".Translate(string.Join(", ", taught.Select(t => t.LabelShort))), def.letterDef, parms, new LookTargets(taught));
            return true;
        }
    }

    public class IncidentWorker_FlowHunters : IncidentWorker_RaidEnemy
    {
        protected override bool CanFireNowSub(IncidentParms parms) => FlowUtility.Enabled && FlowDepth.ColonyFlowSignature() >= 1500f && base.CanFireNowSub(parms);

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            parms.points = Mathf.Max(parms.points, StorytellerUtility.DefaultThreatPointsNow(parms.target)) + FlowDepth.ColonyFlowSignature() * 0.1f;
            GameComponent_Civilizations gc = GameComponent_Civilizations.Instance;
            Civilization lords = gc?.civilizations?.FirstOrDefault(c => !c.dead && c.HasCivic("Rimverse_Policy_CivicRuptureLords"));
            if (lords != null)
            {
                Faction f = gc.EnsureFaction(lords);
                if (f != null && f.HostileTo(Faction.OfPlayer)) parms.faction = f;
            }
            return base.TryExecuteWorker(parms);
        }
    }

    public class CompProperties_UseEffectFlowArchive : CompProperties_UseEffect
    {
        public float xp = 400f;

        public CompProperties_UseEffectFlowArchive()
        {
            compClass = typeof(CompUseEffect_FlowArchive);
        }
    }

    public class CompUseEffect_FlowArchive : CompUseEffect
    {
        public override AcceptanceReport CanBeUsedBy(Pawn p)
        {
            if (!FlowUtility.Enabled) return "Rimverse_FlowDisabled".Translate();
            if (!FlowUtility.IsSensitive(p) && !FlowDepth.Latent(p)) return "Rimverse_FlowNotSensitive".Translate(p.Named("PAWN"));
            return base.CanBeUsedBy(p);
        }

        public override void DoEffect(Pawn usedBy)
        {
            base.DoEffect(usedBy);
            if (FlowDepth.Latent(usedBy)) FlowDepth.AwakenLatent(usedBy, "Rimverse_FlowAwakenMeditation");
            FlowDepth.Data(usedBy)?.GainXp(((CompProperties_UseEffectFlowArchive)props).xp);
        }
    }

    public static class FlowShrines
    {
        public static void TryPlace(Map map, BodyData body)
        {
            if (!FlowUtility.Enabled || map == null || body == null || body.homeWorld) return;
            UniverseRand r = new UniverseRand(UniverseRand.Hash(GenText.StableStringHash(body.name ?? "b"), body.index, 9113));
            bool isolated = CivilizationGenerator.Habitability(body) < 0.4f;
            if (!r.Chance(body.flavor == BodyFlavor.Crystal ? 0.35f : isolated ? 0.06f : 0.02f)) return;
            ThingDef shrine = DefDatabase<ThingDef>.GetNamedSilentFail("Rimverse_FlowShrine");
            if (shrine == null) return;
            if (!RCellFinder.TryFindRandomCellNearTheCenterOfTheMapWith(c => c.Standable(map) && c.GetFirstBuilding(map) == null, map, out IntVec3 at)) return;
            GenSpawn.Spawn(ThingMaker.MakeThing(shrine), at, map);
            ThingDef shard = DefDatabase<ThingDef>.GetNamedSilentFail("Rimverse_ResonanceShard");
            ThingDef archive = DefDatabase<ThingDef>.GetNamedSilentFail("Rimverse_FlowArchive");
            if (shard != null)
            {
                Thing s = ThingMaker.MakeThing(shard);
                s.stackCount = Rand.RangeInclusive(1, 2);
                GenPlace.TryPlaceThing(s, at, map, ThingPlaceMode.Near);
            }
            if (archive != null) GenPlace.TryPlaceThing(ThingMaker.MakeThing(archive), at, map, ThingPlaceMode.Near);
        }
    }
}

namespace Rimverse
{
    public class ThoughtWorker_Precept_FlowSideNear : ThoughtWorker_Precept
    {
        protected override ThoughtState ShouldHaveThought(Pawn p)
        {
            if (p.Map == null) return false;
            FlowSide want = def.defName.Contains("Harmony") ? FlowSide.Harmony : FlowSide.Rupture;
            return p.Map.mapPawns.FreeColonistsSpawned.Any(o => FlowUtility.Side(o) == want);
        }
    }
}
