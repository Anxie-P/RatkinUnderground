using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RatkinUnderground;

[HarmonyPatch(typeof(QualityUtility), nameof(QualityUtility.GenerateQualityCreatedByPawn))]
[HarmonyPatch(new[] { typeof(Pawn), typeof(SkillDef) })]
public static class MakeCube_Patch
{
    // 如果是动物建造，设置成极差
    public static bool Prefix(Pawn pawn, SkillDef relevantSkill, ref QualityCategory __result) 
    {
        if (pawn.RaceProps.Humanlike) return true;
        __result = QualityCategory.Awful;
        return false;
    }
}