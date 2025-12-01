using RimWorld;
using RimWorld.QuestGen;
using System.Linq;
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
            listing_Standard.CheckboxLabeled("RKU_ShowOnlyCurrentDialogueMessages".Translate(), ref settings.showOnlyCurrentDialogueMessages,
                "RKU_ShowOnlyCurrentDialogueMessagesDesc".Translate());
            listing_Standard.Gap(5f);
            listing_Standard.Label("RKU_RadioDialogueBehaviorDesc".Translate());
            listing_Standard.Gap(10f);
            listing_Standard.GapLine();
            listing_Standard.Gap(5f);
            listing_Standard.Label("RKU_DebugFeatures".Translate());
            if (listing_Standard.ButtonText("RKU_TryTriggerGuerrillasQuest".Translate()))
            {
                TryTriggerGuerrillasQuest();
            }
            listing_Standard.Gap(5f);
            listing_Standard.End();
        }

        private void TryTriggerGuerrillasQuest()
        {
            try
            {
                QuestScriptDef questDef = DefDatabase<QuestScriptDef>.GetNamed("RKU_GuerrillasComing");
                if (questDef == null)
                {
                    Messages.Message("无法触发任务：找不到任务定义。", MessageTypeDefOf.RejectInput);
                    return;
                }

                // 检查任务池中是否已有未接受的该任务
                if (Find.QuestManager.QuestsListForReading.Any(q => q.root == questDef && q.State == QuestState.NotYetAccepted))
                {
                    Messages.Message("无法触发任务：任务池中已存在未接受的该任务。", MessageTypeDefOf.RejectInput);
                    return;
                }

                Map targetMap = Find.AnyPlayerHomeMap;
                float points = StorytellerUtility.DefaultThreatPointsNow(targetMap);

                // 使用原有的检测逻辑
                Slate slate = new Slate();
                slate.Set("map", targetMap);
                
                // 直接使用QuestNode的TestRun方法，这会调用TestRunInt进行检测
                if (questDef.root.TestRun(slate))
                {
                    Quest quest = QuestUtility.GenerateQuestAndMakeAvailable(questDef, points);
                    if (quest != null)
                    {
                        QuestUtility.SendLetterQuestAvailable(quest);
                        Messages.Message("成功触发游击队到来任务！", MessageTypeDefOf.PositiveEvent);
                    }
                    else
                    {
                        Messages.Message("无法触发任务：任务生成失败。", MessageTypeDefOf.RejectInput);
                    }
                }
                else
                {
                    Messages.Message("无法触发任务：不满足触发条件（可能已存在电台或缺少游击队派系）。", MessageTypeDefOf.RejectInput);
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"[RKU_Mod] 尝试触发游击队任务时出错: {ex.Message}\n{ex.StackTrace}");
                Messages.Message($"触发任务时出错: {ex.Message}", MessageTypeDefOf.NegativeEvent);
            }
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
