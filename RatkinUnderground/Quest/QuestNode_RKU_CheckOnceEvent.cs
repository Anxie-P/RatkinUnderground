using RimWorld.QuestGen;
using Verse;

namespace RatkinUnderground
{
    public class QuestNode_RKU_CheckOnceEvent : QuestNode
    {
        public SlateRef<string> eventKey;
        public SlateRef<bool> invert = false;

        protected override bool TestRunInt(Slate slate)
        {
            string key = eventKey.GetValue(slate);
            bool shouldInvert = invert.GetValue(slate);
            RKU_RadioGameComponent comp = Current.Game.GetComponent<RKU_RadioGameComponent>();
            if (comp == null)
            {
                return shouldInvert;
            }

            // 检查是否已触发过
            bool hasTriggered = comp.triggeredOnceEvents != null && comp.triggeredOnceEvents.Contains(key);
            bool conditionMet = shouldInvert ? !hasTriggered : hasTriggered;

            return conditionMet;
        }

        protected override void RunInt()
        {
        }
    }
}
