using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI.Group;
using UnityEngine;
using RimWorld.Planet;
using static RatkinUnderground.Utils;
using Verse.AI;

namespace RatkinUnderground
{
    public class SitePartWorker_RatkinCastle : SitePartWorker
    {
        private int reinforcementTickCounter = 0;
        private const int REINFORCEMENT_DELAY_TICKS = 4000;
        private bool hasTriggeredReinforcement = false;

        public override void PostMapGenerate(Map map)
        {
            base.PostMapGenerate(map);
            //Utils.ClearNonFactionPawns(map, new List<Faction> { Faction.OfPlayer });
            SpawnEnemiesInCastle(map);
            SpawnItemsOnShelves(map);
            SpawnNobleWithSpecialWeapon(map);
        }

        /// <summary>
        /// 在城堡中生成敌人
        /// </summary>
        private void SpawnEnemiesInCastle(Map map)
        {
            // 获取鼠族军阀派系
            var ratkinFaction = Find.FactionManager.FirstFactionOfDef(FactionDef.Named("Rakinia_Warlord"));

            // 如果没有军阀派系，则查找任意一个海盗阵营
            if (ratkinFaction == null)
            {
                ratkinFaction = Find.FactionManager.FirstFactionOfDef(DefDatabase<FactionDef>.AllDefs.FirstOrDefault(o => o.defName.Contains("Pirate")));
            }

            if (ratkinFaction == null) return;

            // 找到所有室内房间
            var allRooms = map.regionGrid.AllRooms
                .Where(room => room.CellCount > 5 && IsIndoorRoom(room, map))
                .ToList();

            if (allRooms.Count == 0) return;

            // 找到最大的4个中间房间
            var centerRooms = FindCenterRooms(allRooms, map, 4);

            // 识别塔楼房间（包含三张床的房间，但不包括中间房间）
            var towerRooms = new List<Room>();
            foreach (var room in allRooms)
            {
                if (!centerRooms.Contains(room) && HasThreeBeds(room, map))
                {
                    towerRooms.Add(room);
                }
            }

            // 限制为四个塔楼
            if (towerRooms.Count > 4)
            {
                towerRooms = towerRooms.OrderByDescending(r => r.CellCount).Take(4).ToList();
            }

            // 为每个房间的敌人创建独立的守卫任务
            var roomDefenders = new Dictionary<Room, List<Pawn>>();

            // 在四个塔楼中各生成3个骑士
            foreach (var towerRoom in towerRooms)
            {
                var towerDefenders = new List<Pawn>();
                var knightCells = GetAvailableCellsInRoom(towerRoom, map, 3);
                for (int i = 0; i < knightCells.Count && i < 3; i++)
                {
                    var knightDef = DefDatabase<PawnKindDef>.GetNamedSilentFail("RatkinKnight");
                    if (knightDef != null)
                    {
                        var request = new PawnGenerationRequest(
                            knightDef,
                            ratkinFaction,
                            PawnGenerationContext.NonPlayer
                        );

                        var knight = PawnGenerator.GeneratePawn(request);
                        GenSpawn.Spawn(knight, knightCells[i], map);
                        towerDefenders.Add(knight);
                    }
                }
                roomDefenders[towerRoom] = towerDefenders;
            }

            // 在最大的4个中间房间中各生成3-4个军阀单位
            foreach (var centerRoom in centerRooms)
            {
                var centerDefenders = new List<Pawn>();
                int warlordCount = Rand.RangeInclusive(3, 4);
                var centerCells = GetAvailableCellsInRoom(centerRoom, map, warlordCount);

                // 军阀部队单位列表
                string[] warlordKinds = {
                    "RatkinDemonMan",
                    "RatkinEliteDefender",
                    "RatkinEliteGuardener",
                    "RatkinKnight",
                    "RatkinVanguard"
                };

                for (int i = 0; i < warlordCount && i < centerCells.Count; i++)
                {
                    string kindDefName = warlordKinds.RandomElement();
                    var kindDef = DefDatabase<PawnKindDef>.GetNamedSilentFail(kindDefName);

                    if (kindDef != null)
                    {
                        var request = new PawnGenerationRequest(
                            kindDef,
                            ratkinFaction,
                            PawnGenerationContext.NonPlayer
                        );

                        var pawn = PawnGenerator.GeneratePawn(request);
                        GenSpawn.Spawn(pawn, centerCells[i], map);
                        centerDefenders.Add(pawn);
                    }
                }
                roomDefenders[centerRoom] = centerDefenders;
            }

            // lord
            foreach (var kvp in roomDefenders)
            {
                var room = kvp.Key;
                var defenders = kvp.Value;

                if (defenders.Count > 0)
                {
                    var roomCenter = GetRoomCenter(room);
                    var lordJob = new LordJob_DefendPoint(roomCenter);
                    LordMaker.MakeNewLord(ratkinFaction, lordJob, map, defenders);
                }
            }

            // 建筑归属
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
        /// 在物品架上生成道具
        /// </summary>
        private void SpawnItemsOnShelves(Map map)
        {
            // 找到地图上所有物品架
            var shelves = map.listerBuildings.allBuildingsNonColonist
                .Where(building => building.def.defName.Contains("Shelf"))
                .ToList();

            foreach (var shelf in shelves)
            {
                Thing item1 = GenerateRandomItem();
                if (item1 != null)
                {
                    GenSpawn.Spawn(item1, shelf.Position, map);
                    Thing item2 = GenerateRandomItem();
                    if (item2 != null)
                    {
                        GenSpawn.Spawn(item2, shelf.Position, map);
                    }
                }
            }
        }

        /// <summary>
        /// 生成敌军增援部队
        /// </summary>
        public static void SpawnReinforcements(Map map)
        {
            // 获取鼠族军阀派系
            var ratkinFaction = Find.FactionManager.FirstFactionOfDef(FactionDef.Named("Rakinia_Warlord"));

            // 如果没有军阀派系，则查找任意一个海盗阵营
            if (ratkinFaction == null)
            {
                ratkinFaction = Find.FactionManager.FirstFactionOfDef(DefDatabase<FactionDef>.AllDefs.FirstOrDefault(o => o.defName.Contains("Pirate")));
            }

            if (ratkinFaction == null) return;

            // 找到地图上的随机边缘位置作为增援部队的生成点
            IntVec3 spawnCenter = GetRandomEdgePosition(map);
            List<Pawn> reinforcements = new List<Pawn>();

            // 生成1个RatkinKnightCommander
            var commanderDef = DefDatabase<PawnKindDef>.GetNamedSilentFail("RatkinKnightCommander");
            if (commanderDef != null)
            {
                var request = new PawnGenerationRequest(
                    commanderDef,
                    ratkinFaction,
                    PawnGenerationContext.NonPlayer
                );
                var commander = PawnGenerator.GeneratePawn(request);
                GenSpawn.Spawn(commander, spawnCenter, map);
                reinforcements.Add(commander);
            }

            // 生成12个鼠族骑士
            var knightDef = DefDatabase<PawnKindDef>.GetNamedSilentFail("RatkinKnight");
            if (knightDef != null)
            {
                for (int i = 0; i < 12; i++)
                {
                    var request = new PawnGenerationRequest(
                        knightDef,
                        ratkinFaction,
                        PawnGenerationContext.NonPlayer
                    );
                    var knight = PawnGenerator.GeneratePawn(request);
                    IntVec3 spawnPos = spawnCenter + new IntVec3(Rand.Range(-3, 4), 0, Rand.Range(-3, 4));
                    if (spawnPos.InBounds(map) && spawnPos.Standable(map))
                    {
                        GenSpawn.Spawn(knight, spawnPos, map);
                        reinforcements.Add(knight);
                    }
                }
            }

            // 生成5个守卫者
            var guardenerDef = DefDatabase<PawnKindDef>.GetNamedSilentFail("RatkinEliteGuardener");
            if (guardenerDef != null)
            {
                for (int i = 0; i < 5; i++)
                {
                    var request = new PawnGenerationRequest(
                        guardenerDef,
                        ratkinFaction,
                        PawnGenerationContext.NonPlayer
                    );
                    var guardener = PawnGenerator.GeneratePawn(request);
                    IntVec3 spawnPos = spawnCenter + new IntVec3(Rand.Range(-3, 4), 0, Rand.Range(-3, 4));
                    if (spawnPos.InBounds(map) && spawnPos.Standable(map))
                    {
                        GenSpawn.Spawn(guardener, spawnPos, map);
                        reinforcements.Add(guardener);
                    }
                }
            }

            
            if (reinforcements.Count > 0)
            {
                var assaultJob = new LordJob_AssaultColony(ratkinFaction, true, false, false, false, true);
                LordMaker.MakeNewLord(ratkinFaction, assaultJob, map, reinforcements);
            }
            Messages.Message("RKU_RatkinReinforcementsArrived".Translate(), MessageTypeDefOf.ThreatBig);
        }

        /// <summary>
        /// 获取地图边缘的随机位置作为增援部队生成点
        /// </summary>
        private static IntVec3 GetRandomEdgePosition(Map map)
        {
            for (int attempt = 0; attempt < 10; attempt++)
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

                if (pos.InBounds(map) && pos.Standable(map))
                {
                    if (map.reachability.CanReachNonLocal(pos, new TargetInfo(map.Center, map), PathEndMode.OnCell, TraverseMode.PassDoors, Danger.Deadly))
                    {
                        return pos;
                    }
                }
            }
            IntVec3 fallbackPos = Utils.FindRandomEdgeSpawnPosition(map);
            if (fallbackPos.IsValid && fallbackPos.InBounds(map))
            {
                return fallbackPos;
            }
            return map.Center;
        }

        /// <summary>
        /// 找到城堡中间的主要房间（返回指定数量的房间）
        /// </summary>
        private List<Room> FindCenterRooms(List<Room> rooms, Map map, int count)
        {
            if (rooms.Count == 0) return new List<Room>();
            IntVec3 mapCenter = new IntVec3(map.Size.x / 2, 0, map.Size.z / 2);
            return rooms.OrderByDescending(r => r.CellCount)
            .ThenBy(r => r.Cells.Min(c => c.DistanceTo(mapCenter)))
            .Take(count)
            .ToList();
        }

        /// <summary>
        /// 检查房间是否包含三张床
        /// </summary>
        private bool HasThreeBeds(Room room, Map map)
        {
            int bedCount = 0;
            foreach (var cell in room.Cells)
            {
                var building = cell.GetFirstBuilding(map);
                if (building != null && building.def.IsBed)
                {
                    bedCount++;
                    if (bedCount >= 3) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 获取房间中可用的位置
        /// </summary>
        private List<IntVec3> GetAvailableCellsInRoom(Room room, Map map, int maxCount)
        {
            var cells = room.Cells
                .Where(c => c.Standable(map) && c.GetFirstPawn(map) == null)
                .OrderBy(c => Rand.Value) // 随机排序
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
        /// 在最大房间生成一个贵族，装备极佳品质的迷幻步枪
        /// </summary>
        private void SpawnNobleWithSpecialWeapon(Map map)
        {
            var ratkinFaction = Find.FactionManager.FirstFactionOfDef(FactionDef.Named("Rakinia_Warlord"));
            if (ratkinFaction == null)
            {
                ratkinFaction = Find.FactionManager.FirstFactionOfDef(DefDatabase<FactionDef>.AllDefs.FirstOrDefault(o => o.defName.Contains("Pirate")));
            }
            if (ratkinFaction == null) return;
            var allRooms = map.regionGrid.AllRooms
                .Where(room => room.CellCount > 5 && IsIndoorRoom(room, map))
                .ToList();

            if (allRooms.Count == 0) return;
            var largestRoom = allRooms.OrderByDescending(r => r.CellCount).FirstOrDefault();
            if (largestRoom == null) return;
            var cells = GetAvailableCellsInRoom(largestRoom, map, 1);
            if (cells.Count == 0) return;

            var nobleKind = DefDatabase<PawnKindDef>.GetNamedSilentFail("RatkinNoble");
            if (nobleKind != null)
            {
                var nobleRequest = new PawnGenerationRequest(
                    nobleKind,
                    ratkinFaction,
                    PawnGenerationContext.NonPlayer
                );
                var noble = PawnGenerator.GeneratePawn(nobleRequest);
                GenSpawn.Spawn(noble, cells[0], map);
                var weaponDef = ThingDef.Named("RKU_YunNanBoltActionRifle");
                if (weaponDef != null && noble.equipment != null)
                {
                    noble.equipment.DestroyAllEquipment();
                    var weapon = ThingMaker.MakeThing(weaponDef);
                    weapon.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Excellent, ArtGenerationContext.Outsider);
                    noble.equipment.AddEquipment((ThingWithComps)weapon);
                }
                var roomCenter = GetRoomCenter(largestRoom);
                var lordJob = new LordJob_DefendPoint(roomCenter);
                LordMaker.MakeNewLord(ratkinFaction, lordJob, map, new List<Pawn> { noble });
                Messages.Message("RKU_NobleWarning".Translate(), MessageTypeDefOf.ThreatBig);
            }
        }

        public override void SitePartWorkerTick(SitePart sitePart)
        {
            base.SitePartWorkerTick(sitePart);

            Map map = sitePart.site.Map;
            if (map == null) return;

            // 处理增援触发
            if (!hasTriggeredReinforcement)
            {
                reinforcementTickCounter++;
                if (reinforcementTickCounter >= REINFORCEMENT_DELAY_TICKS)
                {
                    SpawnReinforcements(map);
                    hasTriggeredReinforcement = true;
                }
            }
        }
    }
}
