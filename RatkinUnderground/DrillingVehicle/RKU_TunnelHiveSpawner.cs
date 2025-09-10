using Mono.Unix.Native;
using RimWorld;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;

namespace RatkinUnderground
{
    public class RKU_TunnelHiveSpawner : TunnelHiveSpawner, IThingHolder
    {
        private ThingOwner<Pawn> passengers;
        public bool canMove = false;
        public int hitPoints;   // 传递耐久
        public Faction faction;
        public IThingHolder drillingVehicle;
        public RKU_TunnelHiveSpawner()
        {
            passengers = new ThingOwner<Pawn>(this);
            if (passengers != null)
            {
                List<Pawn> list = new List<Pawn>(passengers);
                foreach (var p in list)
                {
                    Utils.TryRemoveWorldPawn(p);
                }
            }
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            if (passengers == null)
            {
                Log.Error("passengers is null in GetDirectlyHeldThings");
                passengers = new ThingOwner<Pawn>(this);
            }
            return passengers;
        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            if (outChildren == null)
            {
                Log.Error("outChildren is null in GetChildHolders");
                return;
            }
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.LoadingVars && passengers == null)
            {
                passengers = new ThingOwner<Pawn>(this);
            }
            Scribe_Deep.Look(ref passengers, "passengers", this);
        }

        protected override void Spawn(Map map, IntVec3 loc)
        {
            // 检查目标位置是否在地图边界内（考虑钻地车尺寸为1x2）
            if (!IsValidSpawnPosition(loc, map))
            {
                // 寻找最近的合法位置
                loc = FindClosestValidPosition(loc, map);
                if (!IsValidSpawnPosition(loc, map))
                {
                    return;
                }
            }
            if (canMove)
            {
                drillingVehicle = (IThingHolder)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("RKU_DrillingVehicle"));
            }
            else
            {
                drillingVehicle = (IThingHolder)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("RKU_DrillingVehicleInEnemyMap"));
            }

            // 生成钻地车
            ((Thing)drillingVehicle).SetFaction(faction ?? Faction.OfPlayer);
            ((Thing)drillingVehicle).HitPoints = this.hitPoints;    // 传递耐久
            GenSpawn.Spawn((Thing)drillingVehicle, loc, map);

            //非玩家情况：立刻下车
            if (faction != Faction.OfPlayer)
            {
                if (passengers != null)
                {
                    List<Pawn> pawnsToTransfer = new List<Pawn>(passengers);
                    for (int i = 0; i < pawnsToTransfer.Count; i++)
                    {
                        drillingVehicle.GetDirectlyHeldThings().Remove(pawnsToTransfer[i]);
                        Utils.TryAddWorldPawn(pawnsToTransfer[i]);
                        GenSpawn.Spawn(pawnsToTransfer[i], Position, map);
                        passengers.Remove(pawnsToTransfer[i]);
                    }
                }
            }
            else
            {
                // 将所有乘客转移到钻地车中
                MovePassengers(drillingVehicle, passengers);
            }
        }

        protected void MovePassengers(IThingHolder drillingVehicle, ThingOwner<Pawn> passengers)
        {
            if (passengers != null)
            {
                List<Pawn> pawnsToTransfer = new List<Pawn>(passengers);
                foreach (Pawn pawn in pawnsToTransfer)
                {
                    if (!pawn.Destroyed)
                    {
                        passengers.Remove(pawn);
                        Utils.TryAddWorldPawn(pawn);
                        drillingVehicle.GetDirectlyHeldThings().TryAddOrTransfer(pawn);
                    }
                }
            }
        }

        protected bool IsValidSpawnPosition(IntVec3 loc, Map map)
        {
            // 检查目标位置及其右侧一格是否在地图边界内
            return loc.InBounds(map) && (loc + IntVec3.East).InBounds(map);
        }
        protected IntVec3 FindClosestValidPosition(IntVec3 loc, Map map)
        {
            for (int radius = 1; radius <= 3; radius++)
            {
                foreach (IntVec3 candidate in GenRadial.RadialCellsAround(loc, radius, true))
                {
                    if (IsValidSpawnPosition(candidate, map))
                    {
                        return candidate;
                    }
                }
            }
            return loc;
        }
    }
} 