using RimWorld;
using RimWorld.BaseGen;
using RimWorld.Planet;
using RimWorld.QuestGen;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using Verse.Grammar;
using static RimWorld.QuestPart;

namespace RatkinUnderground
{
    public class QuestNode_RKU_GuerrillasComing : QuestNode
    {
        [NoTranslate]
        public SlateRef<string> inSignalAccept;

        [NoTranslate]
        public SlateRef<string> inSignalEnable;

        [NoTranslate]
        public SlateRef<string> outSignalCompleted;

        public SlateRef<float?> points;

        public SlateRef<int> pawnCount;

        public SlateRef<PawnKindDef> pawnKind;

        public SlateRef<ThingDef> vehicleDef;

        public SlateRef<int> delayTicks;

        public SlateRef<string> tag;

        public SlateRef<RulePack> acceptedTextRules;

        public SlateRef<RulePack> arrivedTextRules;

        protected override bool TestRunInt(Slate slate)
        {
            if (Find.AnyPlayerHomeMap == null)
            {
                return false;
            }

            // 检查是否存在游击队派系
            Faction faction = Find.FactionManager.FirstFactionOfDef(DefOfs.RKU_Faction);
            if (faction == null)
            {
                return false;
            }

            return true;
        }

        public class RepairDelay : QuestPart_Delay
        {
            public List<Pawn> pawns;
            public Thing drillingVehicle;
            public string outSignalCompleted;
                    protected override void DelayFinished()
        {
            try
            {
                Log.Error($"[RepairDelay.DelayFinished] 开始执行延迟完成逻辑");
                
                // 优先从slate中获取钻机引用
                RKU_DrillingVehicleInEnemyMap targetDrillingVehicle = null;

                Map map = Find.AnyPlayerHomeMap;
                if (map != null)
                {
                    targetDrillingVehicle = map.listerThings.ThingsOfDef(ThingDef.Named("RKU_DrillingVehicleInEnemyMap"))
                        .OfType<RKU_DrillingVehicleInEnemyMap>()
                        .FirstOrDefault();

                    if (targetDrillingVehicle == null)
                    {
                        Log.Error($"[RepairDelay.DelayFinished] 未找到钻机，创建新的钻机");
                        targetDrillingVehicle = (RKU_DrillingVehicleInEnemyMap)ThingMaker.MakeThing(ThingDef.Named("RKU_DrillingVehicleInEnemyMap"));
                        GenSpawn.Spawn(targetDrillingVehicle, map.Center, map);
                    }
                }

                // 检查 JobDef 是否存在
                if (DefOfs.RKU_AIEnterDrillingVehicle == null)
                {
                    Log.Error($"[RepairDelay.DelayFinished] RKU_AIEnterDrillingVehicle JobDef 为 null！");
                    quest.End(QuestEndOutcome.Success, 0, null, outSignalCompleted);
                    return;
                }

                // 让所有存活的游击队成员进入钻机
                if (targetDrillingVehicle != null)
                {
                    int assignedJobs = 0;
                    foreach (Pawn p in pawns.Where(p => !p.Dead && !p.Downed && p.Spawned))
                    {
                        try
                        {
                            // 检查 pawn 是否已经在执行相关任务
                            if (p.CurJob != null && 
                                (p.CurJob.def == DefOfs.RKU_AIEnterDrillingVehicle || 
                                 p.CurJob.def == DefOfs.RKU_EnterDrillingVehicle))
                            {
                                Log.Error($"[RepairDelay.DelayFinished] Pawn {p.LabelShort} 已经在执行进入钻机任务，跳过");
                                continue;
                            }

                            // 检查 pawn 是否已经在钻机中
                            if (targetDrillingVehicle.ContainsPassenger(p))
                            {
                                Log.Error($"[RepairDelay.DelayFinished] Pawn {p.LabelShort} 已经在钻机中，跳过");
                                continue;
                            }

                            Job job = new Job(DefOfs.RKU_AIEnterDrillingVehicle, targetDrillingVehicle);
                            p.jobs.StartJob(job, JobCondition.InterruptForced);
                            assignedJobs++;
                            Log.Error($"[RepairDelay.DelayFinished] 为 Pawn {p.LabelShort} 分配了进入钻机任务");
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"[RepairDelay.DelayFinished] 为 Pawn {p.LabelShort} 分配任务时出错: {ex.Message}");
                        }
                    }
                    Log.Error($"[RepairDelay.DelayFinished] 总共为 {assignedJobs} 个 pawn 分配了任务");
                }
                else
                {
                    Log.Error($"[RepairDelay.DelayFinished] 未找到钻机，无法分配任务");
                }

                // 结束任务
                quest.End(QuestEndOutcome.Success, 0, null, outSignalCompleted);
            }
            catch (Exception ex)
            {
                Log.Error($"[RepairDelay.DelayFinished] 执行延迟完成逻辑时出错: {ex.Message}");
                // 即使出错也要结束任务
                quest.End(QuestEndOutcome.Success, 0, null, outSignalCompleted);
            }
        }
        }

        public class ReminderDelay : QuestPart_Delay
        {
            public ThingDef requestedThing;
            public int requestedAmount;
            public bool isSend = false;

            protected override void DelayFinished()
            {
                if (isSend) return;
                string message = $"游击队正在等待你提供 {requestedThing?.label ?? "资源"}。请尽快帮助他们修理钻机。";
                Find.LetterStack.ReceiveLetter("游击队等待支援", message, LetterDefOf.NeutralEvent);
                isSend = true;
            }

            public override void ExposeData()
            {
                base.ExposeData();
                Scribe_Defs.Look(ref requestedThing, "requestedThing");
                Scribe_Values.Look(ref requestedAmount, "requestedAmount");
                Scribe_Values.Look(ref isSend, "isSend", false);
            }
        }

        public class FailureDelay : QuestPart_Delay
        {
            public List<Pawn> pawns;
            protected override void DelayFinished()
            {
                quest.Leave(pawns.Where(p => !p.Dead).ToList());
                quest.End(QuestEndOutcome.Fail);
            }
        }

        public class DelayCheckGuerrillas : QuestPart_Delay
        {
            public string outSignalCompleted;
            protected override void DelayFinished()
            {
                if (!string.IsNullOrEmpty(outSignalCompleted))
                {
                    Find.SignalManager.SendSignal(new Signal(outSignalCompleted));
                }
            }

            public override void ExposeData()
            {
                base.ExposeData();
                Scribe_Values.Look(ref outSignalCompleted, "outSignalCompleted");
            }
        }

        public class DelayCheckPawns : QuestPart_Delay
        {
            public string outSignalCompleted;
            protected override void DelayFinished()
            {
                if (!string.IsNullOrEmpty(outSignalCompleted))
                {
                    Find.SignalManager.SendSignal(new Signal(outSignalCompleted));
                }
            }

            public override void ExposeData()
            {
                base.ExposeData();
                Scribe_Values.Look(ref outSignalCompleted, "outSignalCompleted");
            }
        }

        public class SpawnGuerrillas : QuestPart
        {
            public string inSignal;
            public List<Pawn> pawns;
            public Pawn captain;
            public MapParent mapParent;
            public string outSignalArrived;
            public string deliveredSignal;
            public Faction faction;
            public IntVec3 spawnPosition; // 存储钻机生成位置
            public QuestPart_CheckGuerrillas checkPart; // 引用检查游击队组件
            public QuestPart_RequestResources requestPart; // 引用资源请求组件
            public QuestPart_CheckPawnsEntered checkPawnsEntered; // 引用检查Pawn进入钻机组件
            public QuestPart_DrillingVehicleDeparture drillingVehicleDeparture; // 引用钻机离开组件
            public RKU_DrillingVehicleInEnemyMap drillingVehicle; // 新增：钻机引用

            public override void Notify_QuestSignalReceived(Signal signal)
            {
                if (signal.tag != inSignal)
                {
                    return;
                }
                Map map = mapParent.Map;
                if (map == null) return;
                IntVec3 spawnPos = new IntVec3();
                DropCellFinder.FindSafeLandingSpot(out spawnPos, Faction.OfPlayer, map, 35, 15, 8, IntVec2.One, null);
                if (!spawnPos.IsValid) spawnPos = map.Center;

                // 保存生成位置
                spawnPosition = spawnPos;

                // 设置检查游击队组件的生成位置
                if (checkPart != null)
                {
                    checkPart.spawnPosition = spawnPos;
                }

                // 查找与游击队同派系的钻机
                drillingVehicle = map.listerThings.ThingsOfDef(ThingDef.Named("RKU_DrillingVehicleInEnemyMap"))
                    .OfType<RKU_DrillingVehicleInEnemyMap>()
                    .FirstOrDefault(d => d.Faction == faction);

                if (drillingVehicle != null)
                {
                    // 在slate中存储钻机引用
                    QuestGen.slate.Set("drillingVehicle", drillingVehicle);

                    // 设置资源请求组件的钻机引用
                    if (requestPart != null)
                    {
                        requestPart.drillingVehicle = drillingVehicle;
                    }

                    // 设置检查Pawn进入钻机组件的钻机引用
                    if (checkPawnsEntered != null)
                    {
                        checkPawnsEntered.drillingVehicle = drillingVehicle;
                    }

                    // 设置钻机离开组件的钻机引用
                    if (drillingVehicleDeparture != null)
                    {
                        drillingVehicleDeparture.drillingVehicle = drillingVehicle;
                    }
                }

                // 设置资源请求组件的生成位置
                if (requestPart != null)
                {
                    requestPart.spawnPosition = spawnPos;
                }

                RKU_TunnelHiveSpawner spawner = (RKU_TunnelHiveSpawner)ThingMaker.MakeThing(DefOfs.RKU_TunnelHiveSpawner);

                if (spawner != null)
                {
                    spawner.faction = faction;
                    spawner.canMove = false;
                    spawner.hitPoints = 100;
                }

                foreach (Pawn p in pawns)
                {
                    spawner.GetDirectlyHeldThings().TryAddOrTransfer(p);
                }

                GenSpawn.Spawn(spawner, spawnPos, map);
                Find.SignalManager.SendSignal(new Signal(outSignalArrived));
            }

            public override void ExposeData()
            {
                base.ExposeData();
                Scribe_Values.Look(ref inSignal, "inSignal");
                Scribe_Collections.Look(ref pawns, "pawns", LookMode.Reference);
                Scribe_References.Look(ref captain, "captain");
                Scribe_References.Look(ref mapParent, "mapParent");
                Scribe_Values.Look(ref outSignalArrived, "outSignalArrived");
                Scribe_Values.Look(ref deliveredSignal, "deliveredSignal");
                Scribe_References.Look(ref faction, "faction");
                Scribe_Values.Look(ref spawnPosition, "spawnPosition");
                Scribe_References.Look(ref checkPart, "checkPart");
                Scribe_References.Look(ref requestPart, "requestPart");
                Scribe_References.Look(ref checkPawnsEntered, "checkPawnsEntered");
                Scribe_References.Look(ref drillingVehicleDeparture, "drillingVehicleDeparture");
                Scribe_References.Look(ref drillingVehicle, "drillingVehicle");
            }
        }

        protected override void RunInt()
        {
            Slate slate = QuestGen.slate;
            Quest quest = QuestGen.quest;
            Map targetMap = slate.Get<Map>("map") ?? Find.AnyPlayerHomeMap;
            if (targetMap == null)
            {
                return;
            }
            float threatPoints = points.GetValue(slate) ?? StorytellerUtility.DefaultThreatPointsNow(targetMap);
            int numPawns = pawnCount.GetValue(slate);
            PawnKindDef kind = pawnKind.GetValue(slate);
            int delay = delayTicks.GetValue(slate);
            Faction faction = Find.FactionManager.FirstFactionOfDef(DefOfs.RKU_Faction);
            List<Pawn> pawns = new List<Pawn>();
            //队长
            PawnGenerationRequest requestOfficer = new PawnGenerationRequest(PawnKindDef.Named("RKU_Commissar"), faction, PawnGenerationContext.NonPlayer, -1, forceGenerateNewPawn: true);
            Pawn pawnOfficer = PawnGenerator.GeneratePawn(requestOfficer);
            slate.Set("captain", pawnOfficer);
            Thing researchReward = ThingMaker.MakeThing(ThingDef.Named("Techprint_RKU_UndergroundGuerrillaEquipments"));
            pawnOfficer.inventory?.innerContainer.TryAdd(researchReward);
            pawns.Add(pawnOfficer);
            //侦察兵
            for (int i = 0; i < numPawns; i++)
            {
                PawnGenerationRequest request = new PawnGenerationRequest(kind, faction, PawnGenerationContext.NonPlayer, -1, forceGenerateNewPawn: true);
                Pawn pawn = PawnGenerator.GeneratePawn(request);
                pawns.Add(pawn);
            }

            // 添加受伤检测组件
            foreach (var p in pawns)
            {
                Comp_RecordDamage comp = (Comp_RecordDamage)Activator.CreateInstance(typeof(Comp_RecordDamage));
                comp.parent = p;
                p.AllComps.Add(comp);
            }

            string arrivedSignal = QuestGen.GenerateNewSignal("GuerrillasArrived");
            string deliveredSignal = QuestGen.GenerateNewSignal("ResourcesDelivered");
            string acceptSignal = inSignalAccept.GetValue(slate) ?? QuestGenUtility.HardcodedSignalWithQuestID("Initiate");

            // 生成请求的资源
            ThingDef requestedThing = ThingDefOf.ComponentIndustrial; // 零部件
            int requestedAmount = Rand.Range(5, 15);

            SpawnGuerrillas arrivePart = new SpawnGuerrillas();
            arrivePart.inSignal = acceptSignal;
            arrivePart.pawns = pawns;
            arrivePart.captain = pawnOfficer;
            arrivePart.mapParent = targetMap.Parent;
            arrivePart.outSignalArrived = arrivedSignal;
            arrivePart.deliveredSignal = deliveredSignal;
            arrivePart.faction = faction;
            quest.AddPart(arrivePart);

            // 先生成检查游击队生成的QuestPart
            QuestPart_CheckGuerrillas checkPart = new QuestPart_CheckGuerrillas();
            string delayCompletedSignal = QuestGen.GenerateNewSignal("DelayCompleted");
            checkPart.inSignal = delayCompletedSignal;
            checkPart.targetMap = targetMap;
            checkPart.faction = faction;
            quest.AddPart(checkPart);

            arrivePart.checkPart = checkPart;

            // 再生成延迟检查游击队生成的QuestPart
            DelayCheckGuerrillas delayCheck = new DelayCheckGuerrillas();
            delayCheck.delayTicks = 1800; // 1800tick延迟
            delayCheck.inSignalEnable = arrivedSignal;
            delayCheck.signalListenMode = SignalListenMode.OngoingOnly;
            delayCheck.outSignalCompleted = delayCompletedSignal;
            quest.AddPart(delayCheck);

            // 资源请求
            QuestPart_RequestResources requestPart = new QuestPart_RequestResources();
            requestPart.inSignal = delayCompletedSignal; // 改为监听延迟完成信号，确保游击队已生成
            requestPart.outSignalItemsReceived = deliveredSignal;
            requestPart.outSignalStartReturnToDrillingVehicle = QuestGen.GenerateNewSignal("StartReturnToDrillingVehicle");
            requestPart.pawns.AddRange(pawns); // 添加所有Pawn，不只是队长
            requestPart.target = pawnOfficer;
            requestPart.faction = faction;
            requestPart.mapParent = targetMap.Parent;
            requestPart.thingDef = requestedThing;
            requestPart.amount = requestedAmount;
            // 设置钻机位置引用，将在SpawnGuerrillas中设置
            arrivePart.requestPart = requestPart;
            quest.AddPart(requestPart);

            // 检查所有Pawn是否进入钻机
            QuestPart_CheckPawnsEntered checkPawnsEntered = new QuestPart_CheckPawnsEntered();
            checkPawnsEntered.inSignal = requestPart.outSignalStartReturnToDrillingVehicle;
            checkPawnsEntered.checkSignal = QuestGen.GenerateNewSignal("CheckPawnsEntered");
            checkPawnsEntered.outSignalAllPawnsEntered = QuestGen.GenerateNewSignal("AllPawnsEntered");
            checkPawnsEntered.outSignalDrillingVehicleDeparture = QuestGen.GenerateNewSignal("DrillingVehicleDeparture");
            checkPawnsEntered.pawns = pawns;
            // 钻机引用将在SpawnGuerrillas中设置
            arrivePart.checkPawnsEntered = checkPawnsEntered;
            quest.AddPart(checkPawnsEntered);

            // 延迟检查QuestPart
            DelayCheckPawns delayCheckPawns = new DelayCheckPawns();
            delayCheckPawns.delayTicks = 60;
            delayCheckPawns.inSignalEnable = checkPawnsEntered.inSignal; // 监听开始返回钻机信号
            delayCheckPawns.outSignalCompleted = checkPawnsEntered.checkSignal; // 延迟后发送检查信号
            quest.AddPart(delayCheckPawns);

            // 生成任务成功信号
            string winSignal = QuestGen.GenerateNewSignal("RKU_IntroQuestWin");

            // 钻机离开
            QuestPart_DrillingVehicleDeparture drillingVehicleDeparture = new QuestPart_DrillingVehicleDeparture();
            drillingVehicleDeparture.inSignal = checkPawnsEntered.outSignalDrillingVehicleDeparture;
            drillingVehicleDeparture.winSignal = winSignal; // 设置任务成功信号
            // 钻机引用将在SpawnGuerrillas中设置
            arrivePart.drillingVehicleDeparture = drillingVehicleDeparture;
            quest.AddPart(drillingVehicleDeparture);

            RepairDelay delayPart = new RepairDelay();
            delayPart.delayTicks = 6000;
            delayPart.inSignalEnable = deliveredSignal;
            delayPart.signalListenMode = SignalListenMode.OngoingOnly;
            delayPart.pawns = pawns;
            delayPart.outSignalCompleted = outSignalCompleted.GetValue(slate);
            quest.AddPart(delayPart);
            string arrestedSignal = QuestGenUtility.HardcodedSignalWithQuestID("pawns.Arrested");
            string killedSignal = QuestGenUtility.HardcodedSignalWithQuestID("pawns.Killed");
            quest.AnySignal(new[] { arrestedSignal, killedSignal }, () =>
            {
                quest.End(QuestEndOutcome.Fail, 0, null);
            });

            ReminderDelay reminderDelay = new ReminderDelay();
            reminderDelay.delayTicks = 12000;
            reminderDelay.inSignalEnable = arrivedSignal;
            reminderDelay.inSignalDisable = deliveredSignal;
            reminderDelay.requestedThing = requestedThing;
            reminderDelay.requestedAmount = requestedAmount;
            quest.AddPart(reminderDelay);
            FailureDelay failureDelay = new FailureDelay();
            failureDelay.delayTicks = 60000; // 10小时超时
            failureDelay.inSignalEnable = arrivedSignal;
            failureDelay.inSignalDisable = deliveredSignal;
            failureDelay.pawns = pawns;
            quest.AddPart(failureDelay);
            QuestPart_TrackPawnsStatus trackPart = new QuestPart_TrackPawnsStatus();
            trackPart.pawns = pawns;
            trackPart.failIfAllDead = true;
            trackPart.failSignal = QuestGen.GenerateNewSignal("AllGuerrillasDead");
            quest.AddPart(trackPart);
            quest.Signal(trackPart.failSignal, () => quest.End(QuestEndOutcome.Fail, 0, null));
            
            // 监听任务成功信号并结束任务
            quest.Signal(winSignal, () => quest.End(QuestEndOutcome.Success, 0, null, playSound: true));

            LongEventHandler.ExecuteWhenFinished(() =>
            {
                Find.SignalManager.SendSignal(new Signal(acceptSignal));
            });
        }
    }
}