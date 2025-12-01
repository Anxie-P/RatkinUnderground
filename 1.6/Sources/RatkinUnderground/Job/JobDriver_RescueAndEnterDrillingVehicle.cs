using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace RatkinUnderground
{
    public class JobDriver_RescueAndEnterDrillingVehicle : JobDriver
    {
        private const TargetIndex PawnInd = TargetIndex.A;
        private const TargetIndex VehicleInd = TargetIndex.B;

        protected Pawn PawnToRescue => job.GetTarget(PawnInd).Pawn;
        protected RKU_DrillingVehicle Vehicle => job.GetTarget(VehicleInd).Thing as RKU_DrillingVehicle;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(PawnToRescue, job, 1, -1, null, errorOnFailed) &&
                   pawn.Reserve(Vehicle, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(PawnInd);
            this.FailOnDestroyedOrNull(VehicleInd);
            this.FailOnSomeonePhysicallyInteracting(PawnInd);
            this.FailOn(() => PawnToRescue != null && !PawnToRescue.Downed);

            // 前往倒地pawn
            yield return Toils_Goto.GotoThing(PawnInd, PathEndMode.ClosestTouch).FailOnSomeonePhysicallyInteracting(PawnInd);
            pawn.jobs.curJob.count = 1;
            yield return Toils_Haul.StartCarryThing(PawnInd);


            Toil toil = Toils_Goto.GotoCell(VehicleInd, PathEndMode.Touch);

            yield return toil;

            // 等待进入钻机
            yield return Toils_General.Wait(60).WithProgressBarToilDelay(VehicleInd);

            // 最终进入钻机
            Toil enterVehicleToil = new Toil
            {
                initAction = () =>
                {
                    Pawn carriedPawn = pawn.carryTracker.CarriedThing as Pawn;

                    Log.Message($"[RKU_Rescue] 开始进入钻机: 救援者={pawn}, 被救援者={carriedPawn}, 钻机={Vehicle}");

                    if (Vehicle != null)
                    {
                        // 首先将被救援pawn放入钻机
                        if (carriedPawn != null)
                        {
                            Vehicle.passengers.TryAdd(carriedPawn);
                            Vehicle.AddPassenger(carriedPawn);
                            pawn.carryTracker.innerContainer.Remove(carriedPawn);
                            Log.Message($"[RKU_Rescue] 被救援pawn已放入钻机");
                        }

                        // 然后救援者自己进入钻机
                        if (Vehicle is IThingHolder thingHolder)
                        {
                            pawn.DeSpawnOrDeselect();
                            thingHolder.GetDirectlyHeldThings().TryAddOrTransfer(pawn);
                            Log.Message($"[RKU_Rescue] 救援者已进入钻机");
                        }

                        Log.Message($"[RKU_Rescue] 救援任务完全成功完成！");
                    }
                    else
                    {
                        Log.Message($"[RKU_Rescue] 进入钻机失败 - 钻机为空");
                    }
                }
            };
            enterVehicleToil.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return enterVehicleToil;
        }
    }
}