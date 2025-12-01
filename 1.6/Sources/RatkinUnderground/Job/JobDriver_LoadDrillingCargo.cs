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
            foreach (Building building in pawn.Map.listerBuildings.allBuildingsColonist)
            {
                if (building is RKU_DrillingVehicleCargo vehicle &&
                    pawn.CanReach(vehicle, PathEndMode.Touch, Danger.None))
                {
                    CompTransporter compTransporter = vehicle.GetComp<CompTransporter>();
                    if (compTransporter != null && compTransporter.LoadingInProgressOrReadyToLaunch)
                    {
                        yield return vehicle;
                    }
                }
            }
        }

        public override bool HasJobOnThing(Pawn pawn, Thing thing, bool forced = false)
        {
            if (!(thing is RKU_DrillingVehicleCargo vehicle))
                return false;

            if (!pawn.CanReach(vehicle, PathEndMode.Touch, Danger.None))
                return false;

            CompTransporter compTransporter = vehicle.GetComp<CompTransporter>();
            if (compTransporter == null || !compTransporter.LoadingInProgressOrReadyToLaunch)
                return false;

            // 检查是否有需要装载的物品（只检查 leftToLoad 中的物品）
            if (compTransporter.leftToLoad == null || compTransporter.leftToLoad.Count == 0)
                return false;

            // 检查是否有可访问的需要装载的物品
            foreach (TransferableOneWay transferable in compTransporter.leftToLoad)
            {
                if (transferable.CountToTransferToDestination > 0 && transferable.HasAnyThing)
                {
                    foreach (Thing thingToLoad in transferable.things)
                    {
                        if (thingToLoad.Spawned &&
                            thingToLoad.Position.InBounds(pawn.Map) &&
                            pawn.CanReserve(thingToLoad) &&
                            pawn.CanReach(thingToLoad, PathEndMode.Touch, Danger.None))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public override Job JobOnThing(Pawn pawn, Thing thing, bool forced = false)
        {
            if (!(thing is RKU_DrillingVehicleCargo vehicle))
                return null;

            CompTransporter compTransporter = vehicle.GetComp<CompTransporter>();
            if (compTransporter == null || !compTransporter.LoadingInProgressOrReadyToLaunch)
                return null;

            if (compTransporter.leftToLoad == null || compTransporter.leftToLoad.Count == 0)
                return null;

            // 查找最近的需要装载的物品
            Thing bestItem = null;
            float bestDist = float.MaxValue;
            int bestAmount = 0;

            foreach (TransferableOneWay transferable in compTransporter.leftToLoad)
            {
                int countToTransfer = transferable.CountToTransferToDestination;
                if (countToTransfer <= 0 || !transferable.HasAnyThing)
                    continue;

                foreach (Thing item in transferable.things)
                {
                    if (item.Spawned &&
                        item.Position.InBounds(pawn.Map) &&
                        pawn.CanReserve(item) &&
                        pawn.CanReach(item, PathEndMode.Touch, Danger.None))
                    {
                        // 计算这个堆叠需要装载的数量
                        int amountToLoad = Mathf.Min(countToTransfer, item.stackCount);
                        if (amountToLoad > 0)
                        {
                            float dist = pawn.Position.DistanceTo(item.Position);
                            if (dist < bestDist)
                            {
                                bestDist = dist;
                                bestItem = item;
                                bestAmount = amountToLoad;
                            }
                        }
                    }
                }
            }

            if (bestItem != null)
            {
                Job job = JobMaker.MakeJob(DefOfs.RKU_LoadDrillingCargo, bestItem, vehicle);
                job.count = bestAmount;
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
            return pawn.Reserve(ItemToLoad, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(ItemToLoadIndex);
            this.FailOnDestroyedOrNull(VehicleIndex);

            yield return Toils_Goto.GotoThing(ItemToLoadIndex, PathEndMode.ClosestTouch);
            yield return Toils_Haul.StartCarryThing(ItemToLoadIndex);
            yield return Toils_Goto.GotoThing(VehicleIndex, PathEndMode.Touch);

            Toil loadToil = new Toil();
            loadToil.initAction = () =>
            {
                Thing carriedThing = pawn.carryTracker.CarriedThing;
                if (carriedThing != null)
                {
                    CompTransporter compTransporter = Vehicle.GetComp<CompTransporter>();
                    if (compTransporter == null)
                        return;

                    // 使用作业中指定的数量，但不超过实际携带的数量
                    int amountToLoad = Mathf.Min(job.count, carriedThing.stackCount);
                    if (amountToLoad > 0)
                    {
                        Thing splitThing = carriedThing.SplitOff(amountToLoad);
                        compTransporter.innerContainer.TryAddOrTransfer(splitThing, canMergeWithExistingStacks: true);
                        
                        // 更新 leftToLoad 列表
                        if (compTransporter.leftToLoad != null)
                        {
                            compTransporter.SubtractFromToLoadList(splitThing, amountToLoad, sendMessageOnFinished: false);
                        }
                    }

                    if (carriedThing.stackCount == 0)
                    {
                        pawn.carryTracker.innerContainer.Remove(carriedThing);
                    }
                }
            };
            loadToil.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return loadToil;
        }
    }
}
