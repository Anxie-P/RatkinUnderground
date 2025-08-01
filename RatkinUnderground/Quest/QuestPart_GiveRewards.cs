using RimWorld;
using RimWorld.QuestGen;
using System.Collections.Generic;
using Verse;

namespace RatkinUnderground
{
    public class QuestPart_GiveRewards : QuestPart
    {
        public List<Thing> rewards;
        public string inSignal;

        public override void Notify_QuestSignalReceived(Signal signal)
        {
            if (signal.tag == inSignal)
            {
                Map map = Find.AnyPlayerHomeMap;
                foreach (Thing reward in rewards)
                {
                    GenSpawn.Spawn(reward, map.Center, map);
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref rewards, "rewards", LookMode.Deep);
            Scribe_Values.Look(ref inSignal, "inSignal");
        }
    }
}