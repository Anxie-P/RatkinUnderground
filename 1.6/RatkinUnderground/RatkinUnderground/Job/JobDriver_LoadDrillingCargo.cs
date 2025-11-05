using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RatkinUnderground
{
    public class WorkGiver_LoadDrillingCargo : WorkGiver_Scanner
    {
        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            // 查找所有可用的钻机
            foreach (Building building in pawn.Map.listerBuildings.allBuildingsColonist)
            {
                if (building is RKU_DrillingVehicleCargo vehicle &&
                    pawn.CanReach(vehicle, PathEndMode.Touch, Danger.None) &&
                    vehicle.HasItemsToLoad())
                {
                    // 返回这个钻机，让殖民者可以"工作"在这个钻机上
                    yield return vehicle;
                }
            }
        }

        public override bool HasJobOnThing(Pawn pawn, Thing thing, bool forced = false)
        {
            if (!(thing is RKU_DrillingVehicleCargo vehicle))
                return false;

            if (!pawn.CanReach(vehicle, PathEndMode.Touch, Danger.None))
                return false;

            if (!vehicle.HasItemsToLoad())
                return false;

            // 检查是否有用户选择的物品可以装载
            foreach (Thing item in pawn.Map.listerThings.AllThings)
            {
                if (item.def.category == ThingCategory.Item &&
                    item.Position.InBounds(pawn.Map) &&
                    pawn.CanReserve(item) &&
                    pawn.CanReach(item, PathEndMode.Touch, Danger.None) &&
                    vehicle.ShouldLoadItem(item.def))
                {
                    return true;
                }
            }

            return false;
        }

        public override Job JobOnThing(Pawn pawn, Thing thing, bool forced = false)
        {
            if (!(thing is RKU_DrillingVehicleCargo vehicle))
                return null;

            if (!vehicle.HasItemsToLoad())
                return null;

            // 查找最近的用户选择要装载的物品
            Thing bestItem = null;
            float bestDist = float.MaxValue;

            foreach (Thing item in pawn.Map.listerThings.AllThings)
            {
                if (item.def.category == ThingCategory.Item &&
                    item.Position.InBounds(pawn.Map) &&
                    pawn.CanReserve(item) &&
                    pawn.CanReach(item, PathEndMode.Touch, Danger.None) &&
                    vehicle.ShouldLoadItem(item.def))
                {
                    float dist = pawn.Position.DistanceTo(item.Position);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestItem = item;
                    }
                }
            }

            if (bestItem != null)
            {
                int loadAmount = vehicle.GetLoadAmount(bestItem.def);
                int actualAmount = Mathf.Min(loadAmount, bestItem.stackCount);
                Log.Message($"[RKU] WorkGiver分配作业: {bestItem.def.defName}, 需要装载{loadAmount}个, 实际{actualAmount}个");
                Job job = JobMaker.MakeJob(DefOfs.RKU_LoadDrillingCargo, bestItem, vehicle);
                job.count = actualAmount;
                return job;
            }

            return null;
        }
    }

    public class JobDriver_LoadDrillingCargo : JobDriver
    {
        private const TargetIndex ItemToLoadIndex = TargetIndex.A;
        private const TargetIndex VehicleIndex = TargetIndex.B;

        protected Thing ItemToLoad => job.GetTarget(ItemToLoadIndex).Thing;
        protected RKU_DrillingVehicleCargo Vehicle => (RKU_DrillingVehicleCargo)job.GetTarget(VehicleIndex).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // 只预留物品，钻机允许多个殖民者同时工作
            return pawn.Reserve(ItemToLoad, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(ItemToLoadIndex);
            this.FailOnDestroyedOrNull(VehicleIndex);

            // 前往物品
            yield return Toils_Goto.GotoThing(ItemToLoadIndex, PathEndMode.ClosestTouch);

            // 捡起物品
            yield return Toils_Haul.StartCarryThing(ItemToLoadIndex);

            // 前往钻机
            yield return Toils_Goto.GotoThing(VehicleIndex, PathEndMode.Touch);

            // 放入钻机
            Toil unloadToil = new Toil();
            unloadToil.initAction = () =>
            {
                Thing carriedThing = pawn.carryTracker.CarriedThing;
                if (carriedThing != null)
                {
                    // 检查是否还有需要装载的数量
                    int remainingLoadAmount = Vehicle.GetLoadAmount(carriedThing.def);
                    if (remainingLoadAmount <= 0)
                    {
                        Log.Message($"[RKU] 取消装载作业: {carriedThing.def.defName} 已不需要装载");
                        return;
                    }

                    int amount = Mathf.Min(Mathf.Min(carriedThing.stackCount, job.count), remainingLoadAmount);
                    if (amount <= 0)
                    {
                        Log.Message($"[RKU] 取消装载作业: {carriedThing.def.defName} 数量不足");
                        return;
                    }

                    Log.Message($"[RKU] 装载作业执行: 搬运 {amount} 个 {carriedThing.def.defName} 到钻机");
                    Thing splitThing = carriedThing.SplitOff(amount);
                    Log.Message($"[RKU] 分离物品: splitThing={splitThing}, stackCount={splitThing.stackCount}");
                    var cargo = Vehicle.GetDirectlyHeldThings();
                    Log.Message($"[RKU] 钻机cargo: {cargo}");
                    bool added = cargo.TryAdd(splitThing);
                    Log.Message($"[RKU] 添加到cargo结果: {added}, cargo.Count={cargo.Count}");
                    if (carriedThing.stackCount == 0)
                    {
                        pawn.carryTracker.innerContainer.Remove(carriedThing);
                        Log.Message($"[RKU] 从pawn容器移除空物品");
                    }
                    // 更新剩余装载数量
                    Vehicle.ReduceLoadAmount(carriedThing.def, amount);
                    Log.Message($"[RKU] 装载完成，更新剩余数量");
                }
                else
                {
                    Log.Message($"[RKU] 错误：pawn没有携带物品");
                }
            };
            unloadToil.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return unloadToil;
        }
    }
}
