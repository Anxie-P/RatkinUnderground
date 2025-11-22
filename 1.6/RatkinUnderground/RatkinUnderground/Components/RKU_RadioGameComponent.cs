using RimWorld;
using RimWorld.QuestGen;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RatkinUnderground
{
    public class RKU_RadioGameComponent : GameComponent
    {
        // 交易冷却相关
        public int lastTradeTick = 0;
        public int tradeCooldownTicks = 400000; // 10天
        public bool canTrade = true;

        // 交易准备相关==
        public bool isWaitingForTrade = false;
        public int tradeStartTick = 0;
        public int currentTradeDelayTicks = 0;
        public int minTradeDelayTicks = 1000; // 最小交易延迟
        public int maxTradeDelayTicks = 3000; // 最大交易延迟

        // 扫描相关
        public int lastScanTick = 0;
        public int scanCooldownTicks = 900000; // 扫描冷却时间
        public bool canScan = true;

        // 求救呼叫相关
        public int lastEmergencyTick = 0;
        public int emergencyCooldownTicks = 300000; // 5天冷却时间
        public bool canEmergency = true;

        // 事件相关
        public HashSet<string> triggeredOnceEvents;
        public Dictionary<string, int> lastTriggerTimes;
        public bool isSearch = false;  // 开始研究

        // 支援相关
        public int lastRescueTick = 0;
        public int rescueCooldownTicks = 300000;
        public bool canRescue = true;

        // 延时事件计时器
        public int relationWarningLightTriggerTick = -1;
        public int relationWarningSeriousTriggerTick = -1;

        public RKU_RadioGameComponent(Game game)
        {
            // 确保集合被正确初始化
            if (triggeredOnceEvents == null)
                triggeredOnceEvents = new HashSet<string>();
            if (lastTriggerTimes == null)
                lastTriggerTimes = new Dictionary<string, int>();
        }

        #region 交易相关方法
        public bool CanTradeNow => canTrade && !isWaitingForTrade;

        public bool IsWaitingForTrade => isWaitingForTrade;

        public string GetRemainingTradeTime()
        {
            if (!isWaitingForTrade) return "";

            int remainingTicks = currentTradeDelayTicks - (Find.TickManager.TicksGame - tradeStartTick);
            int remainingSeconds = Mathf.Max(0, remainingTicks / TICKS_PER_SECOND);
            return $"{remainingSeconds}s";
        }


        // 时间转换常量
        private const int TICKS_PER_SECOND = 60; // RimWorld中1秒 = 60 ticks
        private const int TICKS_PER_DAY = 60000; // RimWorld中1天 = 60000 ticks

        /// <summary>
        /// 计算剩余冷却天数（通用方法）
        /// </summary>
        /// <param name="canUse">是否可以使用（如果为true则返回0）</param>
        /// <param name="cooldownTicks">冷却总时长（tick）</param>
        /// <param name="lastUseTick">上次使用时间（tick）</param>
        /// <returns>剩余冷却天数</returns>
        private int CalculateRemainingCooldownDays(bool canUse, int cooldownTicks, int lastUseTick)
        {
            if (canUse) return 0;
            int elapsedTicks = Find.TickManager.TicksGame - lastUseTick;
            int remainingTicks = cooldownTicks - elapsedTicks;
            return Mathf.Max(0, remainingTicks / TICKS_PER_DAY);
        }

        public int GetRemainingCooldownDays()
        {
            return CalculateRemainingCooldownDays(canTrade, tradeCooldownTicks, lastTradeTick);
        }

        public int GetRemainingRescueCooldownDays()
        {
            return CalculateRemainingCooldownDays(canRescue, rescueCooldownTicks, lastRescueTick);
        }

        public int GetRemainingScanCooldownDays()
        {
            return CalculateRemainingCooldownDays(canScan, scanCooldownTicks, lastScanTick);
        }

        public int GetRemainingEmergencyCooldownDays()
        {
            return CalculateRemainingCooldownDays(canEmergency, emergencyCooldownTicks, lastEmergencyTick);
        }

        public void StartTradeSignal()
        {
            if (!canTrade) return;

            currentTradeDelayTicks = Rand.Range(minTradeDelayTicks, maxTradeDelayTicks);
            tradeStartTick = Find.TickManager.TicksGame;
            isWaitingForTrade = true;
        }

        public void ResetTradeCooldown()
        {
            canTrade = true;
            isWaitingForTrade = false;
        }

        /// <summary>
        /// 重置所有冷却（开发者工具）
        /// </summary>
        public void ResetAllCooldowns()
        {
            // 重置交易冷却
            canTrade = true;
            isWaitingForTrade = false;
            lastTradeTick = 0;

            // 重置扫描冷却
            canScan = true;
            lastScanTick = 0;

            // 重置救援冷却
            canRescue = true;
            lastRescueTick = 0;

            // 重置紧急呼叫冷却
            canEmergency = true;
            lastEmergencyTick = 0;
        }
        #endregion

        #region 好感度相关

        private int _ralationshipGrade = 0;

        // 好感度范围限制
        public int minRelationshipGrade = -25;
        public int maxRelationshipGrade = 25;

       public int ralationshipGrade
{
    get => _ralationshipGrade;
    set
    {
        // 限制在设置的范围内
        int clampedValue = Mathf.Clamp(value, minRelationshipGrade, maxRelationshipGrade);
        if (_ralationshipGrade != clampedValue)
        {
            // 检查是否超出限制范围
            if (value < minRelationshipGrade || value > maxRelationshipGrade)
            {
                Messages.Message("RKU_RelationLimitExceeded".Translate(), MessageTypeDefOf.RejectInput);
            }
            
            _ralationshipGrade = clampedValue;
            SyncRelationshipGradeToFaction();
        }
    }
}

        public bool isFinal = false;

        public override void LoadedGame()
        {
            base.LoadedGame();
            SyncRelationshipGradeFromFaction();
        }

        /// <summary>
        /// 游戏加载时同步实际好感度到ralationshipGrade
        /// </summary>
        private void SyncRelationshipGradeFromFaction()
        {
            Faction rkuFaction = Find.FactionManager.FirstFactionOfDef(DefOfs.RKU_Faction);
            Faction playerFaction = Faction.OfPlayer;

            if (rkuFaction != null && playerFaction != null)
            {
                int actualGoodwill = rkuFaction.GoodwillWith(playerFaction);
                if (ralationshipGrade != actualGoodwill)
                {
                    ralationshipGrade = actualGoodwill;
                }
            }
        }

        private void SyncRelationshipGradeToFaction()
        {
            Faction rkuFaction = Find.FactionManager.FirstFactionOfDef(DefOfs.RKU_Faction);
            Faction playerFaction = Faction.OfPlayer;

            if (rkuFaction != null && playerFaction != null)
            {
                int currentGoodwill = rkuFaction.GoodwillWith(playerFaction);
                if (currentGoodwill != _ralationshipGrade)
                {
                    // 直接设置基础好感度
                    FactionRelation relation1 = rkuFaction.RelationWith(playerFaction);
                    FactionRelation relation2 = playerFaction.RelationWith(rkuFaction);
                    relation1.baseGoodwill = _ralationshipGrade;
                    relation2.baseGoodwill = _ralationshipGrade;
                    // 检查关系类型变化
                    relation1.CheckKindThresholds(rkuFaction, true, "RKU Relationship Sync", default, out _);
                    relation2.kind = relation1.kind;
                }
            }
        }
        #endregion
        
        #region 协助研究相关

        // 研究进度相关
        public float researchProgress = 0;//研究进度
        public byte researchGrade = 0;//研究等级
        public const float RESEARCH_PROGRESS_MAX = 2000; // 研究进度上限

        /// <summary>
        /// // 增加研究进度
        /// </summary>
        /// <param name="points"></param>
        public void AddResearchProgress(float points)
        {
            researchProgress = (float)Math.Round(Mathf.Min(researchProgress + points, RESEARCH_PROGRESS_MAX), 2);
            if (researchProgress >= RESEARCH_PROGRESS_MAX)
            {
                researchProgress = 0;
                researchGrade++;
            }
        }

        #endregion

        public override void ExposeData()
        {
            base.ExposeData();

            // 确保集合在保存/加载前被正确初始化
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                if (triggeredOnceEvents == null)
                    triggeredOnceEvents = new HashSet<string>();
                if (lastTriggerTimes == null)
                    lastTriggerTimes = new Dictionary<string, int>();
            }

            Scribe_Values.Look(ref lastTradeTick, "lastTradeTick", 0);
            Scribe_Values.Look(ref canTrade, "canTrade", true);
            Scribe_Values.Look(ref isWaitingForTrade, "isWaitingForTrade", false);
            Scribe_Values.Look(ref lastScanTick, "lastScanTick", 0);
            Scribe_Values.Look(ref canScan, "canScan", true);
            Scribe_Values.Look(ref lastRescueTick, "lastRescueTick", 0);
            Scribe_Values.Look(ref canRescue, "canRescue", true);
            Scribe_Values.Look(ref lastEmergencyTick, "lastEmergencyTick", 0);
            Scribe_Values.Look(ref canEmergency, "canEmergency", true);
            Scribe_Values.Look(ref tradeStartTick, "tradeStartTick", 0);
            Scribe_Values.Look(ref currentTradeDelayTicks, "currentTradeDelayTicks", 0);
            Scribe_Values.Look(ref researchProgress, "researchProgress", 0);
            Scribe_Values.Look<byte>(ref researchGrade, "researchGrade", 0);
            Scribe_Values.Look(ref _ralationshipGrade, "ralationshipGrade", 0);
            Scribe_Values.Look(ref minRelationshipGrade, "minRelationshipGrade", -25);
            Scribe_Values.Look(ref maxRelationshipGrade, "maxRelationshipGrade", 25);
            Scribe_Values.Look(ref isFinal, "isFinal", false);
            Scribe_Values.Look(ref isSearch, "isSearch", false);
            Scribe_Values.Look(ref relationWarningLightTriggerTick, "relationWarningLightTriggerTick", -1);
            Scribe_Values.Look(ref relationWarningSeriousTriggerTick, "relationWarningSeriousTriggerTick", -1);
            Scribe_Collections.Look(ref triggeredOnceEvents, "RKU_triggeredOnceList", LookMode.Value);
            Scribe_Collections.Look(ref lastTriggerTimes, "lastTriggerTimes", LookMode.Value, LookMode.Value);
        }
    }
}