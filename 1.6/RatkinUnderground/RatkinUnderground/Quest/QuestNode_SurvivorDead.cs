using RimWorld;
using RimWorld.QuestGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.Grammar;

namespace RatkinUnderground
{
    public class QuestNode_SurvivorDead : QuestNode
    {
        public SlateRef<Pawn> pawn;
        public QuestNode node;

        protected override void RunInt()
        {
            Slate slate = QuestGen.slate;
            Pawn pawnValue = pawn.GetValue(slate);
            Log.Message($"[RKU] 名字： {pawnValue.Name} ");
            if (pawnValue == null)
            {
                return;
            }

            QuestPart_PawnDeathTrigger questPart = new QuestPart_PawnDeathTrigger();
            questPart.inSignal = "RKU_SurvivorDead";
            questPart.pawn = pawnValue;

            QuestGen.quest.AddPart(questPart);
            QuestGen.slate.Set(questPart.inSignal, true);
        }

        protected override bool TestRunInt(Slate slate)
        {
            return true;
        }
    }

    public class QuestPart_PawnDeathTrigger : QuestPart
    {
        public Pawn pawn;
        public string inSignal;

        public override void Notify_QuestSignalReceived(Signal signal)
        {
            if (signal.tag == inSignal)
            {
                base.Notify_QuestSignalReceived(signal);
                /*if (quest != null)
                {
                    quest.End(QuestEndOutcome.Fail, 0, null, playSound: true, sendStandardLetter: false);
                }*/
            }
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref inSignal, "inSignal", defaultValue: null);
        }
    }
}
