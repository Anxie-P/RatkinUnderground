using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RatkinUnderground
{
    public class RKU_DialogueManager
    {
        private static Dictionary<string, int> lastTriggerTimes = new Dictionary<string, int>();
        private static HashSet<string> triggeredOnceEvents = new HashSet<string>();
        
        public static void TriggerDialogueEvents(Dialog_RKU_Radio radio, string triggerType = "")
        {
            var allEvents = DefDatabase<RKU_DialogueEventDef>.AllDefs
                .Where(e => ShouldTriggerEvent(e, triggerType))
                .OrderBy(e => e.priority)
                .ToList();
            
            foreach (var dialogueEvent in allEvents)
            {
                if (CheckConditions(dialogueEvent, radio))
                {
                    ExecuteDialogueEvent(dialogueEvent, radio);
                    break; 
                }
            }
        }
        
        private static bool ShouldTriggerEvent(RKU_DialogueEventDef dialogueEvent, string triggerType)
        {
            if (dialogueEvent.triggerOnce && triggeredOnceEvents.Contains(dialogueEvent.defName))
                return false;
            
            // 检查冷却时间
            if (dialogueEvent.cooldownTicks > 0)
            {
                int currentTick = Find.TickManager.TicksGame;
                if (lastTriggerTimes.TryGetValue(dialogueEvent.defName, out int lastTrigger))
                {
                    if (currentTick - lastTrigger < dialogueEvent.cooldownTicks)
                        return false;
                }
            }
            
            // 检查触发类型
            switch (triggerType)
            {
                case "trade":
                    return dialogueEvent.triggerOnTrade;
                case "scan":
                    return dialogueEvent.triggerOnScan;
                case "emergency":
                    return dialogueEvent.triggerOnEmergency;
                default:
                    return string.IsNullOrEmpty(triggerType);
            }
        }
        
        private static bool CheckConditions(RKU_DialogueEventDef dialogueEvent, Dialog_RKU_Radio radio)
        {
            foreach (var condition in dialogueEvent.conditions)
            {
                if (!condition.CheckCondition(radio))
                    return false;
            }
            return true;
        }
        
        private static void ExecuteDialogueEvent(RKU_DialogueEventDef dialogueEvent, Dialog_RKU_Radio radio)
        {
            // 记录触发时间
            lastTriggerTimes[dialogueEvent.defName] = Find.TickManager.TicksGame;
            
            // 标记为已触发（对于一次性事件）
            if (dialogueEvent.triggerOnce)
                triggeredOnceEvents.Add(dialogueEvent.defName);
            
            // 添加对话消息
            radio.AddMessage(dialogueEvent.dialogueText);
            
            // 执行动作
            foreach (var action in dialogueEvent.actions)
            {
                try
                {
                    action.ExecuteAction(radio);
                }
                catch (Exception ex)
                {
                    Log.Error($"[RKUDialogue] 执行对话动作时出错: {ex.Message}");
                }
            }
        }
        
        // 重置所有触发状态（用于调试或重新开始游戏）
        public static void ResetAllTriggers()
        {
            lastTriggerTimes.Clear();
            triggeredOnceEvents.Clear();
        }
        
        // 检查特定事件是否可以触发
        public static bool CanTriggerEvent(string eventDefName, Dialog_RKU_Radio radio)
        {
            var dialogueEvent = DefDatabase<RKU_DialogueEventDef>.GetNamed(eventDefName, false);
            if (dialogueEvent == null) return false;
            
            return ShouldTriggerEvent(dialogueEvent, "") && CheckConditions(dialogueEvent, radio);
        }
    }
} 