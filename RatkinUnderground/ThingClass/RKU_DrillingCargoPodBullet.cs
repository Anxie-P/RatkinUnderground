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
    public class RKU_DrillingCargoPodBullet : Projectile
    {
        int ticks = 0;
        Rot4 FinalRotation;

        public RKU_DrillingCargoPod rKU_DrillingCargoPod;
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


        public override void Tick()
        {
            base.Tick();
            ticks++;
            if (!Find.TickManager.Paused)
            {
                FleckMaker.ThrowDustPuffThick(DrawPos + new Vector3(0f, 0f, 1f) + new Vector3(Rand.Range(-0.5f, 0.5f), 0, Rand.Range(-0.5f, 0.5f)), Map, 1f, Color.black);
                FleckMaker.ThrowDustPuffThick(DrawPos + new Vector3(0f, 0f, 1f) + new Vector3(Rand.Range(-0.5f, 0.5f), 0, Rand.Range(-0.5f, 0.5f)), Map, 1f, Color.black);
                FleckMaker.ThrowDustPuffThick(DrawPos + new Vector3(0f, 0f, 1f) + new Vector3(Rand.Range(-0.5f, 0.5f), 0, Rand.Range(-0.5f, 0.5f)), Map, 1f, Color.black);
            }
            if (ticks > 3000)
            {
                Destroy();
            }
        }
        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            // 和钻地炸弹相反，这个是投射物到位置生成建筑，建筑可以拆解
            if (rKU_DrillingCargoPod != null)
            {
                GenSpawn.Spawn(rKU_DrillingCargoPod, Position, Map, WipeMode.Vanish);
            }
            base.Destroy(mode);
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            Vector3 vector = drawLoc;
            Log.Warning(ExactRotation.ToStringSafe());
            Graphic.Draw(vector, Rot4.North, this);
            Comps_PostDraw();
        }
    }
}
