using RimWorld;
using RimWorld.QuestGen;
using Verse;

namespace RatkinUnderground
{
    public class QuestNode_RequireFactionGoodwill : QuestNode
    {
        public SlateRef<FactionDef> faction;
        public SlateRef<Faction> factionObj; // 直接接受Faction对象
        public SlateRef<int> minGoodwill;

        protected override bool TestRunInt(Slate slate)
        {
            Faction targetFaction = null;

            if (factionObj != null)
            {
                targetFaction = factionObj.GetValue(slate);
            }
            else if (faction != null)
            {
                FactionDef factionDef = faction.GetValue(slate);
                if (factionDef != null)
                {
                    targetFaction = Find.FactionManager.FirstFactionOfDef(factionDef);
                }
            }

            int minGoodwillValue = minGoodwill.GetValue(slate);
            Faction playerFaction = Faction.OfPlayer;

            if (targetFaction == null || playerFaction == null)
                return false;

            return targetFaction.GoodwillWith(playerFaction) >= minGoodwillValue;
        }

        protected override void RunInt()
        {
        }
    }

    public class QuestNode_RequireFactionGoodwillExact : QuestNode
    {
        public SlateRef<FactionDef> faction;
        public SlateRef<int> exactGoodwill;

        protected override bool TestRunInt(Slate slate)
        {
            FactionDef factionDef = faction.GetValue(slate);
            int exactGoodwillValue = exactGoodwill.GetValue(slate);

            if (factionDef == null)
                return false;

            Faction targetFaction = Find.FactionManager.FirstFactionOfDef(factionDef);
            Faction playerFaction = Faction.OfPlayer;

            if (targetFaction == null || playerFaction == null)
                return false;

            return targetFaction.GoodwillWith(playerFaction) == exactGoodwillValue;
        }

        protected override void RunInt()
        {
        }
    }
}
