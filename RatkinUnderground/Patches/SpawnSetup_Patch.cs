using HarmonyLib;
using RatkinUnderground;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

[HarmonyPatch(typeof(CaravanEnterMapUtility), nameof(CaravanEnterMapUtility.Enter),
    new Type[] { typeof(Caravan), typeof(Map), typeof(Func<Pawn, IntVec3>), typeof(CaravanDropInventoryMode), typeof(bool) })]
public static class Patch_CaravanEnter
{
    static bool Prefix(Caravan caravan, Map map, Func<Pawn, IntVec3> spawnCellGetter,
                       CaravanDropInventoryMode dropInventoryMode, bool draftColonists)
    {
        var modExtension = map.generatorDef.GetModExtension<RKU_MapGeneratorDefModExtension>();
        if (modExtension != null)
        {
            return true;
        }
        if (caravan is RKU_DrillingVehicleOnMap rkuCaravan)
        {
            // 获取生成点
            IntVec3 spawnPos = spawnCellGetter(caravan.PawnsListForReading.FirstOrDefault());

            // 生成钻机建筑
            var vehicle = (RKU_TunnelHiveSpawner)ThingMaker.MakeThing(DefOfs.RKU_TunnelHiveSpawner);
            vehicle.hitPoints = rkuCaravan.hitPoints;
            vehicle.faction = rkuCaravan.Faction;

            // 所有pawn移动到钻机
            foreach (var p in caravan.PawnsListForReading.ToList())
            {
                vehicle.GetDirectlyHeldThings().TryAddOrTransfer(p);
                rkuCaravan.RemovePawn(p);
            }
            GenSpawn.Spawn(vehicle, spawnPos, map);
            // 移除 caravan
            if (!caravan.Destroyed)
                caravan.Destroy();

            return false;
        }

        return true;
    }
}