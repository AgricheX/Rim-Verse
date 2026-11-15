using System.Collections.Generic;
using Verse;

namespace Rimverse
{
    public class QolFeatureDef : Def
    {
        public string category = "General";
        public bool enabledByDefault = true;
        public List<string> disableIfModActive = new List<string>();
        public bool requiresRestart;

        public bool OriginalModActive => ModCompat.AnyActive(disableIfModActive);

        public bool Enabled
        {
            get
            {
                if (OriginalModActive)
                {
                    return false;
                }
                RimverseSettings settings = RimverseMod.Settings;
                if (settings == null || !settings.qolEnabled)
                {
                    return false;
                }
                return settings.IsQolEnabled(this);
            }
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }
            if (category.NullOrEmpty())
            {
                yield return "category is empty";
            }
        }
    }
}
