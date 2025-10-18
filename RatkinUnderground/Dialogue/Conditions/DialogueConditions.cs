using RimWorld;
using System;
using System.Linq;
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
} 