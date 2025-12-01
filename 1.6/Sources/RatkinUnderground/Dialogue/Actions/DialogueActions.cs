using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

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

            ResearchProjectDef researchDef = ResearchProjectDef.Named(researchDefName);
            if (researchDef == null) return;

            Thing techprint = null;
            for (int i = 0; i < count; i++)
            {
                techprint = ThingMaker.MakeThing(researchDef.Techprint);
                GenPlace.TryPlaceThing(techprint, radio.radio.Position, map, ThingPlaceMode.Near);
            }

            // 检查研究进度并升级
            var component = radio.GetRadioComponent();
            if (component != null)
            {
                if (component.researchProgress >= RKU_RadioGameComponent.RESEARCH_PROGRESS_MAX)
                {
                    component.researchProgress = 0;
                    component.researchGrade++;
                }
            }

            // 发送信封提示
            if (techprint != null)
            {
                string letterLabel = "RKU_TechprintReceived".Translate();
                string letterText = "RKU_TechprintReceivedDesc".Translate(researchDef.label, count);
                Find.LetterStack.ReceiveLetter(letterLabel, letterText, LetterDefOf.PositiveEvent, lookTargets: techprint);
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
            IntVec3 spawnCell = new IntVec3();
            CellFinder.TryFindRandomEdgeCellWith((IntVec3 c) =>
            !map.roofGrid.Roofed(c)
            && c.Walkable(map)
            && map.reachability.CanReachColony(c), map, 0f, out spawnCell);
            GenSpawn.Spawn(leader, spawnCell, map);
            GenSpawn.Spawn(Utils.SpawnIronStarCentipede(), spawnCell, map);
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

    // 在玩家殖民地附近生成游击队据点
    public class DialogueAction_SpawnGuerrillaSettlement : DialogueAction
    {
        public override void ExecuteAction(Dialog_RKU_Radio radio)
        {
            Faction guerrillaFaction = Find.FactionManager.FirstFactionOfDef(DefOfs.RKU_Faction);
            if (guerrillaFaction == null)
            {
                Log.Error("[RKU] DialogueAction_SpawnGuerrillaSettlement: 无法找到游击队派系");
                return;
            }
            Map playerMap = Find.AnyPlayerHomeMap;
            if (playerMap == null)
            {
                Log.Error("[RKU] DialogueAction_SpawnGuerrillaSettlement: 无法找到玩家殖民地");
                return;
            }

            int playerTile = playerMap.Tile;
            WorldGrid grid = Find.WorldGrid;
            List<int> candidateTiles = new List<int>();
            HashSet<int> visited = new HashSet<int>();
            Queue<Tuple<int, int>> queue = new Queue<Tuple<int, int>>();
            queue.Enqueue(new Tuple<int, int>(playerTile, 0));
            visited.Add(playerTile);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                int currentTile = current.Item1;
                int currentDistance = current.Item2;

                if (currentDistance >= 5 && currentDistance <= 8 && IsValidSettlementTile(currentTile))
                {
                    candidateTiles.Add(currentTile);
                }
                if (currentDistance < 9)
                {
                    List<PlanetTile> neighbors = new List<PlanetTile>();
                    grid.GetTileNeighbors(currentTile, neighbors);

                    foreach (int neighbor in neighbors)
                    {
                        if (!visited.Contains(neighbor))
                        {
                            visited.Add(neighbor);
                            queue.Enqueue(new Tuple<int, int>(neighbor, currentDistance + 1));
                        }
                    }
                }
            }

            if (candidateTiles.Count == 0)
            {
                Log.Warning("[RKU] DialogueAction_SpawnGuerrillaSettlement: 在5-8格范围内未找到合适的据点位置");
                return;
            }

            int selectedTile = candidateTiles.RandomElement();
            Settlement settlement = (Settlement)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);
            settlement.SetFaction(guerrillaFaction);
            settlement.Tile = selectedTile;
            settlement.Name = SettlementNameGenerator.GenerateSettlementName(settlement);
            Find.WorldObjects.Add(settlement);
            string letterLabel = "RKU_GenerateSettled".Translate();
            string letterText = "RKU_GenerateSettledDesc".Translate();
            Find.LetterStack.ReceiveLetter(letterLabel, letterText, LetterDefOf.PositiveEvent);
        }

        private bool IsValidSettlementTile(int tile)
        {
            WorldGrid grid = Find.WorldGrid;
            Tile tileData = grid[tile];
            if (tileData.WaterCovered)
                return false;
            if (Find.WorldObjects.AnyWorldObjectAt(tile))
                return false;
            if (tileData.hilliness == Hilliness.Impassable)
                return false;
            return true;
        }
    }
}