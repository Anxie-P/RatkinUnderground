using RimWorld;
using RimWorld.QuestGen;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace RatkinUnderground
{
    public class QuestPart_DrillingVehicleDeparture : QuestPart
    {
        public RKU_DrillingVehicleInEnemyMap drillingVehicle;
        public string inSignal; // 钻机离开信号
        public string winSignal; // 任务成功信号
        public Faction faction; // 添加派系引用

        public override void Notify_QuestSignalReceived(Signal signal)
        {
            base.Notify_QuestSignalReceived(signal);

            if (signal.tag == inSignal)
            {
                StartDrillingVehicleDeparture();
            }
        }

        private void StartDrillingVehicleDeparture()
        {
            RKU_DrillingVehicleInEnemyMap targetDrillingVehicle = drillingVehicle;
            
            // 如果直接引用为null，尝试从QuestGen.slate获取钻机引用
            if (targetDrillingVehicle == null)
            {
                targetDrillingVehicle = QuestGen.slate.Get<RKU_DrillingVehicleInEnemyMap>("drillingVehicle");
            }
            
            // 如果slate中也没有，尝试通过派系查找钻机
            if (targetDrillingVehicle == null && faction != null)
            {
                Map map = Find.AnyPlayerHomeMap;
                if (map != null)
                {
                    var allDrillingVehicles = map.listerThings.ThingsOfDef(ThingDef.Named("RKU_DrillingVehicleInEnemyMap"));

                    targetDrillingVehicle = allDrillingVehicles
                        .OfType<RKU_DrillingVehicleInEnemyMap>()
                        .FirstOrDefault(d => d.Faction == faction);
                }
            }

            if (targetDrillingVehicle != null && targetDrillingVehicle.Spawned)
            {
                var passengers = targetDrillingVehicle.GetDirectlyHeldThings();
                
                // 在钻地机位置留下持续的灰尘效果
                for (int i = 0; i < 15; i++)
                {
                    IntVec3 offset = new IntVec3(Rand.Range(-1, 2), 0, Rand.Range(-1, 2));
                    FleckMaker.ThrowDustPuffThick((targetDrillingVehicle.Position + offset).ToVector3(), targetDrillingVehicle.Map, 2f, Color.gray);
                }
                
                if (targetDrillingVehicle.Map != null && targetDrillingVehicle.Position.IsValid)
                {
                    RKU_DigDust dust = (RKU_DigDust)ThingMaker.MakeThing(ThingDef.Named("RKU_DigDust"));
                    if (dust != null)
                    {
                        Vector3 centerPos = targetDrillingVehicle.Position.ToVector3() + new Vector3(0.5f, 0, 0.5f);
                        dust.exactPosition = centerPos;
                        GenSpawn.Spawn(dust, targetDrillingVehicle.Position, targetDrillingVehicle.Map);
                    }
                }
                
                // 在钻机消失位置生成电台
                Thing radio = ThingMaker.MakeThing(DefOfs.RKU_Radio);
                if (radio != null && targetDrillingVehicle.Position.IsValid)
                {
                    GenSpawn.Spawn(radio, targetDrillingVehicle.Position, targetDrillingVehicle.Map);
                }
                
                targetDrillingVehicle.DeSpawn();

                // 发送信件
                string title = "RKU_RadioLeftTitle".Translate();
                string message = "RKU_RadioLeftMessage".Translate();
                
                
                ChoiceLetter letter = LetterMaker.MakeLetter(
                    title,
                    message,
                    LetterDefOf.PositiveEvent
                );
                Find.LetterStack.ReceiveLetter(letter);
                // 发送任务成功信号
                if (!string.IsNullOrEmpty(winSignal))
                {
                    Find.SignalManager.SendSignal(new Signal(winSignal));
                }
            }
            else
            {
                Log.Error("[QuestPart_DrillingVehicleDeparture] 无法找到有效的钻机车辆");
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref drillingVehicle, "drillingVehicle");
            Scribe_Values.Look(ref inSignal, "inSignal");
            Scribe_Values.Look(ref winSignal, "winSignal");
            Scribe_References.Look(ref faction, "faction");
        }
    }
}