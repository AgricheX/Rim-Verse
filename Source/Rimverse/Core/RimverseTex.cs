using UnityEngine;
using Verse;

namespace Rimverse
{
    [StaticConstructorOnStartup]
    public static class RimverseTex
    {
        public static readonly Texture2D Launch = Get("Rimverse/UI/Commands/Launch", "UI/Commands/LaunchShip");
        public static readonly Texture2D Land = Get("Rimverse/UI/Commands/Land", "UI/Commands/LaunchShip");
        public static readonly Texture2D ShipPanel = Get("Rimverse/UI/Commands/ShipPanel", "UI/Commands/LaunchShip");
        public static readonly Texture2D Copy = Get("Rimverse/UI/Commands/Copy", "UI/Commands/CopySettings");
        public static readonly Texture2D Paste = Get("Rimverse/UI/Commands/Paste", "UI/Commands/PasteSettings");
        public static readonly Texture2D HeatDump = Get("Rimverse/UI/Commands/HeatDump", "UI/Commands/DesirePower");
        public static readonly Texture2D Vent = Get("Rimverse/UI/Commands/Vent", "UI/Commands/Attack");
        public static readonly Texture2D Lockdown = Get("Rimverse/UI/Commands/Lockdown", "UI/Commands/Forbidden");
        public static readonly Texture2D Board = Get("Rimverse/UI/Commands/Board", "UI/Commands/LaunchShip");
        public static readonly Texture2D Warp = Get("Rimverse/UI/Commands/Warp", "UI/Commands/LaunchShip");
        public static readonly Texture2D GalaxyMap = Get("Rimverse/UI/Commands/GalaxyMap", "UI/Commands/LaunchShip");

        private static Texture2D Get(string path, string fallback)
        {
            return ContentFinder<Texture2D>.Get(path, false) ?? ContentFinder<Texture2D>.Get(fallback, false) ?? BaseContent.BadTex;
        }
    }
}
