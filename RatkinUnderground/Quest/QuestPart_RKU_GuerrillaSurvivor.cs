using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.Noise;

namespace RatkinUnderground
{
    public class QuestPart_RKU_GuerrillaSurvivor : QuestPart
    {
        public string inSignal;

        public override void Notify_QuestSignalReceived(Signal signal)
        {
            base.Notify_QuestSignalReceived(signal);
            if (signal.tag == inSignal)
            {
                Log.Message($"[RKU] QuestPart 收到失败信号 {inSignal}，将 quest 结束为 Fail。");
                if (this.quest != null)
                {
                    this.quest.End(QuestEndOutcome.Fail);
                }
            }
        }
    }
}
