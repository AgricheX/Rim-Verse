using HarmonyLib;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public class RimverseMod : Mod
    {
        public const string PackageId = "agrichex.rimverse";
        public static RimverseMod Instance;
        public static RimverseSettings Settings;
        public static Harmony Harmony;

        public RimverseMod(ModContentPack content) : base(content)
        {
            Instance = this;
            Settings = GetSettings<RimverseSettings>();
            Harmony = new Harmony(PackageId);
            Harmony.PatchAll(typeof(RimverseMod).Assembly);
            RimverseLog.Message("Rimverse " + content.ModMetaData.ModVersion + " loaded.");
        }

        public override string SettingsCategory() => "Rimverse";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            RimverseSettingsWindow.Draw(inRect, Settings);
        }
    }
}
