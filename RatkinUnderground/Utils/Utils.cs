using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse.Grammar;
using Verse;
using Verse.AI.Group;
using Verse.AI;
using static UnityEngine.GraphicsBuffer;
using UnityEngine.UIElements;

namespace RatkinUnderground
{
    public static class Utils
    {
        public static Site GenerateSite(IEnumerable<SitePartDefWithParams> sitePartsParams, int tile, Faction faction, bool hiddenSitePartsPossible = false, RulePack singleSitePartRules = null)
        {
            _ = QuestGen.slate;
            bool flag = false;
            foreach (SitePartDefWithParams sitePartsParam in sitePartsParams)
            {
                if (sitePartsParam.def.defaultHidden)
                {
                    flag = true;
                    break;
                }
            }

            if (flag || hiddenSitePartsPossible)
            {
                SitePartParams parms = SitePartDefOf.PossibleUnknownThreatMarker.Worker.GenerateDefaultParams(0f, tile, faction);
                SitePartDefWithParams val = new SitePartDefWithParams(SitePartDefOf.PossibleUnknownThreatMarker, parms);
                sitePartsParams = sitePartsParams.Concat(Gen.YieldSingle(val));
            }

            Site site = SiteMaker.MakeSite(sitePartsParams, tile, faction);
            List<string> list2 = new List<string>();
            int num = 0;
            for (int i = 0; i < site.parts.Count; i++)
            {
                List<Rule> list3 = new List<Rule>();
                Dictionary<string, string> dictionary2 = new Dictionary<string, string>();
                site.parts[i].def.Worker.Notify_GeneratedByQuestGen(site.parts[i], QuestGen.slate, list3, dictionary2);
                if (site.parts[i].hidden)
                {
                    continue;
                }

                if (singleSitePartRules != null)
                {
                    List<Rule> list4 = new List<Rule>();
                    list4.AddRange(list3);
                    list4.AddRange(singleSitePartRules.Rules);
                    string text = QuestGenUtility.ResolveLocalText(list4, dictionary2, "root", capitalizeFirstSentence: false);
                    if (!text.NullOrEmpty())
                    {
                        list2.Add(text);
                    }
                }

                for (int j = 0; j < list3.Count; j++)
                {
                    Rule rule = list3[j].DeepCopy();
                    if (rule is Rule_String rule_String && num != 0)
                    {
                        rule_String.keyword = "sitePart" + num + "_" + rule_String.keyword;
                    }
                }

                foreach (KeyValuePair<string, string> item in dictionary2)
                {
                    string text2 = item.Key;
                    if (num != 0)
                    {
                        text2 = "sitePart" + num + "_" + text2;
                    }
                }
                num++;
            }
            return site;
        }

        public static IntVec3 FindSuitableSpawnPosition(Map map)
        {
            int borderSize = 10;
            List<IntVec3> validCells = new List<IntVec3>();

            // 检查地图边界区域
            for (int x = borderSize; x < map.Size.x - borderSize; x++)
            {
                for (int z = borderSize; z < map.Size.z - borderSize; z++)
                {
                    IntVec3 cell = new IntVec3(x, 0, z);
                    if (IsValidSpawnPosition(cell, map))
                    {
                        validCells.Add(cell);
                    }
                }
            }

            // 如果找到有效位置，随机选择一个
            if (validCells.Count > 0)
            {
                return validCells.RandomElement();
            }

            // 如果没有找到合适的位置，返回地图中心
            return map.Center;
        }

        public static bool IsValidSpawnPosition(IntVec3 cell, Map map)
        {
            // 检查1x2区域是否可放置
            for (int x = 0; x < 1; x++)
            {
                for (int z = 0; z < 2; z++)
                {
                    IntVec3 checkCell = new IntVec3(cell.x + x, 0, cell.z + z);
                    if (!checkCell.InBounds(map) || !checkCell.Standable(map))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public static IntVec3 FindLaunchSpot(Map map)
        {
            // 在地图边缘寻找随机位置
            List<IntVec3> edgeCells = new List<IntVec3>();
            for (int x = 0; x < map.Size.x; x++)
            {
                edgeCells.Add(new IntVec3(x, 0, 0));
                edgeCells.Add(new IntVec3(x, 0, map.Size.z - 1));
            }
            for (int z = 0; z < map.Size.z; z++)
            {
                edgeCells.Add(new IntVec3(0, 0, z));
                edgeCells.Add(new IntVec3(map.Size.x - 1, 0, z));
            }
            return edgeCells.Where(cell => cell.InBounds(map) && cell.Standable(map)).RandomElement();
        }

        public static IntVec3 FindTargetSpot(Map map)
        {
            int centerX = map.Size.x / 2;
            int centerZ = map.Size.z / 2;
            int searchRadius = 20;

            List<IntVec3> validCells = new List<IntVec3>();

            for (int x = centerX - searchRadius; x <= centerX + searchRadius; x++)
            {
                for (int z = centerZ - searchRadius; z <= centerZ + searchRadius; z++)
                {
                    IntVec3 cell = new IntVec3(x, 0, z);
                    if (cell.InBounds(map) &&
                        cell.Standable(map))
                    {
                        validCells.Add(cell);
                    }
                }
            }

            return validCells.Count > 0 ? validCells.RandomElement() : IntVec3.Invalid;
        }

        // 尝试搜寻钻机
        public static bool TryFindDrill(Map map, out RKU_DrillingVehicleInEnemyMap drill)
        {

            foreach (var building in map.listerBuildings.allBuildingsNonColonist)
            {
                if (building.def != DefOfs.RKU_DrillingVehicleInEnemyMap) continue;
                drill = (RKU_DrillingVehicleInEnemyMap)building;
                return true;
            }
            drill = null;
            return false;
        }

        // 寻找距离地图边缘10格有效格
        public static IntVec3 FindRandomEdgeSpawnPosition(Map map, int bandWidth = 20, int count = 3, IntVec3 defaultPos = default)
        {
            IntVec3 cell;
            if (defaultPos == default)
            {
                defaultPos = map.Center;
            }

            var sizeX = map.Size.x;
            var sizeZ = map.Size.z;
            var rand = Rand.Value;

            // 重试3轮
            for (int i = 0; i < count; i++)
            {
                int side = Rand.Range(0, 4);    // 随机一个方向
                int offset;                     // 该方向的偏移

                switch (side)
                {
                    case 0: // 西侧
                        offset = Rand.Range(2, sizeZ);
                        cell = new IntVec3(Rand.Range(2, bandWidth), 0, offset);
                        break;
                    case 1: // 东侧
                        offset = Rand.Range(2, sizeZ);
                        cell = new IntVec3(sizeX - 1 - Rand.Range(2, bandWidth), 0, offset);
                        break;
                    case 2: // 南侧
                        offset = Rand.Range(2, sizeX);
                        cell = new IntVec3(offset, 0, Rand.Range(2, bandWidth));
                        break;
                    default: // 北侧
                        offset = Rand.Range(2, sizeX);
                        cell = new IntVec3(offset, 0, sizeZ - 1 - Rand.Range(2, bandWidth));
                        break;
                }

                if (IsValidSpawnPosition(cell, map) &&
                    map.reachability.CanReachNonLocal(cell, new TargetInfo(defaultPos, map), PathEndMode.OnCell, TraverseMode.PassDoors, Danger.Deadly))
                    return cell;

                const int searchRadius = 5;
                foreach (var candidate in GenRadial.RadialCellsAround(cell, searchRadius, true))
                {
                    if (candidate.InBounds(map) &&
                        IsValidSpawnPosition(candidate, map) &&
                        map.reachability.CanReachNonLocal(cell, new TargetInfo(defaultPos, map), PathEndMode.OnCell, TraverseMode.PassDoors, Danger.Deadly))
                    {
                        return candidate;
                    }
                }
            }

            // 找到不到生成点了，生成到中心跟你们爆了
            return defaultPos;
        }

        /// <summary>
        /// 将长文本按指定长度分割成多行
        /// </summary>
        /// <param name="message">要分割的文本</param>
        /// <param name="maxCharsPerLine">每行最大字符数，默认30</param>
        /// <returns>分割后的行列表</returns>
        public static List<string> SplitMessageIntoLines(string message, int maxCharsPerLine = 30)
        {
            var lines = new List<string>();

            if (string.IsNullOrEmpty(message))
            {
                return lines;
            }

            // 如果消息长度小于等于最大长度，直接返回
            if (message.Length <= maxCharsPerLine)
            {
                lines.Add(message);
                return lines;
            }

            // 分割长消息
            for (int i = 0; i < message.Length; i += maxCharsPerLine)
            {
                int length = Math.Min(maxCharsPerLine, message.Length - i);
                string line = message.Substring(i, length);
                lines.Add(line);
            }

            return lines;
        }
        public static void ClearArea(Sketch sketch, IntVec3 origin, int width, int height, Predicate<SketchEntity> filter = null)
        {
            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < height; z++)
                {
                    IntVec3 pos = origin + new IntVec3(x, 0, z);

                    foreach (var entity in sketch.ThingsAt(pos).ToList())
                    {
                        if (filter == null || !filter(entity))
                        {
                            sketch.Remove(entity);
                        }
                    }

                    // 地形清理保持不变
                    if (sketch.TerrainAt(pos) != null)
                    {
                        sketch.RemoveTerrain(pos);
                    }
                }
            }
        }

        public static int GetRelationshipLevel(int relationshipValue)
        {
            if (relationshipValue <= -75) return -4;
            if (relationshipValue <= -50) return -3;
            if (relationshipValue <= -25) return -2;
            if (relationshipValue < 0) return -1;
            if (relationshipValue == 0) return 1;
            if (relationshipValue < 25) return 1;
            if (relationshipValue < 50) return 2;
            if (relationshipValue < 75) return 3;
            return 4;
        }


        // 使用 CellFinder 寻找有效位置
        public static bool TryFindValidSpawnPosition(Map map, out IntVec3 loc)
        {
            if (TryFindPlayerRoomPosition(map, out loc))
            {
                return true;
            }

            if (TryFindEdgePosition(map, out loc))
            {
                return true;
            }
            return CellFinder.TryFindRandomCellNear(map.Center, map, 30,
                c => CanSpawnTunnelAt(c, map), out loc);
        }

        // 尝试在玩家房间内寻找有效位置
        public static bool TryFindPlayerRoomPosition(Map map, out IntVec3 loc)
        {
            // 获取所有玩家拥有的房间
            var playerRooms = map.regionGrid.allRooms
                .Where(room => room.CellCount > 10)
                .ToList();

            if (playerRooms.Count == 0)
            {
                loc = IntVec3.Invalid;
                return false;
            }

            playerRooms.Shuffle();

            foreach (var room in playerRooms)
            {
                if (CellFinder.TryFindRandomCellInRegion(room.FirstRegion, c => CanSpawnTunnelAt(c, map), out loc))
                {
                    return true;
                }
            }

            loc = IntVec3.Invalid;
            return false;
        }

        // 尝试在地图边缘寻找有效位置
        public static bool TryFindEdgePosition(Map map, out IntVec3 loc)
        {
            // 尝试多个边缘位置
            for (int i = 0; i < 30; i++)
            {
                loc = CellFinder.RandomEdgeCell(map);
                if (CanSpawnTunnelAt(loc, map))
                {
                    return true;
                }
            }

            loc = IntVec3.Invalid;
            return false;
        }

        // 检查位置是否可以放置隧道
        public static bool CanSpawnTunnelAt(IntVec3 cell, Map map)
        {
            return GenConstruct.CanPlaceBlueprintAt(DefOfs.RKU_TunnelHiveSpawner_Und, cell, Rot4.North, map) &&
                   cell.Standable(map) &&
                   !cell.Fogged(map) &&
                   !cell.Roofed(map) && // 确保没有屋顶
                   map.reachability.CanReachColony(cell); // 确保可以到达
        }

        /// <summary>
        /// 映射表
        /// </summary>
        public static class InspirationMapper
        {

            public static readonly Dictionary<SkillDef, InspirationDef[]> SkillToInspirationMap = BuildSkillToInspirationMap();

            public static Dictionary<SkillDef, InspirationDef[]> BuildSkillToInspirationMap()
            {
                var map = new Dictionary<SkillDef, InspirationDef[]>();

                void AddMapping(string skillDefName, params string[] inspirationDefNames)
                {
                    var sdef = DefDatabase<SkillDef>.GetNamedSilentFail(skillDefName);
                    if (sdef == null)
                    {
                        Log.Warning($"InspirationMapper: 未找到 SkillDef '{skillDefName}'，跳过映射。");
                        return;
                    }

                    var inspDefs = new List<InspirationDef>();
                    foreach (var inspName in inspirationDefNames)
                    {
                        var idef = DefDatabase<InspirationDef>.GetNamedSilentFail(inspName);
                        if (idef == null)
                        {
                            Log.Warning($"InspirationMapper: 未找到 InspirationDef '{inspName}'（对应 Skill '{skillDefName}'）。");
                            continue;
                        }
                        inspDefs.Add(idef);
                    }

                    if (inspDefs.Count > 0)
                        map[sdef] = inspDefs.ToArray();
                }

                AddMapping("Social", "Inspired_Trade", "Inspired_Recruitment");
                AddMapping("Animals", "Inspired_Taming");
                AddMapping("Medicine", "Inspired_Surgery");
                AddMapping("Crafting", "Inspired_Creativity");
                AddMapping("Construction", "Inspired_Creativity");
                AddMapping("Artistic", "Inspired_Creativity");
                return map;
            }
        }
    }
}

