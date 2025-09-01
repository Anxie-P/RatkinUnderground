using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace RatkinUnderground
{
    public class JobDriver_SetC4 : JobDriver
    {
        private const TargetIndex TargetBuildingIndex = TargetIndex.A;
        private const int WorkDurationTicks = 120;
        
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            Pawn pawn = this.pawn;
            LocalTargetInfo target = job.GetTarget(TargetBuildingIndex);

            if (target.HasThing)
            {
                return pawn.Reserve(target, job, 1, -1, null, errorOnFailed);
            }
            else
            {
                return pawn.Reserve(target, job, 1, -1, null, errorOnFailed);
            }
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // 失败条件
            this.FailOnDespawnedOrNull(TargetBuildingIndex);
            
            yield return Toils_Goto.GotoThing(TargetBuildingIndex, PathEndMode.Touch);
            
            // 安装炸弹
            Toil workToil = Toils_General.Wait(WorkDurationTicks);
            workToil.WithProgressBarToilDelay(TargetBuildingIndex);
            workToil.FailOnDespawnedOrNull(TargetBuildingIndex);
            workToil.FailOnCannotTouch(TargetBuildingIndex, PathEndMode.Touch);
            yield return workToil;
            
            Toil placeBomb = new Toil();
            placeBomb.initAction = () =>
            {
                Pawn actor = placeBomb.actor;
                LocalTargetInfo target = actor.CurJob.GetTarget(TargetBuildingIndex);

                IntVec3 bombPosition;
                Map map;

                if (target.HasThing)
                {
                    Building targetBuilding = (Building)target.Thing;
                    if (targetBuilding == null || targetBuilding.Destroyed) return;

                    bombPosition = targetBuilding.Position;
                    map = targetBuilding.Map;
                }
                else
                {
                    bombPosition = target.Cell;
                    map = actor.Map;
                }

                Thing bomb = ThingMaker.MakeThing(ThingDef.Named("RKK_BuildingBoomber"));
                GenSpawn.Spawn(bomb, bombPosition, map);
            };
            yield return placeBomb;
            
            Toil exitMap = new Toil();
            exitMap.initAction = () =>
            {
                pawn.jobs.StopAll();
                
                Job exitJob = JobMaker.MakeJob(JobDefOf.Goto, CellFinder.RandomEdgeCell(pawn.Map));
                exitJob.exitMapOnArrival = true;
                exitJob.locomotionUrgency = LocomotionUrgency.Sprint; 
                pawn.jobs.StartJob(exitJob, JobCondition.InterruptForced);
            };
            yield return exitMap;
        }
    }
}