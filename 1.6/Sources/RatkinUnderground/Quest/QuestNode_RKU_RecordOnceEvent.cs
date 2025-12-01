using RimWorld.QuestGen;
using Verse;

namespace RatkinUnderground
{
    public class QuestNode_RKU_RecordOnceEvent : QuestNode
    {
        [NoTranslate]
        public SlateRef<string> inSignal;
        public SlateRef<string> eventKey;

        protected override bool TestRunInt(Slate slate)
        {
            return true;
        }

        protected override void RunInt()
        {
            Slate slate = QuestGen.slate;
            string key = eventKey.GetValue(slate);
            string signal = QuestGenUtility.HardcodedSignalWithQuestID(inSignal.GetValue(slate)) ?? slate.Get<string>("inSignal");

            if (string.IsNullOrEmpty(signal))
            {
                RKU_RadioGameComponent comp = Current.Game.GetComponent<RKU_RadioGameComponent>();
                if (comp != null && comp.triggeredOnceEvents != null)
                {
                    if (!comp.triggeredOnceEvents.Contains(key))
                    {
                        comp.triggeredOnceEvents.Add(key);
                    }
                }
            }
            else
            {
                QuestPart_RKU_RecordOnceEvent questPart = new QuestPart_RKU_RecordOnceEvent();
                questPart.inSignal = signal;
                questPart.eventKey = key;
                QuestGen.quest.AddPart(questPart);
            }
        }
    }
}




