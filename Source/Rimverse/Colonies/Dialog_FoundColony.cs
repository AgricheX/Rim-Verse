using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public class Dialog_FoundColony : Window
    {
        private readonly Map map;
        private readonly BodyData body;
        private readonly HashSet<Pawn> chosen = new HashSet<Pawn>();
        private Vector2 scroll;

        public override Vector2 InitialSize => new Vector2(520f, 560f);

        public Dialog_FoundColony(Map map, BodyData body)
        {
            this.map = map;
            this.body = body;
            forcePause = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), "Rimverse_FoundColonyTitle".Translate(body.name));
            Text.Font = GameFont.Small;
            float hazard = ColonyUtility.Hazard(body);
            Widgets.Label(new Rect(inRect.x, inRect.y + 36f, inRect.width, 48f), "Rimverse_FoundColonyInfo".Translate(hazard.ToStringPercent(), ColonyUtility.SteelCost, ColonyUtility.MealCost, ColonyUtility.MinSettlers));
            List<Pawn> pawns = map.mapPawns.FreeColonistsSpawned.ToList();
            Rect outer = new Rect(inRect.x, inRect.y + 90f, inRect.width, inRect.height - 140f);
            Rect view = new Rect(0f, 0f, outer.width - 16f, pawns.Count * 30f);
            Widgets.BeginScrollView(outer, ref scroll, view);
            float y = 0f;
            foreach (Pawn p in pawns)
            {
                bool on = chosen.Contains(p);
                Widgets.CheckboxLabeled(new Rect(0f, y, view.width, 28f), p.LabelShortCap + " (" + "Rimverse_SocialLevel".Translate(p.skills?.GetSkill(SkillDefOf.Social).Level ?? 0) + ")", ref on);
                if (on) chosen.Add(p);
                else chosen.Remove(p);
                y += 30f;
            }
            Widgets.EndScrollView();
            bool ok = chosen.Count >= ColonyUtility.MinSettlers && chosen.Count < pawns.Count;
            Rect btn = new Rect(inRect.x + inRect.width - 200f, inRect.yMax - 40f, 200f, 36f);
            if (!ok) GUI.color = Color.gray;
            if (Widgets.ButtonText(btn, "Rimverse_FoundColonyConfirm".Translate(chosen.Count)) && ok)
            {
                ColonyUtility.Found(map, body, chosen.ToList());
                Close();
            }
            GUI.color = Color.white;
        }
    }
}
