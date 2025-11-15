using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RatkinUnderground
{
    public class SitePartWorker_RatkinFarm : SitePartWorker
    {
        private List<Pawn> civilians = new List<Pawn>();
        private const int DETECTION_RADIUS = 5;

        public override void PostMapGenerate(Map map)
        {
            base.PostMapGenerate(map);
            SpawnEnemiesInFarm(map);
            SpawnCiviliansInFarm(map);
        }

        /// <summary>
        /// 在农场中生成敌人
        /// </summary>
        /// <param name="map">地图</param>
        private void SpawnEnemiesInFarm(Map map)
        {
            // 获取鼠族军阀派系
            var ratkinFaction = Find.FactionManager.FirstFactionOfDef(FactionDef.Named("Rakinia_Warlord"));

            // 如果没有军阀派系，则查找任意一个海盗阵营
            if (ratkinFaction == null)
            {
                ratkinFaction = Find.FactionManager.FirstFactionOfDef(DefDatabase<FactionDef>.AllDefs.FirstOrDefault(o => o.defName.Contains("Pirate")));
            }

            if (ratkinFaction == null) return;

            // 随机生成5-9个敌人
            int enemyCount = Rand.RangeInclusive(5, 9);

            // 鼠族战斗单位列表
            string[] combatantKinds = {
                "RatkinCombatant",
                "RatkinVanguard",
                "RatkinEliteDefender",
                "RatkinEliteGuardener"
            };

            // 找到所有室内房间中可站立的位置
            var allRooms = map.regionGrid.AllRooms
                .Where(room => room.CellCount > 5 && IsIndoorRoom(room, map))
                .ToList();

            if (allRooms.Count == 0) return;

            // 收集所有可用位置
            var availableCells = new List<IntVec3>();
            foreach (var room in allRooms)
            {
                var cells = room.Cells
                    .Where(c => c.Standable(map) && c.GetFirstPawn(map) == null)
                    .ToList();
                availableCells.AddRange(cells);
            }

            if (availableCells.Count == 0) return;

            // 打乱位置顺序，实现随机分布
            availableCells.Shuffle();

            List<Pawn> defenders = new List<Pawn>();

            // 生成敌人
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

            // 将地图上所有建筑归属于军阀
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
        /// 在农场中生成平民
        /// </summary>
        /// <param name="map">地图</param>
        private void SpawnCiviliansInFarm(Map map)
        {
            // 找到包含最多床的房间
            var roomWithMostBeds = FindRoomWithMostBeds(map);
            if (roomWithMostBeds == null) return;

            // 将房间内的所有床设置为囚犯床
            SetRoomBedsForPrisoners(roomWithMostBeds, map);

            // 在畜栏中生成动物
            SpawnAnimalsInPens(map);

            // 获取游击队派系（用于转换后的平民）
            var guerrillaFaction = Find.FactionManager.FirstFactionOfDef(DefOfs.RKU_Faction);
            if (guerrillaFaction == null) return;
            var availableCells = roomWithMostBeds.Cells
                .Where(c => c.Standable(map) && c.GetFirstPawn(map) == null)
                .OrderBy(c => Rand.Value)
                .Take(8)
                .ToList();

            if (availableCells.Count < 8) return;
            // 生成平民单位类型列表：7个普通平民 + 1个修女
            var civilianTypes = new List<string>
            {
                "RatkinColonist", "RatkinServant", "RatkinColonist", "RatkinServant",
                "RatkinColonist", "RatkinServant", "RatkinColonist", "RatkinPriest"
            };

            List<Pawn> civilians = new List<Pawn>();

            for (int i = 0; i < 8 && i < availableCells.Count; i++)
            {
                var kindDef = DefDatabase<PawnKindDef>.GetNamedSilentFail(civilianTypes[i]);
                if (kindDef != null)
                {
                    var request = new PawnGenerationRequest(
                        kindDef,
                        null,
                        PawnGenerationContext.NonPlayer
                    );

                    var civilian = PawnGenerator.GeneratePawn(request);
                    GenSpawn.Spawn(civilian, availableCells[i], map);
                    civilian.health.AddHediff(HediffDef.Named("RKU_CivilianMarker"));
                    civilians.Add(civilian);
                }
            }
            // 平民创建守卫当前房间的lord
            if (civilians.Count > 0)
            {
                var roomCenter = GetRoomCenter(roomWithMostBeds);
                var lordJob = new LordJob_DefendPoint(roomCenter);
                LordMaker.MakeNewLord(null, lordJob, map, civilians);
            }

            // 注册平民到列表
            civilians.AddRange(civilians);
        }

        public override void SitePartWorkerTick(SitePart sitePart)
        {
            base.SitePartWorkerTick(sitePart);

            Map map = sitePart.site.Map;
            if (map == null) return;

            // 每秒检查一次
            if (Find.TickManager.TicksGame % 60 != 0) return;

            CheckAndConvertCivilians(map);
        }

        private void CheckAndConvertCivilians(Map map)
        {
            var civiliansToRemove = new List<Pawn>();

            foreach (var civilian in civilians)
            {
                if (civilian == null || civilian.Destroyed || civilian.Dead)
                {
                    civiliansToRemove.Add(civilian);
                    continue;
                }
                if (IsPlayerOrGuerrillaNearby(civilian, map))
                {
                    ConvertToGuerrillaAndLeave(civilian);
                    civiliansToRemove.Add(civilian);
                }
            }

            foreach (var civilian in civiliansToRemove)
            {
                civilians.Remove(civilian);
            }
        }

        private bool IsPlayerOrGuerrillaNearby(Pawn civilian, Map map)
        {
            var guerrillaFaction = Find.FactionManager.FirstFactionOfDef(DefOfs.RKU_Faction);
            if (guerrillaFaction == null) return false;

            foreach (var cell in GenRadial.RadialCellsAround(civilian.Position, DETECTION_RADIUS, true))
            {
                if (!cell.InBounds(map)) continue;

                var pawns = cell.GetThingList(map).OfType<Pawn>();
                foreach (var pawn in pawns)
                {
                    if (pawn == civilian) continue;
                    // 平民检查是否为玩家殖民者或游击队成员
                    if ((pawn.Faction != null && pawn.Faction.IsPlayer) ||
                        (pawn.Faction == guerrillaFaction))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void ConvertToGuerrillaAndLeave(Pawn civilian)
        {
            var guerrillaFaction = Find.FactionManager.FirstFactionOfDef(DefOfs.RKU_Faction);
            if (guerrillaFaction == null) return;

            civilian.SetFaction(guerrillaFaction);
            var civilianHediff = civilian.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("RKU_CivilianMarker"));
            if (civilianHediff != null)
            {
                civilian.health.RemoveHediff(civilianHediff);
            }
            if (civilian.GetLord() != null)
            {
                civilian.GetLord().Notify_PawnLost(civilian, PawnLostCondition.LeftVoluntarily);
            }
        }

        /// <summary>
        /// 找到包含最多床的房间
        /// </summary>
        private Room FindRoomWithMostBeds(Map map)
        {
            Room bestRoom = null;
            int maxBedCount = 0;

            foreach (var room in map.regionGrid.AllRooms)
            {
                if (!IsIndoorRoom(room, map)) continue;

                int bedCount = 0;
                foreach (var cell in room.Cells)
                {
                    var things = cell.GetThingList(map);
                    foreach (var thing in things)
                    {
                        if (thing.def.IsBed)
                        {
                            bedCount++;
                        }
                    }
                }

                if (bedCount > maxBedCount)
                {
                    maxBedCount = bedCount;
                    bestRoom = room;
                }
            }

            return bestRoom;
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

        /// <summary>
        /// 获取房间的中心位置
        /// </summary>
        private IntVec3 GetRoomCenter(Room room)
        {
            if (room.Cells.Count() == 0) return IntVec3.Zero;

            long sumX = 0;
            long sumZ = 0;

            foreach (var cell in room.Cells)
            {
                sumX += cell.x;
                sumZ += cell.z;
            }

            return new IntVec3(
               ((int)sumX / room.Cells.Count()),
                0,
               ((int)sumZ / room.Cells.Count())
            );
        }

        /// <summary>
        /// 将房间内的所有床设置为囚犯床
        /// </summary>
        private void SetRoomBedsForPrisoners(Room room, Map map)
        {
            foreach (var cell in room.Cells)
            {
                var things = cell.GetThingList(map);
                foreach (var thing in things)
                {
                    if (thing.def.IsBed)
                    {
                        var bed = thing as Building_Bed;
                        if (bed != null)
                        {
                            bed.ForPrisoners = true;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 在畜栏中生成动物
        /// </summary>
        private void SpawnAnimalsInPens(Map map)
        {
            var penMarker = map.listerThings.ThingsOfDef(ThingDef.Named("PenMarker")).FirstOrDefault();
            if (penMarker == null) return;
            var animalKinds = new List<string>
            {
                "Cow",      // 牛
                "Sheep",    // 羊
                "Ratkin_KingHamster"  // 仓鼠带王
            };
            var spawnOffsets = new int[] { 1, 3, 5, 7, 9 };
            int animalsToSpawn = Rand.Range(9, 16);

            for (int i = 0; i < animalsToSpawn; i++)
            {
                var spawnCell = penMarker.Position + new IntVec3(-spawnOffsets.RandomElement(), 0, -spawnOffsets.RandomElement());
                if (!spawnCell.InBounds(map) || !spawnCell.Standable(map)) continue;
                string animalKindName = animalKinds.RandomElement();
                var animalKindDef = DefDatabase<PawnKindDef>.GetNamedSilentFail(animalKindName);
                if (animalKindDef != null)
                {
                    var animal = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                        animalKindDef,
                        faction: null,
                        context: PawnGenerationContext.NonPlayer
                    ));

                    GenSpawn.Spawn(animal, spawnCell, map);
                }
            }
        }
    }
}
