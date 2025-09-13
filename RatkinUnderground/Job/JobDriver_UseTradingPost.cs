using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

namespace RatkinUnderground
{
    public class JobDriver_UseTradingPost : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // 先走到建筑旁
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil waitAndFace = new Toil();
            waitAndFace.initAction = delegate
            {
                pawn.pather.StopDead();
                Thing t = job.targetA.Thing;
                if (t != null) pawn.rotationTracker.FaceTarget(t.Position);
            };

            waitAndFace.defaultCompleteMode = ToilCompleteMode.Delay;
            waitAndFace.defaultDuration = 30;
            yield return waitAndFace;

            // 到达并等待后，打开交易窗口（瞬时）
            Toil openTrade = new Toil();
            openTrade.initAction = delegate
            {
                Pawn actor = pawn;
                Thing t = job.targetA.Thing;
                if (t is ITrader trader)
                {
                    try
                    {
                        Find.WindowStack.Add(new Dialog_Trade(actor, trader, false));
                    }
                    catch (Exception e)
                    {
                        Log.Warning($"[RKU] 打开交易窗口失败： {e}");
                    }
                }
                else
                {
                    Log.Warning($"[RKU] 目标非Itrader");
                }
            };
            openTrade.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return openTrade;

            yield break;
        }
    }
}
