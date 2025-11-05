using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RatkinUnderground;

[StaticConstructorOnStartup]
[HarmonyPatch(typeof(Pawn_ApparelTracker), nameof(Pawn_ApparelTracker.GetGizmos))]
public static class EquipmentTracker_Patch
{
    public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn_ApparelTracker __instance)
    {
        
        foreach (var gizmo in __result) yield return gizmo;

        foreach (var apparel in __instance.WornApparel)
        {
            if (apparel.def.defName.StartsWith("RKU_"))
            {
                foreach (var comp in apparel.AllComps)
                {
                    foreach (var extra in comp.CompGetGizmosExtra())
                    {
                        yield return extra;
                    }
                }
            }
        }
    }
}

// 这个是把武器上定义的gizmo返回到pawn身上的，现在没用了，但不知道以后有没有用
/*[StaticConstructorOnStartup]
[HarmonyPatch(typeof(Pawn_EquipmentTracker), nameof(Pawn_EquipmentTracker.GetGizmos))]
public static class EquipmentTracker_WeaponGizmosPatch
{
    public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn_EquipmentTracker __instance)
    {
        foreach (var gizmo in __result) yield return gizmo;

        foreach (var equip in __instance.AllEquipmentListForReading)
        {
            if (equip.def.defName.StartsWith("RKU_"))
            {
                foreach (var comp in equip.AllComps)
                {
                    foreach (var extra in comp.CompGetGizmosExtra())
                    {
                        yield return extra;
                    }
                }
            }
        }
    }
}*/
