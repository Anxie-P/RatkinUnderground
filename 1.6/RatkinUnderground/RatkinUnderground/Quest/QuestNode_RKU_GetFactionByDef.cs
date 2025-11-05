using RimWorld;
using RimWorld.BaseGen;
using RimWorld.Planet;
using RimWorld.QuestGen;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RatkinUnderground
{
    public class QuestNode_RKU_GetFactionByDef : QuestNode_GetFaction
    {
        public SlateRef<IEnumerable<FactionDef>> requiredFactionDefs;  

        protected override bool TestRunInt(Slate slate)
        {
            //if (requiredFactionDefs.GetValue(slate) == null || !requiredFactionDefs.GetValue(slate).Any())
            //{
            //    return false;
            //}
            return true;//base.TestRunInt(slate);
        }

        protected override void RunInt()
        {
            Slate slate = QuestGen.slate;
            Faction selectedFaction = null;

            if (requiredFactionDefs.GetValue(slate) != null && requiredFactionDefs.GetValue(slate).Any())
            {
                var possibleFactions = Find.FactionManager.AllFactions
                    .Where(f => requiredFactionDefs.GetValue(slate).Contains(f.def))
                    .ToList();

                if (possibleFactions.Any())
                {
                    possibleFactions.TryRandomElement(out selectedFaction);
                }
            }

            if (selectedFaction == null)
            {
                base.RunInt();
                return;
            }
            QuestGen.slate.Set(storeAs.GetValue(slate), selectedFaction);
            if (!selectedFaction.Hidden)
            {
                QuestPart_InvolvedFactions questPart = new QuestPart_InvolvedFactions();
                questPart.factions.Add(selectedFaction);
                QuestGen.quest.AddPart(questPart);
            }
        }
    }
}