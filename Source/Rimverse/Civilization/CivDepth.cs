using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public static class CivFlag
    {
        public static void Draw(Rect r, Civilization c)
        {
            int h = Gen.HashCombineInt(c.id * 7919, (c.root ?? "").GetHashCode());
            Color a = c.color;
            Color.RGBToHSV(a, out float hue, out float sat, out float val);
            Color b = Color.HSVToRGB((hue + 0.5f) % 1f, Mathf.Clamp01(sat * 0.8f), Mathf.Clamp01(val * 0.9f + 0.1f));
            Color e = (h >> 8 & 1) == 0 ? Color.white : new Color(0.1f, 0.1f, 0.12f);
            Widgets.DrawBoxSolid(r, b);
            switch (Mathf.Abs(h) % 5)
            {
                case 0:
                    Widgets.DrawBoxSolid(new Rect(r.x, r.y, r.width, r.height / 3f), a);
                    Widgets.DrawBoxSolid(new Rect(r.x, r.y + r.height * 2f / 3f, r.width, r.height / 3f), a);
                    break;
                case 1:
                    Widgets.DrawBoxSolid(new Rect(r.x, r.y, r.width / 3f, r.height), a);
                    break;
                case 2:
                    Widgets.DrawBoxSolid(new Rect(r.x + r.width * 0.4f, r.y, r.width * 0.2f, r.height), a);
                    Widgets.DrawBoxSolid(new Rect(r.x, r.y + r.height * 0.4f, r.width, r.height * 0.2f), a);
                    break;
                case 3:
                    Widgets.DrawBoxSolid(new Rect(r.x, r.y, r.width, r.height / 2f), a);
                    break;
                default:
                    Widgets.DrawBoxSolid(r.ContractedBy(r.height * 0.15f), a);
                    break;
            }
            float s = r.height * 0.36f;
            Rect em = new Rect(r.center.x - s / 2f, r.center.y - s / 2f, s, s);
            switch ((h >> 4) & 3)
            {
                case 0: Widgets.DrawBoxSolid(em, e); break;
                case 1: Widgets.DrawBoxSolid(em.ContractedBy(s * 0.2f), e); Widgets.DrawBox(em, 2); break;
                case 2:
                    Widgets.DrawBoxSolid(new Rect(em.x, em.center.y - s * 0.12f, s, s * 0.24f), e);
                    Widgets.DrawBoxSolid(new Rect(em.center.x - s * 0.12f, em.y, s * 0.24f, s), e);
                    break;
                default: break;
            }
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            Widgets.DrawBox(r);
            GUI.color = Color.white;
        }
    }

    public static class CivPreSpace
    {
        public static bool IsPreSpace(Civilization c) => c != null && c.stage <= 7;

        public static void Generate(GameComponent_Civilizations civs, GameComponent_Universe u, UniverseRand r, HashSet<string> taken)
        {
            GalaxyData g = u.Galaxy(0);
            float scale = g.sizeClass / 50f * RimverseMod.Settings.civilizationDensity;
            int count = Mathf.RoundToInt(r.RangeInclusive(30, 70) * scale);
            for (int i = 0; i < count && civs.civilizations.Count < GameComponent_Civilizations.MaxCivilizations; i++)
            {
                int stage = r.RangeInclusive(1, 7);
                double a = r.ValueDouble * System.Math.PI * 2.0;
                double rad = g.radiusLy * System.Math.Sqrt(r.ValueDouble) * 0.95;
                Vec3d pos = r.Chance(0.3f) ? u.PositionOf(u.HomeSystemId) + new Vec3d(System.Math.Cos(a) * (30 + r.ValueDouble * 400), 0, System.Math.Sin(a) * (30 + r.ValueDouble * 400)) : new Vec3d(System.Math.Cos(a) * rad, (r.ValueDouble - 0.5) * g.thicknessLy * 0.5, System.Math.Sin(a) * rad);
                StarEntry star = CivilizationGenerator.FindStarNear(u, 0, pos, taken);
                if (star == null) continue;
                taken.Add(star.id.Key);
                Civilization c = new Civilization
                {
                    id = civs.NextCivId(),
                    root = CivilizationGenerator.RandomRoot(r),
                    stage = stage,
                    sizeClass = r.SkewedSizeClass(1, 60, 2f),
                    capitalKey = star.id.Key,
                    galaxy = 0,
                    tech = stage <= 2 ? TechLevel.Neolithic : stage <= 5 ? TechLevel.Medieval : TechLevel.Industrial
                };
                CivilizationGenerator.FillPolicies(c, r);
                CivilizationGenerator.InitState(c, r, u);
                civs.civilizations.Add(c);
            }
        }

        private static readonly TechLevel[] Next = { TechLevel.Neolithic, TechLevel.Neolithic, TechLevel.Medieval, TechLevel.Industrial, TechLevel.Spacer, TechLevel.Ultra, TechLevel.Archotech, TechLevel.Archotech };

        public static void TechRevolution(GameComponent_Civilizations gc, Civilization c, UniverseRand r, float years)
        {
            if (c.dead || c.tech >= TechLevel.Ultra) return;
            TechLevel next = c.tech < TechLevel.Neolithic ? TechLevel.Neolithic : Next[(int)c.tech];
            if (next == TechLevel.Spacer && c.stage < 7) return;
            if (next == TechLevel.Ultra && c.stage < 11) return;
            float perYear = c.tech <= TechLevel.Neolithic ? 0.03f : c.tech == TechLevel.Medieval ? 0.02f : c.tech == TechLevel.Industrial ? 0.015f : 0.004f;
            if (c.ideology?.defName == "Rimverse_Policy_TechProgressivism") perYear *= 2f;
            if (c.ideology?.defName == "Rimverse_Policy_Traditionalism") perYear *= 0.4f;
            if (!r.Chance(Mathf.Clamp01(perYear * years))) return;
            c.tech = next;
            PolicyDef gov = CivilizationGenerator.Pick(r, PolicyGroup.Government, c.tech, c.stage, false);
            if (gov != null && r.Chance(0.5f)) c.government = gov;
            c.legitimacy = Mathf.Max(0f, c.legitimacy - 10f);
            if (c.known) Find.LetterStack.ReceiveLetter("Rimverse_LetterTechRevolutionLabel".Translate(), "Rimverse_LetterTechRevolution".Translate(c.ShownName, c.tech.ToStringHuman(), c.government?.LabelCap ?? "?"), LetterDefOf.NeutralEvent);
        }

        public static FactionDef FactionFor(Civilization c)
        {
            if (!IsPreSpace(c)) return null;
            return c.tech <= TechLevel.Medieval ? FactionDefOf.TribeCivil : FactionDefOf.OutlanderCivil;
        }
    }

    public static class CivLegitimacy
    {
        public static void Tick(GameComponent_Civilizations gc, Civilization c, UniverseRand r, float years)
        {
            float mood = c.groupMood.Count == 0 ? 50f : c.groupMood.Values.Average();
            float target = c.stability * 0.5f + mood * 0.4f + 10f - c.warWeariness * 0.3f;
            if (c.government?.defName != null && (c.government.defName.Contains("Junta") || c.government.defName.Contains("Dictator"))) target -= 10f;
            c.legitimacy = Mathf.Clamp(Mathf.Lerp(c.legitimacy, target, Mathf.Clamp01(years * 2f)), 0f, 100f);
            if (c.legitimacy < 15f && r.Chance(Mathf.Clamp01(0.3f * years)))
            {
                PolicyDef gov = CivilizationGenerator.Pick(r, PolicyGroup.Government, c.tech, c.stage, false);
                if (gov == null || gov == c.government) return;
                c.government = gov;
                c.legitimacy = 45f;
                c.stability = Mathf.Max(0f, c.stability - 10f);
                if (c.known) Find.LetterStack.ReceiveLetter("Rimverse_LetterLegitimacyLabel".Translate(), "Rimverse_LetterLegitimacy".Translate(c.ShownName, gov.LabelCap), LetterDefOf.NeutralEvent);
            }
        }
    }

    public static class CivColonyReactions
    {
        public static void Tick(GameComponent_Civilizations gc, Civilization c, UniverseRand r, float years)
        {
            List<PlayerColony> cols = GameComponent_PlayerColonies.Instance?.colonies;
            if (cols == null || cols.Count == 0 || c.dead || c.NoDiplomacy || CivPreSpace.IsPreSpace(c)) return;
            bool antiAI = IncidentWorker_RimverseEvent.AntiAI(c);
            foreach (PlayerColony col in cols)
            {
                bool near = gc.DistanceLy(c, col.SystemId) <= c.InfluenceRadiusLy + 15f;
                if (!near) continue;
                if (antiAI && GameComponent_ColonyExtras.HasAI(col)) gc.ChangePlayerOpinion(c, -4f * years);
                if (gc.OwnerOf(col.SystemId) == c && c.playerOpinion < -30f && CivSimulation.FleetSpeedLyPerDay(c) > 0f && r.Chance(Mathf.Clamp01(0.5f * years)))
                {
                    if (gc.fleets.Any(f => f.civId == c.id && f.Moving && f.TargetsPlayer)) return;
                    Fleet raid = CivSimulation.LaunchFleet(gc, c, col.systemKey, Civilization.PlayerId, FleetMission.Raid, 0.3f);
                    if (raid != null && c.known) Find.LetterStack.ReceiveLetter("Rimverse_LetterBombardLabel".Translate(), "Rimverse_LetterBombard".Translate(c.ShownName, col.name), LetterDefOf.ThreatBig);
                    return;
                }
            }
        }
    }

    public static class CivIdeo
    {
        private static MemeDef M(string n) => DefDatabase<MemeDef>.GetNamedSilentFail(n);

        public static List<MemeDef> MemesFor(Civilization c)
        {
            List<MemeDef> list = new List<MemeDef>();
            string ideo = c.ideology?.defName ?? "";
            string gov = c.government?.defName ?? "";
            string xeno = c.xeno?.defName ?? "";
            void Add(string n)
            {
                MemeDef m = M(n);
                if (m != null && !list.Contains(m)) list.Add(m);
            }
            if (ideo.Contains("ReligiousZeal") || ideo.Contains("Traditionalism")) Add("Structure_TheistEmbodied");
            else if (ideo.Contains("VoidFaith") || ideo.Contains("GreatFlux") || ideo.Contains("Cosmism")) Add("Structure_TheistAbstract");
            else Add("Structure_Ideological");
            if (ideo.Contains("Liberalism") || ideo.Contains("Anarchism")) Add("Individualist");
            else if (ideo.Contains("Communism") || ideo.Contains("Socialism") || gov.Contains("Hive") || gov.Contains("Collective")) Add("Collectivist");
            if (ideo.Contains("Environmentalism")) Add("NaturePrimacy");
            else if (ideo.Contains("Humanism") || ideo.Contains("TechProgressivism")) Add("HumanPrimacy");
            if (xeno.Contains("Xenophob") || xeno.Contains("Purity") || ideo.Contains("Nationalism")) Add("Supremacist");
            else if (ideo.Contains("XenoUnity") || ideo.Contains("Legalism")) Add("Loyalist");
            if (ideo.Contains("Transhumanism") || ideo.Contains("Emulationism")) Add("Transhumanist");
            else if (ideo.Contains("Traditionalism") || ideo.Contains("ReligiousZeal")) Add("FleshPurity");
            if (c.lore == LoreKind.Pirates || ideo.Contains("Militarism") && (c.government?.profile == AiProfile.Warmonger)) Add("Raider");
            if (c.lore == LoreKind.Pirates && Mathf.Abs(c.id) % 4 == 0) Add("Cannibal");
            return list;
        }

        public static void Apply(Civilization c, Faction f)
        {
            if (!ModCompat.Ideology || f == null || f.ideos == null || c.NoDiplomacy) return;
            try
            {
                List<MemeDef> memes = MemesFor(c);
                Ideo ideo = IdeoGenerator.GenerateIdeo(new IdeoGenerationParms(f.def, false, null, null, memes));
                if (ideo == null) return;
                ideo.name = c.Name;
                Find.IdeoManager.Add(ideo);
                f.ideos.SetPrimary(ideo);
                ideo.RecachePrecepts();
            }
            catch (System.Exception e)
            {
                RimverseLog.Warning("Civ ideo failed: " + e.Message);
            }
        }
    }
}
