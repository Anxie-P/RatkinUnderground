using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace RatkinUnderground
{
    public class JobDriver_DeliverToCaptain : JobDriver
    {
        private Pawn Captain => (Pawn)job.targetA.Thing;
        private Thing Item => job.GetTarget(TargetIndex.B).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Captain, job, 1, -1, null, errorOnFailed) && (Item == null || pawn.Reserve(Item, job, 1, -1, null, errorOnFailed));
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            if (Item != null) {
                yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch);
                yield return Toils_Haul.StartCarryThing(TargetIndex.B);
            }
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch);
            Toil deliver = new Toil();
            deliver.initAction = () => {
                if (Captain.inventory != null && Item != null) {
                    Captain.inventory.innerContainer.TryAddOrTransfer(Item);
                }
            };
            deliver.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return deliver;
        }
    }
}