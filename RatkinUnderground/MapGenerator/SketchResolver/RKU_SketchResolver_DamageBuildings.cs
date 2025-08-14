using RimWorld;
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


        protected override bool CanResolveInt(ResolveParams parms)
        {
            return true;
        }

        protected override void ResolveInt(ResolveParams parms)
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
        }

        private void ClearDisconnectedDoors(ResolveParams parms, IntVec3 position)
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

            destroyed = false;

            if (buildable is SketchTerrain sketchTerrain)
            {
                // 对于地形，保持原销毁逻辑（使用概率）
                if (Rand.Chance(Mathf.Pow(num, destroyChanceExp ?? 1.32f)))
                {
                    sketch.Remove(buildable);
                    destroyed = true;

                    TerrainDef naturalTerrain = DefDatabase<ThingDef>.AllDefs.Where(d => d.IsStuff && d.building != null && d.building.isNaturalRock).RandomElement().building.naturalTerrain;
                    if (naturalTerrain != null)
                    {
                        sketch.AddTerrain(naturalTerrain, sketchTerrain.pos);
                    }
                }
            }
            else if (buildable is SketchThing sketchThing)
            {
                // 计算hitPoints
                int maxHp = sketchThing.MaxHitPoints;
                int calculatedHp = Mathf.Clamp(Mathf.RoundToInt((float)maxHp * (1f - num) * Rand.Range(1f, 1.2f)), 1, maxHp);

                if (calculatedHp < (int)(0.2f * maxHp))
                {
                    if (sketchThing.def == ThingDefOf.Wall)
                    {
                        // 用沙袋替换
                        sketch.Remove(buildable);
                        sketch.AddThing(ThingDefOf.Sandbags, buildable.pos, sketchThing.rot, ThingDefOf.Cloth);
                        destroyed = true;
                    }
                    else
                    {
                        sketch.Remove(buildable);
                        destroyed = true;
                        // 添加naturalTerrain作为地板
                        TerrainDef naturalTerrain = DefDatabase<ThingDef>.AllDefs.Where(d => d.IsStuff && d.building != null && d.building.isNaturalRock).RandomElement().building.naturalTerrain;
                        if (naturalTerrain != null)
                        {
                            sketch.AddTerrain(naturalTerrain, buildable.pos);
                        }
                    }
                }
                else
                {
                    sketchThing.hitPoints = calculatedHp;
                }
            }
        }
    }
}
