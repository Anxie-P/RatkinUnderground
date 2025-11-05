using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RatkinUnderground
{
    public class CompProperties_RecordDamage : CompProperties
    {
        public CompProperties_RecordDamage()
        {
            this.compClass = typeof(Comp_RecordDamage);
        }
    }

    public class Comp_RecordDamage : ThingComp
    {
        const int negRelation = 10;
        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            base.Notify_Killed(prevMap, dinfo);
            Pawn damager = dinfo.Value.Instigator as Pawn;
            if (damager != null && damager.Faction.IsPlayer)
            {
                var radioComp = Current.Game.GetComponent<RKU_RadioGameComponent>();
                if (radioComp.isFinal)
                {
                    radioComp.ralationshipGrade = (radioComp.ralationshipGrade - 10) < -100
                    ? -100
                    : radioComp.ralationshipGrade - 10;
                }
                else
                {
                    radioComp.ralationshipGrade = (radioComp.ralationshipGrade - 10) < -74
                    ? -74
                    : radioComp.ralationshipGrade - 10;
                }
                
                Messages.Message($"游击队遭到了殖民者的攻击，好感度降低{negRelation}点", MessageTypeDefOf.NegativeEvent);
            }
        }
        /*public override void PostPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.PostPostApplyDamage(dinfo, totalDamageDealt);
            Pawn damager = dinfo.Instigator as Pawn;
            if (damager != null && damager.Faction.IsPlayer)
            {
                var radioComp = Current.Game.GetComponent<RKU_RadioGameComponent>();
                radioComp.ralationshipGrade -= 10;
                Messages.Message($"游击队遭到了殖民者的攻击，好感度降低{negRelation}点", MessageTypeDefOf.NegativeEvent);
            }
        }*/
    }

    
}
