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
        public int emergencyCooldownTicks = 420000; // 7天冷却时间
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
            int remainingSeconds = Mathf.Max(0, remainingTicks / 60);
            return $"{remainingSeconds}s";
        }

        public int GetRemainingCooldownDays()
        {
            if (canTrade) return 0;

            int remainingCooldown = tradeCooldownTicks - (Find.TickManager.TicksGame - lastTradeTick);
            return Mathf.Max(0, remainingCooldown / 60000);
        }

        public int GetRemainingScanCooldownDays()
        {
            int remainingCooldown = scanCooldownTicks - (Find.TickManager.TicksGame - lastScanTick);
            return Mathf.Max(0, remainingCooldown / 60000);
        }

        public int GetRemainingEmergencyCooldownDays()
        {
            if (canEmergency) return 0;
            int remainingCooldown = emergencyCooldownTicks - (Find.TickManager.TicksGame - lastEmergencyTick);
            return Mathf.Max(0, remainingCooldown / 60000);
        }

        public void StartTradeSignal()
        {
            if (!canTrade) return;

            // 设置交易延迟
            currentTradeDelayTicks = Rand.Range(minTradeDelayTicks, maxTradeDelayTicks);
            tradeStartTick = Find.TickManager.TicksGame;
            isWaitingForTrade = true;
        }

        public void ResetTradeCooldown()
        {
            canTrade = true;
            isWaitingForTrade = false;
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