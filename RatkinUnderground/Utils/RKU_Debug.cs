using LudeonTK;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RatkinUnderground
{
    public class RKU_Debug
    {
        public static class Debug_GenerateFaction
        {
            [DebugAction(
                category: "RatkinUnderground",
                name: "Generate Faction")]
            private static void GenerateUndergroundFaction()
            {
                // 手动生成派系
                var RKUDef = DefOfs.RKU_Faction;
                if (Find.FactionManager.AllFactions.Any(f => f.def == RKUDef)) return;

                FactionGenerator.CreateFactionAndAddToManager(RKUDef);

                Faction rFaction = Find.FactionManager.FirstFactionOfDef(RKUDef);
                // 设置敌对关系
                List<FactionDef> enemyFaction = new List<FactionDef>
                {
                    FactionDef.Named("Rakinia_Warlord"),
                    FactionDef.Named("Rakinia")
                };
                if (rFaction == null) return;
                foreach (FactionDef enemy in enemyFaction)
                {
                    if (Find.FactionManager.AllFactions.Any(o => o.def == enemy))
                    {
                        Faction eFaction = Find.FactionManager.FirstFactionOfDef(enemy);
                        eFaction.RelationWith(rFaction).baseGoodwill = -100;
                        rFaction.RelationWith(eFaction).baseGoodwill = -100;
                        eFaction.RelationWith(rFaction).kind = FactionRelationKind.Hostile;
                        rFaction.RelationWith(eFaction).kind = FactionRelationKind.Hostile;
                    }
                }

                Log.Message($"[RatkinUnderground] Generated new faction: {RKUDef.defName}");
            }
        }

        public static class Debug_FinalBattleBeginner
        {
            [DebugAction(
                category: "RatkinUnderground",
                name: "FinalBattle Beginner")]
            private static void FinalBattleBeginner()
            {
                Current.Game.GetComponent<RKU_RadioGameComponent>().maxRelationshipGrade = 75;
                Current.Game.GetComponent<RKU_RadioGameComponent>().minRelationshipGrade = -75;
                Current.Game.GetComponent<RKU_RadioGameComponent>().ralationshipGrade = -75;
                FactionRelationKind kind2 = Utils.OfRKU.RelationWith(Faction.OfPlayer).kind;
                Utils.OfRKU.RelationWith(Faction.OfPlayer).kind = FactionRelationKind.Hostile;
                Faction.OfPlayer.RelationWith(Utils.OfRKU).kind = FactionRelationKind.Hostile;
                Utils.OfRKU.Notify_RelationKindChanged(Faction.OfPlayer, kind2, false, "", TargetInfo.Invalid, out var sentLetter);
                Faction.OfPlayer.Notify_RelationKindChanged(Utils.OfRKU, kind2, false, "", TargetInfo.Invalid, out sentLetter);
                Utils.OfRKU.RelationWith(Faction.OfPlayer).baseGoodwill = -100;
                Log.Message($"[RatkinUnderground] 关系已调整至-75");
            }
        }

        public static class Debug_DialogueSystem
        {
            [DebugAction(
                category: "RatkinUnderground",
                name: "Reset Dialogue Triggers")]
            private static void ResetDialogueTriggers()
            {
                RKU_DialogueManager.ResetAllTriggers();
                Messages.Message("对话触发器已重置", MessageTypeDefOf.PositiveEvent);
            }

            [DebugAction(
                category: "RatkinUnderground",
                name: "List Dialogue Events")]
            private static void ListDialogueEvents()
            {
                var allEvents = DefDatabase<RKU_DialogueEventDef>.AllDefs;
                Log.Message($"[RKUDialogue] 找到 {allEvents.Count()} 个对话事件:");

                foreach (var dialogueEvent in allEvents)
                {
                    Log.Message($"- {dialogueEvent.defName}: {dialogueEvent.dialogueText}");
                }
            }

            [DebugAction(
                category: "RatkinUnderground",
                name: "Trigger Test Dialogue")]
            private static void TriggerTestDialogue()
            {
                var map = Find.CurrentMap;
                if (map == null)
                {
                    Messages.Message("没有当前地图", MessageTypeDefOf.RejectInput);
                    return;
                }

                var radio = map.listerThings.ThingsOfDef(DefOfs.RKU_Radio).FirstOrDefault();
                if (radio == null)
                {
                    Messages.Message("地图中没有找到电台", MessageTypeDefOf.RejectInput);
                    return;
                }

                // 创建一个临时的电台窗口来测试对话
                var radioWindow = new Dialog_RKU_Radio(radio);
                RKU_DialogueManager.TriggerDialogueEvents(radioWindow, "trade");

                Messages.Message("已触发测试对话", MessageTypeDefOf.PositiveEvent);
            }

            [DebugAction(
                category: "RatkinUnderground",
                name: "Test Component Creation")]
            private static void TestComponentCreation()
            {
                try
                {
                    // 简单测试组件是否存在
                    RKU_DialogueManager.ResetAllTriggers();
                    Messages.Message("组件访问测试成功", MessageTypeDefOf.PositiveEvent);
                }
                catch (Exception ex)
                {
                    Messages.Message($"组件访问失败: {ex.Message}", MessageTypeDefOf.RejectInput);
                }
            }

            [DebugAction(
                category: "RatkinUnderground",
                name: "立即触发延时事件")]
            private static void DebugTriggerDelayedEvents()
            {
                var component = Current.Game.GetComponent<RKU_RadioGameComponent>();
                if (component != null)
                {
                    component.DebugTriggerDelayedEvents();
                    Messages.Message("延时事件已立即触发", MessageTypeDefOf.PositiveEvent);
                }
                else
                {
                    Messages.Message("未找到RKU_RadioGameComponent", MessageTypeDefOf.RejectInput);
                }
            }
        }

        public static class Debug_MoraleAndCasualty
        {
            [DebugAction(
                category: "RatkinUnderground",
                name: "士气值 +0.1")]
            private static void IncreaseMoraleSmall()
            {
                ModifyMorale(0.1f);
            }

            [DebugAction(
                category: "RatkinUnderground",
                name: "士气值 -0.1")]
            private static void DecreaseMoraleSmall()
            {
                ModifyMorale(-0.4f);
            }

            [DebugAction(
                category: "RatkinUnderground",
                name: "损失度 +10")]
            private static void IncreaseCasualty10()
            {
                ModifyCasualty(10);
            }

            [DebugAction(
                category: "RatkinUnderground",
                name: "损失度 -10")]
            private static void DecreaseCasualty10()
            {
                ModifyCasualty(-10);
            }
          
            private static void ModifyMorale(float amount)
            {
                var finalBattle = Find.CurrentMap.GameConditionManager.GetActiveCondition<RKU_GameCondition_FinalBattle>();
                if (finalBattle != null)
                {
                    finalBattle.ModifyMorale(amount);
                    Messages.Message($"士气值{(amount > 0 ? "+" : "")}{amount:F1}, 当前: {(finalBattle.Morale * 100):F1}%", MessageTypeDefOf.NeutralEvent);
                }
                else
                {
                    Messages.Message("当前没有决战状态", MessageTypeDefOf.RejectInput);
                }
            }

            private static void ModifyCasualty(int amount)
            {
                var finalBattle = Find.CurrentMap.GameConditionManager.GetActiveCondition<RKU_GameCondition_FinalBattle>();
                if (finalBattle != null)
                {
                    // 直接修改私有字段（调试用）
                    finalBattle.GetType().GetField("totalGuerrillaDeaths", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        ?.SetValue(finalBattle, finalBattle.TotalGuerrillaDeaths + amount);
                    Messages.Message($"损失度{(amount > 0 ? "+" : "")}{amount}, 当前: {finalBattle.TotalGuerrillaDeaths}", MessageTypeDefOf.NeutralEvent);
                }
                else
                {
                    Messages.Message("当前没有决战状态", MessageTypeDefOf.RejectInput);
                }
            }
        }
    }
}
