using RimWorld;
using System;
using System.Collections.Generic;
using Verse.Noise;
using Verse;
using UnityEngine;

namespace RatkinUnderground
{
    public class RKU_GenStep_Caves : GenStep // 继承自 GenStep
    {
        // --- 参数字段 ---
        public float tunnelWidthMultiplierMin = 0.3f;
        public float tunnelWidthMultiplierMax = 0.5f;
        public float minTunnelWidth = 1.4f;
        public float widthReductionPerCell = 0.034f;
        public float branchChance = 0.1f;
        public int branchMinDistanceFromStart = 15;
        public float directionChangeSpeed = 8f;
        public float directionNoiseFrequency = 0.00205f;
        public FloatRange branchedTunnelWidthOffset = new FloatRange(0.2f, 0.4f);

        public TerrainDef caveFloorDef = TerrainDefOf.Sand;
        public TerrainDef caveWallDef = TerrainDefOf.Sand;

        private static HashSet<IntVec3> currentGroupSet = new HashSet<IntVec3>();
        private static List<IntVec3> currentGroupList = new List<IntVec3>();

        protected ModuleBase directionNoiseInstance;

        public override int SeedPart => 647814558;

        public override void Generate(Map map, GenStepParams parms)
        {
            // 初始化洞穴地面定义，如果XML中没有提供，则使用硬编码的默认值
            if (this.caveFloorDef == null) this.caveFloorDef = TerrainDefOf.Sand;
            if (this.caveWallDef == null) this.caveWallDef = TerrainDefOf.Sand;

            if (!map.generatorDef.forceCaves && !Find.World.HasCaves(map.Tile))
            {
                return;
            }

            directionNoiseInstance = new Perlin(directionNoiseFrequency, 2.0, 0.5, 4, Rand.Int, QualityMode.Medium);
            MapGenFloatGrid elevation = MapGenerator.Elevation;

            currentGroupList.Clear();
            currentGroupList.AddRange(map.AllCells);
            currentGroupSet.Clear();
            currentGroupSet.AddRange(currentGroupList);

            // 1. 生成一个主开放隧道
            SimpleCurve openTunnelWidthProfile = new SimpleCurve { new CurvePoint(1f, 6f), new CurvePoint(map.Area, 8f) };
            float openTunnelBaseWidth = openTunnelWidthProfile.Evaluate(currentGroupList.Count);
            float mainOpenTunnelWidth = Rand.Range(openTunnelBaseWidth * this.tunnelWidthMultiplierMin, openTunnelBaseWidth * this.tunnelWidthMultiplierMax);
            IntVec3 openTunnelStartCell = currentGroupList.RandomElement();
            HashSet<IntVec3> visitedForOpenTunnel = new HashSet<IntVec3>();
            Dig(openTunnelStartCell, Rand.Range(0f, 360f), mainOpenTunnelWidth, currentGroupSet, map, false, visitedForOpenTunnel);
            // 2. 用闭合隧道填充地图的其余部分
            int numClosedTunnelsToAttempt = Mathf.Max(10, map.Area / 75);
            Log.Warning($"[RKU_GenStep_Caves (继承GenStep, V2)] Attempting to dig {numClosedTunnelsToAttempt} CLOSED tunnels.");

            SimpleCurve closedTunnelsWidthProfile = new SimpleCurve { new CurvePoint(1f, 5f), new CurvePoint(map.Area, 7f) };
            float closedBaseTunnelWidth = closedTunnelsWidthProfile.Evaluate(currentGroupList.Count);

            for (int i = 0; i < numClosedTunnelsToAttempt; ++i)
            {
                IntVec3 startCell = currentGroupList.RandomElement();
                if (MapGenerator.Caves[startCell] > 0.1f && Rand.Chance(0.5f)) continue;
                float tunnelWidth = Rand.Range(closedBaseTunnelWidth * this.tunnelWidthMultiplierMin, closedBaseTunnelWidth * this.tunnelWidthMultiplierMax);
                HashSet<IntVec3> visitedForThisDigCall = new HashSet<IntVec3>();
                Dig(startCell, Rand.Range(0f, 360f), tunnelWidth, currentGroupSet, map, true, visitedForThisDigCall);
            }
            int caveCount = 0;
            foreach (IntVec3 cell in map.AllCells)
            {
                if (MapGenerator.Caves[cell] > 0f) caveCount++;
            }
            Log.Warning($"[RKU_GenStep_Caves] 洞穴格子数量: {caveCount}");

            // 在方法结束前添加调试日志
            int finalCaveCellsCount = 0;
            foreach (IntVec3 cell in map.AllCells)
            {
                if (MapGenerator.Caves[cell] > 0f)
                {
                    finalCaveCellsCount++;
                }
            }
            Log.Warning($"[RKU_GenStep_Caves] Finished generating caves. Total cave cells: {finalCaveCellsCount}");
        }

        protected void Dig(IntVec3 startCell, float initialDir, float currentWidth,
                           HashSet<IntVec3> rockGroupSet, Map map, bool isClosedTunnel,
                           HashSet<IntVec3> visitedByThisTunnelSystem)
        {
            Vector3 currentPositionVector = startCell.ToVector3Shifted();
            IntVec3 currentDigCell = startCell;
            float pathLengthTraveled = 0f;
            MapGenFloatGrid elevation = MapGenerator.Elevation;
            MapGenFloatGrid cavesOutputGrid = MapGenerator.Caves;

            bool hasBranchedPositive = false;
            bool hasBranchedNegative = false;
            int cellsDugCount = 0;
            int maxDigSteps = 500;
            int currentDigStep = 0;

            while (currentDigStep < maxDigSteps)
            {
                currentDigStep++;
                if (isClosedTunnel)
                {
                    int checkRadius = GenRadial.NumCellsInRadius(currentWidth / 2f + 1.5f);
                    for (int k = 0; k < checkRadius; k++)
                    {
                        IntVec3 cellToVerify = currentDigCell + GenRadial.RadialPattern[k];
                        if (!cellToVerify.InBounds(map)) return;
                        if (!rockGroupSet.Contains(cellToVerify) && !visitedByThisTunnelSystem.Contains(cellToVerify)) return;
                        if (cavesOutputGrid[cellToVerify] > 0f && !visitedByThisTunnelSystem.Contains(cellToVerify)) return;
                    }
                }

                if (cellsDugCount >= branchMinDistanceFromStart && currentWidth > minTunnelWidth + branchedTunnelWidthOffset.max)
                {
                    if (!hasBranchedPositive && Rand.Chance(branchChance))
                    {
                        DigInBestDirection(currentDigCell, initialDir, new FloatRange(40f, 90f),
                                           currentWidth - branchedTunnelWidthOffset.RandomInRange,
                                           rockGroupSet, map, isClosedTunnel, visitedByThisTunnelSystem);
                           hasBranchedPositive = true;
                    }
                    if (!hasBranchedNegative && Rand.Chance(branchChance))
                    {
                        DigInBestDirection(currentDigCell, initialDir, new FloatRange(-90f, -40f),
                                           currentWidth - branchedTunnelWidthOffset.RandomInRange,
                                           rockGroupSet, map, isClosedTunnel, visitedByThisTunnelSystem);
                        hasBranchedNegative = true;
                    }
                }

                SetCaveAround(currentDigCell, currentWidth, map, visitedByThisTunnelSystem, cavesOutputGrid, elevation, out bool hitAnotherTunnel);

                if (hitAnotherTunnel && !isClosedTunnel) break;

                IntVec3 previousDigCell = currentDigCell;
                int microSteps = 0;
                while (currentPositionVector.ToIntVec3() == currentDigCell && microSteps < 10)
                {
                    currentPositionVector += Vector3Utility.FromAngleFlat(initialDir) * 0.5f;
                    pathLengthTraveled += 0.5f;
                    microSteps++;
                }
                if (currentPositionVector.ToIntVec3() == currentDigCell && microSteps >= 10) break;

                currentDigCell = currentPositionVector.ToIntVec3();
                if (!currentDigCell.InBounds(map)) break;

                if (IsRock(currentDigCell, elevation, map) || !isClosedTunnel)
                {
                    initialDir += (float)directionNoiseInstance.GetValue(pathLengthTraveled * 60f, (float)startCell.x * 200f, (float)startCell.z * 200f) * directionChangeSpeed;
                    currentWidth -= widthReductionPerCell;
                    if (currentWidth < minTunnelWidth) break;
                    cellsDugCount++;
                }
                else { break; }
            }
        }

        private void DigInBestDirection(IntVec3 currentCell, float currentDir, FloatRange dirOffsetRange,
                                        float tunnelWidth, HashSet<IntVec3> rockGroupSet, Map map,
                                        bool isClosedTunnel, HashSet<IntVec3> visitedByThisTunnelSystem)
        {
            int bestDistToNonRock = -1;
            float bestDirection = -1f;
            for (int i = 0; i < 6; i++)
            {
                float testDir = currentDir + dirOffsetRange.RandomInRange;
                int dist = GetDistToNonRock(currentCell, rockGroupSet, testDir, 50, map);
                if (dist > bestDistToNonRock) { bestDistToNonRock = dist; bestDirection = testDir; }
            }
            if (bestDistToNonRock >= 10)
            {
                Dig(currentCell, bestDirection, tunnelWidth, rockGroupSet, map, isClosedTunnel, visitedByThisTunnelSystem);
            }
        }

        // *** 修正后的 SetCaveAround 方法 ***
        private void SetCaveAround(IntVec3 centerCell, float tunnelWidth, Map map,
                                   HashSet<IntVec3> visitedByThisTunnelSystem, MapGenFloatGrid cavesOutputGrid,
                                   MapGenFloatGrid elevationGrid, out bool hitAnotherTunnel)
        {
            hitAnotherTunnel = false;
            int radius = GenRadial.NumCellsInRadius(tunnelWidth / 2f);
            TerrainGrid terrainGrid = map.terrainGrid;
            RoofGrid roofGrid = map.roofGrid;

            for (int i = 0; i < radius; i++)
            {
                IntVec3 cellInRadius = centerCell + GenRadial.RadialPattern[i];

                if (cellInRadius.InBounds(map))
                {
                    if (cavesOutputGrid[cellInRadius] > 0f && !visitedByThisTunnelSystem.Contains(cellInRadius))
                    {
                        hitAnotherTunnel = true;
                    }

                    // 直接设置洞穴值，不使用Mathf.Max
                    cavesOutputGrid[cellInRadius] = 1.0f;
                    visitedByThisTunnelSystem.Add(cellInRadius);

                    Building edifice = cellInRadius.GetEdifice(map);
                    if (edifice != null)
                    {
                        edifice.Destroy(DestroyMode.Vanish);
                    }
                }
            }
        }

        private bool IsRock(IntVec3 c, MapGenFloatGrid elevation, Map map)
        {
            if (elevation == null) return false;
            return c.InBounds(map) && elevation[c] > 0.7f;
        }

        private int GetDistToNonRock(IntVec3 from, HashSet<IntVec3> rockGroupSet, float directionAngle, int maxDist, Map map)
        {
            MapGenFloatGrid elevation = MapGenerator.Elevation;
            Vector3 directionVector = Vector3Utility.FromAngleFlat(directionAngle);
            for (int dist = 0; dist <= maxDist; ++dist)
            {
                IntVec3 currentCell = (from.ToVector3Shifted() + directionVector * dist).ToIntVec3();
                if (!currentCell.InBounds(map) || !IsRock(currentCell, elevation, map))
                {
                    return dist;
                }
            }
            return maxDist;
        }
    }
}
