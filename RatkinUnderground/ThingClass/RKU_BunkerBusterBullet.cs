using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RatkinUnderground
{
    public class RKU_BunkerBusterBullet : Projectile_Explosive
    {
        int ticks = 0;
        int effectTick = 0;
        Rot4 FinalRotation;
        Sustainer moveSustainer;
        public RKU_BunkerBuster vehicle;
        Effecter effecter;
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

        /*public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            EffecterDef sustainedDef = this.def.building?.groundSpawnerSustainedEffecter
                                  ?? throw new System.Exception("缺少 groundSpawnerSustainedEffecter 定义");

            // 2) 生成 Sustainer
            LocalTargetInfo myTarget = LocalTargetInfo.FromThing(this);
            moveSustainer = sustainedDef.SpawnMaintained(ThingMaker.MakeThing(this.), Map);
        }*/

        public override void Tick()
        {
            base.Tick();
            ticks++;
            effectTick++;
            if (effectTick < 10) return;
            effectTick = 0;
            SpawnSustainedEffecter();

            if (ticks > 3000)
            {
                Destroy();
            }
        }

        void SpawnSustainedEffecter()
        {
            for (int i = 0; i < 3; i++)
            {
                EffecterDef sustainedDef = def.building.groundSpawnerSustainedEffecter;
                if (sustainedDef == null)
                {
                    Log.Warning("sustainedDef为空");
                    return;
                }
                effecter = sustainedDef.SpawnMaintained(ThingMaker.MakeThing(this.def), Map, 0.5f);

                effecter.Trigger(
                    A: new TargetInfo(this),
                    B: new TargetInfo(this)
                );
                effecter.EffectTick(
                    A: new TargetInfo(this),
                    B: new TargetInfo(this)
                );
                effecter?.Cleanup();
            }
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            effecter?.Cleanup();
            effecter = null;
            base.DeSpawn(mode);
        }
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            float num = ArcHeightFactor * GenMath.InverseParabola(DistanceCoveredFractionArc);
            Vector3 vector = drawLoc + new Vector3(0f, 0f, 1f) * num;
            Log.Warning(ExactRotation.ToStringSafe());

            Graphic.Draw(vector, Rot4.North, this);
            Comps_PostDraw();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref vehicle, "RKU_BunkerBusterBullet", this);
        }
    }
}
