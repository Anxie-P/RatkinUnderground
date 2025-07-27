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
    }
}
