using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

namespace RatkinUnderground
{
    public class CompProperties_Allurecap : CompProperties
    {
        public List<string> stateList;
        public int allureRadius = 4;
        public CompProperties_Allurecap()
        {
            compClass = typeof(Comp_Allurecap);
        }
    }

    public class Comp_Allurecap : ThingComp,
        ISpecialMushroom
    {
        int tick = 0;
        public CompProperties_Allurecap Props => (CompProperties_Allurecap)props;

        public override void CompTick()
        {
            base.CompTick();

            tick++;
            if (tick < 30) return;
            tick = 0;

            IEnumerable<Pawn> pawns = GenRadial.RadialDistinctThingsAround(parent.Position, parent.Map, Props.allureRadius, true).OfType<Pawn>();
            foreach (var p in pawns)
            {
                if (!p.RaceProps.Humanlike ||
                    p.Drafted ||
                    p.CurJob.def == DefOfs.RKU_EatSpecialMushroom ||
                    p.CurJob.def == JobDefOf.HarvestDesignated ||
                    p.CurJob.def == JobDefOf.CutPlantDesignated ||
                    p.Downed ||
                    p.InMentalState) continue;
                if (p.IsColonist)
                {
                    Messages.Message("RKU_IntendsToEatAllurecap".Translate(p.Name.ToStringShort), MessageTypeDefOf.NegativeEvent);
                }
                Job job = JobMaker.MakeJob(DefOfs.RKU_EatSpecialMushroom, parent);
                p.jobs.TryTakeOrderedJob(job);
                break;
            }
        }

        public void TryAddEffect(Pawn pawn)
        {
            var allStates = DefDatabase<MentalStateDef>.AllDefsListForReading.ToList();
            try
            {
                MentalStateDef state = null;
                while (state == null)
                {
                    var ele = Props.stateList.RandomElement();
                    state = DefDatabase<MentalStateDef>.GetNamed(ele, false);
                }
                pawn.mindState.mentalStateHandler.TryStartMentalState(state, "RKU_AteAllurecap".Translate() + ":" + state.label.Translate(), forceWake: true);
            }
            catch (Exception e)
            {
                Log.Error("RKU_MentalStateError".Translate() + ": " + e);
            }
        }
    }
}
