using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace RatkinUnderground.Patches;

[StaticConstructorOnStartup]

[HarmonyPatch(typeof(FactionGenerator), nameof(FactionGenerator.GenerateFactionsIntoWorld))]
class GenerateFactions_Patch
{
    static void Postfix()
    {
        List<FactionDef> enemyFaction = new List<FactionDef>
        {
            FactionDef.Named("Rakinia_Warlord"),
            FactionDef.Named("Rakinia")
        };
        Faction rFaction = Find.FactionManager.FirstFactionOfDef(DefOfs.RKU_Faction);
        if (rFaction == null) return;
        foreach (FactionDef enemy in enemyFaction)
        {
            Faction eFaction = Find.FactionManager.FirstFactionOfDef(enemy);
            eFaction.RelationWith(rFaction).baseGoodwill = -100;
            rFaction.RelationWith(eFaction).baseGoodwill = -100;
            FactionRelationKind oldKind1 = eFaction.RelationWith(rFaction).kind;
            FactionRelationKind oldKind2 = rFaction.RelationWith(eFaction).kind;
            eFaction.RelationWith(rFaction).kind = FactionRelationKind.Hostile;
            rFaction.RelationWith(eFaction).kind = FactionRelationKind.Hostile;
        }
    }
}

[HarmonyPatch(typeof(FactionGenerator), nameof(FactionGenerator.NewGeneratedFaction))]
public static class Patch_FactionGenerator_NewGeneratedFaction
{
    static void Postfix(Faction __result, FactionGeneratorParms parms)
    {
        if (__result.def == DefOfs.RKU_Faction)
        {
            var settlements = Find.WorldObjects.Settlements;
            for (int i = settlements.Count - 1; i >= 0; i--)
            {
                if (settlements[i].Faction != __result) continue;
                Find.WorldObjects.Remove(settlements[i]);
                Log.Message("[RKU] 已移除地鼠据点");
            }
        }
    }
}