using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.Sound;

namespace RatkinUnderground
{
    public class RKU_YunNanBullet : Bullet
    {
        HediffDef toxin => HediffDef.Named("RKU_YunNanToxin");
        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            base.Impact(hitThing, blockedByShield);
            if(!(hitThing is Pawn pawn))    return;
            if(pawn.RaceProps.IsMechanoid)  return;
            OnHitPawnCallback(pawn);
        }

        void OnHitPawnCallback(Pawn pawn)
        {
            // 已有的不再生效
            if (pawn.health.hediffSet.HasHediff(toxin)) return;
            var hediff = HediffMaker.MakeHediff(toxin, pawn, null);
            pawn.health.AddHediff(hediff);
        }
    }
}
