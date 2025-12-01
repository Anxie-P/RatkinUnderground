using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RatkinUnderground
{
    public class Dialog_ManagePassengers : Window
    {
        private IThingHolder vehicle;
        private Vector2 scrollPosition;
        private float scrollViewHeight;

        public Dialog_ManagePassengers(IThingHolder vehicle)
        {
            this.vehicle = vehicle;
            this.doCloseButton = false;
            this.doCloseX = false;
            this.forcePause = true;
            this.absorbInputAroundWindow = true;
            this.closeOnClickedOutside = true;
        }

        public override Vector2 InitialSize => new Vector2(500f, 500f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 35f), "RKU.ManagePassengers".Translate());
            Text.Font = GameFont.Small;

            Rect outRect = new Rect(0f, 40f, inRect.width, inRect.height - 100f);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, scrollViewHeight);

            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            float curY = 0f;

            foreach (Pawn pawn in vehicle.GetDirectlyHeldThings())
            {
                Rect rowRect = new Rect(0f, curY, viewRect.width, 30f);
                Rect portraitRect = new Rect(0f, curY, 30f, 30f);
                Rect nameRect = new Rect(35f, curY, viewRect.width - 100f, 30f);
                Rect ejectButtonRect = new Rect(viewRect.width - 60f, curY, 60f, 30f);

                // 绘制肖像
                GUI.DrawTexture(portraitRect, PortraitsCache.Get(pawn, new Vector2(30f, 30f), Rot4.South, default));

                // 绘制名字
                Widgets.Label(nameRect, pawn.LabelCap);

                // 绘制弹出按钮
                if (Widgets.ButtonText(ejectButtonRect, "RKU.Eject".Translate()))
                {
                    if (vehicle is Building building)
                    {
                        vehicle.GetDirectlyHeldThings().Remove(pawn);
                        GenSpawn.Spawn(pawn, building.Position, building.Map);
                    }

                    if (vehicle is RKU_DrillingVehicleCargo)
                    {
                        RKU_DrillingVehicleCargo cargo = vehicle as RKU_DrillingVehicleCargo;
                        cargo.enterPawns = Math.Max(0, cargo.enterPawns - 1);
                    }
                }

                curY += 35f;
            }

            if (Event.current.type == EventType.Layout)
            {
                scrollViewHeight = curY;
            }

            Widgets.EndScrollView();

            // 底部按钮
            Rect bottomButtonRect = new Rect(inRect.width / 2f - 100f, inRect.height - 35f, 200f, 30f);
            if (Widgets.ButtonText(bottomButtonRect, "Close".Translate()))
            {
                Close();
            }
        }
    }
} 