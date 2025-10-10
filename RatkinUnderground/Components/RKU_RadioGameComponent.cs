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

        // 延时事件计时器
        public int relationWarningLightTriggerTick = -1;
        public int relationWarningSeriousTriggerTick = -1; 
        public int guerrillaCampLastOfferTick = -1;
        public bool guerrillaCampOfferPending = false; 

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

        #region 延时事件处理
        public override void GameComponentTick()
        {
            base.GameComponentTick();
            // 每0.5天检查一次
            if (Find.TickManager.TicksGame % 30000 != 0) return;
            HandleDelayedEvents();
        }

        private void HandleDelayedEvents()
        {
            int currentTick = Find.TickManager.TicksGame;
            int days1to3Ticks = Rand.Range(60000, 180000); // 1-3天
            int days3Ticks = 180000; // 3天

            // 土匪营地
            if (relationWarningLightTriggerTick > 0)
            {
                int ticksSinceLightWarning = currentTick - relationWarningLightTriggerTick;
                if (ticksSinceLightWarning >= days1to3Ticks && !guerrillaCampOfferPending)
                {
                    TryOfferGuerrillaCampQuest();
                }
            }
            // 伊文入侵
            if (relationWarningSeriousTriggerTick > 0)
            {
                int ticksSinceSeriousWarning = currentTick - relationWarningSeriousTriggerTick;
                if (ticksSinceSeriousWarning >= days1to3Ticks)
                {
                    TryTriggerRatkinTunnelThi();
                    relationWarningSeriousTriggerTick = -1; 
                }
            }

            // 定期检查游击队营地任务发布
            if (guerrillaCampLastOfferTick > 0)
            {
                int ticksSinceLastOffer = currentTick - guerrillaCampLastOfferTick;
                // 隔3天再次检查是否可以发布任务
                if (ticksSinceLastOffer >= days3Ticks)
                {
                    TryOfferGuerrillaCampQuest();
                }
            }
            else if (guerrillaCampLastOfferTick == -1 && ralationshipGrade == -25)
            {
                // 初始检查，如果好感度为-25且从未发布过任务
                TryOfferGuerrillaCampQuest();
            }
        }

        private void TryOfferGuerrillaCampQuest()
        {
            QuestScriptDef questDef = DefDatabase<QuestScriptDef>.GetNamed("RKU_OpportunitySite_GuerrillaCamp", false);
            if (Find.QuestManager.QuestsListForReading.Any(o => o.root == questDef)) return;
            // 创建一个基本的slate来检查任务是否可以运行
            Slate slate = new Slate();
            slate.Set("points", StorytellerUtility.DefaultThreatPointsNow(Find.World));
            slate.Set("asker", Find.FactionManager.FirstFactionOfDef(FactionDef.Named("Rakinia"))?.leader);
            slate.Set("enemyFaction", Find.FactionManager.FirstFactionOfDef(DefOfs.RKU_Faction));

            if (!questDef.CanRun(slate))
            {
                guerrillaCampOfferPending = true;
                return;
            }
            Quest quest = QuestUtility.GenerateQuestAndMakeAvailable(questDef, StorytellerUtility.DefaultThreatPointsNow(Find.World));
            if (quest != null)
            {
                guerrillaCampLastOfferTick = Find.TickManager.TicksGame;
                guerrillaCampOfferPending = false;
            }
        }

        private void TryTriggerRatkinTunnelThi()
        {
            // 触发伊文事件
            IncidentDef incidentDef = DefDatabase<IncidentDef>.GetNamed("RKU_RatkinTunnel_Thi", false);
            if (incidentDef != null && incidentDef.Worker.CanFireNow(new IncidentParms { target = Find.AnyPlayerHomeMap }))
            {
                IncidentParms parms = StorytellerUtility.DefaultParmsNow(incidentDef.category, Find.AnyPlayerHomeMap);
                incidentDef.Worker.TryExecute(parms);
            }
        }


        private bool HasQuestInPool(string questDefName)
        {
            foreach (Quest quest in Find.QuestManager.QuestsListForReading)
            {
                if (quest.root.defName == questDefName && (!quest.dismissed))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// -25好感度启动延时计时器
        /// </summary>
        public void OnRelationWarningLightTriggered()
        {
            relationWarningLightTriggerTick = Find.TickManager.TicksGame;
        }

        /// <summary>
        /// -75好感度启动延时计时器
        /// </summary>
        public void OnRelationWarningSeriousTriggered()
        {
            relationWarningSeriousTriggerTick = Find.TickManager.TicksGame;
        }

        /// <summary>
        /// 调试：立即触发延时事件
        /// </summary>
        public void DebugTriggerDelayedEvents()
        {
            if (!Prefs.DevMode) return;

            int currentTick = Find.TickManager.TicksGame;

            // 立即设置游击队营地任务计时器为即将触发状态
            if (relationWarningLightTriggerTick > 0)
            {
                relationWarningLightTriggerTick = currentTick - 60000; // 设置为1天前，立即触发
            }

            // 立即设置伊文入侵计时器为即将触发状态
            if (relationWarningSeriousTriggerTick > 0)
            {
                relationWarningSeriousTriggerTick = currentTick - 60000; // 设置为1天前，立即触发
            }

            // 强制执行延时事件检查
            HandleDelayedEvents();
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
            Scribe_Values.Look(ref guerrillaCampLastOfferTick, "guerrillaCampLastOfferTick", -1);
            Scribe_Values.Look(ref guerrillaCampOfferPending, "guerrillaCampOfferPending", false);
            Scribe_Collections.Look(ref triggeredOnceEvents, "RKU_triggeredOnceList", LookMode.Value);
            Scribe_Collections.Look(ref lastTriggerTimes, "lastTriggerTimes", LookMode.Value, LookMode.Value);
        }
    }
}