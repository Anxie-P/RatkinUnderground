using RimWorld.QuestGen;
using Verse;

namespace RatkinUnderground
{
    public class QuestNode_RKU_RecordOnceEvent : QuestNode
    {
        public SlateRef<string> eventKey;

        protected override bool TestRunInt(Slate slate)
        {
            return true;
        }

        protected override void RunInt()
        {
            Slate slate = QuestGen.slate;
            string key = eventKey.GetValue(slate);
            RKU_RadioGameComponent comp = Current.Game.GetComponent<RKU_RadioGameComponent>();
            if (comp != null && comp.triggeredOnceEvents != null)
            {
                comp.triggeredOnceEvents.Add(key);
            }
        }
    }
}




