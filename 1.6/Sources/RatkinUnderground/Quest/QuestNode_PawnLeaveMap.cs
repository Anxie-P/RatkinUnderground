using System.Collections.Generic;
using RimWorld;
using RimWorld.QuestGen;
using Verse;

namespace RatkinUnderground
{
    public class QuestNode_PawnLeaveMap : QuestNode
    {
        public SlateRef<IEnumerable<Pawn>> pawns;
        public SlateRef<bool> sendStandardLetter = true;
        public SlateRef<bool> wakeUp = false;

        protected override bool TestRunInt(Slate slate)
        {
            return true;
        }

        protected override void RunInt()
        {
            Slate slate = QuestGen.slate;
            IEnumerable<Pawn> pawnList = pawns.GetValue(slate);

            if (pawnList != null)
            {
                List<Pawn> pawnsToLeave = new List<Pawn>();
                foreach (Pawn pawn in pawnList)
                {
                    if (pawn != null)
                    {
                        pawnsToLeave.Add(pawn);
                    }
                }

                if (pawnsToLeave.Count > 0)
                {
                    LeaveQuestPartUtility.MakePawnsLeave(pawnsToLeave, sendStandardLetter.GetValue(slate), QuestGen.quest, wakeUp.GetValue(slate));
                }
            }
        }
    }
}
