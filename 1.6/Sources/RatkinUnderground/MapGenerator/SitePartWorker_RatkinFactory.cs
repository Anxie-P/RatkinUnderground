using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using static RatkinUnderground.QuestNode_RKU_GuerrillasComing;
using static RatkinUnderground.Utils;

namespace RatkinUnderground
{
    public class SitePartWorker_RatkinFactory : SitePartWorker
    {
        private static Dictionary<SitePart, bool> destroyedTurretsState = new Dictionary<SitePart, bool>();
        // 状态跟踪
        private List<int> countedCorpseIds = new List<int>(); // 已统计的尸体ID列表
        private int lastRaidTick = 0; // 上次袭击的tick
        private const int CHECK_INTERVAL_TICKS = 60; // 每秒检查（60tick）
        private const int MAX_DEATHS_TO_STOP = 600; // 达到600人死亡后停止
        private const int DEATHS_FOR_FACTION_CHANGE = 300; // 300人死亡后改变派系选择
        private const int MAX_ENEMIES_FOR_NEXT_RAID = 12; // 地图上敌人数量10以下触发下次袭击
        private const int MAX_RAID_INTERVAL_TICKS = 10000; // 最大袭击间隔10000tick
        private const float BASE_RAID_POINTS = 2000f; // 基础袭击点数
        private const float MAX_RAID_POINTS = 5000f; // 最大袭击点数
        private const int DEATHS_FOR_MAX_POINTS = 500; // 500人死亡后达到最大点数

        private bool isStopped = false; // 是否已停止袭击
        private bool hasTriggeredInitialRaid = false; // 是否已触发初始袭击

        public override void PostMapGenerate(Map map)
        {
            base.PostMapGenerate(map);
            Utils.ClearNonFactionPawns(map, new List<Faction> { Faction.OfPlayer });
            SetAllBuildingsFaction(map);
            SpawnFoodOnShelves(map);
            SpawnGuerrillas(map);
            SpawnPrototypeWeapon(map);
            SpawnEnemyTurret(map);
            lastRaidTick = Find.TickManager.TicksGame;
        }


        public override void SitePartWorkerTick(SitePart sitePart)
        {
            base.SitePartWorkerTick(sitePart);

            Map map = sitePart.site.Map;
            if (map == null) return;

            // 如果已达到停止条件，不再继续
            if (isStopped)
            {
                // 检查是否已摧毁机枪塔（使用静态字典保存状态）
                bool hasDestroyed = false;
                destroyedTurretsState.TryGetValue(sitePart, out hasDestroyed);

                if (!hasDestroyed)
                {
                    DestroyAllMiniTurrets(map);
                    destroyedTurretsState[sitePart] = true;
                }
                return;
            }

            // 触发初始袭击（开局时立即触发一次）
            if (!hasTriggeredInitialRaid)
            {
                TriggerInitialRaid(map);
                hasTriggeredInitialRaid = true;
                lastRaidTick = Find.TickManager.TicksGame;
                return;
            }
            if (Find.TickManager.TicksGame % CHECK_INTERVAL_TICKS != 0) return;
            CountEnemyCorpses(map);
            if (countedCorpseIds.Count >= MAX_DEATHS_TO_STOP)
            {
                isStopped = true;
                return;
            }
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
            int enemyCount = CountEnemiesOnMap(map);
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
                Faction raidFaction = SelectRaidFaction();
                if (raidFaction == null)
                {
                    Log.Warning("[RKU Factory] 无法找到合适的袭击派系");
                    return;
                }
                float raidPoints = BASE_RAID_POINTS;
                IntVec3 spawnCenter = GetSouthEdgeSpawnPositionAwayFromColonists(map, 25);
                if (!spawnCenter.IsValid || !spawnCenter.InBounds(map) || !spawnCenter.Standable(map))
                {
                    spawnCenter = GetRandomEdgePositionAwayFromColonists(map, 25);
                }
                if (!spawnCenter.IsValid || !spawnCenter.InBounds(map) || !spawnCenter.Standable(map))
                {
                    spawnCenter = GetSouthEdgeSpawnPosition(map);
                }
                if (!spawnCenter.IsValid || !spawnCenter.InBounds(map) || !spawnCenter.Standable(map))
                {
                    spawnCenter = GetRandomEdgePosition(map);
                }

                IncidentParms parms = new IncidentParms();
                parms.target = map;
                parms.faction = raidFaction;
                parms.forced = true;
                parms.points = raidPoints;
                parms.spawnCenter = spawnCenter;
                RaidStrategyDef strategy = DefDatabase<RaidStrategyDef>.GetNamed("ImmediateAttack");
                if (strategy != null)
                {
                    parms.raidStrategy = strategy;
                }

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
                SendRaidLetter(raidFaction, raidPoints);
                IncidentDef raidIncident = IncidentDefOf.RaidEnemy;
                if (!raidIncident.Worker.TryExecute(parms))
                {
                    Log.Warning("[RKU Factory] 袭击触发失败");
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
                Faction raidFaction = SelectRaidFaction();
                if (raidFaction == null)
                {
                    Log.Warning("[RKU Factory] 无法找到合适的袭击派系");
                    return;
                }
                float raidPoints = CalculateRaidPoints();

                // 获取生成位置（优先从地图下方）
                IntVec3 spawnCenter = GetSouthEdgeSpawnPosition(map);

              

                if (!spawnCenter.IsValid || !spawnCenter.InBounds(map) || !spawnCenter.Standable(map))
                {
                    IntVec3 result = new IntVec3();
                    CellFinder.TryFindRandomEdgeCellWith((IntVec3 x) => x.Standable(map) && map.reachability.CanReachColony(x), map, CellFinder.EdgeRoadChance_Hostile, out result);
                    spawnCenter = result;
                }
                if (!spawnCenter.IsValid || !spawnCenter.InBounds(map) || !spawnCenter.Standable(map))
                {
                    spawnCenter = map.Center;
                }
                IncidentParms parms = new IncidentParms();
                parms.target = map;
                parms.faction = raidFaction;
                parms.forced = true;
                parms.points = raidPoints;
                parms.spawnCenter = spawnCenter;
                RaidStrategyDef strategy = DefDatabase<RaidStrategyDef>.GetNamed("ImmediateAttack");
                if (strategy != null)
                {
                    parms.raidStrategy = strategy;
                }
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
                           f.def.CanEverBeNonHostile == false)
                .ToList();

            if (hostileFactions.Any())
            {
                if (Rand.Value < 0.7f)
                {
                    var warlordFaction = hostileFactions
                        .FirstOrDefault(f => f.def.defName == "Rakinia_Warlord");
                    
                    if (warlordFaction != null)
                    {
                        return warlordFaction;
                    }
                }
                
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
        /// </summary>
        private IntVec3 GetSouthEdgeSpawnPosition(Map map)
        {
            for (int attempts = 0; attempts < 50; attempts++)
            {
                int x = Rand.Range(0, map.Size.x);
                IntVec3 pos = new IntVec3(x, 0, 0); // 南边缘 z=0

                if (pos.InBounds(map) && pos.Standable(map))
                {
                    if (map.reachability.CanReachNonLocal(pos, new TargetInfo(map.Center, map), PathEndMode.OnCell, TraverseMode.PassDoors, Danger.Deadly))
                    {
                        return pos;
                    }
                }
            }

            IntVec3 fallbackPos = GetRandomEdgePosition(map);
            if (fallbackPos.IsValid && fallbackPos.InBounds(map))
            {
                return fallbackPos;
            }
            fallbackPos = Utils.FindRandomEdgeSpawnPosition(map);
            if (fallbackPos.IsValid && fallbackPos.InBounds(map))
            {
                return fallbackPos;
            }

            return map.Center;
        }

        /// <summary>
        /// 检查位置是否可进入
        /// </summary>
        private bool IsAccessiblePosition(Map map, IntVec3 pos)
        {
            bool hasNearbyStandable = false;
            for (int i = -1; i <= 1; i++)
            {
                for (int j = -1; j <= 1; j++)
                {
                    if (i == 0 && j == 0) continue;
                    IntVec3 checkPos = new IntVec3(pos.x + i, 0, pos.z + j);
                    if (checkPos.InBounds(map) && checkPos.Standable(map))
                    {
                        hasNearbyStandable = true;
                        break;
                    }
                }
            }
            if (!hasNearbyStandable) return false;
            return map.reachability.CanReachNonLocal(pos, new TargetInfo(map.Center, map), PathEndMode.OnCell, TraverseMode.PassDoors, Danger.Deadly);
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
                return GetRandomEdgePosition(map);
            }
            // 尝试从各个边缘找到距离殖民者足够远的位置
            for (int attempts = 0; attempts < 100; attempts++)
            {
                int edge = Rand.Range(0, 4);
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
            IntVec3 fallbackPos = GetRandomEdgePosition(map);
            if (fallbackPos.IsValid && fallbackPos.InBounds(map))
            {
                return fallbackPos;
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
        /// 将地图上的炮塔和钻机归属于游击队派系
        /// </summary>
        private void SetAllBuildingsFaction(Map map)
        {
            var guerrillaFaction = Find.FactionManager.FirstFactionOfDef(DefOfs.RKU_Faction);
            if (guerrillaFaction == null) return;

            // 定义需要改变归属的建筑类型
            string[] turretDefs = {
                "Turret_AutoMiniTurret",
                "Turret_Sniper",
                "Turret_Autocannon",
                "Turret_MiniTurret",
                "Turret_Mortar"
            };
            string[] drillDefs = {
                "RKU_DrillingVehicle",
                "RKU_DrillingVehicleCargo",
                "RKU_DrillingVehicleWithTurret"
            };

            var allBuildings = map.listerBuildings.allBuildingsColonist.Concat(map.listerBuildings.allBuildingsNonColonist).ToList();
            foreach (var building in allBuildings)
            {
                if (building != null && building.def != null)
                {
                    string defName = building.def.defName;
                    if (turretDefs.Contains(defName) || drillDefs.Contains(defName))
                    {
                        building.SetFaction(guerrillaFaction);
                    }else { 
                        building.SetFaction(null);
                    }
                }
            }
        }
        /// <summary>
        /// 生成游击队单位并让它们守卫各自的房间
        /// </summary>
        /// <param name="map"></param>
        private void SpawnGuerrillas(Map map)
        {
            var guerrillaFaction = Find.FactionManager.FirstFactionOfDef(DefOfs.RKU_Faction);
            if (guerrillaFaction == null) return;

            // 找到所有室内房间
            var allRooms = map.regionGrid.AllRooms
                .Where(room => room.CellCount > 5 && IsIndoorRoom(room, map))
                .ToList();

            if (allRooms.Count == 0) return;

            // 为每个房间生成防御部队
            foreach (var room in allRooms)
            {
                SpawnDefensiveForceInRoom(room, map, guerrillaFaction);
            }
        }

        /// <summary>
        /// 在房间中生成防御部队
        /// </summary>
        private void SpawnDefensiveForceInRoom(Room room, Map map, Faction faction)
        {
            // 根据房间大小决定生成单位数量
            int unitCount = Math.Min(6, room.CellCount / 60); 
            
            // 游击队战斗单位列表
            string[] combatantKinds = {
                "RKU_Scout", "RKU_Invader", "RKU_Commissar",
                "RKU_EliteScout", "RKU_EliteInvader", "RKU_EliteCommissar"
            };

            var cells = room.Cells.Where(c => c.Standable(map) && c.GetFirstPawn(map) == null).ToList();
            cells.Shuffle();
            List<Pawn> roomDefenders = new List<Pawn>();

            for (int i = 0; i < unitCount && i < cells.Count; i++)
            {
                string kindDefName = combatantKinds.RandomElement();
                var kindDef = DefDatabase<PawnKindDef>.GetNamedSilentFail(kindDefName);

                if (kindDef != null)
                {
                    var request = new PawnGenerationRequest(
                        kindDef,
                        faction,
                        PawnGenerationContext.NonPlayer
                    );

                    var pawn = PawnGenerator.GeneratePawn(request);
                    GenSpawn.Spawn(pawn, cells[i], map);
                    roomDefenders.Add(pawn);
                }
            }

            if (roomDefenders.Count > 0)
            {
                var leader = roomDefenders[0];
                var lordJob = new LordJob_DefendPoint(leader.Position);
                LordMaker.MakeNewLord(faction, lordJob, map, roomDefenders);
            }
        }


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


        /// <summary>
        /// 获取地图边缘的随机位置（带路径验证）
        /// </summary>
        private IntVec3 GetRandomEdgePosition(Map map)
        {
            for (int attempt = 0; attempt < 70; attempt++)
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
                    return pos;
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
        /// 在随机物品架上生成大师品质的毁灭地雷发射器
        /// </summary>
        private void SpawnPrototypeWeapon(Map map)
        {
            var shelves = map.listerThings.ThingsOfDef(ThingDef.Named("Shelf")).ToList();
            if (shelves.Count == 0) return;
            var selectedShelf = shelves.RandomElement();
            var weaponDef = ThingDef.Named("RKK_Weapon_DetroyerLandmineLaunchers");
            if (weaponDef != null)
            {
                var weapon = ThingMaker.MakeThing(weaponDef);
                weapon.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Masterwork, ArtGenerationContext.Outsider);
                GenSpawn.Spawn(weapon, selectedShelf.Position, map);
                Messages.Message("RKU_FactoryWeaponMessage".Translate(), MessageTypeDefOf.PositiveEvent);
            }
        }

        /// <summary>
        /// 在地图边缘生成敌对的机枪塔
        /// </summary>
        private void SpawnEnemyTurret(Map map)
        {
            Faction enemyFaction = SelectRaidFaction();
            if (enemyFaction == null) return;
            IntVec3 spawnPos = GetRandomEdgePosition(map);
            if (!spawnPos.IsValid || !spawnPos.InBounds(map) || !spawnPos.Standable(map))
                return;
            var turretDef = ThingDef.Named("Turret_MiniTurret");
            if (turretDef != null)
            {
                var turret = ThingMaker.MakeThing(turretDef,ThingDefOf.Steel);
                turret.SetFaction(enemyFaction);
                GenSpawn.Spawn(turret, spawnPos, map);
                var powerComp = turret.TryGetComp<CompPowerTrader>();
                if (powerComp != null)
                {
                    powerComp.PowerOn = true;
                }
            }
        }

        /// <summary>
        /// 摧毁地图上所有的小机枪塔
        /// </summary>
        private void DestroyAllMiniTurrets(Map map)
        {
            var miniTurrets = map.listerThings.ThingsOfDef(ThingDef.Named("Turret_MiniTurret")).ToList();
            foreach (var turret in miniTurrets)
            {
                if (turret != null && turret.Spawned)
                {
                    turret.Destroy();
                }
            }
            // 发送关于战争没有停止的信封
            string letterLabel = "RKU_FactoryWarLetter".Translate();
            string letterText = "RKU_FactoryWarContinues".Translate();
            Find.LetterStack.ReceiveLetter(letterLabel, letterText, LetterDefOf.PositiveEvent);
        }
    }
}
