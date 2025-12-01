using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RatkinUnderground
{
    public class RKU_DestroyLitComponent : GameComponent
    {
        public RKU_DestroyLitComponent(Game game) { }
        public override void FinalizeInit()
        {
            base.FinalizeInit();
            DestroyLit();
        }

        /// <summary>
        /// 加载地图后清除保存前残留的light
        /// </summary>
        void DestroyLit()
        {
            foreach (var map in Find.Maps)
            {
                List<Thing> toRemove = map.listerThings.AllThings.ToList();
                foreach (var light in toRemove)
                {
                    if (light.def.defName == "RKU_Light") light.Destroy(DestroyMode.Vanish);
                }

                if (toRemove.Count > 0)
                    Log.Message($"[RKU] 清理光源 {toRemove.Count} 个");
            }
        }
    }
}
