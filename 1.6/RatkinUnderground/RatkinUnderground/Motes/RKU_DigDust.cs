using HarmonyLib;
using Verse;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using System;
using System.Reflection;
using UnityEngine;

namespace RatkinUnderground
{
    public class RKU_DigDust : Mote
    {
        private int ticksLeft = 300; // 5秒 = 300 ticks
        private float dustIntensity = 1f;
        private const float DUST_DECAY_RATE = 0.00333f; // 1/300，使灰尘在5秒内完全消失
        private Vector3 exactScale = new Vector3(2f, 1f, 2f);
        private static readonly Color dirtColor = new Color(0.6f, 0.4f, 0.2f, 0.8f); // 土色

        protected override void Tick()
        {
            base.Tick();

            if (ticksLeft <= 0)
            {
                Destroy();
                return;
            }

            if (Map == null || !Spawned)
            {
                Destroy();
                return;
            }

            // 生成尘土污渍
            if (Rand.Value < 0.3f * dustIntensity)
            {
                FilthMaker.TryMakeFilth(Position, Map, ThingDefOf.Filth_Dirt, 1);
            }

            // 生成烟尘效果
            if (Rand.Value < 0.2f * dustIntensity)
            {
                FleckMaker.ThrowDustPuffThick(DrawPos + new Vector3(Rand.Range(-1f, 1f), 0, Rand.Range(-1f, 1f)), Map, 2f, dirtColor);
            }

            // 绘制基础灰尘效果
            if (!Find.TickManager.Paused)
            {
                FleckMaker.ThrowDustPuff(DrawPos + new Vector3(Rand.Range(-0.5f, 0.5f), 0, Rand.Range(-0.5f, 0.5f)), Map, 1f * dustIntensity);
            }

            // 减少灰尘强度
            dustIntensity -= DUST_DECAY_RATE;
            ticksLeft--;
        }
    }
}