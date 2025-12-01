using RimWorld;
using System;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using System.Collections.Generic;
using static UnityEngine.GraphicsBuffer;

namespace RatkinUnderground
{
    public class LordJob_WaitForItemsAndReturn : LordJob
    {
        private IntVec3 idleSpot;
        private Faction faction;
        private Pawn target;
        private ThingDef thingDef;
        private RKU_DrillingVehicleInEnemyMap drillingVehicle => Map.listerBuildings.allBuildingsNonColonist.Find(b => b is RKU_DrillingVehicleInEnemyMap) as RKU_DrillingVehicleInEnemyMap;
        private int amount;
        private string outSignalItemsReceived;
        private string outSignalStartReturnToDrillingVehicle;

        public LordJob_WaitForItemsAndReturn()
        {
        }

        public LordJob_WaitForItemsAndReturn(Faction faction, IntVec3 idleSpot, Pawn target, ThingDef thingDef, RKU_DrillingVehicleInEnemyMap drillingVehicle, int amount, string outSignalItemsReceived, string outSignalStartReturnToDrillingVehicle)
        {
            this.idleSpot = idleSpot;
            this.faction = faction;
            this.target = target;
            this.thingDef = thingDef;
            this.amount = Math.Abs(amount);
            this.outSignalItemsReceived = outSignalItemsReceived;
            this.outSignalStartReturnToDrillingVehicle = outSignalStartReturnToDrillingVehicle;
        }

        public override StateGraph CreateGraph()
        {
            StateGraph stateGraph = new StateGraph();

            LordToil_TravelAndWaitForItems lordToil_TravelAndWaitForItems = new LordToil_TravelAndWaitForItems(idleSpot, target, thingDef, amount);
            stateGraph.AddToil(lordToil_TravelAndWaitForItems);
            stateGraph.StartingToil = lordToil_TravelAndWaitForItems;
            
            LordToil_WaitForItems waitForItems = new LordToil_WaitForItems(target, thingDef, amount, idleSpot);
            stateGraph.AddToil(waitForItems);
            
            LordToil_ExitMap lordToil_ExitMap = new LordToil_ExitMap();
            stateGraph.AddToil(lordToil_ExitMap);
            
            LordToil_ExitMapAndDefendSelf toil = new LordToil_ExitMapAndDefendSelf();
            stateGraph.AddToil(toil);

            LordToil_WaitForReturnSignal waitForReturnSignal = new LordToil_WaitForReturnSignal();
            stateGraph.AddToil(waitForReturnSignal);

            LordToil_FixDrill fixDrill = new(drillingVehicle);
            stateGraph.AddToil(fixDrill);

            

            // 从旅行状态转换到等待物品状态
            Transition transition = new Transition(lordToil_TravelAndWaitForItems, waitForItems);
            transition.AddTrigger(new Trigger_Memo("TravelArrived"));
            stateGraph.AddTransition(transition);

            // 从等待物品状态转换到修理钻机
            Transition transition2 = new Transition(waitForItems, fixDrill);
            transition2.AddTrigger(new Trigger_Custom((TriggerSignal s) => 
            {
                bool hasAllItems = waitForItems.HasAllRequestedItems;
                return hasAllItems;
            }));

            stateGraph.AddTransition(transition2);

            // 修完钻机到离开
            Transition transitionFix = new Transition(fixDrill, waitForReturnSignal);
            transitionFix.AddTrigger(new Trigger_Custom((TriggerSignal s) =>
            {
                if (drillingVehicle == null)
                {
                    Log.Message("drill为空");
                    return false;
                }
                bool hitPoints = drillingVehicle.HitPoints >= drillingVehicle.MaxHitPoints;
                return hitPoints;
            }));
            transitionFix.AddPostAction(new TransitionAction_EndAllJobs());
            if (!outSignalItemsReceived.NullOrEmpty())
            {
                Log.Message("");
            }
            if (!outSignalItemsReceived.NullOrEmpty()/* &&
                drillingVehicle.HitPoints >= drillingVehicle.MaxHitPoints*/)
            {
                transitionFix.AddPostAction(new TransitionAction_Custom((Action)delegate
                {
                    Log.Message($"[RKU] 发送信号: {outSignalItemsReceived}");
                    Find.SignalManager.SendSignal(new Signal(outSignalItemsReceived));
                }));
            }
            stateGraph.AddTransition(transitionFix);

            // 从等待返回信号状态转换到离开地图状态
            Transition transition2_5 = new Transition(waitForReturnSignal, lordToil_ExitMap);
            transition2_5.AddTrigger(new Trigger_Custom((TriggerSignal s) =>
            {
                // 首先检查是否已经等待了足够的时间
                if (!waitForReturnSignal.HasWaitedLongEnough())
                {
                    return false;
                }

                // 延迟一小段时间，确保所有pawn都有机会被分配任务
                // 然后检查是否所有pawn都已经离开地图或正在返回
                int pawnsInVehicle = 0;
                int pawnsReturning = 0;
                int totalPawns = 0;

                foreach (Pawn pawn in lord.ownedPawns)
                {
                    if (pawn != null && !pawn.Dead)
                    {
                        Log.Message($"pawnsInVehicle:{pawnsInVehicle}");
                        Log.Message($"pawnsReturning:{pawnsReturning}");
                        Log.Message($"totalPawns:{totalPawns}");
                        Log.Message($"-----");
                        totalPawns++;
                        if (pawn.Spawned)
                        {
                            // pawn 还在地图上，检查是否在执行返回任务
                            if (pawn.CurJobDef == DefOfs.RKU_EnterDrillingVehicle)
                            {
                                pawnsReturning++;
                            }
                        }
                        else
                        {
                            // pawn 已经离开地图（可能已进入钻机）
                            pawnsInVehicle++;
                        }
                    }
                }

                // 如果所有pawn都离开了地图，或者至少有一个pawn正在返回，就允许转换
                bool shouldTransition = (pawnsInVehicle + pawnsReturning) >= totalPawns;
                return shouldTransition;
            }));
            transition2_5.AddPostAction(new TransitionAction_Custom((Action)delegate
            {
                if (!outSignalStartReturnToDrillingVehicle.NullOrEmpty())
                {
                    Find.SignalManager.SendSignal(new Signal(outSignalStartReturnToDrillingVehicle));
                }
            }));
            stateGraph.AddTransition(transition2_5);

            // 紧急情况转换：成为敌人或被杀死
            Transition transition3 = new Transition(waitForItems, toil);
            transition3.AddTrigger(new Trigger_BecamePlayerEnemy());
            transition3.AddTrigger(new Trigger_PawnKilled());
            transition3.AddPostAction(new TransitionAction_EndAllJobs());
            stateGraph.AddTransition(transition3);

            // 温度危险转换
            Transition transition4 = new Transition(lordToil_TravelAndWaitForItems, lordToil_ExitMap);
            transition4.AddPreAction(new TransitionAction_Message("MessageVisitorsDangerousTemperature".Translate(faction.def.pawnsPlural.CapitalizeFirst(), faction.Name)));
            transition4.AddPostAction(new TransitionAction_EndAllJobs());
            transition4.AddTrigger(new Trigger_PawnExperiencingDangerousTemperatures());
            stateGraph.AddTransition(transition4);

            return stateGraph;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref target, "target");
            Scribe_References.Look(ref faction, "faction");
            Scribe_Values.Look(ref idleSpot, "idleSpot");
            Scribe_Values.Look(ref amount, "amount", 0);
            Scribe_Values.Look(ref outSignalItemsReceived, "outSignalItemsReceived");
            Scribe_Values.Look(ref outSignalStartReturnToDrillingVehicle, "outSignalStartReturnToDrillingVehicle");
            Scribe_Defs.Look(ref thingDef, "thingDef");
        }
    }

    // 等待物品的状态
    public class LordToil_WaitForItems : LordToil, IWaitForItemsLordToil
    {
        private Pawn target;
        private ThingDef thingDef;
        private int amount;
        private IntVec3 idleSpot;
        public bool HasAllRequestedItems { get; private set; }

        public int CountRemaining
        {
            get
            {
                if (target == null || target.inventory == null) 
                {
                    Log.Warning($"[CountRemaining] target或inventory为null，返回amount: {amount}");
                    return amount;
                }

                int currentAmount = 0;
                foreach (Thing thing in target.inventory.innerContainer)
                {
                    if (thing != null && thing.def == thingDef)
                    {
                        currentAmount += thing.stackCount;
                    }
                }
                int remaining = Math.Max(0, amount - currentAmount);
                return remaining;
            }
        }

        public LordToil_WaitForItems(Pawn target, ThingDef thingDef, int amount, IntVec3 idleSpot)
        {
            this.target = target;
            this.thingDef = thingDef;
            this.amount = amount;
            this.idleSpot = idleSpot;
        }

        public override void UpdateAllDuties()
        {
            foreach (Pawn pawn in lord.ownedPawns)
            {
                pawn.mindState.duty = new PawnDuty(DutyDefOf.TravelOrWait, idleSpot);
                pawn.mindState.duty.radius = 10f;
            }
        }

        public override void DrawPawnGUIOverlay(Pawn pawn)
        {
            if (pawn == target)
            {
                pawn.Map.overlayDrawer.DrawOverlay(pawn, OverlayTypes.QuestionMark);
            }
        }

        public override void LordToilTick()
        {
            base.LordToilTick();
            
            try
            {
                // 检查是否获得了所有请求的物品
                if (target != null && target.inventory != null)
                {
                    int currentAmount = 0;
                    foreach (Thing thing in target.inventory.innerContainer)
                    {
                        if (thing != null && thing.def == thingDef)
                        {
                            currentAmount += thing.stackCount;
                        }
                    }
                    
                    if (currentAmount >= amount && !HasAllRequestedItems)
                    {
                        HasAllRequestedItems = true;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"[LordToil] 检查物品时出错: {ex.Message}");
            }
        }

        public override IEnumerable<FloatMenuOption> ExtraFloatMenuOptions(Pawn requester, Pawn current)
        {
            
            if (target != requester)
            {
                yield break;
            }

            int remaining = CountRemaining;
            if (remaining <= 0)
            {
                yield break;
            }

            foreach (FloatMenuOption item in GiveItemsToPawnUtility.GetFloatMenuOptionsForPawn(requester, current, thingDef, remaining))
            {
                yield return item;
            }
        }
    }

    // 等待返回钻机信号的状态
            public class LordToil_WaitForReturnSignal : LordToil
        {
            private int tickCounter = 0;
            private const int DELAY_TICKS = 10; // 延迟10个tick（约0.17秒）

        public override void UpdateAllDuties()
        {
            Log.Message("进入LordToil_WaitForReturnSignal");
            tickCounter = 0;
            foreach (Pawn pawn in lord.ownedPawns)
            {
                pawn.mindState.duty = new PawnDuty(DutyDefOf.WanderClose);
            }
        }

        public override void LordToilTick()
        {
            base.LordToilTick();
            tickCounter++;
        }

        public bool HasWaitedLongEnough()
        {
            return tickCounter >= DELAY_TICKS;
        }
    }

    // 修理钻机的状态
    public class LordToil_FixDrill : LordToil
    {
        private RKU_DrillingVehicleInEnemyMap drillingVehicle;
        public LordToil_FixDrill(RKU_DrillingVehicleInEnemyMap drillingVehicle)
        {
            this.drillingVehicle = drillingVehicle;
        }
        public override void UpdateAllDuties()
        {
            Log.Message("进入LordToil_FixDrill");
            foreach (var p in lord.ownedPawns)
            {
                if (p == null) continue;
                if (drillingVehicle == null)
                {
                    Log.Message("drill为空");
                    return;
                } 
                Job job = JobMaker.MakeJob(JobDefOf.Repair, drillingVehicle);
                p.jobs.StartJob(job, JobCondition.InterruptForced, null, resumeCurJobAfterwards: false);
                return;
            }
        }
    }
} 