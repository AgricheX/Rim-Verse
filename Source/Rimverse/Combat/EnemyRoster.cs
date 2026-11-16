using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public enum RaiderKind
    {
        Pirates,
        BountyHunters,
        Mercenaries,
        Junta,
        Fanatics,
        Corporate,
        Federation,
        Guardians
    }

    public class NamedShip : IExposable
    {
        public string name;
        public string captainFirst;
        public string captainNick;
        public string captainLast;
        public int factionId = -1;
        public int tier;
        public int escapes;
        public bool alive = true;

        public void ExposeData()
        {
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref captainFirst, "captainFirst");
            Scribe_Values.Look(ref captainNick, "captainNick");
            Scribe_Values.Look(ref captainLast, "captainLast");
            Scribe_Values.Look(ref factionId, "factionId", -1);
            Scribe_Values.Look(ref tier, "tier");
            Scribe_Values.Look(ref escapes, "escapes");
            Scribe_Values.Look(ref alive, "alive", true);
        }
    }

    public class GameComponent_EnemyRegistry : GameComponent
    {
        public List<NamedShip> ships = new List<NamedShip>();

        public static GameComponent_EnemyRegistry Instance => Current.Game?.GetComponent<GameComponent_EnemyRegistry>();

        public GameComponent_EnemyRegistry(Game game)
        {
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref ships, "ships", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && ships == null) ships = new List<NamedShip>();
        }
    }

    public static class EnemyRoster
    {
        public static RaiderKind current = RaiderKind.Pirates;

        private static readonly string[] A = { "Iron", "Red", "Silent", "Black", "Grim", "Hollow", "Burning", "Last", "Pale", "Broken", "Rusted", "Golden", "Cold", "Wandering", "Bitter" };
        private static readonly string[] B = { "Widow", "Comet", "Verdict", "Hound", "Lance", "Promise", "Oath", "Shrike", "Tide", "Reaper", "Lantern", "Debt", "Mercy", "Saint", "Fang" };

        public static void Flavor(BlueprintRequest req, int tier, TechLevel tech)
        {
            switch (current)
            {
                case RaiderKind.BountyHunters:
                    req.Add("Rimverse_ManeuverThruster", 2);
                    if (tech >= TechLevel.Spacer) req.Add("Rimverse_SignatureCloak");
                    req.Add("Rimverse_TractorBeam");
                    break;
                case RaiderKind.Fanatics:
                    req.Add("Rimverse_BoardingTorpedoTube", 1 + tier / 3);
                    break;
                case RaiderKind.Corporate:
                    req.Add("Rimverse_Decoy");
                    req.Add("Rimverse_PointLaser", 2);
                    break;
                case RaiderKind.Federation:
                    if (tech >= TechLevel.Spacer) req.Add("Rimverse_ShieldGenerator");
                    break;
                case RaiderKind.Guardians:
                    req.Add("Rimverse_RuinWeapon");
                    break;
            }
        }

        public static void Name(RimverseShip ship, Faction faction, Pawn captain, int tier)
        {
            GameComponent_EnemyRegistry reg = GameComponent_EnemyRegistry.Instance;
            if (reg == null || ship == null || faction == null) return;
            string cls = ("Rimverse_EnemyClass_" + tier).Translate();
            string kind = ("Rimverse_RaiderKind_" + current).Translate();
            NamedShip back = reg.ships.Where(n => n.alive && n.escapes > 0 && n.factionId == faction.loadID).RandomElementWithFallback();
            if (back != null && Rand.Chance(0.4f))
            {
                ship.state.name = back.name + " (" + cls + ")";
                if (captain?.Name is NameTriple && !back.captainFirst.NullOrEmpty()) captain.Name = new NameTriple(back.captainFirst, back.captainNick, back.captainLast);
                Find.LetterStack.ReceiveLetter("Rimverse_LetterNemesisLabel".Translate(), "Rimverse_LetterNemesis".Translate(back.name, captain?.LabelShort ?? "?", back.escapes), LetterDefOf.ThreatBig, ship.bridge);
                return;
            }
            string name = A.RandomElement() + " " + B.RandomElement();
            ship.state.name = name + " (" + kind + ", " + cls + ")";
            NameTriple nt = captain?.Name as NameTriple;
            reg.ships.Add(new NamedShip { name = name, captainFirst = nt?.First, captainNick = nt?.Nick, captainLast = nt?.Last, factionId = faction.loadID, tier = tier });
            if (reg.ships.Count > 60) reg.ships.RemoveAll(n => !n.alive);
        }

        private static NamedShip Lookup(RimverseShip ship)
        {
            GameComponent_EnemyRegistry reg = GameComponent_EnemyRegistry.Instance;
            string label = ship?.state?.name;
            if (reg == null || label.NullOrEmpty()) return null;
            return reg.ships.FirstOrDefault(n => label.StartsWith(n.name));
        }

        public static void Escaped(RimverseShip ship)
        {
            NamedShip n = Lookup(ship);
            if (n != null) n.escapes++;
        }

        public static void Destroyed(RimverseShip ship)
        {
            NamedShip n = Lookup(ship);
            if (n != null) n.alive = false;
        }

        public static RaiderKind Pick(Map map)
        {
            GameComponent_Civilizations gc = GameComponent_Civilizations.Instance;
            List<Civilization> enemies = gc?.generated == true ? gc.Known.Where(c => c.playerState == DiploState.War).ToList() : new List<Civilization>();
            if (enemies.Any(c => (c.government?.defName ?? "").Contains("Junta")) && Rand.Chance(0.35f)) return RaiderKind.Junta;
            if (enemies.Any(c => (c.ideology?.defName ?? "").Contains("ReligiousZeal") || (c.ideology?.defName ?? "").Contains("VoidFaith")) && Rand.Chance(0.35f)) return RaiderKind.Fanatics;
            if (enemies.Count > 0 && Rand.Chance(0.25f)) return RaiderKind.Mercenaries;
            RimverseShip ship = ColonyUtility.PlayerShipOn(map);
            if (ship != null && ship.Signature > 0.7f && Rand.Chance(0.25f)) return RaiderKind.BountyHunters;
            return RaiderKind.Pirates;
        }
    }
}
