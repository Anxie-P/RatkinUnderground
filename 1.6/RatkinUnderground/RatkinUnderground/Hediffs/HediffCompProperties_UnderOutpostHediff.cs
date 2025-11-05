using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RatkinUnderground
{
    public class HediffCompProperties_UnderOutpostHediff : HediffCompProperties
    {
        public HediffCompProperties_UnderOutpostHediff()
        {
            this.compClass = typeof(HediffComp_UnderOutpostHediff);
        }
    }

    public class HediffComp_UnderOutpostHediff : HediffComp
    {
        int ticks = 0;
        public HediffCompProperties_UnderOutpostHediff Props => (HediffCompProperties_UnderOutpostHediff)this.props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            ticks++;
            if (ticks < 60) return;
            ticks = 0;

            Pawn.needs.food.CurLevel = Pawn.needs.food.MaxLevel;
            Pawn.needs.rest.CurLevel = Pawn.needs.rest.MaxLevel;
            Pawn.needs.mood.CurLevel = Pawn.needs.mood.MaxLevel;
        }
    }
}
