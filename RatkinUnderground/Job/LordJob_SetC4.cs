using RimWorld;
using System;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RatkinUnderground
{
    public class LordJob_SetC4 : LordJob
    {
        private IntVec3 targetPosition;
        private Building targetBuilding;

        public LordJob_SetC4()
        {
        }

        public LordJob_SetC4(IntVec3 targetPos, Building targetBldg = null)
        {
            targetPosition = targetPos;
            targetBuilding = targetBldg;
        }

        public override StateGraph CreateGraph()
        {
            StateGraph graph = new StateGraph();

            // 创建设置炸弹的任务状态
            LordToil_SetC4 setC4Toil = new LordToil_SetC4(targetPosition, targetBuilding);
            graph.AddToil(setC4Toil);

            // 设置起始状态
            graph.StartingToil = setC4Toil;

            return graph;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref targetPosition, "targetPosition");
            Scribe_References.Look(ref targetBuilding, "targetBuilding");
        }
    }

    public class LordToil_SetC4 : LordToil
    {
        private IntVec3 targetPosition;
        private Building targetBuilding;

        public LordToil_SetC4(IntVec3 targetPos, Building targetBldg = null)
        {
            targetPosition = targetPos;
            targetBuilding = targetBldg;
        }

        public override void UpdateAllDuties()
        {
            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                Pawn pawn = lord.ownedPawns[i];

                // 如果有指定的目标建筑，让pawn去设置那个建筑
                if (targetBuilding != null && !targetBuilding.Destroyed)
                {
                    pawn.mindState.duty = new PawnDuty(DutyDefOf.AssaultColony);
                    Job job = JobMaker.MakeJob(DefOfs.RKU_SetC4, targetBuilding);
                    pawn.jobs.StartJob(job, JobCondition.InterruptForced);
                }
                else
                {
                    // 否则去指定的位置
                    pawn.mindState.duty = new PawnDuty(DutyDefOf.AssaultColony);
                    Job job = JobMaker.MakeJob(DefOfs.RKU_SetC4, targetPosition);
                    pawn.jobs.StartJob(job, JobCondition.InterruptForced);
                }
            }
        }

        public override void LordToilTick()
        {
            base.LordToilTick();

            // 检查是否所有pawn都完成了任务
            bool allDone = true;
            foreach (Pawn pawn in lord.ownedPawns)
            {
                if (pawn.jobs.curJob?.def == DefOfs.RKU_SetC4)
                {
                    allDone = false;
                    break;
                }
            }

            if (allDone)
            {
                // 所有任务完成后，结束lord任务
                lord.ReceiveMemo("AllC4Set");
            }
        }
    }
}