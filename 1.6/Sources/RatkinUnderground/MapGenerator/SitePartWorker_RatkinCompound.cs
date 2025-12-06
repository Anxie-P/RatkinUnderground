using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI.Group;

namespace RatkinUnderground
{
    public class SitePartWorker_RatkinCompound : SitePartWorker
    {
        public override void PostMapGenerate(Map map)
        {
            base.PostMapGenerate(map);
            //Utils.ClearNonFactionPawns(map, new List<Faction> { Faction.OfPlayer });
            SpawnEnemiesInCompound(map);
        }

        /// <summary>
        /// 在炮楼中生成7-12个敌人
        /// </summary>
        /// <param name="map">地图</param>
        private void SpawnEnemiesInCompound(Map map)
        {
            // 获取鼠族军阀派系
            var ratkinFaction = Find.FactionManager.FirstFactionOfDef(FactionDef.Named("Rakinia_Warlord"));

            // 如果没有军阀派系，则查找任意一个海盗阵营
            if (ratkinFaction == null)
            {
                ratkinFaction = Find.FactionManager.FirstFactionOfDef(DefDatabase<FactionDef>.AllDefs.FirstOrDefault(o=>o.defName.Contains("Pirate")));
            }

            if (ratkinFaction == null) return;

            // 随机生成7-12个敌人
            int enemyCount = Rand.RangeInclusive(7, 12);

            // 鼠族战斗单位列表
            string[] combatantKinds = {
                "RatkinCombatant",
                "RatkinVanguard",
                "RatkinDemonMan",
                "RatkinEliteDefender",
                "RatkinEliteGuardener",
                "RatkinKnight"
            };

            // 找到所有室内房间中可站立的位置
            var allRooms = map.regionGrid.AllRooms
                .Where(room => room.CellCount > 5 && IsIndoorRoom(room, map))
                .ToList();

            if (allRooms.Count == 0) return;
            var availableCells = new List<IntVec3>();
            foreach (var room in allRooms)
            {
                var cells = room.Cells
                    .Where(c => c.Standable(map) && c.GetFirstPawn(map) == null)
                    .ToList();
                availableCells.AddRange(cells);
            }

            if (availableCells.Count == 0) return;

            availableCells.Shuffle();
            List<Pawn> defenders = new List<Pawn>();
            for (int i = 0; i < enemyCount && i < availableCells.Count; i++)
            {
                string kindDefName = combatantKinds.RandomElement();
                var kindDef = DefDatabase<PawnKindDef>.GetNamedSilentFail(kindDefName);

                if (kindDef != null)
                {
                    var request = new PawnGenerationRequest(
                        kindDef,
                        ratkinFaction,
                        PawnGenerationContext.NonPlayer
                    );

                    var pawn = PawnGenerator.GeneratePawn(request);
                    GenSpawn.Spawn(pawn, availableCells[i], map);
                    defenders.Add(pawn);
                }
            }

            // 为所有敌人创建防御Lord
            if (defenders.Count > 0)
            {
                var centerPos = defenders[0].Position;
                var lordJob = new LordJob_DefendPoint(centerPos);
                LordMaker.MakeNewLord(ratkinFaction, lordJob, map, defenders);
            }

            // 将地图上所有建筑归属于ratkinFaction
            // 先创建副本，避免在遍历时修改集合
            var colonistBuildings = map.listerBuildings.allBuildingsColonist.ToList();
            foreach (var building in colonistBuildings)
            {
                building.SetFaction(ratkinFaction);
            }
            var nonColonistBuildings = map.listerBuildings.allBuildingsNonColonist.ToList();
            foreach (var building in nonColonistBuildings)
            {
                building.SetFaction(ratkinFaction);
            }
        }

        /// <summary>
        /// 检查房间是否为室内房间
        /// </summary>
        /// <param name="room">房间</param>
        /// <param name="map">地图</param>
        /// <returns>是否为室内房间</returns>
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
    }
}

