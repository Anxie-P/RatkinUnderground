using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI.Group;

namespace RatkinUnderground
{
    public class LordToil_WaitForSubsidize : LordToil_Travel, IWaitForItemsLordToil
    {
        IntVec3 point;
        Pawn target;
        ThingDef thingDef;
        int amount;
        public bool itemsGivenTriggered = false;

        public LordToil_WaitForSubsidize(IntVec3 point, Pawn target, ThingDef thingDef, int amount):base(point)
        {
            this.point = point;
            this.target = target;
            this.thingDef = thingDef;
            this.amount = amount;
        }

        public int CountRemaining => GiveItemsToPawnUtility.GetCountRemaining(target, thingDef, amount);

        public bool HasAllRequestedItems => CountRemaining <= 0;

        public override void DrawPawnGUIOverlay(Pawn pawn)
        {
            if (pawn == target)
            {
                pawn.Map.overlayDrawer.DrawOverlay(pawn, OverlayTypes.QuestionMark);
            }
        }

        public override IEnumerable<FloatMenuOption> ExtraFloatMenuOptions(Pawn requester, Pawn current)
        {
            if (target != requester)
            {
                yield break;
            }

            foreach (FloatMenuOption item in GiveItemsToPawnUtility.GetFloatMenuOptionsForPawn(requester, current, thingDef, amount))
            {
                yield return item;
            }
        }

        public override void LordToilTick()
        {
            base.LordToilTick();

            if (itemsGivenTriggered) return;

            try
            {
                if (HasAllRequestedItems)
                {
                    var comp = Current.Game.GetComponent<RKU_RadioGameComponent>();
                    comp.ralationshipGrade += 10;
                    Messages.Message($"{target.Name} 已收到所需物资，与地鼠关系提升10点", MessageTypeDefOf.PositiveEvent);
                    itemsGivenTriggered = true;
                }
            }catch(Exception e)
            {
                Log.Error($"[RKU] LordToil_WaitForSubsidize.LordToilTick 发生错误: {e}");
            }

        }
    }
}
