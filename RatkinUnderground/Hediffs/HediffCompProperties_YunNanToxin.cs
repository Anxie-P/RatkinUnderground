using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;
using HarmonyLib;

namespace RatkinUnderground
{
    
    public class HediffCompProperties_YunNanToxin : HediffCompProperties
    {
        public List<string> stateList;
        public HediffCompProperties_YunNanToxin()
        {
            this.compClass = typeof(HediffComp_YunNanToxin);
        }
    }

    public class HediffComp_YunNanToxin : HediffComp
    {
        List<string> stateList => Props.stateList;
        public HediffCompProperties_YunNanToxin Props => (HediffCompProperties_YunNanToxin)this.props;
        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (parent.pawn.InMentalState) return;
            RandomMentalState();
        }

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            parent.pawn.mindState.mentalStateHandler.CurState.RecoverFromState();
            Messages.Message("RKU_RecoveredFromYunnanHallucination".Translate(parent.pawn.LabelShort), parent.pawn, MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>
        /// 从列表获得随机精神状态
        /// </summary>
        void RandomMentalState()
        {
            var ele = stateList.RandomElement();
            MentalStateDef state = DefDatabase<MentalStateDef>.GetNamed(ele, false);
            if (state == null) return;
            parent.pawn.mindState.mentalStateHandler.TryStartMentalState(state, "RKU_YunnanHallucination".Translate() + ":" + state.label.Translate(), forceWake: true);
            Log.Message("RKU_EnteredMentalState".Translate(parent.pawn, state.defName));
        }
    }
}
