using RimWorld;
using System;
using System.Collections.Generic;
using Verse;
using Verse.Noise;
using UnityEngine;

namespace RatkinUnderground
{
    public class RKU_GenStep_CavesTerrain : GenStep
    {
        private const float NOISE_SCALE = 0.05f;
        private const float NOISE_STRENGTH = 0.1f;
        private const int SCAN_RADIUS = 3;

        private static ModuleBase fertilityNoise;

        public override int SeedPart => 262606459;

        public override void Generate(Map map, GenStepParams parms)
        {
            // 初始化柏林噪声
            fertilityNoise = new Perlin(NOISE_SCALE, 2.0, 0.5, 6, Rand.Range(0, int.MaxValue), QualityMode.High);

            MapGenFloatGrid caves = MapGenerator.Caves;
            TerrainGrid terrainGrid = map.terrainGrid;

            // 用于存储已处理的洞穴格子
            HashSet<IntVec3> processedCells = new HashSet<IntVec3>();
            
            // 为整个地图随机选择一种水体附近的地形（沙地或泥土）
            TerrainDef waterTerrain = Rand.Value < 0.5f ? TerrainDefOf.Sand : TerrainDefOf.Soil;
            
            // 遍历所有单元格
            foreach (IntVec3 cell in map.AllCells)
            {
                // 如果这个格子是洞穴且未被处理
                if (caves[cell] > 0f && !processedCells.Contains(cell))
                {
                    // 获取这个洞穴区域的所有格子
                    List<IntVec3> caveRegion = new List<IntVec3>();
                    float totalFertility = 0f;
                    
                    // 使用队列进行广度优先搜索，找出所有相连的洞穴格子
                    Queue<IntVec3> cellsToCheck = new Queue<IntVec3>();
                    cellsToCheck.Enqueue(cell);
                    processedCells.Add(cell);
                    
                    while (cellsToCheck.Count > 0)
                    {
                        IntVec3 currentCell = cellsToCheck.Dequeue();
                        caveRegion.Add(currentCell);
                        totalFertility += CalculateCustomFertility(currentCell, map);
                        
                        // 检查相邻的8个格子
                        foreach (IntVec3 adjacentCell in GenAdj.CellsAdjacent8Way(new TargetInfo(currentCell, map)))
                        {
                            if (adjacentCell.InBounds(map) && 
                                caves[adjacentCell] > 0f && 
                                !processedCells.Contains(adjacentCell))
                            {
                                cellsToCheck.Enqueue(adjacentCell);
                                processedCells.Add(adjacentCell);
                            }
                        }
                    }
                    
                    // 计算这个洞穴区域的平均肥力
                    float averageFertility = totalFertility / caveRegion.Count;
                    
                    // 根据平均肥力选择基础地形
                    TerrainDef baseTerrainDef = TerrainFrom(cell, map, averageFertility, preferSolid: true);
                    
                    // 为整个洞穴区域设置地形，但保留水体
                    foreach (IntVec3 regionCell in caveRegion)
                    {
                        TerrainDef currentTerrain = regionCell.GetTerrain(map);
                        if (currentTerrain != TerrainDefOf.WaterDeep && currentTerrain != TerrainDefOf.WaterShallow)
                        {
                            // 检查是否在水体2格范围内
                            bool nearWater = false;
                            foreach (IntVec3 adj in GenRadial.RadialCellsAround(regionCell, 2, true))
                            {
                                if (adj.InBounds(map))
                                {
                                    TerrainDef t = adj.GetTerrain(map);
                                    if (t == TerrainDefOf.WaterDeep || t == TerrainDefOf.WaterShallow)
                                    {
                                        nearWater = true;
                                        break;
                                    }
                                }
                            }

                            // 如果在水体2格范围内，使用统一的水体附近地形
                            if (nearWater)
                            {
                                terrainGrid.SetTerrain(regionCell, waterTerrain);
                            }
                            else
                            {
                                terrainGrid.SetTerrain(regionCell, baseTerrainDef);
                            }
                        }
                    }
                }
                else if (caves[cell] <= 0f) // 非洞穴格子
                {
                    TerrainDef currentTerrain = cell.GetTerrain(map);
                    if (currentTerrain != TerrainDefOf.WaterDeep && currentTerrain != TerrainDefOf.WaterShallow)
                    {
                        float fertility = CalculateCustomFertility(cell, map);
                        TerrainDef terrainDef = TerrainFrom(cell, map, fertility, preferSolid: false);
                        terrainGrid.SetTerrain(cell, terrainDef);
                    }
                }
            }
        }

        private float CalculateCustomFertility(IntVec3 cell, Map map)
        {
            float fertility = 0.5f; // 基础肥力值

            // 添加柏林噪声
            float noiseValue = (float)fertilityNoise.GetValue(cell.x, 0, cell.z);
            fertility += noiseValue * NOISE_STRENGTH;

            // 确保肥力值在0-1之间
            return Mathf.Clamp01(fertility);
        }

        private TerrainDef TerrainFrom(IntVec3 c, Map map, float fertility, bool preferSolid)
        {
            // 洞穴格子
            if (preferSolid)
            {
                float noise = (float)fertilityNoise.GetValue(c.x, 0, c.z);
                float value = Rand.Value + noise * 0.5f + fertility * 0.5f;
                if (value < 0.8f)
                    return GenStep_RocksFromGrid.RockDefAt(c).building.naturalTerrain;
                else
                    return TerrainDefOf.Gravel;
            }

            // 非洞穴格子，按原逻辑
            if (fertility >= 0.6f)
                return TerrainDefOf.SoilRich;
            else if (fertility >= 0.3f)
                return TerrainDefOf.Soil;
            else if (fertility >= 0.2f)
                return TerrainDefOf.Gravel;
            return TerrainDefOf.Sand;
        }
    }
}
