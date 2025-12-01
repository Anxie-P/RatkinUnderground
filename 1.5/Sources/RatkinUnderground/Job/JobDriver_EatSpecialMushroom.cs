using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace RatkinUnderground
{
    public class JobDriver_EatSpecialMushroom : JobDriver
    {
        const TargetIndex UseTarget = TargetIndex.A;
        
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            LocalTargetInfo target = job.GetTarget(UseTarget);
            return this.pawn.Reserve(this.job.GetTarget(UseTarget), this.job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(UseTarget);

            yield return Toils_Goto.GotoThing(UseTarget, PathEndMode.ClosestTouch);
            yield return Toils_General.Wait(240).WithProgressBarToilDelay(UseTarget);
            yield return new Toil
            {
                initAction = delegate
                {
                    try
                    {
                        ISpecialMushroom special = null;
                        ThingWithComps t = (job.GetTarget(UseTarget).Thing) as ThingWithComps;
                        special = t.AllComps.OfType<ISpecialMushroom>().FirstOrDefault();
                        if (special == null) return;
                        special.TryAddEffect(pawn);
                        t.Destroy(DestroyMode.Vanish);
                    }
                    catch (Exception e)
                    {

                        throw new("finishaction出错" + e);
                    }
                }
            };
        }
    }
}
