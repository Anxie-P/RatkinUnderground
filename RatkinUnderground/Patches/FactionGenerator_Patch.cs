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
            eFaction.RelationWith(rFaction).kind = FactionRelationKind.Hostile;
            rFaction.RelationWith(eFaction).kind = FactionRelationKind.Hostile;
        }
    }
}
