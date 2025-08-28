using RimWorld;
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

        // 事件相关
        public HashSet<string> triggeredOnceEvents = new HashSet<string>();
        public Dictionary<string, int> lastTriggerTimes = new Dictionary<string, int>();
        public bool isSearch = false;  // 开始研究

        public RKU_RadioGameComponent(Game game) { }
       
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

        public int ralationshipGrade = 0;
        public bool isFinal = false;

        #endregion

        #region 协助研究相关

        // 研究进度相关
        public float researchProgress = 0;//研究进度
        public byte researchGrade = 0;//研究等级
        public const float RESEARCH_PROGRESS_MAX = 3000; // 研究进度上限

        /// <summary>
        /// // 增加研究进度
        /// </summary>
        /// <param name="points"></param>
        public void AddResearchProgress(float points)
        {
            researchProgress = (float)Math.Round( Mathf.Min(researchProgress + points, RESEARCH_PROGRESS_MAX),2);
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
            Scribe_Values.Look(ref lastTradeTick, "lastTradeTick", 0);
            Scribe_Values.Look(ref canTrade, "canTrade", true);
            Scribe_Values.Look(ref isWaitingForTrade, "isWaitingForTrade", false);
            Scribe_Values.Look(ref tradeStartTick, "tradeStartTick", 0);
            Scribe_Values.Look(ref currentTradeDelayTicks, "currentTradeDelayTicks", 0);
            Scribe_Values.Look(ref researchProgress, "researchProgress", 0);
            Scribe_Values.Look<byte>(ref researchGrade, "researchGrade", 0);
            Scribe_Values.Look(ref ralationshipGrade, "ralationshipGrade", 0);
            Scribe_Values.Look(ref isFinal, "isFinal", false);
            Scribe_Values.Look(ref isSearch, "isSearch", false);
            Scribe_Collections.Look(ref triggeredOnceEvents, "RKU_triggeredOnceList", LookMode.Value);
            Scribe_Collections.Look(ref lastTriggerTimes, "lastTriggerTimes", LookMode.Value);
        }
    }
} 