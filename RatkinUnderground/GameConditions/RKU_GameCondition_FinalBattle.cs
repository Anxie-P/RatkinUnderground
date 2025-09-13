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

        // 动态袭击分配相关
        private int totalRaidsTriggered = 0;
        private bool isHammerRaided = false;

        // 士气值和损失度相关
        private float morale = 0.5f; // 士气值，0.5 = 50%，达到0时玩家胜利
        private int totalGuerrillaDeaths = 0; // 游击队总死亡数量
        private int lastCheckDeaths = 0; // 上次检查时的死亡数量
        private float initialWealth = 0f; // 初始财富值，用于计算财富损失

        // 公共属性，用于外部访问
        public float Morale => morale;
        public int TotalGuerrillaDeaths => totalGuerrillaDeaths;
        public void ModifyMorale(float amount)
        {
            morale = Mathf.Clamp(morale + amount, 0f, 1f);
        }

        public RKU_GameCondition_FinalBattle() { }

        public override void Init()
        {
            base.Init();
            nextCheckTick = Find.TickManager.TicksGame + CHECK_INTERVAL_TICKS;
            UpdateColonyStatus();
            foreach (Map map in Find.Maps)
            {
                if (map.IsPlayerHome)
                {
                    initialWealth += map.wealthWatcher.WealthBuildings + map.wealthWatcher.WealthPawns;
                }
            }
            Log.Warning(initialWealth.ToString());
            lastWealth = initialWealth;
            currentRaidPoints = CalculateRaidPoints();
            totalRaidsTriggered = 0;
            // 初始化士气值和损失度
            morale = 0.5f;
            totalGuerrillaDeaths = 0;
            lastCheckDeaths = 0;
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
                SendMoraleAndCasualtyLetter();
                nextCheckTick = Find.TickManager.TicksGame + CHECK_INTERVAL_TICKS;
            }
        }

        private void CheckColonyStatusAndAssignRaids()
        {
            // 更新殖民地状态
            UpdateColonyStatus();

            // 更新士气值和损失度
            UpdateMoraleAndCasualtyRate();

            // 检查胜利条件
            CheckVictoryConditions();

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
                    lastWealth += map.wealthWatcher.WealthBuildings + map.wealthWatcher.WealthPawns;
                    lastColonistCount += map.mapPawns.ColonistsSpawnedCount;
                    lastThreatPoints += StorytellerUtility.DefaultThreatPointsNow(map);
                }
            }
        }

        private float CalculateRaidPoints()
        {
            float points = baseRaidPoints;
            // 基于财富调整袭击点数
            if (lastWealth > 200000)
            {
                points *= 1.6f; // 高财富殖民地获得更强的袭击
            }
            else if (lastWealth > 100000)
            {
                points *= 1.4f;
            }
            else if (lastWealth > 50000)
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
            // 基于士气值调整袭击点数 - 士气越高，袭击越强
            points *= (1.0f + morale);
            return points * raidPointsMultiplier;
        }

        // 更新士气值和损失度
        private void UpdateMoraleAndCasualtyRate()
        {
            // 统计本次检查期间的游击队死亡数量
            int currentDeaths = CountGuerrillaCorpses();
            int deathsThisPeriod = currentDeaths - lastCheckDeaths;
            totalGuerrillaDeaths = currentDeaths;

            // 计算财富损失
            float wealthLoss = Mathf.Max(0, initialWealth - lastWealth);
            float moraleAdjustment = 0f;

            // 基于财富损失调整士气
            if (wealthLoss > 0)
            {
                moraleAdjustment += Mathf.Min(0.3f, wealthLoss / 50000f); // 最多增加0.3
            }

            // 基于死亡人数调整士气
            if (deathsThisPeriod > 0)
            {
                moraleAdjustment -= Mathf.Min(0.2f, deathsThisPeriod * 0.003f); //每个人0.003
            }

            // 更新士气值 (限制在0.0到1.0之间)
            morale = Mathf.Clamp(morale + moraleAdjustment, 0f, 1f);
        }

        // 检查胜利条件
        private void CheckVictoryConditions()
        {
            // 士气值降至0
            if (morale <= 0f)
            {
                Utils.BroadcastRadioMessage("士气崩溃。");
                this.End();
                return;
            }

            // 死亡500人以上
            if (totalGuerrillaDeaths > 500)
            {
                Utils.BroadcastRadioMessage("全军覆没。");
                this.End();
                return;
            }

            // 特殊事件触发条件：士气值降至12%以下 或 游击队损失度大于600人
            if ((morale <= 0.20f || totalGuerrillaDeaths >= 400) && !isHammerRaided)
            {
                TriggerHammerAttackEvent();
            }

        }

        // 统计地图上鼠族游击队阵营的尸体数量
        private int CountGuerrillaCorpses()
        {
            int corpseCount = 0;

            foreach (Map map in Find.Maps)
            {
                if (map.IsPlayerHome)
                {
                    foreach (Thing thing in map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse))
                    {
                        Corpse corpse = thing as Corpse;
                        if (corpse != null && corpse.InnerPawn != null)
                        {
                            if (corpse.InnerPawn.Faction != null && corpse.InnerPawn.Faction == Utils.OfRKU)
                            {
                                corpseCount++;
                            }
                        }
                    }
                }
            }
            return corpseCount;
        }

        private void AssignRaidsBasedOnPoints(float totalPoints, float pointsChange)
        {
            // 增加总袭击计数
            totalRaidsTriggered++;
            // 基于士气值和损失度分配不同类型的袭击
            string raidMessage = "";
            // 确保关系为敌对
            if (Utils.OfRKU.RelationWith(Faction.OfPlayer).kind != FactionRelationKind.Hostile)
            {
                FactionRelationKind kind2 = Utils.OfRKU.RelationWith(Faction.OfPlayer).kind;
                Utils.OfRKU.RelationWith(Faction.OfPlayer).kind = FactionRelationKind.Hostile;
                Faction.OfPlayer.RelationWith(Utils.OfRKU).kind = FactionRelationKind.Hostile;
                Utils.OfRKU.Notify_RelationKindChanged(Faction.OfPlayer, kind2, false, "", TargetInfo.Invalid, out var sentLetter);
                Faction.OfPlayer.Notify_RelationKindChanged(Utils.OfRKU, kind2, false, "", TargetInfo.Invalid, out sentLetter);
            }
            // 计算伤亡比例 (基于总死亡数占800的百分比)
            float casualtyRatio = Mathf.Min(1.0f, totalGuerrillaDeaths / 500f);
            // 决定可用的袭击类型
            bool canTriggerAllTypes = (morale > 0.75f || casualtyRatio > 0.7f || morale < 0.4f);
            bool canTriggerThiTunnel = (morale < 0.3f);
            // 基于当前状态动态分配袭击
            if (canTriggerAllTypes)
            {
                // 高士气或高伤亡时，随机触发多种组合
                TriggerDynamicRaidCombination(totalPoints, canTriggerThiTunnel);
            }
            else
            {
                // 正常状态下的袭击分配
                TriggerNormalRaidCombination(totalPoints, canTriggerThiTunnel);
            }
            // 发布电台消息
            if (!string.IsNullOrEmpty(raidMessage))
            {
                Utils.BroadcastRadioMessage(raidMessage);
            }
        }

        // 高强度状态下的动态袭击组合
        private void TriggerDynamicRaidCombination(float totalPoints, bool canTriggerThiTunnel)
        {
            // 基础围攻袭击 (高强度状态下必定触发)
            TriggerMortarSiegeRaid(totalPoints);
            // 随机额外事件 (33%概率)
            if (Rand.Value < 0.33f)
            {
                float random = Rand.Range(0, 1f);
                if (random < 0.4f)
                {
                    // 额外基础袭击
                    TriggerBasicRaid(Mathf.Clamp(totalPoints * 0.5f, 500f, 3000f));
                }
                else if (random < 0.8f || !canTriggerThiTunnel)
                {
                    // 鼠族隧道第一种
                    TriggerRatkinTunnelEvent("RKU_RatkinTunnel_Fir");
                }
                else
                {
                    // 伊文袭击
                    TriggerRatkinTunnelEvent("RKU_RatkinTunnel_Thi");
                }
            }
        }

        // 正常状态下的袭击组合
        private void TriggerNormalRaidCombination(float totalPoints, bool canTriggerThiTunnel)
        {
            float random = Rand.Range(0, 1f);
            if (random < 0.35f)
            {
                // 基础袭击
                TriggerBasicRaid(Mathf.Clamp(totalPoints, 500f, 3000f));
            }
            else if (random < 0.7f)
            {
                // 隧道+ 基础
                TriggerRatkinTunnelEvent("RKU_RatkinTunnel_Fir");
                TriggerBasicRaid(Mathf.Clamp(totalPoints * 0.6f, 500f, 2000f));
            }
            else if (random < 0.9f || !canTriggerThiTunnel)
            {
                // 20% 概率：围攻 + 基础
                TriggerMortarSiegeRaid(totalPoints);
                TriggerBasicRaid(Mathf.Clamp(totalPoints * 0.7f, 500f, 2500f));
            }
            else
            {
                // 10% 概率：伊文+一般 
                TriggerRatkinTunnelEvent("RKU_RatkinTunnel_Thi");
                TriggerBasicRaid(Mathf.Clamp(totalPoints * 0.5f, 500f, 2000f));
            }
        }

        // 发送游击队状态信封消息
        private void SendMoraleAndCasualtyLetter()
        {
            // 计算财富损失
            Log.Warning(initialWealth.ToString());
            Log.Warning(lastWealth.ToString());
            float wealthLoss = Mathf.Max(0, initialWealth - lastWealth);
            // 计算本次周期的死亡数（用于显示）
            int recentDeaths = Mathf.Max(0, totalGuerrillaDeaths - lastCheckDeaths);
            // 创建信封消息
            string letterTitle = "状态报告";
            string letterText = $"当前游击队士气值: {(morale * 100):F1}%\n" +
                               $"当前游击队损失度: {(totalGuerrillaDeaths / 500f * 100):F1}%\n" +
                               $"累计游击队阵亡数: {totalGuerrillaDeaths}\n\n" +
                               $"财富影响: 殖民地已损失 {wealthLoss:F0} 财富值\n" +
                               $" - 当前财富损失使士气增加约 {(wealthLoss > 0 ? Mathf.Min(30f, wealthLoss / 50000f * 100) : 0):F1}%\n\n" +
                               $"阵亡影响: 本周期阵亡 {recentDeaths} 名游击队员\n" +
                               $" - 当前阵亡数使士气减少约 {(recentDeaths > 0 ? Mathf.Min(20f, recentDeaths * 0.003f * 100) : 0):F1}%\n\n" +
                               $"袭击强度倍率: {1.0f + morale:F2}倍\n" +
                               $" (受士气值与财富值影响)";
            Find.LetterStack.ReceiveLetter(letterTitle, letterText, LetterDefOf.NeutralEvent);
            lastCheckDeaths = totalGuerrillaDeaths;
            initialWealth = lastWealth;
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
                parms.points = Mathf.Max(raidPoints, 1000f);
                RaidStrategyDef siegeStrategy = DefDatabase<RaidStrategyDef>.GetNamed("Siege");
                if (siegeStrategy != null)
                {
                    parms.raidStrategy = siegeStrategy;
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
                parms.faction = Utils.OfRKU;
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

        // 特殊事件：地锤袭击
        private void TriggerHammerAttackEvent()
        {
            Map targetMap = Find.AnyPlayerHomeMap;
            if (targetMap == null)
            {
                return;
            }
            RaidStrategyDef hammerAttackStrategy = DefDatabase<RaidStrategyDef>.GetNamed("RKU_HammerAttack");
            IncidentDef raidIncident = IncidentDefOf.RaidEnemy;
            IncidentParms parms = new IncidentParms();
            parms.faction = Utils.OfRKU;
            parms.target = targetMap;
            parms.forced = true;
            parms.points = Mathf.Max(StorytellerUtility.DefaultThreatPointsNow(targetMap) * 2.0f, 1500f);
            parms.raidStrategy = hammerAttackStrategy;
            if (parms.raidArrivalMode == null || parms.raidArrivalMode.defName.Contains("drop"))
            {
                var nonDropModes = DefDatabase<PawnsArrivalModeDef>.AllDefs
                    .Where(d => !d.defName.Contains("Drop")).ToList();
                if (nonDropModes.Any())
                {
                    parms.raidArrivalMode = nonDropModes.RandomElement();
                }
            }

            if (!raidIncident.Worker.TryExecute(parms))
            {
                Log.Warning("[RKU] 撼地袭击触发失败");
            }
            else
            {
                Find.LetterStack.ReceiveLetter(
                    "撼地装置",
                    "游击队准备部署他们的超级武器！",
                    LetterDefOf.ThreatBig
                );
            }
            isHammerRaided = true;
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
            Scribe_Values.Look(ref totalRaidsTriggered, "totalRaidsTriggered");
            Scribe_Values.Look(ref isHammerRaided, "isHammerRaided");
            // 保存士气值和损失度相关数据
            Scribe_Values.Look(ref morale, "morale");
            Scribe_Values.Look(ref totalGuerrillaDeaths, "totalGuerrillaDeaths");
            Scribe_Values.Look(ref lastCheckDeaths, "lastCheckDeaths");
            Scribe_Values.Look(ref initialWealth, "initialWealth");
        }
    }
}
