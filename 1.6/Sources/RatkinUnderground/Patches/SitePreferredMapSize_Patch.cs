using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RatkinUnderground.Patches
{
    [HarmonyPatch(typeof(Site), "PreferredMapSize", MethodType.Getter)]
    public class SitePreferredMapSize_Patch
    {
        private static void Postfix(Site __instance, ref IntVec3 __result)
        {
            foreach (var part in __instance.parts)
            {
                if (part.def.defName == "RatkinBioLab")
                {
                    __result = new IntVec3(250, 1, 250);
                    return;
                }
            }
        }
    }
}
