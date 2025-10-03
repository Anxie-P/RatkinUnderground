using System;
using Verse;
using RimWorld;

namespace RatkinUnderground
{
    public class RKU_IncidentWorker_TriggerQuest : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return false;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            QuestScriptDef questDef = def.GetModExtension<QuestExtension>()?.quest;
            if (questDef == null)
            {
                return false;
            }

            Quest quest = QuestUtility.GenerateQuestAndMakeAvailable(questDef, StorytellerUtility.DefaultThreatPointsNow(parms.target));
            if (quest == null)
            {
                return false;
            }
            return true;
        }
    }

    public class QuestExtension : DefModExtension
    {
        public QuestScriptDef quest;
    }
}
