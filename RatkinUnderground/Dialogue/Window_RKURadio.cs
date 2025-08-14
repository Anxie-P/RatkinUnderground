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
public class Dialog_RKU_Radio : Window, ITrader
{
    #region 字段与构造
    public Thing radio;
    public Vector2 scrollPosition;
    public float scrollViewHeight;

    // 字符滚动
    private StringBuilder displayBuilder = new StringBuilder();
    private string fullMessage = "";
    private int currentCharIndex = 0;
    private int tickCounter = 0;
    private const int CHARS_PER_TICK = 3;
    private bool isTyping = false;

    // 电台状态字符串属性
    public string RadioStatus { get; set; } = "在线";
    public string SignalQuality { get; set; } = "信号良好";
    public string PowerStatus { get; set; } = "电量充足";
    public string RadioPosition => radio?.Map.Tile.ToString() ?? "未知位置";

    // 交易相关变量
    private List<Thing> traderGoods = new List<Thing>();
    private bool tradeReady = false;
    private bool hasTraded = false; // 标记是否发生了实际交易
    private bool tradeInProgress = false; // 一次运货

    public Dialog_RKU_Radio(Thing radio)
    {
        this.radio = radio;
        this.doCloseButton = false;
        this.doCloseX = true;
        this.forcePause = false;
        this.absorbInputAroundWindow = true;
        this.closeOnClickedOutside = true;

        // 触发初始对话
        AddMessage("电台已启动，正在监听信号...");
        AddMessage("地下网络连接正常");
        AddMessage(" ");

        // 触发一般对话事件
        RKU_DialogueManager.TriggerDialogueEvents(this);
    }
    #endregion

    #region ITrader接口实现
    public TraderKindDef TraderKind => DefDatabase<TraderKindDef>.GetNamed("RKU_RadioShop");

    public IEnumerable<Thing> Goods => traderGoods;

    public int RandomPriceFactorSeed => Find.TickManager.TicksGame;

    public string TraderName => "地鼠物资运输队";

    public bool CanTradeNow => (tradeReady || (GetRadioComponent()?.CanTradeNow == true)) && !tradeInProgress;

    public float TradePriceImprovementOffsetForPlayer => 0f;

    public Faction Faction => Find.FactionManager.FirstFactionOfDef(FactionDef.Named("RKU_Faction"));

    public TradeCurrency TradeCurrency => this.TraderKind.tradeCurrency;
    #endregion

    //地鼠组件
    public RKU_RadioGameComponent GetRadioComponent()
    {
        return Current.Game.GetComponent<RKU_RadioGameComponent>();
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
        UpdateTypingEffect();

        // 窗口标题
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(0f, 0f, inRect.width, 35f), "抵抗军电台");
        Text.Font = GameFont.Small;

        // 头像框区域 (左上角) - 调整为160x160
        Rect avatarRect = new Rect(10f, 45f, 160f, 160f);
        Widgets.DrawBoxSolid(avatarRect, Color.gray);
        Widgets.DrawBox(avatarRect, 2);

        // 绘制头像图标
        Texture2D avatarTex = ContentFinder<Texture2D>.Get("Things/Commander", false);
        if (avatarTex != null)
        {
            Widgets.DrawTextureFitted(avatarRect, avatarTex, 0.9f);
        }
        Rect dialogRect = new Rect(180f, 45f, inRect.width - 190f, 440f);
        Widgets.DrawBoxSolid(dialogRect, new Color(0.1f, 0.1f, 0.1f, 0.8f));
        Widgets.DrawBox(dialogRect, 2);
        Text.Font = GameFont.Small;
        Widgets.Label(new Rect(dialogRect.x + 5f, dialogRect.y + 5f, dialogRect.width - 10f, 20f), "信号传入中...");

        // 消息历史滚动区域
        Rect messageRect = new Rect(dialogRect.x + 5f, dialogRect.y + 30f, dialogRect.width - 10f, dialogRect.height - 35f);
        Widgets.BeginScrollView(messageRect, ref scrollPosition, new Rect(0f, 0f, messageRect.width - 16f, scrollViewHeight));

        float curY = 0f;
        var radioComp = GetRadioComp();
        if (radioComp != null)
        {
            // 如果正在打字，不显示最后一条消息（因为它正在被特效显示）
            int messageCount = isTyping ? 0: radioComp.MessageHistory.Count;
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

        float buttonX = 10f;
        float buttonWidth = 140f;
        float buttonHeight = 35f;

        // 交易信号按钮
        var radioComponent = GetRadioComponent();
        string tradeButtonText = "交易信号";
        bool canClickTrade = true;

        if (radioComponent != null)
        {
            if (!tradeInProgress && radioComponent.IsWaitingForTrade)
            {
                tradeButtonText = $"等待交易... ({radioComponent.GetRemainingTradeTime()})";
                canClickTrade = false;
            }
            else if (tradeReady)
            {
                tradeButtonText = "开始交易";
                canClickTrade = true;
            }
            else if (!radioComponent.canTrade)
            {
                tradeButtonText = "交易冷却中";
                canClickTrade = false;
            }
            else
            {
                tradeButtonText = "交易信号";
                canClickTrade = true;
            }
        }

        if (Widgets.ButtonText(new Rect(buttonX, buttonArea.y + 12f, buttonWidth, buttonHeight), tradeButtonText))
        {
            if (canClickTrade)
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
        buttonX += buttonWidth + 15f;

        // 扫描信号按钮
        if (Widgets.ButtonText(new Rect(buttonX, buttonArea.y + 12f, buttonWidth, buttonHeight), "扫描地图"))
        {
            // 触发扫描相关对话事件
            RKU_DialogueManager.TriggerDialogueEvents(this, "scan");
            AddMessage("开始扫描信号...");
        }
        buttonX += buttonWidth + 15f;

        // 紧急呼叫按钮
        if (Widgets.ButtonText(new Rect(buttonX, buttonArea.y + 12f, buttonWidth, buttonHeight), "求救呼叫"))
        {
            // 触发紧急呼叫相关对话事件
            RKU_DialogueManager.ExecuteDialogueEvent(DefDatabase<RKU_DialogueEventDef>.GetNamed("RKU_EmergencyCall"),this);
            AddMessage("紧急呼叫已发送!");
        }
        buttonX += buttonWidth + 15f;

        // 添加自定义关闭按钮
        if (Widgets.ButtonText(new Rect(inRect.width - 80f, buttonArea.y + 12f, 70f, buttonHeight), "关闭"))
        {
            this.Close();
        }
    }
    #endregion

    #region 交易相关方法
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

        // 交易准备完成 - 只设置准备状态，不进入冷却
        if (comp.isWaitingForTrade && currentTick - comp.tradeStartTick >= comp.currentTradeDelayTicks)
        {
            comp.isWaitingForTrade = false;
            tradeReady = true;
        }
    }

    private void StartTradeSignal()
    {
        var radioComponent = GetRadioComponent();
        if (radioComponent == null || !radioComponent.CanTradeNow) return;

        radioComponent.StartTradeSignal();
        tradeReady = false;

        AddMessage("已收到信号，我们正在备货！");

        Messages.Message("交易信号已发送，等待地下运输队到达...", MessageTypeDefOf.PositiveEvent);
    }

    private void OpenTradeWindow()
    {
        tradeInProgress = true;
        hasTraded = false;
        // 生成交易货物
        this.TraderKind.stockGenerators.ForEach(o => traderGoods.AddRange(o.GenerateThings(radio.Map.Tile)));
        AddMessage("来看看我们准备了什么好东西！                                                                        ",
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
                    Messages.Message("没有可用的殖民者进行交易。", MessageTypeDefOf.RejectInput);
                }
            });
    }

    public void OnTradeReady()
    {
        tradeReady = true;
        AddMessage("货物已准备完毕，可以开始交易了！");
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
        if (string.IsNullOrEmpty(message))
        {
            curY += 25f;
            return;
        }

        // 按换行符分割消息
        string[] lines = message.Split('\n');
        float lineHeight = 20f;

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
        Widgets.Label(new Rect(statusRect.x + 5f, statusRect.y + 5f, statusRect.width - 10f, 20f), "电台状态:");
        Widgets.Label(new Rect(statusRect.x + 5f, statusRect.y + 30f, statusRect.width - 10f, 20f), $"● {RadioStatus}");

        string researchText = "研究进度: 0/100";
        if (radioComponent != null)
        {
            researchText = $"研究进度: {radioComponent.researchProgress}/{RKU_RadioGameComponent.RESEARCH_PROGRESS_MAX}";
        }
        Widgets.Label(new Rect(statusRect.x + 5f, statusRect.y + 105f, statusRect.width - 10f, 20f), $"● {researchText}");
        Widgets.Label(new Rect(statusRect.x + 5f, statusRect.y + 55f, statusRect.width - 10f, 20f), $"● {SignalQuality}");
        Widgets.Label(new Rect(statusRect.x + 5f, statusRect.y + 80f, statusRect.width - 10f, 20f), $"● {PowerStatus}");

        string rationText = "阵营关系: 0";
        if (GetRadioComponent() != null)
        {
            rationText = $"阵营关系: {radioComponent.ralationshipGrade}";
        }
        Widgets.Label(new Rect(statusRect.x + 5f, statusRect.y + 130f, statusRect.width - 10f, 20f), $"● {rationText}");

        // 添加交易状态信息
        if (radioComponent != null && !radioComponent.CanTradeNow)
        {
            int remainingDays = radioComponent.GetRemainingCooldownDays();
            Widgets.Label(new Rect(statusRect.x + 5f, statusRect.y + 180f, statusRect.width - 10f, 20f), $"交易冷却: {remainingDays}天");
        }

        Widgets.Label(new Rect(statusRect.x + 5f, statusRect.y + 155f, statusRect.width - 10f, 20f), $"地图位置: {RadioPosition}");

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
        Log.Warning("rua");
        if (pendingCargo.Count == 0) return null;
        IntVec3 launchSpot = Utils.FindLaunchSpot(radio.Map);
        if (launchSpot == IntVec3.Invalid) return null;
        IntVec3 targetSpot = Utils.FindTargetSpot(radio.Map);
        if (targetSpot == IntVec3.Invalid) return null;
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
                GenSpawn.Spawn(cargoPodBullet, launchSpot, radio.Map);
                cargoPodBullet.Launch(null, new LocalTargetInfo(targetSpot), new LocalTargetInfo(targetSpot), ProjectileHitFlags.None);
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
            ChoiceLetter choiceLetter = LetterMaker.MakeLetter("货物抵达".Translate(), "你订购的货物抵达了", LetterDefOf.PositiveEvent);
            RKU_DrillingCargoPodBullet pod = null;
            LongEventHandler.QueueLongEvent(() =>
            {
                pod = SendCargoPod();
                Find.LetterStack.ReceiveLetter(choiceLetter.Label, choiceLetter.Text, choiceLetter.def, lookTargets: pod);
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