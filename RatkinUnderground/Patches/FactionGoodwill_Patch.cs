using HarmonyLib;
using RimWorld;
using Verse;

namespace RatkinUnderground.Patches
{
    [HarmonyPatch(typeof(Faction), "TryAffectGoodwillWith")]
    public class FactionGoodwill_Patch
    {
        private static void Postfix(Faction __instance, Faction other, int goodwillChange, bool __result)
        {
            if (__result && (__instance.IsPlayer || other.IsPlayer) &&
                ((__instance.def.defName == "RKU_Faction" && other.IsPlayer) ||
                 (__instance.IsPlayer && other.def.defName == "RKU_Faction")))
            {
                var radioComponent = Current.Game.GetComponent<RKU_RadioGameComponent>();
                if (radioComponent != null)
                {
                    Faction rkuFaction = Find.FactionManager.FirstFactionOfDef(DefOfs.RKU_Faction);
                    Faction playerFaction = Faction.OfPlayer;

                    if (rkuFaction != null && playerFaction != null)
                    {
                        int actualGoodwill = rkuFaction.GoodwillWith(playerFaction);
                        radioComponent.ralationshipGrade = actualGoodwill;
                    }
                }
            }
        }
    }
}
