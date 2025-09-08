using HarmonyLib;
using RatkinUnderground;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic; cf
using System.IO;
using System.Linq;
using Verse;

[HarmonyPatch(typeof(CaravanEnterMapUtility), nameof(CaravanEnterMapUtility.Enter),
    new Type[] { typeof(Caravan), typeof(Map), typeof(Func<Pawn, IntVec3>), typeof(CaravanDropInventoryMode), typeof(bool) })]
public static class Patch_CaravanEnter
{
    static bool Prefix(Caravan caravan, Map map, Func<Pawn, IntVec3> spawnCellGetter,
                       CaravanDropInventoryMode dropInventoryMode, bool draftColonists)
    {
        if (caravan is RKU_DrillingVehicleOnMap rkuCaravan)
        {
            Log.Message($"[RKU] Caravan {caravan.Label} 捕获，准备替换为 RKU_DrillingVehicle。");

            if (map == null)
            {
                Log.Error("[RKU] 目标 map 为 null");
                return true;
            }

            // 移动中触发的事件
            if (!rkuCaravan.IsArrived()
                && rkuCaravan.pather.MovingNow)
            {
                return true;
            }
            
            CameraJumper.TryJump(map.Center, map);

            Find.Targeter.BeginTargeting(
                new TargetingParameters
                {
                    canTargetLocations = true,
                    canTargetSelf = false,
                    canTargetPawns = false,
                    canTargetBuildings = false
                },
                delegate (LocalTargetInfo target)
                {
                    try
                    {
                        // 获取生成点
                        // IntVec3 spawnPos = spawnCellGetter(caravan.PawnsListForReading.FirstOrDefault());
                        IntVec3 spawnPos = target.Cell;
                        Log.Message($"选择位置:{target.Cell}");
                        // 生成钻机建筑
                        var vehicle = (RKU_TunnelHiveSpawner)ThingMaker.MakeThing(DefOfs.RKU_TunnelHiveSpawner);
                        vehicle.hitPoints = rkuCaravan.hitPoints;
                        vehicle.faction = rkuCaravan.Faction;

                        // 所有pawn移动到钻机
                        foreach (var p in caravan.PawnsListForReading.ToList())
                        {
                            try
                            {
                                vehicle.GetDirectlyHeldThings().TryAddOrTransfer(p);
                                rkuCaravan.RemovePawn(p);
                            }
                            catch (Exception e)
                            {
                                Log.Message($"遍历报错：{e}");
                            }
                            
                        }
                        GenSpawn.Spawn(vehicle, spawnPos, map);
                        // 移除 caravan
                        if (!caravan.Destroyed)
                            caravan.Destroy();
                    }
                    catch (Exception e)
                    {
                        Log.Message($"delegate报错：{e}");
                    }
                },
                null,
                (LocalTargetInfo target) =>
                {
                    try
                    {
                        if (!target.IsValid) return false;
                        if (!target.Cell.InBounds(map)) return false;
                        if (!target.Cell.Standable(map)) return false;
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                }
                );

            return false;
        }

        return true;
    }
}