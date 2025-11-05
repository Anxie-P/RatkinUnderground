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
        private RKU_RadioGameComponent component => Current.Game.GetComponent<RKU_RadioGameComponent>();

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
            research.initAction = () =>
            {
                pawn.pather.StopDead();
                component.isSearch = true;      // 设置为正在研究状态
            };
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
            research.AddFinishAction(() => component.isSearch = false);     // 关闭正在研究状态
            yield return research;

            Toil checkProgress = new Toil();
            checkProgress.initAction = () =>
            {
                var radioThing = job.targetA.Thing;
                if (radioThing != null)
                {
                    var tempDialog = new Dialog_RKU_Radio(radioThing);
                    RKU_DialogueManager.TriggerDialogueEvents(tempDialog, "research");
                }
            };
            yield return checkProgress;
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
        public override PathEndMode PathEndMode => PathEndMode.InteractionCell;

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (t == null || t.IsForbidden(pawn) || !pawn.CanReserve(t, 1, -1, null, forced))
                return false;

            if (pawn.WorkTypeIsDisabled(WorkTypeDefOf.Research))
                return false;

            var component = Current.Game.GetComponent<RKU_RadioGameComponent>();
            var RadioComp = t.TryGetComp<Comp_RKU_Radio>();
            if (!RadioComp.isSearchJob) return false;
            if (component == null || component.ralationshipGrade <= 20) return false;

            return true;
        }
        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            return pawn.Map.listerThings.ThingsOfDef(ThingDef.Named("RKU_Radio"));
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return JobMaker.MakeJob(DefOfs.RKU_AssistResearch, t);
        }
    }

}