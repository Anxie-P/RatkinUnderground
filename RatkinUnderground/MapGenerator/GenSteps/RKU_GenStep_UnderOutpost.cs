using RimWorld;
using System;
using System.Collections.Generic;
using Verse;
using Verse.Noise;
using UnityEngine;
using System.Linq;
using RimWorld.BaseGen;

namespace RatkinUnderground
{
    public class RKU_GenStep_UnderOutpost : GenStep
    {
        private const int OUTPOST_SIZE = 24; // 前哨站大小
        private const int SHORE_DISTANCE = 3; // 湖岸距离改为4格

        public override int SeedPart => 446845;

        public override void Generate(Map map, GenStepParams parms)
        {
            if (map == null) return;

            // 找到湖的中心点
            IntVec3 lakeCenter = FindLakeCenter(map);
            if (lakeCenter == IntVec3.Invalid) return;

            // 从湖中心开始寻找可放置区域
            bool[,] visited = new bool[map.Size.x, map.Size.z];
            CellRect outpostRect = FindAreaFromLakeCenter(map, lakeCenter, visited);

            // 清除所有水体
            foreach (IntVec3 cell in map.AllCells)
            {
                if (cell.InBounds(map))
                {
                    TerrainDef currentTerrain = cell.GetTerrain(map);
                    if (currentTerrain == TerrainDefOf.WaterDeep || currentTerrain == TerrainDefOf.WaterShallow)
                    {
                        // 清除当前格子
                        ClearCell(map, cell);
                        
                        // 设置地形为混凝土
                        map.terrainGrid.SetTerrain(cell, TerrainDefOf.Concrete);
                    }
                }
            }

            // 标记区域内的所有格子
            MarkAreaCells(map, outpostRect, visited);

            // 在区域边界添加沙袋
            PlaceSandbagsOnBoundary(map, visited);

            // 在区域中心生成前哨站
            GenerateOutpost(map, outpostRect, parms);
        }

        private void MarkAreaCells(Map map, CellRect rect, bool[,] visited)
        {
            int markedCount = 0;
            for (int x = rect.minX; x <= rect.maxX; x++)
            {
                for (int z = rect.minZ; z <= rect.maxZ; z++)
                {
                    IntVec3 cell = new IntVec3(x, 0, z);
                    if (cell.InBounds(map))
                    {
                        TerrainDef currentTerrain = cell.GetTerrain(map);
                        bool isWater = currentTerrain == TerrainDefOf.WaterDeep || currentTerrain == TerrainDefOf.WaterShallow;
                        bool isNearShore = IsNearShore(map, cell);

                        // 标记不是水体且距离湖岸4格以内的格子
                        if (!isWater && isNearShore)
                        {
                            visited[x, z] = true;
                            markedCount++;
                        }
                    }
                }
            }
            Log.Message($"[RKU_UnderOutpost] Marked {markedCount} cells in area");
        }

        private void PlaceSandbagsOnBoundary(Map map, bool[,] visited)
        {
            int sandbagCount = 0;
            int boundaryCount = 0;
            for (int x = 0; x < map.Size.x; x++)
            {
                for (int z = 0; z < map.Size.z; z++)
                {
                    if (visited[x, z])
                    {
                        // 检查是否是边界（周围有未标记的格子）
                        bool isBoundary = false;
                        foreach (IntVec3 offset in GenAdj.AdjacentCells)
                        {
                            IntVec3 checkCell = new IntVec3(x, 0, z) + offset;
                            if (checkCell.InBounds(map) && !visited[checkCell.x, checkCell.z])
                            {
                                isBoundary = true;
                                boundaryCount++;
                                break;
                            }
                        }

                        if (isBoundary)
                        {
                            IntVec3 cell = new IntVec3(x, 0, z);
                            try
                            {
                                Thing sandbag = ThingMaker.MakeThing(ThingDefOf.Sandbags, ThingDefOf.Cloth);
                                if (GenPlace.TryPlaceThing(sandbag, cell, map, ThingPlaceMode.Direct))
                                {
                                    sandbagCount++;
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Error($"[RKU_UnderOutpost] Error placing sandbag at {cell}: {ex}");
                            }
                        }
                    }
                }
            }

            Log.Message($"[RKU_UnderOutpost] Found {boundaryCount} boundary cells, placed {sandbagCount} sandbags");
        }

        private void PrepareAreaForOutpost(Map map, CellRect rect)
        {
            foreach (IntVec3 cell in map.AllCells)
            {
                if (cell.InBounds(map))
                {
                    TerrainDef currentTerrain = cell.GetTerrain(map);
                    if (currentTerrain == TerrainDefOf.WaterDeep || currentTerrain == TerrainDefOf.WaterShallow)
                    {
                        // 清除当前格子
                        ClearCell(map, cell);
                        
                        // 设置地形为混凝土
                        map.terrainGrid.SetTerrain(cell, TerrainDefOf.Concrete);
                    }
                }
            }
        }

        private bool IsBuildableTerrain(TerrainDef terrain)
        {
            if (terrain == null) return false;

            // 检查地形是否支持重型建筑
            bool supportsHeavy = terrain.affordances.Contains(TerrainAffordanceDefOf.Heavy);
            
            // 检查地形是否可站立
            bool isStandable = terrain.passability != Traversability.Impassable;
            
            // 检查地形是否不是水体
            bool isNotWater = terrain != TerrainDefOf.WaterDeep && terrain != TerrainDefOf.WaterShallow;

            // 检查地形是否不是特殊地形
            bool isNotSpecial = terrain != TerrainDefOf.WoodPlankFloor;

            return supportsHeavy && isStandable && isNotWater && isNotSpecial;
        }

        private IntVec3 FindLakeCenter(Map map)
        {
            int totalX = 0;
            int totalZ = 0;
            int waterCount = 0;

            foreach (IntVec3 cell in map.AllCells)
            {
                TerrainDef currentTerrain = cell.GetTerrain(map);
                if (currentTerrain == TerrainDefOf.WaterDeep || currentTerrain == TerrainDefOf.WaterShallow)
                {
                    int neighborWater = 0;
                    foreach (IntVec3 offset in GenAdj.AdjacentCellsAndInside)
                    {
                        if (offset == IntVec3.Zero) continue;
                        IntVec3 check = cell + offset;
                        if (check.InBounds(map))
                        {
                            TerrainDef t = check.GetTerrain(map);
                            if (t == TerrainDefOf.WaterDeep || t == TerrainDefOf.WaterShallow)
                                neighborWater++;
                        }
                    }
                    // 只有大面积水体才算湖
                    if (neighborWater >= 6)
                    {
                        totalX += cell.x;
                        totalZ += cell.z;
                        waterCount++;
                    }
                }
            }

            if (waterCount == 0) return IntVec3.Invalid;
            return new IntVec3(totalX / waterCount, 0, totalZ / waterCount);
        }

        private CellRect FindAreaFromLakeCenter(Map map, IntVec3 lakeCenter, bool[,] visited)
        {
            int minX = lakeCenter.x;
            int maxX = lakeCenter.x;
            int minZ = lakeCenter.z;
            int maxZ = lakeCenter.z;

            Queue<IntVec3> cellsToCheck = new Queue<IntVec3>();
            cellsToCheck.Enqueue(lakeCenter);

            while (cellsToCheck.Count > 0)
            {
                IntVec3 cell = cellsToCheck.Dequeue();
                if (visited[cell.x, cell.z]) continue;

                // 检查是否是水体或距离湖岸太远
                TerrainDef currentTerrain = cell.GetTerrain(map);
                bool isWater = currentTerrain == TerrainDefOf.WaterDeep || currentTerrain == TerrainDefOf.WaterShallow;
                bool isNearShore = IsNearShore(map, cell);

                if (isWater || !isNearShore) continue;

                // 标记为已访问
                visited[cell.x, cell.z] = true;

                minX = Math.Min(minX, cell.x);
                maxX = Math.Max(maxX, cell.x);
                minZ = Math.Min(minZ, cell.z);
                maxZ = Math.Max(maxZ, cell.z);

                // 检查相邻的八个方向
                foreach (IntVec3 offset in GenAdj.AdjacentCells)
                {
                    IntVec3 newCell = cell + offset;
                    if (newCell.InBounds(map) && !visited[newCell.x, newCell.z])
                    {
                        cellsToCheck.Enqueue(newCell);
                    }
                }
            }

            Log.Message($"[RKU_UnderOutpost] Found area: minX={minX}, maxX={maxX}, minZ={minZ}, maxZ={maxZ}");

            return new CellRect(minX, minZ, maxX - minX + 1, maxZ - minZ + 1);
        }

        private bool IsNearShore(Map map, IntVec3 cell)
        {
            // 检查周围是否有水体
            for (int x = -SHORE_DISTANCE; x <= SHORE_DISTANCE; x++)
            {
                for (int z = -SHORE_DISTANCE; z <= SHORE_DISTANCE; z++)
                {
                    IntVec3 checkCell = new IntVec3(cell.x + x, 0, cell.z + z);
                    if (checkCell.InBounds(map))
                    {
                        TerrainDef terrain = checkCell.GetTerrain(map);
                        if (terrain == TerrainDefOf.WaterDeep || terrain == TerrainDefOf.WaterShallow)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private void GenerateOutpost(Map map, CellRect rect, GenStepParams parms)
        {
            // 获取派系
            Faction faction = (map.ParentFaction != null && map.ParentFaction != Faction.OfPlayer) 
                ? map.ParentFaction 
                : Find.FactionManager.RandomEnemyFaction();

            // 计算前哨站的实际位置（在区域中心）
            int outpostX = rect.minX + (rect.Width - OUTPOST_SIZE) / 2;
            int outpostZ = rect.minZ + (rect.Height - OUTPOST_SIZE) / 2;
            CellRect outpostRect = new CellRect(outpostX, outpostZ, OUTPOST_SIZE, OUTPOST_SIZE);

            // 设置生成参数
            ResolveParams resolveParams = new ResolveParams
            {
                rect = outpostRect,
                faction = faction,
                edgeDefenseWidth = 2,
                edgeDefenseTurretsCount = Rand.RangeInclusive(0, 1),
                edgeDefenseMortarsCount = 0,
                settlementDontGeneratePawns = false,
                allowGeneratingThronerooms = true,
            };

            // 设置全局参数
            BaseGen.globalSettings.map = map;
            BaseGen.globalSettings.minBuildings = 2;
            BaseGen.globalSettings.minBarracks = 1;
            BaseGen.globalSettings.maxFarms = -1;

            // 生成前哨站
            BaseGen.symbolStack.Push("settlement", resolveParams);
            BaseGen.Generate();
        }

        private void ClearCell(Map map, IntVec3 cell)
        {
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
        }
    }
}
