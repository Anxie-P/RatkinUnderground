using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.Sound;

namespace RatkinUnderground
{
    public class RKU_DestroyFleshMap : CustomMapComponent
    {
        private static readonly IntRange CollapseDurationTicks = new IntRange(25000, 27500);
        private int collapseTick = -999999;
        bool isCollapsing = false;
        public RKU_DestroyFleshMap(Map map) : base(map)
        {
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (!isCollapsing) return;

            if (Find.TickManager.TicksGame >= collapseTick)
            {
                var parent = map.Parent;
                if (parent != null)
                {
                    parent.Destroy();
                    // Find.MainTabsRoot.CloseOpenTabs();
                    Find.LetterStack.ReceiveLetter((Letter)LetterMaker.MakeLetter(
                        "地图已摧毁", 
                        "都是哈基母兽的错", 
                        LetterDefOf.NeutralEvent));
                }

                isCollapsing = false;
            }
        }

        public void BeginCollapsing()
        {
            int randomInRange = CollapseDurationTicks.RandomInRange;
            collapseTick = Find.TickManager.TicksGame + randomInRange;
            Notify_BeginCollapsing(randomInRange);
            isCollapsing = true;
        }

        public void Notify_BeginCollapsing(int collapseDurationTicks)
        {
            map.GetComponent<FleshmassMapComponent>()?.DestroyFleshmass(collapseDurationTicks, 5f, destroyInChunks: true);
            SoundDefOf.UndercaveRumble.PlayOneShotOnCamera(map);
            Find.CameraDriver.shaker.DoShake(0.2f, 120);
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref isCollapsing, "isCollapsing", defaultValue: false);
            Scribe_Values.Look(ref collapseTick, "collapseTick", defaultValue: -999999);
        }
    }
}
