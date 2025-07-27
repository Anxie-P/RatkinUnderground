using RimWorld;
using System;
using System.Collections.Generic;
using Verse;
using Verse.Noise;
using UnityEngine;

namespace RatkinUnderground
{
    public class RKU_GenStep_FairyRing : GenStep
    {
        public override int SeedPart => 446845;

        public override void Generate(Map map, GenStepParams parms)
        {
            map.Biome.plantDensity = 0.7f;
            if (map == null) return;

            // 遍历所有单元格
            foreach (IntVec3 cell in map.AllCells)
            {
                // 检查是否是水体
                TerrainDef currentTerrain = cell.GetTerrain(map);
                if (currentTerrain == TerrainDefOf.WaterShallow)
                {
                    // 清除当前格子
                    ClearCell(map, cell);
                    // 替换为普通泥土
                    map.terrainGrid.SetTerrain(cell, TerrainDefOf.Soil);
                }
                if (currentTerrain == TerrainDefOf.WaterDeep)
                {
                    // 清除当前格子
                    ClearCell(map, cell);
                    // 替换为普通泥土
                    map.terrainGrid.SetTerrain(cell, TerrainDefOf.SoilRich);
                }
            }

            // 在地图中心生成蘑菇圈
            IntVec3 center = new IntVec3(map.Size.x / 2, 0, map.Size.z / 2);
            GenerateFairyRing(map, center, 8);
            GenerateFairyRing(map, center, 5);
            ClearAreaAroundRings(map, center, 11);

            //中心生成一种特殊蘑菇*1
            ThingDef centerCap = Rand.Range(0, 100) > 50 ? ThingDef.Named("RKU_Hatecap") : ThingDef.Named("RKU_Mindspark");
            Plant plant = (Plant)GenSpawn.Spawn(centerCap, center, map);
        }
        private void ClearCell(Map map, IntVec3 cell)
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
        }

        private void GenerateFairyRing(Map map, IntVec3 center, int radius)
        {
            // 蘑菇圈的大小
            int mushroomCount = Rand.Range(20, 40);

            // 生成一圈蘑菇
            for (int i = 0; i < mushroomCount; i++)
            {
                float angle = (float)i / mushroomCount * 2f * Mathf.PI;
                int x = Mathf.RoundToInt(center.x + radius * Mathf.Cos(angle));
                int z = Mathf.RoundToInt(center.z + radius * Mathf.Sin(angle));
                
                IntVec3 cell = new IntVec3(x, 0, z);
                if (cell.InBounds(map))
                {
                    // 清除当前格子
                    ClearCell(map, cell);
                    
                    // 设置地形为肥沃泥土
                    map.terrainGrid.SetTerrain(cell, TerrainDefOf.SoilRich);

                    // 生成蘑菇
                    ThingDef mushroomDef = GetRandomMushRoomKinds();
                    if (mushroomDef != null)
                    {
                        Plant plant = (Plant)GenSpawn.Spawn(mushroomDef, cell, map);
                        if (plant != null)
                        {
                            plant.Growth = 1f;
                        }
                    }
                }
            }
        }


        /// <summary>
        /// 清空外圈外3格范围内的内容
        /// </summary>
        /// <param name="map"></param>
        /// <param name="center"></param>
        /// <param name="outerRadius"></param>
        private void ClearAreaAroundRings(Map map, IntVec3 center, int outerRadius)
        {
            int clearRadius = outerRadius + 3; // 外圈半径 + 3格
            
            for (int x = -clearRadius; x <= clearRadius; x++)
            {
                for (int z = -clearRadius; z <= clearRadius; z++)
                {
                    IntVec3 cell = new IntVec3(center.x + x, 0, center.z + z);
                    if (cell.InBounds(map))
                    {
                        float distance = Mathf.Sqrt(x * x + z * z);
                        if (distance > outerRadius && distance <= clearRadius)
                        {
                            ClearCell(map, cell);
                            map.terrainGrid.SetTerrain(cell, TerrainDefOf.Soil);
                        }
                    }
                }
            }
        }
        /// <summary>
        /// 就，蘑菇生成有权重，不好看的小蘑菇少整点
        /// </summary>
        /// <returns></returns>
        private ThingDef GetRandomMushRoomKinds()
        {
            List<ThingDef> kinds = new List<ThingDef>
            {
                ThingDef.Named("Plant_Devilstrand"),
                ThingDef.Named("Agarilux"),
                ThingDef.Named("Glowstool"),
                ThingDef.Named("Plant_Devilstrand"),
                ThingDef.Named("Glowstool")
            };

            if (ModLister.CheckIdeology(""))
            {
                kinds.Add(ThingDef.Named("Plant_Timbershroom"));
                kinds.Add(ThingDef.Named("Plant_Timbershroom"));
                kinds.Add(ThingDef.Named("Plant_Nutrifungus"));
            }

            return kinds.RandomElement();
        }
    }
}
