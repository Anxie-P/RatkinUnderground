using RimWorld;
using System;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RatkinUnderground
{
    public class LordJob_WaitForResources : LordJob
    {
        private Faction faction;
        private int waitDuration = 60000; // 等待10分钟游戏时间
        private int startTick;
        private IntVec3 spot;

        public LordJob_WaitForResources(Faction faction, IntVec3 spot)
        {
            this.faction = faction;
            this.startTick = Find.TickManager.TicksGame;
            this.spot = spot;
        }

        public override StateGraph CreateGraph()
        {
            StateGraph stateGraph = new StateGraph();
            
            // 等待状态
            LordToil_WaitForResources waitToil = new LordToil_WaitForResources(spot);
            stateGraph.AddToil(waitToil);
            stateGraph.StartingToil = waitToil;
            
            // 离开状态
            LordToil_ExitMap exitToil = new LordToil_ExitMap();
            stateGraph.AddToil(exitToil);
            
            // 等待 -> 离开的转换
            Transition transitionToExit = new Transition(waitToil, exitToil);
            transitionToExit.AddTrigger(new Trigger_TicksPassed(waitDuration));
            stateGraph.AddTransition(transitionToExit);
            
            return stateGraph;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref faction, "faction");
            Scribe_Values.Look(ref waitDuration, "waitDuration", 60000);
            Scribe_Values.Look(ref startTick, "startTick");
            Scribe_Values.Look(ref spot, "spot");
        }
    }

    public class LordToil_WaitForResources : LordToil
    {
        private IntVec3 spot;

        public LordToil_WaitForResources(IntVec3 spot)
        {
            this.spot = spot;
        }

        public override void UpdateAllDuties()
        {
            foreach (Pawn pawn in lord.ownedPawns)
            {
                pawn.mindState.duty = new PawnDuty(DutyDefOf.TravelOrWait, spot);
                pawn.mindState.duty.radius = 10f;
            }
        }
    }
} 