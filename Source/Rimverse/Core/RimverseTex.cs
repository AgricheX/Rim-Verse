using UnityEngine;
using Verse;

namespace Rimverse
{
    [StaticConstructorOnStartup]
    public static class RimverseTex
    {
        public static readonly Texture2D Launch = ContentFinder<Texture2D>.Get("UI/Commands/LaunchShip");
        public static readonly Texture2D Land = ContentFinder<Texture2D>.Get("UI/Commands/LaunchShip");
        public static readonly Texture2D ShipPanel = ContentFinder<Texture2D>.Get("UI/Commands/LaunchShip");
        public static readonly Texture2D Copy = ContentFinder<Texture2D>.Get("UI/Commands/CopySettings", false) ?? BaseContent.BadTex;
        public static readonly Texture2D Paste = ContentFinder<Texture2D>.Get("UI/Commands/PasteSettings", false) ?? BaseContent.BadTex;
    }
}
