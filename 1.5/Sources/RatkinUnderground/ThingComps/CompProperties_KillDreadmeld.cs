using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RatkinUnderground
{
    public class RKU_KillDreadmeld : ThingComp
    {
        public CompProperties_KillDreadmeld Props =>
            (CompProperties_KillDreadmeld)this.props;
        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            Log.Message("已触发亡语");
            if (!ModLister.CheckAnomaly("Dreadmeld"))
                return;
            var comp = prevMap.GetComponent<RKU_DestroyFleshMap>();
            if (comp == null) return;
            RKU_DestroyFleshMap component = prevMap.GetComponent<RKU_DestroyFleshMap>();
            if (component == null)
                return;
            Find.LetterStack.ReceiveLetter((Letter)LetterMaker.MakeLetter("LetterLabelUndercaveCollapsing".Translate(), "LetterUndercaveCollapsing".Translate(), LetterDefOf.NeutralEvent));
            // 
            component.BeginCollapsing();
        }
    }

    public class CompProperties_KillDreadmeld : CompProperties
    {
        public CompProperties_KillDreadmeld()
        {
            this.compClass = typeof(RKU_KillDreadmeld);
        }
    }
}
