using UnityEngine;
using Verse;

namespace RatkinUnderground
{
    public class RKU_Mod : Mod
    {
        public RKU_ModSettings settings;

        public static RKU_Mod Instance { get; private set; }

        public RKU_Mod(ModContentPack content) : base(content)
        {
            settings = GetSettings<RKU_ModSettings>();
            Instance = this;
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing_Standard = new Listing_Standard();
            listing_Standard.Begin(inRect);
            listing_Standard.Gap(5f);
            listing_Standard.CheckboxLabeled("电台只显示当前对话的消息", ref settings.showOnlyCurrentDialogueMessages,
                "启用后，每次打开电台对话框时将清空历史消息，只显示本次对话的内容。禁用后将保留所有历史消息。");
            listing_Standard.Gap(5f);
            listing_Standard.Label("调整电台对话显示行为。启用后每次打开电台都会开始新的对话会话。");
            listing_Standard.End();
        }

        public override string SettingsCategory()
        {
            return "Ratkin Underground+";
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
        }

        public static bool ShouldShowOnlyCurrentDialogueMessages()
        {
            return Instance != null ? Instance.settings.showOnlyCurrentDialogueMessages : true;
        }
    }
}
