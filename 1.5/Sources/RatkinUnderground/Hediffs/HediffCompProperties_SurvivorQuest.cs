using RimWorld;
using RimWorld.QuestGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RatkinUnderground
{
    public class HediffCompProperties_SurvivorQuest : HediffCompProperties
    {
        public string signalTag = "RKU_SurvivorDead";
        public HediffCompProperties_SurvivorQuest()
        {
            this.compClass = typeof(HediffComp_SurvivorQuest);
        }

        public class HediffComp_SurvivorQuest : HediffComp
        {
            int ticks = 0;

            public HediffCompProperties_SurvivorQuest Props => (HediffCompProperties_SurvivorQuest)this.props;

            public override void CompPostTick(ref float severityAdjustment)
            {
                base.CompPostTick(ref severityAdjustment);

                ticks++;
                if (ticks < 60) return;
                ticks = 0;

                if (parent.pawn.Faction.IsPlayer)
                {
                    Faction faction = Find.FactionManager.FirstFactionOfDef(DefOfs.RKU_Faction);
                    faction.RelationWith(Faction.OfPlayer).baseGoodwill += 5;
                    Messages.Message("幸存者获救，与游击队的好感度提升了", MessageTypeDefOf.PositiveEvent);
                    parent.pawn.health.RemoveHediff(parent);
                }
            }
            public override void Notify_PawnKilled()
            {
                //Signal signal = new Signal();
                //Find.SignalManager.SendSignal(signal);
                Signal signal = new Signal("RKU_SurvivorDead", true);
                
                if (Find.SignalManager == null)
                {
                    Log.Error("[RCPU] SignalManager is not initialized!");
                }
                else
                {
                    Log.Message("[RCPU] Sending signal: " + signal.tag);
                    Find.SignalManager.SendSignal(signal);
                }
                parent.pawn.health.RemoveHediff(parent);
                base.Notify_PawnKilled();
            }
        }
    }
}
