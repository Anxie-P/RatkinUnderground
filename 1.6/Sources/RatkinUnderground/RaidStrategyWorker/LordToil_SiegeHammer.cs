using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
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
        }
        public override void Init()
        {
            base.Init(); // 调用 LordToil_Siege.Init()
            LordToilData_Siege data = Data;
            data.baseRadius = Mathf.InverseLerp(14f, 25f, (float)lord.ownedPawns.Count / 50f);
            data.baseRadius = Mathf.Clamp(data.baseRadius, 14f, 25f);
            List<Thing> list = new List<Thing>();
            List<List<Thing>> list2 = new List<List<Thing>>();
            foreach (Blueprint_Build item2 in SiegeBlueprintPlacer_Hammer.PlaceBlueprints(data.siegeCenter, base.Map, lord.faction, data.blueprintPoints))
            {
                if (item2 == null)
                {
                    continue;
                }
                data.blueprints.Add(item2);
                foreach (ThingDefCountClass cost in item2.TotalMaterialCost())
                {
                    if (cost.thingDef == null)
                    {
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
            DropPodUtility.DropThingGroupsNear(data.siegeCenter, base.Map, list2);
            data.desiredBuilderFraction = new FloatRange(0.25f, 0.4f).RandomInRange;
        }

        public override void UpdateAllDuties()
        {
            try
            {
                // 过滤出有效的pawn，因为有机械体
                var validPawns = lord.ownedPawns.Where(p => p != null && !p.Dead && !p.Downed && p.Spawned && p.def.race.FleshType!=DefDatabase<FleshTypeDef>.GetNamed("Mechanoid")).ToList();

                if (!validPawns.Any())
                {
                    return;
                }
                base.UpdateAllDuties();
            }
            catch (Exception ex)
            {
                CleanupInvalidPawns();
            }
        }

        private void CleanupInvalidPawns()
        {
            try
            {
                var invalidPawns = lord.ownedPawns.Where(p => p == null || p.Dead || p.Destroyed || !p.Spawned).ToList();
                foreach (var invalidPawn in invalidPawns)
                {
                    lord.RemovePawn(invalidPawn);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RKU] SiegeHammer: Exception in CleanupInvalidPawns: {ex.Message}");
            }
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
                return GenConstruct.CanPlaceBlueprintAt(buildingDef, root, rot, map, godMode: false, null, null, stuffDef, ignoreEdgeArea: true, ignoreInteractionSpots: true, ignoreClearableFreeBuildings: true).Accepted;
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
                Rot4 fixedRotation = Rot4.North;
                IntVec3 intVec = FindArtySpot(hammerAttackerDef, fixedRotation, map);

                if (!intVec.IsValid)
                {
                    yield break;
                }
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
                CellRect cellRect = CellRect.CenteredOn(center, 8);
                cellRect.ClipInsideMap(map);

                foreach (IntVec3 cell in cellRect)
                {
                    if (map.reachability.CanReach(cell, center, PathEndMode.OnCell, TraverseMode.PassAllDestroyableThingsNotWater, Danger.Deadly) &&
                        CanPlaceBlueprintAt(cell, rot, artyDef, map, null))
                    {
                        return cell;
                    }
                }

                // 如果没有找到合适的格子，清除周围建筑并返回中心
                ClearBuildingsAround(center, map, 3);
                return center;
            }

            private static void ClearBuildingsAround(IntVec3 center, Map map, int radius)
            {
                foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, radius, true))
                {
                    if (cell.InBounds(map))
                    {
                        Thing thing = cell.GetFirstItem(map);
                        if (!(thing is Pawn)&&thing != null && thing.def.destroyable)
                        {
                            thing.Destroy(DestroyMode.Vanish);
                        }
                    }
                }
            }
        }
    }
}