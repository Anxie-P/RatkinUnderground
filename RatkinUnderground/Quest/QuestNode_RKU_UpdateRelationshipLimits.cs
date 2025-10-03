using RimWorld.QuestGen;
using Verse;

namespace RatkinUnderground
{
    public class QuestNode_RKU_UpdateRelationshipLimits : QuestNode
    {
        protected override bool TestRunInt(Slate slate)
        {
            return true;
        }

        protected override void RunInt()
        {
            Utils.OnGuerrillaCampQuestSuccess();
        }
    }
}
