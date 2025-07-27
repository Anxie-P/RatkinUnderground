using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

namespace RatkinUnderground
{
    public class JobDriver_AIEnterDrillingVehicle : JobDriver
    {
        private const TargetIndex VehicleInd = TargetIndex.A;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }


        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(VehicleInd);
            yield return Toils_Goto.GotoThing(VehicleInd, PathEndMode.Touch);
            yield return Toils_General.Wait(60).WithProgressBarToilDelay(VehicleInd);
            yield return new Toil
            {
                initAction = delegate
                {
                    Thing thing = job.GetTarget(VehicleInd).Thing;
                    if (thing is IThingHolder vehicle)
                    {
                        pawn.CurJob.Clear();
                        pawn.DeSpawnOrDeselect();
                        vehicle.GetDirectlyHeldThings().TryAddOrTransfer(pawn);
                    }
                }
            };
        }
    }
}
