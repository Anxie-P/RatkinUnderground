using RimWorld;
using System;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RatkinUnderground
{
    class RKU_DrilingBullet : Projectile
    {
        int ticks = 0;

        Rot4 FinalRotation;

        // 用来储存当前精确位置和运动方向
        private Vector3 exactPos;
        private Vector3 dir;
        private Vector3 worldDestination;

        // 当速度切换时需要判断的阈值
        float curSpeed = 0.16f;          // 当前速度
        const float hightSpeed = 0.16f;  // 正常移动的速度
        const float slowSpeed = 0.06f;  // 慢速移动的速度
        bool isSlow = false;            // 是否进入减速状态
        int speedCooldown = 0;          // 恢复正常速度的冷却时间

        int damage = 0;     // 挖墙撞人掉的耐久

        public override Vector3 DrawPos
        {
            get
            {
                return new Vector3(exactPos.x, def.Altitude, exactPos.z);
            }
        }

        public override void Launch(Thing launcher, Vector3 origin, LocalTargetInfo usedTarget, LocalTargetInfo intendedTarget, ProjectileHitFlags hitFlags, bool preventFriendlyFire = false, Thing equipment = null, ThingDef targetCoverDef = null)
        {
            base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);
            exactPos = origin;
            worldDestination = intendedTarget.Cell.ToVector3Shifted();
            dir = (intendedTarget.Cell.ToVector3Shifted() - exactPos).normalized;
        }

        private void SetGraphicForVehicleType()
        {
            if (vehicle == null) return;

            // 根据钻机类型设置基础纹理路径
            if (vehicle is RKU_DrillingVehicleCargo)
            {
                baseTexturePath = "Buildings/DrillingVehicle/DrillingVehicleCargoBullet/DrillingVehicleCargoBullet";
            }
            else if (vehicle is RKU_DrillingVehicleWithTurret)
            {
                baseTexturePath = "Buildings/DrillingVehicle/DrillingVehicleTurretBullet/DrillingVehicleTurretBullet";
            }
            else
            {
                baseTexturePath = "Buildings/DrillingVehicle/BaseDrillingVehicleBullet/BaseDrillingVehicleBullet";
            }

            // 纹理路径已设置，DrawAt中会根据rotation选择具体纹理
        }

        private string baseTexturePath;



        private RKU_DrillingVehicle _vehicle;
        public RKU_DrillingVehicle vehicle
        {
            get { return _vehicle; }
            set
            {
                _vehicle = value;
                if (_vehicle != null)
                {
                    SetGraphicForVehicleType();
                }
            }
        }
        private float ArcHeightFactor
        {
            get
            {
                float num = def.projectile.arcHeightFactor;
                float num2 = (destination - origin).MagnitudeHorizontalSquared();
                if (num * num > num2 * 0.2f * 0.2f)
                {
                    num = Mathf.Sqrt(num2) * 0.2f;
                }

                return num;
            }
        }

        protected override void Tick()
        {
            base.Tick();
            ticks++;
            speedCooldown++;
            this.Position = DrawPos.ToIntVec3();
            if (ticks > 3000)
            {
                Destroy();
            }
            this.Map.fogGrid.FloodUnfogAdjacent(this, false);
            if (Map != null && Position.GetFirstPawn(Map) != null)
            {
                SoundDefOf.Pawn_Melee_Punch_HitPawn.PlayOneShot(new TargetInfo(Position, Map));
                Position.GetFirstPawn(Map).TakeDamage(new DamageInfo(DamageDefOf.Cut, 10, 0.05f, -1f));
                damage += 10;
            }
            if (Map != null && Position.GetFirstBuilding(Map) != null)
            {
                Position.GetFirstBuilding(Map).TakeDamage(new DamageInfo(DamageDefOf.Crush, Position.GetFirstBuilding(Map).MaxHitPoints, 2f, -1f));
                isSlow = true; 
                speedCooldown = 0;
                damage += Rand.Range(0, 7);
            }
            else
            {
                if (speedCooldown > 30)
                {
                    isSlow = false;
                    speedCooldown = 0;
                }
                
            }
            
            // 检测是否到达目的地
            float disX = Math.Abs(exactPos.x - worldDestination.x);
            float disZ = Math.Abs(exactPos.z - worldDestination.z);
            if (disX <= curSpeed * 2 && disZ <= curSpeed * 2) 
            {
                Destroy();
                return;
            }
            Log.Message($"钻机状态: 位置={exactPos}, 方向={Rotation}, 速度={curSpeed}, 目标={worldDestination}");
            Log.Message($"当前剪掉后的耐久：{vehicle.HitPoints - damage}");
            Log.Message($"当前damage：{damage}");
            if (vehicle.HitPoints - damage <= 10)
            {
                Messages.Message($"钻机损坏过于严重，被迫停下！", MessageTypeDefOf.NegativeEvent);
                Log.Message($"当前位置：{DrawPos}");
                
                Destroy();
                //damage = 0;
                return;
            }

            curSpeed = isSlow ? slowSpeed : hightSpeed;
            exactPos += dir * curSpeed * GenTicks.TicksPerRealSecond / 60;
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            this.Position = DrawPos.ToIntVec3();
            for (int i = 0; i < 5; i++)
            {
                if (Position.GetFirstPawn(Map) != null)
                {
                    Position.GetFirstPawn(Map).TakeDamage(new DamageInfo(DamageDefOf.Flame, 35, 0.4f, -1f));
                }
                if (Position.GetFirstBuilding(Map) != null)
                {
                    Log.Message($"撞到的建筑{Position.GetFirstBuilding(Map).def.defName}");
                    Position.GetFirstBuilding(Map).TakeDamage(new DamageInfo(DamageDefOf.Crush, 300, 2f, -1f));
                }
            }
            if (vehicle != null)
            {
                foreach (IntVec3 item in GenRadial.RadialCellsAround(this.Position, 1, useCenter: true))
                {
                    if (item.GetFirstBuilding(Map)!=null)
                    {
                        item.GetFirstBuilding(Map).Destroy();
                    }
                }
                var newHitPoints = vehicle.HitPoints - damage;
                vehicle.HitPoints = Math.Max(1, newHitPoints);
                Log.Message($"生成位置：{this.Position}");
                GenSpawn.Spawn(vehicle, this.Position, this.Map);
                vehicle.Rotation = FinalRotation;
            }
            base.Destroy(mode);
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            float num = ArcHeightFactor * GenMath.InverseParabola(DistanceCoveredFractionArc);
            Vector3 vector = drawLoc + new Vector3(0f, 0f, 1f) * num;

            FaceAdjacentCell(destination.ToIntVec3());

            // 根据rotation选择正确的纹理
            string texturePath = baseTexturePath ?? "Buildings/DrillingVehicle/BaseDrillingVehicleBullet/BaseDrillingVehicleBullet";
            if (Rotation == Rot4.North)
            {
                texturePath += "_north";
            }
            else if (Rotation == Rot4.South)
            {
                texturePath += "_south";
            }
            else if (Rotation == Rot4.East)
            {
                texturePath += "_east";
            }
            else if (Rotation == Rot4.West)
            {
                texturePath += "_west";
            }
            // 其他rotation使用默认纹理

            // 获取纹理并绘制
            try
            {
                Texture2D texture = ContentFinder<Texture2D>.Get(texturePath);
                if (texture != null)
                {
                    Material material = MaterialPool.MatFrom(texture);
                    material.mainTextureOffset = new Vector2(0f, 0f);
                    material.mainTextureScale = new Vector2(1f, 1f);
                    Matrix4x4 matrix = Matrix4x4.TRS(vector, Quaternion.identity, new Vector3(4.5f,1f, 4.5f));
                    Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0, null, 0, null);
                }
                else
                {
                    Log.Error($"[RKU] 无法找到纹理: {texturePath}");
                }
            }
            catch (Exception e)
            {
                Log.Error($"[RKU] 加载纹理失败 {texturePath}: {e.Message}");
            }

            float numOfHeight = ArcHeightFactor * GenMath.InverseParabola(DistanceCoveredFractionArc);
            Comps_PostDraw();

            if (!Find.TickManager.Paused)
            {
                FleckMaker.ThrowDustPuffThick(DrawPos + new Vector3(0f, 0f, 1f) * numOfHeight + new Vector3(Rand.Range(-0.5f, 0.5f), 0, Rand.Range(-0.5f, 0.5f)), Map, 1f, Color.black);
            }
        }

        /// <summary>
        /// 停车的时候要改为南北向
        /// </summary>
        /// <param name="c"></param>
        private void FaceAdjacentCell(IntVec3 c)
        {
            if (!(c == Position))
            {
                IntVec3 intVec = c - Position;
                if (intVec.x > 0)
                {
                    Rotation = Rot4.East;
                    FinalRotation = Rot4.South; 
                }
                else if (intVec.x < 0)
                {
                    Rotation = Rot4.West;
                    FinalRotation = Rot4.North;
                }
                else if (intVec.z > 0)
                {
                    Rotation = Rot4.North;
                    FinalRotation = Rot4.North;
                }
                else if (intVec.z < 0)
                {
                    Rotation = Rot4.South;
                    FinalRotation = Rot4.South;
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref _vehicle, "vehicle", this);
            Scribe_Deep.Look(ref curSpeed, "curSpeed", this);
            Scribe_Deep.Look(ref worldDestination, "worldDestination", this);
            Scribe_Deep.Look(ref exactPos, "exactPos", this);
            Scribe_Values.Look(ref baseTexturePath, "baseTexturePath");
        }
    }
}
