using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace RatkinUnderground
{
    public class JobDriver_AssistResearch : JobDriver
    {
        private const int ResearchTicks = 2500;
        private Comp_RKU_Radio RadioComp => TargetThingA.TryGetComp<Comp_RKU_Radio>();

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            Pawn pawn = this.pawn;
            LocalTargetInfo target = this.job.targetA;
            return pawn.Reserve(target, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);

            Toil research = new Toil();
            research.initAction = () => pawn.pather.StopDead();
            research.tickAction = () =>
            {
                pawn.rotationTracker.FaceTarget(job.targetA);
                pawn.skills.Learn(SkillDefOf.Intellectual, 0.1f);

                if (pawn.IsHashIntervalTick(100))
                {
                    float points = CalculateResearchPoints();
                }
            };
            research.FailOnCannotTouch(TargetIndex.A, PathEndMode.InteractionCell);
            research.defaultCompleteMode = ToilCompleteMode.Delay;
            research.defaultDuration = ResearchTicks;
            yield return research;
        }

        private float CalculateResearchPoints()
        {
            float basePoints = 0.5f;
            float intelBonus = pawn.skills.GetSkill(SkillDefOf.Intellectual).Level * 0.05f;
            float totalPoints = basePoints + intelBonus;

            // 将研究点数添加到全局组件
            var radioComponent = Current.Game.GetComponent<RKU_RadioGameComponent>();
            if (radioComponent != null)
            {
                radioComponent.AddResearchProgress(totalPoints);
            }

            return totalPoints;
        }
    }

    public class WorkGiver_AssistResearch : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForDef(ThingDef.Named("RKU_Radio"));

        public override PathEndMode PathEndMode => PathEndMode.InteractionCell;

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (t == null || t.IsForbidden(pawn) || !pawn.CanReserve(t, 1, -1, null, forced))
                return false;

            //在这加好感度判断和任务阶段判断，还没写
            if (pawn.WorkTypeIsDisabled(WorkTypeDefOf.Research))
                return false;

            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return JobMaker.MakeJob(DefOfs.RKU_AssistResearch, t);
        }
    }

}