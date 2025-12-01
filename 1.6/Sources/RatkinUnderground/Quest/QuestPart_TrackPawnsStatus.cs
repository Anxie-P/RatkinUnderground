using RimWorld;
using RimWorld.QuestGen;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RatkinUnderground
{
    public class QuestPart_TrackPawnsStatus : QuestPart
    {
        public List<Pawn> pawns;
        public bool failIfAllDead;
        public string failSignal;

        public override void Notify_QuestSignalReceived(Signal signal)
        {
            base.Notify_QuestSignalReceived(signal);
            if (pawns != null && pawns.Count > 0 && pawns.All(p => p != null && p.Dead) && failIfAllDead)
            {
                Find.SignalManager.SendSignal(new Signal(failSignal));
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref pawns, "pawns", LookMode.Reference);
            Scribe_Values.Look(ref failIfAllDead, "failIfAllDead");
            Scribe_Values.Look(ref failSignal, "failSignal");
        }
    }
}