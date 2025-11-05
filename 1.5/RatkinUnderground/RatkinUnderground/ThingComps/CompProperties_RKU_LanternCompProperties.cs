using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Verse;
using Verse.Noise;

namespace RatkinUnderground
{
    public class RKU_LanternComponent : ThingComp,
        IThingGlower
    {
        bool isOpen = true;
        Thing light = null;

        string lightDef => Props.lightDef;
        //int fuleTick;
        public CompProperties_RKU_LanternCompProperties Props => (CompProperties_RKU_LanternCompProperties)this.props;
        public Pawn pawn
        {
            get
            {
                if (parent is Apparel apparel) return apparel.Wearer;
                if (parent is Pawn result) return result;
                return null;
            }
        }
        
        public override void CompTick()
        {
            base.CompTick();
            if (!ShouldBeLitNow())
            {
                //Log.Message("未点亮");
                if (light != null && !light.Destroyed) light.Destroy();
                return;
            }
            UpdateLit();
        }
        
        /// <summary>
        /// 是否点亮
        /// </summary>
        /// <returns></returns>
        public bool ShouldBeLitNow()
        {
            if(!isOpen) return false;
            //if (!(fuleTick > 0)) return false;
            if (parent.Map == null && pawn == null) return false;
            return true;
        }

        /// <summary>
        /// 刷新移动光源
        /// </summary>
        /// <param name="map">pawn所在地图</param>
        void UpdateLit()
        {
            if (light == null || !light.Spawned)
            {
                SpawnLit();
            }
            if (pawn != null)   MoveLit(pawn.Position);
        }

        /// <summary>
        /// 更新光源位置
        /// </summary>
        /// <param name="position">新位置</param>
        void MoveLit(IntVec3 position)
        {
            light.DeSpawn(DestroyMode.Vanish);
            GenSpawn.Spawn(light, position, pawn.Map);
        }

        /// <summary>
        /// 生成光源
        /// </summary>
        void SpawnLit()
        {
            light = ThingMaker.MakeThing(ThingDef.Named(lightDef));

            if (pawn != null)
            {
                GenSpawn.Spawn(light, pawn.Position, pawn.Map);
            }
            else
            {
                GenSpawn.Spawn(light, parent.Position, parent.Map);
            }
        }
        
        /// <summary>
        /// 销毁光源
        /// </summary>
        void DestroyLit()
        {
            if (light != null && light.Spawned)
            {
                light.Destroy();
                light = null;
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            Command_Action openStat = new Command_Action
            {
                defaultLabel = "开关",
                activateSound = SoundDefOf.Tick_Tiny,
                action = delegate
                {
                    isOpen = !isOpen;
                    if (isOpen == false)
                    {
                        DestroyLit();
                    }
                    else
                    {
                        SpawnLit();
                    }
                    Log.Message($"当前开关状态:{isOpen}");
                }
            };
            yield return openStat;
        }
        public override void PostExposeData()
        {
            base.PostExposeData();

            Scribe_Values.Look(ref isOpen, "RKU_Lantern_isOpen", defaultValue: true);
        }
    }

    public class CompProperties_RKU_LanternCompProperties : CompProperties
    {
        public string lightDef;
        public CompProperties_RKU_LanternCompProperties()
        {
            this.compClass = typeof(RKU_LanternComponent);
        }

        public CompProperties_RKU_LanternCompProperties(Type compClass) : base(compClass)
        {
            this.compClass = compClass;
        }
    }
}
