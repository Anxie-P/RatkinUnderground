using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse.AI;
using Verse;
using System.Reflection;
using Verse.AI.Group;

namespace RatkinUnderground
{
    // 我看战败cg的小人走的是精神状态，所以就patch的这一部分
    [HarmonyPatch(typeof(MentalStateHandler), "MentalStateHandlerTick")]
    public static class MentalStateHandlerTick_Patch
    {
        static void Postfix(MentalStateHandler __instance)
        {
            var pawnField = AccessTools.Field(typeof(MentalStateHandler), "pawn");
            Pawn pawn = pawnField.GetValue(__instance) as Pawn;
            if (pawn.Faction?.def != DefOfs.RKU_Faction) return;
            // 通过属性判断当前状态是否为 PanicFlee
            if (__instance.CurStateDef == MentalStateDefOf.PanicFlee)
            {
                foreach (var building in pawn.Map.listerBuildings.allBuildingsNonColonist)
                {
                    if (building.def.defName != "RKU_DrillingVehicleInEnemyMap") continue;
                    pawn.mindState.Reset();
                    pawn.mindState.duty = new PawnDuty(DutyDefOf.TravelOrWait, building.Position);
                    Job job = new Job(DefOfs.RKU_AIEnterDrillingVehicle, building);
                    pawn.jobs.TryTakeOrderedJob(job);
                    var comp = pawn.Map.GetComponent<RKU_GuerrillasLeave>();
                    if (comp != null)
                    {
                        Log.Message("含有RKU_GuerrillasLeave");
                        comp.SetStartLeave(true);
                    }
                    break;
                }
            }
        }
    }
}
