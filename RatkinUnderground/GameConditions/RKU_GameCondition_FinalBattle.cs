using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using System.Linq; // Added for FirstOrDefault

namespace RatkinUnderground
{
    public class RKU_GameCondition_FinalBattle : GameCondition
    {
        // 检测间隔：4小时检测一次状态并分配袭击
        private const int CHECK_INTERVAL_TICKS = 10000;
        private int nextCheckTick = 0;

        // 袭击点数相关
        public float currentRaidPoints = 0f;
        public float baseRaidPoints = 1000f; // 基础袭击点数
        public float raidPointsMultiplier = 1.0f; // 袭击点数倍率

        // 殖民地状态检测相关
        private float lastWealth = 0;
        private int lastColonistCount = 0;
        private float lastThreatPoints = 0f;

        // 波次系统相关
        private int currentWave = 0; 
        private const int TOTAL_WAVES = 6; 
        private int wavesSinceLastMortarRaid = 0; 

        public RKU_GameCondition_FinalBattle() { }

        public override void Init()
        {
            base.Init();
            nextCheckTick = Find.TickManager.TicksGame + CHECK_INTERVAL_TICKS;
            UpdateColonyStatus();
            currentRaidPoints = CalculateRaidPoints();
            currentWave = 0;
            wavesSinceLastMortarRaid = 0;
            Utils.OfRKU.RelationWith(Faction.OfPlayer).kind = FactionRelationKind.Hostile;
            FactionRelationKind kind2 = Utils.OfRKU.RelationWith(Faction.OfPlayer).kind;
            Utils.OfRKU.Notify_RelationKindChanged(Faction.OfPlayer, kind2, false, "", TargetInfo.Invalid, out var sentLetter);
            Faction.OfPlayer.Notify_RelationKindChanged(Utils.OfRKU, kind2, false, "", TargetInfo.Invalid, out sentLetter);
        }

        public override void GameConditionTick()
        {
            base.GameConditionTick();

            // 每4小时检测一次
            if (Find.TickManager.TicksGame >= nextCheckTick)
            {
                CheckColonyStatusAndAssignRaids();
                nextCheckTick = Find.TickManager.TicksGame + CHECK_INTERVAL_TICKS;
            }
        }

        private void CheckColonyStatusAndAssignRaids()
        {
            // 更新殖民地状态
            UpdateColonyStatus();
            // 计算新的袭击点数
            float newRaidPoints = CalculateRaidPoints();
            float raidPointsDifference = newRaidPoints - currentRaidPoints;
            currentRaidPoints = newRaidPoints;
            AssignRaidsBasedOnPoints(currentRaidPoints, raidPointsDifference);
        }

        private void UpdateColonyStatus()
        {
            lastWealth = 0;
            lastColonistCount = 0;
            lastThreatPoints = 0f;

            foreach (Map map in Find.Maps)
            {
                if (map.IsPlayerHome)
                {
                    lastWealth += map.wealthWatcher.WealthTotal;
                    lastColonistCount += map.mapPawns.ColonistsSpawnedCount;
                    lastThreatPoints += StorytellerUtility.DefaultThreatPointsNow(map);
                }
            }
        }

        private float CalculateRaidPoints()
        {
            float points = baseRaidPoints;

            // 基于财富调整袭击点数
            if (lastWealth > 50000)
            {
                points *= 1.5f; // 高财富殖民地获得更强的袭击
            }
            else if (lastWealth > 20000)
            {
                points *= 1.2f;
            }

            // 基于殖民者数量调整
            if (lastColonistCount > 10)
            {
                points *= 1.3f; // 大殖民地获得更强的袭击
            }
            else if (lastColonistCount > 5)
            {
                points *= 1.1f;
            }

            // 基于威胁点数调整
            if (lastThreatPoints > 2000)
            {
                points *= 0.8f; // 高威胁环境下的袭击稍微减弱
            }

            return points * raidPointsMultiplier;
        }

        private void AssignRaidsBasedOnPoints(float totalPoints, float pointsChange)
        {
            // 增加波次计数
            currentWave++;
            wavesSinceLastMortarRaid++;

            // 基于波次分配不同类型的袭击
            string raidMessage = "";
            Log.Message($"[RKU] 第{currentWave}波 - ");

            // 确保关系为敌对
            if (Utils.OfRKU.RelationWith(Faction.OfPlayer).kind != FactionRelationKind.Hostile)
            {
                FactionRelationKind kind2 = Utils.OfRKU.RelationWith(Faction.OfPlayer).kind;
                Utils.OfRKU.RelationWith(Faction.OfPlayer).kind = FactionRelationKind.Hostile;
                Faction.OfPlayer.RelationWith(Utils.OfRKU).kind = FactionRelationKind.Hostile;
                Utils.OfRKU.Notify_RelationKindChanged(Faction.OfPlayer, kind2, false, "", TargetInfo.Invalid, out var sentLetter);
                Faction.OfPlayer.Notify_RelationKindChanged(Utils.OfRKU, kind2, false, "", TargetInfo.Invalid, out sentLetter);
            }

            // 第一波：上限2000点，下限500点的普通袭击
            if (currentWave == 1)
            {
                TriggerBasicRaid(Mathf.Clamp(totalPoints, 500f, 4000));
            }
            // 第二波：上限3000点，下限1000点的普通袭击
            else if (currentWave == 2)
            {
                TriggerBasicRaid(Mathf.Clamp(totalPoints, 1000f, 3000f));
            }
            // 第三波：RKU_RatkinTunnel_Fir事件 + 上限2000点，下限500点的普通袭击
            else if (currentWave == 3)
            {
                TriggerRatkinTunnelEvent("RKU_RatkinTunnel_Fir");
                TriggerBasicRaid(Mathf.Clamp(totalPoints, 1000f, 10000));
            }
            // 第四波：围攻袭击 + 随机事件
            else if (currentWave == 4)
            {
                TriggerMortarSiegeRaid(totalPoints);

                float random = Rand.Range(0, 1);
                if (random < 0.33f)
                {
                    TriggerBasicRaid(Mathf.Clamp(totalPoints, 2000f, 10000));
                }
                else if (random < 0.8f)
                {
                    TriggerRatkinTunnelEvent("RKU_RatkinTunnel_Fir");
                }
                else
                {
                    TriggerRatkinTunnelEvent("RKU_RatkinTunnel_Thi");
                }
            }
            // 第五波：围攻袭击 + 随机事件
            else if (currentWave == 5)
            {
                TriggerMortarSiegeRaid(totalPoints);
                TriggerBasicRaid(Mathf.Clamp(totalPoints, 1000, 10000));
                float random = Rand.Range(0, 1);
                if (random < 0.33f)
                {
                    TriggerBasicRaid(Mathf.Clamp(totalPoints, 500f, 2000f));
                }
                else if (random < 0.8f)
                {
                    TriggerRatkinTunnelEvent("RKU_RatkinTunnel_Fir");
                }
                else
                {
                    TriggerRatkinTunnelEvent("RKU_RatkinTunnel_Thi");
                }
            }
            // 第六波：围攻袭击 + 随机事件 + 撼地
            else if (currentWave == 6)
            {
                TriggerMortarSiegeRaid(totalPoints);

                float random = Rand.Range(0, 1);
                if (random < 0.33f)
                {
                    TriggerBasicRaid(Mathf.Clamp(totalPoints, 500f, 2000f));
                }
                else if (random < 0.8f)
                {
                    TriggerRatkinTunnelEvent("RKU_RatkinTunnel_Fir");
                }
                else
                {
                    TriggerRatkinTunnelEvent("RKU_RatkinTunnel_Thi");
                }

            }
            // 发布电台消息
            if (!string.IsNullOrEmpty(raidMessage))
            {
                Utils.BroadcastRadioMessage(raidMessage);
            }

            // 检查是否击毁撼地装置
            // if (currentWave >= TOTAL_WAVES)
            // {
            //     Log.Message("电台里的信号随着撼地装置的毁灭而消失了。");
            //     Utils.BroadcastRadioMessage("无信号");
            //     // 可以在这里添加结束逻辑
            // }
            //this.End();
        }

        // 触发迫击炮围攻袭击
        private void TriggerMortarSiegeRaid(float raidPoints)
        {
            try
            {
                Map targetMap = Find.AnyPlayerHomeMap;
                if (targetMap == null)
                {
                    Log.Error("[RKU] 找不到玩家主地图，无法触发迫击炮围攻");
                    return;
                }
                IncidentDef siegeIncident = IncidentDefOf.RaidEnemy;
                IncidentParms parms = new IncidentParms();
                parms.target = targetMap;
                parms.forced = true;
                parms.points = Mathf.Max(raidPoints, 1000f); // 确保最小点数
                RaidStrategyDef siegeStrategy = DefDatabase<RaidStrategyDef>.GetNamed("Siege");
                if (siegeStrategy != null)
                {
                    parms.raidStrategy = siegeStrategy; // 指定使用Siege策略
                }
                parms.faction = Utils.OfRKU;
                // 尝试触发事件
                if (!siegeIncident.Worker.TryExecute(parms))
                {
                    Log.Warning("[RKU] 迫击炮围攻袭击触发失败");
                }
            }
            catch (System.Exception e)
            {
                Log.Error($"[RKU] 触发迫击炮围攻袭击时发生错误: {e.Message}");
            }
        }

        // 触发基础普通袭击
        private void TriggerBasicRaid(float raidPoints)
        {
            try
            {
                Map targetMap = Find.AnyPlayerHomeMap;
                if (targetMap == null)
                {
                    return;
                }

                IncidentDef raidIncident = IncidentDefOf.RaidEnemy;
                IncidentParms parms = new IncidentParms();
                parms.faction= Utils.OfRKU;
                parms.target = targetMap;
                parms.forced = true;
                parms.points = Mathf.Max(raidPoints, 500f);
                
                if (parms.raidArrivalMode == null || parms.raidArrivalMode.defName.Contains("drop"))
                {
                    RaidStrategyDef immediateStrategy = DefDatabase<RaidStrategyDef>.GetNamed("ImmediateAttack");
                    parms.raidStrategy = immediateStrategy;
                    // 不要空投 
                    parms.raidArrivalMode = DefDatabase<PawnsArrivalModeDef>.AllDefs
                        .FirstOrDefault(d => !d.defName.Contains("Drop"));
                }

                // 尝试触发事件
                if (!raidIncident.Worker.TryExecute(parms))
                {
                    Log.Warning("[RKU] 基础袭击触发失败");
                }
            }
            catch (System.Exception e)
            {
                Log.Error($"[RKU] 触发基础袭击时发生错误: {e.Message}");
            }
        }

        // 触发鼠族隧道事件
        private void TriggerRatkinTunnelEvent(string eventDefName)
        {
            try
            {
                Map targetMap = Find.AnyPlayerHomeMap;
                if (targetMap == null)
                {
                    return;
                }
                IncidentDef tunnelEvent = DefDatabase<IncidentDef>.GetNamed(eventDefName);
                IncidentParms parms = new IncidentParms();
                parms.target = targetMap;
                parms.forced = true;
                parms.faction = Utils.OfRKU;

                // 尝试触发事件
                if (!tunnelEvent.Worker.TryExecute(parms))
                {
                    Log.Warning($"[RKU] {eventDefName}事件触发失败");
                }
            }
            catch (System.Exception e)
            {
                Log.Error($"[RKU] 触发{eventDefName}事件时发生错误: {e.Message}");
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref nextCheckTick, "nextCheckTick");
            Scribe_Values.Look(ref currentRaidPoints, "currentRaidPoints");
            Scribe_Values.Look(ref baseRaidPoints, "baseRaidPoints");
            Scribe_Values.Look(ref raidPointsMultiplier, "raidPointsMultiplier");
            Scribe_Values.Look(ref lastWealth, "lastWealth");
            Scribe_Values.Look(ref lastColonistCount, "lastColonistCount");
            Scribe_Values.Look(ref lastThreatPoints, "lastThreatPoints");
            Scribe_Values.Look(ref currentWave, "currentWave");
            Scribe_Values.Look(ref wavesSinceLastMortarRaid, "wavesSinceLastMortarRaid");
        }
    }
}
