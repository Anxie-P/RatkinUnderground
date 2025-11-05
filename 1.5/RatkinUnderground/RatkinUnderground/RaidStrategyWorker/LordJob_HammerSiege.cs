using RimWorld;
using System;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RatkinUnderground
{
    public class LordJob_HammerSiege : LordJob_Siege
    {
        private Faction faction;

        private IntVec3 siegeSpot;

        private float blueprintPoints;

        public override bool GuiltyOnDowned => true;

        public LordJob_HammerSiege():base()
        {
        }

        public LordJob_HammerSiege(Faction faction, IntVec3 siegeSpot, float blueprintPoints)
        {
            this.faction = faction;
            this.siegeSpot = siegeSpot;
            this.blueprintPoints = blueprintPoints;
        }

        public override StateGraph CreateGraph()
        {
            Log.Message($"[RKU] 开始创建锤子围攻StateGraph - 围攻点: {siegeSpot}");
            StateGraph stateGraph = new StateGraph();

            // 创建旅行toil - 使用AttachSubgraph保持完整性
            Log.Message("[RKU] 创建旅行子图");
            LordToil startingToil = stateGraph.AttachSubgraph(new LordJob_Travel(siegeSpot).CreateGraph()).StartingToil;

            // 创建锤子围攻toil
            Log.Message("[RKU] 创建锤子围攻toil");
            LordToil_SiegeHammer lordToil_SiegeHammer = new LordToil_SiegeHammer(siegeSpot, blueprintPoints);
            stateGraph.AddToil(lordToil_SiegeHammer);

            // 创建退出地图toil
            Log.Message("[RKU] 创建退出toil");
            LordToil_ExitMap lordToil_ExitMap = new LordToil_ExitMap(LocomotionUrgency.Jog, canDig: false, interruptCurrentJob: true)
            {
                useAvoidGrid = true
            };
            stateGraph.AddToil(lordToil_ExitMap);

            // 添加从旅行到围攻的转换
            Log.Message("[RKU] 添加转换");
            Transition transition = new Transition(startingToil, lordToil_SiegeHammer);
            transition.AddTrigger(new Trigger_Memo("TravelArrived"));
            transition.AddTrigger(new Trigger_TicksPassed(5000)); 
            stateGraph.AddTransition(transition);

            // 添加从围攻到退出的转换（当围攻失败时）
            Transition exitTransition = new Transition(lordToil_SiegeHammer, lordToil_ExitMap);
            exitTransition.AddTrigger(new Trigger_Memo("NoArtillery"));
            exitTransition.AddTrigger(new Trigger_FractionPawnsLost(10f));
            stateGraph.AddTransition(exitTransition);

            return stateGraph;
        }

    public override void Notify_PawnLost(Pawn pawn, PawnLostCondition condition)
    {
        base.Notify_PawnLost(pawn, condition);
    }

    public override void Notify_LordDestroyed()
    {
        base.Notify_LordDestroyed();
    }

    public override void ExposeData()
    {
        Scribe_References.Look(ref faction, "faction");
        Scribe_Values.Look(ref siegeSpot, "siegeSpot");
        Scribe_Values.Look(ref blueprintPoints, "blueprintPoints", 0f);
    }
    }
}