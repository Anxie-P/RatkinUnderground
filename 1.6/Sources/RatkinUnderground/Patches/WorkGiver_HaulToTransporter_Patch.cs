using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace RatkinUnderground
{
    [HarmonyPatch(typeof(WorkGiver_LoadTransporters), "HasJobOnThing", new System.Type[] { typeof(Pawn), typeof(Thing), typeof(bool) })]
    public static class WorkGiver_LoadTransporters_HasJobOnThing_Patch
    {
        public static bool Prefix(Pawn pawn, Thing t, bool forced, ref bool __result)
        {
            if (t is RKU_DrillingVehicleCargo)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(WorkGiver_LoadTransporters), "JobOnThing", new System.Type[] { typeof(Pawn), typeof(Thing), typeof(bool) })]
    public static class WorkGiver_LoadTransporters_JobOnThing_Patch
    {
        public static bool Prefix(Pawn pawn, Thing t, bool forced, ref Job __result)
        {
            if (t is RKU_DrillingVehicleCargo)
            {
                __result = null;
                return false;
            }
            return true;
        }
    }
}

