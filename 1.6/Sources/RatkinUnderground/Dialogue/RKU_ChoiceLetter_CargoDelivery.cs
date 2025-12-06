using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RatkinUnderground
{
    public class RKU_ChoiceLetter_CargoDelivery : ChoiceLetter
    {
        private Dialog_RKU_Radio radioWindow;

        public RKU_ChoiceLetter_CargoDelivery(Dialog_RKU_Radio radioWindow)
        {
            this.radioWindow = radioWindow;
        }

        public RKU_ChoiceLetter_CargoDelivery(Dialog_RKU_Radio radioWindow, string title, string text, LetterDef def)
        {
            this.radioWindow = radioWindow;
            this.Label = title;
            this.title = title;
            this.Text = text;
            this.def = def;
        }

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                yield return new DiaOption("Accept".Translate())
                {
                    action = delegate
                    {
                        if (radioWindow != null && radioWindow.pendingCargo.Count > 0)
                        {
                            RKU_DrillingCargoPodBullet pod = radioWindow.SendCargoPod();
                        }

                        Find.LetterStack.RemoveLetter(this);
                    },
                    resolveTree = true
                };
            }
        }
    }
}
