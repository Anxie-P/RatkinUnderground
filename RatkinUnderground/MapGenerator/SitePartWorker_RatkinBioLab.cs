using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RatkinUnderground
{
    public class SitePartWorker_RatkinBioLab : SitePartWorker
    {
        private int tickCounter = 0;
        private const int TRIGGER_DELAY_TICKS = 500;
        private bool hasTriggeredIncident = false;
        //private List<CellRect> roomCells = new List<CellRect>();

        private int assaultTickCounter = 0;
        private const int ASSAULT_DELAY_TICKS = 5000;

        public override void PostMapGenerate(Map map)
        {
            base.PostMapGenerate(map);
            map.fogGrid.ClearAllFog();
            ProcessBioLabMap(map);
        }

        /// <summary>
        /// 在地图生成完成后处理特殊逻辑
        /// </summary>
        /// <param name="map">地图</param>
        private void ProcessBioLabMap(Map map)
        {
            SpawnCombatantsInUpperRooms(map);
            SpawnExperimentSubjectsNearBeds(map);
            var cellRect = new CellRect(63, 63, 125, 125);
            map.fogGrid.Refog(cellRect);
            /*if (roomCells!= null)
            {
                foreach(var cell in roomCells)
                {
                    map.fogGrid.Refog(cell);
                }
            }*/
        }

        private void SpawnCombatantsInUpperRooms(Map map)
        {
            // 获取鼠族王国派系
            var ratkinFaction = Find.FactionManager.FirstFactionOfDef(FactionDef.Named("Rakinia"));
            if (ratkinFaction == null) return;

            // 找到包含两张豪华双人床的房间
            var nobleRoom = FindRoomWithRoyalBeds(map, 2);
            var allUpperRooms = map.regionGrid.AllRooms
                .Where(room => room.CellCount > 5 &&
                              room.Cells.Any(cell => cell.z > 125) &&
                              IsIndoorRoom(room, map))
                .ToList();

            // 移除最大的房间
            if (allUpperRooms.Count > 0)
            {
                var largestRoom = allUpperRooms
                    .Where(room => nobleRoom == null || room != nobleRoom)
                    .OrderByDescending(room => room.CellCount)
                    .FirstOrDefault();

                if (largestRoom != null)
                {
                    allUpperRooms.Remove(largestRoom);
                }
            }

            if (nobleRoom != null)
            {
                SpawnNobleAndKnightsInRoom(nobleRoom, map, ratkinFaction);
                allUpperRooms.Remove(nobleRoom); // 从列表中移除，避免重复处理
            }

            // 为其他房间生成防御部队
            foreach (var room in allUpperRooms.Where(r => r.CellCount > 5))
            {
                // Log.Message($"[RKU] 在房间生成防御部队，房间ID: {room.ID},BorderCells:{room.BorderCells.First()}, cellCount : {room.CellCount}");
                SpawnDefensiveForceInRoom(room, map, ratkinFaction);
            }
        }

        private Room FindRoomWithRoyalBeds(Map map, int minBedCount)
        {
            foreach (var room in map.regionGrid.AllRooms)
            {
                if (room.CellCount <= 5 || !IsIndoorRoom(room, map)) continue;

                int royalBedCount = 0;
                foreach (var cell in room.Cells)
                {
                    var things = cell.GetThingList(map);
                    foreach (var thing in things)
                    {
                        if (thing.def?.defName == "RoyalBed")
                        {
                            royalBedCount++;
                            if (royalBedCount >= minBedCount)
                            {
                                return room;
                            }
                        }
                    }
                }
            }
            return null;
        }

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

        private void SpawnNobleAndKnightsInRoom(Room room, Map map, Faction faction)
        {
            var cells = room.Cells.Where(c => c.Standable(map) && c.GetFirstPawn(map) == null).ToList();
            if (cells.Count < 5) return; // 需要5个位置：贵族 + 3个骑士 + 1个骑士长

            // 打乱可用位置顺序，实现随机分布
            cells.Shuffle();

            List<Pawn> nobleGroup = new List<Pawn>();

            // 生成贵族
            var nobleKind = DefDatabase<PawnKindDef>.GetNamedSilentFail("RatkinNoble");
            Pawn noble = null;
            if (nobleKind != null && cells.Count > 0)
            {
                var nobleRequest = new PawnGenerationRequest(
                    nobleKind,
                    faction,
                    PawnGenerationContext.NonPlayer
                );
                noble = PawnGenerator.GeneratePawn(nobleRequest);
                GenSpawn.Spawn(noble, cells[0], map);
                nobleGroup.Add(noble);
                cells.RemoveAt(0); // 移除已使用的位置
            }

            // 生成三个骑士
            var knightKind = DefDatabase<PawnKindDef>.GetNamedSilentFail("RatkinKnight");
            for (int i = 0; i < 3 && cells.Count > 0; i++)
            {
                if (knightKind != null)
                {
                    var knightRequest = new PawnGenerationRequest(
                        knightKind,
                        faction,
                        PawnGenerationContext.NonPlayer
                    );
                    var knight = PawnGenerator.GeneratePawn(knightRequest);
                    GenSpawn.Spawn(knight, cells[0], map);
                    nobleGroup.Add(knight);
                    cells.RemoveAt(0); // 移除已使用的位置
                }
            }

            // 生成特殊的骑士长护卫
            var knightCommanderKind = DefDatabase<PawnKindDef>.GetNamedSilentFail("RatkinKnightCommander");
            if (knightCommanderKind != null && cells.Count > 0)
            {
                var commanderRequest = new PawnGenerationRequest(
                    knightCommanderKind,
                    faction,
                    PawnGenerationContext.NonPlayer
                );
                var knightCommander = PawnGenerator.GeneratePawn(commanderRequest);

                // 设置特殊属性
                knightCommander.story.HairColor = Color.white;
                if (knightCommander.Name is NameTriple nameTriple)
                {
                    knightCommander.Name = new NameTriple(nameTriple.First, nameTriple.Nick, "Raeline".Translate());
                }

                // 添加RKU_CombatEfficiency Hediff
                HediffDef combatEfficiencyDef = DefDatabase<HediffDef>.GetNamedSilentFail("RKU_CombatEfficiency");
                if (combatEfficiencyDef != null)
                {
                    Hediff combatEfficiency = HediffMaker.MakeHediff(combatEfficiencyDef, knightCommander);
                    combatEfficiency.Severity = 1.0f;
                    knightCommander.health.AddHediff(combatEfficiency);
                }
                knightCommander.story.traits.GainTrait(new Trait(DefDatabase<TraitDef>.GetNamed("Tough")));
                GenSpawn.Spawn(knightCommander, cells[0], map);
                nobleGroup.Add(knightCommander);
            }

            // 为贵族和骑士组创建防御Lord
            if (noble != null && nobleGroup.Count > 0)
            {
                var lordJob = new LordJob_DefendPoint(noble.Position);
                LordMaker.MakeNewLord(faction, lordJob, map, nobleGroup);
            }
        }

        private void SpawnDefensiveForceInRoom(Room room, Map map, Faction faction)
        {
            // 根据房间大小决定生成单位数量
            int unitCount = Math.Min(12, room.CellCount / 50); // 每50个格子至少生成1个单位
            // 鼠族战斗单位列表
            string[] combatantKinds = {
                "RatkinCombatant", "RatkinVanguard", "RatkinDemonMan",
                "RatkinEliteDefender", "RatkinEliteGuardener",
                "RatkinKnight"
            };

            var cells = room.Cells.Where(c => c.Standable(map) &&
                                         c.GetFirstPawn(map) == null).ToList();

            // 打乱可用位置顺序，实现随机分布
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

            // 为房间创建防御Lord
            if (roomDefenders.Count > 0)
            {
                var leader = roomDefenders[0];
                var lordJob = new LordJob_DefendPoint(leader.Position);
                LordMaker.MakeNewLord(faction, lordJob, map, roomDefenders);
            }
        }

        private void SpawnExperimentSubjectsNearBeds(Map map)
        {
            // 获取地图下半部分的所有床
            var beds = map.listerBuildings.allBuildingsNonColonist
                .Where(bed => bed.Position.z <= 120 &&
                bed.def == ThingDefOf.Bed)
                .ToList();

            foreach (var bed in beds)
            {
                if (Rand.Range(0, 100) > 50) SpawnConfusedRat(bed.Position, map);
            }
        }

        private void SpawnConfusedRat(IntVec3 spawnPos, Map map)
        {
            PawnKindDef ratkinKind = DefDatabase<PawnKindDef>.GetNamed("RatkinSubject");
            PawnGenerationRequest request = new PawnGenerationRequest(
                ratkinKind,
                null,
                PawnGenerationContext.NonPlayer,
                fixedGender: Gender.Male,
                fixedBiologicalAge: Rand.Range(15, 25),
                fixedChronologicalAge: Rand.Range(15, 25)
            );

            Pawn ratkinPawn = PawnGenerator.GeneratePawn(request);
            GenSpawn.Spawn(ratkinPawn, spawnPos, map);
            ratkinPawn.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Wander_Psychotic/*DefDatabase<MentalStateDef>.AllDefs.RandomElement()*/);
            ratkinPawn.story.traits.GainTrait(new Trait(DefDatabase<TraitDef>.GetNamed("Tough")));
            ratkinPawn.Name = new NameTriple((ratkinPawn.Name as NameTriple).First, (ratkinPawn.Name as NameTriple).First, "Raeline".Translate());
            (ratkinPawn.Position.GetFirstBuilding(map) as Building_Bed).ForPrisoners = true;
            //ratkinPawn.guest.guestStatusInt = GuestStatus.Prisoner;
            (ratkinPawn.Position.GetFirstBuilding(map) as Building_Bed).GetComp<CompAssignableToPawn>().TryAssignPawn(ratkinPawn);
            HediffDef experimentHediff = DefDatabase<HediffDef>.GetNamed("RKU_CombatEfficiency");
            if (experimentHediff != null)
            {
                Hediff hediff = HediffMaker.MakeHediff(experimentHediff, ratkinPawn);
                hediff.Severity = Rand.Range(0.01f, 0.99f);
                ratkinPawn.health.AddHediff(hediff);
            }
        }

        private void TriggerGuerrillaIncident(Map map)
        {
            IncidentDef siegeIncident = IncidentDefOf.RaidFriendly;
            IncidentParms parms = new IncidentParms();
            parms.target = map;
            parms.forced = true;
            parms.points = 4000f;
            RaidStrategyDef siegeStrategy = DefDatabase<RaidStrategyDef>.GetNamed("Siege");
            if (siegeStrategy != null)
            {
                parms.raidStrategy = siegeStrategy;
            }
                parms.faction = Utils.OfRKU;
            if (!siegeIncident.Worker.TryExecute(parms))
            {
                Log.Warning("[RKU] 迫击炮围攻袭击触发失败");
            }
        }

        private void TriggerRoomAssault(Map map)
        {
            // 获取鼠族王国派系
            var ratkinFaction = Find.FactionManager.FirstFactionOfDef(FactionDef.Named("Rakinia"));
            if (ratkinFaction == null) return;
            var hostilePawns = map.mapPawns.AllPawnsSpawned
                .Where(p => p.Faction != null && p.Faction.HostileTo(ratkinFaction) && !p.Downed)
                .ToList();

            if (hostilePawns.Count == 0) return;
            var allRatkins = map.mapPawns.AllPawnsSpawned
                .Where(p => p.Faction == ratkinFaction && !p.Downed)
                .ToList();
            if (allRatkins.Count == 0) return;
            var ratkinRooms = map.regionGrid.AllRooms
                .Where(room => room.CellCount > 5 &&
                              allRatkins.Any(p => room.ContainsCell(p.Position)))
                .ToList();
            if (ratkinRooms.Count == 0) return;
            var selectedRoom = ratkinRooms.RandomElement();
            var roomRatkins = allRatkins
                .Where(p => selectedRoom.ContainsCell(p.Position))
                .ToList();
            if (roomRatkins.Count == 0) return;

            foreach (var pawn in roomRatkins)
            {
                if (pawn.GetLord() != null)
                {
                    pawn.GetLord().RemovePawn(pawn);
                }
            }

            var assaultJob = new LordJob_AssaultColony(ratkinFaction, true, false, false, false, true);
            LordMaker.MakeNewLord(ratkinFaction, assaultJob, map, roomRatkins);
            Messages.Message($"一些敌对鼠族开始突击了", roomRatkins[0], MessageTypeDefOf.NeutralEvent); 
        }

        public override void SitePartWorkerTick(SitePart sitePart)
        {
            base.SitePartWorkerTick(sitePart);

            Map map = sitePart.site.Map;
            if (map == null) return;

            // 处理初始incident触发
            if (!hasTriggeredIncident)
            {
                tickCounter++;
                if (tickCounter >= TRIGGER_DELAY_TICKS)
                {
                    TriggerGuerrillaIncident(map);
                    hasTriggeredIncident = true;
                }
            }

            // 处理周期性突击逻辑
            assaultTickCounter++;
            if (assaultTickCounter >= ASSAULT_DELAY_TICKS)
            {
                TriggerRoomAssault(map);
                assaultTickCounter = 0;
            }
        }

    }
}
