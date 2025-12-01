using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using RimWorld;
using RimWorld.BaseGen;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace RatkinUnderground;

public class RKU_GenStep_BioLab : GenStep
{
    private const int MAP_SIZE = 250;
    private const int BUILDING_SIZE = 150; // 建筑大小
    private const int GAP_SIZE = 31; // 中心空缺大小

    public bool generateLoot = true;
    public bool unfogged = true;
    public string layoutFilePath = "BioLabLayouts.xml"; // 布局文件路径，可在def中配置
    public string placementPosition = "Center";
    public override int SeedPart => 487293847;
    // 字符画布局数据（懒加载并缓存）
    private List<string> _layoutData;
    private List<string> LayoutData
    {
        get
        {
            if (_layoutData == null)
            {
                _layoutData = LoadLayoutFromXml();
            }
            return _layoutData;
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

            string xmlPath = Path.Combine(modPath, layoutFilePath);
            if (!File.Exists(xmlPath))
            {
                return new List<string>();
            }

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(xmlPath);
            XmlNode layoutNode = xmlDoc.SelectSingleNode("/BioLabLayout/layout");
            if (layoutNode == null)
            {
                return new List<string>();
            }

            string layoutText = layoutNode.InnerText.Trim();
            if (string.IsNullOrEmpty(layoutText))
            {
                return new List<string>();
            }
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
        catch (System.Exception)
        {
            return new List<string>();
        }
    }

    public override void Generate(Map map, GenStepParams parms)
    {

        // 清理全图的所有气泉
        var allSteamGeysers = map.listerThings.ThingsOfDef(ThingDef.Named("SteamGeyser")).ToList();
        foreach (var geyser in allSteamGeysers)
        {
            geyser.Destroy();
        }

        // 获取布局数据
        var layoutRows = LayoutData;
        // 获取布局尺寸
        int layoutHeight = layoutRows.Count;
        int layoutWidth = layoutHeight > 0 ? layoutRows[0].Length : 0;
        // 根据位置参数计算布局左上角位置
        var (startX, startZ) = CalculateStartPosition(map, layoutWidth, layoutHeight);
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
                try
                {
                GenerateCell(map, pos, cell, layoutRows, startX, startZ, layoutHeight);
                }
                catch (System.Exception ex)
                {
                    Log.Warning($"[RKU_GenStep_BioLab] Error generating cell at {pos} with character '{cell}': {ex.Message}");
                }
            }
        }
        // 在所有建筑生成完成后生成墙体、房顶
        GenerateWallsAndRoof(map, layoutRows, startX, startZ, layoutHeight);
        // 生成壁灯
        GenerateWallLamps(map, layoutRows, startX, startZ, layoutHeight);
        if (unfogged)
        {
            FloodFillerFog.FloodUnfog(IntVec3.Zero, map);
        }
    }

    /// <summary>
    /// 根据位置参数计算布局的起始位置
    /// </summary>
    private (int startX, int startZ) CalculateStartPosition(Map map, int layoutWidth, int layoutHeight)
    {
        int centerX = map.Size.x / 2;
        int centerZ = map.Size.z / 2;
        int startX, startZ;

        // 根据位置参数计算起始位置
        switch (placementPosition?.ToLower())
        {
            case "top": // 顶端
                startX = centerX - layoutWidth / 2;
                startZ = map.Size.z - layoutHeight; // 紧贴顶端
                break;

            case "bottom": // 底端
                startX = centerX - layoutWidth / 2;
                startZ = 0; // 紧贴底端
                break;

            case "left": // 左侧
                startX = 0; // 紧贴左侧
                startZ = centerZ - layoutHeight / 2;
                break;

            case "right": // 右侧
                startX = map.Size.x - layoutWidth; // 紧贴右侧
                startZ = centerZ - layoutHeight / 2;
                break;

            case "center": // 中心
            default:
                startX = centerX - layoutWidth / 2;
                startZ = centerZ - layoutHeight / 2;
                break;
        }

        // 确保位置在地图范围内
        startX = Mathf.Clamp(startX, 0, map.Size.x - layoutWidth);
        startZ = Mathf.Clamp(startZ, 0, map.Size.z - layoutHeight);

        return (startX, startZ);
    }

    /// <summary>
    /// 生成墙体和房顶（在所有其他建筑生成完成后统一处理）
    /// </summary>
    /// <param name="map">地图</param>
    /// <param name="layoutRows">布局行数据</param>
    /// <param name="startX">起始X坐标</param>
    /// <param name="startZ">起始Z坐标</param>
    /// <param name="layoutHeight">布局高度</param>
    private void GenerateWallsAndRoof(Map map, List<string> layoutRows, int startX, int startZ, int layoutHeight)
    {
        for (int y = 0; y < layoutHeight; y++)
        {
            string row = layoutRows[y];
            for (int x = 0; x < row.Length; x++)
            {
                try
            {
                char cell = row[x];
                IntVec3 pos = new IntVec3(startX + x, 0, startZ + (layoutHeight - 1 - y));

                if (!pos.InBounds(map))
                    continue;

                // 处理墙体生成
                if (cell == '█' || cell == ','|| cell == 'd'||cell== '●')
                {
                    ThingDef wallDef = ThingDefOf.Wall;
                    ThingDef wallStuff = ThingDefOf.Steel;
                    if (cell == ',') wallStuff = ThingDef.Named("BlocksMarble");
                    if (cell == 'd') wallStuff = ThingDef.Named("WoodLog");
                    if (wallDef != null)
                    {
                        Thing wall = ThingMaker.MakeThing(wallDef, wallStuff);
                        GenSpawn.Spawn(wall, pos, map);
                    }
                    SpawnHiddenConduitIfNeeded(pos, map);
                }
                //植物不生房顶
                if (cell != '.'&& cell != '}' && cell != '{' && cell != 'β' && cell != '[' && cell != ']' && cell != 'R' && cell != 'j' && cell != 'a' && cell != 'ι')
                {
                    SetAppropriateRoofForPosition(map, pos);
                }
                else
                {
                    map.roofGrid.SetRoof(pos, null);
                }
                }
                catch (System.Exception ex)
                {
                    Log.Warning($"[RKU_GenStep_BioLab] Error generating wall/roof at ({startX + x}, {startZ + (layoutHeight - 1 - y)}): {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// 生成壁灯（在墙体和房顶生成完成后）
    /// </summary>
    private void GenerateWallLamps(Map map, List<string> layoutRows, int startX, int startZ, int layoutHeight)
    {
        for (int y = 0; y < layoutHeight; y++)
        {
            string row = layoutRows[y];
            for (int x = 0; x < row.Length; x++)
            {
                try
            {
                char cell = row[x];
                if (cell == 'L')
                {
                    IntVec3 pos = new IntVec3(startX + x, 0, startZ + (layoutHeight - 1 - y));
                    if (!pos.InBounds(map)) continue;

                    Thing wallLamp = ThingMaker.MakeThing(ThingDef.Named("WallLamp"));
                        if (TryGetWallLampPositionAndRotation(map, pos, out IntVec3 lampPos, out Rot4 wallLampRot))
                        {
                            SafeSpawn(wallLamp, lampPos, map, wallLampRot);
                    ConnectToPower(wallLamp, map);
                    ApplyTileTerrainPropagation(lampPos, map);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Log.Warning($"[RKU_GenStep_BioLab] Error generating wall lamp at ({startX + x}, {startZ + (layoutHeight - 1 - y)}): {ex.Message}");
                }
            }
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
        try
        {
        // 特殊处理DD
        if (cell == 'D' && x + 1 < layoutWidth && row[x + 1] == 'D')
        {
                ThingDef doorDef = null;
                Thing door = null;
            if (ModsConfig.AnomalyActive)
            {
                doorDef = DefDatabase<ThingDef>.GetNamed("SecurityDoor");
                door = ThingMaker.MakeThing(doorDef);
                    SafeSpawn(door, pos, map);
                    var buildingDoor = door as Building_Door;
                    if (buildingDoor != null)
                    {
                        buildingDoor.SetFaction(Find.FactionManager.FirstFactionOfDef(FactionDef.Named("Rakinia")));
                typeof(Building_Door).GetField("openInt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(door, false);
                    }
                ConnectToPower(door, map);
            }
            else
            {
                doorDef = DefDatabase<ThingDef>.GetNamed("OrnateDoor");
                ThingDef doorStuff = ThingDefOf.Steel; // 使用钢铁作为默认材质
                door = ThingMaker.MakeThing(doorDef, doorStuff);
                    SafeSpawn(door, pos, map);
            }
            SpawnHiddenConduitIfNeeded(pos, map);
            // 跳过下一个字符，因为已经处理了DD
            skipChars = 1;
            return true;
            }
        }
        catch (System.Exception ex)
        {
            Log.Warning($"[RKU_GenStep_BioLab] Error handling special cell '{cell}' at {pos}: {ex.Message}");
        }

        return false;
    }

    /// <summary>
    /// 安全地生成物品，如果失败则记录警告但不中断生成
    /// </summary>
    private bool SafeSpawn(Thing thing, IntVec3 pos, Map map, Rot4 rot = default)
    {
        if (thing == null || map == null || !pos.InBounds(map))
            return false;
            
        try
        {
            if (rot != default)
                GenSpawn.Spawn(thing, pos, map, rot);
            else
                GenSpawn.Spawn(thing, pos, map);
            return true;
        }
        catch (System.Exception ex)
        {
            Log.Warning($"[RKU_GenStep_BioLab] Failed to spawn {thing?.def?.defName ?? "null"} at {pos}: {ex.Message}");
            return false;
        }
    }

    private void GenerateCell(Map map, IntVec3 pos, char cellType, List<string> layoutRows, int startX, int startZ, int layoutHeight)
    {
        if (map == null || !pos.InBounds(map))
            return;
            
        try
    {
        map.terrainGrid.SetTerrain(pos, TerrainDefOf.Concrete);
        // 安全地清理位置上的现有物品
        List<Thing> things = pos.GetThingList(map);
        for (int i = things.Count - 1; i >= 0; i--)
        {
                if (things[i] != null && things[i].Position == pos)
            {
                things[i].Destroy();
            }
        }
        }
        catch (System.Exception ex)
        {
            Log.Warning($"[RKU_GenStep_BioLab] Error preparing cell at {pos}: {ex.Message}");
            return;
        }
        
        try
        {
        switch (cellType)
        {
            case '.': // 不管
                if (!pos.GetTerrain(map).affordances.Any(o => o.defName == "Heavy")) ;
                {
                    TerrainDef targetTerrain = TerrainDefOf.Soil;
                    map.terrainGrid.SetTerrain(pos, targetTerrain);
                }
                break;
            case ',': // 大理石墙
                break;
            case '*': // 大理石
                Thing mb = ThingMaker.MakeThing(ThingDef.Named("Marble"));
                    SafeSpawn(mb, pos, map);
                break;
            case '░': // 空地
                map.terrainGrid.SetTerrain(pos, TerrainDef.Named("AncientTile"));
                SpawnHiddenConduitIfNeeded(pos, map);
                break;
            case '♦': // 混凝土
                map.terrainGrid.SetTerrain(pos, TerrainDef.Named("Concrete"));
                SpawnHiddenConduitIfNeeded(pos, map); 
                break;
            case 'Δ': // 大理石破烂地面
                map.terrainGrid.SetTerrain(pos, TerrainDef.Named("FlagstoneMarble"));
                SpawnHiddenConduitIfNeeded(pos, map); 
                break;
            case '□': // 无菌地砖
                map.terrainGrid.SetTerrain(pos, TerrainDef.Named("SterileTile"));
                SpawnHiddenConduitIfNeeded(pos, map);
                break;
            case 'T': // 精致地毯
                map.terrainGrid.SetTerrain(pos, TerrainDef.Named("CarpetFineRed"));
                SpawnHiddenConduitIfNeeded(pos, map);
                break;
            case 'D': // 单个钢铁门
                ThingDef doorDef = ThingDefOf.Door;
                ThingDef doorStuff = ThingDefOf.Steel;
                if (doorDef != null)
                {
                    Thing singleDoor = ThingMaker.MakeThing(doorDef, doorStuff);
                    GenSpawn.Spawn(singleDoor, pos, map);
                }
                break;
            case 'G': // 纵向门
                ThingDef doorDefG;
                Thing door;
                if (ModsConfig.AnomalyActive)
                {
                    doorDefG = DefDatabase<ThingDef>.GetNamed("SecurityDoor");
                    door = ThingMaker.MakeThing(doorDefG);
                    GenSpawn.Spawn(door, pos, map);
                    (door as Building_Door).SetFaction(Find.FactionManager.FirstFactionOfDef(FactionDef.Named("Rakinia")));
                    typeof(Building_Door).GetField("openInt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(door, false);
                    ConnectToPower(door, map);
                }
                else
                {
                    doorDefG = DefDatabase<ThingDef>.GetNamed("OrnateDoor");
                    ThingDef doorStuffG = ThingDefOf.Steel;
                    door = ThingMaker.MakeThing(doorDefG, doorStuffG);
                    GenSpawn.Spawn(door, pos, map);
                }
                Thing doorG = ThingMaker.MakeThing(doorDefG);
                GenSpawn.Spawn(doorG, pos, map, Rot4.East);
                (doorG as Building_Door).SetFaction(Find.FactionManager.FirstFactionOfDef(FactionDef.Named("Rakinia")));
                ConnectToPower(doorG, map);
                SpawnHiddenConduitIfNeeded(pos, map);
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
            case 'z': // 木栅栏
                ThingDef woodFenceDef = ThingDef.Named("Fence");
                ThingDef woodfenceStuff = ThingDefOf.WoodLog;
                if (woodFenceDef != null)
                {
                    Thing fence = ThingMaker.MakeThing(woodFenceDef, woodfenceStuff);
                    GenSpawn.Spawn(fence, pos, map);
                }
                break;
            case 'n': // 栅栏门
                ThingDef fenceGateDef = ThingDef.Named("FenceGate");
                ThingDef fenceGateStuff = ThingDefOf.WoodLog;
                if (fenceGateDef != null)
                {
                    Thing fence = ThingMaker.MakeThing(fenceGateDef, fenceGateStuff);
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
            case 'j': // 水稻
                map.terrainGrid.SetTerrain(pos, TerrainDefOf.SoilRich);
                Thing rice = ThingMaker.MakeThing(ThingDef.Named("Plant_Rice"));
                Plant ricep = (Plant)GenSpawn.Spawn(rice, pos, map);
                if (ricep != null)
                {
                    ricep.Growth = Rand.Range(0.6f,1f);
                }
                break;
            case '}': // 玉米
                map.terrainGrid.SetTerrain(pos, TerrainDefOf.SoilRich);
                Thing corn = ThingMaker.MakeThing(ThingDef.Named("Plant_Corn"));
                Plant cornp = (Plant)GenSpawn.Spawn(corn, pos, map);
                if (cornp != null)
                {
                    cornp.Growth = Rand.Range(0.6f, 1f);
                }
                break; 
            case 'a': // 干草
                map.terrainGrid.SetTerrain(pos, TerrainDefOf.SoilRich);
                Thing hay = ThingMaker.MakeThing(ThingDef.Named("Plant_Haygrass"));
                Plant hayp = (Plant)GenSpawn.Spawn(hay, pos, map);
                if (hayp != null)
                {
                    hayp.Growth = Rand.Range(0.6f, 1f);
                }
                break;
            case 'N': // 发电机
                Thing cpg = ThingMaker.MakeThing(ThingDef.Named("ChemfuelPoweredGenerator"));
                GenSpawn.Spawn(cpg, pos, map);
                FillPowerStorage(cpg);
                FillFuelStorage(cpg);
                break; 
            case 'v': // 发电机
                Thing geothermalGenerator = ThingMaker.MakeThing(ThingDef.Named("GeothermalGenerator"));
                GenSpawn.Spawn(geothermalGenerator, pos, map);
                break; 
            case '=': // 休眠舱
                Thing xmc = ThingMaker.MakeThing(ThingDef.Named("AncientCryptosleepCasket"));
                GenSpawn.Spawn(xmc, pos, map,Rot4.East);
                break;
            case 'ｦ': // 古代储物柜（南）
                Thing cwgS = ThingMaker.MakeThing(ThingDef.Named("AncientLockerBank"));
                GenSpawn.Spawn(cwgS, pos, map, Rot4.South);
                break;
            case 'ｧ': // 古代储物柜（北）
                Thing cwgN = ThingMaker.MakeThing(ThingDef.Named("AncientLockerBank"));
                GenSpawn.Spawn(cwgN, pos, map, Rot4.North);
                break;
            case 'ｨ': // 古代储物柜（东）
                Thing cwgE = ThingMaker.MakeThing(ThingDef.Named("AncientLockerBank"));
                GenSpawn.Spawn(cwgE, pos, map, Rot4.East);
                break;
            case 'F': // 电池
                Thing btr = ThingMaker.MakeThing(ThingDef.Named("Battery"));
                GenSpawn.Spawn(btr, pos, map);
                FillPowerStorage(btr);
                break;
            case 'ﾛ': // 炉灶
                Thing stove = ThingMaker.MakeThing(ThingDef.Named("ElectricStove"));
                GenSpawn.Spawn(stove, pos, map, Rot4.North);
                ApplyTileTerrainPropagation(pos, map);
                break;
            case 'B': // 床
                Thing bed = ThingMaker.MakeThing(ThingDef.Named("Bed"), ThingDefOf.WoodLog);
                Rot4 bedRotation = (pos.z > 150) && (pos.x <= 80 || pos.x >= 173) ? GetBedRotation(pos, layoutRows, startX, startZ, layoutHeight) : Rot4.South;
                GenSpawn.Spawn(bed, pos, map, bedRotation);
                ApplyTileTerrainPropagation(pos, map);
                break;
            case 'ﾜ': // 床北
                Thing bedN = ThingMaker.MakeThing(ThingDef.Named("Bed"), ThingDefOf.WoodLog);
                GenSpawn.Spawn(bedN, pos, map, Rot4.North);
                ApplyTileTerrainPropagation(pos, map); 
                break;
            case '|': // 火把
                Thing torch = ThingMaker.MakeThing(ThingDef.Named("TorchLamp"));
                GenSpawn.Spawn(torch, pos, map);
                ApplyTileTerrainPropagation(pos, map);
                break;
            case 'C': // 仓鼠轮
                Thing hamsterWheel = ThingMaker.MakeThing(ThingDef.Named("RK_HamsterWheelGenerator"));
                GenSpawn.Spawn(hamsterWheel, pos, map);
                FillPowerStorage(hamsterWheel);
                break;
            case 'Q': // 医疗床
                ThingDef bedQStuff = ThingDefOf.Steel;
                Thing bedQ = ThingMaker.MakeThing(ThingDef.Named("HospitalBed"), bedQStuff);
                GenSpawn.Spawn(bedQ, pos, map, GetHospitalRotation(pos, map));
                ApplyTileTerrainPropagation(pos, map);
                // 30%概率在附近生成血渍
                if (Rand.Chance(0.3f))
                {
                    FilthMaker.TryMakeFilth(pos, map, ThingDefOf.Filth_Blood);
                }
                break;
            case 'M': // 医疗柜
                ThingDef vitalsMonitorStuff = ThingDefOf.Steel;
                Thing vitalsMonitor = ThingMaker.MakeThing(ThingDefOf.VitalsMonitor);
                GenSpawn.Spawn(vitalsMonitor, pos, map, GetHospitalRotation(pos, map));
                ApplyTileTerrainPropagation(pos, map);
                ConnectToPower(vitalsMonitor, map);
                break;
            case 'L': // 壁灯
                break;
            case 'A': // 高级研究台
                ThingDef researchBenchStuff = ThingDefOf.Steel;
                Thing researchBench = ThingMaker.MakeThing(ThingDef.Named("HiTechResearchBench"), researchBenchStuff);
                GenSpawn.Spawn(researchBench, pos, map, Rot4.South);
                ConnectToPower(researchBench, map);
                ApplyTileTerrainPropagation(pos, map);
                break;
            case 'P': // 自动炮塔
                Thing turret = ThingMaker.MakeThing(ThingDef.Named("Turret_AutoMiniTurret"));
                GenSpawn.Spawn(turret, pos, map);
                ConnectToPower(turret, map);
                ApplyTileTerrainPropagation(pos, map);
                break;
            case '#': // 2x2铀炮
                Thing sniperTurret = ThingMaker.MakeThing(ThingDef.Named("Turret_Sniper"));
                GenSpawn.Spawn(sniperTurret, pos, map);
                ApplyTileTerrainPropagation(pos, map);
                ConnectToPower(sniperTurret, map);
                break;
            case 'O': // 自动炮
                Thing mortar = ThingMaker.MakeThing(ThingDef.Named("Turret_Autocannon"));
                GenSpawn.Spawn(mortar, pos, map);
                ApplyTileTerrainPropagation(pos, map);
                ConnectToPower(mortar, map);
                break;
            case 't': // 机枪
                ThingDef turretStuff = ThingDefOf.Steel;
                Thing miniTurret = ThingMaker.MakeThing(ThingDef.Named("Turret_MiniTurret"), turretStuff);
                GenSpawn.Spawn(miniTurret, pos, map);
                ApplyTileTerrainPropagation(pos, map);
                ConnectToPower(miniTurret, map);
                break;
            case 'J': // 物品架
                ThingDef shelfStuff = ThingDefOf.WoodLog;
                if (pos.z <= 140)
                {
                    shelfStuff = ThingDefOf.Steel;
                }
                Thing shelf = ThingMaker.MakeThing(ThingDef.Named("Shelf"), shelfStuff);
                GenSpawn.Spawn(shelf, pos, map);
                ApplyTileTerrainPropagation(pos, map);
                break;
            case 'S': // （豪华）双人床
                ThingDef royalBedStuff = ThingDefOf.Gold; // 豪华材质
                Thing doubleBed = ThingMaker.MakeThing(ThingDef.Named("RoyalBed"), royalBedStuff);
                GenSpawn.Spawn(doubleBed, pos, map, Rot4.South);
                break;
            case 'H': // 花盆
                ThingDef plantPotStuff = ThingDefOf.WoodLog;
                Thing plantPot = ThingMaker.MakeThing(ThingDef.Named("PlantPot"), plantPotStuff);
                Thing yellowFlower = ThingMaker.MakeThing(ThingDef.Named("Plant_Daylily"));
                GenSpawn.Spawn(plantPot, pos, map);
                GenSpawn.Spawn(yellowFlower, pos, map);
                ApplyTileTerrainPropagation(pos, map);
                break;
            case 'r': // 畜栏
                ThingDef penStuff = ThingDefOf.WoodLog;
                Thing penMarker = ThingMaker.MakeThing(ThingDef.Named("PenMarker"), penStuff);
                GenSpawn.Spawn(penMarker, pos, map);
                ApplyTileTerrainPropagation(pos, map);
                break;
            case 'W': // 钢铁餐椅
                ThingDef chairStuff = ThingDefOf.Steel;
                Thing diningChair = ThingMaker.MakeThing(ThingDef.Named("DiningChair"), chairStuff);
                GenSpawn.Spawn(diningChair, pos, map, Rot4.South);
                break;
            case 'θ': // 床头柜
                ThingDef endTableStuff = ThingDefOf.WoodLog;
                Thing endTable = ThingMaker.MakeThing(ThingDef.Named("EndTable"), endTableStuff);
                GenSpawn.Spawn(endTable, pos, map);
                ApplyTileTerrainPropagation(pos, map);
                break;
            case 'α'://矮柜
                ThingDef dresserStuff = ThingDefOf.Steel;
                Thing dresser = ThingMaker.MakeThing(ThingDef.Named("Dresser"), dresserStuff);
                Rot4 dresserRot = Rot4.West;
                GenSpawn.Spawn(dresser, pos, map, dresserRot);
                ApplyTileTerrainPropagation(pos, map);
                break;
            case 'ω': // 钢铁路障
                ThingDef barrierStuff = ThingDefOf.Steel;
                Thing barrier = ThingMaker.MakeThing(ThingDef.Named("Barricade"), barrierStuff);
                GenSpawn.Spawn(barrier, pos, map);
                break;
            case 'τ': // 2*4钢铁桌
                ThingDef tableStuff = ThingDefOf.Steel;
                Thing table = ThingMaker.MakeThing(ThingDef.Named("Table2x4c"), tableStuff);
                GenSpawn.Spawn(table, pos, map, Rot4.East);
                break;
            case 'ﾝ': // 3*3木桌
                ThingDef table3Stuff = ThingDefOf.Steel;
                Thing table3 = ThingMaker.MakeThing(ThingDef.Named("Table3x3c"), ThingDefOf.WoodLog);
                GenSpawn.Spawn(table3, pos, map, Rot4.East);
                break; 
            case 'ε': // 1*2钢铁桌
                ThingDef x2tableStuff = ThingDefOf.Steel;
                Thing microwave = ThingMaker.MakeThing(ThingDef.Named("Table1x2c"), x2tableStuff);
                GenSpawn.Spawn(microwave, pos, map, Rot4.East);
                break;
            case 'ﾖ': //马桶
                Thing ancientToilet = ThingMaker.MakeThing(ThingDef.Named("AncientToilet"));
                GenSpawn.Spawn(ancientToilet, pos, map);
                break; 
            case 'ζ': // 黑板
                ThingDef boardStuff = ThingDef.Named("BlocksMarble");
                Thing blackBoard = ThingMaker.MakeThing(ThingDefOf.Blackboard, boardStuff);
                GenSpawn.Spawn(blackBoard, pos, map, Rot4.South);
                break;
            case 'ￓ': // 黑板竖
                Thing blackBoardw = ThingMaker.MakeThing(ThingDefOf.Blackboard, ThingDefOf.WoodLog);
                GenSpawn.Spawn(blackBoardw, pos, map, Rot4.East);
                break;
            case 'ￛ': // 课桌
                Thing schoolDesk = ThingMaker.MakeThing(ThingDef.Named("SchoolDesk"), ThingDefOf.WoodLog);
                GenSpawn.Spawn(schoolDesk, pos, map, Rot4.East);
                break;
            case 'μ': // 灭火器
                Thing firefoamPopper = ThingMaker.MakeThing(ThingDef.Named("FirefoamPopper"));
                GenSpawn.Spawn(firefoamPopper, pos, map);
                break;
            case 'σ': // 通讯台
                Thing commsConsole = ThingMaker.MakeThing(ThingDef.Named("CommsConsole"));
                GenSpawn.Spawn(commsConsole, pos, map,Rot4.North);
                ConnectToPower(commsConsole, map);
                break;
            case 'π': // 塑形仓
                Thing neuralSupercharger = ThingMaker.MakeThing(ThingDef.Named("BiosculpterPod"));
                GenSpawn.Spawn(neuralSupercharger, pos, map);
                ConnectToPower(neuralSupercharger, map);
                break;
            case 'E': // 泛光灯
                Thing floodlight = ThingMaker.MakeThing(ThingDef.Named("FloodLight"));
                GenSpawn.Spawn(floodlight, pos, map);
                ConnectToPower(floodlight, map);
                ApplyTileTerrainPropagation(pos, map);
                break;
            case 'K': // 2x4木桌
                ThingDef woodTableStuff = ThingDefOf.WoodLog;
                Thing woodTable = ThingMaker.MakeThing(ThingDef.Named("Table2x4c"), woodTableStuff);
                GenSpawn.Spawn(woodTable, pos, map, Rot4.East);
                break;
            case 'p': // 马蹄钉
                ThingDef pinStuff = ThingDefOf.WoodLog;
                Thing horseshoesPin = ThingMaker.MakeThing(ThingDef.Named("HorseshoesPin"), pinStuff);
                GenSpawn.Spawn(horseshoesPin, pos, map, Rot4.North);
                break;
            case 'c': // 木板凳
                ThingDef stoolStuff = ThingDefOf.WoodLog;
                Thing stool = ThingMaker.MakeThing(ThingDef.Named("Stool"), stoolStuff);
                GenSpawn.Spawn(stool, pos, map, Rot4.South);
                break;
            case 'u': // 木板凳竖
                Thing stoolw = ThingMaker.MakeThing(ThingDef.Named("Stool"), ThingDefOf.WoodLog);
                GenSpawn.Spawn(stoolw, pos, map, Rot4.West);
                break;
            case 'U': // 火盆
                ThingDef brazierStuff = ThingDefOf.Steel;
                Thing brazier = ThingMaker.MakeThing(ThingDef.Named("Brazier"), brazierStuff);
                GenSpawn.Spawn(brazier, pos, map);
                ApplyTileTerrainPropagation(pos, map);
                break;
            case 'V': // 物品架
                ThingDef shelfVStuff = ThingDefOf.Steel;
                Thing shelfV = ThingMaker.MakeThing(ThingDef.Named("Shelf"), shelfVStuff);
                GenSpawn.Spawn(shelfV, pos, map, Rot4.South);
                ApplyTileTerrainPropagation(pos, map);

                // 30%几率生成第一个物品
                if (Rand.Range(0f, 1f) < 0.5f)
                {
                    Thing item1 = Utils.GenerateRandomItem();
                    GenSpawn.Spawn(item1, pos, map);

                    if (item1 != null)
                    {
                        // 40%几率再生成第二个物品
                        if (Rand.Range(0f, 1f) < 0.4f)
                        {
                            Thing item2 = Utils.GenerateRandomItem();
                            GenSpawn.Spawn(item2, pos + new IntVec3(1, 0, 0), map);
                        }
                    }
                }
                break;
            case 'X': // 小物品架
                ThingDef smallShelfStuff = ThingDefOf.WoodLog;
                Thing smallShelf = ThingMaker.MakeThing(ThingDef.Named("ShelfSmall"), smallShelfStuff);
                GenSpawn.Spawn(smallShelf, pos, map);
                ApplyTileTerrainPropagation(pos, map);
                break;
            case 'x': // 火化炉
                Thing electricCrematorium = ThingMaker.MakeThing(ThingDef.Named("ElectricCrematorium"), ThingDefOf.Steel);
                GenSpawn.Spawn(electricCrematorium, pos, map);
                ApplyTileTerrainPropagation(pos, map);
                ConnectToPower(electricCrematorium, map);
                break;
            case 'Y': // 远古路障
                if (ModsConfig.OdysseyActive)
                {
                    Thing ancientBarrier = ThingMaker.MakeThing(ThingDef.Named("AncientBarrierLong"));
                    GenSpawn.Spawn(ancientBarrier, pos, map);
                }
                else
                {
                    Thing ancientBarrier = ThingMaker.MakeThing(ThingDef.Named("AncientConcreteBarrier"));
                    GenSpawn.Spawn(ancientBarrier, pos, map);
                }
                break;
            case 'b': // 采矿炸药（大
                if (ModsConfig.OdysseyActive)
                {
                    Thing miningExplosiveLarge = ThingMaker.MakeThing(ThingDef.Named("AncientExplosivesCrate"));
                    GenSpawn.Spawn(miningExplosiveLarge, pos, map);
                }
                else
                {
                    SpawnSandbag(pos, map);
                }
                break;
            case '?': // 采矿炸药（小
                if (ModsConfig.OdysseyActive)
                {
                    Thing miningExplosiveSmall = ThingMaker.MakeThing(ThingDef.Named("AncientMiningCharge"));
                    GenSpawn.Spawn(miningExplosiveSmall, pos, map);
                }
                else
                {
                    SpawnSandbag(pos, map);
                }
                break;
            case 's': // 沙袋
                SpawnSandbag(pos, map);
                break;
            case 'e': // 远古发电机（大
                Thing ancientFuelTankLarge = ThingMaker.MakeThing(ThingDef.Named("AncientGenerator"));
                GenSpawn.Spawn(ancientFuelTankLarge, pos, map);
                break;
            case 'f': // 远古储物罐（小
                Thing ancientFuelTankSmall = ThingMaker.MakeThing(ThingDef.Named("AncientCrate"));
                GenSpawn.Spawn(ancientFuelTankSmall, pos, map); 
                break;
            case 'ﾰ': // 远古液罐
                Thing ancientStorageCylinder = ThingMaker.MakeThing(ThingDef.Named("AncientStorageCylinder"));
                GenSpawn.Spawn(ancientStorageCylinder, pos, map,Rot4.West); 
                break;
            case 'g': // 平板电视
                Thing television = ThingMaker.MakeThing(ThingDef.Named("FlatscreenTelevision"));
                GenSpawn.Spawn(television, pos, map, Rot4.South);
                ConnectToPower(television, map);
                break;
            case 'k': // 木扑克桌
                ThingDef pokerTableStuff = ThingDefOf.WoodLog;
                Thing pokerTable = ThingMaker.MakeThing(ThingDef.Named("PokerTable"), pokerTableStuff);
                GenSpawn.Spawn(pokerTable, pos, map);
                ApplyTileTerrainPropagation(pos, map);
                break;
            case 'm': // 木台球桌
                ThingDef billiardsTableStuff = ThingDefOf.WoodLog;
                Thing billiardsTable = ThingMaker.MakeThing(ThingDef.Named("BilliardsTable"), billiardsTableStuff);
                GenSpawn.Spawn(billiardsTable, pos, map);
                ApplyTileTerrainPropagation(pos, map);
                break;
            case 'h': // 宏伟石碑（大理石
                ThingDef largeMonumentStuff = ThingDef.Named("BlocksMarble");
                Thing largeMonument = ThingMaker.MakeThing(ThingDef.Named("SteleGrand"), largeMonumentStuff);
                GenSpawn.Spawn(largeMonument, pos, map);
                break;
            case 'i': // 大石碑（大理石
                ThingDef smallMonumentStuff = ThingDef.Named("BlocksMarble");
                Thing smallMonument = ThingMaker.MakeThing(ThingDef.Named("SteleLarge"), smallMonumentStuff);
                GenSpawn.Spawn(smallMonument, pos, map);
                break;
            case '1': // 木地板
                map.terrainGrid.SetTerrain(pos, TerrainDef.Named("WoodPlankFloor"));
                SpawnHiddenConduitIfNeeded(pos, map);
                break;
            case '2': // 地毯（白
                map.terrainGrid.SetTerrain(pos, TerrainDef.Named("CarpetWhite"));
                SpawnHiddenConduitIfNeeded(pos, map);
                break;
            case '3': // 地毯（红 
                map.terrainGrid.SetTerrain(pos, TerrainDef.Named("CarpetRed"));
                SpawnHiddenConduitIfNeeded(pos, map);
                break;
            case '4': // 地毯（蓝 
                map.terrainGrid.SetTerrain(pos, TerrainDef.Named("CarpetBlue"));
                SpawnHiddenConduitIfNeeded(pos, map);
                break;
            case '5': // 精致地板（大理石
                map.terrainGrid.SetTerrain(pos, TerrainDef.Named("TileMarble"));
                SpawnHiddenConduitIfNeeded(pos, map);
                break;
            case '6': // 精致地毯（白
                map.terrainGrid.SetTerrain(pos, TerrainDef.Named("CarpetFineWhite"));
                SpawnHiddenConduitIfNeeded(pos, map);
                break;
            case '7': // 精致地毯（红 
                map.terrainGrid.SetTerrain(pos, TerrainDef.Named("CarpetFineRed"));
                SpawnHiddenConduitIfNeeded(pos, map);
                break;
            case '8': // 精致地毯（蓝
                map.terrainGrid.SetTerrain(pos, TerrainDef.Named("CarpetFineBlue"));
                SpawnHiddenConduitIfNeeded(pos, map);
                break;
            case ':': // 机械加工台
                Thing machiningTable = ThingMaker.MakeThing(ThingDef.Named("TableMachining"));
                Rot4 machiningRot = CheckBelowIsWall(layoutRows, pos, startX, startZ, layoutHeight) ? Rot4.South : Rot4.North;
                GenSpawn.Spawn(machiningTable, pos, map, machiningRot);
                ConnectToPower(machiningTable, map);
                ApplyTileTerrainPropagation(pos, map);
                break;
            case '(': // 工具柜（竖）
                Thing toolCabinet = ThingMaker.MakeThing(ThingDef.Named("ToolCabinet"));
                GenSpawn.Spawn(toolCabinet, pos, map,Rot4.East);
                ApplyTileTerrainPropagation(pos, map);
                break;
            case ')': // 工具柜（横）
                Thing toolCabinetS = ThingMaker.MakeThing(ThingDef.Named("ToolCabinet"));
                GenSpawn.Spawn(toolCabinetS, pos, map, Rot4.South);
                ApplyTileTerrainPropagation(pos, map);
                break;
            case 'y': // 锻造台（电力）
                Thing electricSmithy = ThingMaker.MakeThing(ThingDef.Named("ElectricSmithy"));
                Rot4 smithyRot = CheckBelowIsWall(layoutRows, pos, startX, startZ, layoutHeight) ? Rot4.South : Rot4.North;
                GenSpawn.Spawn(electricSmithy, pos, map, smithyRot);
                ConnectToPower(electricSmithy, map);
                ApplyTileTerrainPropagation(pos, map);
                break;
            case '~': // 钻机
                Thing drill = ThingMaker.MakeThing(ThingDef.Named("RKU_DrillingVehicle"));
                GenSpawn.Spawn(drill, pos, map);
                ApplyTileTerrainPropagation(pos, map);
                break;
            case '!': // 货运钻机
                Thing cargoDrill = ThingMaker.MakeThing(ThingDef.Named("RKU_DrillingVehicleCargo"));
                GenSpawn.Spawn(cargoDrill, pos, map);
                ApplyTileTerrainPropagation(pos, map);
                break;
            case '@': // 武装钻机
                Thing armedDrill = ThingMaker.MakeThing(ThingDef.Named("RKU_DrillingVehicleWithTurret"));
                GenSpawn.Spawn(armedDrill, pos, map);
                ApplyTileTerrainPropagation(pos, map);
                break;
            case 'η': // 大理石石板
                map.terrainGrid.SetTerrain(pos, TerrainDef.Named("TileMarble"));
                SpawnHiddenConduitIfNeeded(pos, map);
                break;
            case '{': // 莓果
                map.terrainGrid.SetTerrain(pos, TerrainDefOf.SoilRich);
                Thing berry = ThingMaker.MakeThing(ThingDef.Named("Plant_Berry"));
                Plant berryp = (Plant)GenSpawn.Spawn(berry, pos, map);
                if (berryp != null)
                {
                    berryp.Growth = Rand.Range(0.6f, 1f);
                }
                break;
            case '[': // 恶魔菇
                map.terrainGrid.SetTerrain(pos, TerrainDefOf.SoilRich);
                Thing devilstrand = ThingMaker.MakeThing(ThingDef.Named("Plant_Devilstrand"));
                Plant devilstrandp = (Plant)GenSpawn.Spawn(devilstrand, pos, map);
                if (devilstrandp != null)
                {
                    devilstrandp.Growth = Rand.Range(0.6f, 1f);
                }
                break;
            case ']': // 棉花
                map.terrainGrid.SetTerrain(pos, TerrainDefOf.SoilRich);
                Thing cotton = ThingMaker.MakeThing(ThingDef.Named("Plant_Cotton"));
                Plant cottonp = (Plant)GenSpawn.Spawn(cotton, pos, map);
                if (cottonp != null)
                {
                    cottonp.Growth = Rand.Range(0.6f, 1f);
                }
                break;
            case ';': // 浅水
                map.terrainGrid.SetTerrain(pos, TerrainDefOf.WaterShallow);
                break;
            case 'β': // 啤酒花
                map.terrainGrid.SetTerrain(pos, TerrainDefOf.SoilRich);
                Thing hops = ThingMaker.MakeThing(ThingDef.Named("Plant_Hops"));
                Plant hopsp = (Plant)GenSpawn.Spawn(hops, pos, map);
                if (hopsp != null)
                {
                    hopsp.Growth = Rand.Range(0.6f, 1f);
                }
                break;
            case 'γ': // 木墙
                ThingDef woodWallDef = ThingDefOf.Wall;
                ThingDef woodWallStuff = ThingDefOf.WoodLog;
                if (woodWallDef != null)
                {
                    Thing woodWall = ThingMaker.MakeThing(woodWallDef, woodWallStuff);
                    GenSpawn.Spawn(woodWall, pos, map);
                }
                SpawnHiddenConduitIfNeeded(pos, map);
                break;
            case 'δ': // 木门
                ThingDef woodDoorDef = ThingDefOf.Door;
                ThingDef woodDoorStuff = ThingDefOf.WoodLog;
                if (woodDoorDef != null)
                {
                    Thing woodDoor = ThingMaker.MakeThing(woodDoorDef, woodDoorStuff);
                    GenSpawn.Spawn(woodDoor, pos, map);
                }
                SpawnHiddenConduitIfNeeded(pos, map);
                break;
            case 'ι': // 草药
                map.terrainGrid.SetTerrain(pos, TerrainDefOf.SoilRich);
                Thing healroot = ThingMaker.MakeThing(ThingDef.Named("Plant_Healroot"));
                Plant healrootp = (Plant)GenSpawn.Spawn(healroot, pos, map);
                if (healrootp != null)
                {
                    healrootp.Growth = Rand.Range(0.6f, 1f);
                }
                break;
            case '$': // 钢制地板（黄）
                map.terrainGrid.SetTerrain(pos, TerrainDef.Named("CarpetYellowPastel"));
                SpawnHiddenConduitIfNeeded(pos, map);
                break;
            case '%': // 钢制地板（黑）
                map.terrainGrid.SetTerrain(pos, TerrainDef.Named("CarpetFineBlack"));
                SpawnHiddenConduitIfNeeded(pos, map);
                break;
            case '^': // 自动机枪塔（物品）
                Thing autoTurretItem = ThingMaker.MakeThing(ThingDef.Named("Turret_AutoMiniTurret"));
                GenSpawn.Spawn(autoTurretItem, pos, map);
                ConnectToPower(autoTurretItem, map);
                ApplyTileTerrainPropagation(pos, map);
                break;
            case '\'': // 木质电力裁缝台
                ThingDef tailoringBenchStuff = ThingDefOf.WoodLog;
                Thing tailoringBench = ThingMaker.MakeThing(ThingDef.Named("ElectricTailoringBench"), tailoringBenchStuff);
                GenSpawn.Spawn(tailoringBench, pos, map, Rot4.South);
                ConnectToPower(tailoringBench, map);
                ApplyTileTerrainPropagation(pos, map);
                break;
            case '/': // 木质制药台
                ThingDef drugLabStuff = ThingDefOf.WoodLog;
                Thing drugLab = ThingMaker.MakeThing(ThingDef.Named("DrugLab"), drugLabStuff);
                GenSpawn.Spawn(drugLab, pos, map, Rot4.South);
                ConnectToPower(drugLab, map);
                ApplyTileTerrainPropagation(pos, map);
                break;
            case 'I': // 迫击炮
                Thing mortarDef = ThingMaker.MakeThing(ThingDef.Named("Turret_Mortar"), GenStuff.DefaultStuffFor(ThingDef.Named("Turret_Mortar")));
                GenSpawn.Spawn(mortarDef, pos, map);
                ApplyTileTerrainPropagation(pos, map);
                break;
            case 'l': // 物品架（迫击炮弹）
                ThingDef mortarShellShelfStuff = ThingDefOf.Steel;
                Thing mortarShellShelf = ThingMaker.MakeThing(ThingDef.Named("Shelf"), mortarShellShelfStuff);
                GenSpawn.Spawn(mortarShellShelf, pos, map);
                // 生成迫击炮弹
                Thing mortarShell = ThingMaker.MakeThing(ThingDef.Named("Shell_HighExplosive"));
                mortarShell.stackCount = Rand.RangeInclusive(5, 15);
                GenSpawn.Spawn(mortarShell, pos, map);
                ApplyTileTerrainPropagation(pos, map);
                break;
            case 'o': // 木讲台
                ThingDef lecternStuff = ThingDefOf.WoodLog;
                Thing lectern = ThingMaker.MakeThing(ThingDef.Named("Lectern"), lecternStuff);
                GenSpawn.Spawn(lectern, pos, map, Rot4.South);
                ApplyTileTerrainPropagation(pos, map);
                break;
            case 'q': // 酿造台
                Thing brewingBench = ThingMaker.MakeThing(ThingDef.Named("Brewery"));
                GenSpawn.Spawn(brewingBench, pos, map, Rot4.South);
                ConnectToPower(brewingBench, map);
                ApplyTileTerrainPropagation(pos, map);
                break;
            case 'w': // 发酵桶
                Thing fermentingBarrel = ThingMaker.MakeThing(ThingDef.Named("FermentingBarrel"));
                GenSpawn.Spawn(fermentingBarrel, pos, map);
                ApplyTileTerrainPropagation(pos, map);
                break;
            case '_': // 木餐椅
                ThingDef woodChairStuff = ThingDefOf.WoodLog;
                Thing woodChair = ThingMaker.MakeThing(ThingDef.Named("DiningChair"), woodChairStuff);
                GenSpawn.Spawn(woodChair, pos, map, Rot4.South);
                break;
            case '+': // 1x2木桌
                ThingDef woodTable1x2Stuff = ThingDefOf.WoodLog;
                Thing woodTable1x2 = ThingMaker.MakeThing(ThingDef.Named("Table1x2c"), woodTable1x2Stuff);
                GenSpawn.Spawn(woodTable1x2, pos, map, Rot4.East);
                break;
            case '"': // 精密装配台
                Thing assemblyBench = ThingMaker.MakeThing(ThingDef.Named("TableMachining"));
                Rot4 assemblyRot = CheckBelowIsWall(layoutRows, pos, startX, startZ, layoutHeight) ? Rot4.South : Rot4.North;
                GenSpawn.Spawn(assemblyBench, pos, map, assemblyRot);
                ConnectToPower(assemblyBench, map);
                ApplyTileTerrainPropagation(pos, map);
                break;
            case '`': // 远古液罐
                Thing ancientLiquidTank = ThingMaker.MakeThing(ThingDef.Named("AncientStorageCylinder"));
                GenSpawn.Spawn(ancientLiquidTank, pos, map, Rot4.West);
                break;
            default:
                map.terrainGrid.SetTerrain(pos, DefDatabase<TerrainDef>.GetNamed("AncientTile"));
                break;
            }
        }
        catch (System.Exception ex)
        {
            Log.Warning($"[RKU_GenStep_BioLab] Error generating cell at {pos} with character '{cellType}': {ex.Message}");
            // Continue generation even if this cell fails
        }
    }

    /// <summary>
    /// 为发电设备或电池充满电量
    /// </summary>
    /// <param name="thing">发电设备或电池</param>
    private void FillPowerStorage(Thing thing)
    {
        var powerComp = thing.TryGetComp<CompPowerBattery>();
        if (powerComp != null)
        {
            powerComp.SetStoredEnergyPct(1f); // 设置为100%电量
        }
    }

    /// <summary>
    /// 为发电机充满燃料
    /// </summary>
    /// <param name="thing">发电机</param>
    private void FillFuelStorage(Thing thing)
    {
        var fuelComp = thing.TryGetComp<CompRefuelable>();
        if (fuelComp != null)
        {
            fuelComp.Refuel(fuelComp.Props.fuelCapacity); // 设置为满燃料
        }
    }

    /// <summary>
    /// 将物品连接到电力网络
    /// </summary>
    /// <param name="thing">需要连接电力的物品</param>
    /// <param name="map">地图</param>
    private void ConnectToPower(Thing thing, Map map)
    {
        var powerComp = thing.TryGetComp<CompPowerTrader>();
        if (powerComp != null)
        {
            powerComp.PowerOn = true;
        }
    }

    /// <summary>
    /// 医院下半部分医疗床朝向处理
    /// </summary>
    /// <param name="pos">医疗床位置</param>
    /// <param name="map">当前地图</param>
    /// <returns></returns>
    private Rot4 GetHospitalRotation(IntVec3 pos, Map map)
    {
        if (pos.z <= 120 &&
            pos.z >= 118)
        {
            return Rot4.North;
        }
        return Rot4.South;
    }


    /// <summary>
    /// 为指定位置设置合适的房顶（替换厚岩顶为建造房顶）
    /// </summary>
    /// <param name="map">地图</param>
    /// <param name="pos">位置</param>
    private void SetAppropriateRoofForPosition(Map map, IntVec3 pos)
    {
        // 如果位置不在房间中，则清除房顶
        if (!IsPositionInRoom(map, pos))
        {
            map.roofGrid.SetRoof(pos, null);
            return;
        }

        RoofDef existingRoof = map.roofGrid.RoofAt(pos);
        if (existingRoof == RoofDefOf.RoofRockThick)
        {
            map.roofGrid.SetRoof(pos, RoofDefOf.RoofConstructed);
        }
        else if (existingRoof == null || existingRoof != RoofDefOf.RoofConstructed)
        {
            map.roofGrid.SetRoof(pos, RoofDefOf.RoofConstructed);
        }
    }

    /// <summary>
    /// 检查指定位置是否在房间中
    /// </summary>
    /// <param name="map">地图</param>
    /// <param name="pos">位置</param>
    /// <returns>是否在房间中</returns>
    private bool IsPositionInRoom(Map map, IntVec3 pos)
    {
        Room room = pos.GetRoom(map);
        return room != null;
    }

    /// <summary>
    /// 将指定位置的地形设置为周围8格中最多的地形类型
    /// </summary>
    /// <param name="thing">需要连接电力的物品</param>
    /// <param name="map">地图</param>
    /// <param name="originalPos">原始位置</param>
    /// <param name="lampPos">输出：壁灯实际位置</param>
    /// <param name="rotation">输出：壁灯朝向</param>
    /// <returns>是否找到合适的墙壁位置</returns>
    private bool TryGetWallLampPositionAndRotation(Map map, IntVec3 originalPos, out IntVec3 lampPos, out Rot4 rotation)
    {
        lampPos = originalPos;
        rotation = Rot4.North;
        IntVec3 northPos = originalPos + IntVec3.North;
        IntVec3 eastPos = originalPos + IntVec3.East;
        IntVec3 southPos = originalPos + IntVec3.South;
        IntVec3 westPos = originalPos + IntVec3.West;

        if (northPos.InBounds(map) && northPos.GetEdifice(map) != null && northPos.GetEdifice(map).def == ThingDefOf.Wall)
        {
            lampPos = originalPos;
            rotation = Rot4.North;
            return true;
        }
        else if (eastPos.InBounds(map) && eastPos.GetEdifice(map) != null && eastPos.GetEdifice(map).def == ThingDefOf.Wall)
        {
            lampPos = originalPos;
            rotation = Rot4.East;
            return true;
        }
        else if (southPos.InBounds(map) && southPos.GetEdifice(map) != null && southPos.GetEdifice(map).def == ThingDefOf.Wall)
        {
            lampPos = originalPos;
            rotation = Rot4.South;
            return true;
        }
        else if (westPos.InBounds(map) && westPos.GetEdifice(map) != null && westPos.GetEdifice(map).def == ThingDefOf.Wall)
        {
            lampPos = originalPos;
            rotation = Rot4.West;
            return true;
        }
        return false;
    }

    private void SpawnHiddenConduitIfNeeded(IntVec3 pos, Map map)
    {
        if (!pos.GetThingList(map).Any(o => o.def == ThingDef.Named("HiddenConduit")))
        {
            Thing conduit = ThingMaker.MakeThing(ThingDef.Named("HiddenConduit"));
            GenSpawn.Spawn(conduit, pos, map);
        }
    }

    /// <summary>
    /// 获取床的朝向，朝向布局中最近的墙壁字符（z>150时）
    /// </summary>
    /// <param name="pos">床位置</param>
    /// <param name="layoutRows">布局字符画</param>
    /// <param name="startX">布局起始X坐标</param>
    /// <param name="startZ">布局起始Z坐标</param>
    /// <param name="layoutHeight">布局高度</param>
    /// <returns>朝向墙壁的方向，如果没有找到墙壁则返回南向</returns>
    private Rot4 GetBedRotation(IntVec3 pos, List<string> layoutRows, int startX, int startZ, int layoutHeight)
    {
        const int maxDistance = 5;
        // 检查四个方向
        Rot4[] directions = { Rot4.North, Rot4.East, Rot4.South, Rot4.West };
        IntVec3[] directionVectors = { IntVec3.North, IntVec3.East, IntVec3.South, IntVec3.West };

        // 将世界坐标转换为布局坐标
        int layoutX = pos.x - startX;
        int layoutY = layoutHeight - 1 - (pos.z - startZ);

        for (int distance = 1; distance <= maxDistance; distance++)
        {
            for (int dirIndex = 0; dirIndex < directions.Length; dirIndex++)
            {
                int checkLayoutX = layoutX;
                int checkLayoutY = layoutY;

                // 根据方向调整布局坐标
                if (directions[dirIndex] == Rot4.North)
                {
                    checkLayoutY = layoutY - distance;
                }
                else if (directions[dirIndex] == Rot4.West)
                {
                    checkLayoutX = layoutX + distance;
                }
                else if (directions[dirIndex] == Rot4.South)
                {
                    checkLayoutY = layoutY + distance;
                }
                else if (directions[dirIndex] == Rot4.East)
                {
                    checkLayoutX = layoutX - distance;
                }

                // 检查布局边界
                if (checkLayoutX < 0 || checkLayoutX >= layoutRows[0].Length ||
                    checkLayoutY < 0 || checkLayoutY >= layoutRows.Count)
                    continue;
                if (layoutRows[checkLayoutY][checkLayoutX] == '█')
                {
                    return directions[dirIndex];
                }
            }
        }
        return Rot4.South; // 默认朝南
    }

    /// <summary>
    /// 检查周围格子的地形，如果有足够的瓷砖或地毯，则设置当前位置的地形
    /// </summary>
    /// <param name="pos">当前位置</param>
    /// <param name="map">地图</param>
    private void ApplyTileTerrainPropagation(IntVec3 pos, Map map)
    {
        // 定义需要检查的地形
        string[] tileTerrainNames = { "SterileTile", "Floor_Carpet_Fine", "CarpetFineRed" };
        IntVec3[] adjacentPositions = {
            pos + IntVec3.North,
            pos + IntVec3.South,
            pos + IntVec3.East,
            pos + IntVec3.West
        };

        int tileCount = 0;
        TerrainDef mostCommonTerrain = null;
        Dictionary<TerrainDef, int> terrainCounts = new Dictionary<TerrainDef, int>();

        foreach (IntVec3 adjacentPos in adjacentPositions)
        {
            if (adjacentPos.InBounds(map))
            {
                TerrainDef terrain = map.terrainGrid.TerrainAt(adjacentPos);
                if (terrain != null && tileTerrainNames.Contains(terrain.defName))
                {
                    tileCount++;
                    if (!terrainCounts.ContainsKey(terrain))
                    {
                        terrainCounts[terrain] = 0;
                    }
                    terrainCounts[terrain]++;
                }
            }
        }
        // 如果周围有2个或以上的瓷砖/地毯，则设置当前位置为最常见的地形
        if (tileCount >= 2)
        {
            int maxCount = 0;
            foreach (var kvp in terrainCounts)
            {
                if (kvp.Value > maxCount)
                {
                    maxCount = kvp.Value;
                    mostCommonTerrain = kvp.Key;
                }
            }
            if (mostCommonTerrain != null)
            {
                map.terrainGrid.SetTerrain(pos, mostCommonTerrain);
            }
        }
    }

    /// <summary>
    /// 在指定位置生成沙袋
    /// </summary>
    /// <param name="pos">生成位置</param>
    /// <param name="map">地图</param>
    private void SpawnSandbag(IntVec3 pos, Map map)
    {
        ThingDef sandbagDef = ThingDef.Named("Sandbags");
        ThingDef sandbagStuff = ThingDefOf.Cloth;
        Thing sandbag = ThingMaker.MakeThing(sandbagDef, sandbagStuff);
        GenSpawn.Spawn(sandbag, pos, map);
    }

    /// <summary>
    /// 检查布局中下面一行对应位置是否是墙体
    /// </summary>
    /// <param name="layoutRows">布局行数据</param>
    /// <param name="pos">当前位置（世界坐标）</param>
    /// <param name="startX">布局起始X坐标</param>
    /// <param name="startZ">布局起始Z坐标</param>
    /// <param name="layoutHeight">布局高度</param>
    /// <returns>如果下面一行对应位置是墙体则返回true</returns>
    private bool CheckBelowIsWall(List<string> layoutRows, IntVec3 pos, int startX, int startZ, int layoutHeight)
    {
        int layoutX = pos.x - startX;
        int layoutY = layoutHeight - 1 - (pos.z - startZ);
        int belowY = layoutY + 1;
        if (belowY < 0 || belowY >= layoutRows.Count)
            return false;
        if (layoutX < 0 || layoutX >= layoutRows[belowY].Length)
            return false;
        char belowChar = layoutRows[belowY][layoutX];
        return belowChar == '█' || belowChar == ',' || belowChar == 'd' || belowChar == '●';
    }
}