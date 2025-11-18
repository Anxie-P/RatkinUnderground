using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using UnityEngine;
using Verse;
using Verse.AI.Group;
using static RatkinUnderground.QuestNode_RKU_GuerrillasComing;
using static RatkinUnderground.Utils;

namespace RatkinUnderground
{
    public class SitePartWorker_RatkinFactory : SitePartWorker
    {
        // 状态跟踪
        private List<int> countedCorpseIds = new List<int>(); // 已统计的尸体ID列表
        private int lastRaidTick = 0; // 上次袭击的tick
        private const int CHECK_INTERVAL_TICKS = 60; // 每秒检查（60tick）
        private const int MAX_DEATHS_TO_STOP = 800; // 达到800人死亡后停止
        private const int DEATHS_FOR_FACTION_CHANGE = 300; // 300人死亡后改变派系选择
        private const int MAX_ENEMIES_FOR_NEXT_RAID = 10; // 地图上敌人数量10以下触发下次袭击
        private const int MAX_RAID_INTERVAL_TICKS = 10000; // 最大袭击间隔10000tick
        private const float BASE_RAID_POINTS = 2000f; // 基础袭击点数
        private const float MAX_RAID_POINTS = 5000f; // 最大袭击点数
        private const int DEATHS_FOR_MAX_POINTS = 500; // 500人死亡后达到最大点数

        private bool isStopped = false; // 是否已停止袭击
        private bool hasTriggeredInitialRaid = false; // 是否已触发初始袭击

        public override void PostMapGenerate(Map map)
        {
            base.PostMapGenerate(map);
            SetAllBuildingsFaction(map);
            SpawnFoodOnShelves(map);
            SpawnGuerrillas(map);
            lastRaidTick = Find.TickManager.TicksGame;
        }

        public override void SitePartWorkerTick(SitePart sitePart)
        {
            base.SitePartWorkerTick(sitePart);

            Map map = sitePart.site.Map;
            if (map == null) return;

            // 如果已达到停止条件，不再继续
            if (isStopped) return;

            // 触发初始袭击（开局时立即触发一次）
            if (!hasTriggeredInitialRaid)
            {
                TriggerInitialRaid(map);
                hasTriggeredInitialRaid = true;
                lastRaidTick = Find.TickManager.TicksGame;
                return;
            }

            // 每秒检查一次
            if (Find.TickManager.TicksGame % CHECK_INTERVAL_TICKS != 0) return;

            // 统计袭击者尸体数量
            CountEnemyCorpses(map);

            // 检查是否应该停止
            if (countedCorpseIds.Count >= MAX_DEATHS_TO_STOP)
            {
                isStopped = true;
                return;
            }

            // 检查是否应该触发下次袭击
            CheckAndTriggerNextRaid(map);
        }

        /// <summary>
        /// 统计袭击者尸体数量
        /// </summary>
        private void CountEnemyCorpses(Map map)
        {
            var playerFaction = Faction.OfPlayer;
            if (playerFaction == null) return;

            // 统计所有敌对派系的尸体
            var allCorpses = map.listerThings.AllThings
                .OfType<Corpse>()
                .Where(corpse => corpse.InnerPawn != null && 
                                corpse.InnerPawn.Faction != null && 
                                corpse.InnerPawn.Faction.HostileTo(playerFaction))
                .ToList();

            // 找出新的尸体（不在已统计列表中的）
            foreach (var corpse in allCorpses)
            {
                int corpseId = corpse.thingIDNumber;
                if (!countedCorpseIds.Contains(corpseId))
                {
                    countedCorpseIds.Add(corpseId);
                }
            }
        }

        /// <summary>
        /// 检查并触发下次袭击
        /// </summary>
        private void CheckAndTriggerNextRaid(Map map)
        {
            int currentTick = Find.TickManager.TicksGame;
            int ticksSinceLastRaid = currentTick - lastRaidTick;

            // 检查地图上的敌人数量
            int enemyCount = CountEnemiesOnMap(map);

            // 条件1：敌人数量10以下
            // 条件2：或达到最大间隔时间
            bool shouldTrigger = enemyCount <= MAX_ENEMIES_FOR_NEXT_RAID || ticksSinceLastRaid >= MAX_RAID_INTERVAL_TICKS;

            if (shouldTrigger)
            {
                TriggerRaid(map);
                lastRaidTick = currentTick;
            }
        }

        /// <summary>
        /// 统计地图上的敌人数量
        /// </summary>
        private int CountEnemiesOnMap(Map map)
        {
            var playerFaction = Faction.OfPlayer;
            if (playerFaction == null) return 0;

            return map.mapPawns.AllPawnsSpawned
                .Where(pawn => pawn.Faction != null && 
                              pawn.Faction.HostileTo(playerFaction) && 
                              !pawn.Dead && 
                              !pawn.Downed)
                .Count();
        }

        /// <summary>
        /// 触发初始袭击（开局时从地图底部进入）
        /// </summary>
        private void TriggerInitialRaid(Map map)
        {
            try
            {
                // 选择派系
                Faction raidFaction = SelectRaidFaction();
                if (raidFaction == null)
                {
                    Log.Warning("[RKU Factory] 无法找到合适的袭击派系");
                    return;
                }

                // 初始袭击使用基础点数
                float raidPoints = BASE_RAID_POINTS;

                // 强制从地图底部（南边缘）进入，距离殖民者至少25格
                IntVec3 spawnCenter = GetSouthEdgeSpawnPositionAwayFromColonists(map, 25);
                if (!spawnCenter.IsValid)
                {
                    // 如果南边缘没有可用位置，尝试其他边缘（也保持25格距离）
                    spawnCenter = GetRandomEdgePositionAwayFromColonists(map, 25);
                }

                // 创建袭击参数
                IncidentParms parms = new IncidentParms();
                parms.target = map;
                parms.faction = raidFaction;
                parms.forced = true;
                parms.points = raidPoints;
                parms.spawnCenter = spawnCenter;

                // 设置袭击策略
                RaidStrategyDef strategy = DefDatabase<RaidStrategyDef>.GetNamed("ImmediateAttack");
                if (strategy != null)
                {
                    parms.raidStrategy = strategy;
                }

                // 设置到达方式（优先使用边缘进入，不使用空投）
                PawnsArrivalModeDef arrivalMode = DefDatabase<PawnsArrivalModeDef>.AllDefs
                    .FirstOrDefault(d => !d.defName.Contains("Drop") && 
                                        (d.defName.Contains("Edge") || d.defName.Contains("WalkIn")));
                if (arrivalMode == null)
                {
                    // 如果找不到边缘进入方式，使用任何非空投方式
                    arrivalMode = DefDatabase<PawnsArrivalModeDef>.AllDefs
                        .FirstOrDefault(d => !d.defName.Contains("Drop"));
                }
                if (arrivalMode != null)
                {
                    parms.raidArrivalMode = arrivalMode;
                }

                // 发送袭击信封
                SendRaidLetter(raidFaction, raidPoints);

                // 触发袭击
                IncidentDef raidIncident = IncidentDefOf.RaidEnemy;
                if (!raidIncident.Worker.TryExecute(parms))
                {
                    Log.Warning("[RKU Factory] 初始袭击触发失败");
                }
            }
            catch (System.Exception e)
            {
                Log.Error($"[RKU Factory] 触发初始袭击时发生错误: {e.Message}\n{e.StackTrace}");
            }
        }

        /// <summary>
        /// 触发袭击
        /// </summary>
        private void TriggerRaid(Map map)
        {
            try
            {
                // 选择派系
                Faction raidFaction = SelectRaidFaction();
                if (raidFaction == null)
                {
                    Log.Warning("[RKU Factory] 无法找到合适的袭击派系");
                    return;
                }

                // 计算袭击点数
                float raidPoints = CalculateRaidPoints();

                // 获取生成位置（优先从地图下方）
                IntVec3 spawnCenter = GetSouthEdgeSpawnPosition(map);
                if (!spawnCenter.IsValid)
                {
                    spawnCenter = GetRandomEdgePosition(map);
                }

                // 创建袭击参数
                IncidentParms parms = new IncidentParms();
                parms.target = map;
                parms.faction = raidFaction;
                parms.forced = true;
                parms.points = raidPoints;
                parms.spawnCenter = spawnCenter;

                // 设置袭击策略
                RaidStrategyDef strategy = DefDatabase<RaidStrategyDef>.GetNamed("ImmediateAttack");
                if (strategy != null)
                {
                    parms.raidStrategy = strategy;
                }

                // 设置到达方式（优先使用边缘进入，不使用空投）
                PawnsArrivalModeDef arrivalMode = DefDatabase<PawnsArrivalModeDef>.AllDefs
                    .FirstOrDefault(d => !d.defName.Contains("Drop") && 
                                        (d.defName.Contains("Edge") || d.defName.Contains("WalkIn")));
                if (arrivalMode == null)
                {
                    // 如果找不到边缘进入方式，使用任何非空投方式
                    arrivalMode = DefDatabase<PawnsArrivalModeDef>.AllDefs
                        .FirstOrDefault(d => !d.defName.Contains("Drop"));
                }
                if (arrivalMode != null)
                {
                    parms.raidArrivalMode = arrivalMode;
                }

                // 发送袭击信封
                SendRaidLetter(raidFaction, raidPoints);
                IncidentDef raidIncident = IncidentDefOf.RaidEnemy;
                if (!raidIncident.Worker.TryExecute(parms))
                {
                    Log.Warning("[RKU Factory] 袭击触发失败");
                }
            }
            catch (System.Exception e)
            {
                Log.Error($"[RKU Factory] 触发袭击时发生错误: {e.Message}\n{e.StackTrace}");
            }
        }

        /// <summary>
        /// 选择袭击派系
        /// </summary>
        private Faction SelectRaidFaction()
        {
            var playerFaction = Faction.OfPlayer;
            if (playerFaction == null) return null;

            // 如果死亡人数少于300，使用军阀势力
            if (countedCorpseIds.Count < DEATHS_FOR_FACTION_CHANGE)
            {
                // 优先选择军阀势力
                var warlordFaction = Find.FactionManager.AllFactions
                    .FirstOrDefault(f => f.def.defName == "Rakinia_Warlord" && 
                                        !f.defeated && 
                                        f.HostileTo(playerFaction) && 
                                        !f.Hidden);
                
                if (warlordFaction != null)
                    return warlordFaction;

                // 如果没有军阀，尝试王国
                var kingdomFaction = Find.FactionManager.AllFactions
                    .FirstOrDefault(f => f.def.defName == "Rakinia" && 
                                        !f.defeated && 
                                        f.HostileTo(playerFaction) && 
                                        !f.Hidden);
                
                if (kingdomFaction != null)
                    return kingdomFaction;
            }

            var guerrillaFaction = Find.FactionManager.FirstFactionOfDef(DefOfs.RKU_Faction);
            
            // 筛选同时敌对玩家和游击队的派系
            var hostileFactions = Find.FactionManager.AllFactions
                .Where(f => !f.defeated && 
                           f.HostileTo(playerFaction) && 
                           (guerrillaFaction == null || f.HostileTo(guerrillaFaction)) &&
                           !f.Hidden &&
                           f.def.CanEverBeNonHostile == false) // 确保是永久敌对
                .ToList();

            if (hostileFactions.Any())
            {
                // 70%概率选择军阀
                if (Rand.Value < 0.7f)
                {
                    var warlordFaction = hostileFactions
                        .FirstOrDefault(f => f.def.defName == "Rakinia_Warlord");
                    
                    if (warlordFaction != null)
                    {
                        return warlordFaction;
                    }
                }
                
                // 30%概率或其他情况，从所有符合条件的派系中随机选择
                return hostileFactions.RandomElement();
            }

            return null;
        }

        /// <summary>
        /// 计算袭击点数
        /// </summary>
        private float CalculateRaidPoints()
        {
            if (countedCorpseIds.Count >= DEATHS_FOR_MAX_POINTS)
            {
                return MAX_RAID_POINTS;
            }

            // 线性插值
            float progress = (float)countedCorpseIds.Count / DEATHS_FOR_MAX_POINTS;
            return BASE_RAID_POINTS + (MAX_RAID_POINTS - BASE_RAID_POINTS) * progress;
        }

        /// <summary>
        /// 获取地图南边缘的生成位置，距离殖民者至少指定距离
        /// </summary>
        private IntVec3 GetSouthEdgeSpawnPositionAwayFromColonists(Map map, int minDistance)
        {
            var colonistPositions = map.mapPawns.FreeColonistsSpawned
                .Where(pawn => pawn.Spawned && !pawn.Dead)
                .Select(pawn => pawn.Position)
                .ToList();

            if (colonistPositions.Count == 0)
            {
                return GetSouthEdgeSpawnPosition(map);
            }

            for (int attempts = 0; attempts < 100; attempts++)
            {
                int x = Rand.Range(0, map.Size.x);
                IntVec3 pos = new IntVec3(x, 0, 0); 

                if (pos.InBounds(map) && pos.Standable(map))
                {
                    if (IsAccessiblePosition(map, pos))
                    {
                        bool farEnough = colonistPositions.All(colPos => 
                            pos.DistanceTo(colPos) >= minDistance);
                        
                        if (farEnough)
                        {
                            return pos;
                        }
                    }
                }
            }

            return IntVec3.Invalid;
        }

        /// <summary>
        /// 获取地图南边缘的生成位置
        /// </summary>
        private IntVec3 GetSouthEdgeSpawnPosition(Map map)
        {
            // 尝试在地图南边缘找到可进入的位置
            for (int attempts = 0; attempts < 30; attempts++)
            {
                int x = Rand.Range(0, map.Size.x);
                IntVec3 pos = new IntVec3(x, 0, 0); // 南边缘 z=0

                if (pos.InBounds(map) && pos.Standable(map))
                {
                    // 检查周围是否有可进入的路径
                    if (IsAccessiblePosition(map, pos))
                    {
                        return pos;
                    }
                }
            }

            return IntVec3.Invalid;
        }

        /// <summary>
        /// 检查位置是否可进入
        /// </summary>
        private bool IsAccessiblePosition(Map map, IntVec3 pos)
        {
            // 检查周围8格是否有可站立的位置
            for (int i = -1; i <= 1; i++)
            {
                for (int j = -1; j <= 1; j++)
                {
                    if (i == 0 && j == 0) continue;
                    IntVec3 checkPos = new IntVec3(pos.x + i, 0, pos.z + j);
                    if (checkPos.InBounds(map) && checkPos.Standable(map))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 获取地图边缘的随机位置，距离殖民者至少指定距离
        /// </summary>
        private IntVec3 GetRandomEdgePositionAwayFromColonists(Map map, int minDistance)
        {
            // 获取所有殖民者的位置
            var colonistPositions = map.mapPawns.FreeColonistsSpawned
                .Where(pawn => pawn.Spawned && !pawn.Dead)
                .Select(pawn => pawn.Position)
                .ToList();

            if (colonistPositions.Count == 0)
            {
                // 如果没有殖民者，使用普通方法
                return GetRandomEdgePosition(map);
            }

            // 尝试从各个边缘找到距离殖民者足够远的位置
            for (int attempts = 0; attempts < 100; attempts++)
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
                    // 检查是否距离所有殖民者都足够远
                    bool farEnough = colonistPositions.All(colPos => 
                        pos.DistanceTo(colPos) >= minDistance);
                    
                    if (farEnough && IsAccessiblePosition(map, pos))
                    {
                        return pos;
                    }
                }
            }

            return IntVec3.Invalid;
        }

        /// <summary>
        /// 发送袭击信封
        /// </summary>
        private void SendRaidLetter(Faction faction, float raidPoints)
        {
            string letterLabel = "RKU_EnemyRaid".Translate();
            string letterText = "RKU_EnemyRaidDesc".Translate(faction.Name, raidPoints.ToString("F0"), countedCorpseIds.Count);

            // 如果翻译不存在，使用默认文本
            if (letterLabel == "RKU_EnemyRaid")
            {
                letterLabel = "敌方袭击";
            }
            if (letterText == "RKU_EnemyRaidDesc")
            {
                letterText = $"{faction.Name}正在发起袭击！\n袭击点数: {raidPoints:F0}\n累计敌方死亡: {countedCorpseIds.Count}";
            }

            Find.LetterStack.ReceiveLetter(letterLabel, letterText, LetterDefOf.ThreatBig);
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
        /// 生成矿工
        /// </summary>
        /// <param name="map"></param>
        /// <param name="center"></param>
        /// <param name="count"></param>
        private void SpawnGuerrillas(Map map)
        {
            var allRooms = map.regionGrid.AllRooms
                .Where(room => room.CellCount > 5 && IsIndoorRoom(room, map))
                .ToList();
            List<Pawn> spawnedGuerrillas = new List<Pawn>();
            var guerrillaFaction = Find.FactionManager.FirstFactionOfDef(DefOfs.RKU_Faction);
            if (guerrillaFaction == null) return;
            foreach (var room in allRooms)
            {
                IntVec3 spawnPos = FindNearbySpawnPosition(room.Cells.RandomElement<IntVec3>(), map, 1, 5);
                if (spawnPos.IsValid)
                {
                    Pawn guerrilla = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                        DefOfs.RKU_Miner,
                        guerrillaFaction,
                        PawnGenerationContext.NonPlayer,
                        -1,
                        forceGenerateNewPawn: true));

                    GenSpawn.Spawn(guerrilla, spawnPos, map);
                    spawnedGuerrillas.Add(guerrilla);
                }
            }
            Lord lord = LordMaker.MakeNewLord(
            guerrillaFaction,
            new LordJob_DefendBase(guerrillaFaction, map.Center, 0),
            map,
            spawnedGuerrillas);

        }


        /// <summary>
        /// <summary>
        /// 在物品架上生成食品
        /// </summary>
        private void SpawnFoodOnShelves(Map map)
        {
            // 找到地图上所有的物品架
            var shelves = map.listerThings.ThingsOfDef(ThingDef.Named("Shelf")).ToList();

            if (shelves.Count == 0) return;
            var selectedShelves = shelves.Where(_ => Rand.Value < 0.5f).ToList();
            var foodDefs = new List<ThingDef>
            {
                ThingDefOf.MealSimple,
                ThingDefOf.MealFine,
                ThingDefOf.Pemmican,
            };

            foreach (var shelf in selectedShelves)
            {
                int foodCount = Rand.RangeInclusive(1, 3);
                for (int i = 0; i < foodCount; i++)
                {
                    ThingDef foodDef = foodDefs.RandomElement();
                    Thing food = ThingMaker.MakeThing(foodDef);
                    food.stackCount = Rand.RangeInclusive(1, foodDef.stackLimit);
                    GenSpawn.Spawn(food, shelf.Position, map);
                }
            }
        }


        /// 获取地图边缘的随机位置
        /// </summary>
        private IntVec3 GetRandomEdgePosition(Map map)
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
