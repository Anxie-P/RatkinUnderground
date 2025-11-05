using UnityEngine;
using Verse;

namespace RatkinUnderground
{
    public class RKU_ModSettings : ModSettings
    {
        public bool showOnlyCurrentDialogueMessages = true;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref showOnlyCurrentDialogueMessages, "showOnlyCurrentDialogueMessages", true);
        }
    }
}
