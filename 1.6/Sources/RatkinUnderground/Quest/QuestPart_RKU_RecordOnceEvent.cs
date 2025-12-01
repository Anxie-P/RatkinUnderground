using RimWorld;
using RimWorld.QuestGen;
using Verse;

namespace RatkinUnderground
{
    public class QuestPart_RKU_RecordOnceEvent : QuestPart
    {
        public string inSignal;
        public string eventKey;

        public override void Notify_QuestSignalReceived(Signal signal)
        {
            base.Notify_QuestSignalReceived(signal);
            
            if (signal.tag == inSignal)
            {
                RecordEvent();
            }
        }

        private void RecordEvent()
        {
            if (string.IsNullOrEmpty(eventKey))
            {
                Log.Warning("[RKU] QuestPart_RKU_RecordOnceEvent: eventKey 为空");
                return;
            }

            RKU_RadioGameComponent comp = Current.Game.GetComponent<RKU_RadioGameComponent>();
            if (comp != null && comp.triggeredOnceEvents != null)
            {
                if (!comp.triggeredOnceEvents.Contains(eventKey))
                {
                    comp.triggeredOnceEvents.Add(eventKey);
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref inSignal, "inSignal");
            Scribe_Values.Look(ref eventKey, "eventKey");
        }
    }
}

