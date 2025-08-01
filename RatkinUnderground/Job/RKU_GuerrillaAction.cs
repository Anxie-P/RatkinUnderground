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
        Faction faction;
        int escapeAfterTicks;
        public RKU_GuerrillaAction(Faction faction, int escapeAfterTicks = 20000) // 默认20000 ticks (大约8小时游戏时间)
        {
            this.faction = faction;
            this.escapeAfterTicks = escapeAfterTicks;
        }

        public override StateGraph CreateGraph()
        {
            StateGraph stateGraph = new StateGraph();
            // 初始状态
            LordToil_AssaultColony assaultToil = new LordToil_AssaultColony(true);
            stateGraph.StartingToil = assaultToil; // 将其设为初始状态

            // 逃跑
            LordToil_GuerrillaEscape escapeToil = new();
            stateGraph.AddToil(escapeToil);

            // 离开
            LordToil_GuerrillaExitMap exitToil = new();
            stateGraph.AddToil(exitToil);

            // 初始->逃跑
            Transition transitionToEscape = new Transition(assaultToil, escapeToil);
            // 触发器 减员50%
            transitionToEscape.AddTrigger(new Trigger_FractionPawnsLost(0.5f)); // 当损失50%的成员时逃跑
            stateGraph.AddTransition(transitionToEscape);

            // 初始->离开
            Transition transitionToExit = new Transition(assaultToil, exitToil);
            // 触发器 8小时
            transitionToExit.AddTrigger(new Trigger_TicksPassed(20000)); // 当损失50%的成员时逃跑
            stateGraph.AddTransition(transitionToExit);

            return stateGraph;
        }

        public override void ExposeData()
        {
            Scribe_References.Look(ref faction, "faction");
            Scribe_Values.Look(ref escapeAfterTicks, "escapeAfterTicks", 20000);
        }
    }

    // 离开
    public class LordToil_GuerrillaExitMap : LordToil
    {
        RKU_DrillingVehicleInEnemyMap drill;
        public override void UpdateAllDuties()
        {
            Log.Message("[RKU] 已执行自定义LordToil_GuerrillaExitMap");
            foreach (var building in lord.Map.listerBuildings.allBuildingsNonColonist)
            {
                if (building.def != DefOfs.RKU_DrillingVehicleInEnemyMap) continue;
                drill = (RKU_DrillingVehicleInEnemyMap)building;
            }

            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                var comp = lord.Map.GetComponent<RKU_GuerrillasLeave>();
                if (comp != null)
                {
                    Log.Message("含有RKU_GuerrillasLeave");
                    comp.SetStartLeave(true);
                }
                Pawn pawn = lord.ownedPawns[i];
                // 2. 如果找到了可用的钻井车
                if (drill != null)
                {
                    // 给 pawn 分配进入钻井车的任务
                    Job job = new Job(DefOfs.RKU_AIEnterDrillingVehicle, drill);
                    pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                }
                // 3. 如果没找到钻井车
                else
                {
                    // 执行标准的离开地图逻辑
                    pawn.mindState.duty = new PawnDuty(DutyDefOf.ExitMapRandom);
                }
            }
        }
    }

    // 逃跑
    public class LordToil_GuerrillaEscape : LordToil
    {
        RKU_DrillingVehicleInEnemyMap drill;
        public override void UpdateAllDuties()
        {
            Log.Message("[RKU] 已执行自定义LordToil_GuerrillaEscape");
            foreach (var building in lord.Map.listerBuildings.allBuildingsNonColonist)
            {
                if (building.def != DefOfs.RKU_DrillingVehicleInEnemyMap) continue;
                drill = (RKU_DrillingVehicleInEnemyMap)building;
            }

            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                var comp = lord.Map.GetComponent<RKU_GuerrillasLeave>();
                if (comp != null)
                {
                    Log.Message("含有RKU_GuerrillasLeave");
                    comp.SetStartLeave(true);
                }
                Pawn pawn = lord.ownedPawns[i];
                // 2. 如果找到了可用的钻井车
                if (drill != null)
                {
                    // 给 pawn 分配进入钻井车的任务
                    Job job = new Job(DefOfs.RKU_AIEnterDrillingVehicle, drill);
                    pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                }
                // 3. 如果没找到钻井车
                else
                {
                    // 执行标准的离开地图逻辑
                    pawn.mindState.duty = new PawnDuty(DutyDefOf.ExitMapRandom);
                }
            }
        }
    }
}
