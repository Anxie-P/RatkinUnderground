using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using System.Collections.Generic;
using System.Reflection;

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

    // 动态修改bodyAddon的path - 使用动态方法查找
    [HarmonyPatch]
    public static class BodyAddon_GetPath_Patch
    {
        // 动态查找GetPath方法
        static MethodBase TargetMethod()
        {
            // 查找所有包含"GetPath"的方法
            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                if (assembly.GetName().Name.Contains("AlienRace"))
                {
                    var types = assembly.GetTypes();
                    foreach (var type in types)
                    {
                        if (type.Name.Contains("BodyAddon"))
                        {
                            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            foreach (var method in methods)
                            {
                                if (method.Name.Contains("GetPath") && method.GetParameters().Length >= 1 &&
                                    method.GetParameters()[0].ParameterType == typeof(Pawn))
                                {
                                    Log.Message($"[RKU] 找到BodyAddon.GetPath方法: {type.FullName}.{method.Name}");
                                    return method;
                                }
                            }
                        }
                    }
                }
            }
            Log.Warning("[RKU] 未找到BodyAddon.GetPath方法");
            return null;
        }

        [HarmonyPrefix]
        public static bool GetPath_Prefix(object __instance, Pawn pawn, ref int sharedIndex, int? savedIndex, ref string __result)
        {
            // 检查pawn是否有特殊外观hediff
            bool hasHediff = pawn.health?.hediffSet?.HasHediff(DefOfs.RKU_SpecialNPCAppearance) ?? false;

            // 获取path属性
            var pathProperty = __instance.GetType().GetProperty("path");
            if (pathProperty == null) return true;

            string currentPath = pathProperty.GetValue(__instance) as string;
            if (string.IsNullOrEmpty(currentPath)) return true;

            // 如果是原版的鼠族耳朵或尾巴，根据hediff决定返回哪个path
            if (currentPath == "Body/RK_EarLeft")
            {
                __result = hasHediff ? "Things/Commander/Ear" : currentPath;
                Log.Message($"[RKU] BodyAddon.GetPath - pawn: {pawn.Name}, original: Body/RK_EarLeft, result: {__result}, hasHediff: {hasHediff}");
                return false; // 跳过原方法
            }
            else if (currentPath == "Body/RK_Tail")
            {
                __result = hasHediff ? "Things/Commander/Tail" : currentPath;
                Log.Message($"[RKU] BodyAddon.GetPath - pawn: {pawn.Name}, original: Body/RK_Tail, result: {__result}, hasHediff: {hasHediff}");
                return false; // 跳过原方法
            }

            return true; // 继续原方法
        }
    }

    // 实时设置头发 - patch Pawn.TickRare
    [HarmonyPatch(typeof(Pawn), "TickRare")]
    public static class Pawn_TickRare_Patch
    {
        private static readonly HashSet<int> processedPawns = new HashSet<int>();

        [HarmonyPostfix]
        public static void Postfix(Pawn __instance)
        {
            // 只处理鼠族
            if (__instance.def.defName != "Ratkin") return;

            bool hasHediff = __instance.health?.hediffSet?.HasHediff(DefOfs.RKU_SpecialNPCAppearance) ?? false;

            // 如果有hediff但还没处理过
            if (hasHediff && !processedPawns.Contains(__instance.thingIDNumber))
            {
                SetCommanderHair(__instance);
                processedPawns.Add(__instance.thingIDNumber);
            }
            // 如果没有hediff但之前处理过，移除标记
            else if (!hasHediff && processedPawns.Contains(__instance.thingIDNumber))
            {
                processedPawns.Remove(__instance.thingIDNumber);
            }
        }

        // 头发设置工具方法
        private static void SetCommanderHair(Pawn pawn)
        {
            if (pawn.story != null)
            {
                HairDef commanderHair = DefDatabase<HairDef>.GetNamed("RKU_CommanderHair", false);
                if (commanderHair != null)
                {
                    Log.Message($"[RKU] 设置pawn {pawn.Name} 的头发为指挥官发型，颜色RGB(222,208,212)");
                    pawn.story.hairDef = commanderHair;
                    pawn.story.HairColor = new Color(222f/255f, 208f/255f, 212f/255f);
                    PortraitsCache.SetDirty(pawn);
                    Log.Message($"[RKU] 头发设置完成，portrait cache已更新");
                }
                else
                {
                    Log.Error($"[RKU] 找不到RKU_CommanderHair定义！");
                }
            }
            else
            {
                Log.Warning($"[RKU] pawn {pawn.Name} 没有story，无法设置头发");
            }
        }
    }
}
