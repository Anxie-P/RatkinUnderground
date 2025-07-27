using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.Noise;

namespace RatkinUnderground
{
    public class RKU_GenStep_Flesh : GenStep
    {
        int count = 0;
        public override int SeedPart => 56436867;
        public float NOISE_SCALE = 0.02f; // 柏林噪声缩放
        public float LAKE_THRESHOLD = 0.30f; // 湖泊生成阈值，值越大湖泊越大
        public float SHALLOW_WATER_THRESHOLD = 0.36f; // 浅水区域阈值

        private const int BranchCount = 12;     //分支数量
        private const float BranchLengthMin = 0.5f;    //分支最小长度
        private const float BranchLengthMax = 0.7f;    //分支最大长度
        private const float CurveFactor = 0.5f; // 转向强度，0.0-1.0

        public override void Generate(Map map, GenStepParams parms)
        {
            if (map == null) return;

            // 先生成隧道部分
            SpawnBranch(map);

            ThingDef flesh = DefDatabase<ThingDef>.GetNamed("Fleshmass");
            Log.Message("已执行flesh的生成");
            map.Biome.plantDensity = 0.45f;
            ModuleBase noise = new Perlin(NOISE_SCALE, 2.0, 0.5, 6, Rand.Range(0, int.MaxValue), QualityMode.High);
            // 计算地图中心
            IntVec3 center = new IntVec3(map.Size.x / 2, 0, map.Size.z / 2);

            // 创建高度图
            float[,] heightMap = new float[map.Size.x, map.Size.z];

            float[,] noiseMap = new float[map.Size.x, map.Size.z];
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
                    noiseMap[x, z] = noiseValue;
                    
                }
            }
            
            // 生成湖泊
            for (int x = 0; x < map.Size.x; x++)
            {
                for (int z = 0; z < map.Size.z; z++)
                {
                    IntVec3 cell = new IntVec3(x, 0, z);
                    float height = heightMap[x, z];
                    if (height < SHALLOW_WATER_THRESHOLD)
                    {
                        //ClearCell(map, cell);
                        // 血肉外圈生成
                        ClearCell(map, cell);
                        GenSpawn.Spawn(ThingMaker.MakeThing(flesh), cell, map, WipeMode.Vanish);
                        if (height < LAKE_THRESHOLD)
                        {
                            // 血肉内部生成，使用噪声将0的区域填充血肉
                            float noiseInner = noiseMap[x, z];
                            if (noiseInner < 0.5f)
                            {
                                ClearCell(map, cell);
                                //GenSpawn.Spawn(ThingMaker.MakeThing(flesh), cell, map, WipeMode.Vanish);
                                //Log.Message($"已生在{cell}生成中心血肉");
                            }

                        }
                    }
                    
                }
            }
            //SpawnBranch(map);

        }

        // 清理区域
        void ClearCell(Map map, IntVec3 cell)
        {
            // 安全地清除所有物品和建筑
            if (!cell.InBounds(map)) return;
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

        // 生成分支
        void SpawnBranch(Map map)
        {
            //Log.Message("已生成回路");
            IntVec3 center = new IntVec3(map.Size.x / 2, 0, map.Size.z / 2);
            System.Random rand = new System.Random();
            for (int i = 0; i < BranchCount; i++)
            {
                float angle = ((float)rand.NextDouble()) * 360f;
                int r = rand.Next(5, 11);
                //Log.Message($"当前{count}路线的初始半径为{r}");
                GenerateCurveTunnel(map, center, angle, rand, r);
                count++;
            }
            //Log.Message($"总生成{count}条线路");
        }

        // 生成曲线路径
        void GenerateCurveTunnel(Map map, IntVec3 start, float startAngle, System.Random rand, int r)
        {
            IntVec3 current = start;
            IntVec3 endPoint = start;
            float angle = startAngle;
            int length = rand.Next((int)(map.Size.x * BranchLengthMin), (int)(map.Size.x * BranchLengthMax));
            
            int reduceCount = (int)(map.Size.x * 0.04);    // 平滑过度
            int reduceStep = reduceCount;

            bool inBounds = true; // 是否在地图边界内
            
            for (int step = 0; step < length; step++)
            {
                                
                // 随机偏转
                angle += (((float)rand.NextDouble()) * 2 - 1) * 30f * CurveFactor;
                IntVec3 next = current + AngleToOffset(angle);

                // 检测是否越界
                if (!next.InBounds(map))
                {
                    inBounds = false;
                    //Log.Message($"地{count}条线路，已在{current}中断");
                    break;
                }

                reduceStep--;
                // 半径随机衰减
                if (rand.NextDouble() < 0.10f && r > 1 && reduceStep <= 0)
                {
                    reduceStep = reduceCount;
                    r -= 1;
                    r = Math.Max(r, 1);     // 确保半径不跑出0
                }
                //Log.Message($"{count}线当前半径为{r}");

                endPoint = current;
                // 执行生成
                CarveCircle(map, next, r);
                current = next;
            }

            // 保证末端平滑
            if (r > 1 && inBounds)
            {
                int rc = 4;  // 末端过度总步长
                while (r >= 1)
                {
                    IntVec3 next = endPoint + AngleToOffset(angle);
                    CarveCircle(map, next, r);
                    endPoint = next;
                    rc--;
                    if (rc < 0)
                    {
                        r--;
                        rc = 4;
                    }

                }
            }
            //Log.Message($"地{count}条线路，终点在{current}");
            //Log.Message($"当前{count}路线的结束半径为{r}");
        }

        // 填充血肉快
        void CarveCircle(Map map, IntVec3 center, int r)
        {
            if (!center.InBounds(map)) return;
            ThingDef flesh = DefDatabase<ThingDef>.GetNamed("Fleshmass");
            
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, r, true))
            {
                if (!cell.InBounds(map)) continue;
                Building edifice = cell.GetEdifice(map);
                if (edifice.def.defName == "Fleshmass") continue;
                ClearCell(map, cell);
                //Log.Message($"已在{cell}生成血肉");
                Thing fleshMass = ThingMaker.MakeThing(flesh);
                GenSpawn.Spawn(fleshMass, cell, map, WipeMode.Vanish);
            }
        }

        // 将角度转换为地图方向偏移
        private IntVec3 AngleToOffset(float angle)
        {
            var rad = angle * (float)Math.PI / 180f;
            int dx = (int)Math.Round(Math.Cos(rad));
            int dz = (int)Math.Round(Math.Sin(rad));
            return new IntVec3(dx, 0, dz);
        }
    }
}
