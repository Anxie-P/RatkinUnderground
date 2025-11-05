using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RatkinUnderground
{
    public class QuestPart_CheckGuerrillas : QuestPart
    {
        public string inSignal;
        public Map targetMap;
        public Faction faction;
        public IntVec3 spawnPosition; // 钻机生成位置
        private bool hasProcessed = false; // 防止重复处理

        public override void Notify_QuestSignalReceived(Signal signal)
        {
            if (signal.tag != inSignal)
            {
                return;
            }

            // 防止重复处理
            if (hasProcessed)
            {
                return;
            }
            
            if (targetMap == null || faction == null)
            {
                return;
            }

            // 查找游击队
            List<Pawn> guerrillas = new List<Pawn>();
            foreach (Pawn pawn in targetMap.mapPawns.AllPawnsSpawned)
            {
                if (pawn.Faction == faction && pawn.Spawned && !pawn.Dead && !pawn.Downed)
                {
                    guerrillas.Add(pawn);
                }
            }

            if (guerrillas.Count > 0)
            {
                // 使用钻机生成位置作为守卫点
                IntVec3 guardSpot = spawnPosition.IsValid ? spawnPosition : targetMap.Center;
                // 为每个游击队清除现有Lord并添加到新Lord
                foreach (Pawn guerrilla in guerrillas)
                {
                    // 检查游击队是否已经有 LordJob_WaitForResources
                    Lord currentLord = guerrilla.GetLord();
                    bool hasWaitForResourcesLord = currentLord?.LordJob is LordJob_WaitForResources;
                    
                    if (guerrilla != null && !guerrilla.Destroyed && guerrilla.Spawned && guerrilla.jobs != null && (currentLord == null || !hasWaitForResourcesLord))
                    {
                        // 创建新的LordJob - 使用简单的等待Lord，资源请求由QuestPart_RequestResources处理
                        var lordJob = new LordJob_WaitForResources(faction, guardSpot);
                        Lord lord = LordMaker.MakeNewLord(faction, lordJob, targetMap);
                        
                        // 清除现有Lord
                        if (currentLord != null)
                        {
                            currentLord.RemovePawn(guerrilla);
                        }
                        
                        // 如果当前有任务，强制结束
                        if (guerrilla.jobs.curDriver != null)
                        {
                            guerrilla.jobs.EndCurrentJob(JobCondition.InterruptForced, false);
                        }
                        
                        // 添加到新Lord
                        lord.AddPawn(guerrilla);
                    }
                    else if (hasWaitForResourcesLord)
                    {
                        // 已经有等待资源Lord，跳过
                    }
                    else
                    {
                        // 不符合分配条件，跳过
                    }
                }
                
                hasProcessed = true; // 标记为已处理
            }
            else
            {
                // 未找到游击队
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref inSignal, "inSignal");
            Scribe_References.Look(ref targetMap, "targetMap");
            Scribe_References.Look(ref faction, "faction");
            Scribe_Values.Look(ref spawnPosition, "spawnPosition");
            Scribe_Values.Look(ref hasProcessed, "hasProcessed", false);
        }
    }
} 