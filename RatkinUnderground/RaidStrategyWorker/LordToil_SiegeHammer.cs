using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RatkinUnderground
{
    public class LordToil_SiegeHammer : LordToil_Siege
    {
        private LordToilData_Siege Data => (LordToilData_Siege)data;
        public LordToil_SiegeHammer(IntVec3 siegeCenter, float blueprintPoints) : base(siegeCenter, blueprintPoints)
        {
            Log.Message($"[RKU] LordToil_SiegeHammer 构造函数调用 - 中心: {siegeCenter}, 点数: {blueprintPoints}");
        }
        public override void Init()
        {
            Log.Message("[RKU] LordToil_SiegeHammer.Init() 开始执行");
            base.Init(); // 调用 LordToil_Siege.Init()
            LordToilData_Siege data = Data;
            Log.Message($"[RKU] LordToil_SiegeHammer.Init() 数据初始化完成 - 中心: {data.siegeCenter}, 点数: {data.blueprintPoints}");
            data.baseRadius = Mathf.InverseLerp(14f, 25f, (float)lord.ownedPawns.Count / 50f);
            data.baseRadius = Mathf.Clamp(data.baseRadius, 14f, 25f);
            List<Thing> list = new List<Thing>();
            List<List<Thing>> list2 = new List<List<Thing>>();
            foreach (Blueprint_Build item2 in SiegeBlueprintPlacer_Hammer.PlaceBlueprints(data.siegeCenter, base.Map, lord.faction, data.blueprintPoints))
            {
                if (item2 == null)
                {
                    Log.Error("[RKU] SiegeBlueprintPlacer_Hammer.PlaceBlueprints 返回了null蓝图，跳过");
                    continue;
                }
                data.blueprints.Add(item2);
                foreach (ThingDefCountClass cost in item2.TotalMaterialCost())
                {
                    if (cost.thingDef == null)
                    {
                        Log.Error($"[RKU] 建筑 {item2.def.defName} 的材料成本中包含null ThingDef，跳过");
                        continue;
                    }

                    Thing thing = list.FirstOrDefault((Thing t) => t.def == cost.thingDef);
                    if (thing != null)
                    {
                        thing.stackCount += cost.count;
                        continue;
                    }

                    try
                    {
                        Thing thing2 = ThingMaker.MakeThing(cost.thingDef);
                        thing2.stackCount = cost.count;
                        list.Add(thing2);
                    }
                    catch (System.Exception e)
                    {
                        Log.Error($"[RKU] 创建材料 {cost.thingDef.defName} 时出错: {e.Message}");
                    }
                }
            }

            for (int i = 0; i < list.Count; i++)
            {
                list[i].stackCount = Mathf.CeilToInt((float)list[i].stackCount * Rand.Range(1f, 1.2f));
            }

            for (int j = 0; j < list.Count; j++)
            {
                while (list[j].stackCount > list[j].def.stackLimit)
                {
                    int num = Mathf.CeilToInt((float)list[j].def.stackLimit * Rand.Range(0.9f, 0.999f));
                    Thing thing4 = ThingMaker.MakeThing(list[j].def);
                    thing4.stackCount = num;
                    list[j].stackCount -= num;
                    list.Add(thing4);
                }
            }
            list2.Add(list);
            List<Thing> list3 = new List<Thing>();
            float mealCount = new FloatRange(1f, 3f).RandomInRange * (float)lord.ownedPawns.Count;
            int num2 = Mathf.RoundToInt(mealCount);
            for (int l = 0; l < num2; l++)
            {
                Thing item = ThingMaker.MakeThing(ThingDefOf.MealSurvivalPack);
                list3.Add(item);
            }
            list2.Add(list3);

            try
            {
                Log.Message($"[RKU] 开始投放物资到位置 {data.siegeCenter}, 材料组数: {list2.Count}");
                DropPodUtility.DropThingGroupsNear(data.siegeCenter, base.Map, list2);
                Log.Message("[RKU] 物资投放完成");
            }
            catch (System.Exception e)
            {
                Log.Error($"[RKU] 投放物资时出错: {e}");
            }
            data.desiredBuilderFraction = new FloatRange(0.25f, 0.4f).RandomInRange;
            Log.Message("[RKU] LordToil_SiegeHammer.Init() 完成");
        }

        public override void UpdateAllDuties()
        {
            Log.Message("[RKU] LordToil_SiegeHammer.UpdateAllDuties() 调用");
            base.UpdateAllDuties();
        }

        private static class SiegeBlueprintPlacer_Hammer
        {
            private static IntVec3 center;
            private static Faction faction;
            private static List<IntVec3> placedCoverLocs = new List<IntVec3>();
            private const int MaxArtyCount = 2;
            public const float ArtyCost = 60f;
            private const int MinCoverDistSquared = 36;
            private static readonly IntRange NumCoverRange = new IntRange(2, 4);
            private static readonly IntRange CoverLengthRange = new IntRange(2, 7);

            public static IEnumerable<Blueprint_Build> PlaceBlueprints(IntVec3 placeCenter, Map map, Faction placeFaction, float points)
            {
                center = placeCenter;
                faction = placeFaction;
                foreach (Blueprint_Build item in PlaceCoverBlueprints(map))
                {
                    yield return item;
                }

                foreach (Blueprint_Build item2 in PlaceHammerAttackers(points, map))
                {
                    yield return item2;
                }
            }

            private static bool CanPlaceBlueprintAt(IntVec3 root, Rot4 rot, ThingDef buildingDef, Map map, ThingDef stuffDef)
            {
                return GenConstruct.CanPlaceBlueprintAt_NewTemp(buildingDef, root, rot, map, godMode: false, null, null, stuffDef, ignoreEdgeArea: true, ignoreInteractionSpots: true, ignoreClearableFreeBuildings: true).Accepted;
            }

            private static IEnumerable<Blueprint_Build> PlaceCoverBlueprints(Map map)
            {
                placedCoverLocs.Clear();
                ThingDef coverThing;
                ThingDef coverStuff;
                if (Rand.Chance(0.5f))
                {
                    coverThing = ThingDefOf.Sandbags;
                    coverStuff = ThingDefOf.Cloth;
                }
                else
                {
                    coverThing = ThingDefOf.Barricade;
                    coverStuff = (Rand.Chance(0.5f) ? ThingDefOf.Steel : ThingDefOf.WoodLog);
                }

                int numCover = NumCoverRange.RandomInRange;
                for (int i = 0; i < numCover; i++)
                {
                    IntVec3 bagRoot = FindCoverRoot(map, coverThing, coverStuff);
                    if (!bagRoot.IsValid)
                    {
                        break;
                    }

                    Rot4 growDir = ((bagRoot.x <= center.x) ? Rot4.East : Rot4.West);
                    Rot4 growDirB = ((bagRoot.z <= center.z) ? Rot4.North : Rot4.South);
                    foreach (Blueprint_Build item in MakeCoverLine(bagRoot, map, growDir, CoverLengthRange.RandomInRange, coverThing, coverStuff))
                    {
                        yield return item;
                    }

                    bagRoot += growDirB.FacingCell;
                    foreach (Blueprint_Build item2 in MakeCoverLine(bagRoot, map, growDirB, CoverLengthRange.RandomInRange, coverThing, coverStuff))
                    {
                        yield return item2;
                    }
                }
            }

            private static IEnumerable<Blueprint_Build> PlaceHammerAttackers(float points, Map map)
            {
                ThingDef hammerAttackerDef = DefDatabase<ThingDef>.GetNamed("RKU_HammerAttacker");

                if (hammerAttackerDef == null)
                {
                    Log.Error("[RKU] 找不到锤子攻击者定义 HammerAttacker，跳过放置");
                    yield break;
                }

                if (points < 60f)
                {
                    Log.Message("[RKU] 点数不足，跳过放置锤子攻击者");
                    yield break;
                }

                Log.Message($"[RKU] 开始放置锤子攻击者: {hammerAttackerDef.defName}");

                Rot4 fixedRotation = Rot4.North;
                IntVec3 intVec = FindArtySpot(hammerAttackerDef, fixedRotation, map);

                if (!intVec.IsValid)
                {
                    Log.Warning($"[RKU] 找不到合适位置放置锤子攻击者 {hammerAttackerDef.defName}");
                    yield break;
                }

                Log.Message($"[RKU] 在位置 {intVec} 放置锤子攻击者蓝图");
                yield return GenConstruct.PlaceBlueprintForBuild(hammerAttackerDef, intVec, map, fixedRotation, faction, null);
            }

            private static IEnumerable<Blueprint_Build> MakeCoverLine(IntVec3 root, Map map, Rot4 growDir, int maxLength, ThingDef coverThing, ThingDef coverStuff)
            {
                IntVec3 cur = root;
                for (int i = 0; i < maxLength; i++)
                {
                    if (!CanPlaceBlueprintAt(cur, Rot4.North, coverThing, map, coverStuff))
                    {
                        break;
                    }

                    yield return GenConstruct.PlaceBlueprintForBuild(coverThing, cur, map, Rot4.North, faction, coverStuff);
                    placedCoverLocs.Add(cur);
                    cur += growDir.FacingCell;
                }
            }

            private static IntVec3 FindCoverRoot(Map map, ThingDef coverThing, ThingDef coverStuff)
            {
                CellRect cellRect = CellRect.CenteredOn(center, 13);
                cellRect.ClipInsideMap(map);
                CellRect cellRect2 = CellRect.CenteredOn(center, 8);
                cellRect2.ClipInsideMap(map);
                int num = 0;
                IntVec3 randomCell;
                while (true)
                {
                    num++;
                    if (num > 200)
                    {
                        return IntVec3.Invalid;
                    }

                    randomCell = cellRect.RandomCell;
                    if (cellRect2.Contains(randomCell) || !map.reachability.CanReach(randomCell, center, PathEndMode.OnCell, TraverseMode.NoPassClosedDoors, Danger.Deadly) || !CanPlaceBlueprintAt(randomCell, Rot4.North, coverThing, map, coverStuff))
                    {
                        continue;
                    }

                    bool flag = false;
                    for (int i = 0; i < placedCoverLocs.Count; i++)
                    {
                        if ((float)(placedCoverLocs[i] - randomCell).LengthHorizontalSquared < 36f)
                        {
                            flag = true;
                        }
                    }

                    if (!flag)
                    {
                        break;
                    }
                }

                return randomCell;
            }

            private static IntVec3 FindArtySpot(ThingDef artyDef, Rot4 rot, Map map)
            {
                CellRect cellRect = CellRect.CenteredOn(center, 20);
                cellRect.ClipInsideMap(map);
                int num = 0;
                IntVec3 randomCell;
                do
                {
                    num++;
                    if (num > 200)
                    {
                        return IntVec3.Invalid;
                    }

                    randomCell = cellRect.RandomCell;
                }
                while (!map.reachability.CanReach(randomCell, center, PathEndMode.OnCell, TraverseMode.NoPassClosedDoors, Danger.Deadly) || randomCell.Roofed(map) || !CanPlaceBlueprintAt(randomCell, rot, artyDef, map, ThingDefOf.Steel));
                return randomCell;
            }
        }
    }
}