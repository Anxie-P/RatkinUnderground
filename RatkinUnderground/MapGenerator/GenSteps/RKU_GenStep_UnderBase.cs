using RimWorld;
using System;
using System.Collections.Generic;
using Verse;
using UnityEngine;

namespace RatkinUnderground
{
    public class RKU_GenStep_UnderBase : GenStep
    {
        public override int SeedPart => 8524965;

        public const int BUILDINGSNUM = 3;
        public const int MIN_RADIUS = 5;
        public const int MAX_RADIUS = 7;
        public const int MIN_DISTANCE = 5; // 建筑之间的最小距离

        public List<IntVec3> buildingCenters = new List<IntVec3>();
        public IntVec3 exitPoint;
        public TerrainDef pathDef;
        public TerrainDef floorDef;
        public ThingDef wallDef;

        public override void Generate(Map map, GenStepParams parms)
        {
            if (map == null) return;
            if (pathDef == null || wallDef == null)
            {
                Log.Error("[RKU_GenStep_UnderBase] Failed to find required TerrainDef or ThingDef");
                return;
            }

            // 选择出口点（在地图边缘）
            exitPoint = GetRandomEdgePoint(map);
            // 生成建筑中心点
            GenerateBuildingCenters(map);
            // 清除连接路径区域
            ClearPathAreas(map);
            // 清除出口隧道区域
            ClearExitTunnelArea(map);
            // 生成圆形建筑
            foreach (IntVec3 center in buildingCenters)
            {
                GenerateCircularBuilding(map, center);
            }
            // 生成连接路径
            GenerateConnectingPaths(map);
            // 生成出口隧道
            GenerateExitTunnel(map);
        }

        private IntVec3 GetRandomEdgePoint(Map map)
        {
            int edge = Rand.Range(0, 4); // 0:上, 1:右, 2:下, 3:左
            IntVec3 point = new IntVec3();

            switch (edge)
            {
                case 0: // 上边
                    point = new IntVec3(Rand.Range(0, map.Size.x), 0, map.Size.z - 1);
                    break;
                case 1: // 右边
                    point = new IntVec3(map.Size.x - 1, 0, Rand.Range(0, map.Size.z));
                    break;
                case 2: // 下边
                    point = new IntVec3(Rand.Range(0, map.Size.x), 0, 0);
                    break;
                case 3: // 左边
                    point = new IntVec3(0, 0, Rand.Range(0, map.Size.z));
                    break;
            }

            return point;
        }

        private void GenerateBuildingCenters(Map map)
        {
            int buildingCount = BUILDINGSNUM;
            int attempts = 0;
            int maxAttempts = 100;

            while (buildingCenters.Count < buildingCount && attempts < maxAttempts)
            {
                IntVec3 potentialCenter = new IntVec3(
                    Rand.Range(30, map.Size.x - 30),
                    0,
                    Rand.Range(30, map.Size.z - 30)
                );

                bool tooClose = false;
                foreach (IntVec3 existingCenter in buildingCenters)
                {
                    if (potentialCenter.DistanceTo(existingCenter) < MIN_DISTANCE)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose)
                {
                    buildingCenters.Add(potentialCenter);
                }

                attempts++;
            }
        }

        private void GenerateCircularBuilding(Map map, IntVec3 center)
        {
            int radius = Rand.Range(MIN_RADIUS, MAX_RADIUS + 1);

            for (int x = -radius; x <= radius; x++)
            {
                for (int z = -radius; z <= radius; z++)
                {
                    IntVec3 cell = new IntVec3(center.x + x, 0, center.z + z);
                    if (cell.InBounds(map))
                    {
                        float distance = Mathf.Sqrt(x * x + z * z);
                        if (distance < radius)
                        {
                            try
                            {
                                // 安全地清除所有物品和建筑
                                Building edifice = cell.GetEdifice(map);
                                if (edifice != null)
                                {
                                    edifice.Destroy(DestroyMode.Vanish);
                                }
                                List<Thing> things = new List<Thing>(cell.GetThingList(map));
                                foreach (Thing thing in things)
                                {
                                    if (thing != null && !thing.Destroyed)
                                    {
                                        thing.Destroy();
                                    }
                                }
                                // 生成地板
                                map.terrainGrid.SetUnderTerrain(cell, TerrainDefOf.Sand);
                                map.terrainGrid.SetTerrain(cell, floorDef);
                                // 如果是边缘，生成墙
                                if (distance >= radius - 1)
                                {
                                    GenPlace.TryPlaceThing(ThingMaker.MakeThing(ThingDefOf.Wall, DefDatabase<ThingDef>.GetNamed("BlocksMarble")), cell, map, ThingPlaceMode.Direct);
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Error($"[RKU_GenStep_UnderBase] Error generating building at {cell}: {ex.Message}");
                            }
                        }
                    }
                }
            }
        }

        private void GenerateConnectingPaths(Map map)
        {
            // 连接所有建筑中心点
            for (int i = 0; i < buildingCenters.Count - 1; i++)
            {
                GeneratePath(map, buildingCenters[i], buildingCenters[i + 1]);
            }

            // 连接最后一个建筑到第一个建筑，形成环路
            GeneratePath(map, buildingCenters[buildingCenters.Count - 1], buildingCenters[0]);
        }

        private void GeneratePath(Map map, IntVec3 start, IntVec3 end)
        {
            IntVec3 current = start;
            while (current != end)
            {
                try
                {
                    // 生成地板
                    map.terrainGrid.SetTerrain(current, pathDef);

                    // 移动到下一个点
                    if (Math.Abs(current.x - end.x) > Math.Abs(current.z - end.z))
                    {
                        current.x += Math.Sign(end.x - current.x);
                    }
                    else
                    {
                        current.z += Math.Sign(end.z - current.z);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[RKU_GenStep_UnderBase] Error generating path at {current}: {ex.Message}");
                    break;
                }
            }
        }

        private void GenerateExitTunnel(Map map)
        {
            // 找到最近的建筑中心点
            IntVec3 nearestBuilding = buildingCenters[0];
            float minDistance = float.MaxValue;

            foreach (IntVec3 center in buildingCenters)
            {
                float distance = center.DistanceTo(exitPoint);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestBuilding = center;
                }
            }

            // 生成通往出口的隧道
            GeneratePath(map, nearestBuilding, exitPoint);
        }

        private void ClearPathAreas(Map map)
        {
            // 清除所有建筑之间的路径区域
            for (int i = 0; i < buildingCenters.Count - 1; i++)
            {
                ClearPathArea(map, buildingCenters[i], buildingCenters[i + 1]);
            }
            // 清除最后一个建筑到第一个建筑的路径
            ClearPathArea(map, buildingCenters[buildingCenters.Count - 1], buildingCenters[0]);
        }

        private void ClearPathArea(Map map, IntVec3 start, IntVec3 end)
        {
            IntVec3 current = start;
            while (current != end)
            {
                // 清除当前格子和周围一格
                for (int x = -1; x <= 1; x++)
                {
                    for (int z = -1; z <= 1; z++)
                    {
                        IntVec3 clearCell = new IntVec3(current.x + x, 0, current.z + z);
                        if (clearCell.InBounds(map))
                        {
                            // 安全地清除所有物品和建筑
                            List<Thing> things = new List<Thing>(clearCell.GetThingList(map));
                            foreach (Thing thing in things)
                            {
                                if (thing != null && !thing.Destroyed)
                                {
                                    thing.Destroy();
                                }
                            }
                        }
                    }
                }

                // 移动到下一个点
                if (Math.Abs(current.x - end.x) > Math.Abs(current.z - end.z))
                {
                    current.x += Math.Sign(end.x - current.x);
                }
                else
                {
                    current.z += Math.Sign(end.z - current.z);
                }
            }
        }

        private void ClearExitTunnelArea(Map map)
        {
            // 找到最近的建筑中心点
            IntVec3 nearestBuilding = buildingCenters[0];
            float minDistance = float.MaxValue;

            foreach (IntVec3 center in buildingCenters)
            {
                float distance = center.DistanceTo(exitPoint);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestBuilding = center;
                }
            }
            // 清除出口隧道区域
            ClearPathArea(map, nearestBuilding, exitPoint);
        }
    }
}
