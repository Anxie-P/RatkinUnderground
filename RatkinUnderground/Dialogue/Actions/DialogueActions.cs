using RimWorld;
using System;
using System.Linq;
using Verse;

namespace RatkinUnderground
{
    // 改变阵营关系
    public class DialogueAction_ChangeFactionRelation : DialogueAction
    {
        public int changeAmount;
        
        public override void ExecuteAction(Dialog_RKU_Radio radio)
        {
            var component = radio.GetRadioComponent();
            if (component != null)
            {
                component.ralationshipGrade += changeAmount;
            }
        }
    }

    // 改变关系范围
    public class DialogueAction_ChangeFactionRelationRange : DialogueAction
    {
        public int max;
        public int min;

        public override void ExecuteAction(Dialog_RKU_Radio radio)
        {
            var component = radio.GetRadioComponent();
            if (component != null)
            {
                component.maxRelationshipGrade = max;
                component.minRelationshipGrade = min;
            }
        }
    }

    // 增加研究进度
    public class DialogueAction_AddResearchProgress : DialogueAction
    {
        public float progressAmount;
        
        public override void ExecuteAction(Dialog_RKU_Radio radio)
        {
            var component = radio.GetRadioComponent();
            if (component != null)
            {
                component.researchProgress = Math.Min(
                    component.researchProgress + progressAmount, 
                    RKU_RadioGameComponent.RESEARCH_PROGRESS_MAX
                );
            }
        }
    }
    
    // 生成物品
    public class DialogueAction_SpawnItems : DialogueAction
    {
        public string thingDefName;
        public int count = 1;
        public int stackSize = 1;
        
        public override void ExecuteAction(Dialog_RKU_Radio radio)
        {
            var map = radio.radio?.Map;
            if (map == null) return;
            
            var thingDef = DefDatabase<ThingDef>.GetNamed(thingDefName, false);
            if (thingDef == null) return;
            
            for (int i = 0; i < count; i++)
            {
                Thing thing = ThingMaker.MakeThing(thingDef);
                thing.stackCount = stackSize;
                
                IntVec3 spawnCell = CellFinder.RandomEdgeCell(map);
                GenPlace.TryPlaceThing(thing, spawnCell, map, ThingPlaceMode.Near);
            }
        }
    }
    
    // 发送消息
    public class DialogueAction_SendMessage : DialogueAction
    {
        public string message;
        public MessageTypeDef messageType = MessageTypeDefOf.NeutralEvent;
        
        public override void ExecuteAction(Dialog_RKU_Radio radio)
        {
            Messages.Message(message, messageType);
        }
    }
    
    // 触发事件
    public class DialogueAction_TriggerIncident : DialogueAction
    {
        public string incidentDefName;
        
        public override void ExecuteAction(Dialog_RKU_Radio radio)
        {
            var incidentDef = DefDatabase<IncidentDef>.GetNamed(incidentDefName, false);
            if (incidentDef != null)
            {
                IncidentParms parms = StorytellerUtility.DefaultParmsNow(incidentDef.category, radio.radio.Map);
                incidentDef.Worker.TryExecute(parms);
            }
        }
    }
    
    // 设置交易状态
    public class DialogueAction_SetTradeStatus : DialogueAction
    {
        public bool canTrade;
        
        public override void ExecuteAction(Dialog_RKU_Radio radio)
        {
            var component = radio.GetRadioComponent();
            if (component != null)
            {
                component.canTrade = canTrade;
            }
        }
    }
    
    // 开始交易信号
    public class DialogueAction_StartTradeSignal : DialogueAction
    {
        public override void ExecuteAction(Dialog_RKU_Radio radio)
        {
            var component = radio.GetRadioComponent();
            if (component != null && component.CanTradeNow)
            {
                component.StartTradeSignal();
            }
        }
    }
    
    // 添加自定义消息到电台历史
    public class DialogueAction_AddRadioMessage : DialogueAction
    {
        public string message;
        
        public override void ExecuteAction(Dialog_RKU_Radio radio)
        {
            radio.AddMessage(message);
        }
    }

    public class DialogueAction_SpawnTechprint : DialogueAction
    {
        public string researchDefName;
        public int count = 1;

        public override void ExecuteAction(Dialog_RKU_Radio radio)
        {
            var map = radio.radio?.Map;
            if (map == null) return;

            for (int i = 0; i < count; i++)
            {
                Thing techprint = ThingMaker.MakeThing(ResearchProjectDef.Named(researchDefName).Techprint);
                GenPlace.TryPlaceThing(techprint, radio.radio.Position, map, ThingPlaceMode.Near);
            }
        }
    }

    // 启动延时事件计时器
    public class DialogueAction_StartDelayedEventTimer : DialogueAction
    {
        public string eventType; // "WarningLight" 或 "WarningSerious"

        public override void ExecuteAction(Dialog_RKU_Radio radio)
        {
            var component = radio.GetRadioComponent();
            if (component != null)
            {
                switch (eventType)
                {
                    case "WarningLight":
                        component.OnRelationWarningLightTriggered();
                        break;
                    case "WarningSerious":
                        component.OnRelationWarningSeriousTriggered();
                        break;
                }
            }
        }
    }
} 