using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using Verse;
using Verse.AI;

namespace RatkinUnderground;
public class CompProperties_RKU_Radio : CompProperties
{
    public CompProperties_RKU_Radio()
    {
        compClass = typeof(Comp_RKU_Radio);
    }
}

public class Comp_RKU_Radio : ThingComp
{
    // 消息历史记录
    private List<string> messageHistory = new List<string>();
    private const int MAX_MESSAGE_HISTORY = 50;

    public List<string> MessageHistory => messageHistory;

    public bool isSearchJob = false;  // 是否正在协助研究

    RKU_RadioGameComponent radioComponent = Current.Game.GetComponent<RKU_RadioGameComponent>();

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        if (messageHistory == null)
        {
            messageHistory = new List<string>();
        }
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Collections.Look(ref messageHistory, "messageHistory", LookMode.Value);
        Scribe_Values.Look(ref isSearchJob, "isSearchJob", false);
        if (Scribe.mode == LoadSaveMode.LoadingVars && messageHistory == null)
        {
            messageHistory = new List<string>();
        }
    }

    public void AddMessage(string message)
    {
        if (messageHistory == null)
        {
            messageHistory = new List<string>();
        }

        messageHistory.Add(message);
        
        // 限制消息历史数量
        if (messageHistory.Count > MAX_MESSAGE_HISTORY)
        {
            messageHistory.RemoveAt(0);
        }
    }

    public void ClearMessageHistory()
    {
        messageHistory?.Clear();
    }

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        foreach (Gizmo gizmo in base.CompGetGizmosExtra())
        {
            yield return gizmo;
        }
        yield return new Command_Action
        {
            defaultLabel = "RKU.OpenRadio".Translate(),
            defaultDesc = "RKU.OpenRadioDesc".Translate(),
            icon = Resources.dig,
            hotKey = KeyBindingDefOf.Misc1,
            action = delegate
            {
                Find.WindowStack.Add(new Dialog_RKU_Radio(parent));
            }
        };

        // 开关自动研究
        if (radioComponent.ralationshipGrade > 20)
        {
            yield return new Command_Action
            {
                defaultLabel = "自动研究",
                defaultDesc = "开关自动研究的状态",
                icon = isSearchJob ? ContentFinder<Texture2D>.Get("UI/Widgets/CheckOn") : ContentFinder<Texture2D>.Get("UI/Widgets/CheckOff"),
                hotKey = KeyBindingDefOf.Misc1,
                action = delegate
                {
                    isSearchJob = !isSearchJob;
                }
            };
        }

        // 开发用Gizmo - 取消交易冷却
        if (Prefs.DevMode)
        {
            yield return new Command_Action
            {
                defaultLabel = "取消冷却",
                defaultDesc = "立即取消交易冷却时间",
                icon = ContentFinder<Texture2D>.Get("RKU_Null"),
                action = delegate
                {
                    var radioComponent = Current.Game.GetComponent<RKU_RadioGameComponent>();
                    if (radioComponent != null)
                    {
                        radioComponent.canTrade = true;
                        radioComponent.canScan = true;
                        radioComponent.isWaitingForTrade = false;
                        radioComponent.lastTradeTick = 0;
                        radioComponent.lastScanTick = 0;
                        Messages.Message("冷却已取消", MessageTypeDefOf.PositiveEvent);
                    }
                }
            };

            // 开发用Gizmo - 立即准备交易
            yield return new Command_Action
            {
                defaultLabel = "立即交易",
                defaultDesc = "立即准备交易，跳过所有等待时间",
                icon = ContentFinder<Texture2D>.Get("RKU_Null"),
                action = delegate
                {
                    var radioComponent = Current.Game.GetComponent<RKU_RadioGameComponent>();
                    if (radioComponent != null)
                    {
                        radioComponent.canTrade = true;
                        radioComponent.isWaitingForTrade = false;
                        radioComponent.lastTradeTick = 0;
                        Messages.Message("交易已准备就绪", MessageTypeDefOf.PositiveEvent);
                    }
                }
            };

            // 开发用Gizmo - 加满研究进度
            yield return new Command_Action
            {
                defaultLabel = "加满研究进度",
                defaultDesc = "将当前研究进度设置为上限",
                icon = ContentFinder<Texture2D>.Get("RKU_Null"),
                action = delegate
                {
                    var component = Current.Game.GetComponent<RKU_RadioGameComponent>();
                    if (component != null)
                    {
                        component.researchProgress = RKU_RadioGameComponent.RESEARCH_PROGRESS_MAX;
                        Messages.Message("研究进度已加满", MessageTypeDefOf.PositiveEvent);
                    }
                }
            };

            // 开发用Gizmo - 研究进度加一半
            yield return new Command_Action
            {
                defaultLabel = "研究进度加一半",
                defaultDesc = "将当前研究进度加一半",
                icon = ContentFinder<Texture2D>.Get("RKU_Null"),
                action = delegate
                {
                    var component = Current.Game.GetComponent<RKU_RadioGameComponent>();
                    if (component != null)
                    {
                        component.researchProgress += RKU_RadioGameComponent.RESEARCH_PROGRESS_MAX / 2;
                        Messages.Message("研究进度已加一半", MessageTypeDefOf.PositiveEvent);
                    }
                }
            };

            // 开发用Gizmo - 关系等级 +10
            yield return new Command_Action
            {
                defaultLabel = "关系等级 +10",
                defaultDesc = "增加游击队关系等级10点",
                icon = ContentFinder<Texture2D>.Get("RKU_Null"),
                action = delegate
                {
                    var component = Current.Game.GetComponent<RKU_RadioGameComponent>();
                    if (component != null)
                    {
                        component.ralationshipGrade += 10;
                        Messages.Message("关系等级已增加10点", MessageTypeDefOf.PositiveEvent);
                    }
                }
            };

            // 开发用Gizmo - 关系等级 -10
            yield return new Command_Action
            {
                defaultLabel = "关系等级 -10",
                defaultDesc = "减少游击队关系等级10点",
                icon = ContentFinder<Texture2D>.Get("RKU_Null"),
                action = delegate
                {
                    var component = Current.Game.GetComponent<RKU_RadioGameComponent>();
                    if (component != null)
                    {
                        component.ralationshipGrade -= 10;
                        Messages.Message("关系等级已减少10点", MessageTypeDefOf.PositiveEvent);
                    }
                }
            };
        }
    }
}
