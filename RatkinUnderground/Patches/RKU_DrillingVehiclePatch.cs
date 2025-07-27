using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld.Planet;
using RimWorld;
using HarmonyLib;
using static HarmonyLib.AccessTools;
using static UnityEngine.GraphicsBuffer;
using System.Security.Cryptography;
using System.IO;

namespace RatkinUnderground;

[StaticConstructorOnStartup]
public static class HarmonyEntry
{
    static HarmonyEntry()
    {
        Harmony entry = new Harmony("RatkinUnderground");
        entry.PatchAll();
    }

}

[StaticConstructorOnStartup]
public static class RKU_DrillingVehiclePatch
{
    private static readonly FieldRef<Caravan_PathFollower, Caravan> caravanField =
        AccessTools.FieldRefAccess<Caravan_PathFollower, Caravan>("caravan");

    [HarmonyPatch(typeof(Caravan_PathFollower), "CostToPayThisTick")]
    public static class CostToPayThisTick_Patch
    {
        public static bool Prefix(Caravan_PathFollower __instance, ref float __result)
        {
            if (caravanField(__instance) is RKU_DrillingVehicleOnMap)
            {
                System.Random rand = new System.Random();
                if (rand.Next(0, 999) < 2) 
                {
                    Log.Message($"已生成地图");
                    __result = 1f;
                    var drill = caravanField(__instance);
                    var incidentDef = DefDatabase<IncidentDef>.AllDefs.ToList().FindAll(o=>o.defName.Contains("RKU_Incident_BuildMap_")).RandomElement();
                    if (incidentDef != null)
                    {

                        var pather = __instance;
                        int nodes = pather.curPath.NodesReversed.Count;
                        float traveledPct = (drill as RKU_DrillingVehicleOnMap).getTraveledPct();
                        int index = (int)(traveledPct * (nodes - 1));
                        index = Mathf.Clamp(index, 0, nodes - 1);
                        int eventTile = pather.curPath.NodesReversed[index];
                        //检测这地方上不能有任何存在的worldthing
                        if (Find.WorldObjects.AllWorldObjects.Any(obj => obj.Tile == eventTile && obj != caravanField(__instance)))
                        {
                            Log.Warning("存在worldthing");
                            return false;
                        }
                        var temp = drill;
                        temp.Tile = eventTile;
                        var parms = StorytellerUtility.DefaultParmsNow(incidentDef.category, temp);
                        parms.target = drill;
                        parms.faction = Faction.OfPlayer;

                        incidentDef.Worker.TryExecute(parms);

                        (caravanField(__instance) as RKU_DrillingVehicleOnMap).resetTraveledPct();
                    }
                    else
                    {
                        Log.Error("IncidentDef: RKU_Incident_BuildMap");
                    }
                }
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Caravan), "NightResting", MethodType.Getter)]
    public static class NightResting_Patch
    {
        public static bool Prefix(Caravan __instance, ref bool __result)
        {
            if (__instance is RKU_DrillingVehicleOnMap)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(WorldPathFinder), "FindPath")]
    public static class FindPath_Patch
    {
        public static bool Prefix(int startTile, int destTile, Caravan caravan, Func<float, bool> terminator, ref WorldPath __result)
        {
            if (caravan is RKU_DrillingVehicleOnMap)
            {
                WorldPath path = new WorldPath();
                int currentTile = startTile;

                path.AddNodeAtStart(startTile);

                while (currentTile != destTile)
                {
                    List<int> neighbors = new List<int>();
                    Find.WorldGrid.GetTileNeighbors(currentTile, neighbors);

                    float minDist = float.MaxValue;
                    int nextTile = currentTile;

                    foreach (int neighbor in neighbors)
                    {
                        float dist = Find.WorldGrid.ApproxDistanceInTiles(neighbor, destTile);
                        if (dist < minDist)
                        {
                            minDist = dist;
                            nextTile = neighbor;
                        }
                    }

                    if (nextTile == currentTile || neighbors.Count == 0)
                    {
                        break;
                    }

                    path.AddNodeAtStart(nextTile);
                    currentTile = nextTile;
                }

                path.AddNodeAtStart(destTile);
                path.SetupFound(Find.WorldGrid.ApproxDistanceInTiles(startTile, destTile));

                __result = path;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(CaravanEnterMapUtility), "Enter", new Type[] { typeof(Caravan), typeof(Map), typeof(Func<Pawn, IntVec3>), typeof(CaravanDropInventoryMode), typeof(bool) })]
    public static class Enter_Patch
    {
        public static bool Prefix(Caravan caravan, Map map, Func<Pawn, IntVec3> spawnCellGetter, CaravanDropInventoryMode dropInventoryMode, bool draftColonists)
        {
            if (caravan is RKU_DrillingVehicleOnMap)
            {
                if (map?.generatorDef == null)
                {
                    return true;
                }

                // 如果是殖民地
                if (map.IsPlayerHome)
                {
                    CameraJumper.TryJump(map.Center, map);
                    IntVec3 target = new IntVec3();
                    CellFinder.TryFindRandomEdgeCellWith((IntVec3 x) => x.Standable(map) && x.InBounds(map), map, CellFinder.EdgeRoadChance_Hostile, out target);
                    RKU_TunnelHiveSpawner tunnelHiveSpawner = (RKU_TunnelHiveSpawner)ThingMaker.MakeThing(DefOfs.RKU_TunnelHiveSpawner);
                    tunnelHiveSpawner.hitPoints = (caravan as RKU_DrillingVehicleOnMap).hitPoints;  // 传递耐久
                    tunnelHiveSpawner.canMove = true;//可以移动

                    List<Pawn> pawnsToTransfer2 = new List<Pawn>(caravan.pawns);

                    foreach (Pawn pawn in pawnsToTransfer2)
                    {
                        if (!pawn.Destroyed)
                        {
                            tunnelHiveSpawner.GetDirectlyHeldThings().TryAddOrTransfer(pawn);
                        }
                    }
                    GenSpawn.Spawn(tunnelHiveSpawner, target, map, WipeMode.Vanish);
                    CameraJumper.TryJump(tunnelHiveSpawner);
                    map.fogGrid.FloodUnfogAdjacent(target, false);
                    caravan.Destroy();
                }
                var modExtension = map.generatorDef.GetModExtension<RKU_MapGeneratorDefModExtension>();
                if (modExtension == null)
                {
                    return true;
                }
                if (modExtension != null && modExtension.isEncounterMap)
                {
                    CameraJumper.TryJump(map.Center, map);
                    IntVec3 target = new IntVec3();
                    CellFinder.TryFindRandomEdgeCellWith((IntVec3 x) => x.Standable(map) && x.InBounds(map), map, CellFinder.EdgeRoadChance_Hostile, out target);
                    RKU_TunnelHiveSpawner tunnelHiveSpawner = (RKU_TunnelHiveSpawner)ThingMaker.MakeThing(DefOfs.RKU_TunnelHiveSpawner);
                    tunnelHiveSpawner.hitPoints = (caravan as RKU_DrillingVehicleOnMap).hitPoints;  // 传递耐久
                    tunnelHiveSpawner.canMove = false;//遇敌地图不能移动

                    List<Pawn> pawnsToTransfer2 = new List<Pawn>(caravan.pawns);
                    foreach (Pawn pawn in pawnsToTransfer2)
                    {
                        if (!pawn.Destroyed)
                        {
                            tunnelHiveSpawner.GetDirectlyHeldThings().TryAddOrTransfer(pawn);
                        }
                    }
                    GenSpawn.Spawn(tunnelHiveSpawner, target, map, WipeMode.Vanish);
                    CameraJumper.TryJump(tunnelHiveSpawner);
                    map.fogGrid.FloodUnfogAdjacent(target, false);
                    caravan.Destroy();
                }
                else if (modExtension != null &&! modExtension.isEncounterMap) {

                    CameraJumper.TryJump(map.Center, map);
                    IntVec3 target = new IntVec3();
                    CellFinder.TryFindRandomEdgeCellWith((IntVec3 x) => x.Standable(map) && x.InBounds(map), map, CellFinder.EdgeRoadChance_Hostile, out target);
                    RKU_TunnelHiveSpawner tunnelHiveSpawner = (RKU_TunnelHiveSpawner)ThingMaker.MakeThing(DefOfs.RKU_TunnelHiveSpawner);
                    tunnelHiveSpawner.hitPoints = (caravan as RKU_DrillingVehicleOnMap).hitPoints;  // 传递耐久
                    tunnelHiveSpawner.canMove = true;//资源地图可以移动

                    List<Pawn> pawnsToTransfer2 = new List<Pawn>(caravan.pawns);

                    foreach (Pawn pawn in pawnsToTransfer2)
                    {
                        if (!pawn.Destroyed)
                        {
                            tunnelHiveSpawner.GetDirectlyHeldThings().TryAddOrTransfer(pawn);
                        }
                    }
                    GenSpawn.Spawn(tunnelHiveSpawner, target, map, WipeMode.Vanish);
                    CameraJumper.TryJump(tunnelHiveSpawner);
                    map.fogGrid.FloodUnfogAdjacent(target, false);
                    caravan.Destroy();
                }
                else
                {
                    Messages.Message(new Message("RKU.ChooseDrillingVehicleSpot".Translate(), MessageTypeDefOf.PositiveEvent));
                    CameraJumper.TryJump(map.Center, map);
                    Find.Targeter.BeginTargeting(
                        new TargetingParameters
                        {
                            canTargetLocations = true,
                            canTargetPawns = false,
                            canTargetBuildings = false,
                            canTargetItems = false,
                            canTargetFires = false,
                            canTargetSelf = false,
                            canTargetAnimals = false,
                            canTargetHumans = false,
                            canTargetMechs = false,
                            canTargetPlants = false,
                            canTargetCorpses = false,
                            mustBeSelectable = true,
                            validator = (target) => target.Cell.Standable(map) && target.Cell.InBounds(map)
                        },
                        delegate (LocalTargetInfo target)
                        {
                            Func<Pawn, IntVec3> newSpawnCellGetter = (pawn) => target.Cell;
                            CaravanEnterMapUtility.Enter(caravan, map, newSpawnCellGetter, dropInventoryMode, draftColonists);

                            RKU_TunnelHiveSpawner tunnelHiveSpawner = (RKU_TunnelHiveSpawner)ThingMaker.MakeThing(DefOfs.RKU_TunnelHiveSpawner);
                            tunnelHiveSpawner.hitPoints = (caravan as RKU_DrillingVehicleOnMap).hitPoints;  // 传递耐久
                            List<Pawn> pawnsToTransfer2 = new List<Pawn>(caravan.pawns);

                            foreach (Pawn pawn in pawnsToTransfer2)
                            {
                                if (!pawn.Destroyed)
                                {
                                    tunnelHiveSpawner.GetDirectlyHeldThings().TryAddOrTransfer(pawn);
                                }
                            }
                            GenSpawn.Spawn(tunnelHiveSpawner, target.Cell, map, WipeMode.Vanish);
                            tunnelHiveSpawner.canMove = false;//敌对据点不能移动
                            CameraJumper.TryJump(tunnelHiveSpawner);
                            map.fogGrid.FloodUnfogAdjacent(target.Cell, false);
                            caravan.Destroy();
                            Find.Targeter.StopTargeting();
                        },
                        null,
                        null,
                        null
                    );

                    return false;
                }
            }
            return true;
        }
    }
}