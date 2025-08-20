using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RatkinUnderground
{
    public class RKU_DialogueManager
    {
        //private static Dictionary<string, int> lastTriggerTimes = new Dictionary<string, int>();
        //private static HashSet<string> triggeredOnceEvents = new HashSet<string>();
        private static RKU_RadioGameComponent comp => Current.Game.GetComponent<RKU_RadioGameComponent>();

        public static void TriggerDialogueEvents(Dialog_RKU_Radio radio, string triggerType = "")
        {
            // 获取所有符合条件的对话事件
            var allEvents = DefDatabase<RKU_DialogueEventDef>.AllDefs
                .Where(e => ShouldTriggerEvent(e, triggerType))
                .ToList();
            
            // 按优先级分组并随机排序相同优先级的事件
            var groupedEvents = allEvents
                .GroupBy(e => e.priority)
                .OrderBy(g => g.Key) 
                .Select(g => g.OrderBy(e => Rand.Range(0, 10000)))
                .SelectMany(g => g);

            foreach (var dialogueEvent in groupedEvents)
            {
                if (CheckConditions(dialogueEvent, radio))
                {
                    Log.Message($"符合条件的：{dialogueEvent}");
                }
            }

            foreach (var ban in comp.triggeredOnceEvents)
            {
                Log.Message($"已经播放过的：{ban}");
            }

            // 遍历所有事件，找到第一个满足条件的执行
            foreach (var first in groupedEvents)
            {
                if (CheckConditions(first, radio))
                {
                    ExecuteDialogueEvent(first, radio);
                    Log.Message($"第一个符合条件的：{first}");
                    break;
                }
            }
        }

        private static bool ShouldTriggerEvent(RKU_DialogueEventDef dialogueEvent, string triggerType)
        {
            if (dialogueEvent.triggerOnce && comp.triggeredOnceEvents.Contains(dialogueEvent.defName))
                return false;

            // 检查冷却时间
            if (dialogueEvent.cooldownTicks > 0)
            {
                int currentTick = Find.TickManager.TicksGame;
                if (comp.lastTriggerTimes.TryGetValue(dialogueEvent.defName, out int lastTrigger))
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
                case "startup": 
                    return dialogueEvent.triggerOnStartup;
                default:
                    return string.IsNullOrEmpty(triggerType);
            }
        }

        private static bool CheckConditions(RKU_DialogueEventDef dialogueEvent, Dialog_RKU_Radio radio)
        {
            if (dialogueEvent.triggerNon) return false;
            foreach (var condition in dialogueEvent.conditions)
            {
                if (!condition.CheckCondition(radio))
                    return false;
            }
            return true;
        }

        public static void ExecuteDialogueEvent(RKU_DialogueEventDef dialogueEvent, Dialog_RKU_Radio radio)
        {
            // 记录触发时间
            comp.lastTriggerTimes[dialogueEvent.defName] = Find.TickManager.TicksGame;

            if (dialogueEvent.triggerOnce)
            {
                comp.triggeredOnceEvents.Add(dialogueEvent.defName);
                Log.Message($"已执行添加{dialogueEvent.defName}");
            }
                

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
            comp.lastTriggerTimes.Clear();
            comp.triggeredOnceEvents.Clear();
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