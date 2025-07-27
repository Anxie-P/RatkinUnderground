using RimWorld;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

namespace RatkinUnderground
{
    // 
    public class RKU_GuerrillasLeave : MapComponent
    {
        int tick = 0;
        bool gotoEdge = false;           // 是否到时间离开地图
        bool drillDetect = false;       // 是否检测过钻机
        bool drillLeave = true;         // 钻机是否具备离开条件
        bool startLeave = false;        // 地鼠触发离开地图逻辑
        List<Pawn> guerrillas = new();
        RKU_DrillingVehicleInEnemyMap drill = null;

        public void SetStartLeave(bool startLeave)
        {
            this.startLeave = startLeave;
        }
        public RKU_GuerrillasLeave(Map map) : base(map)
        {
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();

            // 60tick 进行一次逻辑的检测和执行
            tick++;
            if (tick < 60) return;
            tick = 0;

            TryDrillOnMap();
            if(!drillDetect) return;    // 地图上未检测过钻机，不执行下面逻辑
            
            // 检测到地鼠的钻机后获取所有地鼠
            if (!(guerrillas.Count > 0))
            {
                foreach(var p in map.mapPawns.AllHumanlike)
                {
                    if (p.Faction.def != DefOfs.RKU_Faction) continue;
                    guerrillas.Add(p);
                }
            }

            CanLeave();
            LeaveMapWithNoDrill();
            RestWithNoDrill();
            if (!drillLeave) return;
            if (drill?.GetDirectlyHeldThings().Count > 0)
            {
                Messages.Message("地鼠们已经通过钻机离开了地图", MessageTypeDefOf.NeutralEvent);
                drill.DeSpawn();
                Reset();
            }
            else
            {
                Reset();
            }
        }

        // 检测地图上地鼠的钻机
        void TryDrillOnMap()
        {
            if(drillDetect) return;
            var allBuildings = map.listerBuildings.allBuildingsNonColonist;
            foreach (var building in allBuildings)
            {
                if (building.def.defName != "RKU_DrillingVehicleInEnemyMap") continue;
                var drill = ((RKU_DrillingVehicleInEnemyMap)building);
                this.drill = drill;
                drillDetect = true;
                if (drill != null)
                {
                    drillDetect = true;
                    break;
                }
            }
        }

        // 是否具备离开条件
        void CanLeave()
        {
            if (guerrillas == null || guerrillas.Count == 0)
            {
                drillLeave = false;
                return;
            }
            if (drill == null)
            {
                drillLeave = false;
                return;
            }

            foreach (var p in guerrillas.ToList())
            {
                if (p == null) continue;

                // p.CurJob is shorthand for p.jobs.curJob
                var job = p.CurJob;
                if (job != null && job.exitMapOnArrival)
                {
                    startLeave = true;
                    gotoEdge = true;
                    break;
                }
            }

            // 多次给与job会爆红字
            if (gotoEdge)
            {
                foreach (var p in guerrillas.ToList())
                {
                    if (p == null || p.Dead || p.Downed || drill.ContainsPassenger(p))
                        continue;

                    var newJob = new Job(DefOfs.RKU_AIEnterDrillingVehicle, drill)
                    {
                        exitMapOnArrival = true
                    };
                    p.jobs?.StartJob(newJob, JobCondition.InterruptForced);
                }
            }
            gotoEdge = false;

            // 检测是否有地鼠未上钻机
            bool leftPawn = guerrillas
                .Where(p => p != null)
                .Any(p => !(p.Dead || p.Downed) && !drill.ContainsPassenger(p));

            drillLeave = !leftPawn;
        }

        // 没有钻机离开地图逻辑
        void LeaveMapWithNoDrill()
        {
            Log.Message($"已执行");
            Log.Message($"startLeave:{startLeave}");
            Log.Message($"gotoEdge:{gotoEdge}");
            if (!startLeave) return;
            foreach (var p in guerrillas)
            {

                if (p == null) continue;
                var curJob = p.jobs?.curJob;
                if (curJob != null && curJob.def == DefOfs.RKU_AIEnterDrillingVehicle) continue;
                var handler = p.mindState?.mentalStateHandler;
                if (handler == null) continue;
                if (handler.CurStateDef == MentalStateDefOf.PanicFlee) continue;
                handler.TryStartMentalState(MentalStateDefOf.PanicFlee);
                
            }
        }

        // 没有钻机+地鼠全灭后进行重置
        void RestWithNoDrill()
        {
            if (!startLeave || drillLeave) return;
            foreach (var p in map.mapPawns.AllHumanlike)
            {
                if (p.Faction.def == DefOfs.RKU_Faction) return;

            }
            Reset();
        }

        // 重置为初始值
        void Reset()
        {
            drill = null;
            gotoEdge = false;
            drillLeave = true;
            drillDetect = false;
            startLeave = false;
            guerrillas = new();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref drill, "drill");
            Scribe_Values.Look(ref drillDetect, "drillDetect", false);
            Scribe_Values.Look(ref drillLeave, "drillLeave", true);
            Scribe_Values.Look(ref gotoEdge, "gotoEdge", false);
            Scribe_Values.Look(ref startLeave, "startLeave", false);
            Scribe_Collections.Look(ref guerrillas, "guerrillas", LookMode.Reference);
        }
    }
}
