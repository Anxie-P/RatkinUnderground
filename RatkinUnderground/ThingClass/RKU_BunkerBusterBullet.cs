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
        Rot4 FinalRotation;
        public RKU_BunkerBuster vehicle;
        

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
            if (ticks > 3000)
            {
                Destroy();
            }
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
