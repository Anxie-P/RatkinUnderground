using LudeonTK;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using static Mono.Security.X509.X520;

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
        }
    }
}
