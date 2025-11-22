using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using Verse;
using Verse.AI;
using static Unity.Burst.Intrinsics.X86.Avx;

namespace RatkinUnderground;
public class Dialog_RKU_Radio : Window, ITrader
{
    #region 字段与构造
    public Thing radio;
    public Vector2 scrollPosition;
    public float scrollViewHeight;
    //当前对话
    public RKU_DialogueEventDef dialogueEventDefNow;
    // 字符滚动
    private StringBuilder displayBuilder = new StringBuilder();
    private string fullMessage = "";
    private int currentCharIndex = 0;
    private int tickCounter = 0;
    private const int CHARS_PER_TICK = 3;
    private bool isTyping = false;

    // 电台状态字符串属性
    public RKU_RadioGameComponent radioComponent => GetRadioComponent();
    public string RadioStatus { get; set; } = "RKU_RadioOnline".Translate();
    public string SignalQuality { get; set; } = "RKU_SignalGood".Translate();
    public string PowerStatus { get; set; } = "RKU_PowerGood".Translate();
    public string RadioPosition => radio?.Map.Tile.ToString() ?? "RKU_UnknownLocation".Translate();

    // 交易相关变量
    private List<Thing> traderGoods = new List<Thing>();
    private bool tradeReady = false;
    private bool hasTraded = false; // 标记是否发生了实际交易
    private bool tradeInProgress = false; // 一次运货

    // 扫描相关
    private List<WorldObjectDef> incidentMap => DefDatabase<WorldObjectDef>.AllDefsListForReading
                                .Where(d => d.defName != null && d.defName.StartsWith("RKU_MapParent"))
                                .ToList();


    // 界面+触发器
    private List<Rect> rects = new List<Rect>();   // 所有按钮实例 
    private List<RKU_RadioButton> buttons = new();
    private HashSet<string> triggers = new();
    private string randTrigger = "startup";

    // 王国军模式标记
    private bool isRoyalRadioMode = false;
    public Dialog_RKU_Radio(Thing radio)
    {
        try
        {
            this.radio = radio;
            this.doCloseButton = false;
            this.doCloseX = true;
            this.forcePause = false;
            this.absorbInputAroundWindow = true;
            this.closeOnClickedOutside = true;
            triggers.Clear();

            // 默认：每次打开对话框时清空消息历史，只显示本次对话的消息
            if (RKU_Mod.Instance.settings.showOnlyCurrentDialogueMessages)
            {
                var radioComp = GetRadioComp();
                if (radioComp != null)
                {
                    radioComp.ClearMessageHistory();
                }
            }
            var comp = radioComponent;
            if (comp != null && comp.isSearch)
            {
                Log.Message("符合正在研究条件");
                triggers.Add("research");
            }
            else
            {
                Log.Message("不符合正在研究条件");
            }

            if (Rand.Range(0, 100) < 50 && triggers.Count > 0)
            {
                randTrigger = triggers.RandomElement();
            }
            // 延迟触发初始对话，避免构造函数中出现问题
            LongEventHandler.QueueLongEvent(() =>
            {
                Log.Message($"当前trigger：{randTrigger}");
                RKU_DialogueManager.TriggerDialogueEvents(this, randTrigger);
            }, "RKU_TriggerInitialDialogue", false, null);
        }
        catch (Exception ex)
        {
            Log.Error($"[RKU] Dialog_RKU_Radio构造函数失败: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
    }
    #endregion

    #region ITrader接口实现
    public TraderKindDef TraderKind => DefDatabase<TraderKindDef>.GetNamed("RKU_RadioShop");

    public IEnumerable<Thing> Goods => traderGoods;

    public int RandomPriceFactorSeed => Find.TickManager.TicksGame;

    public string TraderName => "RKU_TraderName".Translate();

    public bool CanTradeNow => (tradeReady || (GetRadioComponent()?.CanTradeNow == true)) && !tradeInProgress;

    public float TradePriceImprovementOffsetForPlayer => 0f;

    public Faction Faction => Find.FactionManager.FirstFactionOfDef(FactionDef.Named("RKU_Faction"));

    public TradeCurrency TradeCurrency => this.TraderKind.tradeCurrency;
    #endregion

    //地鼠组件
    public RKU_RadioGameComponent GetRadioComponent()
    {
        if (Current.Game == null)
        {
            return null;
        }

        var component = Current.Game.GetComponent<RKU_RadioGameComponent>();
        if (component == null)
        {
            component = new RKU_RadioGameComponent(Current.Game);
            Current.Game.components.Add(component);
        }

        return component;
    }

    //电台comp
    private Comp_RKU_Radio GetRadioComp()
    {
        return radio?.TryGetComp<Comp_RKU_Radio>();
    }

    #region 主窗口与UI
    public override Vector2 InitialSize => new Vector2(800f, 600f);

    public override void DoWindowContents(Rect inRect)
    {
        UpdateTradeStatus();
        UpdateScanStatus();
        UpdateTypingEffect();
        UpdateRescueStatus();

        // 窗口标题
        Text.Font = GameFont.Medium;
        string windowTitle = isRoyalRadioMode ? "RKU_RoyalRadio".Translate() : "RKU_GuerrillaRadio".Translate();
        Widgets.Label(new Rect(0f, 0f, inRect.width, 35f), windowTitle);
        Text.Font = GameFont.Small;

        // 头像框区域 (左上角) - 调整为160x160
        Rect avatarRect = new Rect(10f, 45f, 160f, 160f);
        Widgets.DrawBoxSolid(avatarRect, Color.gray);
        Widgets.DrawBox(avatarRect, 2);

        // 根据关系等级加载不同头像
        Texture2D avatarTex = null;
        if (radioComponent != null)
        {
            string texPath = "";
            // 王国军模式
            if (isRoyalRadioMode)
            {
                switch (dialogueEventDefNow.defName)
                {
                    case "RKU_ProvideSupport_RoyalNegotiation":
                    case "RKU_ProvideSupport_RoyalNegotiationB":
                    case "RKU_ProvideSupport_RoyalNegotiationC":
                        texPath = "Things/Noble_1";
                        break;
                    default:
                        texPath = "Things/Noble_0";
                        break;
                }
            }
            else
            {
                int relationshipLevel = Utils.GetRelationshipLevel(radioComponent.ralationshipGrade);
                texPath = $"Things/Commander_{relationshipLevel}";
                //对话特殊情况
                if (dialogueEventDefNow != null)
                {
                    switch (dialogueEventDefNow.defName)
                    {
                        case "RKU_StartupEventDC":
                            texPath = $"Things/Commander_tongue";
                            break;
                        case "RKU_StartupEventK":
                            texPath = $"Things/Commander_x";
                            break;
                        case "RKU_FirstContact":
                        case "RKU_StartupEventFir":
                            texPath = $"Things/Commander_2";
                            break;
                        default:
                            break;
                    }
                }
            }
            avatarTex = ContentFinder<Texture2D>.Get(texPath, false);

        }

        // 如果未加载到关系头像，使用默认头像
        if (avatarTex == null)
        {
            avatarTex = ContentFinder<Texture2D>.Get("Things/Commander_Default", false);
        }

        // 绘制头像
        if (avatarTex != null)
        {
            Widgets.DrawTextureFitted(avatarRect, avatarTex, 0.9f);
        }

        Rect dialogRect = new Rect(180f, 45f, inRect.width - 190f, 440f);
        Widgets.DrawBoxSolid(dialogRect, new Color(0.1f, 0.1f, 0.1f, 0.8f));
        Widgets.DrawBox(dialogRect, 2);
        Text.Font = GameFont.Small;
        Widgets.Label(new Rect(dialogRect.x + 5f, dialogRect.y + 5f, dialogRect.width - 10f, 20f), "RKU_SignalIncoming".Translate());

        // 消息历史滚动区域
        Rect messageRect = new Rect(dialogRect.x + 5f, dialogRect.y + 30f, dialogRect.width - 10f, dialogRect.height - 35f);
        Widgets.BeginScrollView(messageRect, ref scrollPosition, new Rect(0f, 0f, messageRect.width - 16f, scrollViewHeight));

        float curY = 0f;
        var radioComp = GetRadioComp();
        if (radioComp != null)
        {
            // 如果正在打字，不显示最后一条消息（因为它正在被特效显示）
            int messageCount = isTyping ? 0 : radioComp.MessageHistory.Count;
            if (messageCount > 0)
            {
                string lastMessage = radioComp.MessageHistory[messageCount - 1];
                DrawMessageWithLineBreaks(new Rect(0f, curY, messageRect.width - 16f, 20f), lastMessage, ref curY);
            }
        }

        // 显示当前正在打字的消息
        if (isTyping)
        {
            string currentTypingMessage = displayBuilder.ToString();
            DrawMessageWithLineBreaks(new Rect(0f, curY, messageRect.width - 16f, 20f), currentTypingMessage, ref curY);
        }

        scrollViewHeight = curY + 10f;
        Widgets.EndScrollView();

        // 绘制电台状态
        DrawRadioStatus();

        // 按钮区域 (底部) - 调整位置，避免与关闭按钮重叠
        Rect buttonArea = new Rect(0f, inRect.height - 70f, inRect.width, 60f);
        Widgets.DrawBoxSolid(buttonArea, new Color(0.15f, 0.15f, 0.15f, 0.9f));
        Widgets.DrawBox(buttonArea, 1);

        Thing thing = new();

        float buttonX = 10f;
        float buttonWidth = 140f;
        float buttonHeight = 35f;

        RKU_RadioButton tradeButton = new(buttons, "RKU_TradeSignal".Translate(), true, null, new Rect(buttonX, buttonArea.y + 12f, buttonWidth, buttonHeight));
        buttonX += buttonWidth + 15f;
        RKU_RadioButton scanButton = new(buttons, "RKU_ScanSignal".Translate(), true, null, new Rect(buttonX, buttonArea.y + 12f, buttonWidth, buttonHeight));
        buttonX += buttonWidth + 15f;
        RKU_RadioButton rescueButton = new(buttons, "RKU_TacticalSupport".Translate(), true, null, new Rect(buttonX, buttonArea.y + 12f, buttonWidth, buttonHeight));
        buttonX += buttonWidth + 15f;
        RKU_RadioButton provideSupportButton = new(buttons, "RKU_ProvideSupport".Translate(), true, null, new Rect(buttonX, buttonArea.y + 12f, buttonWidth, buttonHeight));

        if (radioComponent != null)
        {
            // 交易检测
            if (!tradeInProgress && radioComponent.IsWaitingForTrade)
            {
                tradeButton.buttonText = $"{"RKU_WaitingForTrade".Translate()} ({radioComponent.GetRemainingTradeTime()})";
                tradeButton.canClick = false;
            }
            else if (tradeReady)
            {
                tradeButton.buttonText = "RKU_StartTrade".Translate();
                tradeButton.canClick = true;
            }
            else if (!radioComponent.canTrade)
            {
                tradeButton.buttonText = "RKU_TradeCooldownText".Translate();
                tradeButton.canClick = false;
            }
            else if (radioComponent.ralationshipGrade < 1)
            {
                tradeButton.buttonText = $"<color=#808080>{"RKU_TradeSignal".Translate()}</color>";
                tradeButton.failReason = "RKU_TradeFavorRequirement".Translate();
                tradeButton.canClick = false;
            }
            else
            {
                tradeButton.buttonText = "RKU_TradeSignal".Translate();
                tradeButton.canClick = true;
            }

            // 扫描检测
            if (radioComponent.ralationshipGrade < 50)
            {
                scanButton.buttonText = $"<color=#808080>{"RKU_ScanSignal".Translate()}</color>";
                scanButton.failReason = "RKU_ScanFavorRequirement".Translate();
                scanButton.canClick = false;
            }
            else if (!radioComponent.canScan)
            {
                scanButton.buttonText = "RKU_ScanCooldownText".Translate();
                scanButton.canClick = false;
            }

            // 救援检测
            if (radioComponent.ralationshipGrade < 75)
            {
                rescueButton.buttonText = $"<color=#808080>{"RKU_EmergencyCall".Translate()}</color>";
                rescueButton.failReason = "RKU_TacticalSupportFailReason".Translate();
                rescueButton.canClick = false;
            }
            else if (!radioComponent.canEmergency)
            {
                int remainingDays = radioComponent.GetRemainingEmergencyCooldownDays();
                rescueButton.buttonText = "RKU_EmergencyCooldownButton".Translate(remainingDays);
                rescueButton.failReason = "RKU_EmergencyCooldownText".Translate(remainingDays);
                rescueButton.canClick = false;
            }

            // 提供支援冷却检测
            if (!radioComponent.canRescue)
            {
                int remainingDays = radioComponent.GetRemainingRescueCooldownDays();
                provideSupportButton.buttonText = $"<color=#808080>{"RKU_RescueCooldownButton".Translate(remainingDays)}</color>";
                provideSupportButton.failReason = "RKU_RescueCooldownText".Translate(remainingDays);
                provideSupportButton.canClick = false;
            }
        }

        if (Widgets.ButtonText(tradeButton.rect, tradeButton.buttonText))
        {
            if (tradeButton.canClick)
            {
                if (tradeReady)
                {
                    // 触发交易相关对话事件
                    RKU_DialogueManager.TriggerDialogueEvents(this, "trade");

                    // 打开交易窗口
                    OpenTradeWindow();
                    // 重置交易准备状态
                    tradeReady = false;
                }
                else
                {
                    // 触发交易信号相关对话事件
                    RKU_DialogueManager.TriggerDialogueEvents(this, "trade");
                    // 发送交易信号
                    StartTradeSignal();
                }
            }
        }
        //buttonX += buttonWidth + 15f;

        // 扫描信号按钮
        if (Widgets.ButtonText(scanButton.rect, scanButton.buttonText))
        {
            if (scanButton.canClick)
            {
                // 触发扫描相关对话事件
                RKU_DialogueManager.TriggerDialogueEvents(this, "scan");
                AddMessage("RKU_StartScanningSignal".Translate());

                int tile = Utils.GetRadiusTiles(radio.Map.Tile, 20);
                try
                {
                    // 如果你看到这行，我跟军爷抢饭去了，回来再修
                    // 2025/9/23 别动这块了，修了一晚上，我怕
                    // worldObjectClass要使用RatkinUnderground.RKU_MapParent，走自定义逻辑进地图（实则生成地图）
                    // RKU_MapParentModExtension用于标记是否为生成地图，防止钻机遭遇的地图有多个进入方法

                    /*WorldObjectDef def = incidentMap.RandomElement();
                    WorldObject worldObject = WorldObjectMaker.MakeWorldObject(def);
                    worldObject.Tile = tile;
                    worldObject.SetFaction(Faction.OfPlayer);
                    Find.WorldObjects.Add(worldObject);*/

                    List<WorldObjectDef> worldObjectDefs = DefDatabase<WorldObjectDef>.AllDefsListForReading
                                .Where(d => d.defName != null &&
                                d.defName.StartsWith("RKU_MapParent") &&
                                d.GetModExtension<RKU_MapParentModExtension>() != null)
                                .ToList();
                    if (worldObjectDefs == null || worldObjectDefs.Count == 0)
                    {
                        Log.Error("[RKU] 没有找到任何 RKU_Incident 开头的 IncidentDef，无法生成地图/世界物体。");
                        return;
                    }
                    WorldObjectDef def = worldObjectDefs.RandomElement();
                    var ext = def.GetModExtension<RKU_MapParentModExtension>();
                    if (ext == null)
                    {
                        Log.Warning($"[RKU] 选中的 IncidentDef {def.defName} 没有 RKU_MapParentModExtension；将跳过设置 spawnMap 标记。");
                    }
                    def.GetModExtension<RKU_MapParentModExtension>().spawnMap = true;
                    Map map = Find.Maps.FirstOrDefault(m => m.Tile == tile);
                    // build parms - 使用 StorytellerUtility 获取合理默认值
                    WorldObject worldObject = WorldObjectMaker.MakeWorldObject(def);
                    worldObject.Tile = tile;
                    worldObject.SetFaction(Faction.OfPlayer);
                    Find.WorldObjects.Add(worldObject);
                    // 指定 tile（许多 world 级事件会使用 parms.targetTile）
                    /*parms.faction = null;
                    parms.target = map;
                    def.Worker.TryExecute(parms);*/
                    string label = "RKU_SiteDiscoveredLabel".Translate(def.label);
                    string text = "RKU_SiteDiscoveredText".Translate(def.label);
                    LookTargets lookTargets = new LookTargets(worldObject);
                    Find.LetterStack.ReceiveLetter(label, text, LetterDefOf.PositiveEvent, lookTargets);

                    radioComponent.canScan = false;
                    radioComponent.lastScanTick = Find.TickManager.TicksGame;

                    Log.Message($"[RKU] 成功在 tile {tile} 生成世界物体：{def.defName}");
                }
                catch (Exception e)
                {
                    Log.Error($"[RKU] 扫描信号生成地图发生错误：{e}");
                }
            }
        }
        //buttonX += buttonWidth + 15f;

        // 紧急呼叫按钮
        if (Widgets.ButtonText(rescueButton.rect, rescueButton.buttonText))
        {
            if (rescueButton.canClick)
            {
                AddMessage("RKU_EmergencyCallSent".Translate());
                var emergencyEvents = DefDatabase<RKU_DialogueEventDef>.AllDefs
                    .Where(e => e.defName.StartsWith("RKU_EmergencyCall"))
                    .ToList();

                if (emergencyEvents.Count > 0)
                {
                    RKU_DialogueEventDef randomEvent = emergencyEvents.RandomElement();
                    RKU_DialogueManager.ExecuteDialogueEvent(randomEvent, this);
                }
            }
        }

        // 提供支援
        if (Widgets.ButtonText(provideSupportButton.rect, provideSupportButton.buttonText))
        {
            if (provideSupportButton.canClick)
            {
                int conditionMet = 0;
                if (radioComponent != null)
                {
                    //敌对线
                    if (radioComponent.ralationshipGrade <= -25)
                    {
                        QuestScriptDef questDef = DefDatabase<QuestScriptDef>.GetNamed("RKU_OpportunitySite_GuerrillaCamp", false);
                        if (radioComponent.ralationshipGrade == -25 && !Find.QuestManager.QuestsListForReading.Any(o => o.root == questDef) &&
                            radioComponent.canRescue)
                        {
                            conditionMet = -2;
                            radioComponent.canRescue = false;
                            radioComponent.lastRescueTick = Find.TickManager.TicksGame;
                        }
                        else if (radioComponent.ralationshipGrade == -50)
                        {
                            // 检查事件是否已经触发过（只能触发一次）
                            string eventKey = "RKU_RatkinTunnel_Thi";
                            bool hasTriggered = radioComponent.triggeredOnceEvents != null && radioComponent.triggeredOnceEvents.Contains(eventKey);
                            if (!hasTriggered && 
                                radioComponent.canRescue)
                            {
                                conditionMet = -3;
                                radioComponent.canRescue = false;
                                radioComponent.lastRescueTick = Find.TickManager.TicksGame;
                            }
                            else
                            {
                                conditionMet = -1; // 已经触发过，切换到王国军频段但没人接
                            }
                        }
                        else if (radioComponent.ralationshipGrade == -75)
                        {
                            // 检查事件是否已经触发过（只能触发一次）
                            string eventKey = "RKU_IncidentWorker_FinalRaid";
                            bool hasTriggered = radioComponent.triggeredOnceEvents != null && radioComponent.triggeredOnceEvents.Contains(eventKey);
                            if (!hasTriggered &&
                                radioComponent.canRescue)
                            {
                                conditionMet = -4;
                                radioComponent.canRescue = false;
                                radioComponent.lastRescueTick = Find.TickManager.TicksGame;
                            }
                            else
                            {
                                conditionMet = -1; // 已经触发过，切换到王国军频段但没人接
                            }
                        }
                        else
                        {
                            conditionMet = -1;
                        }
                    }
                    else
                    {
                        //农场
                        if (radioComponent.ralationshipGrade >= 15 && radioComponent.ralationshipGrade <= 100)
                        {
                            bool hasTriggeredWarLord1 = radioComponent.triggeredOnceEvents != null && radioComponent.triggeredOnceEvents.Contains("RKU_ProvideSupport_FarmRaid");
                            if (!hasTriggeredWarLord1 &&
                                radioComponent.canRescue)
                            {
                                conditionMet = 1; 
                                radioComponent.canRescue = false;
                                radioComponent.lastRescueTick = Find.TickManager.TicksGame;
                            }
                            else
                            {
                                //城堡
                                if (radioComponent.ralationshipGrade >= 30)
                                {
                                    bool hasTriggeredWarLord2 = radioComponent.triggeredOnceEvents != null && radioComponent.triggeredOnceEvents.Contains("RKU_ProvideSupport_WarLord2");
                                    if (!hasTriggeredWarLord2 &&
                                        radioComponent.canRescue)
                                    {
                                        conditionMet = 2; 
                                        radioComponent.canRescue = false;
                                        radioComponent.lastRescueTick = Find.TickManager.TicksGame;
                                    }
                                    else
                                    {
                                        //炮楼
                                        if (radioComponent.ralationshipGrade >= 55)
                                        {
                                            bool hasTriggeredFarm = radioComponent.triggeredOnceEvents != null && radioComponent.triggeredOnceEvents.Contains("RKU_ProvideSupport_WarLord1");
                                            if (!hasTriggeredFarm &&
                                                radioComponent.canRescue)
                                            {
                                                conditionMet = 3;
                                                radioComponent.canRescue = false;
                                                radioComponent.lastRescueTick = Find.TickManager.TicksGame;
                                            }
                                            else
                                            {
                                                //古代设施
                                                if (radioComponent.ralationshipGrade >= 76)
                                                {
                                                    bool hasTriggeredAncient = radioComponent.triggeredOnceEvents != null && radioComponent.triggeredOnceEvents.Contains("RKU_ProvideSupport_AncientRaid");
                                                    if (!hasTriggeredAncient &&
                                                        radioComponent.canRescue)
                                                    {
                                                        conditionMet = 4; 
                                                        radioComponent.canRescue = false;
                                                        radioComponent.lastRescueTick = Find.TickManager.TicksGame;
                                                    }
                                                    else
                                                    {
                                                        //工厂防御
                                                        if (radioComponent.ralationshipGrade >= 80)
                                                        {
                                                            bool hasTriggeredFactoryDefense = radioComponent.triggeredOnceEvents != null && radioComponent.triggeredOnceEvents.Contains("RKU_ProvideSupport_FactoryDefense");
                                                            if (!hasTriggeredFactoryDefense && 
                                                                radioComponent.canRescue)
                                                            {
                                                                conditionMet = 5;
                                                                radioComponent.canRescue = false;
                                                                radioComponent.lastRescueTick = Find.TickManager.TicksGame;
                                                            }
                                                            else
                                                            {
                                                                conditionMet = 0;
                                                            }
                                                        }
                                                        else
                                                        {
                                                            conditionMet = 0;
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    conditionMet = 0;
                                                }
                                            }
                                        }
                                        else
                                        {
                                            conditionMet = 0;
                                        }
                                    }
                                }
                                else
                                {
                                    conditionMet = 0;
                                }
                            }
                        }
                        else
                        {
                            conditionMet = 0;
                        }
                    }
                }
                // 触发
                switch (conditionMet)
                {
                    case 0: // 没事，游击队情况
                        isRoyalRadioMode = false;
                        RKU_DialogueEventDef nobodyEventA = DefDatabase<RKU_DialogueEventDef>.GetNamed("RKU_StartupEventZeroA", false);
                        RKU_DialogueEventDef nobodyEventB = DefDatabase<RKU_DialogueEventDef>.GetNamed("RKU_StartupEventZeroB", false);
                        if (radioComponent.ralationshipGrade > 0)
                        {
                            RKU_DialogueManager.ExecuteDialogueEvent(nobodyEventA, this);
                        }
                        else
                        {
                            RKU_DialogueManager.ExecuteDialogueEvent(nobodyEventB, this);
                        }
                        break;
                    case -1: // 好感度小于等于-25，切换到王国军频段但没人接
                        isRoyalRadioMode = true;
                        RKU_DialogueEventDef nobodyEvent = DefDatabase<RKU_DialogueEventDef>.GetNamed("RKU_StartupEventZero", false);
                        if (nobodyEvent != null)
                        {
                            RKU_DialogueManager.ExecuteDialogueEvent(nobodyEvent, this);
                        }
                        break;
                    case -2: // 好感度等于-25且没有任务可接受，触发与王国军交涉对话并开启任务
                        isRoyalRadioMode = true;
                        QuestScriptDef questDef = DefDatabase<QuestScriptDef>.GetNamed("RKU_OpportunitySite_GuerrillaCamp", false);
                        if (questDef != null && !Find.QuestManager.QuestsListForReading.Any(o => o.root == questDef))
                        {
                            RKU_DialogueEventDef negotiationEvent = DefDatabase<RKU_DialogueEventDef>.GetNamed("RKU_ProvideSupport_RoyalNegotiation", false);
                            if (negotiationEvent != null)
                            {
                                RKU_DialogueManager.ExecuteDialogueEvent(negotiationEvent, this);
                            }
                        }
                        break;
                    case -3: // 好感度等于-50，触发对话并触发伊文事件
                        isRoyalRadioMode = true;
                        RKU_DialogueEventDef negotiationEventC = DefDatabase<RKU_DialogueEventDef>.GetNamed("RKU_ProvideSupport_RoyalNegotiationB", false);
                        if (negotiationEventC != null)
                        {
                            RKU_DialogueManager.ExecuteDialogueEvent(negotiationEventC, this);
                        }
                        break;
                    case -4: // 好感度等于-75，触发对话并触发血战
                        isRoyalRadioMode = true;
                        RKU_DialogueEventDef negotiationEventD = DefDatabase<RKU_DialogueEventDef>.GetNamed("RKU_ProvideSupport_RoyalNegotiationC", false);
                        if (negotiationEventD != null)
                        {
                            RKU_DialogueManager.ExecuteDialogueEvent(negotiationEventD, this);
                        }
                        break;

                    case 1: // 农场任务：好感度 >= 15
                        isRoyalRadioMode = false;
                        RKU_DialogueEventDef farmEvent = DefDatabase<RKU_DialogueEventDef>.GetNamed("RKU_ProvideSupport_FarmRaid", false);
                        if (farmEvent != null)
                        {
                            RKU_DialogueManager.ExecuteDialogueEvent(farmEvent, this);
                        }
                        break;
                   
                    case 2: // 城堡：好感度 >= 30
                        isRoyalRadioMode = false;
                        RKU_DialogueEventDef warLordCastleEvent = DefDatabase<RKU_DialogueEventDef>.GetNamed("RKU_ProvideSupport_WarLord2", false);
                        if (warLordCastleEvent != null)
                        {
                            RKU_DialogueManager.ExecuteDialogueEvent(warLordCastleEvent, this);
                        }
                        break;
                    case 3: // 炮楼：好感度 >= 55
                        isRoyalRadioMode = false;
                        RKU_DialogueEventDef warLordEvent = DefDatabase<RKU_DialogueEventDef>.GetNamed("RKU_ProvideSupport_WarLord1", false);
                        if (warLordEvent != null)
                        {
                            RKU_DialogueManager.ExecuteDialogueEvent(warLordEvent, this);
                        }
                        break;
                    case 4: // 古代设施任务：好感度 >= 76
                        isRoyalRadioMode = false;
                        RKU_DialogueEventDef ancientEvent = DefDatabase<RKU_DialogueEventDef>.GetNamed("RKU_ProvideSupport_AncientRaid", false);
                        if (ancientEvent != null)
                        {
                            RKU_DialogueManager.ExecuteDialogueEvent(ancientEvent, this);
                        }
                        break;
                    case 5: // 工厂防御任务：好感度 >= 80 且农场任务已完成，触发对话并开启工厂防御任务
                        isRoyalRadioMode = false;
                        RKU_DialogueEventDef factoryDefenseEvent = DefDatabase<RKU_DialogueEventDef>.GetNamed("RKU_ProvideSupport_FactoryDefense", false);
                        if (factoryDefenseEvent != null)
                        {
                            RKU_DialogueManager.ExecuteDialogueEvent(factoryDefenseEvent, this);
                        }
                        break;
                    default:
                        break;
                }
            }
        }

        // 添加自定义关闭按钮
        if (Widgets.ButtonText(new Rect(inRect.width - 80f, buttonArea.y + 12f, 70f, buttonHeight), "Close".Translate()))
        {
            this.Close();
        }
        DrawFailReason(buttons);
    }
    #endregion

    /// <summary>
    /// 更新交易状态
    /// </summary>
    private void UpdateTradeStatus()
    {
        var comp = GetRadioComponent();
        int currentTick = Find.TickManager.TicksGame;

        // 冷却结束
        if (!comp.canTrade && currentTick - comp.lastTradeTick >= comp.tradeCooldownTicks)
        {
            comp.canTrade = true;
            comp.isWaitingForTrade = false;
        }

        // 扫描冷却结束
        if (!comp.canScan && currentTick - comp.lastScanTick >= comp.scanCooldownTicks)
        {
            comp.canScan = true;
        }

        // 求救呼叫冷却结束
        if (!comp.canEmergency && currentTick - comp.lastEmergencyTick >= comp.emergencyCooldownTicks)
        {
            comp.canEmergency = true;
        }

        // 交易准备完成 - 只设置准备状态，不进入冷却
        if (comp.isWaitingForTrade && currentTick - comp.tradeStartTick >= comp.currentTradeDelayTicks)
        {
            comp.isWaitingForTrade = false;
            tradeReady = true;
        }
    }

    /// <summary>
    /// 更新营救状态
    /// </summary>
    private void UpdateRescueStatus()
    {
        var comp = GetRadioComponent();
        int currentTick = Find.TickManager.TicksGame;
        // 求救呼叫冷却结束
        if (!comp.canRescue && currentTick - comp.lastRescueTick >= comp.rescueCooldownTicks)
        {
            comp.canRescue = true;
        }
    }

    #region 交易相关方法

    private void StartTradeSignal()
    {
        var radioComponent = GetRadioComponent();
        if (radioComponent == null || !radioComponent.CanTradeNow) return;

        radioComponent.StartTradeSignal();
        tradeReady = false;

        AddMessage("RKU_SignalReceived".Translate());

        Messages.Message("RKU_TradeSignalSent".Translate(), MessageTypeDefOf.PositiveEvent);
    }

    private void OpenTradeWindow()
    {
        tradeInProgress = true;
        hasTraded = false;
        // 生成交易货物
        this.TraderKind.stockGenerators.ForEach(o => traderGoods.AddRange(o.GenerateThings(radio.Map.Tile)));
        AddMessage("RKU_CheckOurGoods".Translate(),
            delegate
            {
                // 打开交易窗口
                Pawn negotiator = Find.CurrentMap.mapPawns.FreeColonists
                    .Where(p => p.skills.GetSkill(SkillDefOf.Social).Level >= 1) // 得有社交
                    .OrderByDescending(p => p.skills.GetSkill(SkillDefOf.Social).Level)
                    .FirstOrDefault();

                if (negotiator == null)
                {
                    // 如果没有合适的谈判者，选择任意殖民者
                    negotiator = Find.CurrentMap.mapPawns.FreeColonists.RandomElement();
                }

                if (negotiator != null)
                {
                    Find.WindowStack.Add(new Dialog_Trade(negotiator, this, false));
                }
                else
                {
                    Messages.Message("RKU_NoNegotiatorAvailable".Translate(), MessageTypeDefOf.RejectInput);
                }
            });
    }

    public void OnTradeReady()
    {
        tradeReady = true;
        AddMessage("RKU_GoodsReady".Translate());
    }
    #endregion

    #region 扫描部分
    private void UpdateScanStatus()
    {
        var comp = GetRadioComponent();
        int currentTick = Find.TickManager.TicksGame;

        // 冷却结束
        if (currentTick - comp.lastScanTick >= comp.scanCooldownTicks)
        {
            comp.canScan = true;
        }
    }
    #endregion

    #region 对话与消息滚动
    public void AddMessage(string message)
    {
        var radioComp = GetRadioComp();
        StartTyping(message);
        scrollPosition.y = float.MaxValue;
    }

    public void AddMessage(string message, Action onComplete)
    {
        var radioComp = GetRadioComp();
        StartTyping(message, onComplete);
        scrollPosition.y = float.MaxValue;
    }

    // 开始打字机效果
    private void StartTyping(string message)
    {
        fullMessage = message;
        currentCharIndex = 0;
        tickCounter = 0;
        displayBuilder.Clear();
        isTyping = true;
    }

    // 开始打字机效果，并指定完成回调
    private void StartTyping(string message, Action onComplete)
    {
        fullMessage = message;
        currentCharIndex = 0;
        tickCounter = 0;
        displayBuilder.Clear();
        isTyping = true;
        // 在打字完成后执行回调
        if (onComplete != null)
        {
            onComplete();
        }
    }

    // 字符滚动
    private void UpdateTypingEffect()
    {
        if (!isTyping || currentCharIndex >= fullMessage.Length)
        {
            // 打字特效完成，将消息添加到历史记录
            if (isTyping)
            {
                var radioComp = GetRadioComp();
                radioComp?.AddMessage(fullMessage);
            }
            isTyping = false;
            return;
        }

        tickCounter++;
        if (tickCounter >= CHARS_PER_TICK)
        {
            tickCounter = 0;
            if (currentCharIndex < fullMessage.Length)
            {
                char currentChar = fullMessage[currentCharIndex];
                displayBuilder.Append(currentChar);
                currentCharIndex++;
            }
        }
    }

    /// <summary>
    /// 绘制消息，支持换行符
    /// </summary>
    private void DrawMessageWithLineBreaks(Rect rect, string message, ref float curY)
    {
        // 按换行符分割消息
        string[] lines = message.Split('\n');
        float lineHeight = 20f;

        if (string.IsNullOrEmpty(message))
        {
            curY += 25f;
            return;
        }

        foreach (string line in lines)
        {
            Widgets.Label(new Rect(rect.x, curY, rect.width, lineHeight), line);
            curY += lineHeight;
        }
    }
    #endregion

    /// <summary>
    /// 绘制电台状态信息
    /// </summary>
    private void DrawRadioStatus()
    {
        // 状态信息区域 (头像下方) - 调整位置和大小
        Rect statusRect = new Rect(10f, 215f, 160f, 270f);
        Widgets.DrawBoxSolid(statusRect, new Color(0.2f, 0.2f, 0.2f, 0.8f));
        Widgets.DrawBox(statusRect, 1);
        var radioComponent = GetRadioComponent();

        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(statusRect.x + 5f, statusRect.y + 5f, statusRect.width - 10f, 20f), "RKU_RadioStatus".Translate());
        Widgets.Label(new Rect(statusRect.x + 5f, statusRect.y + 30f, statusRect.width - 10f, 20f), $"● {RadioStatus}");

        string researchText = "RKU_ResearchProgress".Translate() + ": 0/100";
        if (radioComponent != null)
        {
            researchText = "RKU_ResearchProgress".Translate() + $": {radioComponent.researchProgress}/{RKU_RadioGameComponent.RESEARCH_PROGRESS_MAX}";
        }
        Widgets.Label(new Rect(statusRect.x + 5f, statusRect.y + 105f, statusRect.width - 10f, 20f), $"● {researchText}");
        Widgets.Label(new Rect(statusRect.x + 5f, statusRect.y + 55f, statusRect.width - 10f, 20f), $"● {SignalQuality}");
        Widgets.Label(new Rect(statusRect.x + 5f, statusRect.y + 80f, statusRect.width - 10f, 20f), $"● {PowerStatus}");

        string rationText = "RKU_FactionRelation".Translate() + ": 0";
        if (GetRadioComponent() != null)
        {
            rationText = "RKU_FactionRelation".Translate() + $": {radioComponent.ralationshipGrade}";
        }
        Widgets.Label(new Rect(statusRect.x + 5f, statusRect.y + 130f, statusRect.width - 10f, 20f), $"● {rationText}");

        var counts = 0;

        // 添加交易状态信息
        if (radioComponent != null && !radioComponent.CanTradeNow)
        {
            int remainingDays = radioComponent.GetRemainingCooldownDays();
            Widgets.Label(new Rect(statusRect.x + 5f, statusRect.y + 180f + counts * 25f, statusRect.width - 10f, 20f), "RKU_TradeCooldown".Translate(remainingDays));
            counts++;
        }
        // 扫描状态信息
        if (radioComponent != null && !radioComponent.canScan)
        {
            int remainingDays = radioComponent.GetRemainingScanCooldownDays();
            Widgets.Label(new Rect(statusRect.x + 5f, statusRect.y + 180f + counts * 25f, statusRect.width - 10f, 20f), "RKU_ScanCooldown".Translate(remainingDays));
            counts++;
        }

        // 紧急呼叫状态信息
        if (radioComponent != null && !radioComponent.canEmergency)
        {
            int remainingDays = radioComponent.GetRemainingEmergencyCooldownDays();
            Widgets.Label(new Rect(statusRect.x + 5f, statusRect.y + 180f + counts * 25f, statusRect.width - 10f, 20f), "RKU_EmergencyCooldown".Translate(remainingDays));
            counts++;
        }
        Widgets.Label(new Rect(statusRect.x + 5f, statusRect.y + 155f, statusRect.width - 10f, 20f), "● " + "RKU_MapLocation".Translate(RadioPosition));

    }

    /// <summary>
    /// 绘制失败原因提示
    /// </summary>
    /// <param name="canClick"></param>
    /// <param name="rects"></param>
    /// <param name="tradeDisableReason"></param>
    void DrawFailReason(List<RKU_RadioButton> buttons)
    {

        foreach (var button in buttons)
        {
            if (button.canClick && string.IsNullOrEmpty(button.failReason)) continue;
            TooltipHandler.TipRegion(button.rect, button.failReason);
        }
    }

    #region ITrader接口方法实现

    public IEnumerable<Thing> ColonyThingsWillingToBuy(Pawn playerNegotiator)
    {
        IEnumerable<Thing> enumerable = from x in radio.Map.listerThings.AllThings
                                        where (x is Pawn p && p.Faction == Faction.OfPlayer) ||
                                              (x.def.category == ThingCategory.Item &&
                                               !x.IsForbidden(playerNegotiator) &&
                                               !x.Position.Fogged(x.Map))
                                        select x;
        foreach (Thing thing in radio.Map.listerThings.AllThings)
        {
            if (thing.def.IsProcessedFood &&
                !thing.IsForbidden(playerNegotiator) &&
                !thing.Position.Fogged(radio.Map))
            {
                yield return thing;
            }
        }

        foreach (Thing thing in enumerable)
        {
            yield return thing;
        }
    }

    public void GiveSoldThingToTrader(Thing toGive, int countToGive, Pawn playerNegotiator)
    {
        if (toGive != null && !toGive.Destroyed && countToGive > 0)
        {
            toGive.Destroy();
            hasTraded = true;
        }
    }

    public void GiveSoldThingToPlayer(Thing toGive, int countToGive, Pawn playerNegotiator)
    {
        if (toGive != null && !toGive.Destroyed && countToGive > 0)
        {
            Thing thing = toGive.SplitOff(countToGive);
            if (thing != null)
            {
                AddToCargoList(thing);
                hasTraded = true;
            }
        }
    }

    private List<Thing> pendingCargo = new List<Thing>();

    private void AddToCargoList(Thing thing)
    {
        if (thing != null && !thing.Destroyed)
        {
            Log.Warning("add+" + thing.def.defName);

            pendingCargo.Add(thing);
        }
    }

    // 在交易完成时发送钻地货舱
    public RKU_DrillingCargoPodBullet SendCargoPod()
    {
        if (pendingCargo.Count == 0) return null;
        IntVec3 launchSpot = Utils.FindLaunchSpot(radio.Map);
        if (launchSpot == IntVec3.Invalid) return null;
        RKU_DrillingCargoPod cargoPod = ThingMaker.MakeThing(ThingDef.Named("RKU_DrillingCargoPod")) as RKU_DrillingCargoPod;
        if (cargoPod != null)
        {
            // 将货物添加到货舱中
            foreach (Thing thing in pendingCargo)
            {
                if (thing != null && !thing.Destroyed)
                {
                    cargoPod.AddCargo(thing);
                }
            }

            RKU_DrillingCargoPodBullet cargoPodBullet = ThingMaker.MakeThing(ThingDef.Named("RKU_DrillingCargoPodBullet")) as RKU_DrillingCargoPodBullet;
            if (cargoPodBullet != null)
            {
                cargoPodBullet.rKU_DrillingCargoPod = cargoPod;

                var targetingParams = new TargetingParameters
                {
                    canTargetLocations = true,
                    canTargetBuildings = false,
                    canTargetPawns = false,
                    validator = (target) =>
                    {
                        if (!target.Cell.InBounds(radio.Map)) return false;
                        return Utils.CanSpawnTunnelAt(target.Cell, radio.Map);
                    }
                };

                Find.Targeter.BeginTargeting(targetingParams, (LocalTargetInfo target) =>
                {
                    if (target.Cell.InBounds(radio.Map) && Utils.CanSpawnTunnelAt(target.Cell, radio.Map))
                    {
                        GenSpawn.Spawn(cargoPodBullet, launchSpot, radio.Map);
                        cargoPodBullet.Launch(null, new LocalTargetInfo(target.Cell), new LocalTargetInfo(target.Cell), ProjectileHitFlags.None);
                    }
                });

                pendingCargo.Clear();
            }
            return cargoPodBullet;
        }
        return null;
    }
    public override void Close(bool doCloseSound = true)
    {
        // 处理货舱发送逻辑
        if (pendingCargo.Count > 0)
        {
            ChoiceLetter choiceLetter = LetterMaker.MakeLetter("RKU_GoodsArrived".Translate(), "RKU_OrderedGoodsArrived".Translate(), LetterDefOf.PositiveEvent);
            RKU_DrillingCargoPodBullet pod = null;
            LongEventHandler.QueueLongEvent(() =>
            {
                pod = SendCargoPod();
                Find.LetterStack.ReceiveLetter(choiceLetter.Label, choiceLetter.Text, choiceLetter.def, lookTargets: pod.usedTarget.ToTargetInfo(pod.Map));
            }, "SendingCargoPod", doAsynchronously: false, null);
        }

        // 检查是否发生实际交易
        var radioComponent = GetRadioComponent();
        if (radioComponent != null && hasTraded)
        {
            // 只有实际交易时才触发冷却
            radioComponent.canTrade = false;
            radioComponent.lastTradeTick = Find.TickManager.TicksGame;
        }
        tradeInProgress = false;
        base.Close(doCloseSound);
    }
    #endregion

}

#region 按钮类
public class RKU_RadioButton
{
    public string buttonText;
    public bool canClick = true;
    public string failReason = null;
    public Rect rect = new();

    public RKU_RadioButton() { }
    public RKU_RadioButton(List<RKU_RadioButton> targetList, string buttonText, bool canClick = true, string failReason = null, Rect rect = default)
    {
        this.buttonText = buttonText;
        this.canClick = canClick;
        this.failReason = failReason;
        this.rect = rect;
        targetList?.Add(this);
    }
}
#endregion