using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RatkinUnderground
{
    public class RKU_GuerrillaAction : LordJob
    {
        public RKU_DrillingVehicleInEnemyMap_Und drill;
        public override bool AddFleeToil => false;
        public RKU_GuerrillaAction(RKU_DrillingVehicleInEnemyMap_Und drill)
        {
            this.drill = drill;
        }

        public override StateGraph CreateGraph()
        {
            StateGraph stateGraph = new StateGraph();
            // 食用地上的同伴 false    选择更强的武器 true
            LordToil_AssaultColony assaultToil = new LordToil_AssaultColony(false, true);
            stateGraph.StartingToil = assaultToil; // 将其设为初始状态
            stateGraph.transitions.RemoveAll(t => t.target is LordToil_PanicFlee);
            // 逃跑
            LordToil_GuerrillaEscape escapeToil = new(drill);
            stateGraph.AddToil(escapeToil);

            // 离开
            LordToil_GuerrillaExitMap exitToil = new(drill);
            stateGraph.AddToil(exitToil);

            // 初始->逃跑
            Transition transitionToEscape = new Transition(assaultToil, escapeToil);
            // 触发器 减员50%
            transitionToEscape.AddTrigger(new Trigger_FractionPawnsLost(0.5f)); // 当损失50%的成员时逃跑
            stateGraph.AddTransition(transitionToEscape);

            // 初始->离开
            Transition transitionToExit = new Transition(assaultToil, exitToil);
            // 触发器 8小时 20000
            transitionToExit.AddTrigger(new Trigger_TicksPassed(5000));
            stateGraph.AddTransition(transitionToExit);
            
            return stateGraph;
        }

        public override void ExposeData()
        {
            Scribe_References.Look(ref drill, "drill");
        }
    }

    // 离开
    public class LordToil_GuerrillaExitMap : LordToil
    {
        RKU_DrillingVehicleInEnemyMap_Und drill;
        public LordToil_GuerrillaExitMap(RKU_DrillingVehicleInEnemyMap_Und drill)
        {
            this.drill = drill;
        }
        public override void UpdateAllDuties()
        {

            Log.Message("[RKU] 已执行自定义LordToil_GuerrillaEscape");

            Log.Message($"{drill}");
            if (drill != null)
            {
                drill.StartLeave(true);
                for (int i = 0; i < lord.ownedPawns.Count; i++)
                {
                    Pawn pawn = lord.ownedPawns[i];
                    Job job = new Job(DefOfs.RKU_AIEnterDrillingVehicle, drill);
                    pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                    Log.Message($"{pawn}当前的job:{pawn.CurJob.def.defName}，进入{drill}");
                }
            }
            else
            {
                for (int i = 0; i < lord.ownedPawns.Count; i++)
                {
                    Pawn pawn = lord.ownedPawns[i];
                    pawn.mindState.duty = new PawnDuty(DutyDefOf.ExitMapBestAndDefendSelf);
                    pawn.mindState.duty.locomotion = LocomotionUrgency.Sprint;
                }
            }
        }
    }

    // 逃跑
    public class LordToil_GuerrillaEscape : LordToil
    {
        RKU_DrillingVehicleInEnemyMap_Und drill;
        public LordToil_GuerrillaEscape(RKU_DrillingVehicleInEnemyMap_Und drill)
        {
            this.drill = drill;
        }
        public override void UpdateAllDuties()
        {

            Log.Message("[RKU] 已执行自定义LordToil_GuerrillaEscape");
            
            Log.Message($"{drill}");
            if (drill!=null)
            {
                drill.StartLeave(true);
                for (int i = 0; i < lord.ownedPawns.Count; i++)
                {
                    Pawn pawn = lord.ownedPawns[i];
                    Job job = new Job(DefOfs.RKU_AIEnterDrillingVehicle, drill);
                    job.locomotionUrgency = LocomotionUrgency.Sprint;
                    pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                    Log.Message($"{pawn}当前的job:{pawn.CurJob.def.defName}，进入{drill}");
                }
            }
            else
            {
                for (int i = 0; i < lord.ownedPawns.Count; i++)
                {
                    Pawn pawn = lord.ownedPawns[i];
                    pawn.mindState.duty = new PawnDuty(DutyDefOf.ExitMapBestAndDefendSelf);
                    pawn.mindState.duty.locomotion = LocomotionUrgency.Sprint;
                }
            }
        }
    }
}
