using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public enum LessonSubject
    {
        General,
        Piloting,
        Engineering,
        Medicine
    }

    public class CompProperties_ShipClassroom : CompProperties
    {
        public float learnPerInterval = 0.03f;
        public float xpPerInterval = 40f;
        public bool lessons = true;
        public float joyPerInterval;
        public float cabinFeverRelief;

        public CompProperties_ShipClassroom()
        {
            compClass = typeof(CompShipClassroom);
        }
    }

    public class CompShipClassroom : ThingComp
    {
        public const int Interval = 250;
        public LessonSubject subject;

        public CompProperties_ShipClassroom Props => (CompProperties_ShipClassroom)props;

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref subject, "subject", LessonSubject.General);
        }

        public override void CompTick()
        {
            if (!parent.IsHashIntervalTick(Interval) || !parent.Spawned) return;
            CompPowerTrader power = parent.TryGetComp<CompPowerTrader>();
            if (power != null && !power.PowerOn) return;
            Room room = parent.GetRoom();
            if (room == null || room.PsychologicallyOutdoors) return;
            bool teacher = false;
            List<Pawn> kids = new List<Pawn>();
            foreach (Pawn p in parent.Map.mapPawns.FreeColonistsSpawned)
            {
                if (p.GetRoom() != room || !p.Awake()) continue;
                if (p.DevelopmentalStage == DevelopmentalStage.Child) kids.Add(p);
                else if (p.DevelopmentalStage == DevelopmentalStage.Adult && !p.Downed) teacher = true;
            }
            float factor = teacher ? 1.5f : 1f;
            foreach (Pawn k in kids)
            {
                if (Props.lessons)
                {
                    k.needs?.learning?.Learn(Props.learnPerInterval * factor);
                    SkillDef skill = PickSkill();
                    if (skill != null && k.skills != null) k.skills.Learn(skill, Props.xpPerInterval * factor);
                }
                if (Props.joyPerInterval > 0f) k.needs?.joy?.GainJoy(Props.joyPerInterval, JoyKindDefOf.Meditative);
                if (Props.cabinFeverRelief > 0f)
                {
                    Hediff h = k.health.hediffSet.GetFirstHediffOfDef(LifeDefOf.Rimverse_CabinFever);
                    if (h != null) h.Severity = Mathf.Max(0f, h.Severity - Props.cabinFeverRelief);
                }
            }
        }

        private SkillDef PickSkill()
        {
            switch (subject)
            {
                case LessonSubject.Piloting: return Rand.Bool ? SkillDefOf.Shooting : SkillDefOf.Intellectual;
                case LessonSubject.Engineering: return Rand.Bool ? SkillDefOf.Construction : SkillDefOf.Crafting;
                case LessonSubject.Medicine: return Rand.Bool ? SkillDefOf.Medicine : SkillDefOf.Intellectual;
                default: return DefDatabase<SkillDef>.AllDefsListForReading.RandomElement();
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!Props.lessons) yield break;
            yield return new Command_Action
            {
                defaultLabel = ("Rimverse_Lesson_" + subject).Translate(),
                defaultDesc = "Rimverse_LessonDesc".Translate(),
                icon = TexCommand.SelectShelf,
                action = () => subject = (LessonSubject)(((int)subject + 1) % 4)
            };
        }

        public override string CompInspectStringExtra()
        {
            return Props.lessons ? "Rimverse_LessonInspect".Translate(("Rimverse_Lesson_" + subject).Translate()) : null;
        }
    }

    public class CompProperties_WasteHandler : CompProperties
    {
        public int intervalTicks = 2500;
        public bool needsSpace;
        public List<ThingDefCountClass> outputs = new List<ThingDefCountClass>();

        public CompProperties_WasteHandler()
        {
            compClass = typeof(CompWasteHandler);
        }
    }

    public class CompWasteHandler : ThingComp
    {
        public int handled;

        public CompProperties_WasteHandler Props => (CompProperties_WasteHandler)props;

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref handled, "handled", 0);
        }

        public override void CompTick()
        {
            if (!parent.IsHashIntervalTick(Props.intervalTicks) || !parent.Spawned) return;
            CompPowerTrader power = parent.TryGetComp<CompPowerTrader>();
            if (power != null && !power.PowerOn) return;
            if (Props.needsSpace && !SpaceMapUtility.IsSpace(parent.Map) && MapComponent_ShipLocation.For(parent.Map)?.Body == null) return;
            foreach (IntVec3 c in parent.OccupiedRect())
            {
                List<Thing> things = c.GetThingList(parent.Map);
                for (int i = 0; i < things.Count; i++)
                {
                    Thing t = things[i];
                    if (t.def.defName != "Wastepack") continue;
                    t.SplitOff(1).Destroy();
                    handled++;
                    foreach (ThingDefCountClass o in Props.outputs)
                    {
                        Thing made = ThingMaker.MakeThing(o.thingDef);
                        made.stackCount = o.count;
                        GenPlace.TryPlaceThing(made, parent.def.hasInteractionCell ? parent.InteractionCell : parent.Position, parent.Map, ThingPlaceMode.Near);
                    }
                    if (Props.needsSpace) FleckMaker.ThrowSmoke(parent.DrawPos, parent.Map, 1.5f);
                    return;
                }
            }
        }

        public override string CompInspectStringExtra()
        {
            string s = "Rimverse_WasteHandled".Translate(handled);
            if (Props.needsSpace && parent.Spawned && !SpaceMapUtility.IsSpace(parent.Map) && MapComponent_ShipLocation.For(parent.Map)?.Body == null) s += "\n" + "Rimverse_WasteNeedsSpace".Translate();
            return s;
        }
    }
}
