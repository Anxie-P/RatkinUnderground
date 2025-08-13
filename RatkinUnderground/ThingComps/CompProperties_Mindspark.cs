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
    public class CompProperties_Mindspark : CompProperties
    {
        public CompProperties_Mindspark()
        {
            compClass = typeof(Comp_Mindspark);
        }
    }

    public class Comp_Mindspark : ThingComp
    {
        public CompProperties_Mindspark Props => (CompProperties_Mindspark)props;

        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
        {
            foreach (var opt in base.CompFloatMenuOptions(selPawn))
                yield return opt;

            string label = "食用: " + parent.LabelCap;

            if (!selPawn.CanReach(parent, PathEndMode.ClosestTouch,Danger.None))
            {
                yield return new FloatMenuOption("无法到达" + parent.LabelCap, null);
                yield break;
            }

            if (!selPawn.CanReserve(parent))
            {
                yield return new FloatMenuOption(parent.LabelCap + "已被占用", null);
                yield break;
            }

            yield return new FloatMenuOption(label, delegate
            {
                Log.Message("已执行食用");
                Job job = JobMaker.MakeJob(DefOfs.RKU_EatMindspark, parent);
                selPawn.jobs.TryTakeOrderedJob(job);
            });
        }
    }
}
