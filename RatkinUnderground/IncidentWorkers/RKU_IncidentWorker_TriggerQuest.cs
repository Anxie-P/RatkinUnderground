using System;
using Verse;
using RimWorld;
using RimWorld.QuestGen;

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
            Slate slate = new Slate();
            slate.Set("points", StorytellerUtility.DefaultThreatPointsNow(Find.World));
            slate.Set("asker", Find.FactionManager.FirstFactionOfDef(FactionDef.Named("Rakinia"))?.leader);
            slate.Set("enemyFaction", Find.FactionManager.FirstFactionOfDef(DefOfs.RKU_Faction));
            if (questDef.root.TestRun(slate))
            {
                Quest quest = QuestUtility.GenerateQuestAndMakeAvailable(questDef, StorytellerUtility.DefaultThreatPointsNow(parms.target));
                QuestUtility.SendLetterQuestAvailable(quest);
                if (quest == null)
                {
                    return false;
                }
                return true;
            }
            return false;
        }
    }

    public class QuestExtension : DefModExtension
    {
        public QuestScriptDef quest;
    }
}
