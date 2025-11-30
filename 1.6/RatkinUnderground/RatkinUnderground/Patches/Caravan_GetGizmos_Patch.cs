using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RatkinUnderground
{
    [StaticConstructorOnStartup]
    [HarmonyPatch(typeof(CaravanMergeUtility), nameof(CaravanMergeUtility.ShouldShowMergeCommand), MethodType.Getter)]
    public class CaravanMerge_ShouldShowMergeCommand_Patch
    {
        private static bool Prefix(ref bool __result)
        {
            List<WorldObject> selectedObjects = Find.WorldSelector.SelectedObjects;

            for (int i = 0; i < selectedObjects.Count; i++)
            {
                if (selectedObjects[i] is Caravan caravan && caravan.IsPlayerControlled)
                {
                    if (caravan is RKU_DrillingVehicleOnMap)
                    {
                        __result = false;
                        return false;
                    }
                }
            }
            return true;
        }
    }
}
