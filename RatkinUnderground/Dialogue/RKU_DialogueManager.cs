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
        private static RKU_RadioGameComponent comp
        {
            get
            {
                if (Current.Game == null)
                    return null;

                var component = Current.Game.GetComponent<RKU_RadioGameComponent>();
                if (component == null)
                {
                    component = new RKU_RadioGameComponent(Current.Game);
                    Current.Game.components.Add(component);
                }
                return component;
            }
        }

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
            // 遍历所有事件，找到第一个满足条件的执行
            foreach (var first in groupedEvents)
            {
                if (CheckConditions(first, radio))
                {
                    ExecuteDialogueEvent(first, radio);
                    break;
                }
            }
        }

        private static bool ShouldTriggerEvent(RKU_DialogueEventDef dialogueEvent, string triggerType)
        {
            if (dialogueEvent == null)
                return false;

            var currentComp = comp;
            if (currentComp == null)
                return false;

            if (dialogueEvent.triggerOnce && currentComp.triggeredOnceEvents != null &&
                currentComp.triggeredOnceEvents.Contains(dialogueEvent.defName))
                return false;
            if (dialogueEvent.cooldownTicks > 0)
            {
                int currentTick = Find.TickManager.TicksGame;
                if (currentComp.lastTriggerTimes != null &&
                    currentComp.lastTriggerTimes.TryGetValue(dialogueEvent.defName, out int lastTrigger))
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
                case "research":
                    return dialogueEvent.triggerOnResearch;
                default:
                    return string.IsNullOrEmpty(triggerType);
            }
        }

        private static bool CheckConditions(RKU_DialogueEventDef dialogueEvent, Dialog_RKU_Radio radio)
        {
            if (dialogueEvent == null)
                return false;

            var currentComp = comp;
            if (dialogueEvent.triggerOnce && currentComp != null &&
                currentComp.triggeredOnceEvents != null &&
                currentComp.triggeredOnceEvents.Contains(dialogueEvent.defName))
                return false;
            foreach (var condition in dialogueEvent.conditions)
            {
                if (!condition.CheckCondition(radio))
                    return false;
            }
            return true;
        }

        public static void ExecuteDialogueEvent(RKU_DialogueEventDef dialogueEvent, Dialog_RKU_Radio radio)
        {
            if (dialogueEvent == null || radio == null)
                return;

            var currentComp = comp;
            if (currentComp == null)
                return;

            // 记录触发时间
            if (currentComp.lastTriggerTimes != null)
            {
                currentComp.lastTriggerTimes[dialogueEvent.defName] = Find.TickManager.TicksGame;
            }

            if (dialogueEvent.triggerOnce && currentComp.triggeredOnceEvents != null)
            {
                currentComp.triggeredOnceEvents.Add(dialogueEvent.defName);
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
            var currentComp = comp;
            if (currentComp == null)
                return;

            if (currentComp.lastTriggerTimes != null)
            {
                currentComp.lastTriggerTimes.Clear();
            }
            if (currentComp.triggeredOnceEvents != null)
            {
                currentComp.triggeredOnceEvents.Clear();
            }
        }
    }
}