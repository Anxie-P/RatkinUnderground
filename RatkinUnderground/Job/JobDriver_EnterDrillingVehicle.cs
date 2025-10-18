using AlienRace;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Jobs;
using Verse;
using Verse.AI;
using static Verse.AI.ReservationManager;

namespace RatkinUnderground
{
    public class JobDriver_EnterDrillingVehicle : JobDriver
    {
        private const TargetIndex VehicleInd = TargetIndex.A;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }
        protected override IEnumerable<Toil> MakeNewToils()
        {
            // �ڽ���֮ǰ����Ƿ���
            if (job.GetTarget(VehicleInd).Thing is RKU_DrillingVehicleCargo)
            {
                yield return new Toil
                {
                    initAction = delegate
                    {
                        // ��ȡ��ǰ�ж���Pawn�����Ŀ�������Ԥ����
                        RKU_DrillingVehicleCargo TargetA = job.GetTarget(VehicleInd).Thing as RKU_DrillingVehicleCargo;
                        if (TargetA.enterPawns >= TargetA.maxPassengers)
                        {
                            pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                        }
                        TargetA.enterPawns = Math.Min(TargetA.enterPawns + 1, 2);
                        Log.Message($"Ԥ����������+1, ��ǰԤ����������: {TargetA.enterPawns}");
                    }
                };
            }


            this.FailOnDespawnedOrNull(VehicleInd);
            this.AddFinishAction(delegate (JobCondition condition)
            {
                if (job.GetTarget(VehicleInd).Thing is RKU_DrillingVehicleCargo)
                {
                    RKU_DrillingVehicleCargo TargetA = job.GetTarget(VehicleInd).Thing as RKU_DrillingVehicleCargo;
                    if (!TargetA.ContainsPassenger(pawn)) TargetA.enterPawns = Math.Max(0, TargetA.enterPawns - 1);
                }

            });
            yield return Toils_Goto.GotoThing(VehicleInd, PathEndMode.Touch);
            yield return Toils_General.Wait(60).WithProgressBarToilDelay(VehicleInd);
            yield return new Toil
            {
                initAction = delegate
                {
                    Thing thing = job.GetTarget(VehicleInd).Thing;
                    if (thing is IThingHolder vehicle)
                    {
                        pawn.DeSpawnOrDeselect();
                        vehicle.GetDirectlyHeldThings().TryAddOrTransfer(pawn);

                        Log.Message($"{vehicle} �ѽ���{pawn}");
                        if (vehicle is RKU_DrillingVehicleCargo)
                        {
                            RKU_DrillingVehicleCargo TargetA = vehicle as RKU_DrillingVehicleCargo;
                            TargetA.enterPawns = Math.Min(TargetA.enterPawns + 1, 2);
                        }
                    }
                }
            };
        }
        public override bool PlayerInterruptable => base.PlayerInterruptable;
    }
}
