using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using RimWorld;
using RimWorld.BaseGen;
using UnityEngine;
using Verse;

namespace RatkinUnderground;

public class RKU_GenStep_BioLab : GenStep
{
    private const int MAP_SIZE = 250;
    private const int BUILDING_SIZE = 150; // 建筑大小
    private const int GAP_SIZE = 31; // 中心空缺大小

    public bool generateLoot = true;
    public bool unfogged = true;

    public override int SeedPart => 487293847;
    // 字符画布局数据（懒加载）
    private List<string> LayoutData
    {
        get
        {
            return LoadLayoutFromXml();
        }
    }
    private List<string> LoadLayoutFromXml()
    {
        try
        {
            string modPath = null;
            foreach (var mod in ModsConfig.ActiveModsInLoadOrder)
            {
                if (mod.PackageId.ToLower() == "rku.ratkinunderground" || mod.RootDir.Name == "250407")
                {
                    modPath = mod.RootDir.FullName;
                    break;
                }
            }

            string xmlPath = Path.Combine(modPath, "1.5", "Defs", "MapGenerator", "BioLabLayouts.xml");
            if (!File.Exists(xmlPath))
            {
                Log.Error("BioLab: XML file not found: " + xmlPath);
                return new List<string>();
            }

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(xmlPath);
            XmlNode layoutNode = xmlDoc.SelectSingleNode("/Defs/BioLabLayout/layout");
            if (layoutNode == null)
            {
                Log.Error("BioLab: Layout node not found in XML");
                return new List<string>();
            }

            string layoutText = layoutNode.InnerText.Trim();
            if (string.IsNullOrEmpty(layoutText))
            {
                Log.Error("BioLab: Layout text is empty");
                return new List<string>();
            }

            // 按行分割并清理
            var rows = new List<string>();
            var lines = layoutText.Split('\n');
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (!string.IsNullOrEmpty(trimmedLine))
                {
                    rows.Add(trimmedLine);
                }
            }
            return rows;
        }
        catch (System.Exception ex)
        {
            Log.Error("Error loading BioLab layout from XML: " + ex.Message);
            return new List<string>();
        }
    }

    public override void Generate(Map map, GenStepParams parms)
    {
        // 计算地图中心位置
        int centerX = map.Size.x / 2;
        int centerY = map.Size.z / 2;
        // 获取布局数据
        var layoutRows = LayoutData;
        // 获取布局尺寸
        int layoutHeight = layoutRows.Count;
        int layoutWidth = layoutHeight > 0 ? layoutRows[0].Length : 0;
        // 计算布局左上角位置（居中）
        int startX = centerX - layoutWidth / 2;
        int startZ = centerY - layoutHeight / 2;
        // 遍历布局数据并生成相应地形
        for (int y = 0; y < layoutHeight; y++)
        {
            string row = layoutRows[y];
            for (int x = 0; x < layoutWidth; x++)
            {
                char cell = row[x];
                IntVec3 pos = new IntVec3(startX + x, 0, startZ + (layoutHeight - 1 - y));
                if (!pos.InBounds(map))
                    continue;

                // 特殊情况
                int skipChars = 0;
                if (TryHandleSpecialCell(map, pos, cell, row, x, layoutWidth, out skipChars))
                {
                    x += skipChars;
                    continue;
                }

                GenerateCell(map, pos, cell);
            }
        }
        if (unfogged)
        {
            FloodFillerFog.FloodUnfog(IntVec3.Zero, map);
        }
    }

    /// <summary>
    /// 尝试处理特殊单元格（如DD安全大门）
    /// </summary>
    /// <param name="map">地图</param>
    /// <param name="pos">当前位置</param>
    /// <param name="cell">当前字符</param>
    /// <param name="row">当前行</param>
    /// <param name="x">当前x坐标</param>
    /// <param name="layoutWidth">布局宽度</param>
    /// <param name="skipChars">需要跳过的字符数</param>
    /// <returns>是否处理了特殊情况</returns>
    private bool TryHandleSpecialCell(Map map, IntVec3 pos, char cell, string row, int x, int layoutWidth, out int skipChars)
    {
        skipChars = 0;

        // 特殊处理DD
        if (cell == 'D' && x + 1 < layoutWidth && row[x + 1] == 'D')
        {
            ThingDef doorDef;
            if (ModsConfig.AnomalyActive)
            {
                doorDef = DefDatabase<ThingDef>.GetNamed("SecurityDoor");
                Thing door = ThingMaker.MakeThing(doorDef);
                GenSpawn.Spawn(door, pos, map);
            }
            else
            {
                doorDef = DefDatabase<ThingDef>.GetNamed("OrnateDoor");
                ThingDef doorStuff = ThingDefOf.Steel; // 使用钢铁作为默认材质
                Thing door = ThingMaker.MakeThing(doorDef, doorStuff);
                GenSpawn.Spawn(door, pos, map);

            }
            // 跳过下一个字符，因为已经处理了DD
            skipChars = 1;
            return true;
        }

        return false;
    }

    private void GenerateCell(Map map, IntVec3 pos, char cellType)
    {
        // 安全地清理位置上的现有物品
        List<Thing> things = pos.GetThingList(map);
        for (int i = things.Count - 1; i >= 0; i--)
        {
            if (things[i].Position == pos)
            {
                things[i].Destroy();
            }
        }
        switch (cellType)
        {
            case '█': // 墙体
                ThingDef wallDef = ThingDefOf.Wall;
                ThingDef wallStuff = ThingDefOf.Steel; 
                if (wallDef != null)
                {
                    Thing wall = ThingMaker.MakeThing(wallDef, wallStuff);
                    GenSpawn.Spawn(wall, pos, map);
                }
                break;

            case '░': // 空地
                map.terrainGrid.SetTerrain(pos, TerrainDefOf.Concrete);
                break;
            case '□': // 无菌地砖 
                map.terrainGrid.SetTerrain(pos, TerrainDef.Named("SterileTile"));
                break;
            case 'D': // 单个钢铁门
                ThingDef doorDef = ThingDefOf.Door;
                ThingDef doorStuff = ThingDefOf.Steel; 
                if (doorDef != null)
                {
                    Thing door = ThingMaker.MakeThing(doorDef, doorStuff);
                    GenSpawn.Spawn(door, pos, map);
                }
                break;
            case 'G': // 纵向门
                ThingDef doorDefG;
                if (ModsConfig.AnomalyActive)
                {
                    doorDefG = DefDatabase<ThingDef>.GetNamed("SecurityDoor");
                    Thing door = ThingMaker.MakeThing(doorDefG);
                    GenSpawn.Spawn(door, pos, map);
                }
                else
                {
                    doorDefG = DefDatabase<ThingDef>.GetNamed("OrnateDoor");
                    ThingDef doorStuffG = ThingDefOf.Steel; 
                    Thing door = ThingMaker.MakeThing(doorDefG, doorStuffG);
                    GenSpawn.Spawn(door, pos, map);

                }
                Thing doorG = ThingMaker.MakeThing(doorDefG);
                GenSpawn.Spawn(doorG, pos, map,Rot4.East);
                break;
            case 'Z': // 栅栏
                ThingDef fenceDef = ThingDef.Named("Fence");
                ThingDef fenceStuff = ThingDef.Named("BlocksSandstone");
                if (fenceDef != null)
                {
                    Thing fence = ThingMaker.MakeThing(fenceDef, fenceStuff);
                    GenSpawn.Spawn(fence, pos, map);
                }
                break;
            case 'R': // 玫瑰
                map.terrainGrid.SetTerrain(pos, TerrainDefOf.SoilRich);
                Thing rose = ThingMaker.MakeThing(ThingDef.Named("Plant_Rose"));
                Plant plant = (Plant)GenSpawn.Spawn(rose, pos, map);
                plant.Growth = Rand.Range(plant.def.plant.harvestMinGrowth, 1f);
                if (plant != null)
                {
                    plant.Growth = 1f;
                }
                break;
            case 'C': // 仓鼠轮 
                Thing hamsterWheel = ThingMaker.MakeThing(ThingDef.Named("RK_HamsterWheelGenerator"));
                GenSpawn.Spawn(hamsterWheel, pos, map);
                break;
            case 'B': // 床
                ThingDef bedStuff = ThingDefOf.WoodLog;
                Thing bed = ThingMaker.MakeThing(ThingDef.Named("Bed"), bedStuff);
                GenSpawn.Spawn(bed, pos, map, Rot4.South);
                // 设置床位置的地形为周围最多的地形
                SetTerrainToMostCommonAround(map, pos);
                break;
            default:
                map.terrainGrid.SetTerrain(pos, DefDatabase<TerrainDef>.GetNamed("SterileTile"));
                break;
        }
    }

    /// <summary>
    /// 将指定位置的地形设置为周围8格中最多的地形类型
    /// </summary>
    /// <param name="map">地图</param>
    /// <param name="pos">目标位置</param>
    private void SetTerrainToMostCommonAround(Map map, IntVec3 pos)
    {
        // 统计周围8格的地形类型
        Dictionary<TerrainDef, int> terrainCount = new Dictionary<TerrainDef, int>();
        
        for (int x = -1; x <= 1; x++)
        {
            for (int z = -1; z <= 1; z++)
            {
                if (x == 0 && z == 0) continue; // 跳过中心位置
                
                IntVec3 neighborPos = new IntVec3(pos.x + x, pos.y, pos.z + z);
                if (neighborPos.InBounds(map))
                {
                    TerrainDef terrain = neighborPos.GetTerrain(map);
                    if (terrainCount.ContainsKey(terrain))
                    {
                        terrainCount[terrain]++;
                    }
                    else
                    {
                        terrainCount[terrain] = 1;
                    }
                }
            }
        }
        
        // 找到最多的地形类型
        TerrainDef mostCommonTerrain = null;
        int maxCount = 0;
        foreach (var kvp in terrainCount)
        {
            if (kvp.Value > maxCount)
            {
                maxCount = kvp.Value;
                mostCommonTerrain = kvp.Key;
            }
        }
        
        // 将目标位置的地形设置为周围最多的地形
        if (mostCommonTerrain != null)
        {
            map.terrainGrid.SetTerrain(pos, mostCommonTerrain);
        }
    }
}
