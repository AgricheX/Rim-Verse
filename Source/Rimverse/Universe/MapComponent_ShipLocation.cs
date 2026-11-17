using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public class MapComponent_ShipLocation : MapComponent
    {
        public string systemKey;
        public int bodyIndex = -1;
        public int destinationBody = -1;
        public int departTick = -1;
        public int arriveTick = -1;
        public float travelAU;
        public int travelingShipId = -1;
        public bool surface;
        public bool surfaceSet;
        public string destinationSystemKey;
        public InterstellarMode travelMode;
        public float travelYears;

        private bool atmosphereCached;
        private AtmosphereType atmosphereCache;
        private bool temperatureCached;
        private bool hasBodyTemperature;
        private float bodyTemperature;
        private float temperatureSwing;

        public static readonly Dictionary<int, MapComponent_ShipLocation> ByMapId = new Dictionary<int, MapComponent_ShipLocation>();

        public MapComponent_ShipLocation(Map map) : base(map)
        {
            if (!EndlessRoomsCompat.IsBackrooms(map)) ByMapId[map.uniqueID] = this;
        }

        public override void FinalizeInit()
        {
            ByMapId[map.uniqueID] = this;
            if (systemKey.NullOrEmpty() && GameComponent_OdysseyLayers.TryLinkedBody(map, out BodyData linked, out bool linkedSurface))
            {
                StarSystemData home = GameComponent_Universe.Instance?.HomeSystem;
                if (home != null)
                {
                    SetLocation(home.id, linked.index);
                    SetSurface(linkedSurface);
                }
            }
            InvalidateCaches();
        }

        public override void MapRemoved()
        {
            ByMapId.Remove(map.uniqueID);
            EndlessRoomsCompat.Forget(map);
        }

        public void InvalidateCaches()
        {
            atmosphereCached = false;
            temperatureCached = false;
        }

        public AtmosphereType OutsideAtmosphere
        {
            get
            {
                if (atmosphereCached) return atmosphereCache;
                atmosphereCached = true;
                if (SpaceMapUtility.IsSpace(map) || !OnSurface) atmosphereCache = AtmosphereType.None;
                else
                {
                    BodyData b = Body;
                    atmosphereCache = b == null || b.homeWorld ? AtmosphereType.Breathable : b.atmosphere;
                }
                return atmosphereCache;
            }
        }

        public bool TryGetBodyTemperature(out float baseTemp, out float swing)
        {
            if (!temperatureCached)
            {
                temperatureCached = true;
                hasBodyTemperature = false;
                if (map.IsPocketMap && OnSurface)
                {
                    BodyData b = Body;
                    if (b != null && !b.homeWorld)
                    {
                        hasBodyTemperature = true;
                        bodyTemperature = b.temperatureC;
                        switch (b.atmosphere)
                        {
                            case AtmosphereType.None: temperatureSwing = 40f; break;
                            case AtmosphereType.Thin: temperatureSwing = 20f; break;
                            case AtmosphereType.Dense: temperatureSwing = 3f; break;
                            default: temperatureSwing = 7f; break;
                        }
                    }
                }
            }
            baseTemp = bodyTemperature;
            swing = temperatureSwing;
            return hasBodyTemperature;
        }

        public static MapComponent_ShipLocation For(Map map) => map == null || EndlessRoomsCompat.IsBackrooms(map) ? null : map.GetComponent<MapComponent_ShipLocation>();

        public bool Traveling => arriveTick > 0 && (destinationBody >= 0 || Warping);

        public bool Warping => !destinationSystemKey.NullOrEmpty();

        public bool WarpArrivalDue => Warping && arriveTick > 0 && Find.TickManager.TicksGame >= arriveTick;

        public StarSystemId SystemId
        {
            get
            {
                EnsureLocation();
                return StarSystemId.FromKey(systemKey);
            }
        }

        public StarSystemData System => GameComponent_Universe.Instance?.GetSystem(SystemId);

        public BodyData Body
        {
            get
            {
                EnsureLocation();
                return System?.Body(bodyIndex);
            }
        }

        public void EnsureLocation()
        {
            if (!systemKey.NullOrEmpty() && bodyIndex >= 0) return;
            GameComponent_Universe u = GameComponent_Universe.Instance;
            if (u == null) return;
            StarSystemData home = u.HomeSystem;
            systemKey = home.id.Key;
            bodyIndex = home.HomeWorld?.index ?? 0;
        }

        public bool OnSurface
        {
            get
            {
                if (!surfaceSet)
                {
                    surface = !SpaceMapUtility.IsSpace(map);
                    surfaceSet = true;
                }
                return surface;
            }
        }

        public void SetSurface(bool value)
        {
            surface = value;
            surfaceSet = true;
            InvalidateCaches();
        }

        public void SetLocation(StarSystemId id, int body)
        {
            systemKey = id.Key;
            bodyIndex = body;
            InvalidateCaches();
            destinationBody = -1;
            destinationSystemKey = null;
            arriveTick = -1;
            departTick = -1;
        }

        public float Progress
        {
            get
            {
                if (!Traveling || arriveTick <= departTick) return 0f;
                return Mathf.Clamp01((Find.TickManager.TicksGame - departTick) / (float)(arriveTick - departTick));
            }
        }

        public override void MapComponentTick()
        {
            if (!Traveling || Warping || Find.TickManager.TicksGame < arriveTick) return;
            Arrive();
        }

        private void Arrive()
        {
            int dest = destinationBody;
            bodyIndex = dest;
            InvalidateCaches();
            destinationBody = -1;
            arriveTick = -1;
            departTick = -1;
            RimverseShip ship = map.GetComponent<MapComponent_RimverseShips>()?.ShipById(travelingShipId);
            travelingShipId = -1;
            if (ship != null)
            {
                ship.state.thrusting = false;
                map.GetComponent<MapComponent_RimverseShips>().Dirty();
            }
            GameComponent_Universe u = GameComponent_Universe.Instance;
            BodyData body = Body;
            if (u != null && body != null)
            {
                u.SetScan(SystemId, body.index, ship != null && ship.sensorSensitivity > 0f && ship.sensorSensitivity <= 100f ? ScanLevel.Exact : ScanLevel.Range);
                Find.LetterStack.ReceiveLetter("Rimverse_LetterArrivedLabel".Translate(), "Rimverse_LetterArrived".Translate(ship?.Label ?? map.Parent.LabelCap, body.name), LetterDefOf.NeutralEvent, ship?.bridge);
            }
        }

        public float surfaceLat = 999f;
        public float surfaceLon;

        public string SurfaceCutLabel
        {
            get
            {
                BodyData b = Body;
                if (b == null || !OnSurface) return null;
                if (surfaceLat > 900f)
                {
                    UniverseRand r = new UniverseRand(UniverseRand.Hash(map.Tile.tileId, map.uniqueID, bodyIndex, 5150));
                    surfaceLat = (float)(global::System.Math.Asin(r.ValueDouble * 2.0 - 1.0) * 180.0 / global::System.Math.PI);
                    surfaceLon = (float)(r.ValueDouble * 360.0 - 180.0);
                }
                string lat = Mathf.Abs(surfaceLat).ToString("0.0") + (surfaceLat >= 0f ? "N" : "S");
                string lon = Mathf.Abs(surfaceLon).ToString("0.0") + (surfaceLon >= 0f ? "E" : "W");
                return "Rimverse_SurfaceCut".Translate(lat, lon, (2f * Mathf.PI * b.RadiusKm).ToString("N0"));
            }
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref surfaceLat, "surfaceLat", 999f);
            Scribe_Values.Look(ref surfaceLon, "surfaceLon");
            Scribe_Values.Look(ref systemKey, "systemKey");
            Scribe_Values.Look(ref bodyIndex, "bodyIndex", -1);
            Scribe_Values.Look(ref destinationBody, "destinationBody", -1);
            Scribe_Values.Look(ref departTick, "departTick", -1);
            Scribe_Values.Look(ref arriveTick, "arriveTick", -1);
            Scribe_Values.Look(ref travelAU, "travelAU");
            Scribe_Values.Look(ref travelingShipId, "travelingShipId", -1);
            Scribe_Values.Look(ref surface, "surface");
            Scribe_Values.Look(ref surfaceSet, "surfaceSet");
            Scribe_Values.Look(ref destinationSystemKey, "destinationSystemKey");
            Scribe_Values.Look(ref travelMode, "travelMode");
            Scribe_Values.Look(ref travelYears, "travelYears");
        }
    }
}
