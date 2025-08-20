using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RatkinUnderground
{
    [HarmonyPatch(typeof(SiteMakerHelper), nameof(SiteMakerHelper.FactionCanOwn), new System.Type[] { typeof(SitePartDef), typeof(Faction), typeof(bool), typeof(System.Predicate<Faction>) })]
    public static class SiteMakerHelper_FactionCanOwn_Patch
    {
        private static bool Prefix(SitePartDef sitePart, Faction faction, bool disallowNonHostileFactions, System.Predicate<Faction> extraFactionValidator, ref bool __result)
        {
            if (faction != null && faction.def.defName == "RKU_Faction")
            {
                // 如果是RKU_Faction，直接返回true，跳过所有原校验
                __result = true;
                return false; 
            }
            return true;
        }
    }
}