using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI.Group;

namespace RatkinUnderground
{
    public class RKU_TunnelHiveSpawner_Und : RKU_TunnelHiveSpawner, IThingHolder
    {
        public Faction faction;
        ThingOwner<Pawn> passengers => (ThingOwner<Pawn>)this.GetDirectlyHeldThings();

        protected override void Spawn(Map map, IntVec3 loc)
        {

            if (!IsValidSpawnPosition(loc, map))
            {
                // 寻找最近的合法位置
                loc = FindClosestValidPosition(loc, map);
                if (!IsValidSpawnPosition(loc, map)) return;
            }

            // 生成钻地车
            IThingHolder drillingVehicle;
            drillingVehicle = (IThingHolder)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("RKU_DrillingVehicleInEnemyMap"));
            ((Thing)drillingVehicle).SetFaction(faction);
            ((Thing)drillingVehicle).HitPoints = ((Thing)drillingVehicle).MaxHitPoints;
            ((Thing)drillingVehicle).FactionPreventsClaimingOrAdopting(faction, false);
            //((Thing)drillingVehicle).ClaimableBy(Faction.IsPlayer);
            GenSpawn.Spawn((Thing)drillingVehicle, loc, map);
            var passengerHolder = (ThingOwner<Pawn>)this.GetDirectlyHeldThings();
            List<Pawn> toSpawn = new List<Pawn>();


            foreach (var p in passengerHolder)
            {
                toSpawn.Add(p);
            }

            /*var lordJob = new LordJob_AssaultColony(
            faction,                          // 袭击方派系
            canKidnap: true,                       // 允许绑架
            canTimeoutOrFlee: true,                // 时间到了逃跑
            sappers: true,                         // 破坏
            useAvoidGridSmart: true,                // 智能避让
            canSteal: true,                        // 俺拾嘞
            breachers: true,                       // 破墙
            canPickUpOpportunisticWeapons: true    // 捡武器
            );

            LordMaker.MakeNewLord(
                faction,   // Lord 所属派系
                lordJob,         // 上面 new 出来的 LordJob
                map,             // 当前地图
                toSpawn          // 这一批要纳入 Lord 管理的 Pawn 列表
            );*/

            foreach (var p in toSpawn)
            {

                passengers.Remove(p);
                //GenSpawn.Spawn(selPawn, Position, Map);
                // 放到载具生成点附近
                //var dropPos = CellFinder.RandomClosewalkCellNear(Position, Map, 1);
                GenSpawn.Spawn(p, loc, map);
            }
            // MovePassengers(drillingVehicle, passengers);

        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Deep.Look(ref faction, "faction", this);
        }
    }
}