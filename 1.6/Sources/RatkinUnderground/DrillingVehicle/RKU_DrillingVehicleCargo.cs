using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RatkinUnderground
{
    public class RKU_DrillingVehicleCargo : RKU_DrillingVehicle, IThingHolder
    {
        private const int MaxPassengers = 2;
        private static Dictionary<string, List<Thing>> cargoStorage = new Dictionary<string, List<Thing>>();
        private static bool cargoStorageSaved = false;

        public int enterPawns = 0;

        public int maxPassengers
        {
            get { return MaxPassengers; }
        }

        public RKU_DrillingVehicleCargo()
        {
            passengers = new ThingOwner<Pawn>(this);
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            // 返回乘客列表，pawn应该进入乘客列表而不是货物容器
            return base.GetDirectlyHeldThings();
        }

        public ThingOwner GetCargoContainer()
        {
            return this.GetComp<CompTransporter>().innerContainer;
        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        public ThingOwner GetParentHolder()
        {
            return null;
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                if (gizmo is Command_Action command && command.defaultLabel == "RKU.Drill".Translate())
                {
                    Command_Action modifiedCommand = new Command_Action
                    {
                        defaultLabel = command.defaultLabel,
                        defaultDesc = command.defaultDesc,
                        icon = command.icon,
                        action = () =>
                        {
                            command.action();
                        }
                    };
                    yield return modifiedCommand;
                }
                else
                {
                    yield return gizmo;
                }
            }

            CompTransporter compTransporter = this.GetComp<CompTransporter>();
            yield return new Command_Action
            {
                defaultLabel = "CommandLoadTransporter".Translate(),
                defaultDesc = "CommandLoadTransporterDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/LoadTransporter"),
                action = () =>
                {
                    Find.WindowStack.Add(new Dialog_LoadDrillingCargo(this, compTransporter));
                }
            };
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            
            ThingOwner container = GetCargoContainer();
            
            if (map == null || !Position.IsValid)
            {
                return;
            }

            if (cargoStorage == null)
            {
                cargoStorage = new Dictionary<string, List<Thing>>();
            }

            string cargoKey = $"{map.uniqueID}_{Position.x}_{Position.y}_{Position.z}";
            if (cargoStorage.TryGetValue(cargoKey, out List<Thing> savedCargo))
            {
                if (container != null && savedCargo != null)
                {
                    foreach (Thing savedItem in savedCargo)
                    {
                        if (savedItem != null && !savedItem.Destroyed)
                        {
                            container.TryAddOrTransfer(savedItem);
                        }
                    }
                }
                cargoStorage.Remove(cargoKey);
            }
        }

        public void PrepareCargoForDrilling()
        {
            ThingOwner container = GetCargoContainer();
            if (container != null && container.Count > 0)
            {
                // 保存货物到静态存储中，使用地图ID+位置作为复合键
                string cargoKey = $"{Map.uniqueID}_{Position.x}_{Position.y}_{Position.z}";
                List<Thing> cargoToSave = new List<Thing>(container);

                cargoStorage[cargoKey] = cargoToSave;
                container.Clear();
            }
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            ThingOwner cargoContainer = GetCargoContainer();
            if (cargoContainer != null && cargoContainer.Count > 0)
            {
                cargoContainer.Clear();
            }
            base.DeSpawn(mode);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                if (!cargoStorageSaved)
                {
                    Scribe_Collections.Look(ref cargoStorage, "cargoStorage", LookMode.Value, LookMode.Deep);
                    cargoStorageSaved = true;
                }
            }
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                Scribe_Collections.Look(ref cargoStorage, "cargoStorage", LookMode.Value, LookMode.Deep);
                cargoStorageSaved = false;
            }
            Scribe_Values.Look(ref enterPawns, "enterPawns", 0);
        }

        private class Dialog_LoadDrillingCargo : Window
        {
            private RKU_DrillingVehicleCargo vehicle;
            private CompTransporter compTransporter;
            private List<TransferableOneWay> transferables;
            private TransferableOneWayWidget itemsTransfer;
            private float lastMassFlashTime = -9999f;
            private bool massUsageDirty = true;
            private float cachedMassUsage;
            private const float TitleRectHeight = 35f;
            private const float BottomAreaHeight = 55f;
            private readonly Vector2 BottomButtonSize = new Vector2(160f, 40f);
            private float MaxCargoMass => compTransporter?.Props?.massCapacity ?? 1000f;

            public override Vector2 InitialSize => new Vector2(1024f, UI.screenHeight);

            protected override float Margin => 0f;

            private float MassCapacity => MaxCargoMass;

            private float MassUsage
            {
                get
                {
                    if (massUsageDirty)
                    {
                        massUsageDirty = false;
                        cachedMassUsage = CollectionsMassCalculator.MassUsageTransferables(transferables, IgnorePawnsInventoryMode.IgnoreIfAssignedToUnload, false);
                    }
                    return cachedMassUsage;
                }
            }

            public Dialog_LoadDrillingCargo(RKU_DrillingVehicleCargo vehicle, CompTransporter compTransporter)
            {
                this.vehicle = vehicle;
                this.compTransporter = compTransporter;
                forcePause = true;
                absorbInputAroundWindow = true;
            }

            public override void PostOpen()
            {
                base.PostOpen();
                CalculateAndRecacheTransferables();
            }

            public override void DoWindowContents(Rect inRect)
            {
                Rect rect = new Rect(0f, 0f, inRect.width, 35f);
                Text.Font = GameFont.Medium;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(rect, "CommandLoadTransporter".Translate());
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;

                // 显示重量信息
                CaravanUIUtility.DrawCaravanInfo(new CaravanUIUtility.CaravanInfo(MassUsage, MassCapacity, "", 0f, "", default((float days, float tillRot)), default((ThingDef food, float perDay)), "", 0f, "", MassUsage, MassCapacity, ""), null, vehicle.Map.Tile, null, lastMassFlashTime, new Rect(12f, 35f, inRect.width - 24f, 40f), lerpMassColor: false);
                inRect.yMin += 52f;

                inRect.yMin += 67f;
                Widgets.DrawMenuSection(inRect);
                inRect = inRect.ContractedBy(17f);
                Widgets.BeginGroup(inRect);
                Rect rect2 = inRect.AtZero();
                DoBottomButtons(rect2);
                Rect inRect2 = rect2;
                inRect2.yMax -= 59f;
                bool anythingChanged = false;
                itemsTransfer.OnGUI(inRect2, out anythingChanged);
                if (anythingChanged)
                {
                    CountToTransferChanged();
                }
                Widgets.EndGroup();
            }

            public override bool CausesMessageBackground()
            {
                return true;
            }

            private void AddToTransferables(Thing t)
            {
                TransferableOneWay transferableOneWay = TransferableUtility.TransferableMatching(t, transferables, TransferAsOneMode.PodsOrCaravanPacking);
                if (transferableOneWay == null)
                {
                    transferableOneWay = new TransferableOneWay();
                    transferables.Add(transferableOneWay);
                }
                if (transferableOneWay.things.Contains(t))
                {
                    Log.Error("Tried to add the same thing twice to TransferableOneWay: " + t);
                    return;
                }
                transferableOneWay.things.Add(t);
            }

            private void DoBottomButtons(Rect rect)
            {
                Rect rect2 = new Rect(rect.width / 2f - BottomButtonSize.x / 2f, rect.height - 55f, BottomButtonSize.x, BottomButtonSize.y);
                if (Widgets.ButtonText(rect2, "AcceptButton".Translate()))
                {
                    if (TryAccept())
                    {
                        Close(doCloseSound: false);
                    }
                }

                if (Widgets.ButtonText(new Rect(rect2.x - 10f - BottomButtonSize.x, rect2.y, BottomButtonSize.x, BottomButtonSize.y), "ResetButton".Translate()))
                {
                    CalculateAndRecacheTransferables();
                }

                if (Widgets.ButtonText(new Rect(rect2.xMax + 10f, rect2.y, BottomButtonSize.x, BottomButtonSize.y), "CancelButton".Translate()))
                {
                    Close();
                }
            }

            private void CalculateAndRecacheTransferables()
            {
                transferables = new List<TransferableOneWay>();
                AddItemsToTransferables();
                itemsTransfer = new TransferableOneWayWidget(transferables.Where((TransferableOneWay x) => x.ThingDef.category != ThingCategory.Pawn), null, null, "FormCaravanColonyThingCountTip".Translate(), drawMass: true, IgnorePawnsInventoryMode.IgnoreIfAssignedToUnload, includePawnsMassInMassUsage: false, () => MassCapacity - MassUsage, 0f, ignoreSpawnedCorpseGearAndInventoryMass: false, vehicle.Map.Tile, drawMarketValue: true, drawEquippedWeapon: false, drawItemNutrition: true, drawForagedFoodPerDay: false, drawDaysUntilRot: true);
                CountToTransferChanged();
            }

            private bool TryAccept()
            {
                if (!CheckForErrors())
                {
                    return false;
                }

                // 处理转移
                foreach (TransferableOneWay transferable in transferables)
                {
                    if (!transferable.HasAnyThing)
                        continue;

                    int targetCount = transferable.CountToTransfer;
                    ThingOwner cargo = compTransporter.innerContainer;
                    
                    // 统计当前容器中的数量
                    int currentCount = 0;
                    foreach (Thing thing in cargo)
                    {
                        if (thing.def == transferable.ThingDef)
                        {
                            currentCount += thing.stackCount;
                        }
                    }

                    int difference = targetCount - currentCount;

                    if (difference > 0)
                    {
                        // 需要装载
                        int remainingToLoad = difference;
                        foreach (Thing thing in transferable.things)
                        {
                            if (remainingToLoad <= 0)
                                break;

                            if (!thing.Spawned)
                                continue;

                            int amountToLoad = Mathf.Min(remainingToLoad, thing.stackCount);
                            if (amountToLoad > 0)
                            {
                                Thing splitThing = thing.SplitOff(amountToLoad);
                                if (splitThing != null)
                                {
                                    cargo.TryAddOrTransfer(splitThing, canMergeWithExistingStacks: true);
                                    remainingToLoad -= amountToLoad;
                                }
                            }
                        }
                    }
                    else if (difference < 0)
                    {
                        // 需要卸载
                        int remainingToUnload = -difference;
                        List<Thing> thingsToUnload = new List<Thing>();
                        foreach (Thing thing in cargo)
                        {
                            if (thing.def == transferable.ThingDef)
                            {
                                thingsToUnload.Add(thing);
                            }
                        }

                        foreach (Thing thing in thingsToUnload)
                        {
                            if (remainingToUnload <= 0)
                                break;

                            int amountToUnload = Mathf.Min(remainingToUnload, thing.stackCount);
                            if (amountToUnload > 0)
                            {
                                Thing splitThing = thing.SplitOff(amountToUnload);
                                if (splitThing != null)
                                {
                                    GenPlace.TryPlaceThing(splitThing, vehicle.Position, vehicle.Map, ThingPlaceMode.Near);
                                    remainingToUnload -= amountToUnload;
                                }
                            }
                        }
                    }
                }

                return true;
            }

            private bool CheckForErrors()
            {
                if (MassUsage > MassCapacity)
                {
                    FlashMass();
                    Messages.Message("TransportersMassUsageExceedsMassCapacity".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                    return false;
                }

                return true;
            }

            private void AddItemsToTransferables()
            {
                // 先统计容器内物品的数量
                Dictionary<ThingDef, int> cargoItemCounts = new Dictionary<ThingDef, int>();
                ThingOwner cargo = compTransporter.innerContainer;
                if (cargo != null)
                {
                    foreach (Thing item in cargo)
                    {
                        if (!cargoItemCounts.ContainsKey(item.def))
                        {
                            cargoItemCounts[item.def] = 0;
                        }
                        cargoItemCounts[item.def] += item.stackCount;
                    }
                }

                // 添加地图上的物品到transferables
                foreach (Thing item in vehicle.Map.listerThings.AllThings.Where(t => t.def.category == ThingCategory.Item && t.Spawned && t.Position.InBounds(vehicle.Map)))
                {
                    AddToTransferables(item);
                }

                // 为每个transferable设置初始countToTransfer为容器内对应物品的数量
                foreach (TransferableOneWay transferable in transferables)
                {
                    if (transferable.HasAnyThing && cargoItemCounts.TryGetValue(transferable.ThingDef, out int cargoCount))
                    {
                        // 使用反射设置countToTransfer，因为setter是protected的
                        var countToTransferField = typeof(TransferableOneWay).GetField("countToTransfer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (countToTransferField != null)
                        {
                            countToTransferField.SetValue(transferable, cargoCount);
                        }
                        transferable.EditBuffer = cargoCount.ToStringCached();
                    }
                }

                // 对于容器内有但地图上没有的物品，创建一个空的transferable来显示
                foreach (var kvp in cargoItemCounts)
                {
                    var existingTransferable = transferables.FirstOrDefault(t => t.ThingDef == kvp.Key);
                    if (existingTransferable == null)
                    {
                        // 地图上没有这个物品，创建一个空的transferable来显示容器内数量
                        var transferable = new TransferableOneWay();
                        var virtualThing = ThingMaker.MakeThing(kvp.Key);
                        virtualThing.stackCount = 0; // 虚拟对象，stackCount设为0
                        transferable.things.Add(virtualThing);
                        transferables.Add(transferable);
                        
                        var countToTransferField = typeof(TransferableOneWay).GetField("countToTransfer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (countToTransferField != null)
                        {
                            countToTransferField.SetValue(transferable, kvp.Value);
                        }
                        transferable.EditBuffer = kvp.Value.ToStringCached();
                    }
                }
            }

            private void FlashMass()
            {
                lastMassFlashTime = Time.time;
            }

            private void CountToTransferChanged()
            {
                massUsageDirty = true;
            }
        }
    }
}
