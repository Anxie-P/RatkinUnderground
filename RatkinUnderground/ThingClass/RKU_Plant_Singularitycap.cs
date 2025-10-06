using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RatkinUnderground
{
    public class RKU_Plant_Singularitycap : Plant
    {
        private enum AnimationState
        {
            Idle,
            Animating,
            Exploding
        }

        private AnimationState currentState = AnimationState.Idle;
        private int animationTimer = 0;
        private int rareTickCounter = 0;
        private Graphic cachedGraphic;
        private float checkRange = 5f;
        private int animationDurationTicks = 180;

        public override Graphic Graphic
        {
            get
            {
                if (cachedGraphic == null)
                {
                    cachedGraphic = def.graphicData.Graphic;
                }
                return cachedGraphic;
            }
        }

        public override void Tick()
        {
            base.Tick();

            if (currentState == AnimationState.Animating)
            {
                animationTimer++;

                if (animationTimer >= animationDurationTicks)
                {
                    currentState = AnimationState.Exploding;
                    TriggerExplosion();
                }
            }

            // 每60 ticks执行一次检测
            rareTickCounter++;
            if (rareTickCounter >= 60)
            {
                rareTickCounter = 0;
                CheckForTargets();
            }
        }

        private void CheckForTargets()
        {
            // 如果正在动画或已经爆炸，跳过检测
            if (currentState != AnimationState.Idle)
            {
                return;
            }

            // 检查光照强度
            float glow = Map.glowGrid.GroundGlowAt(Position);
            if (glow >= 0.5f)
            {
                return;
            }

            // 检查附近是否有Pawn或钻机，且能接触到蘑菇
            List<Thing> nearbyTargets = new List<Thing>();
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(Position, checkRange, true))
            {
                if (!cell.InBounds(Map)) continue;

                Pawn pawn = cell.GetFirstPawn(Map);
                if (pawn != null && pawn.def.race.Humanlike && !nearbyTargets.Contains(pawn))
                {
                    if (Map.reachability.CanReach(pawn.Position, Position,Verse.AI.PathEndMode.Touch,TraverseMode.PassDoors))
                    {
                        nearbyTargets.Add(pawn);
                    }
                }

                // 检查钻机
                List<Thing> thingsAtCell = cell.GetThingList(Map);
                foreach (Thing thing in thingsAtCell)
                {
                    if (thing.def.defName == "RKU_DrillingVehicle" && !nearbyTargets.Contains(thing))
                    {
                        // 只要钻机能接触到蘑菇就算
                        TraverseParms traverseParms = TraverseParms.For(TraverseMode.PassAllDestroyableThings);
                        if (Map.reachability.CanReach(thing.Position, Position, Verse.AI.PathEndMode.Touch, TraverseMode.PassDoors))
                        {
                            nearbyTargets.Add(thing);
                        }
                    }
                }
            }

            if (nearbyTargets.Count > 0)
            {
                // 开始动画
                currentState = AnimationState.Animating;
                animationTimer = 0;
            }
        }

        private void TriggerExplosion()
        {
            IntVec3 explosionCenter = Position;
            GenExplosion.DoExplosion(explosionCenter, Map, 8, DamageDefOf.Bomb, this, 30, 1);
            Effecter effecter = EffecterDefOf.Vaporize_Heatwave.Spawn();
            effecter.scale = 5;
            effecter.Trigger(new TargetInfo(Position, Map), TargetInfo.Invalid);
            effecter.Cleanup();

            foreach (IntVec3 cell in GenRadial.RadialCellsAround(explosionCenter, checkRange, true))
            {
                if (cell.InBounds(Map))
                {
                    List<Thing> thingsAtCell = cell.GetThingList(Map);
                    List<Thing> thingsCopy = new List<Thing>(thingsAtCell); // 创建副本避免集合修改异常

                    foreach (Thing thing in thingsCopy)
                    {
                        if (thing is Pawn pawn)
                        {
                            DamageInfo damageInfo = new DamageInfo(DamageDefOf.Bomb, 20f, 0f, 0f, this);
                            pawn.TakeDamage(damageInfo);
                        }
                        else if (thing is Building building)
                        {
                            DamageInfo damageInfo = new DamageInfo(DamageDefOf.Bomb, 100f, 0f, 0f, this);
                            if (building.def.defName == "RKU_DrillingVehicle")
                            {
                                // 限制对钻机的伤害，默认最大耐久的20%，保底1血
                                var damage = (int)(building.MaxHitPoints * 0.2f);
                                if (damage <= 0) damage = 1;
                                var damageDrill = new DamageInfo(DamageDefOf.Bomb, damage, 0f, 0f, this);
                                building.TakeDamage(damageDrill);
                            }
                            else
                            {
                                building.TakeDamage(damageInfo);
                            }
                        }
                    }
                }
            }
        }

        public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
        {
            Log.Warning("rua");
            if (currentState == AnimationState.Animating)
            {
                Log.Warning("rua?");
                float progress = (float)animationTimer / animationDurationTicks;
                float shakePhase = (float)animationTimer * 0.1f;
                float shakeX = Mathf.Sin(shakePhase) * progress * 0.05f;
                float shakeY = Mathf.Cos(shakePhase * 1.3f) * progress * 0.05f;

                // 计算颜色变化
                Color baseColor = Color.Lerp(Color.white, Color.red, progress);
                Color finalColor = new Color(
                    Mathf.Clamp01(baseColor.r + shakeX),
                    Mathf.Clamp01(baseColor.g + shakeY),
                    Mathf.Clamp01(baseColor.b + (shakeX + shakeY) * 0.5f),
                    baseColor.a
                );

                // 计算缩放
                float scale = Mathf.Lerp(1.0f, 1.4f, progress);
                Material mat = MaterialPool.MatFrom(new MaterialRequest(ContentFinder<Texture2D>.Get("Things/Item/Plants/Singularitycap", false), ShaderDatabase.Cutout));
                mat.color = finalColor;
                Vector3 drawPos = drawLoc;
                drawPos.y += Altitudes.AltitudeFor(AltitudeLayer.MoteOverhead);
                drawPos.x += shakeX * 0.1f; // 添加位置颤抖
                drawPos.z += shakeY * 0.1f;

                // 创建变换矩阵
                Matrix4x4 matrix = Matrix4x4.TRS(drawPos, Rotation.AsQuat, new Vector3(scale, 5f, scale));

                // 直接绘制网格
                Graphics.DrawMesh(MeshPool.plane10, matrix, mat, 0);
                return;
            }
            else
            {
                base.DynamicDrawPhaseAt(phase, drawLoc, flip);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref currentState, "currentState", AnimationState.Idle);
            Scribe_Values.Look(ref animationTimer, "animationTimer", 0);
            Scribe_Values.Look(ref rareTickCounter, "rareTickCounter", 0);
        }
    }
}