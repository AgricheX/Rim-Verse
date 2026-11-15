using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace Rimverse
{
    public abstract class RoyalTitlePermitWorker_RimverseInstant : RoyalTitlePermitWorker
    {
        protected virtual string Blocked(Map map, Pawn pawn) => null;

        protected abstract void Use(Map map, Pawn pawn, Faction faction);

        public override IEnumerable<FloatMenuOption> GetRoyalAidOptions(Map map, Pawn pawn, Faction faction)
        {
            if (AidDisabled(map, pawn, faction, out string reason))
            {
                yield return new FloatMenuOption(def.LabelCap + ": " + reason, null);
                yield break;
            }
            string why = Blocked(map, pawn);
            if (why != null)
            {
                yield return new FloatMenuOption(def.LabelCap + ": " + why, null);
                yield break;
            }
            string desc = def.LabelCap + ": ";
            bool ok = FillAidOption(pawn, faction, ref desc, out bool free);
            Action act = null;
            if (ok)
            {
                act = () =>
                {
                    Use(map, pawn, faction);
                    if (!free) pawn.royalty.TryRemoveFavor(faction, def.royalAid.favorCost);
                    pawn.royalty.GetPermit(def, faction)?.Notify_Used();
                };
            }
            yield return new FloatMenuOption(desc, act);
        }
    }

    public class RoyalTitlePermitWorker_ShipyardRepair : RoyalTitlePermitWorker_RimverseInstant
    {
        protected override void Use(Map map, Pawn pawn, Faction faction)
        {
            int n = 0;
            foreach (Building b in map.listerBuildings.allBuildingsColonist.ToList())
            {
                if (b.HitPoints < b.MaxHitPoints)
                {
                    b.HitPoints = b.MaxHitPoints;
                    n++;
                }
            }
            map.GetComponent<MapComponent_RimverseShips>()?.Dirty();
            Messages.Message("Rimverse_MsgShipyardRepair".Translate(n), MessageTypeDefOf.PositiveEvent, false);
        }
    }

    public class RoyalTitlePermitWorker_CrewRescue : RoyalTitlePermitWorker_RimverseInstant
    {
        protected override void Use(Map map, Pawn pawn, Faction faction)
        {
            int n = 0;
            foreach (Pawn p in map.mapPawns.FreeColonistsSpawned.ToList())
            {
                foreach (Hediff h in p.health.hediffSet.hediffs.ToList())
                {
                    if (h.TendableNow())
                    {
                        h.Tended(1f, 1f);
                        n++;
                    }
                }
                Hediff hyp = p.health.hediffSet.GetFirstHediffOfDef(RimverseDefOf.Rimverse_Hypoxia);
                if (hyp != null) p.health.RemoveHediff(hyp);
            }
            Messages.Message("Rimverse_MsgCrewRescue".Translate(n), MessageTypeDefOf.PositiveEvent, false);
        }
    }

    public class RoyalTitlePermitWorker_ConvoyEscort : RoyalTitlePermitWorker_RimverseInstant
    {
        protected override void Use(Map map, Pawn pawn, Faction faction)
        {
            GameComponent_RimverseExtras x = GameComponent_RimverseExtras.Instance;
            if (x == null) return;
            x.escortUntil = Find.TickManager.TicksGame + GenDate.TicksPerDay * 5;
            Messages.Message("Rimverse_MsgConvoyEscort".Translate(), MessageTypeDefOf.PositiveEvent, false);
        }
    }

    public static class RoyalPermits
    {
        public static bool HasGatePass(Map map)
        {
            if (!ModCompat.Royalty || map == null) return false;
            RoyalTitlePermitDef pass = DefDatabase<RoyalTitlePermitDef>.GetNamedSilentFail("Rimverse_GatePass");
            Faction empire = Faction.OfEmpire;
            if (pass == null || empire == null) return false;
            return map.mapPawns.FreeColonistsSpawned.Any(p => p.royalty != null && p.royalty.HasPermit(pass, empire));
        }
    }

    public class CompProperties_AbilityVacuumPush : CompProperties_AbilityEffect
    {
        public int distance = 6;

        public CompProperties_AbilityVacuumPush()
        {
            compClass = typeof(CompAbilityEffect_VacuumPush);
        }
    }

    public class CompAbilityEffect_VacuumPush : CompAbilityEffect
    {
        public new CompProperties_AbilityVacuumPush Props => (CompProperties_AbilityVacuumPush)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            if (!(target.Thing is Pawn p) || !p.Spawned) return;
            Map map = p.Map;
            Vector3 dir = (p.Position - parent.pawn.Position).ToVector3();
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            dir.Normalize();
            IntVec3 best = p.Position;
            for (int i = 1; i <= Props.distance; i++)
            {
                IntVec3 c = (p.Position.ToVector3Shifted() + dir * i).ToIntVec3();
                if (!c.InBounds(map) || !c.Walkable(map)) break;
                best = c;
            }
            if (best != p.Position)
            {
                p.Position = best;
                p.Notify_Teleported(true, true);
            }
            p.stances?.stunner?.StunFor(90, parent.pawn, false);
        }
    }

    public class CompProperties_AbilitySealBreach : CompProperties_AbilityEffect
    {
        public ThingDef wall;
        public float radius = 1.5f;

        public CompProperties_AbilitySealBreach()
        {
            compClass = typeof(CompAbilityEffect_SealBreach);
        }
    }

    public class CompAbilityEffect_SealBreach : CompAbilityEffect
    {
        public new CompProperties_AbilitySealBreach Props => (CompProperties_AbilitySealBreach)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Map map = parent.pawn.Map;
            ThingDef wall = Props.wall ?? DefDatabase<ThingDef>.GetNamedSilentFail("Rimverse_HullWall");
            if (map == null || wall == null) return;
            int n = 0;
            foreach (IntVec3 c in GenRadial.RadialCellsAround(target.Cell, Props.radius, true))
            {
                if (!c.InBounds(map) || !HullUtility.IsHull(map, c)) continue;
                if (c.GetFirstPawn(map) != null || c.GetEdifice(map) != null) continue;
                if (!IsBreachCell(map, c)) continue;
                Thing t = ThingMaker.MakeThing(wall, wall.MadeFromStuff ? GenStuff.DefaultStuffFor(wall) : null);
                t.SetFaction(Faction.OfPlayer);
                GenSpawn.Spawn(t, c, map);
                n++;
            }
            map.GetComponent<MapComponent_RimverseShips>()?.Dirty();
            Messages.Message("Rimverse_MsgSealedBreach".Translate(n), MessageTypeDefOf.PositiveEvent, false);
        }

        private static bool IsBreachCell(Map map, IntVec3 c)
        {
            int walls = 0;
            bool vacuum = false;
            foreach (IntVec3 d in GenAdj.CardinalDirections)
            {
                IntVec3 n = c + d;
                if (!n.InBounds(map)) continue;
                Building e = n.GetEdifice(map);
                if (e != null && e.def.passability == Traversability.Impassable) walls++;
                if (HullUtility.IsVacuumTerrain(map, n) && !HullUtility.IsHull(map, n)) vacuum = true;
            }
            return walls >= 2 || vacuum;
        }
    }

    public class CompProperties_AbilityShortSkip : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityShortSkip()
        {
            compClass = typeof(CompAbilityEffect_ShortSkip);
        }
    }

    public class CompAbilityEffect_ShortSkip : CompAbilityEffect
    {
        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Map map = parent.pawn?.Map;
            if (map == null || !target.Cell.InBounds(map) || !target.Cell.Standable(map)) return false;
            return base.Valid(target, throwMessages);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn p = parent.pawn;
            if (p?.Map == null || !target.Cell.Standable(p.Map)) return;
            FleckMaker.ThrowDustPuffThick(p.DrawPos, p.Map, 1.5f, Color.white);
            p.Position = target.Cell;
            p.Notify_Teleported(true, true);
            FleckMaker.ThrowDustPuffThick(p.DrawPos, p.Map, 1.5f, Color.white);
        }
    }

    public class CompProperties_AbilityBlindSensors : CompProperties_AbilityEffect
    {
        public HediffDef hediff;
        public float radius = 22f;
        public int durationTicks = 5000;

        public CompProperties_AbilityBlindSensors()
        {
            compClass = typeof(CompAbilityEffect_BlindSensors);
        }
    }

    public class CompAbilityEffect_BlindSensors : CompAbilityEffect
    {
        public new CompProperties_AbilityBlindSensors Props => (CompProperties_AbilityBlindSensors)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            Map map = caster?.Map;
            if (map == null) return;
            int n = 0;
            if (Props.hediff != null)
            {
                foreach (Pawn p in map.mapPawns.AllPawnsSpawned.ToList())
                {
                    if (!p.HostileTo(caster) || p.Position.DistanceTo(caster.Position) > Props.radius) continue;
                    Hediff h = HediffMaker.MakeHediff(Props.hediff, p);
                    HediffComp_Disappears d = h.TryGetComp<HediffComp_Disappears>();
                    if (d != null) d.ticksToDisappear = Props.durationTicks;
                    p.health.AddHediff(h);
                    n++;
                }
            }
            RimverseShip ship = ColonyUtility.PlayerShipOn(map);
            GameComponent_RimverseExtras x = GameComponent_RimverseExtras.Instance;
            if (ship != null && x != null) x.jamUntil[ship.id] = Find.TickManager.TicksGame + Props.durationTicks;
            Messages.Message("Rimverse_MsgSensorsBlinded".Translate(n), caster, MessageTypeDefOf.PositiveEvent, false);
        }
    }

    public class CompProperties_AbilityHullShield : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityHullShield()
        {
            compClass = typeof(CompAbilityEffect_HullShield);
        }
    }

    public class CompAbilityEffect_HullShield : CompAbilityEffect
    {
        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            RimverseShip ship = ColonyUtility.PlayerShipOn(parent.pawn?.Map);
            if (ship == null || !ShipCombatUtility.Shields(ship).Any())
            {
                if (throwMessages) Messages.Message("Rimverse_PsyNeedShield".Translate(), MessageTypeDefOf.RejectInput, false);
                return false;
            }
            return base.Valid(target, throwMessages);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            RimverseShip ship = ColonyUtility.PlayerShipOn(parent.pawn?.Map);
            if (ship == null) return;
            foreach (CompShipShield s in ShipCombatUtility.Shields(ship)) s.hitPoints = s.Props.maxHitPoints;
            Messages.Message("Rimverse_MsgHullShield".Translate(), parent.pawn, MessageTypeDefOf.PositiveEvent, false);
        }
    }

    public class FocusStrengthOffset_InSpace : FocusStrengthOffset
    {
        public override bool CanApply(Thing parent, Pawn user = null) => parent?.Map != null && SpaceMapUtility.IsSpace(parent.Map);

        public override float GetOffset(Thing parent, Pawn user = null) => CanApply(parent, user) ? offset : 0f;

        public override string GetExplanation(Thing parent) => CanApply(parent) ? "Rimverse_FocusInSpace".Translate() + ": " + offset.ToStringWithSign("0%") : "";

        public override string GetExplanationAbstract(ThingDef def = null) => "Rimverse_FocusInSpace".Translate() + ": " + offset.ToStringWithSign("0%");
    }

    public abstract class IncidentWorker_RoyalShip : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = parms.target as Map;
            return RoyalQuests.EmpireFriendly && map != null && map.IsPlayerHome && SpaceMapUtility.IsSpace(map) && ColonyUtility.PlayerShipOn(map) != null;
        }
    }

    public class IncidentWorker_EscortNoble : IncidentWorker_RoyalShip
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            GameComponent_RimverseExtras x = GameComponent_RimverseExtras.Instance;
            return base.CanFireNowSub(parms) && x != null && x.noble == null;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(Rand.Bool ? "Empire_Royal_Knight" : "Empire_Royal_Baron");
            Pawn anchor = map.mapPawns.FreeColonistsSpawned.RandomElementWithFallback();
            if (kind == null || anchor == null) return false;
            Pawn noble = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, RoyalQuests.Empire, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true));
            IntVec3 cell = CellFinder.RandomClosewalkCellNear(anchor.Position, map, 6);
            GenSpawn.Spawn(noble, cell, map);
            LordMaker.MakeNewLord(RoyalQuests.Empire, new LordJob_VisitColony(RoyalQuests.Empire, cell, GenDate.TicksPerDay * 7), map, new List<Pawn> { noble });
            GameComponent_RimverseExtras x = GameComponent_RimverseExtras.Instance;
            x.noble = noble;
            x.nobleLeaveTick = Find.TickManager.TicksGame + GenDate.TicksPerDay * 5;
            SendStandardLetter("Rimverse_LetterEscortNobleLabel".Translate(), "Rimverse_LetterEscortNoble".Translate(noble.LabelShort), LetterDefOf.PositiveEvent, parms, noble);
            return true;
        }
    }

    public class IncidentWorker_DefendStellarch : IncidentWorker_RoyalShip
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = parms.target as Map;
            MapComponent_ShipCombat c = MapComponent_ShipCombat.For(map);
            return base.CanFireNowSub(parms) && c != null && !c.InCombat && RimverseMod.Settings.shipCombatEnabled;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            RimverseShip ship = ColonyUtility.PlayerShipOn(map);
            Faction pirates = ShipCombatUtility.PirateFaction(GameComponent_Civilizations.Instance);
            if (ship == null || pirates == null) return false;
            float power = Mathf.Max(400f, StorytellerUtility.DefaultThreatPointsNow(map));
            if (!ShipCombatUtility.StartEncounter(map, ship, pirates, power, TechLevel.Spacer, -2)) return false;
            GameComponent_RimverseExtras x = GameComponent_RimverseExtras.Instance;
            x.defenseMapId = map.uniqueID;
            x.defenseUntil = Find.TickManager.TicksGame + GenDate.TicksPerDay * 2;
            SendStandardLetter("Rimverse_LetterDefendStellarchLabel".Translate(), "Rimverse_LetterDefendStellarch".Translate(), LetterDefOf.ThreatBig, parms, LookTargets.Invalid);
            return true;
        }
    }

    public class IncidentWorker_HuntDeserters : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = parms.target as Map;
            GameComponent_RimverseExtras x = GameComponent_RimverseExtras.Instance;
            return RoyalQuests.EmpireFriendly && map != null && map.IsPlayerHome && x != null && x.deserters.Count == 0 && !EndlessRoomsCompat.IsBackrooms(map);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            Faction pirates = ShipCombatUtility.PirateFaction(GameComponent_Civilizations.Instance) ?? Find.FactionManager.RandomEnemyFaction(false, false, false);
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail("Empire_Fighter_Trooper");
            if (pirates == null || kind == null) return false;
            int count = Rand.RangeInclusive(3, 5);
            List<Pawn> group = new List<Pawn>();
            for (int i = 0; i < count; i++) group.Add(PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, pirates, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true)));
            RimverseShip ship = ColonyUtility.PlayerShipOn(map);
            IntVec3 cell = ship != null ? ShipCombatUtility.RandomCell(ship) : DropCellFinder.RandomDropSpot(map);
            DropPodUtility.DropThingsNear(cell, map, group.Cast<Thing>(), 110, false, false, true);
            LordMaker.MakeNewLord(pirates, new LordJob_AssaultColony(pirates, false, false, false, false, false), map, group);
            GameComponent_RimverseExtras.Instance.deserters.AddRange(group);
            SendStandardLetter("Rimverse_LetterHuntDesertersLabel".Translate(), "Rimverse_LetterHuntDeserters".Translate(count), LetterDefOf.ThreatSmall, parms, group[0]);
            return true;
        }
    }
}
