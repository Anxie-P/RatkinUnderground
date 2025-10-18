using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.Noise;
using UnityEngine;

namespace RatkinUnderground
{
    public class RKU_GenStep_Lake : GenStep
    {
        public override int SeedPart => 446844;

        public float LAKE_CENTER_DEPTH = 20f; // 湖泊中心最大深度
        public float NOISE_SCALE = 0.05f; // 柏林噪声缩放
        public float RIVER_WIDTH = 3f; // 河流宽度
        public float LAKE_THRESHOLD = 0.4f; // 湖泊生成阈值，值越大湖泊越大
        public float SHALLOW_WATER_THRESHOLD = 0.45f; // 浅水区域阈值

        public override void Generate(Map map, GenStepParams parms)
        {
            if (map == null) return;
            map.Biome.plantDensity = 0.45f;
            // 创建柏林噪声
            ModuleBase noise = new Perlin(NOISE_SCALE, 2.0, 0.5, 6, Rand.Range(0, int.MaxValue), QualityMode.High);

            // 计算地图中心
            IntVec3 center = new IntVec3(map.Size.x / 2, 0, map.Size.z / 2);

            // 创建高度图
            float[,] heightMap = new float[map.Size.x, map.Size.z];

            // 生成基础高度图
            for (int x = 0; x < map.Size.x; x++)
            {
                for (int z = 0; z < map.Size.z; z++)
                {
                    // 计算到中心的距离（归一化到0-1）
                    float distToCenter = Vector2.Distance(
                        new Vector2(x, z),
                        new Vector2(center.x, center.z)
                    ) / (map.Size.x / 2f);

                    // 线性函数：从边缘到中心降低（边缘高，中心低）
                    float baseHeight = distToCenter;

                    // 添加柏林噪声
                    float noiseValue = (float)noise.GetValue(x, 0, z);
                    noiseValue = (noiseValue + 1f) / 2f; // 归一化到0-1

                    // 组合基础高度和噪声
                    heightMap[x, z] = baseHeight * 0.85f + noiseValue * 0.15f;
                }
            }

            // 找到最低点作为河流起点
            IntVec3 riverStart = FindLowestPoint(heightMap, map);

            // 生成湖泊
            for (int x = 0; x < map.Size.x; x++)
            {
                for (int z = 0; z < map.Size.z; z++)
                {
                    float height = heightMap[x, z];
                    if (height < SHALLOW_WATER_THRESHOLD)
                    {
                        IntVec3 cell = new IntVec3(x, 0, z);
                        ClearCell(map, cell);
                        // 浅水区域
                        map.terrainGrid.SetTerrain(cell, TerrainDefOf.WaterShallow);
                        if (height < LAKE_THRESHOLD)
                        {
                            // 深水区域
                            map.terrainGrid.SetTerrain(cell, TerrainDefOf.WaterDeep);
                        }
                    }
                }
            }
            // 生成河流
            GenerateRiver(map, riverStart, center, heightMap);

            // 30%概率在地图中心生成冰地板和圣物展台
            if (Rand.Value < 0.3f)
            {
                GenerateIceFloorAndReliquary(map, center);
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

        private IntVec3 FindLowestPoint(float[,] heightMap, Map map)
        {
            float minHeight = float.MaxValue;
            IntVec3 lowestPoint = IntVec3.Zero;

            // 只在地图边缘搜索
            for (int x = 0; x < map.Size.x; x++)
            {
                for (int z = 0; z < map.Size.z; z++)
                {
                    if (x == 0 || x == map.Size.x - 1 || z == 0 || z == map.Size.z - 1)
                    {
                        if (heightMap[x, z] < minHeight)
                        {
                            minHeight = heightMap[x, z];
                            lowestPoint = new IntVec3(x, 0, z);
                        }
                    }
                }
            }

            return lowestPoint;
        }

        private void GenerateRiver(Map map, IntVec3 start, IntVec3 end, float[,] heightMap)
        {
            IntVec3 current = start;
            HashSet<IntVec3> riverCells = new HashSet<IntVec3>();

            while (current != end)
            {
                // 添加当前点到河流路径
                riverCells.Add(current);

                // 计算到目标的方向
                int dx = Math.Sign(end.x - current.x);
                int dz = Math.Sign(end.z - current.z);

                // 根据高度图选择下一步
                IntVec3 next = current;
                float minHeight = heightMap[current.x, current.z];

                // 检查周围8个方向
                for (int x = -1; x <= 1; x++)
                {
                    for (int z = -1; z <= 1; z++)
                    {
                        if (x == 0 && z == 0) continue;

                        IntVec3 check = new IntVec3(current.x + x, 0, current.z + z);
                        if (check.InBounds(map) && !riverCells.Contains(check))
                        {
                            float height = heightMap[check.x, check.z];
                            if (height < minHeight)
                            {
                                minHeight = height;
                                next = check;
                            }
                        }
                    }
                }

                // 如果找不到更低点，使用基本方向
                if (next == current)
                {
                    if (Math.Abs(end.x - current.x) > Math.Abs(end.z - current.z))
                    {
                        next = new IntVec3(current.x + dx, 0, current.z);
                    }
                    else
                    {
                        next = new IntVec3(current.x, 0, current.z + dz);
                    }
                }
                current = next;
            }

            // 生成河流
            foreach (IntVec3 cell in riverCells)
            {
                // 生成河流主体
                for (int x = -2; x <= 2; x++)
                {
                    for (int z = -2; z <= 2; z++)
                    {
                        IntVec3 waterCell = new IntVec3(cell.x + x, 0, cell.z + z);
                        if (waterCell.InBounds(map) && waterCell.GetTerrain(map) != TerrainDefOf.WaterDeep)
                        {
                            ClearCell(map, waterCell);
                            // 最外侧2格为浅水
                            if (Math.Abs(x) == 2 || Math.Abs(z) == 2)
                            {
                                map.terrainGrid.SetTerrain(waterCell, TerrainDefOf.WaterShallow);
                            }
                            else
                            {
                                map.terrainGrid.SetTerrain(waterCell, TerrainDefOf.WaterDeep);
                            }
                        }
                    }
                }
            }
        }

        private void GenerateIceFloorAndReliquary(Map map, IntVec3 center)
        {
            float radius = 3.9f;
            int radiusInt = Mathf.CeilToInt(radius);

            for (int x = center.x - radiusInt; x <= center.x + radiusInt; x++)
            {
                for (int z = center.z - radiusInt; z <= center.z + radiusInt; z++)
                {
                    IntVec3 cell = new IntVec3(x, 0, z);
                    if (cell.InBounds(map))
                    {
                        float distance = Vector3.Distance(new Vector3(x, 0, z), new Vector3(center.x, 0, center.z));
                        if (distance <= radius)
                        {
                            ClearCell(map, cell);
                            if ((x == center.x + radiusInt) || (x == center.x - radiusInt) || (z == center.z - radiusInt) || (z == center.z + radiusInt))
                            {
                                map.terrainGrid.SetTerrain(cell, TerrainDefOf.Ice);
                            }
                        }
                    }
                }
            }
            // 在地图中心放置圣物展台
            Building reliquary = (Building)GenSpawn.Spawn(ThingDefOf.Reliquary, center, map);
            reliquary.Rotation = Rot4.South;

            // 在两侧放火把
            IntVec3 leftTorchPos = new IntVec3(center.x - 2, 0, center.z);
            IntVec3 rightTorchPos = new IntVec3(center.x + 2, 0, center.z);

            if (leftTorchPos.InBounds(map))
            {
                ClearCell(map, leftTorchPos);
                GenSpawn.Spawn(ThingDefOf.TorchLamp, leftTorchPos, map);
            }

            if (rightTorchPos.InBounds(map))
            {
                ClearCell(map, rightTorchPos);
                GenSpawn.Spawn(ThingDefOf.TorchLamp, rightTorchPos, map);
            }
            // 生成随机传奇近战武器
            ThingDef weaponDef = DefDatabase<ThingDef>.AllDefs
                .Where(def => def.IsWeapon && !def.IsRangedWeapon && def.weaponClasses != null)
                .RandomElement();
            if (weaponDef != null)
            {
                ThingWithComps weapon = (ThingWithComps)ThingMaker.MakeThing(weaponDef);
                weapon.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Legendary, ArtGenerationContext.Outsider);
                // 将武器放入圣物展台
                CompRelicContainer relicContainer = reliquary.GetComp<CompRelicContainer>();
                if (relicContainer != null)
                {
                    relicContainer.innerContainer.TryAdd(weapon);
                }
            }
        }
    }
}