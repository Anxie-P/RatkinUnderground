using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

namespace RatkinUnderground
{
    public class RKU_TradingPost : Building, ITrader
    {
        private List<Thing> traderGoods = new List<Thing>();

        public TraderKindDef TraderKind => DefDatabase<TraderKindDef>.GetNamed("RKU_TradingPost");

        public IEnumerable<Thing> Goods => traderGoods;

        public int RandomPriceFactorSeed => Find.TickManager.TicksGame;

        public string TraderName => "游击队地下据点交易所";

        public bool CanTradeNow => true;

        public float TradePriceImprovementOffsetForPlayer => 0f;

        public TradeCurrency TradeCurrency => this.TraderKind.tradeCurrency;

        public IEnumerable<Thing> ColonyThingsWillingToBuy(Pawn playerNegotiator)
        {
            IEnumerable<Thing> enumerable = from x in this.Map.listerThings.AllThings
                                            where (x is Pawn p && p.Faction == Faction.OfPlayer) ||
                                                  (x.def.category == ThingCategory.Item &&
                                                   !x.IsForbidden(playerNegotiator) &&
                                                   !x.Position.Fogged(x.Map))
                                            select x;
            foreach (Thing thing in this.Map.listerThings.AllThings)
            {
                if (thing.def.IsProcessedFood &&
                    !thing.IsForbidden(playerNegotiator) &&
                    !thing.Position.Fogged(this.Map))
                {
                    yield return thing;
                }
            }

            foreach (Thing thing in enumerable)
            {
                yield return thing;
            }
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            foreach (var gen in TraderKind.stockGenerators)
            {
                foreach (var thing in gen.GenerateThings(this.Map.Tile))
                {
                    if (thing.def.tradeability != Tradeability.None && thing.MarketValue > 0f)
                    {
                        traderGoods.Add(thing);
                    }
                    else
                    {
                        thing.Destroy();
                    }
                }
            }
        }

        public void GiveSoldThingToPlayer(Thing toGive, int countToGive, Pawn playerNegotiator)
        {
            if (toGive == null || countToGive <= 0) return;

            Thing given = null;

            if (toGive.stackCount > countToGive)
            {
                given = toGive.SplitOff(countToGive);
            }
            else
            {
                given = toGive;
                traderGoods.Remove(toGive);
            }

            if (playerNegotiator?.inventory != null)
            {
                if (!playerNegotiator.inventory.innerContainer.TryAdd(given, true))
                {
                    Log.Warning($"无法将 {given.Label} 添加到 {playerNegotiator.LabelShort} 的库存，物品被销毁。");
                    given.Destroy();
                }
            }
            else
            {
                given.Destroy();
            }
        }

        public void GiveSoldThingToTrader(Thing toGive, int countToGive, Pawn playerNegotiator)
        {
            if (toGive == null || countToGive <= 0) return;

            Thing taken;
            if (toGive.stackCount > countToGive)
            {
                taken = toGive.SplitOff(countToGive);
            }
            else
            {
                taken = toGive;
            }

            if (taken.Spawned)
                taken.DeSpawn();

            traderGoods.Add(taken);
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn myPawn)
        {
            foreach (var opt in base.GetFloatMenuOptions(myPawn))
                yield return opt;

            if (myPawn == null) yield break;
            if (myPawn.Faction != Faction.OfPlayer) yield break;

            if (!this.Spawned || this.Map == null || myPawn.Map != this.Map) yield break;

            if (!myPawn.CanReach(this, PathEndMode.Touch, Danger.Deadly))
            {
                string label = "不可交易（超出范围）";
                yield return new FloatMenuOption(label, null);
                yield break;
            }

            // 否则添加正常的“交易”选项：下 Job 让 pawn 去与该交易站交互（JobDriver_TradeWithPawn 会在抵达时打开交易 UI）
            yield return new FloatMenuOption("交易", delegate
            {
                try
                {
                    // 发一个 TradeWithPawn 的 Job，TargetA 指向本建筑
                    var job = JobMaker.MakeJob(DefOfs.RKU_UseTradingPost, this);
                    myPawn.jobs.TryTakeOrderedJob(job);
                }
                catch (Exception e)
                {
                    Log.Warning($"[RKU] 执行交易job失败 {e}");
                }
            });
        }
    }
}
