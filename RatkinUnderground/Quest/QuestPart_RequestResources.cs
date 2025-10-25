using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using System.Linq;

namespace RatkinUnderground
{
    public class QuestPart_RequestResources : QuestPart_MakeLord
    {
        public Pawn target;
        public ThingDef thingDef;
        public int amount;
        public string outSignalItemsReceived;
        public string outSignalStartReturnToDrillingVehicle;
        public IntVec3 spawnPosition; // 钻机生成位置
        public RKU_DrillingVehicleInEnemyMap drillingVehicle; 
        private bool lordCreated = false; // 添加标志防止重复创建

        protected override Lord MakeLord()
        {
            // 如果已经创建过 Lord，返回 null
            if (lordCreated)
            {
                return null;
            }

            IntVec3 result;
            
            // 优先使用钻机位置，如果无效则使用备选方案
            if (spawnPosition.IsValid)
            {
                result = spawnPosition;
            }
            else if (pawns != null && pawns.Count > 0 && pawns[0] != null && pawns[0].Spawned)
            {
                // 尝试找到殖民地外的随机位置
                if (!RCellFinder.TryFindRandomSpotJustOutsideColony(pawns[0], out result))
                {
                    result = CellFinder.RandomCell(pawns[0].Map);
                }
            }
            else
            {
                // 如果没有pawns或游击队未生成，使用目标地图的中心位置
                Map map = base.Map;
                if (map != null)
                {
                    result = map.Center;
                }
                else
                {
                    result = IntVec3.Invalid;
                }
            }

            // 创建LordJob_WaitForItemsAndReturn
            var drill = Map.listerBuildings.allBuildingsNonColonist.Find(b => b is RKU_DrillingVehicleInEnemyMap) as RKU_DrillingVehicleInEnemyMap;
            var newLord = LordMaker.MakeNewLord(faction, new LordJob_WaitForItemsAndReturn(faction, result, target, thingDef, drill, amount, outSignalItemsReceived, outSignalStartReturnToDrillingVehicle), base.Map);
            
            lordCreated = true; // 标记已创建
            return newLord;
        }

        public override void Notify_QuestSignalReceived(Signal signal)
        {
            // 只有在第一次收到信号时才调用基类方法创建 Lord
            if (!lordCreated)
            {
                base.Notify_QuestSignalReceived(signal);
            }
            
            // 当收到物品交付信号时，开始返回钻机流程
            if (signal.tag == outSignalItemsReceived)
            {
                RKU_DrillingVehicleInEnemyMap targetDrillingVehicle = drillingVehicle;
                if (targetDrillingVehicle == null && pawns != null && pawns.Count > 0)
                {
                    Map map = pawns[0].Map;
                    if (map != null)
                    {
                        var allDrillingVehicles = map.listerThings.ThingsOfDef(ThingDef.Named("RKU_DrillingVehicleInEnemyMap"));
                       
                        targetDrillingVehicle = allDrillingVehicles
                            .OfType<RKU_DrillingVehicleInEnemyMap>()
                            .FirstOrDefault(d => d.Faction == faction) as RKU_DrillingVehicleInEnemyMap;
                                                
                        if (targetDrillingVehicle != null)
                        {
                            drillingVehicle = targetDrillingVehicle;
                        }
                    }
                }
                
                if (targetDrillingVehicle != null)
                {
                    drillingVehicle = targetDrillingVehicle; // 更新引用
                    StartReturnToDrillingVehicle();
                }
                else
                {
                    foreach (Pawn pawn in pawns)
                    {
                        if (pawn != null && pawn.Spawned)
                        { 
                            pawn.jobs.StopAll();
                        }
                    }
                }
            }
        }

        private void StartReturnToDrillingVehicle()
        {
            if (drillingVehicle == null || !drillingVehicle.Spawned)
            {
                return;
            }
            foreach (Pawn pawn in pawns)
            {
                if (pawn != null && pawn.Spawned && !drillingVehicle.ContainsPassenger(pawn) && pawn.CurJobDef != DefOfs.RKU_EnterDrillingVehicle)
                {
                    Job job = JobMaker.MakeJob(DefOfs.RKU_EnterDrillingVehicle, drillingVehicle);
                    pawn.jobs.StopAll();
                    pawn.jobs.StartJob(job);
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref target, "target");
            Scribe_Defs.Look(ref thingDef, "thingDef");
            Scribe_Values.Look(ref amount, "amount", 0);
            Scribe_Values.Look(ref outSignalItemsReceived, "outSignalItemsReceived");
            Scribe_Values.Look(ref outSignalStartReturnToDrillingVehicle, "outSignalStartReturnToDrillingVehicle");
            Scribe_Values.Look(ref spawnPosition, "spawnPosition");
            Scribe_References.Look(ref drillingVehicle, "drillingVehicle");
            Scribe_Values.Look(ref lordCreated, "lordCreated", false);
        }
    }
} 