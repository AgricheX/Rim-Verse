using UnityEngine;
using Verse;

namespace Rimverse
{
    [StaticConstructorOnStartup]
    public static class MapIcons
    {
        public static readonly Texture2D ShipTin = Load("ShipTin");
        public static readonly Texture2D ShipCivil = Load("ShipCivil");
        public static readonly Texture2D ShipMilitary = Load("ShipMilitary");
        public static readonly Texture2D ShipCapital = Load("ShipCapital");
        public static readonly Texture2D Station = Load("Station");
        public static readonly Texture2D Outpost = Load("Outpost");
        public static readonly Texture2D PirateBase = Load("PirateBase");
        public static readonly Texture2D Colony = Load("Colony");
        public static readonly Texture2D CivWorld = Load("CivWorld");
        public static readonly Texture2D Fleet = Load("Fleet");
        public static readonly Texture2D Wreck = Load("Wreck");
        public static readonly Texture2D Anomaly = Load("Anomaly");

        private static Texture2D Load(string name) => ContentFinder<Texture2D>.Get("Rimverse/UI/MapIcons/" + name, false) ?? BaseContent.WhiteTex;

        public static Texture2D ForShip(ShipClass c)
        {
            switch (c)
            {
                case ShipClass.Capital: return ShipCapital;
                case ShipClass.Military: return ShipMilitary;
                case ShipClass.Civil: return ShipCivil;
                case ShipClass.Station: return Station;
                default: return ShipTin;
            }
        }

        public static Texture2D ForMegastructure(MegastructureData m)
        {
            if (m.kind == MegastructureKind.PirateBase) return PirateBase;
            if (m.kind == MegastructureKind.CivStation) return Station;
            return MegastructureRender.Icon(m);
        }

        public static void Draw(Rect r, Texture2D tex, Color color, float angle = 0f)
        {
            GUI.color = color;
            if (angle == 0f) GUI.DrawTexture(r, tex);
            else Widgets.DrawTextureRotated(r, tex, angle);
            GUI.color = Color.white;
        }
    }
}
