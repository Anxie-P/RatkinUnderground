using RimWorld;
using System;
using System.Collections.Generic;
using Verse;
using Verse.Noise;
using UnityEngine;

namespace RatkinUnderground
{
    public class RKU_GenStep_Worm : GenStep
    {
        private const float INSECT_SPAWN_CHANCE = 0.025f; // 虫子生成概率

        public override int SeedPart => 446845;

        public override void Generate(Map map, GenStepParams parms)
        {
            //设置植物密度
            map.Biome.plantDensity = 0.7f;
            if (map == null) return;

            // 遍历所有单元格
            foreach (IntVec3 cell in map.AllCells)
            {
                // 检查是否是水体
                TerrainDef currentTerrain = cell.GetTerrain(map);
                if (currentTerrain == TerrainDefOf.WaterDeep || currentTerrain == TerrainDefOf.WaterShallow)
                {
                    // 清除当前格子
                    ClearCell(map, cell);
                    // 替换为肥沃泥土
                    map.terrainGrid.SetTerrain(cell, TerrainDefOf.SoilRich);
                }
            }

            // 在地图中心生成虫巢
            IntVec3 center = new IntVec3(map.Size.x / 2, 0, map.Size.z / 2);
            GenerateWarmNest(map, center);

            // 随机生成虫子
            GenerateRandomInsects(map);
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

        private void GenerateWarmNest(Map map, IntVec3 center)
        {
            // 虫巢的大小
            int nestRadius = 3;

            // 生成虫巢主体
            for (int x = -nestRadius; x <= nestRadius; x++)
            {
                for (int z = -nestRadius; z <= nestRadius; z++)
                {
                    IntVec3 cell = new IntVec3(center.x + x, 0, center.z + z);
                    if (cell.InBounds(map))
                    {
                        // 清除当前格子
                        ClearCell(map, cell);

                        // 设置地形为肥沃泥土
                        map.terrainGrid.SetTerrain(cell, TerrainDefOf.SoilRich);

                        // 在中心区域生成虫巢建筑
                        if (Math.Abs(x) <= 2 && Math.Abs(z) <= 2)
                        {
                            // 在中心生成主虫巢
                            if (x == 0 && z == 0)
                            {
                                Hive obj = (Hive)GenSpawn.Spawn(ThingMaker.MakeThing(ThingDefOf.Hive), cell, map, WipeMode.FullRefund);
                            }
                        }
                        // 在周围生成小型虫巢
                        else if (Rand.Value < 0.3f)
                        {
                            Hive obj = (Hive)GenSpawn.Spawn(ThingMaker.MakeThing(ThingDefOf.Hive), cell, map, WipeMode.FullRefund);
                        }
                    }
                }
            }
        }

        private void GenerateRandomInsects(Map map)
        {
            // 遍历所有单元格
            foreach (IntVec3 cell in map.AllCells)
            {
                // 检查格子是否可以放置虫子
                if (cell.Standable(map) && cell.GetFirstPawn(map) == null)
                {
                    // 检查是否在地图边缘20格内
                    if (cell.x < 20 || cell.x >= map.Size.x - 20 || cell.z < 20 || cell.z >= map.Size.z - 20)
                    {
                        continue; // 跳过边缘区域
                    }
                    
                    if (Rand.Value < INSECT_SPAWN_CHANCE)
                    {
                        // 随机选择一种虫子
                        PawnKindDef insectKind = GetRandomInsectKind();
                        if (insectKind != null)
                        {
                            Pawn insect = PawnGenerator.GeneratePawn(insectKind, Faction.OfInsects);
                            GenSpawn.Spawn(insect, cell, map, WipeMode.Vanish);
                            
                        }
                    }
                }
            }
        }

        private PawnKindDef GetRandomInsectKind()
        {
            // 获取所有虫子类型的定义
            List<PawnKindDef> insectKinds = new List<PawnKindDef>
            {
                PawnKindDefOf.Megaspider,
                PawnKindDefOf.Spelopede,
                PawnKindDefOf.Megascarab,
            };

            // 随机返回一种虫子
            return insectKinds.RandomElement();
        }
    }
}
