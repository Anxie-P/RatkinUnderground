using RimWorld;
using System.Collections.Generic;
using Verse;

namespace RatkinUnderground
{
    public class Recipe_RKU_AllurecapConversion : Recipe_Surgery
    {
        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            base.ApplyOnPawn(pawn, part, billDoer, ingredients, bill);

            // 强制转阵营到玩家阵营
            if (pawn.Faction != Faction.OfPlayer)
            {
                pawn.SetFaction(Faction.OfPlayer);
                Messages.Message("RKU_ConversionSuccess".Translate(pawn.NameShortColored), pawn, MessageTypeDefOf.PositiveEvent);
            }

            // 添加15天麻醉效果
            Hediff anesthetic = HediffMaker.MakeHediff(HediffDefOf.Anesthetic, pawn);
            anesthetic.TryGetComp<HediffComp_Disappears>().ticksToDisappear = 15 * 60000; // 15天 = 15 * 60000 ticks
            pawn.health.AddHediff(anesthetic);
            Messages.Message("RKU_AllurecapConversionCompleted".Translate(pawn.NameShortColored), pawn, MessageTypeDefOf.NeutralEvent);
        }

        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
        {
            if (thing is Pawn pawn)
            {
                return pawn.Faction != Faction.OfPlayer;
            }
            return false;
        }

        public override AcceptanceReport AvailableReport(Thing thing, BodyPartRecord part = null)
        {
            if (thing is Pawn pawn)
            {
                if (pawn.Faction == Faction.OfPlayer)
                {
                    return "RKU_AlreadyPlayerFaction".Translate();
                }
            }
            return base.AvailableReport(thing, part);
        }
    }
}
