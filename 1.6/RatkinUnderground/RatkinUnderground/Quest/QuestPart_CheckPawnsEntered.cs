using RimWorld;
using RimWorld.QuestGen;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RatkinUnderground
{
    public class QuestPart_CheckPawnsEntered : QuestPart
    {
        public List<Pawn> pawns;
        public RKU_DrillingVehicleInEnemyMap drillingVehicle;
        public string inSignal; // 开始返回钻机信号
        public string checkSignal; // 检查信号
        public string outSignalAllPawnsEntered; // 所有Pawn进入钻机信号
        public string outSignalDrillingVehicleDeparture; // 钻机离开信号
        private bool isChecking = false; // 是否正在检查

        public override void Notify_QuestSignalReceived(Signal signal)
        {
            base.Notify_QuestSignalReceived(signal);
                        
            if (signal.tag == inSignal)
            {
                CheckAllPawnsEntered();
            }
            else if (signal.tag == checkSignal)
            {
                CheckAllPawnsEntered();
            }
        }

        private void CheckAllPawnsEntered()
        {
            // 防止无限循环检查
            if (isChecking)
            {
                return;
            }
            
            isChecking = true;
            
            RKU_DrillingVehicleInEnemyMap targetDrillingVehicle = drillingVehicle;
            
            // 尝试在地图上查找同派系钻机
            if (targetDrillingVehicle == null && pawns != null && pawns.Count > 0)
            {
                Map map = pawns[0].Map;
                if (map != null)
                {
                    var allDrillingVehicles = map.listerThings.ThingsOfDef(ThingDef.Named("RKU_DrillingVehicleInEnemyMap"));
              
                    // 尝试从第一个pawn的派系获取钻机
                    if (pawns[0] != null && pawns[0].Faction != null)
                    {
                        targetDrillingVehicle = allDrillingVehicles
                            .OfType<RKU_DrillingVehicleInEnemyMap>()
                            .FirstOrDefault(d => d.Faction == pawns[0].Faction);
                                                
                        // 如果找到钻机，更新本地引用和slate
                        if (targetDrillingVehicle != null)
                        {
                            drillingVehicle = targetDrillingVehicle;
                            QuestGen.slate.Set("drillingVehicle", targetDrillingVehicle);
                        }
                    }
                }
            }
            
            if (targetDrillingVehicle == null || !targetDrillingVehicle.Spawned) 
            {
                isChecking = false;
                return;
            }

            // 检查所有pawn是否都已进入钻机
            int enteredCount = 0;
            int totalCount = pawns.Count;
                        
            foreach (Pawn pawn in pawns)
            {
                bool isInVehicle = targetDrillingVehicle.ContainsPassenger(pawn);
                if (isInVehicle)
                {
                    enteredCount++;
                }
            }


            if (enteredCount >= totalCount)
            {
                // 所有pawn都已进入钻机，发送信号
                isChecking = false;
                if (!outSignalAllPawnsEntered.NullOrEmpty())
                {
                    Find.SignalManager.SendSignal(new Signal(outSignalAllPawnsEntered));
                }
                
                // 发送钻机离开信号
                if (!outSignalDrillingVehicleDeparture.NullOrEmpty())
                {
                    Find.SignalManager.SendSignal(new Signal(outSignalDrillingVehicleDeparture));
                }
            }
            else
            {
                // 延迟一段时间后重新检查
                isChecking = false;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref pawns, "pawns", LookMode.Reference);
            Scribe_References.Look(ref drillingVehicle, "drillingVehicle");
            Scribe_Values.Look(ref inSignal, "inSignal");
            Scribe_Values.Look(ref checkSignal, "checkSignal");
            Scribe_Values.Look(ref outSignalAllPawnsEntered, "outSignalAllPawnsEntered");
            Scribe_Values.Look(ref outSignalDrillingVehicleDeparture, "outSignalDrillingVehicleDeparture");
            Scribe_Values.Look(ref isChecking, "isChecking", false);
        }
    }
} 