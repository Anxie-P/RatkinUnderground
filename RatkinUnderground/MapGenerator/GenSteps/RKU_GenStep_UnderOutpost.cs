using RimWorld;
using System;
using System.Collections.Generic;
using Verse;
using Verse.Noise;
using UnityEngine;
using System.Linq;
using RimWorld.BaseGen;
using RimWorld.SketchGen;
using Verse.AI.Group;

namespace RatkinUnderground
{
    public class RKU_GenStep_UnderOutpost : GenStep
    {
        private const int OUTPOST_SIZE = 18; // 前哨站大小
        private const int SHORE_DISTANCE = 3; // 湖岸距离改为4格

        public override int SeedPart => 446845;

        private SimpleCurve ruinSizeChanceCurve = new SimpleCurve
        {
            new CurvePoint(6f, 0f),
            new CurvePoint(6.001f, 4f),
            new CurvePoint(10f, 1f),
            new CurvePoint(30f, 0f)
        };

        private bool clearSurroundingArea;

        private float destroyChanceExp = 1.32f;

        private bool mustBeStandable;

        private bool canBeOnEdge;

        public override void Generate(Map map, GenStepParams parms)
        {
            if (map == null) return;

            // 清除所有水体
            foreach (IntVec3 cell in map.AllCells)
            {
                if (cell.InBounds(map))
                {
                    TerrainDef currentTerrain = cell.GetTerrain(map);
                    List<TerrainDef> stoneFloors = new List<TerrainDef>
            {
                TerrainDef.Named("TileGranite"),
                TerrainDef.Named("TileLimestone"),
                TerrainDef.Named("TileMarble"),
                TerrainDef.Named("TileSandstone"),
                TerrainDef.Named("TileSlate")
            };
                    if (currentTerrain == TerrainDefOf.WaterDeep || currentTerrain == TerrainDefOf.WaterShallow)
                    {
                        // 清除当前格子
                        ClearCell(map, cell);
                        TerrainDef randomFloor = stoneFloors.RandomElement();
                        map.terrainGrid.SetTerrain(cell, GenStep_RocksFromGrid.RockDefAt(cell).building.naturalTerrain);
                    }
                }
            }

            // 固定在地图中心生成
            IntVec3 center = map.Center;

            CellRect rect = CellRect.CenteredOn(center, OUTPOST_SIZE, OUTPOST_SIZE).ClipInsideMap(map);

            if (!CanPlaceAncientBuildingInRange(rect, map))
            {
                return;
            }

            RimWorld.SketchGen.ResolveParams parms2 = default(RimWorld.SketchGen.ResolveParams);
            parms2.sketch = new Sketch();
            parms2.monumentSize = new IntVec2(rect.Width, rect.Height);
            parms2.destroyChanceExp = destroyChanceExp;
            SketchGen.Generate(DefOfs.RKU_MonumentRuin, parms2).Spawn(map, rect.CenterCell, null, Sketch.SpawnPosType.Unchanged, Sketch.SpawnMode.Normal, wipeIfCollides: false, clearEdificeWhereFloor: false, null, dormant: false, buildRoofsInstantly: false, delegate (SketchEntity entity, IntVec3 cell)
            {
                IntVec3[] cardinalDirectionsAndInside = GenAdj.CardinalDirectionsAndInside;
                foreach (IntVec3 intVec in cardinalDirectionsAndInside)
                {
                    if ((cell + intVec).InBounds(map))
                    {
                        Building edifice = (cell + intVec).GetEdifice(map);
                        if (edifice != null && !edifice.Position.CloseToEdge(map, 3) && edifice.def.building.isNaturalRock)
                        {
                            edifice.Destroy();
                        }
                    }
                }

                bool result = false;
                foreach (IntVec3 adjacentCell in entity.OccupiedRect.AdjacentCells)
                {
                    IntVec3 c2 = cell + adjacentCell;
                    if (c2.InBounds(map))
                    {
                        Building edifice2 = c2.GetEdifice(map);
                        if (edifice2 == null || !edifice2.def.building.isNaturalRock)
                        {
                            result = true;
                            break;
                        }
                    }
                }
                return result;
            });
            GenerateDrillingVehicleAndScouts(map, rect);
        }

        protected bool CanPlaceAncientBuildingInRange(CellRect rect, Map map)
        {
            foreach (IntVec3 cell in rect.Cells)
            {
                if (!canBeOnEdge && !cell.InBounds(map))
                {
                    return false;
                }

                TerrainDef terrainDef = map.terrainGrid.TerrainAt(cell);
                if (terrainDef.HasTag("River") || terrainDef.HasTag("Road"))
                {
                    return false;
                }

                if (!GenConstruct.CanBuildOnTerrain(ThingDefOf.Wall, cell, map, Rot4.North))
                {
                    return false;
                }
            }

            return true;
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

        private void GenerateDrillingVehicleAndScouts(Map map, CellRect rect)
        {
            IntVec3 center = rect.CenterCell;
            IntVec3 vehiclePos = IntVec3.Invalid;
            int minDistance = 5; // 最小距离中心5格
            int maxRadius = Mathf.Max(rect.Width, rect.Height) / 2;

            for (int radius = minDistance; radius <= maxRadius; radius++)
            {
                foreach (var dir in new[] { Rot4.East, Rot4.West })
                {
                    IntVec3 cell = center + dir.FacingCell * radius;
                    if (cell.InBounds(map) && IsEmptyTwoCells(map, cell, dir))
                    {
                        vehiclePos = cell;
                        break;
                    }
                }
                if (vehiclePos.IsValid) break;
                for (int x = -radius; x <= radius; x++)
                {
                    for (int z = -radius; z <= radius; z++)
                    {
                        if (Mathf.Abs(x) != radius && Mathf.Abs(z) != radius)
                            continue;

                        IntVec3 cell = center + new IntVec3(x, 0, z);
                        if (cell.DistanceTo(center) < minDistance || !cell.InBounds(map))
                            continue;

                        if (IsEmptyTwoCells(map, cell, Rot4.North))
                        {
                            vehiclePos = cell;
                            break;
                        }
                    }
                    if (vehiclePos.IsValid) break;
                }
                if (vehiclePos.IsValid) break;
            }

            if (!vehiclePos.IsValid) return;

            Thing vehicle = ThingMaker.MakeThing(DefOfs.RKU_DrillingVehicle);
            GenSpawn.Spawn(vehicle, vehiclePos, map, Rot4.North); // 固定Rot4.North

            List<Pawn> scouts = new List<Pawn>();
            Faction faction = Find.FactionManager.FirstFactionOfDef(DefOfs.RKU_Faction);
            for (int i = 0; i < 2; i++)
            {
                Pawn scout = PawnGenerator.GeneratePawn(DefOfs.RKU_Scout, faction);
                IntVec3 spawnPos = CellFinder.RandomClosewalkCellNear(vehiclePos, map, 3);
                GenSpawn.Spawn(scout, spawnPos, map);
                scouts.Add(scout);
            }
            for (int i = 0; i < 1; i++)
            {
                Pawn scout = PawnGenerator.GeneratePawn(PawnKindDef.Named("RKU_Commissar"), faction);
                IntVec3 spawnPos = CellFinder.RandomClosewalkCellNear(vehiclePos, map, 3);
                GenSpawn.Spawn(scout, spawnPos, map);
                scouts.Add(scout);
            }

            LordMaker.MakeNewLord(faction, new LordJob_DefendPoint(vehiclePos), map, scouts);
        }


        private bool IsEmptyTwoCells(Map map, IntVec3 pos, Rot4 dir)
        {
            IntVec3 next = pos + dir.FacingCell;
            if (!next.InBounds(map)) return false;
            return pos.Standable(map) && next.Standable(map) &&
                   pos.GetEdifice(map) == null && next.GetEdifice(map) == null &&
                   pos.GetFirstBuilding(map) == null && next.GetFirstBuilding(map) == null;
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
