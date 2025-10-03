using RimWorld;
using System;
using System.Collections.Generic;
using Verse;
using UnityEngine;
using UnityEngine.Analytics;

namespace RatkinUnderground
{
    public class RKU_GenStep_PlantsUnderground : GenStep
    {
        private const float CAVE_PLANT_DENSITY_FACTOR = 0.9f;
        private const float CHANCE_TO_SKIP = 0.001f;
        private const float MIN_FERTILITY_FOR_PLANTS = 0.1f;
        private const float CAVE_PLANT_GROWTH_RANGE_MIN = 0.15f;
        private const float CAVE_PLANT_GROWTH_RANGE_MAX = 1.5f;
        private const float PLANT_CLUSTER_RADIUS = 5f;
        private const float PLANT_CLUSTER_DENSITY = 0.7f;
        private const float CLUSTERS_PER_1000_CELLS = 0.001f;

        public override int SeedPart => 578415222;

        public override void Generate(Map map, GenStepParams parms)
        {
            if (map == null) return;

            // 获取当前植物密度和期望植物数量
            float currentPlantDensity = map.wildPlantSpawner.CurrentPlantDensity * CAVE_PLANT_DENSITY_FACTOR;
            float currentWholeMapNumDesiredPlants = map.wildPlantSpawner.CurrentWholeMapNumDesiredPlants;

            // 创建植物集群中心点
            List<IntVec3> clusterCenters = GenerateClusterCenters(map);

            // 使用随机排序的单元格列表
            List<IntVec3> cellsInRandomOrder = map.cellsInRandomOrder.GetAll();
            int totalCells = cellsInRandomOrder.Count;

            // 遍历地图上的每个单元格
            for (int i = 0; i < totalCells; i++)
            {
                IntVec3 cell = cellsInRandomOrder[i];
                
                if (Rand.Chance(CHANCE_TO_SKIP)) continue;

                // 检查是否适合生成植物
                if (IsSuitableForPlant(cell, map))
                {
                    // 计算该位置的植物密度修正
                    float densityModifier = CalculateDensityModifier(cell, clusterCenters, map);
                    float adjustedDensity = currentPlantDensity * densityModifier;

                    // 尝试生成植物
                    if (Rand.Chance(adjustedDensity))
                    {
                        TrySpawnPlant(cell, map, currentPlantDensity, currentWholeMapNumDesiredPlants);
                    }
                }
            }
        }

        private List<IntVec3> GenerateClusterCenters(Map map)
        {
            List<IntVec3> centers = new List<IntVec3>();
            int numClusters = Mathf.RoundToInt(map.Area * CLUSTERS_PER_1000_CELLS);
            
            // 使用随机排序的单元格列表来生成集群中心
            List<IntVec3> cellsInRandomOrder = map.cellsInRandomOrder.GetAll();
            int totalCells = cellsInRandomOrder.Count;
            int cellsChecked = 0;

            while (centers.Count < numClusters && cellsChecked < totalCells)
            {
                IntVec3 cell = cellsInRandomOrder[cellsChecked++];
                if (IsSuitableForClusterCenter(cell, map))
                {
                    centers.Add(cell);
                }
            }

            return centers;
        }

        private bool IsSuitableForClusterCenter(IntVec3 cell, Map map)
        {
            return cell.GetRoof(map)?.isNatural ?? false && 
                   map.fertilityGrid.FertilityAt(cell) > MIN_FERTILITY_FOR_PLANTS;
        }

        private bool IsSuitableForPlant(IntVec3 cell, Map map)
        {
            return cell.GetRoof(map)?.isNatural ?? false && 
                   map.fertilityGrid.FertilityAt(cell) > MIN_FERTILITY_FOR_PLANTS &&
                   cell.GetPlant(map) == null &&
                   cell.GetCover(map) == null &&
                   cell.GetEdifice(map) == null &&
                   PlantUtility.SnowAllowsPlanting(cell, map);
        }

        private float CalculateDensityModifier(IntVec3 cell, List<IntVec3> clusterCenters, Map map)
        {
            float maxModifier = 0f;
            foreach (IntVec3 center in clusterCenters)
            {
                float distance = cell.DistanceTo(center);
                if (distance <= PLANT_CLUSTER_RADIUS)
                {
                    float modifier = 1f - (distance / PLANT_CLUSTER_RADIUS);
                    maxModifier = Mathf.Max(maxModifier, modifier);
                }
            }

            return Mathf.Lerp(0.3f, 1f, maxModifier * PLANT_CLUSTER_DENSITY);
        }

        private void TrySpawnPlant(IntVec3 cell, Map map, float plantDensity, float wholeMapNumDesiredPlants)
        {
            if (map.wildPlantSpawner.CheckSpawnWildPlantAt(cell, plantDensity, wholeMapNumDesiredPlants, setRandomGrowth: true))
            {
                Plant plant = cell.GetPlant(map);
                if (Rand.Range(0, 100) <= 2)
                {
                    cell.GetPlant(map).Destroy();
                    //生成一种特殊蘑菇
                    ThingDef centerCap;
                    int randomValue = Rand.Range(0, 100);
                    if (randomValue > 75)
                    {
                        centerCap = ThingDef.Named("RKU_Hatecap");
                    }
                    else if (randomValue > 50)
                    {
                        centerCap = ThingDef.Named("RKU_Mindspark");
                    }
                    else if (randomValue > 25)
                    {
                        centerCap = ThingDef.Named("RKU_Allurecap");
                    }
                    else
                    {
                        centerCap = ThingDef.Named("RKU_Singularitycap");
                    }
                    plant = (Plant)GenSpawn.Spawn(centerCap, cell, map);
                    plant.Growth = Rand.Range(plant.def.plant.harvestMinGrowth, 1f);
                }
                if (plant != null)
                {
                    // 设置地下植物特有的生长状态
                    plant.Growth = Mathf.Clamp01(Rand.Range(CAVE_PLANT_GROWTH_RANGE_MIN, CAVE_PLANT_GROWTH_RANGE_MAX));
                    if (plant.def.plant.LimitedLifespan)
                    {
                        plant.Age = Rand.Range(0, Mathf.Max(plant.def.plant.LifespanTicks - 50, 0));
                    }
                }
            }
        }
    }
}