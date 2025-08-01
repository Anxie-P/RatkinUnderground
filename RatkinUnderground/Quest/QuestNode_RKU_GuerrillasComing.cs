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

        public class RepairDelay : QuestPart_Delay {
            public List<Pawn> pawns;
            public Thing drillingVehicle;
            public string outSignalCompleted;
            protected override void DelayFinished() {
                if (drillingVehicle == null) {
                    Map map = Find.AnyPlayerHomeMap;
                    if (map != null) {
                        drillingVehicle = map.listerThings.ThingsOfDef(ThingDef.Named("RKU_DrillingVehicleInEnemyMap")).FirstOrDefault() as Thing;
                        if (drillingVehicle == null) {
                            drillingVehicle = ThingMaker.MakeThing(ThingDef.Named("RKU_DrillingVehicleInEnemyMap"));
                            GenSpawn.Spawn(drillingVehicle, map.Center, map);
                        }
                    }
                }
                foreach (Pawn p in pawns.Where(p => !p.Dead && !p.Downed)) {
                    if (drillingVehicle is RKU_DrillingVehicleInEnemyMap drill) {
                        Job job = new Job(DefOfs.RKU_AIEnterDrillingVehicle, drill);
                        p.jobs.StartJob(job, JobCondition.InterruptForced);
                    }
                }
                quest.End(QuestEndOutcome.Success, 0, null, outSignalCompleted);
            }
        }

        public class ReminderDelay : QuestPart_Delay {
            protected override void DelayFinished() {
                Find.LetterStack.ReceiveLetter("Reminder", "Remember to deliver resources to the guerrilla captain.", LetterDefOf.NeutralEvent);
            }
        }

        public class FailureDelay : QuestPart_Delay {
            public List<Pawn> pawns;
            protected override void DelayFinished() {
                quest.Leave(pawns.Where(p => !p.Dead).ToList());
                quest.End(QuestEndOutcome.Fail);
            }
        }

        public class SpawnGuerrillas : QuestPart {
            public string inSignal;
            public List<Pawn> pawns;
            public Pawn captain;
            public MapParent mapParent;
            public string outSignalArrived;
            public string deliveredSignal;
            public Faction faction;

            public override void Notify_QuestSignalReceived(Signal signal) {
                if (signal.tag != inSignal) {
                    return;
                }
                Map map = mapParent.Map;
                if (map == null) return;
                IntVec3 spawnPos=new IntVec3();
                DropCellFinder.FindSafeLandingSpot(out spawnPos, Faction.OfPlayer, map, 35, 15, 8, IntVec2.One, null);
                if (!spawnPos.IsValid) spawnPos = map.Center;
                
                RKU_TunnelHiveSpawner spawner = (RKU_TunnelHiveSpawner)ThingMaker.MakeThing(DefOfs.RKU_TunnelHiveSpawner);
                
                if (spawner != null) {
                    spawner.faction=faction;
                    spawner.canMove = false;
                    spawner.hitPoints = 100;
                }
                
                foreach (Pawn p in pawns) {
                    spawner.GetDirectlyHeldThings().TryAddOrTransfer(p);
                }
                
                GenSpawn.Spawn(spawner, spawnPos, map);
                
            }

            public override void ExposeData() {
                base.ExposeData();
                Scribe_Values.Look(ref inSignal, "inSignal");
                Scribe_Collections.Look(ref pawns, "pawns", LookMode.Deep);
                Scribe_References.Look(ref captain, "captain");
                Scribe_References.Look(ref mapParent, "mapParent");
                Scribe_Values.Look(ref outSignalArrived, "outSignalArrived");
                Scribe_Values.Look(ref deliveredSignal, "deliveredSignal");
                Scribe_References.Look(ref faction, "faction");
            }
        }

        protected override void RunInt()
        {
            Slate slate = QuestGen.slate;
            Quest quest = QuestGen.quest;
            Map targetMap = slate.Get<Map>("map") ?? Find.AnyPlayerHomeMap;
            if (targetMap == null) {
                return;
            }
            float threatPoints = points.GetValue(slate) ?? StorytellerUtility.DefaultThreatPointsNow(targetMap);
            int numPawns = pawnCount.GetValue(slate);
            PawnKindDef kind = pawnKind.GetValue(slate);
            int delay = delayTicks.GetValue(slate);
            Faction faction = Find.FactionManager.FirstFactionOfDef(DefOfs.RKU_Faction);
            List<Pawn> pawns = new List<Pawn>();
            //队长
            PawnGenerationRequest requestOfficer = new PawnGenerationRequest(PawnKindDef.Named("RKU_Officer"), faction, PawnGenerationContext.NonPlayer, -1, forceGenerateNewPawn: true);
            Pawn pawnOfficer = PawnGenerator.GeneratePawn(requestOfficer);
            pawns.Add(pawnOfficer);
            //侦察兵
            for (int i = 0; i < numPawns; i++) {
                PawnGenerationRequest request = new PawnGenerationRequest(kind, faction, PawnGenerationContext.NonPlayer, -1, forceGenerateNewPawn: true);
                Pawn pawn = PawnGenerator.GeneratePawn(request);
                pawns.Add(pawn);
            }
            Pawn captain = pawns.RandomElement();
            slate.Set("captain", captain);
            string arrivedSignal = QuestGen.GenerateNewSignal("GuerrillasArrived");
            string deliveredSignal = QuestGen.GenerateNewSignal("ResourcesDelivered");
            string acceptSignal = inSignalAccept.GetValue(slate) ?? QuestGenUtility.HardcodedSignalWithQuestID("Initiate");
            SpawnGuerrillas arrivePart = new SpawnGuerrillas();
            arrivePart.inSignal = acceptSignal;
            arrivePart.pawns = pawns;
            arrivePart.captain = captain;
            arrivePart.mapParent = targetMap.Parent;
            arrivePart.outSignalArrived = arrivedSignal;
            arrivePart.deliveredSignal = deliveredSignal;
            arrivePart.faction = faction;
            quest.AddPart(arrivePart);
            RepairDelay delayPart = new RepairDelay();
            delayPart.delayTicks = delay;
            delayPart.inSignalEnable = deliveredSignal;
            delayPart.signalListenMode = SignalListenMode.OngoingOnly;
            delayPart.pawns = pawns;
            delayPart.outSignalCompleted = outSignalCompleted.GetValue(slate);
            quest.AddPart(delayPart);
            string arrestedSignal = QuestGenUtility.HardcodedSignalWithQuestID("pawns.Arrested");
            string killedSignal = QuestGenUtility.HardcodedSignalWithQuestID("pawns.Killed");
            quest.AnySignal(new[] { arrestedSignal, killedSignal }, () => {
                quest.End(QuestEndOutcome.Fail, 0, null);
            });
            ReminderDelay reminderDelay = new ReminderDelay();
            reminderDelay.delayTicks = 12000;
            reminderDelay.inSignalEnable = arrivedSignal;
            reminderDelay.inSignalDisable = deliveredSignal;
            quest.AddPart(reminderDelay);
            FailureDelay failureDelay = new FailureDelay();
            failureDelay.delayTicks = delayTicks.GetValue(slate);
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
            List<Thing> rewards = new List<Thing> { ThingMaker.MakeThing(DefOfs.RKU_Radio) };
            QuestPart_GiveRewards rewardPart = new QuestPart_GiveRewards();
            rewardPart.rewards = rewards;
            rewardPart.inSignal = outSignalCompleted.GetValue(slate);
            quest.AddPart(rewardPart);
                    
            LongEventHandler.ExecuteWhenFinished(() => {
                Find.SignalManager.SendSignal(new Signal(acceptSignal));
            });
        }
    }
}