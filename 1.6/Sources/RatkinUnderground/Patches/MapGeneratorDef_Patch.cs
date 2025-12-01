using HarmonyLib;
using Verse;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using System;
using System.Reflection;


namespace RatkinUnderground;

[HarmonyPatch(typeof(MapGenerator), "GenerateContentsIntoMap")]
public static class MapGenerator_GenerateContentsIntoMap_Patch
{
    private static readonly FieldInfo dataField = AccessTools.Field(typeof(MapGenerator), "data");
    private static readonly FieldInfo tmpGenStepsField = AccessTools.Field(typeof(MapGenerator), "tmpGenSteps");
    private static readonly MethodInfo getSeedPartMethod = AccessTools.Method(typeof(MapGenerator), "GetSeedPart");

    public static bool Prefix(IEnumerable<GenStepWithParams> genStepDefs, Map map, int seed)
    {
        if (map.generatorDef != null && map.generatorDef.defName.StartsWith("RKU_"))
        {
            var data = (Dictionary<string, object>)dataField.GetValue(null);
            var tmpGenSteps = (List<GenStepWithParams>)tmpGenStepsField.GetValue(null);

            data.Clear();
            Rand.PushState();
            try
            {
                Rand.Seed = seed;
                RockNoises.Init(map);
                tmpGenSteps.Clear();
                tmpGenSteps.AddRange(genStepDefs.OrderBy(x => map.generatorDef.genSteps.IndexOf(x.def)));
                
                for (int i = 0; i < tmpGenSteps.Count; i++)
                {
                    DeepProfiler.Start("GenStep - " + tmpGenSteps[i].def);
                    try
                    {
                        Rand.Seed = Gen.HashCombineInt(seed, (int)getSeedPartMethod.Invoke(null, new object[] { tmpGenSteps, i }));

                        Log.Warning(tmpGenSteps[i].def.defName);

                        tmpGenSteps[i].def.genStep.Generate(map, tmpGenSteps[i].parms);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Error in GenStep: " + ex);
                    }
                    finally
                    {
                        DeepProfiler.End();
                    }
                }
            }
            finally
            {
                Rand.PopState();
                RockNoises.Reset();
                data.Clear();
            }
            return false;
        }
        return true;
    }
}
