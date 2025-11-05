using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.Noise;

namespace RatkinUnderground
{
    public class RKU_GenStep_FleshBeast : GenStep
    {
        public override int SeedPart => 56436868;
        private const float FLESH_SPAWN_CHANCE = 0.005f; // 瘤子团生成概率
        List<IntVec3> cells = new List<IntVec3>();

        public override void Generate(Map map, GenStepParams parms)
        {
            map.Biome.plantDensity = 0.7f;
            if (map == null) return;

            // 随机生成虫子
            GenerateRandomInsects(map);

            // 生成母兽
            IntVec3 spawnCell = cells.RandomElement();
            Pawn flesh = PawnGenerator.GeneratePawn(PawnKindDefOf.Dreadmeld, Faction.OfEntities);
            GenSpawn.Spawn(flesh, spawnCell, map, WipeMode.Vanish);
        }

        void ClearCell(Map map, IntVec3 cell)
        {
            // 安全地清除所有物品和建筑
            Building edifice = cell.GetEdifice(map);
            if (edifice != null)
            {
                edifice.Destroy(DestroyMode.Vanish);
            }
            List<Thing> things = new List<Thing>(cell.GetThingList(map));
            foreach (Thing thing in things)
            {
                if (thing != null && !thing.Destroyed)
                {
                    thing.Destroy();
                }
            }
        }

        // 随机生成哈基瘤
        void GenerateRandomInsects(Map map)
        {
            // 遍历所有单元格
            foreach (IntVec3 cell in map.AllCells)
            {
                // 检查格子是否可以放置虫子
                if (cell.Standable(map) && cell.GetFirstPawn(map) == null)
                {
                    // 检查是否在地图边缘20格内
                    if (cell.x < 20 || cell.x >= map.Size.x - 20 || cell.z < 20 || cell.z >= map.Size.z - 20)
                    {
                        continue; // 跳过边缘区域
                    }
                    cells.Add(cell);
                    if (Rand.Value < FLESH_SPAWN_CHANCE)
                    {
                        foreach(var p in SpawnGroup())
                        {
                            Pawn flesh = PawnGenerator.GeneratePawn(p, Faction.OfEntities);
                            GenSpawn.Spawn(flesh, cell, map, WipeMode.Vanish);
                        }
                    }
                }
            }
        }

        // 以群的方式生成
        List<PawnKindDef> SpawnGroup()
        {
            List<PawnKindDef> fleshGroup = new List<PawnKindDef>();
            int count = Rand.Range(3, 8);
            for(int i = 0; i < count; i++)
            {
                fleshGroup.Add(GetRandomInsectKind());
            }
            return fleshGroup;
        }

        // 随机瘤子
        PawnKindDef GetRandomInsectKind()
        {
            List<PawnKindDef> insectKinds = new List<PawnKindDef>
            {
                PawnKindDefOf.Bulbfreak,
                PawnKindDefOf.Trispike,
                PawnKindDefOf.Toughspike,
                PawnKindDefOf.Fingerspike
            };
            return insectKinds.RandomElement();
        }
    }
}
