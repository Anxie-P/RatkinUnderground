using RimWorld;
using System;
using System.Linq;
using UnityEngine;
using Verse;

namespace RatkinUnderground
{
    // 研究进度条件
    public class DialogueCondition_ResearchProgress : DialogueCondition
    {
        public float minProgress;
        public float maxProgress = float.MaxValue;
        public override bool CheckCondition(Dialog_RKU_Radio radio)
        {
            var component = radio.GetRadioComponent();
            if (component == null) return false;
            
            return component.researchProgress >= minProgress && 
                   component.researchProgress <= maxProgress;
        }
    }
    
    // 阵营关系条件
    public class DialogueCondition_FactionRelation : DialogueCondition
    {
        public int minGoodwill;
        public int maxGoodwill = 100;
        
        public override bool CheckCondition(Dialog_RKU_Radio radio)
        {
            var faction = Find.FactionManager.FirstFactionOfDef(DefOfs.RKU_Faction);
            if (faction == null) return false;
            
            return faction.PlayerGoodwill >= minGoodwill && 
                   faction.PlayerGoodwill <= maxGoodwill;
        }
    }
    
    // 时间条件
    public class DialogueCondition_GameTime : DialogueCondition
    {
        public int minDays;
        public int maxDays = int.MaxValue;
        
        public override bool CheckCondition(Dialog_RKU_Radio radio)
        {
            int currentDays = Find.TickManager.TicksGame / 60000; // 转换为天数
            return currentDays >= minDays && currentDays <= maxDays;
        }
    }
    
    // 地图物品条件
    public class DialogueCondition_MapItems : DialogueCondition
    {
        public string thingDefName;
        public int minCount;
        
        public override bool CheckCondition(Dialog_RKU_Radio radio)
        {
            var map = radio.radio?.Map;
            if (map == null) return false;
            
            var thingDef = DefDatabase<ThingDef>.GetNamed(thingDefName, false);
            if (thingDef == null) return false;
            
            int count = map.listerThings.ThingsOfDef(thingDef).Count();
            return count >= minCount;
        }
    }
    
    // 殖民者数量条件
    public class DialogueCondition_ColonistCount : DialogueCondition
    {
        public int minCount;
        public int maxCount = int.MaxValue;
        
        public override bool CheckCondition(Dialog_RKU_Radio radio)
        {
            var map = radio.radio?.Map;
            if (map == null) return false;
            
            int count = map.mapPawns.FreeColonists.Count;
            return count >= minCount && count <= maxCount;
        }
    }
    
    // 交易状态条件
    public class DialogueCondition_TradeStatus : DialogueCondition
    {
        public bool canTrade;
        public override bool CheckCondition(Dialog_RKU_Radio radio)
        {
            var component = radio.GetRadioComponent();
            if (component == null) return false;
            
            return component.canTrade == canTrade;
        }
    }
    
    // 等待交易状态条件
    public class DialogueCondition_WaitingForTrade : DialogueCondition
    {
        public bool isWaiting;
        
        public override bool CheckCondition(Dialog_RKU_Radio radio)
        {
            var component = radio.GetRadioComponent();
            if (component == null) return false;
            
            return component.isWaitingForTrade == isWaiting;
        }
    }

    public class DialogueCondition_ResearchGrade : DialogueCondition
    {
        public int requiredGrade;
        
        public override bool CheckCondition(Dialog_RKU_Radio radio)
        {
            var component = radio.GetRadioComponent();
            return component != null && component.researchGrade == requiredGrade;
        }
    }

    public class DialogueCondition_RelationshipGrade : DialogueCondition
    {
        public int minGrade;
        public int maxGrade = 100;

        public override bool CheckCondition(Dialog_RKU_Radio radio)
        {
            var component = radio.GetRadioComponent();
            if (component == null) return false;

            return component.ralationshipGrade >= minGrade &&
                   component.ralationshipGrade <= maxGrade;
        }
    }

    public class DialogueCondition_QuestCompleted : DialogueCondition
    {
        public string questDefName;

        public override bool CheckCondition(Dialog_RKU_Radio radio)
        {
            var component = radio.GetRadioComponent();
            if (component == null) return false;

            string completionKey = questDefName + "_Completed";
            return component.triggeredOnceEvents != null &&
                   component.triggeredOnceEvents.Contains(completionKey);
        }
    }

    // 检查事件是否被记录
    public class DialogueCondition_EventRecorded : DialogueCondition
    {
        public string eventKey;

        public override bool CheckCondition(Dialog_RKU_Radio radio)
        {
            var component = radio.GetRadioComponent();
            if (component == null) return false;

            return component.triggeredOnceEvents != null &&
                   component.triggeredOnceEvents.Contains(eventKey);
        }
    }

    // 检查阵营是否存在且未被击败
    public class DialogueCondition_FactionExists : DialogueCondition
    {
        public string factionDefName;
        public bool checkDefeated = true;

        public override bool CheckCondition(Dialog_RKU_Radio radio)
        {
            var factionDef = DefDatabase<FactionDef>.GetNamed(factionDefName, false);
            if (factionDef == null) return false;

            var faction = Find.FactionManager.FirstFactionOfDef(factionDef);
            if (faction == null) return false;

            if (checkDefeated && faction.defeated) return false;

            return true;
        }
    }

    // 检查阵营是否不存在或被击败
    public class DialogueCondition_FactionNotExists : DialogueCondition
    {
        public string factionDefName;
        public bool checkDefeated = true;

        public override bool CheckCondition(Dialog_RKU_Radio radio)
        {
            var factionDef = DefDatabase<FactionDef>.GetNamed(factionDefName, false);
            if (factionDef == null) return true; 
            var faction = Find.FactionManager.FirstFactionOfDef(factionDef);
            if (faction == null) return true;
            if (checkDefeated && faction.defeated) return true;
            return false;
        }
    }

    // 检查玩家殖民地中是否有奶酪
    public class DialogueCondition_HasZiggyInColony : DialogueCondition
    {
        public override bool CheckCondition(Dialog_RKU_Radio radio)
        {
            var map = radio.radio?.Map ?? Find.AnyPlayerHomeMap;
            if (map == null) return false;

            // 检查所有属于玩家的Pawn
            foreach (var pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (pawn.Faction == Faction.OfPlayer && IsZiggy(pawn))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsZiggy(Pawn pawn)
        {
            if (pawn == null) return false;

            // 检查名字
            if (pawn.Name == null || !pawn.Name.ToString().Contains("RKU_Zigstark".Translate()))
                return false;

            // 检查性别
            if (pawn.gender != Gender.Female)
                return false;

            // 检查故事属性
            if (pawn.story == null) return false;

            // 检查头发
            if (pawn.story.hairDef == null || pawn.story.hairDef.defName != "RKU_CommanderHair")
                return false;

            // 检查童年背景
            if (pawn.story.Childhood == null || pawn.story.Childhood.defName != "Ratkin_GuerrillaCT")
                return false;

            // 检查成年背景
            if (pawn.story.Adulthood == null || pawn.story.Adulthood.defName != "RKU_GuerrillaAR")
                return false;
            return true;
        }
    }
} 