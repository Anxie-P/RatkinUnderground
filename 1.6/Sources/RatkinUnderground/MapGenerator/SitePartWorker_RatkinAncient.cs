using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using static RatkinUnderground.Utils;

namespace RatkinUnderground
{
    public class SitePartWorker_RatkinAncient : SitePartWorker
    {
        private int assaultTickCounter = 0;
        private const int ASSAULT_DELAY_TICKS = 4000; // 4000 ticks (约6.7分钟) 后触发友军突击
        private bool hasTriggeredReinforcement = false; // 确保援军只来一次
        public override void PostMapGenerate(Map map)
        {
            base.PostMapGenerate(map);
            //Utils.ClearNonFactionPawns(map, new List<Faction> { Faction.OfPlayer });
            SpawnEnemiesInAncientFacility(map);
            SpawnPrototypeWeapon(map);
        }

        /// <summary>
        /// 在古代设施中生成敌人
        /// </summary>
        /// <param name="map">地图</param>
        private void SpawnEnemiesInAncientFacility(Map map)
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

            // 找到最大的3个房间作为主要防守区域
            var mainRooms = allRooms.OrderByDescending(r => r.CellCount).Take(3).ToList();

            var roomDefenders = new Dictionary<Room, List<Pawn>>();

            // 在主要房间中生成敌人
            foreach (var room in mainRooms)
            {
                var defenders = new List<Pawn>();
                int defenderCount = Rand.RangeInclusive(4, 5);
                var availableCells = GetAvailableCellsInRoom(room, map, defenderCount);

                // 古代设施守卫单位列表
                string[] defenderKinds = {
                    "RatkinEliteDefender",
                    "RatkinEliteGuardener",
                    "RatkinKnight",
                    "RatkinDemonMan",
                    "RatkinVanguard"
                };

                for (int i = 0; i < defenderCount && i < availableCells.Count; i++)
                {
                    string kindDefName = defenderKinds.RandomElement();
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

                        // 根据技能等级更换武器
                        UpgradeWeaponBasedOnSkills(pawn);
                        // 检查并销毁盾牌装备
                        CheckAndDestroyShields(pawn);

                        defenders.Add(pawn);
                    }
                }
                roomDefenders[room] = defenders;
            }

            // 在其他房间中生成少量巡逻单位
            var patrolRooms = allRooms.Except(mainRooms).ToList();
            foreach (var room in patrolRooms)
            {
                if (Rand.Value < 0.5f) // 50%概率在房间中生成巡逻单位
                {
                    var patrolDefenders = new List<Pawn>();
                    int patrolCount = Rand.RangeInclusive(2, 3);
                    var patrolCells = GetAvailableCellsInRoom(room, map, patrolCount);

                    for (int i = 0; i < patrolCount && i < patrolCells.Count; i++)
                    {
                        var combatantDef = DefDatabase<PawnKindDef>.GetNamedSilentFail("RatkinCombatant");
                        if (combatantDef != null)
                        {
                            var request = new PawnGenerationRequest(
                                combatantDef,
                                ratkinFaction,
                                PawnGenerationContext.NonPlayer
                            );

                            var pawn = PawnGenerator.GeneratePawn(request);
                            GenSpawn.Spawn(pawn, patrolCells[i], map);

                            // 根据技能等级更换武器
                            UpgradeWeaponBasedOnSkills(pawn);
                            // 检查并销毁盾牌装备
                            CheckAndDestroyShields(pawn);

                            patrolDefenders.Add(pawn);
                        }
                    }
                    roomDefenders[room] = patrolDefenders;
                }
            }

            // 为每个房间的敌人创建守卫任务
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

            // 将地图上所有建筑归属于ratkinFaction
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
        /// 根据技能等级升级武器
        /// </summary>
        private void UpgradeWeaponBasedOnSkills(Pawn pawn)
        {
            if (pawn == null || pawn.equipment == null) return;

            var shootingSkill = pawn.skills.GetSkill(SkillDefOf.Shooting);
            var meleeSkill = pawn.skills.GetSkill(SkillDefOf.Melee);

            if (shootingSkill == null || meleeSkill == null) return;
            if (shootingSkill.Level >= meleeSkill.Level - 1)
            {
                ThingDef weaponDef;
                if (Rand.Value < 0.5f)
                {
                    weaponDef = ThingDef.Named("Gun_ChargeRifle");
                }
                else
                {
                    weaponDef = ThingDef.Named("Gun_Minigun");
                }

                if (weaponDef != null)
                {
                    pawn.equipment.DestroyAllEquipment();
                    var weapon = ThingMaker.MakeThing(weaponDef);
                    weapon.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Excellent, ArtGenerationContext.Outsider);
                    pawn.equipment.AddEquipment((ThingWithComps)weapon);
                    // 保底射击技能等级为3
                    if (shootingSkill.Level < 3)
                    {
                        shootingSkill.Level = 3;
                    }
                    // 如果没有头盔，给高级头盔
                    EquipAdvancedHelmetIfNeeded(pawn);
                }
            }
            else if (meleeSkill.Level > shootingSkill.Level - 1)
            {
                var shotgunDef = ThingDef.Named("Gun_ChainShotgun");
                if (shotgunDef != null)
                {
                    pawn.equipment.DestroyAllEquipment();
                    var shotgun = ThingMaker.MakeThing(shotgunDef);
                    shotgun.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Excellent, ArtGenerationContext.Outsider);
                    pawn.equipment.AddEquipment((ThingWithComps)shotgun);

                    // 保底射击技能等级为3
                    if (shootingSkill.Level < 3)
                    {
                        shootingSkill.Level = 3;
                    }
                    EquipAdvancedHelmetIfNeeded(pawn);
                }
            }
        }

        /// <summary>
        /// 如果敌人没有头盔
        /// </summary>
        private void EquipAdvancedHelmetIfNeeded(Pawn pawn)
        {
            if (pawn == null || pawn.apparel == null) return;
            bool hasHelmet = false;
            foreach (var apparel in pawn.apparel.WornApparel)
            {
                if (apparel.def.apparel.bodyPartGroups.Any(group => group.defName == "Head" || group.defName == "FullHead"))
                {
                    hasHelmet = true;
                    break;
                }
            }
            if (!hasHelmet)
            {
                var helmetDef = ThingDef.Named("Apparel_AdvancedHelmet");
                if (helmetDef != null)
                {
                    var helmet = ThingMaker.MakeThing(helmetDef, ThingDefOf.Steel);
                    helmet.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Excellent, ArtGenerationContext.Outsider);
                    pawn.apparel.Wear((Apparel)helmet);
                }
            }
        }

        /// <summary>
        /// 检查并销毁盾牌装备
        /// </summary>
        private void CheckAndDestroyShields(Pawn pawn)
        {
            if (pawn == null || pawn.equipment == null) return;

            var equipmentToDestroy = new List<ThingWithComps>();

            foreach (var equipment in pawn.equipment.AllEquipmentListForReading)
            {
                if (equipment.def.defName.ToLower().Contains("shield"))
                {
                    equipmentToDestroy.Add(equipment);
                }
            }

            foreach (var equipment in equipmentToDestroy)
            {
                pawn.equipment.Remove(equipment);
                equipment.Destroy();
            }
        }

        /// <summary>
        /// 友军突击逻辑 - 生成新的友军并直接攻击
        /// </summary>
        private void TriggerAllyAssault(Map map)
        {
            // 获取游击队派系
            var ratkinFaction = Find.FactionManager.FirstFactionOfDef(DefOfs.RKU_Faction);
            if (ratkinFaction == null) return;
            // 找到地图上所有的敌对pawn
            var hostilePawns = map.mapPawns.AllPawnsSpawned
                .Where(p => p.Faction != null && p.Faction.HostileTo(ratkinFaction) && !p.Downed)
                .ToList();

            if (hostilePawns.Count == 0) return;

            // 找到地图边缘的随机位置作为友军生成点
            IntVec3 spawnCenter = FindRandomEdgeSpawnPosition(map);
            if (!spawnCenter.IsValid || !spawnCenter.InBounds(map) || !spawnCenter.Standable(map))
            {
                spawnCenter = GetRandomEdgePosition(map);
            }
            List<Pawn> reinforcements = new List<Pawn>();
            int allyCount = Rand.RangeInclusive(6, 8);
            for (int i = 0; i < allyCount; i++)
            {
                var kindDef = DefDatabase<PawnKindDef>.GetNamed("RKU_EliteInvader");
                if (kindDef != null)
                {
                    var request = new PawnGenerationRequest(
                        kindDef,
                        ratkinFaction,
                        PawnGenerationContext.NonPlayer
                    );

                    var ally = PawnGenerator.GeneratePawn(request);
                    IntVec3 spawnPos = spawnCenter;
                    if (spawnPos.InBounds(map))
                    {
                        GenSpawn.Spawn(ally, spawnPos, map);
                        EquipEliteWeapon(ally);
                        reinforcements.Add(ally);
                    }
                }
            }
            // 创建直接进攻的任务
            if (reinforcements.Count > 0)
            {
                var assaultJob = new LordJob_AssaultColony(ratkinFaction, true, false, false, false, true);
                LordMaker.MakeNewLord(ratkinFaction, assaultJob, map, reinforcements);
                Messages.Message($"援军部队抵达并发起了突击！", reinforcements[0], MessageTypeDefOf.PositiveEvent);
            }
        }

        /// <summary>
        /// 为友军装备良好品质的RKU_SVT40M_Elite
        /// </summary>
        private void EquipEliteWeapon(Pawn pawn)
        {
            if (pawn == null || pawn.equipment == null) return;

            var weaponDef = ThingDef.Named("RKU_SVT40M_Elite");
            if (weaponDef != null)
            {
                pawn.equipment.DestroyAllEquipment();
                var weapon = ThingMaker.MakeThing(weaponDef);
                weapon.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Good, ArtGenerationContext.Outsider);
                pawn.equipment.AddEquipment((ThingWithComps)weapon);
            }
        }

        /// <summary>
        /// 获取地图边缘的随机位置作为援军生成点（带路径验证）
        /// </summary>
        private static IntVec3 GetRandomEdgePosition(Map map)
        {
            for (int attempt = 0; attempt < 50; attempt++)
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
        /// 在随机物品架上生成大师品质的原型反坦克步枪
        /// </summary>
        private void SpawnPrototypeWeapon(Map map)
        {
            var shelves = map.listerBuildings.allBuildingsNonColonist
                .Where(building => building.def.defName.Contains("Shelf"))
                .ToList();
            if (shelves.Count == 0) return;
            var selectedShelf = shelves.RandomElement();
            var weaponDef = ThingDef.Named("RKU_AntiTank");
            if (weaponDef != null)
            {
                var weapon = ThingMaker.MakeThing(weaponDef);
                weapon.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Masterwork, ArtGenerationContext.Outsider);
                GenSpawn.Spawn(weapon, selectedShelf.Position, map);
                Messages.Message("RKU_AncientWeaponMessage".Translate(), MessageTypeDefOf.PositiveEvent);
            }
        }

        public override void SitePartWorkerTick(SitePart sitePart)
        {
            base.SitePartWorkerTick(sitePart);

            Map map = sitePart.site.Map;
            if (map == null) return;

            // 处理友军突击逻辑
            if (!hasTriggeredReinforcement)
            {
                assaultTickCounter++;
                if (assaultTickCounter >= ASSAULT_DELAY_TICKS)
                {
                    TriggerAllyAssault(map);
                    hasTriggeredReinforcement = true;
                }
            }
        }
    }
}
