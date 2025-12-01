using HarmonyLib;
using Verse;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using System;
using System.Reflection;
namespace RatkinUnderground
{
    public class RKU_WorldObjectDefModExtension : DefModExtension
    {
        public WorldObjectDef worldObjectDef;
    }
    public class RKU_MapGeneratorDefModExtension : DefModExtension
    {
        public bool isEncounterMap;
        public bool isSpawnCenter;
        public bool requireDrillingVehicle = false;
    }

    /// <summary>
    /// 为了防止出现3个enter
    /// </summary>
    public class RKU_MapParentModExtension : DefModExtension
    {
        public bool spawnMap = false;
    }
}