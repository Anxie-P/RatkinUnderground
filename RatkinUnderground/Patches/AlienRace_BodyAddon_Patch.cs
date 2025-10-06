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
            Log.Message("[RKU] AlienRace patch 初始化完成");
        }
    }

    // 在BodyAddon的GraphicFor方法中替换Graphic
    [HarmonyPatch]
    public static class BodyAddon_GraphicFor_Patch
    {
        static MethodBase TargetMethod()
        {
            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                if (assembly.GetName().Name.Contains("AlienRace") || assembly.GetName().Name.Contains("NewRatkin"))
                {
                    var type = assembly.GetType("AlienRace.AlienPawnRenderNode_BodyAddon");
                    if (type != null)
                    {
                        var method = type.GetMethod("GraphicFor", new Type[] { typeof(Pawn) });
                        if (method != null)
                        {
                            return method;
                        }
                    }
                }
            }
            return null;
        }

        [HarmonyPostfix]
        public static void GraphicFor_Postfix(object __instance, Pawn pawn, ref Graphic __result)
        {
            if (__result == null || pawn == null || pawn.def.defName != "Ratkin") return;

            // 检查pawn是否有特殊外观hediff
            bool hasHediff = pawn.health?.hediffSet?.HasHediff(DefOfs.RKU_SpecialNPCAppearance) ?? false;

            if (!hasHediff) return;

            // 获取AlienPawnRenderNode_BodyAddon实例（通过反射，因为类型在运行时解析）
            var bodyAddonType = __instance.GetType();
            if (!bodyAddonType.Name.Contains("BodyAddon")) return;

            // 获取props中的addon信息
            var propsField = bodyAddonType.GetField("props", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (propsField == null) return;

            var props = propsField.GetValue(__instance);
            if (props == null) return;

            // 获取addon属性
            var addonField = props.GetType().GetField("addon", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (addonField == null) return;

            var addon = addonField.GetValue(props);
            if (addon == null) return;

            // 检查addon的path
            var pathField = addon.GetType().GetField("path", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (pathField == null) return;

            string addonPath = pathField.GetValue(addon) as string;
            Log.Message($"[RKU] BodyAddon检查 - pawn: {pawn.Name}, hasHediff: {hasHediff}, addonPath: '{addonPath}', GraphicType: {__result.GetType().Name}");

            string targetPath = null;
            if (addonPath.Contains("Body/RK_Ear") || addonPath.Contains("RK_Ear"))
            {
                targetPath = "Things/Commander/Ear";
            }
            else if (addonPath.Contains("Body/RK_Tail") || addonPath.Contains("RK_Tail"))
            {
                targetPath = "Things/Commander/Tail";
            }

            if (targetPath != null && targetPath != addonPath)
            {
                var graphicProperty = bodyAddonType.GetProperty("graphic", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (graphicProperty != null)
                {
                    Graphic currentGraphic = graphicProperty.GetValue(__instance) as Graphic;
                    if (currentGraphic != null && currentGraphic != __result)
                    {
                        __result = currentGraphic;
                        return;
                    }
                }

                Graphic replacementGraphic = null;

                try
                {
                    replacementGraphic = GraphicDatabase.Get<Graphic_Multi>(targetPath, __result.Shader, __result.drawSize, __result.color, __result.colorTwo);
                }
                catch
                {
                    try
                    {
                        replacementGraphic = GraphicDatabase.Get<Graphic_Single>(targetPath, __result.Shader, __result.drawSize, __result.color, __result.colorTwo);
                    }
                    catch (Exception ex)
                    {
                        return;
                    }
                }

                if (replacementGraphic != null)
                {
                    if (graphicProperty != null)
                    {
                        graphicProperty.SetValue(__instance, replacementGraphic);
                    }

                    __result = replacementGraphic;
                }
            }
        }
    }
}
