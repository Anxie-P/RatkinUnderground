using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using System.Collections.Generic;
using System.Reflection;
using AlienRace;
using System;

namespace RatkinUnderground
{
    [StaticConstructorOnStartup]
    public static class AlienRacePatchInit
    {
        static AlienRacePatchInit()
        {
        }
    }

    // 在BodyAddon的GraphicFor方法中替换Graphic
    [HarmonyPatch(typeof(AlienPawnRenderNode_BodyAddon), "GraphicFor")]
    public static class BodyAddon_GraphicFor_Patch
    {
        [HarmonyPostfix]
        public static void GraphicFor_Postfix(AlienPawnRenderNode_BodyAddon __instance, Pawn pawn, ref Graphic __result)
        {
            if (__result == null || pawn == null || pawn.def.defName != "Ratkin")
            {
                return;
            }

            // 检查pawn背景和头发
            bool hasBG = pawn.story.Adulthood == DefDatabase<AlienRace.AlienBackstoryDef>.GetNamed("RKU_GuerrillaAR");
            if (hasBG)
            {
                hasBG = pawn.story.hairDef == DefDatabase<HairDef>.GetNamed("RKU_CommanderHair");
            }
            if (!hasBG)
            {
                return;
            }

            // 获取当前的graphic路径
            string currentPath = __result?.path;
            if (string.IsNullOrEmpty(currentPath))
            {
                return;
            }

            string replacementPath = null;

            if (currentPath.Contains("Body/RK_Ear"))
            {
                replacementPath = currentPath.Replace("Body/RK_Ear", "Things/Commander/RK_Ear");
            }
            else if (currentPath.Contains("Body/RK_Tail"))
            {
                replacementPath = currentPath.Replace("Body/RK_Tail", "Things/Commander/Tail");
            }

            if (replacementPath != null && replacementPath != currentPath)
            {
                // 创建新的graphic
                try
                {
                    Graphic newGraphic = GraphicDatabase.Get<Graphic_Multi>(replacementPath, __result.Shader, __result.drawSize, __result.color, __result.colorTwo);
                    __result = newGraphic;
                }
                catch
                {
                    try
                    {
                        Graphic newGraphic = GraphicDatabase.Get<Graphic_Single>(replacementPath, __result.Shader, __result.drawSize, __result.color, __result.colorTwo);
                        __result = newGraphic;
                    }
                    catch (Exception ex)
                    {
                    }
                }
            }
        }
    }
}
