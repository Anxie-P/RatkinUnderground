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
    public class RKU_GuerrillasLeave : MapComponent
    {
        int tick = 0;
        bool isSpawned = false;          // 该地图是否生成地鼠
        bool gotoEdge = false;           // 是否到时间离开地图
        bool drillDetect = false;       // 是否具备钻机条件
        bool drillLeave = true;         // 钻机是否具备离开条件
        bool startLeave = false;        // 地鼠触发离开地图逻辑
        List<Pawn> guerrillas = new();
        RKU_DrillingVehicleInEnemyMap drill = null;

        public void SetStartLeave(bool startLeave)
        {
            this.startLeave = startLeave;
        }

        public void SetSpawned(bool isSpawned)
        {
            this.isSpawned = isSpawned;
        }

        public RKU_GuerrillasLeave(Map map) : base(map)
        {
        }
        public override void MapComponentTick()
        {
            base.MapComponentTick();

            if (!isSpawned) return;

            // 60tick 进行一次逻辑的检测和执行
            tick++;
            if (tick < 60) return;
            tick = 0;
            /*Log.Message($"isSpawned{isSpawned}");
            Log.Message($"gotoEdge{gotoEdge}");
            Log.Message($"drillDetect{drillDetect}");
            Log.Message($"drillLeave{drillLeave}");
            Log.Message($"startLeave{startLeave}");
            Log.Message($"————————————————————————");*/
            TryDrillOnMap();
            if(!drillDetect) return;    // 地图上不存在钻机，不执行下面逻辑
            // 检测到地鼠的钻机后获取所有地鼠
            if (!(guerrillas.Count > 0))
            {
                foreach(var p in map.mapPawns.AllHumanlikeSpawned)
                {
                    if (p.Faction.def != DefOfs.RKU_Faction) continue;
                    guerrillas.Add(p);
                }
            }

            // 如果地图上没有能够移动的地鼠，就触发钻机离开
            CanLeave();
            if (!drillLeave) return;
            if (drill.GetDirectlyHeldThings().Count > 0)
            {
                Messages.Message("地鼠们已经通过钻机离开了地图", MessageTypeDefOf.NeutralEvent);
                drill.DeSpawn();
                Reset();
            }
            else
            {
                Messages.Message("已经没有能够把钻机开回去的地鼠了...", MessageTypeDefOf.NeutralEvent);
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
                    return;
                } 
            }
        }

        // 是否具备离开条件
        void CanLeave()
        {
            foreach (var p in guerrillas)
            {
                if(p.CurJob == null) continue;
                if (p.CurJob.exitMapOnArrival)
                {
                    startLeave = true;
                    gotoEdge = true;
                }
            }
            if (gotoEdge)
            {
                foreach (var p in guerrillas)
                {
                    //p.CurJob.exitMapOnArrival = false;
                    Job job = new Job(DefOfs.RKU_AIEnterDrillingVehicle, drill);
                    p.jobs.StartJob(job, JobCondition.InterruptForced);

                }
                gotoEdge = false;
            }

            foreach (var p in guerrillas)
            {
                // 不在车里且能正常行动
                if (!(p.Dead || p.Downed) && !drill.ContainsPassenger(p))
                {
                    drillLeave = false;
                    return;
                }
            }

            drillLeave = true;
        }

        // 没有钻机+地鼠全灭后进行重置
        void RestWithNoDrill()
        {
            if (!startLeave || drillLeave) return;
            foreach (var p in map.mapPawns.AllHumanlikeSpawned)
            {
                if (p.Faction.def == DefOfs.RKU_Faction) return;

            }
        }

        // 重置为初始值
        void Reset()
        {
            drill = null;
            gotoEdge = false;
            drillLeave = true;
            drillDetect = false;
            startLeave = false;
            isSpawned = false;
            guerrillas = new();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref drill, "drill");
            Scribe_Values.Look(ref tick, "tick", 0);
            Scribe_Values.Look(ref drillDetect, "drillDetect", false);
            Scribe_Values.Look(ref drillLeave, "drillLeave", true);
            Scribe_Values.Look(ref gotoEdge, "gotoEdge", false);
            Scribe_Collections.Look(ref guerrillas, "guerrillas", LookMode.Reference);
        }
    }
}
