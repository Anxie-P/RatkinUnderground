using RimWorld;
using System;
using System.Linq;
using UnityEngine;
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


            Map targetMap = radio?.radio?.Map ?? Find.AnyPlayerHomeMap;
            if (targetMap == null)
            {
                Log.Error("[RKU] DialogueAction_TriggerIncident: 无法找到目标地图");
                return;
            }

            IncidentParms parms;


            parms = StorytellerUtility.DefaultParmsNow(incidentDef.category, targetMap);
            try
            {
                incidentDef.Worker.TryExecute(parms);
            }
            catch (Exception ex)
            {
                Log.Error($"[RKU] DialogueAction_TriggerIncident: 执行事件时出错: {ex.Message}\n{ex.StackTrace}");
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

    // 触发任务
    public class DialogueAction_TriggerQuest : DialogueAction
    {
        public string questDefName;
        public override void ExecuteAction(Dialog_RKU_Radio radio)
        {
            QuestScriptDef questDef = DefDatabase<QuestScriptDef>.GetNamed(questDefName);
            if (questDef != null)
            {
                Quest quest = QuestUtility.GenerateQuestAndMakeAvailable(questDef, StorytellerUtility.DefaultThreatPointsNow(Find.World));
                if (quest != null)
                {
                    QuestUtility.SendLetterQuestAvailable(quest);
                }
            }
        }
    }

    // 让游击队指挥官和蜈蚣加入玩家殖民地
    public class DialogueAction_JoinColony : DialogueAction
    {
        public override void ExecuteAction(Dialog_RKU_Radio radio)
        {
            var map = radio.radio?.Map ?? Find.AnyPlayerHomeMap;
            if (map == null)
            {
                Log.Error("[RKU] DialogueAction_JoinColony: 无法找到目标地图");
                return;
            }
            Faction rFaction = Find.FactionManager.FirstFactionOfDef(DefOfs.RKU_Faction);
            if (rFaction == null || rFaction.leader == null)
            {
                Log.Error("[RKU] DialogueAction_JoinColony: 无法找到游击队阵营或指挥官");
                return;
            }
            Pawn leader = rFaction.leader;
            if (leader.Spawned)
            {
                leader.DeSpawn();
            }
            leader.SetFaction(Faction.OfPlayer);
            IntVec3 spawnCell = CellFinder.RandomEdgeCell(map);
            GenSpawn.Spawn(leader, spawnCell, map);
            Pawn centiped = PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("Mech_CentipedeGunner"));                
            centiped.equipment?.DestroyAllEquipment();
                    ThingWithComps weapon = (ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("RKU_IronStarCannon"), null);
                    weapon.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Legendary, ArtGenerationContext.Outsider);
                    centiped.equipment?.AddEquipment(weapon);
                    // 给蜈蚣加BUff
                    HediffDef ironStarHediff = DefDatabase<HediffDef>.GetNamedSilentFail("RKU_IronStarHediff");
                    if (ironStarHediff != null && !centiped.health.hediffSet.HasHediff(ironStarHediff))
                    {
                        centiped.health.AddHediff(ironStarHediff);
                    }
            if (centiped != null)
            {
                if (centiped.Spawned)
                {
                    centiped.DeSpawn();
                }
                centiped.SetFaction(Faction.OfPlayer);
                GenSpawn.Spawn(centiped, spawnCell, map);
            }
        }
    }

    // 让游击队阵营消失
    public class DialogueAction_DefeatFaction : DialogueAction
    {
        public string factionDefName;

        public override void ExecuteAction(Dialog_RKU_Radio radio)
        {
            FactionDef factionDef = DefDatabase<FactionDef>.GetNamed(factionDefName, false);
            Faction faction = Find.FactionManager.FirstFactionOfDef(factionDef);
            faction.defeated = true;
            var settlements = Find.WorldObjects.Settlements;
            for (int i = settlements.Count - 1; i >= 0; i--)
            {
                if (settlements[i].Faction == faction)
                {
                    Find.WorldObjects.Remove(settlements[i]);
                    Log.Message($"[RKU] 已移除{faction.Name}的据点");
                }
            }
            var allWorldObjects = Find.WorldObjects.AllWorldObjects;
            for (int i = allWorldObjects.Count - 1; i >= 0; i--)
            {
                if (allWorldObjects[i].Faction == faction)
                {
                    Find.WorldObjects.Remove(allWorldObjects[i]);
                }
            }
        }
    }
}