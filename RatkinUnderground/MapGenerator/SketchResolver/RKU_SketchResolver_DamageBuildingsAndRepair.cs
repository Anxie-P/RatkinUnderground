using RimWorld;
using RimWorld.BaseGen;
using RimWorld.SketchGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace RatkinUnderground
{
    public class RKU_SketchResolver_DamageBuildingsAndRepair : SketchResolver
    {
        private const float MaxPctOfTotalDestroyed = 0.65f;

        private const float HpRandomFactor = 1.2f;

        private const float DestroyChanceExp = 1.32f;


        protected override void ResolveInt(SketchResolveParams parms)
        {
            CellRect occupiedRect = parms.sketch.OccupiedRect;
            Rot4 random = Rot4.Random;
            int num = 0;
            int num2 = parms.sketch.Buildables.Count();
            foreach (SketchBuildable item in parms.sketch.Buildables.InRandomOrder().ToList())
            {
                Damage(item, occupiedRect, random, parms.sketch, out var destroyed, parms.destroyChanceExp);
                if (destroyed)
                {
                    num++;
                    ClearDisconnectedDoors(parms, item.pos);
                    if ((float)num > (float)num2 * 0.65f)
                    {
                        break;
                    }
                }
            }
            RepairWalls(parms.sketch);
        }

        private void ClearDisconnectedDoors(SketchResolveParams parms, IntVec3 position)
        {
            IntVec3[] cardinalDirectionsAround = GenAdj.CardinalDirectionsAround;
            for (int i = 0; i < cardinalDirectionsAround.Length; i++)
            {
                IntVec3 position2 = cardinalDirectionsAround[i] + position;
                SketchThing door = parms.sketch.GetDoor(position2);
                if (door != null && !AdjacentToWall(parms.sketch, door))
                {
                    parms.sketch.Remove(door);
                }
            }
        }

        private bool AdjacentToWall(Sketch sketch, SketchEntity entity)
        {
            IntVec3[] cardinalDirections = GenAdj.CardinalDirections;
            foreach (IntVec3 intVec in cardinalDirections)
            {
                if (sketch.ThingsAt(entity.pos + intVec).Any((SketchThing t) => t.def == ThingDefOf.Wall))
                {
                    return true;
                }
            }

            return false;
        }

        private void Damage(SketchBuildable buildable, CellRect rect, Rot4 dir, Sketch sketch, out bool destroyed, float? destroyChanceExp = null)
        {
            float num = ((!dir.IsHorizontal) ? ((float)(buildable.pos.z - rect.minZ) / (float)rect.Height) : ((float)(buildable.pos.x - rect.minX) / (float)rect.Width));
            if (dir == Rot4.East || dir == Rot4.South)
            {
                num = 1f - num;
            }

            if (Rand.Chance(Mathf.Pow(num, destroyChanceExp ?? 1.32f)))
            {
                //不摧毁
                destroyed = true;
                if (buildable is SketchTerrain sketchTerrain && sketchTerrain.def.burnedDef != null)
                {
                    sketch.AddTerrain(sketchTerrain.def.burnedDef, sketchTerrain.pos);
                }
            }
            else
            {
                destroyed = false;
                if (buildable is SketchThing sketchThing)
                {
                    if (sketchThing.def == ThingDefOf.Wall)
                    {
                        // 只损坏墙的HP，但不移除
                        sketchThing.hitPoints = Mathf.Clamp(Mathf.RoundToInt((float)sketchThing.MaxHitPoints * (1f - num) * Rand.Range(0.5f, 1f)), 1, sketchThing.MaxHitPoints);
                    }
                    else
                    {
                        sketchThing.hitPoints = Mathf.Clamp(Mathf.RoundToInt((float)sketchThing.MaxHitPoints * (1f - num) * Rand.Range(1f, 1.2f)), 1, sketchThing.MaxHitPoints);
                    }
                }
            }
        }

        private void RepairWalls(Sketch sketch)
        {
            CellRect rect = sketch.OccupiedRect;
            int maxLayers = Mathf.Min(rect.Width, rect.Height) / 2;
            int bestLayer = -1;
            int maxWalls = 0;
            List<IntVec3> bestPerimeter = null;

            for (int layer = 0; layer <= maxLayers; layer++)
            {
                int minX = rect.minX + layer;
                int maxX = rect.maxX - layer;
                int minZ = rect.minZ + layer;
                int maxZ = rect.maxZ - layer;

                if (minX > maxX || minZ > maxZ) break;

                List<IntVec3> perimeter = GetPerimeterCells(minX, maxX, minZ, maxZ);
                int wallCount = 0;
                foreach (var pos in perimeter)
                {
                    if (sketch.ThingsAt(pos).Any(t => t.def == ThingDefOf.Wall))
                        wallCount++;
                }

                if (wallCount > maxWalls)
                {
                    maxWalls = wallCount;
                    bestLayer = layer;
                    bestPerimeter = perimeter;
                }
            }

            if (bestPerimeter == null) return;

            foreach (var pos in bestPerimeter)
            {
                if (!HasEdifice(sketch, pos))
                {
                    sketch.AddThing(ThingDefOf.Sandbags, pos, Rot4.North, ThingDefOf.Cloth, 1);
                }
            }

            // 新添加：替换中心最近的建筑为篝火
            ReplaceNearestEdificeWithCampfire(sketch);
        }

        private void ReplaceNearestEdificeWithCampfire(Sketch sketch)
        {
            IntVec3 center = sketch.OccupiedRect.CenterCell;
           
            // 移除中心原有建筑
            sketch.Remove(sketch.EdificeAt(center));

            // 添加篝火（中心位置）
            sketch.AddThing(ThingDefOf.Campfire, center, Rot4.North, null, 1);

            // 在篝火东侧和西侧并排放置两个睡袋
            IntVec3 bedPos1 = center + IntVec3.East;  // 东侧睡袋
            IntVec3 bedPos2 = center + IntVec3.West;  // 西侧睡袋

            // 检查位置是否可用
            if (!sketch.ThingsAt(bedPos1).Any())
            {
                sketch.AddThing(ThingDefOf.Bedroll, bedPos1, Rot4.South, ThingDefOf.Cloth, 1); // 朝南
            }
            else if (!sketch.ThingsAt(bedPos1 + IntVec3.North).Any()) // 如果东侧被占，尝试北移一格
            {
                sketch.AddThing(ThingDefOf.Bedroll, bedPos1 + IntVec3.North, Rot4.South, ThingDefOf.Cloth, 1);
            }

            if (!sketch.ThingsAt(bedPos2).Any())
            {
                sketch.AddThing(ThingDefOf.Bedroll, bedPos2, Rot4.South, ThingDefOf.Cloth, 1); // 朝南
            }
            else if (!sketch.ThingsAt(bedPos2 + IntVec3.North).Any()) // 如果西侧被占，尝试北移一格
            {
                sketch.AddThing(ThingDefOf.Bedroll, bedPos2 + IntVec3.North, Rot4.South, ThingDefOf.Cloth, 1);
            }

            ReplaceSarcophagiWithBedrolls(sketch);
        }

        private void ReplaceSarcophagiWithBedrolls(Sketch sketch)
        {
            List<SketchThing> sarcophagi = new List<SketchThing>();
            foreach (SketchEntity entity in sketch.Things)
            {
                if (entity is SketchThing thing && thing.def == ThingDefOf.Sarcophagus)
                {
                    sarcophagi.Add(thing);
                }
            }

            foreach (var sarc in sarcophagi)
            {
                IntVec3 pos = sarc.pos;
                Rot4 rot = sarc.rot;
                sketch.Remove(sarc);
                // 添加布制睡袋（假设使用Bed Def）
                sketch.AddThing(ThingDefOf.Bedroll, pos, rot, ThingDefOf.Cloth, 1);
            }
        }

        private List<IntVec3> GetPerimeterCells(int minX, int maxX, int minZ, int maxZ)
        {
            List<IntVec3> cells = new List<IntVec3>();

            // Top
            for (int x = minX; x <= maxX; x++)
                cells.Add(new IntVec3(x, 0, maxZ));

            // Bottom
            for (int x = minX; x <= maxX; x++)
                cells.Add(new IntVec3(x, 0, minZ));

            // Left (excluding corners)
            for (int z = minZ + 1; z < maxZ; z++)
                cells.Add(new IntVec3(minX, 0, z));

            // Right (excluding corners)
            for (int z = minZ + 1; z < maxZ; z++)
                cells.Add(new IntVec3(maxX, 0, z));

            return cells;
        }

        private bool HasEdifice(Sketch sketch, IntVec3 pos)
        {
            return sketch.ThingsAt(pos).Any(t => t.def.building != null && t.def.building.isEdifice);
        }

        protected override bool CanResolveInt(SketchResolveParams parms)
        {
            return true;
        }
    }
}