using RimWorld;
using System;
using System.Collections.Generic;
using Verse;

namespace RatkinUnderground
{
    public class RKU_DialogueEventDef : Def
    {
        // 对话内容
        public string dialogueText;
        
        // 触发条件
        public List<DialogueCondition> conditions = new List<DialogueCondition>();
        
        // 执行的动作
        public List<DialogueAction> actions = new List<DialogueAction>();
        
        // 优先级（数字越小优先级越高）
        public int priority = 100;
        
        // 是否只触发一次
        public bool triggerOnce = false;
        
        // 冷却时间
        public int cooldownTicks = 0;
        
        // 是否在交易时触发
        public bool triggerOnTrade = false;
        
        // 是否在扫描时触发
        public bool triggerOnScan = false;
        
        // 是否在紧急呼叫时触发
        public bool triggerOnEmergency = false;
        
        // 自定义触发条件
        public string customTriggerMethod;
    }
    
    // 条件
    public abstract class DialogueCondition
    {
        public abstract bool CheckCondition(Dialog_RKU_Radio radio);
    }
    //触发后动作
    public abstract class DialogueAction
    {
        public abstract void ExecuteAction(Dialog_RKU_Radio radio);
    }
} 