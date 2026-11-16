using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld.Planet;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Rimverse
{
    public static class SpaceBases
    {
        public static void Seed(GameComponent_Megastructures gc, StarSystemId id, string systemKey)
        {
            GameComponent_Universe u = GameComponent_Universe.Instance;
            StarSystemData sys = u?.GetSystem(id);
            if (sys == null || u.IsHome(id) && sys.bodies.Count == 0) return;
            UniverseRand r = new UniverseRand(UniverseRand.Hash(u.universeSeed, 970, systemKey.GetHashCode()));
            List<BodyData> bodies = sys.bodies.Where(b => !b.homeWorld).ToList();
            if (bodies.Count == 0) bodies = sys.bodies.ToList();
            GameComponent_Civilizations civs = GameComponent_Civilizations.Instance;
            Civilization owner = civs?.OwnerOf(id);
            if (owner != null && !owner.dead && !owner.Hostile && (owner.capitalKey == systemKey || r.Chance(0.6f)))
            {
                CivPlanet cp = owner.planets.FirstOrDefault(p => p.systemKey == systemKey);
                int body = cp?.body ?? (bodies.Count > 0 ? bodies[r.RangeInclusive(0, bodies.Count - 1)].index : -1);
                MegastructureData m = gc.Register(MegastructureKind.CivStation, systemKey, body, false, owner.id, MegastructureData.Stages);
                m.angle = r.Range(0f, 360f);
            }
            float pirateChance = owner != null && owner.Hostile ? 0.6f : owner != null ? 0.04f : 0.09f;
            if (!u.IsHome(id) && bodies.Count > 0 && r.Chance(pirateChance))
            {
                BodyData b = bodies.Where(x => x.type == BodyType.Belt || x.type == BodyType.Asteroid || x.IsMoon).RandomElementWithFallback(bodies[r.RangeInclusive(0, bodies.Count - 1)]);
                MegastructureData m = gc.Register(MegastructureKind.PirateBase, systemKey, b.index, false, owner != null && owner.Hostile ? owner.id : int.MinValue, MegastructureData.Stages);
                m.angle = r.Range(0f, 360f);
            }
        }

        public static Faction FactionOf(MegastructureData m)
        {
            Civilization c = m.Civ;
            if (c != null && !c.dead) return GameComponent_Civilizations.Instance.EnsureFaction(c);
            if (m.kind == MegastructureKind.PirateBase) return Find.FactionManager.FirstFactionOfDef(RimverseDefOf.Rimverse_SpacePirates) ?? Find.FactionManager.RandomEnemyFaction(false, false, true, TechLevel.Industrial);
            return null;
        }

        public static void Generate(Map map, MegastructureData m, MapComponent_MegaSection comp)
        {
            Faction f = m.ruin ? null : FactionOf(m);
            bool pirate = m.kind == MegastructureKind.PirateBase;
            BlueprintRequest req = new BlueprintRequest
            {
                layout = BlueprintLayout.Station,
                style = pirate ? BlueprintStyle.Pirate : f != null && f.def.techLevel >= TechLevel.Ultra ? BlueprintStyle.Imperial : BlueprintStyle.Civil,
                sizeClass = pirate ? 2 : 3,
                seed = m.seed,
                damage = m.ruin ? 0.5f : pirate ? 0.15f : 0f
            };
            req.Add("Rimverse_BridgeCommand").Add("Rimverse_FuelCell", 3).Add("Rimverse_AirGenerator", 2).Add("Rimverse_AirTank", 2).Add("Rimverse_RadiatorFixed", 3).Add("Rimverse_CargoBay", pirate ? 2 : 3).Add("Rimverse_HoldContainer", 3).Add("Bed", pirate ? 8 : 6).Add("Rimverse_ShipGalley").Add("Rimverse_ScannerLongRange");
            if (pirate) req.Add("Rimverse_Autocannon", 3).Add("Rimverse_MissileLauncher", 2).Add("Rimverse_ShieldGenerator");
            else req.Add("Rimverse_TradeTerminal").Add("Rimverse_HydroSemi", 3).Add("Rimverse_AutoMedPod").Add("Rimverse_ShipSolarPanel", 4);
            ShipBlueprint bp = ShipBlueprintGenerator.Generate(req);
            if (bp == null) return;
            IntVec3 center = new IntVec3(map.Size.x / 2, 0, Mathf.Max(bp.height / 2 + 4, 60));
            CellRect rect = ShipBlueprintBuilder.RectAt(center, bp).ClipInsideMap(map);
            BlueprintBuildResult res = ShipBlueprintBuilder.Build(map, ShipBlueprintBuilder.RectAt(center, bp), bp, new BlueprintBuildOptions { faction = f, fuel = 0.6f, hull = m.ruin ? 0.5f : 1f, powerOn = !m.ruin, clearArea = false });
            if (res == null) return;
            int top = res.interior.Count > 0 ? res.interior.Max(c => c.z) + 1 : rect.maxZ;
            List<IntVec3> topWalls = rect.Cells.Where(c => c.z == top && c.GetEdifice(map) != null && !(c.GetEdifice(map) is Building_Door) && (c + IntVec3.South).InBounds(map) && res.interior.Contains(c + IntVec3.South)).OrderBy(c => Mathf.Abs(c.x - center.x)).ToList();
            if (topWalls.Count > 0)
            {
                IntVec3 lockCell = topWalls[0];
                lockCell.GetEdifice(map).Destroy(DestroyMode.Vanish);
                ShipBlueprintBuilder.Spawn(map, DefDatabase<ThingDef>.GetNamedSilentFail("Rimverse_Airlock") ?? ThingDefOf.Door, lockCell, f, Rot4.North, null);
            }
            comp.bandMinZ = rect.minZ;
            comp.bandMaxZ = top;
            AnomalyContent.Loot(map, res.RandomCellsIn(RoomRole.Cargo, 12), m.ruin ? 2500f : pirate ? 1800f : 600f);
            if (f != null && !m.ruin) Garrison(map, m, f, res, pirate);
        }

        private static void Garrison(Map map, MegastructureData m, Faction f, BlueprintBuildResult res, bool pirate)
        {
            float points = pirate ? 900f + 400f * Rand.Value : 500f;
            List<Pawn> pawns = new List<Pawn>();
            try
            {
                pawns.AddRange(PawnGroupMakerUtility.GeneratePawns(new PawnGroupMakerParms
                {
                    groupKind = pirate ? PawnGroupKindDefOf.Combat : PawnGroupKindDefOf.Settlement,
                    tile = map.Tile,
                    faction = f,
                    points = points,
                    generateFightersOnly = pirate
                }, false));
            }
            catch (System.Exception e)
            {
                RimverseLog.Warning("Base garrison failed: " + e.Message);
            }
            if (pawns.Count == 0 && f.def.basicMemberKind != null)
            {
                for (int i = 0; i < 4; i++) pawns.Add(PawnGenerator.GeneratePawn(f.def.basicMemberKind, f));
            }
            while (pawns.Count > 14)
            {
                Find.WorldPawns.PassToWorld(pawns[pawns.Count - 1], PawnDiscardDecideMode.Discard);
                pawns.RemoveAt(pawns.Count - 1);
            }
            int i2 = 0;
            foreach (Pawn p in pawns)
            {
                RoomRole role = (i2++ % 4) == 0 ? RoomRole.Bridge : RoomRole.Corridor;
                GenSpawn.Spawn(p, res.RandomCellIn(role, res.rect.CenterCell), map);
            }
            if (pawns.Count > 0)
            {
                IntVec3 heart = res.bridge != null ? res.bridge.Position : res.rect.CenterCell;
                LordMaker.MakeNewLord(f, new LordJob_DefendBase(f, heart, 25000), map, pawns);
            }
        }

        public static void OnDocked(Map map, MegastructureData m)
        {
            if (m.kind != MegastructureKind.CivStation || m.ruin) return;
            Faction f = FactionOf(m);
            if (f == null || f.HostileTo(Faction.OfPlayer)) return;
            TradeShip ts = map.passingShipManager.passingShips.OfType<TradeShip>().FirstOrDefault(s => s.Faction == f && !s.Departed);
            if (ts == null)
            {
                ts = new TradeShip(RimverseDefOf.Rimverse_CivSpaceTrader, f) { name = m.Civ?.Name ?? f.Name };
                map.passingShipManager.AddShip(ts);
            }
            foreach (Building_TradeTerminal t in map.listerThings.AllThings.OfType<Building_TradeTerminal>()) t.traderLoadId = ts.GetUniqueLoadID();
        }

        public static void CheckCleared(Map map)
        {
            MapComponent_MegaSection s = MapComponent_MegaSection.For(map);
            MegastructureData m = s?.Mega;
            if (m == null || m.kind != MegastructureKind.PirateBase || m.ruin) return;
            if (map.mapPawns.AllPawnsSpawned.Any(p => !p.Dead && !p.Downed && p.Faction != null && p.Faction.HostileTo(Faction.OfPlayer) && p.RaceProps.Humanlike)) return;
            m.ruin = true;
            MegastructureRender.Version++;
            Find.LetterStack.ReceiveLetter("Rimverse_LetterBaseCleared".Translate(), "Rimverse_LetterBaseClearedDesc".Translate(m.Label), LetterDefOf.PositiveEvent, new LookTargets(new TargetInfo(map.Center, map)));
        }
    }

    public class Building_TradeTerminal : Building
    {
        public string traderLoadId;

        public TradeShip Trader => Map?.passingShipManager.passingShips.OfType<TradeShip>().FirstOrDefault(s => s.GetUniqueLoadID() == traderLoadId && !s.Departed);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref traderLoadId, "traderLoadId");
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
        {
            foreach (FloatMenuOption o in base.GetFloatMenuOptions(selPawn)) yield return o;
            TradeShip ts = Trader;
            if (ts == null && Faction != null && Faction != Faction.OfPlayer && !Faction.HostileTo(Faction.OfPlayer))
            {
                ts = new TradeShip(RimverseDefOf.Rimverse_CivSpaceTrader, Faction) { name = Faction.Name };
                Map.passingShipManager.AddShip(ts);
                traderLoadId = ts.GetUniqueLoadID();
            }
            if (ts == null)
            {
                yield return new FloatMenuOption("Rimverse_TerminalNoTrader".Translate(), null);
                yield break;
            }
            if (!selPawn.CanReach(this, PathEndMode.InteractionCell, Danger.Some))
            {
                yield return new FloatMenuOption("CannotUseNoPath".Translate(), null);
                yield break;
            }
            if (selPawn.skills != null && selPawn.skills.GetSkill(SkillDefOf.Social).TotallyDisabled)
            {
                yield return new FloatMenuOption("CannotPrioritizeWorkTypeDisabled".Translate(SkillDefOf.Social.LabelCap), null);
                yield break;
            }
            yield return new FloatMenuOption("Rimverse_TerminalTrade".Translate(ts.FullTitle), () =>
            {
                Job job = JobMaker.MakeJob(RimverseTradeDefOf.Rimverse_UseTradeTerminal, this);
                selPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            });
            Civilization bankCiv = Currencies.CivOf(ts.Faction);
            if (bankCiv != null && bankCiv.stage >= 7 && !ResearchGate.Done("Rimverse_InterstellarBanking")) yield return new FloatMenuOption("Rimverse_TerminalBank".Translate(bankCiv.ShownName) + " (" + ResearchGate.Need("Rimverse_InterstellarBanking") + ")", null);
            else if (bankCiv != null && bankCiv.stage >= 7)
            {
                Building_TradeTerminal self = this;
                yield return new FloatMenuOption("Rimverse_TerminalBank".Translate(bankCiv.ShownName), () => Find.WindowStack.Add(new Window_Bank(bankCiv, self.Map, self.Position)));
            }
        }

        public override string GetInspectString()
        {
            string s = base.GetInspectString();
            TradeShip ts = Trader;
            string line = ts != null ? "Rimverse_TerminalLinked".Translate(ts.FullTitle).ToString() : "Rimverse_TerminalNoTrader".Translate().ToString();
            return s.NullOrEmpty() ? line : s + "\n" + line;
        }
    }

    [DefOf]
    public static class RimverseTradeDefOf
    {
        public static JobDef Rimverse_UseTradeTerminal;
        public static ThingDef Rimverse_TradeTerminal;

        static RimverseTradeDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(RimverseTradeDefOf));
        }
    }

    public class JobDriver_UseTradeTerminal : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);
            Toil open = ToilMaker.MakeToil("OpenTrade");
            open.initAction = () =>
            {
                Building_TradeTerminal t = job.targetA.Thing as Building_TradeTerminal;
                TradeShip ts = t?.Trader;
                if (ts == null) return;
                ts.TryOpenComms(pawn);
            };
            open.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return open;
        }
    }

    public class TraderDock : IExposable
    {
        public string traderLoadId;
        public List<Thing> things = new List<Thing>();
        public CellRect rect;

        public void ExposeData()
        {
            Scribe_Values.Look(ref traderLoadId, "traderLoadId");
            Scribe_Values.Look(ref rect, "rect");
            Scribe_Collections.Look(ref things, "things", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && things == null) things = new List<Thing>();
        }
    }

    public class MapComponent_TraderDocks : MapComponent
    {
        public List<TraderDock> docks = new List<TraderDock>();

        public MapComponent_TraderDocks(Map map) : base(map)
        {
        }

        public override void MapComponentTick()
        {
            if (Find.TickManager.TicksGame % 2000 == 77) SpaceBases.CheckCleared(map);
            if (Find.TickManager.TicksGame % 500 != 13 || docks.Count == 0) return;
            foreach (TraderDock d in docks.ToList())
            {
                if (!map.passingShipManager.passingShips.Any(s => s.GetUniqueLoadID() == d.traderLoadId && !s.Departed)) TraderDocking.Leave(map, d);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref docks, "docks", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && docks == null) docks = new List<TraderDock>();
        }
    }

    public static class TraderDocking
    {
        public static void Arrive(Map map, TradeShip ts)
        {
            if (map == null || ts == null || !SpaceMapUtility.IsRimverseSpace(map) || MegaSectionUtility.IsSection(map)) return;
            MapComponent_TraderDocks comp = map.GetComponent<MapComponent_TraderDocks>();
            if (comp == null || comp.docks.Any(d => d.traderLoadId == ts.GetUniqueLoadID())) return;
            RimverseShip player = ColonyUtility.PlayerShipOn(map);
            if (player == null) return;
            int size = ts.def.defName.Contains("Bulk") || Rand.Chance(0.35f) ? 3 : 2;
            BlueprintRequest req = new BlueprintRequest { style = BlueprintStyle.Civil, sizeClass = size, seed = ts.GetUniqueLoadID().GetHashCode() * 7919 + 13 };
            req.Add(size >= 3 ? "Rimverse_BridgeCommand" : "Rimverse_BridgeSmall").Add("Rimverse_ChemThruster", size + 1).Add("Rimverse_FuelCell", size).Add("Rimverse_AirGenerator").Add("Rimverse_AirTank").Add("Rimverse_ChemTank").Add("Rimverse_ManeuverThruster", 2).Add("Rimverse_RadiatorFixed", size).Add("Rimverse_CargoBay", size + 1).Add("Rimverse_HoldContainer", size * 2).Add("Rimverse_TradeTerminal").Add("Bed", 2).Add("Rimverse_ShipGalley");
            if (size >= 3) req.Add("Rimverse_Autocannon", 2).Add("Rimverse_ShieldGenerator");
            ShipBlueprint bp = ShipBlueprintGenerator.Generate(req);
            if (bp == null) return;
            if (!EnemyShipGenerator.TryFindRect(map, player, bp.width, bp.height, out CellRect rect, new[] { 6, 9, 14, 20 })) return;
            BlueprintBuildResult res = ShipBlueprintBuilder.Build(map, rect, bp, new BlueprintBuildOptions { faction = ts.Faction, clearArea = false });
            if (res == null) return;
            TraderDock dock = new TraderDock { traderLoadId = ts.GetUniqueLoadID(), rect = rect };
            dock.things.AddRange(res.things.Where(t => t != null && !t.Destroyed));
            foreach (Building_TradeTerminal t in res.things.OfType<Building_TradeTerminal>()) t.traderLoadId = ts.GetUniqueLoadID();
            if (!res.things.OfType<Building_TradeTerminal>().Any())
            {
                ThingDef td = DefDatabase<ThingDef>.GetNamedSilentFail("Rimverse_TradeTerminal");
                IntVec3 c = res.RandomCellIn(RoomRole.Cargo, rect.CenterCell);
                if (td != null && GenConstruct.CanPlaceBlueprintAt(td, c, Rot4.North, map).Accepted)
                {
                    Thing t = ShipBlueprintBuilder.Spawn(map, td, c, ts.Faction, Rot4.North, null);
                    if (t is Building_TradeTerminal tt) tt.traderLoadId = ts.GetUniqueLoadID();
                    if (t != null) dock.things.Add(t);
                }
            }
            comp.docks.Add(dock);
            DockingWalk(map, res, player);
            Messages.Message("Rimverse_MsgTraderDocked".Translate(ts.FullTitle), new LookTargets(new TargetInfo(rect.CenterCell, map)), MessageTypeDefOf.NeutralEvent, false);
        }

        private static void DockingWalk(Map map, BlueprintBuildResult res, RimverseShip player)
        {
            TerrainDef f = DefDatabase<TerrainDef>.GetNamedSilentFail("Rimverse_MegaFoundation");
            if (f == null) return;
            Building_Door theirs = res.things.OfType<Building_Door>().Where(d => !d.Destroyed).OrderBy(d => d.Position.DistanceToSquared(ShipMover.Bounds(player).CenterCell)).FirstOrDefault();
            Building_Door ours = player.cells.Select(c => c.GetEdifice(map) as Building_Door).Where(d => d != null).OrderBy(d => theirs == null ? 0 : d.Position.DistanceToSquared(theirs.Position)).FirstOrDefault();
            if (theirs == null || ours == null) return;
            IntVec3 cur = theirs.Position;
            IntVec3 goal = ours.Position;
            int guard = 0;
            while (cur != goal && guard++ < 200)
            {
                int dx = goal.x - cur.x, dz = goal.z - cur.z;
                cur += Mathf.Abs(dx) > Mathf.Abs(dz) ? new IntVec3(System.Math.Sign(dx), 0, 0) : new IntVec3(0, 0, System.Math.Sign(dz));
                if (cur == goal) break;
                if (map.terrainGrid.FoundationAt(cur) == null) map.terrainGrid.SetFoundation(cur, f);
            }
        }

        public static void Leave(Map map, TraderDock dock)
        {
            MapComponent_TraderDocks comp = map.GetComponent<MapComponent_TraderDocks>();
            comp?.docks.Remove(dock);
            RimverseShip player = ColonyUtility.PlayerShipOn(map);
            foreach (IntVec3 c in dock.rect.ExpandedBy(1))
            {
                if (!c.InBounds(map)) continue;
                foreach (Pawn p in c.GetThingList(map).OfType<Pawn>().ToList())
                {
                    if (p.Faction != Faction.OfPlayer || player == null) continue;
                    if (CellFinder.TryFindRandomCellNear(player.bridge.Position, map, 8, x => x.Standable(map) && player.cells.Contains(x), out IntVec3 dest)) p.Position = dest;
                    p.Notify_Teleported();
                }
            }
            foreach (Thing t in dock.things)
            {
                if (t != null && !t.Destroyed && t.Spawned) t.Destroy(DestroyMode.Vanish);
            }
            foreach (IntVec3 c in dock.rect)
            {
                if (!c.InBounds(map)) continue;
                foreach (Thing t in c.GetThingList(map).ToList())
                {
                    if (t is Building && t.Faction != Faction.OfPlayer && t.def.destroyable) t.Destroy(DestroyMode.Vanish);
                }
                if (c.GetFirstThing<Pawn>(map) != null || c.GetFirstItem(map) != null) continue;
                if (player != null && player.cells.Contains(c)) continue;
                map.roofGrid.SetRoof(c, null);
                if (map.terrainGrid.FoundationAt(c) != null) map.terrainGrid.RemoveFoundation(c, false);
            }
            map.GetComponent<MapComponent_RimverseShips>()?.Dirty();
            Messages.Message("Rimverse_MsgTraderLeft".Translate(), MessageTypeDefOf.NeutralEvent, false);
        }
    }

    [HarmonyPatch(typeof(PassingShipManager), nameof(PassingShipManager.AddShip))]
    public static class Patch_PassingShipManager_AddShip
    {
        public static void Postfix(PassingShipManager __instance, PassingShip vis)
        {
            if (!(vis is TradeShip ts) || Current.ProgramState != ProgramState.Playing) return;
            try
            {
                TraderDocking.Arrive(__instance.map, ts);
            }
            catch (System.Exception e)
            {
                RimverseLog.Warning("Trader docking failed: " + e.Message);
            }
        }
    }
}
