using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace Rimverse
{
    public static class ShipCombatUtility
    {
        public const float FleeSeconds = 20f;
        public const float BoardIntervalSeconds = 15f;

        public static IEnumerable<CompShipWeapon> Weapons(RimverseShip ship)
        {
            if (ship == null) yield break;
            foreach (CompShipPart p in ship.parts)
            {
                CompShipWeapon w = p.parent.GetComp<CompShipWeapon>();
                if (w != null) yield return w;
            }
        }

        public static IEnumerable<CompShipShield> Shields(RimverseShip ship)
        {
            if (ship == null) yield break;
            foreach (CompShipPart p in ship.parts)
            {
                CompShipShield s = p.parent.GetComp<CompShipShield>();
                if (s != null) yield return s;
            }
        }

        public static float ShieldPoints(RimverseShip ship)
        {
            float total = 0f;
            foreach (CompShipShield s in Shields(ship)) if (s.Online) total += s.hitPoints;
            return total;
        }

        public static float Accuracy(CombatBand weapon, CombatBand current)
        {
            int diff = Mathf.Abs((int)weapon - Mathf.Min((int)current, (int)CombatBand.Near)) + (current == CombatBand.Boarding && weapon != CombatBand.Near ? 1 : 0);
            switch (diff)
            {
                case 0: return 0.8f;
                case 1: return 0.5f;
                case 2: return 0.2f;
                default: return 0.05f;
            }
        }

        public static List<Pawn> LivingCrew(RimverseShip ship)
        {
            if (ship == null) return new List<Pawn>();
            return ShipCrewUtility.Crew(ship).Where(p => p.RaceProps.Humanlike || p.RaceProps.IsMechanoid).Where(p => !p.Downed && !p.Dead).ToList();
        }

        public static float GunnerFactor(RimverseShip ship)
        {
            int best = 0;
            foreach (Pawn p in LivingCrew(ship))
            {
                if (p.skills == null) { best = Mathf.Max(best, 8); continue; }
                SkillRecord s = p.skills.GetSkill(SkillDefOf.Shooting);
                if (!s.TotallyDisabled) best = Mathf.Max(best, s.Level);
            }
            return Mathf.Max(0.5f, 1f + 0.02f * (best - 10));
        }

        public static float Dodge(RimverseShip ship)
        {
            ShipCrewUtility.PilotAndCopilot(ship, out Pawn pilot, out _);
            return ShipCrewUtility.Dodge(ship, pilot);
        }

        public static float Jamming(RimverseShip ship)
        {
            float total = 0f;
            foreach (CompShipWeapon w in Weapons(ship)) if (w.Active) total += w.Props.jamming;
            return Mathf.Min(0.5f, total) + GameComponent_RimverseExtras.ExtraJam(ship);
        }

        public static bool Tractoring(RimverseShip ship, CombatBand band)
        {
            if (band < CombatBand.Near) return false;
            foreach (CompShipWeapon w in Weapons(ship)) if (w.Props.tractor && w.Active) return true;
            return false;
        }

        public static float BandChangeSeconds(RimverseShip ship)
        {
            if (ship == null || ship.thrust <= 0f) return 9999f;
            return Mathf.Max(10f, ship.mass / ship.thrust * 20f);
        }

        public static bool WarpReady(RimverseShip ship) => ship != null && ship.warpRangeLy > 0 && WarpTravel.CooldownTicksLeft(ship) == 0;

        private static int Dir(CombatOrder order, CombatBand band, CombatBand preferred)
        {
            switch (order)
            {
                case CombatOrder.Approach: return band < CombatBand.Near ? 1 : 0;
                case CombatOrder.Board: return band < CombatBand.Boarding ? 1 : 0;
                case CombatOrder.Flee: return -1;
                case CombatOrder.KeepRange: return preferred > band ? 1 : preferred < band ? -1 : 0;
                default: return 0;
            }
        }

        public static void Process(MapComponent_ShipCombat comp, ShipEncounter e)
        {
            Map map = comp.map;
            MapComponent_RimverseShips ships = map.GetComponent<MapComponent_RimverseShips>();
            RimverseShip player = ships.ShipById(e.playerShipId);
            RimverseShip enemy = ships.ShipById(e.enemyShipId);
            if (enemy == null || enemy.bridge == null || enemy.bridge.Destroyed)
            {
                Victory(comp, e, enemy);
                return;
            }
            if (player == null || player.bridge == null || player.bridge.Destroyed || player.bridge.Faction != Faction.OfPlayer)
            {
                comp.End(e);
                Find.LetterStack.ReceiveLetter("Rimverse_LetterCombatLostLabel".Translate(), "Rimverse_LetterCombatLost".Translate(), LetterDefOf.NegativeEvent);
                return;
            }
            if (enemy.bridge.Faction == Faction.OfPlayer)
            {
                comp.End(e);
                return;
            }
            int tick = Find.TickManager.TicksGame;
            if ((tick - e.startTick) % 300 < 60) EnemyDecide(e, player, enemy);
            MoveBands(comp, e, player, enemy);
            if (!comp.encounters.Contains(e)) return;
            FireAll(map, e, player, enemy);
            FireAll(map, e, enemy, player);
            HangarUtility.FighterSorties(map, e, player, enemy);
            if (e.band == CombatBand.Boarding && e.enemyOrder == CombatOrder.Board && tick - e.lastBoardTick >= BoardIntervalSeconds * 60f)
            {
                e.lastBoardTick = tick;
                EnemyBoard(map, e, player, enemy);
            }
            if ((tick - e.startTick) % 600 < 60) CheckSurrender(comp, e, player, enemy);
        }

        private static CombatBand PreferredBand(RimverseShip ship)
        {
            float[] dmg = new float[4];
            foreach (CompShipWeapon w in Weapons(ship))
            {
                if (w.Props.damage <= 0f) continue;
                dmg[(int)w.Props.band] += w.Props.damage * w.Props.burst / Mathf.Max(1f, w.Props.cooldownSeconds);
            }
            int best = 1;
            for (int i = 0; i < 3; i++) if (dmg[i] > dmg[best]) best = i;
            return (CombatBand)best;
        }

        private static void EnemyDecide(ShipEncounter e, RimverseShip player, RimverseShip enemy)
        {
            e.enemyPreferred = PreferredBand(enemy);
            if (enemy.Integrity < 0.35f && enemy.thrust > 0f && !e.militarist)
            {
                e.enemyOrder = CombatOrder.Flee;
                return;
            }
            int enemyCrew = LivingCrew(enemy).Count;
            int playerCrew = LivingCrew(player).Count;
            bool boarders = e.enemyFaction != null && (e.enemyFaction.def.permanentEnemy || e.enemyFaction == Faction.OfMechanoids);
            if (boarders && enemyCrew >= Mathf.Max(2, playerCrew) && (player.Integrity < 0.7f || ShieldPoints(player) <= 0f))
            {
                e.enemyOrder = CombatOrder.Board;
                return;
            }
            e.enemyOrder = CombatOrder.KeepRange;
        }

        private static void MoveBands(MapComponent_ShipCombat comp, ShipEncounter e, RimverseShip player, RimverseShip enemy)
        {
            int pd = Dir(e.playerOrder, e.band, e.band);
            int ed = Dir(e.enemyOrder, e.band, e.enemyPreferred);
            float ps = 1f / BandChangeSeconds(player);
            float es = 1f / BandChangeSeconds(enemy);
            int dir = 0;
            float speed = 0f;
            if (pd == ed) { dir = pd; speed = Mathf.Max(ps, es); }
            else if (pd == 0) { dir = ed; speed = es; }
            else if (ed == 0) { dir = pd; speed = ps; }
            else
            {
                if (ps > es) { dir = pd; speed = ps - es; }
                else if (es > ps) { dir = ed; speed = es - ps; }
            }
            if (dir > 0 && Tractoring(enemy, e.band) && pd < 0) dir = 0;
            if (dir < 0 && (pd < 0 && Tractoring(enemy, e.band) || ed < 0 && Tractoring(player, e.band))) dir = 0;
            if (dir != 0)
            {
                int next = (int)e.band + dir;
                if (next < 0 || next > (int)CombatBand.Boarding) e.bandProgress = 0f;
                else
                {
                    e.bandProgress += speed;
                    if (e.bandProgress >= 1f)
                    {
                        e.bandProgress = 0f;
                        e.band = (CombatBand)next;
                    }
                }
            }
            else e.bandProgress = Mathf.Max(0f, e.bandProgress - 0.05f);
            if (e.playerOrder == CombatOrder.Flee && e.band == CombatBand.Far && !Tractoring(enemy, e.band) && (ps >= es || WarpReady(player) || player.Signature < 0.5f)) e.playerFlee += 1f / Mathf.Clamp(player.Signature, 0.5f, 1f);
            else e.playerFlee = Mathf.Max(0f, e.playerFlee - 1f);
            if (e.enemyOrder == CombatOrder.Flee && e.band == CombatBand.Far && !Tractoring(player, e.band) && (es >= ps || WarpReady(enemy))) e.enemyFlee += 1f;
            else e.enemyFlee = Mathf.Max(0f, e.enemyFlee - 1f);
            if (e.playerFlee >= FleeSeconds)
            {
                RemoveShip(comp.map, enemy);
                comp.End(e);
                Messages.Message("Rimverse_MsgEscaped".Translate(player.Label), player.bridge, MessageTypeDefOf.PositiveEvent, false);
                return;
            }
            if (e.enemyFlee >= FleeSeconds)
            {
                RemoveShip(comp.map, enemy);
                comp.End(e);
                Messages.Message("Rimverse_MsgEnemyEscaped".Translate(enemy.Label), player.bridge, MessageTypeDefOf.NeutralEvent, false);
            }
        }

        private static void FireAll(Map map, ShipEncounter e, RimverseShip shooter, RimverseShip target)
        {
            float gunner = GunnerFactor(shooter);
            float dodge = Dodge(target);
            float jam = Jamming(target);
            float sig = Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(target.Signature));
            foreach (CompShipWeapon w in Weapons(shooter).ToList())
            {
                CompProperties_ShipWeapon p = w.Props;
                if (p.tractor || p.jamming > 0f || p.projectile == null) continue;
                if (p.interceptChance > 0f && p.damage <= 0f) continue;
                if (!w.Ready) continue;
                if (e.band == CombatBand.Boarding && p.band == CombatBand.Far) continue;
                float acc = Accuracy(p.band, e.band) * (1f - dodge) * gunner * (1f - jam) * sig;
                for (int i = 0; i < p.burst; i++) FireShot(map, w, shooter, target, Mathf.Clamp01(acc));
                shooter.state.heat += p.heatPerShot * p.burst;
                w.ConsumeShot();
            }
        }

        private static void FireShot(Map map, CompShipWeapon w, RimverseShip shooter, RimverseShip target, float hitChance)
        {
            CompProperties_ShipWeapon p = w.Props;
            IntVec3 aim = RandomCell(target);
            if (!aim.IsValid) return;
            if (p.missile && Rand.Chance(HangarUtility.FighterIntercept(target)))
            {
                Launch(map, p.projectile, w.parent, shooter, Midpoint(shooter, target, map));
                return;
            }
            if (p.missile)
            {
                foreach (CompShipWeapon pd in Weapons(target))
                {
                    if (pd.Props.interceptChance <= 0f || !pd.Active) continue;
                    if (Rand.Chance(pd.Props.interceptChance))
                    {
                        IntVec3 mid = Midpoint(shooter, target, map);
                        Launch(map, p.projectile, w.parent, shooter, mid);
                        return;
                    }
                }
            }
            if (!Rand.Chance(hitChance))
            {
                Launch(map, p.projectile, w.parent, shooter, MissCell(target, map));
                return;
            }
            float damage = p.damage * p.shieldMultiplier;
            float remaining = damage;
            foreach (CompShipShield s in Shields(target))
            {
                if (remaining <= 0f) break;
                remaining = s.Absorb(remaining);
            }
            if (remaining <= 0.01f)
            {
                IntVec3 edge = EdgeCellToward(target, shooter, map);
                FleckMaker.Static(edge.ToVector3Shifted(), map, FleckDefOf.ExplosionFlash, 4f);
                FleckMaker.ThrowLightningGlow(edge.ToVector3Shifted(), map, 2f);
                return;
            }
            if (p.onlyShields && ShieldPoints(target) > 0f) return;
            Launch(map, p.projectile, w.parent, shooter, aim);
        }

        public static CompShipWeapon BoardingTorpedo(RimverseShip ship, CombatBand band)
        {
            if (ship == null || band != CombatBand.Near) return null;
            foreach (CompShipWeapon w in Weapons(ship)) if (w.Props.boardingTorpedo && w.Ready) return w;
            return null;
        }

        public static IntVec3 RandomCell(RimverseShip ship)
        {
            if (ship == null || ship.cells.Count == 0) return IntVec3.Invalid;
            return ship.cells.RandomElement();
        }

        private static IntVec3 Midpoint(RimverseShip a, RimverseShip b, Map map)
        {
            IntVec3 ca = ShipMover.Bounds(a).CenterCell, cb = ShipMover.Bounds(b).CenterCell;
            return new IntVec3((ca.x + cb.x) / 2, 0, (ca.z + cb.z) / 2).ClampInsideMap(map);
        }

        private static IntVec3 MissCell(RimverseShip target, Map map)
        {
            CellRect r = ShipMover.Bounds(target).ExpandedBy(Rand.RangeInclusive(4, 10));
            IntVec3 c = r.EdgeCells.RandomElement();
            return c.ClampInsideMap(map);
        }

        private static IntVec3 EdgeCellToward(RimverseShip target, RimverseShip from, Map map)
        {
            IntVec3 center = ShipMover.Bounds(target).CenterCell;
            IntVec3 src = ShipMover.Bounds(from).CenterCell;
            IntVec3 last = center;
            foreach (IntVec3 c in GenSight.BresenhamCellsBetween(center, src))
            {
                if (!target.cells.Contains(c)) return c.ClampInsideMap(map);
                last = c;
            }
            return last;
        }

        public static void Launch(Map map, ThingDef projectileDef, Thing launcher, RimverseShip shooter, IntVec3 target)
        {
            IntVec3 origin = launcher.Position;
            foreach (IntVec3 c in GenSight.BresenhamCellsBetween(launcher.Position, target))
            {
                if (!shooter.cells.Contains(c))
                {
                    origin = c;
                    break;
                }
            }
            if (!origin.InBounds(map)) return;
            if (CombatExtendedCompat.IsCEProjectile(projectileDef))
            {
                CombatExtendedCompat.TryLaunch(projectileDef, launcher, origin, target);
                return;
            }
            if (projectileDef.thingClass == null || !typeof(Projectile).IsAssignableFrom(projectileDef.thingClass))
            {
                RimverseLog.ErrorOnce(projectileDef.defName + " is not a projectile and cannot be fired by a ship weapon.", "RimverseShipProj" + projectileDef.defName);
                return;
            }
            Projectile proj = (Projectile)GenSpawn.Spawn(projectileDef, origin, map);
            proj.Launch(launcher, origin.ToVector3Shifted(), new LocalTargetInfo(target), new LocalTargetInfo(target), ProjectileHitFlags.All);
        }

        private static void EnemyBoard(Map map, ShipEncounter e, RimverseShip player, RimverseShip enemy)
        {
            List<Pawn> boarders = LivingCrew(enemy).Where(p => p.Faction == e.enemyFaction && !p.InMentalState).Take(3).ToList();
            if (boarders.Count == 0) return;
            List<IntVec3> interior = player.cells.Where(c => c.Standable(map) && c.GetRoof(map) != null).ToList();
            if (interior.Count == 0) return;
            IntVec3 spot = interior.RandomElement();
            foreach (Pawn p in boarders)
            {
                p.GetLord()?.RemovePawn(p);
                p.DeSpawn();
            }
            LordMaker.MakeNewLord(e.enemyFaction, new LordJob_AssaultColony(e.enemyFaction, false, false, false, false, false), map, boarders);
            DropPodUtility.DropThingsNear(spot, map, boarders.Cast<Thing>(), 110, false, false, true, false, true, e.enemyFaction);
            Messages.Message("Rimverse_MsgBoarded".Translate(boarders.Count), new TargetInfo(spot, map), MessageTypeDefOf.ThreatBig, false);
        }

        public static void PlayerBoard(MapComponent_ShipCombat comp, ShipEncounter e)
        {
            Map map = comp.map;
            MapComponent_RimverseShips ships = map.GetComponent<MapComponent_RimverseShips>();
            RimverseShip player = ships.ShipById(e.playerShipId);
            RimverseShip enemy = ships.ShipById(e.enemyShipId);
            if (player == null || enemy == null) return;
            CompShipWeapon torpedo = e.band == CombatBand.Boarding ? null : BoardingTorpedo(player, e.band);
            if (e.band != CombatBand.Boarding && torpedo == null)
            {
                Messages.Message("Rimverse_MsgBoardNeedsBand".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            List<Pawn> team = map.mapPawns.FreeColonistsSpawned.Where(p => p.Drafted && player.cells.Contains(p.Position)).ToList();
            if (torpedo != null)
            {
                team = team.Take(4).ToList();
                if (team.Count > 0) torpedo.ConsumeShot();
            }
            if (team.Count == 0)
            {
                Messages.Message("Rimverse_MsgBoardNeedsDrafted".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            List<IntVec3> spots = enemy.cells.Where(c => c.Standable(map)).ToList();
            if (spots.Count == 0) return;
            IntVec3 spot = spots.RandomElement();
            foreach (Pawn p in team)
            {
                IntVec3 cell = CellFinder.RandomClosewalkCellNear(spot, map, 3, c => enemy.cells.Contains(c));
                p.DeSpawn();
                GenSpawn.Spawn(p, cell, map);
                p.drafter.Drafted = true;
                FleckMaker.ThrowDustPuff(cell, map, 1f);
            }
            Messages.Message("Rimverse_MsgPlayerBoarded".Translate(team.Count), new TargetInfo(spot, map), MessageTypeDefOf.PositiveEvent, false);
        }

        private static void CheckSurrender(MapComponent_ShipCombat comp, ShipEncounter e, RimverseShip player, RimverseShip enemy)
        {
            int crew = LivingCrew(enemy).Count(p => p.Faction == e.enemyFaction);
            if (crew == 0)
            {
                Victory(comp, e, enemy);
                return;
            }
            if (e.enemyFaction == Faction.OfMechanoids) return;
            bool beaten = enemy.Integrity < 0.25f || e.initialEnemyCrew > 0 && crew < e.initialEnemyCrew * 0.3f;
            if (!beaten) return;
            if (!Rand.Chance(e.militarist ? 0.05f : 0.3f)) return;
            Surrender(comp, e, player, enemy);
        }

        private static void Surrender(MapComponent_ShipCombat comp, ShipEncounter e, RimverseShip player, RimverseShip enemy)
        {
            Map map = comp.map;
            List<Pawn> crew = ShipCrewUtility.Crew(enemy).Where(p => p.Faction == e.enemyFaction && p.RaceProps.Humanlike).ToList();
            foreach (Pawn p in crew)
            {
                p.GetLord()?.RemovePawn(p);
                if (p.guest != null) p.guest.CapturedBy(Faction.OfPlayer);
            }
            foreach (CompShipPart part in enemy.parts) if (part.parent.Faction != Faction.OfPlayer) part.parent.SetFaction(Faction.OfPlayer);
            foreach (IntVec3 c in enemy.cells)
            {
                foreach (Thing t in c.GetThingList(map).ToList())
                {
                    if (t is Building b && b.Faction != null && b.Faction != Faction.OfPlayer) b.SetFaction(Faction.OfPlayer);
                }
            }
            comp.End(e);
            RimverseEvents.OnShipVictory(map, map.GetComponent<MapComponent_RimverseShips>().ShipById(e.playerShipId), true);
            map.GetComponent<MapComponent_RimverseShips>().Dirty();
            Find.LetterStack.ReceiveLetter("Rimverse_LetterSurrenderLabel".Translate(), "Rimverse_LetterSurrender".Translate(enemy.Label, crew.Count), LetterDefOf.PositiveEvent, enemy.bridge);
        }

        private static void Victory(MapComponent_ShipCombat comp, ShipEncounter e, RimverseShip enemy)
        {
            comp.End(e);
            RimverseEvents.OnShipVictory(comp.map, comp.map.GetComponent<MapComponent_RimverseShips>()?.ShipById(e.playerShipId));
            Find.LetterStack.ReceiveLetter("Rimverse_LetterCombatWonLabel".Translate(), "Rimverse_LetterCombatWon".Translate(), LetterDefOf.PositiveEvent);
        }

        public static void RemoveShip(Map map, RimverseShip ship)
        {
            if (ship == null) return;
            List<IntVec3> cells = ship.cells.ToList();
            foreach (IntVec3 c in cells)
            {
                foreach (Thing t in c.GetThingList(map).ToList())
                {
                    if (t is Pawn p)
                    {
                        if (p.Faction == Faction.OfPlayer) continue;
                        p.DeSpawn();
                        if (!p.Destroyed && !Find.WorldPawns.Contains(p)) Find.WorldPawns.PassToWorld(p);
                        continue;
                    }
                    if (t.def.destroyable) t.Destroy(DestroyMode.Vanish);
                }
                map.roofGrid.SetRoof(c, null);
                if (map.terrainGrid.FoundationAt(c) != null) map.terrainGrid.RemoveFoundation(c, false);
                if (map.terrainGrid.TerrainAt(c) != RimverseDefOf.Rimverse_Vacuum && SpaceMapUtility.IsRimverseSpace(map)) map.terrainGrid.SetTerrain(c, RimverseDefOf.Rimverse_Vacuum);
            }
            map.GetComponent<MapComponent_RimverseShips>().Dirty();
        }

        public static IEnumerable<Gizmo> Gizmos(RimverseShip ship)
        {
            MapComponent_ShipCombat comp = MapComponent_ShipCombat.For(ship.map);
            ShipEncounter e = comp?.EncounterOf(ship);
            if (e == null || e.playerShipId != ship.id) yield break;
            foreach (CombatOrder o in new[] { CombatOrder.Approach, CombatOrder.Hold, CombatOrder.Flee, CombatOrder.Board })
            {
                CombatOrder order = o;
                yield return new Command_Toggle
                {
                    defaultLabel = ("Rimverse_Order_" + order).Translate(),
                    defaultDesc = ("Rimverse_OrderDesc_" + order).Translate(),
                    icon = RimverseTex.ShipPanel,
                    isActive = () => e.playerOrder == order,
                    toggleAction = () => e.playerOrder = order
                };
            }
            Command_Action board = new Command_Action
            {
                defaultLabel = "Rimverse_CmdSendBoarders".Translate(),
                defaultDesc = "Rimverse_CmdSendBoardersDesc".Translate(),
                icon = RimverseTex.Board,
                action = () => PlayerBoard(comp, e)
            };
            if (e.band != CombatBand.Boarding && BoardingTorpedo(comp.map.GetComponent<MapComponent_RimverseShips>()?.ShipById(e.playerShipId), e.band) == null) board.Disable("Rimverse_MsgBoardNeedsBand".Translate());
            yield return board;
        }

        public static void MaybeAmbush(Map map, MapComponent_ShipCombat comp)
        {
            if (GameComponent_RimverseExtras.EscortActive) return;
            if (!RimverseMod.Settings.shipCombatEnabled || comp.InCombat) return;
            if (!SpaceMapUtility.IsRimverseSpace(map) || !SpaceMapUtility.IsOrbit(map)) return;
            MapComponent_ShipLocation loc = MapComponent_ShipLocation.For(map);
            if (loc == null || loc.Traveling) return;
            RimverseShip ship = map.GetComponent<MapComponent_RimverseShips>()?.Ships.FirstOrDefault(s => s.bridge != null && s.bridge.Faction == Faction.OfPlayer);
            if (ship == null) return;
            if (Find.TickManager.TicksGame < RimverseUnits.TicksPerDay * 10) return;
            GameComponent_Civilizations gc = GameComponent_Civilizations.Instance;
            Civilization owner = gc?.generated == true ? gc.OwnerOf(loc.SystemId) : null;
            float chance = 0.02f;
            if (owner != null && owner.Hostile) chance = 0.1f;
            else if (owner != null && owner.playerState >= DiploState.Peace) chance = 0.005f;
            chance *= RimverseMod.Settings.threatMultiplier;
            if (!Rand.Chance(chance)) return;
            Faction pirates = PirateFaction(gc);
            if (pirates == null) return;
            if (!Rand.Chance(Mathf.Lerp(0.3f, 1f, Mathf.Clamp01(ship.Signature))))
            {
                Messages.Message("Rimverse_MsgSignatureHid".Translate(ship.Label), ship.bridge, MessageTypeDefOf.PositiveEvent, false);
                return;
            }
            float points = StorytellerUtility.DefaultThreatPointsNow(map);
            StartEncounter(map, ship, pirates, points / 8f, TechLevel.Spacer, -2);
        }

        public static Faction PirateFaction(GameComponent_Civilizations gc)
        {
            Civilization civ = gc?.civilizations.FirstOrDefault(c => c.lore == LoreKind.Pirates && !c.dead);
            if (civ != null) return gc.EnsureFaction(civ);
            Faction f = Find.FactionManager.FirstFactionOfDef(RimverseDefOf.Rimverse_SpacePirates);
            if (f != null) return f;
            return Find.FactionManager.RandomEnemyFaction(false, false, true, TechLevel.Industrial);
        }

        public static bool StartEncounter(Map map, RimverseShip playerShip, Faction faction, float power, TechLevel tech, int civId)
        {
            MapComponent_ShipCombat comp = MapComponent_ShipCombat.For(map);
            if (comp == null || playerShip == null) return false;
            RimverseShip enemy = EnemyShipGenerator.Generate(map, playerShip, faction, power, tech, out int crewCount);
            if (enemy == null) return false;
            Civilization civ = civId >= 0 ? GameComponent_Civilizations.Instance?.Civ(civId) : null;
            ShipEncounter e = new ShipEncounter
            {
                playerShipId = playerShip.id,
                enemyShipId = enemy.id,
                enemyFaction = faction,
                civId = civId,
                startTick = Find.TickManager.TicksGame,
                lastBoardTick = Find.TickManager.TicksGame,
                initialEnemyCrew = crewCount,
                militarist = civ?.ideology?.profile == AiProfile.Warmonger || faction == Faction.OfMechanoids
            };
            comp.encounters.Add(e);
            Find.LetterStack.ReceiveLetter("Rimverse_LetterShipCombatLabel".Translate(), "Rimverse_LetterShipCombat".Translate(faction.Name, enemy.Label), LetterDefOf.ThreatBig, enemy.bridge);
            return true;
        }
    }
}
