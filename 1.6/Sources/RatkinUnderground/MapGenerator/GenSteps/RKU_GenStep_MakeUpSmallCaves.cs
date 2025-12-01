using RimWorld;
using System;
using System.Collections.Generic;
using Verse;

namespace RatkinUnderground 
{
    public class RKU_GenStep_MakeUpSmallCaves : GenStep
    {
        public override int SeedPart => 83571635;
        public int maxCaveSizeToFill = 200;
        public List<ThingDef> possibleMinerals = new List<ThingDef>();

        private ThingDef GetAdjacentRockType(Map map, IntVec3 cell)
        {
            foreach (IntVec3 adjCell in GenAdj.AdjacentCells)
            {
                IntVec3 checkCell = cell + adjCell;
                if (checkCell.InBounds(map))
                {
                    Thing edifice = checkCell.GetEdifice(map);
                    if (edifice != null && edifice.def.building != null && edifice.def.building.isNaturalRock)
                    {
                        return edifice.def;
                    }
                }
            }
            return null;
        }

        public override void Generate(Map map, GenStepParams parms)
        {
            if (map == null)
            {
                Log.Error("[RKU_GenStep_MakeUpSmallCaves] Map or parameters are null.");
                return;
            }

            if (possibleMinerals.NullOrEmpty())
            {
                Log.Error("[RKU_GenStep_MakeUpSmallCaves] 'possibleMinerals' list is null or empty. Cannot generate minerals. Please configure it in XML.");
                return;
            }

            Log.Message($"[RKU_GenStep_MakeUpSmallCaves] Starting generation with {possibleMinerals.Count} possible minerals.");

            FloodFiller floodFiller = new FloodFiller(map);
            bool[,] cellHandled = new bool[map.Size.x, map.Size.z];
            List<IntVec3> collectedCells = new List<IntVec3>();
            int createdMinerals = 0;

            // 遍历地图的每个单元格
            for (int x = 0; x < map.Size.x; x++)
            {
                for (int z = 0; z < map.Size.z; z++)
                {
                    if (cellHandled[x, z]) continue;

                    IntVec3 rootCell = new IntVec3(x, 0, z);
                    if (rootCell.GetEdifice(map) != null) continue;

                    // 收集相连的空单元格
                    collectedCells.Clear();
                    floodFiller.FloodFill(rootCell,
                        cell => cell.InBounds(map) && cell.GetEdifice(map) == null,
                        cell => collectedCells.Add(cell),
                        int.MaxValue, false, null);

                    // 标记已处理的单元格
                    foreach (IntVec3 cell in collectedCells)
                    {
                        if (cell.InBounds(map))
                        {
                            cellHandled[cell.x, cell.z] = true;
                        }
                    }

                    // 如果找到的区域大小合适，则填充矿物
                    if (collectedCells.Count > 0 && collectedCells.Count < maxCaveSizeToFill)
                    {
                        ThingDef chosenMineral = null;
                        
                        // 检查所有单元格周围是否有岩石，80%概率使用该岩石类型
                        if (Rand.Value < 0.8f)
                        {
                            //找到最近的有岩石的单元格
                            foreach (IntVec3 cell in collectedCells)
                            {
                                ThingDef rockType = GetAdjacentRockType(map, cell);
                                if (rockType != null)
                                {
                                    chosenMineral = rockType;
                                    break;
                                }
                            }
                        }
                        
                        // 如果没有找到周围岩石或概率未触发，使用随机矿物
                        if (chosenMineral == null)
                        {
                            chosenMineral = possibleMinerals.RandomElement();
                        }
                        
                        if (chosenMineral == null) continue;

                        // 把整个区域都填满矿物
                        foreach (IntVec3 cell in collectedCells)
                        {
                            try
                            {
                                Thing mineral = ThingMaker.MakeThing(chosenMineral);
                                if (mineral != null)
                                {
                                    GenSpawn.Spawn(mineral, cell, map, WipeMode.Vanish);
                                    createdMinerals++;
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Error($"[RKU_GenStep_MakeUpSmallCaves] Error while generating mineral at {cell}: {ex.Message}");
                            }
                        }
                    }
                }
            }

            Log.Message($"[RKU_GenStep_MakeUpSmallCaves] Generation completed. Created {createdMinerals} mineral deposits.");
        }
    }
}