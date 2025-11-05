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
            QuestScriptDef questDef = def.GetModExtension<QuestExtension>()?.quest;
            if (questDef == null)
            {
                return false;
            }

            Slate slate = new Slate();
            slate.Set("points", StorytellerUtility.DefaultThreatPointsNow(Find.World));
            slate.Set("asker", Find.FactionManager.FirstFactionOfDef(FactionDef.Named("Rakinia"))?.leader);
            slate.Set("enemyFaction", Find.FactionManager.FirstFactionOfDef(DefOfs.RKU_Faction));
            slate.Set("map", (Map)parms.target);

            return questDef.root.TestRun(slate);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            QuestScriptDef questDef = def.GetModExtension<QuestExtension>()?.quest;
            if (questDef == null)
            {
                return false;
            }

            // 设置全局的 QuestGen.slate，这是 QuestNode_Raid.RunInt() 所使用的
            QuestGen.slate = new Slate();
            QuestGen.slate.Set("points", StorytellerUtility.DefaultThreatPointsNow(Find.World));
            QuestGen.slate.Set("asker", Find.FactionManager.FirstFactionOfDef(FactionDef.Named("Rakinia"))?.leader);
            QuestGen.slate.Set("enemyFaction", Find.FactionManager.FirstFactionOfDef(DefOfs.RKU_Faction));
            QuestGen.slate.Set("map", (Map)parms.target);

            if (questDef.root.TestRun(QuestGen.slate))
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
