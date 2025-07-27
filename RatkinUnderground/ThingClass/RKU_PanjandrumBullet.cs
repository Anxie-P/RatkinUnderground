using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.Noise;

namespace RatkinUnderground
{
    public class RKU_PanjandrumBullet : Projectile
    {
        Vector3 direction;
        Vector3 currentDrawPos;
        bool initialized = false;
        int ticks;
        const float speedPerTick = 1.2f;
        const float explosionRadius = 10.9f;
        static readonly DamageDef explosionDamage = DefDatabase<DamageDef>.GetNamed("BombSuper");

        public RKU_Panjandrum vehicle;
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

        public override void Launch(
            Thing launcher,
            Vector3 origin,
            LocalTargetInfo usedTarget,
            LocalTargetInfo intendedTarget,
            ProjectileHitFlags hitFlags,
            bool preventFriendlyFire = false,
            Thing equipment = null,
            ThingDef targetCoverDef = null)
        {
            base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);

            // 关闭到target爆炸的逻辑
            var f = typeof(Projectile).GetField("ticksToImpact", BindingFlags.Instance | BindingFlags.NonPublic);
            if (f != null) f.SetValue(this, int.MaxValue);
            
            // 获取当前位置，target位置，计算出向量
            currentDrawPos = origin;
            Position = origin.ToIntVec3();
            Vector3 dest = intendedTarget.Cell.ToVector3Shifted();
            direction = (dest - origin).normalized;
            initialized = true;
        }

        public override void Tick()
        {
            ticks++;
            if (!initialized) return;

            // 移动
            currentDrawPos += direction * def.projectile.speed/60;
            IntVec3 newCell = currentDrawPos.ToIntVec3();
            Log.Message($"当前newCell是{newCell}");
            Log.Message($"当前Position是{Position}");
            if (newCell != Position) Position = newCell;

            // 如果出地图边缘，销毁projectile
            if (Position.x <= 0 || Position.x >= Map.Size.x - 1
             || Position.z <= 0 || Position.z >= Map.Size.z - 1)
            {
                Destroy(DestroyMode.Vanish);
                return;
            }

            // 如果有pawn或building执行爆炸
            var things = Map.thingGrid.ThingsListAt(Position);
            if (things.Any(t => t is Pawn || t is Building))
            {
                DoExplosion();
                foreach(var t in things)
                {
                    Log.Message($"当前位置含有{t.def.defName},位置是{Position}");
                }
                
                DeSpawn();
                return;
            }

            if (ticks > 3000)
            {
                Destroy(DestroyMode.Vanish);
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            float arcOffset = ArcHeightFactor * GenMath.InverseParabola(DistanceCoveredFractionArc);
            Vector3 drawPosWithArc = currentDrawPos + new Vector3(0f, 0f, 1f) * arcOffset;
            Graphic.Draw(drawPosWithArc, Rotation, this);
            Comps_PostDraw();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref vehicle, "RKU_BunkerBusterBullet", this);
        }

        void DoExplosion()
        {
            Log.Message($"已执行爆炸，下面应打印出位置信息");
            IntVec3 center = Position;

            GenExplosion.DoExplosion(
                center,
                Map,
                def.projectile.explosionRadius,
                def.projectile.damageDef,
                this.launcher,    
                def.projectile.GetDamageAmount(1f),
                armorPenetration: def.projectile.GetArmorPenetration(1f),
                def.projectile.soundExplode,
                equipmentDef,
                postExplosionSpawnThingDef: null,
                postExplosionSpawnChance: 0f
            );
        }
    }
}
