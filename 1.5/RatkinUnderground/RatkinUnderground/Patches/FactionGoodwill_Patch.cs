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
                    Faction rkuFaction = Utils.OfRKU;
                    Faction playerFaction = Faction.OfPlayer;

                    if (rkuFaction != null && playerFaction != null)
                    {
                        int actualGoodwill = rkuFaction.GoodwillWith(playerFaction);
                        radioComponent.ralationshipGrade = actualGoodwill;
                        if (actualGoodwill<=-25) {
                            FactionRelationKind kind2 = Utils.OfRKU.RelationWith(Faction.OfPlayer).kind;
                            Utils.OfRKU.RelationWith(Faction.OfPlayer).kind = FactionRelationKind.Hostile;
                            Faction.OfPlayer.RelationWith(Utils.OfRKU).kind = FactionRelationKind.Hostile;
                            Utils.OfRKU.Notify_RelationKindChanged(Faction.OfPlayer, kind2, false, "", TargetInfo.Invalid, out var sentLetter);
                            Faction.OfPlayer.Notify_RelationKindChanged(Utils.OfRKU, kind2, false, "", TargetInfo.Invalid, out sentLetter);
                        }
                    }
                }
            }
        }
    }

    // 拦截原版的自然goodwill调整机制，防止RKU与玩家的关系被重置
    [HarmonyPatch(typeof(Faction), "CheckReachNaturalGoodwill")]
    public class Faction_CheckReachNaturalGoodwill_Patch
    {
        private static bool Prefix(Faction __instance)
        {
            if (__instance.def.defName == "RKU_Faction")
            {
                Faction playerFaction = Faction.OfPlayer;
                if (playerFaction != null)
                {
                    FactionRelationKind relation = __instance.RelationKindWith(playerFaction);
                    if (relation == FactionRelationKind.Hostile)
                    {
                        Traverse.Create(__instance).Field("naturalGoodwillTimer").SetValue(0);
                        return false; 
                    }
                }
            }
            return true;
        }
    }
}
