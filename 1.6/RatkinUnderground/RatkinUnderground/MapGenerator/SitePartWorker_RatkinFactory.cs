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
            SpawnKingdomAttackers(map);
            SpawnGuerrillaDefenders(map);
        }

        /// <summary>
        /// 生成王国军攻击者
        /// </summary>
        private void SpawnKingdomAttackers(Map map)
        {
            // 获取王国军派系
            var kingdomFaction = Find.FactionManager.FirstFactionOfDef(FactionDef.Named("Rakinia"));
            if (kingdomFaction == null) return;

            // 找到所有室内房间
            var allRooms = map.regionGrid.AllRooms
                .Where(room => room.CellCount > 5 && IsIndoorRoom(room, map))
                .ToList();

            if (allRooms.Count == 0) return;

            // 找到最大的3个房间作为主要攻击区域
            var mainRooms = allRooms.OrderByDescending(r => r.CellCount).Take(3).ToList();

            var roomAttackers = new Dictionary<Room, List<Pawn>>();

            // 在主要房间中生成王国军攻击者
            foreach (var room in mainRooms)
            {
                var attackers = new List<Pawn>();
                int attackerCount = Rand.RangeInclusive(6, 8);
                var availableCells = GetAvailableCellsInRoom(room, map, attackerCount);

                // 王国军攻击单位列表
                string[] attackerKinds = {
                    "RatkinRoyalGuard",
                    "RatkinKnight",
                    "RatkinEliteDefender",
                    "RatkinEliteGuardener",
                    "RatkinDemonMan"
                };

                for (int i = 0; i < attackerCount && i < availableCells.Count; i++)
                {
                    string kindDefName = attackerKinds.RandomElement();
                    var kindDef = DefDatabase<PawnKindDef>.GetNamedSilentFail(kindDefName);

                    if (kindDef != null)
                    {
                        var request = new PawnGenerationRequest(
                            kindDef,
                            kingdomFaction,
                            PawnGenerationContext.NonPlayer
                        );

                        var pawn = PawnGenerator.GeneratePawn(request);
                        GenSpawn.Spawn(pawn, availableCells[i], map);

                        // 装备王国军武器
                        EquipKingdomWeapon(pawn);
                        // 装备王国军盔甲
                        EquipKingdomArmor(pawn);

                        attackers.Add(pawn);
                    }
                }
                roomAttackers[room] = attackers;
            }

            // 在其他房间中生成少量巡逻单位
            var patrolRooms = allRooms.Except(mainRooms).ToList();
            foreach (var room in patrolRooms)
            {
                if (Rand.Value < 0.6f) // 60%概率在房间中生成巡逻单位
                {
                    var patrolAttackers = new List<Pawn>();
                    int patrolCount = Rand.RangeInclusive(3, 4);
                    var patrolCells = GetAvailableCellsInRoom(room, map, patrolCount);

                    for (int i = 0; i < patrolCount && i < patrolCells.Count; i++)
                    {
                        var combatantDef = DefDatabase<PawnKindDef>.GetNamedSilentFail("RatkinCombatant");
                        if (combatantDef != null)
                        {
                            var request = new PawnGenerationRequest(
                                combatantDef,
                                kingdomFaction,
                                PawnGenerationContext.NonPlayer
                            );

                            var pawn = PawnGenerator.GeneratePawn(request);
                            GenSpawn.Spawn(pawn, patrolCells[i], map);

                            // 装备基础武器和盔甲
                            EquipBasicKingdomGear(pawn);

                            patrolAttackers.Add(pawn);
                        }
                    }
                    roomAttackers[room] = patrolAttackers;
                }
            }

            // 为每个房间的攻击者创建攻击任务
            foreach (var kvp in roomAttackers)
            {
                var room = kvp.Key;
                var attackers = kvp.Value;

                if (attackers.Count > 0)
                {
                    var assaultJob = new LordJob_AssaultColony(kingdomFaction, true, false, false, false, true);
                    LordMaker.MakeNewLord(kingdomFaction, assaultJob, map, attackers);
                }
            }
        }

        /// <summary>
        /// 生成游击队守卫部队
        /// </summary>
        private void SpawnGuerrillaDefenders(Map map)
        {
            // 获取游击队派系
            var guerrillaFaction = Find.FactionManager.FirstFactionOfDef(DefOfs.RKU_Faction);
            if (guerrillaFaction == null) return;

            // 找到包含钻机等重要设备的区域
            var drillPositions = new List<IntVec3>();
            var drillDefs = new[] { "RKU_DrillingVehicle", "RKU_DrillingCargoPod", "RKU_DrillingVehicleWithTurret" };

            foreach (var drillDef in drillDefs)
            {
                var drills = map.listerThings.ThingsOfDef(ThingDef.Named(drillDef));
                foreach (var drill in drills)
                {
                    drillPositions.Add(drill.Position);
                }
            }

            if (drillPositions.Count == 0) return;

            // 在钻机周围生成守卫
            List<Pawn> defenders = new List<Pawn>();
            int defenderCount = Rand.RangeInclusive(8, 12);

            // 游击队守卫单位列表
            string[] defenderKinds = {
                "RKU_EliteInvader",
                "RatkinEliteDefender",
                "RatkinEliteGuardener",
                "RatkinVanguard"
            };

            for (int i = 0; i < defenderCount; i++)
            {
                // 在钻机周围随机位置生成
                var drillPos = drillPositions.RandomElement();
                var spawnPos = FindNearbySpawnPosition(drillPos, map, 3, 8);

                if (spawnPos.IsValid && spawnPos.InBounds(map) && spawnPos.Standable(map))
                {
                    string kindDefName = defenderKinds.RandomElement();
                    var kindDef = DefDatabase<PawnKindDef>.GetNamedSilentFail(kindDefName);

                    if (kindDef != null)
                    {
                        var request = new PawnGenerationRequest(
                            kindDef,
                            guerrillaFaction,
                            PawnGenerationContext.NonPlayer
                        );

                        var pawn = PawnGenerator.GeneratePawn(request);
                        GenSpawn.Spawn(pawn, spawnPos, map);

                        // 装备游击队武器
                        EquipGuerrillaWeapon(pawn);

                        defenders.Add(pawn);
                    }
                }
            }

            // 创建防御任务，守卫钻机区域
            if (defenders.Count > 0)
            {
                var centerPos = drillPositions.Aggregate(IntVec3.Zero, (sum, pos) => sum + pos) / drillPositions.Count;
                var defendJob = new LordJob_DefendPoint(centerPos);
                LordMaker.MakeNewLord(guerrillaFaction, defendJob, map, defenders);
            }
        }

        /// <summary>
        /// 为王国军装备武器
        /// </summary>
        private void EquipKingdomWeapon(Pawn pawn)
        {
            if (pawn == null || pawn.equipment == null) return;

            var weaponDef = ThingDef.Named("RKU_SVT40M_Elite");
            if (weaponDef != null)
            {
                pawn.equipment.DestroyAllEquipment();
                var weapon = ThingMaker.MakeThing(weaponDef);
                weapon.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Excellent, ArtGenerationContext.Outsider);
                pawn.equipment.AddEquipment((ThingWithComps)weapon);
            }

            // 提升射击技能
            var shootingSkill = pawn.skills.GetSkill(SkillDefOf.Shooting);
            if (shootingSkill != null && shootingSkill.Level < 5)
            {
                shootingSkill.Level = Rand.Range(5, 8);
            }
        }

        /// <summary>
        /// 为王国军装备盔甲
        /// </summary>
        private void EquipKingdomArmor(Pawn pawn)
        {
            if (pawn == null || pawn.apparel == null) return;

            // 装备王国军盔甲
            var armorDefs = new[] { "Apparel_PlateArmor", "Apparel_AdvancedHelmet" };
            foreach (var armorDefName in armorDefs)
            {
                var armorDef = DefDatabase<ThingDef>.GetNamedSilentFail(armorDefName);
                if (armorDef != null)
                {
                    var armor = ThingMaker.MakeThing(armorDef, ThingDefOf.Steel);
                    armor.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Good, ArtGenerationContext.Outsider);
                    pawn.apparel.Wear((Apparel)armor);
                }
            }
        }

        /// <summary>
        /// 为普通王国军装备基础装备
        /// </summary>
        private void EquipBasicKingdomGear(Pawn pawn)
        {
            if (pawn == null) return;

            // 装备基础武器
            EquipKingdomWeapon(pawn);

            // 有30%概率装备头盔
            if (Rand.Value < 0.3f)
            {
                var helmetDef = DefDatabase<ThingDef>.GetNamedSilentFail("Apparel_AdvancedHelmet");
                if (helmetDef != null && pawn.apparel != null)
                {
                    var helmet = ThingMaker.MakeThing(helmetDef, ThingDefOf.Steel);
                    helmet.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Normal, ArtGenerationContext.Outsider);
                    pawn.apparel.Wear((Apparel)helmet);
                }
            }
        }

        /// <summary>
        /// 为游击队装备武器
        /// </summary>
        private void EquipGuerrillaWeapon(Pawn pawn)
        {
            if (pawn == null || pawn.equipment == null) return;

            var weaponDef = ThingDef.Named("RKU_SVT40M");
            if (weaponDef != null)
            {
                pawn.equipment.DestroyAllEquipment();
                var weapon = ThingMaker.MakeThing(weaponDef);
                weapon.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Good, ArtGenerationContext.Outsider);
                pawn.equipment.AddEquipment((ThingWithComps)weapon);
            }

            // 提升射击技能
            var shootingSkill = pawn.skills.GetSkill(SkillDefOf.Shooting);
            if (shootingSkill != null && shootingSkill.Level < 4)
            {
                shootingSkill.Level = Rand.Range(4, 7);
            }
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
        /// 王国军增援逻辑
        /// </summary>
        private void TriggerKingdomReinforcement(Map map)
        {
            var kingdomFaction = Find.FactionManager.FirstFactionOfDef(FactionDef.Named("Rakinia"));
            if (kingdomFaction == null) return;

            // 找到地图边缘的随机位置作为援军生成点
            IntVec3 spawnCenter = GetRandomEdgePosition(map);
            List<Pawn> reinforcements = new List<Pawn>();
            int reinforcementCount = Rand.RangeInclusive(4, 6);

            for (int i = 0; i < reinforcementCount; i++)
            {
                var kindDef = DefDatabase<PawnKindDef>.GetNamedSilentFail("RatkinKnight");
                if (kindDef != null)
                {
                    var request = new PawnGenerationRequest(
                        kindDef,
                        kingdomFaction,
                        PawnGenerationContext.NonPlayer
                    );

                    var pawn = PawnGenerator.GeneratePawn(request);
                    IntVec3 spawnPos = spawnCenter;
                    if (spawnPos.InBounds(map))
                    {
                        GenSpawn.Spawn(pawn, spawnPos, map);
                        EquipKingdomWeapon(pawn);
                        EquipKingdomArmor(pawn);
                        reinforcements.Add(pawn);
                    }
                }
            }

            // 创建攻击任务
            if (reinforcements.Count > 0)
            {
                var assaultJob = new LordJob_AssaultColony(kingdomFaction, true, false, false, false, true);
                LordMaker.MakeNewLord(kingdomFaction, assaultJob, map, reinforcements);
                Messages.Message("王国军援军抵达！他们发起了猛烈的攻击！", reinforcements[0], MessageTypeDefOf.ThreatBig);
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

        public override void SitePartWorkerTick(SitePart sitePart)
        {
            base.SitePartWorkerTick(sitePart);

            Map map = sitePart.site.Map;
            if (map == null) return;

            // 处理王国军增援逻辑
            if (!hasTriggeredReinforcement)
            {
                assaultTickCounter++;
                if (assaultTickCounter >= ASSAULT_DELAY_TICKS)
                {
                    TriggerKingdomReinforcement(map);
                    hasTriggeredReinforcement = true;
                }
            }
        }
    }
}
