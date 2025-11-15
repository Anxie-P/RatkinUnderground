using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI.Group;
using static RatkinUnderground.Utils;

namespace RatkinUnderground
{
    public class SitePartWorker_RatkinFactory : SitePartWorker
    {
        private int assaultTickCounter = 0;
        private const int ASSAULT_DELAY_TICKS = 3000; // 3000 ticks (约5分钟) 后触发王国军增援
        private bool hasTriggeredReinforcement = false; // 确保援军只来一次

        public override void PostMapGenerate(Map map)
        {
            base.PostMapGenerate(map);
            SetAllBuildingsFaction(map);
        }
        /// <summary>
        /// 查找附近的可用生成位置
        /// </summary>
        private IntVec3 FindNearbySpawnPosition(IntVec3 center, Map map, int minDist, int maxDist)
        {
            for (int attempts = 0; attempts < 20; attempts++)
            {
                var offset = new IntVec3(
                    Rand.RangeInclusive(-maxDist, maxDist),
                    0,
                    Rand.RangeInclusive(-maxDist, maxDist)
                );

                var pos = center + offset;
                var distance = offset.LengthHorizontal;

                if (distance >= minDist && distance <= maxDist &&
                    pos.InBounds(map) && pos.Standable(map) && pos.GetFirstPawn(map) == null)
                {
                    return pos;
                }
            }

            return IntVec3.Invalid;
        }

        /// <summary>
        /// 获取房间中可用的位置
        /// </summary>
        private List<IntVec3> GetAvailableCellsInRoom(Room room, Map map, int maxCount)
        {
            var cells = room.Cells
                .Where(c => c.Standable(map) && c.GetFirstPawn(map) == null)
                .OrderBy(c => Rand.Value)
                .Take(maxCount)
                .ToList();
            return cells;
        }

        /// <summary>
        /// 检查房间是否为室内房间
        /// </summary>
        private bool IsIndoorRoom(Room room, Map map)
        {
            int roofedCells = 0;
            foreach (var cell in room.Cells)
            {
                var roof = map.roofGrid.RoofAt(cell);
                if (roof != null && roof != RoofDefOf.RoofRockThick)
                {
                    roofedCells++;
                }
            }
            return (float)roofedCells / room.CellCount > 0.7f;
        }

    
        /// <summary>
        /// 将地图上所有建筑归属于游击队派系
        /// </summary>
        private void SetAllBuildingsFaction(Map map)
        {
            var guerrillaFaction = Find.FactionManager.FirstFactionOfDef(DefOfs.RKU_Faction);
            if (guerrillaFaction == null) return;

            var colonistBuildings = map.listerBuildings.allBuildingsColonist.ToList();
            foreach (var building in colonistBuildings)
            {
                building.SetFaction(guerrillaFaction);
            }
            var nonColonistBuildings = map.listerBuildings.allBuildingsNonColonist.ToList();
            foreach (var building in nonColonistBuildings)
            {
                building.SetFaction(guerrillaFaction);
            }
        }

        /// <summary>
        /// 获取地图边缘的随机位置
        /// </summary>
        private static IntVec3 GetRandomEdgePosition(Map map)
        {
            int edge = Rand.Range(0, 4); // 0=北, 1=东, 2=南, 3=西
            IntVec3 pos;

            switch (edge)
            {
                case 0: // 北边缘
                    pos = new IntVec3(Rand.Range(0, map.Size.x), 0, map.Size.z - 1);
                    break;
                case 1: // 东边缘
                    pos = new IntVec3(map.Size.x - 1, 0, Rand.Range(0, map.Size.z));
                    break;
                case 2: // 南边缘
                    pos = new IntVec3(Rand.Range(0, map.Size.x), 0, 0);
                    break;
                default: // 西边缘
                    pos = new IntVec3(0, 0, Rand.Range(0, map.Size.z));
                    break;
            }

            return pos;
        }
    }
}
