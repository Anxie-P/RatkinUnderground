using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RatkinUnderground
{
    public class RKK_BuildingBoomber : ThingWithComps
    {
        private bool useHighTexture = true;
        private int textureSwitchTicks = 0;
        private int explosionCountdown = 1440; // 24秒
        public override Graphic Graphic
        {
            get
            {
                string texPath = useHighTexture ?
                    "Things/Item/Weapons/Ivan" :
                    "Things/Item/Weapons/Ivan_Low";

                return GraphicDatabase.Get<Graphic_Single>(
                    texPath,
                    ShaderDatabase.MoteGlow,
                    this.def.graphicData.drawSize,
                    Color.white
                );
            }
        }

        public override void PostMake()
        {
            base.PostMake();
            SoundDef.Named("RKU_BombSet").PlayOneShot(new TargetInfo(Position, Map));
        }

        public override void Tick()
        {
            base.Tick();
            if (!Spawned) return;

            // 循环播放音效（每隔一段时间播放一次）
            if (Find.TickManager.TicksGame % 35 == 0) 
            {
                SoundDef.Named("RKU_BombLoop").PlayOneShot(new TargetInfo(Position, Map));
            }

            textureSwitchTicks++;
            if (textureSwitchTicks >= 120)
            {
                textureSwitchTicks = 0;
                useHighTexture = !useHighTexture;

                if (Find.CurrentMap == Map)
                {
                    Map.mapDrawer.MapMeshDirty(
                        Position,
                        MapMeshFlagDefOf.Things,
                        regenAdjacentCells: false,
                        regenAdjacentSections: false
                    );
                }
            }

            explosionCountdown--;
            if (explosionCountdown <= 0)
            {
                Explode();
            }
        }

        private void Explode()
        {

            GenExplosion.DoExplosion(
                Position,
                Map,
                10f,
                DamageDefOf.Vaporize,
                instigator: this
            );
            Destroy();
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            base.Destroy(mode);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref useHighTexture, "useHighTexture", true);
            Scribe_Values.Look(ref textureSwitchTicks, "textureSwitchTicks", 0);
            Scribe_Values.Look(ref explosionCountdown, "explosionCountdown", 1440);
        }
    }
}