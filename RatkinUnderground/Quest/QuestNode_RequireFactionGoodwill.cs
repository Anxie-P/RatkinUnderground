using RimWorld;
using RimWorld.QuestGen;
using Verse;

namespace RatkinUnderground
{
    public class QuestNode_RequireFactionGoodwill : QuestNode
    {
        public SlateRef<FactionDef> faction;
        public SlateRef<int> minGoodwill;

        protected override bool TestRunInt(Slate slate)
        {
            FactionDef factionDef = faction.GetValue(slate);
            int minGoodwillValue = minGoodwill.GetValue(slate);

            if (factionDef == null)
                return false;

            Faction targetFaction = Find.FactionManager.FirstFactionOfDef(factionDef);
            Faction playerFaction = Faction.OfPlayer;

            if (targetFaction == null || playerFaction == null)
                return false;

            return targetFaction.GoodwillWith(playerFaction) >= minGoodwillValue;
        }

        protected override void RunInt()
        {
        }
    }
}
