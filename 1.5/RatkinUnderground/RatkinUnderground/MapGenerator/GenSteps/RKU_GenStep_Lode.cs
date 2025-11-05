using RimWorld;
using System;
using System.Collections.Generic;
using Verse;
using Verse.Noise;
using UnityEngine;

namespace RatkinUnderground
{
    public class RKU_GenStep_Lode : GenStep
    {
        public override int SeedPart => 446845;
        public List<ThingDef> possibleMinerals = new List<ThingDef>();
        public List<ThingDef> centerMinerals = new List<ThingDef>();

        public override void Generate(Map map, GenStepParams parms)
        {
            map.Biome.plantDensity = 0.7f;
            if (map == null) return;

            // 随机选择一种矿石
            ThingDef selectedMineral = possibleMinerals.RandomElement();
            if (selectedMineral == null) return;

            // 获取地图中心
            IntVec3 center = new IntVec3(map.Size.x / 2, 0, map.Size.z / 2);

            // 遍历所有单元格
            foreach (IntVec3 cell in map.AllCells)
            {
                // 检查是否是水体
                TerrainDef currentTerrain = cell.GetTerrain(map);
                if (currentTerrain == TerrainDefOf.WaterDeep || currentTerrain == TerrainDefOf.WaterShallow)
                {
                    // 清除当前格子
                    ClearCell(map, cell);
                    map.terrainGrid.SetTerrain(cell, GenStep_RocksFromGrid.RockDefAt(cell).building.naturalTerrain);

                    // 检查是否在地图边缘20格内
                    if (cell.x < 20 || cell.x > map.Size.x - 20 || cell.z < 20 || cell.z > map.Size.z - 20)
                        continue;

                    int distance = Mathf.Abs(cell.x - center.x) + Math.Abs(cell.z - center.z);
                    if (distance <= 3)
                    {
                        if (centerMinerals.Count > 0)
                        {
                            ThingDef centerMineral = centerMinerals.RandomElement();
                            Thing mineral = ThingMaker.MakeThing(centerMineral);
                            GenSpawn.Spawn(mineral, cell, map);
                            continue;
                        }
                    }

                    // 根据水深决定生成规则
                    if (currentTerrain == TerrainDefOf.WaterShallow)
                    {
                        // 浅水区70%几率生成
                        if (Rand.Value < 0.7f)
                        {
                            Thing mineral = ThingMaker.MakeThing(selectedMineral);
                            GenSpawn.Spawn(mineral, cell, map);
                        }
                    }
                    else // 深水区
                    {
                        // 检查周围是否有已生成的矿物
                        bool hasAdjacentMineral = false;
                        foreach (IntVec3 adjCell in GenAdj.CardinalDirections)
                        {
                            IntVec3 checkCell = cell + adjCell;
                            if (checkCell.InBounds(map))
                            {
                                Thing mineral = checkCell.GetFirstThing<Thing>(map);
                                if (mineral != null && mineral.def == selectedMineral)
                                {
                                    hasAdjacentMineral = true;
                                    break;
                                }
                            }
                        }

                        // 如果周围有矿物，30%几率生成
                        if (hasAdjacentMineral && Rand.Value < 0.3f)
                        {
                            Thing mineral = ThingMaker.MakeThing(selectedMineral);
                            GenSpawn.Spawn(mineral, cell, map);
                        }
                    }
                }
            }
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
    }
}
