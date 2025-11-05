
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using Verse;

namespace RatkinUnderground;

public class QuestNode_RKU_GetSitePartDefsByTagsAndFaction : QuestNode
{
    public class SitePartOption
    {
        [NoTranslate]
        public string tag;

        public float chance = 1f;
    }

    public SlateRef<IEnumerable<SitePartOption>> sitePartsTags;

    [NoTranslate]
    public SlateRef<string> storeAs;

    [NoTranslate]
    public SlateRef<string> storeFactionAs;

    public SlateRef<Thing> mustBeHostileToFactionOf;

    private static List<string> tmpTags = new List<string>();

    protected override bool TestRunInt(Slate slate)
    {
        return TrySetVars(slate);
    }

    protected override void RunInt()
    {
        if (!TrySetVars(QuestGen.slate))
        {
            Log.Error("Could not resolve site parts.");
        }
    }

    private bool TrySetVars(Slate slate)
    {
        Faction factionToUse = slate.Get<Faction>("enemyFaction");
        Pawn asker = slate.Get<Pawn>("asker");
        Thing mustBeHostileToFactionOfResolved = mustBeHostileToFactionOf.GetValue(slate);
        for (int i = 0; i < 2; i++)
        {
            tmpTags.Clear();
            foreach (SitePartOption item in sitePartsTags.GetValue(slate))
            {
                if (Rand.Chance(item.chance) && (i != 1 || !(item.chance < 1f)))
                {
                    tmpTags.Add(item.tag);
                }
            }
            Faction faction;
            List<SitePartDef> siteParts = new List<SitePartDef>();

            // tags检测
            foreach (var sitePartDef in DefDatabase<SitePartDef>.AllDefs)
            {
                if (sitePartDef.tags != null && tmpTags.Any(tag => sitePartDef.tags.Contains(tag)))
                {
                    siteParts.Add(sitePartDef);
                }
            }

            if (siteParts.Count == 0)
            {
                continue;
            }

            // 派系检测
            faction = factionToUse ?? Find.FactionManager.FirstFactionOfDef(DefDatabase<FactionDef>.GetNamed("Rakinia"));
            if (faction == null)
            {
                continue;
            }

            slate.Set(storeAs.GetValue(slate), siteParts);
            slate.Set("sitePartCount", siteParts.Count);
            if (QuestGen.Working)
            {
                Dictionary<string, string> dictionary = new Dictionary<string, string>();
                for (int j = 0; j < siteParts.Count; j++)
                {
                    dictionary[siteParts[j].defName + "_exists"] = "True";
                }

                QuestGen.AddQuestDescriptionConstants(dictionary);
            }

            if (!storeFactionAs.GetValue(slate).NullOrEmpty())
            {
                slate.Set(storeFactionAs.GetValue(slate), faction);
            }

            return true;
        }

        return false;
    }
}