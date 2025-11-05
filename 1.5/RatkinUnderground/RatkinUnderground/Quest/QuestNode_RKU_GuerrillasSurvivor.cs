using RimWorld;
using RimWorld.QuestGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RatkinUnderground
{
    public class QuestNode_RKU_GuerrillasSurvivor : QuestNode
    {
        protected override void RunInt()
        {
            
        }

        protected override bool TestRunInt(Slate slate)
        {
            var faction = Find.FactionManager.FirstFactionOfDef(DefOfs.RKU_Faction);
            return faction.RelationWith(Faction.OfPlayer).baseGoodwill > 0;
        }
    }
}
