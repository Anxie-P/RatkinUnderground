using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RatkinUnderground
{
    public class RKU_DrillingVehicleCargo : RKU_DrillingVehicle
    {
        private ThingOwner<Thing> cargo;
        private static Dictionary<string, List<Thing>> cargoStorage = new Dictionary<string, List<Thing>>();
        private Dictionary<ThingDef, int> itemsToLoad = new Dictionary<ThingDef, int>();
        private Dictionary<ThingDef, int> itemsToUnload = new Dictionary<ThingDef, int>();

        private static bool cargoStorageSaved = false;

        public int enterPawns = 0;

        public int maxPassengers
            {
                get { return MaxPassengers; }
            }

        public RKU_DrillingVehicleCargo()
        {
            passengers = new ThingOwner<Pawn>(this);
            cargo = new ThingOwner<Thing>(this);
        }

        public new ThingOwner GetDirectlyHeldThings()
        {
            if (cargo == null)
            {
                cargo = new ThingOwner<Thing>(this);
            }
            return cargo;
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

            // 添加货物管理Gizmo
            yield return new Command_Action
            {
                defaultLabel = "装载货物",
                defaultDesc = "从地图上选择货物装载到钻机中",
                icon = ContentFinder<Texture2D>.Get("UI/Commands/LoadTransporter"),
                action = () =>
                {
                    Find.WindowStack.Add(new Dialog_LoadDrillingCargo(this));
                }
            };
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (cargo == null)
            {
                cargo = new ThingOwner<Thing>(this);
            }

            string cargoKey = $"{map.uniqueID}_{Position.x}_{Position.y}_{Position.z}";
            if (cargoStorage.TryGetValue(cargoKey, out List<Thing> savedCargo))
            {
                if (cargo != null)
                {
                    foreach (Thing savedItem in savedCargo)
                    {
                        if (savedItem != null && !savedItem.Destroyed)
                        {
                            cargo.TryAddOrTransfer(savedItem);
                        }
                    }
                }
                cargoStorage.Remove(cargoKey);
            }
        }

        public void PrepareCargoForDrilling()
        {
            if (cargo != null && cargo.Count > 0)
            {
                // 保存货物到静态存储中，使用地图ID+位置作为复合键
                string cargoKey = $"{Map.uniqueID}_{Position.x}_{Position.y}_{Position.z}";
                List<Thing> cargoToSave = new List<Thing>(cargo);

                cargoStorage[cargoKey] = cargoToSave;
                cargo.Clear();
            }
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            base.DeSpawn(mode);
        }

        public override void Tick()
        {
            base.Tick();
            if (cargo != null)
            {
                cargo.ThingOwnerTick();
            }
        }
        public override void ExposeData()
        {
            base.ExposeData();

            if (Scribe.mode == LoadSaveMode.LoadingVars && cargo == null)
            {
                cargo = new ThingOwner<Thing>(this);
            }
            Scribe_Deep.Look(ref cargo, "cargo", this);

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


        public bool HasItemsToLoad()
        {
            return itemsToLoad.Count > 0;
        }

        public bool ShouldLoadItem(ThingDef def)
        {
            return itemsToLoad.ContainsKey(def) && itemsToLoad[def] > 0;
        }

        public int GetLoadAmount(ThingDef def)
        {
            return itemsToLoad.TryGetValue(def, out int amount) ? amount : 0;
        }

        public int GetUnloadAmount(ThingDef def)
        {
            return itemsToUnload.TryGetValue(def, out int amount) ? amount : 0;
        }

        public void UpdateItemsToLoad(List<TransferableOneWay> transferables, Dictionary<ThingDef, int> currentCargoCounts)
        {
            itemsToLoad.Clear();
            itemsToUnload.Clear();
            foreach (TransferableOneWay transferable in transferables)
            {
                if (transferable.HasAnyThing)
                {
                    int targetCount = transferable.CountToTransfer; // 目标数量
                    int currentCount = currentCargoCounts.TryGetValue(transferable.ThingDef, out int count) ? count : 0;
                    int loadAmount = targetCount - currentCount;

                    if (loadAmount > 0)
                    {
                        // 需要装载
                        itemsToLoad[transferable.ThingDef] = loadAmount;
                    }
                    else if (loadAmount < 0)
                    {
                        // 需要卸货
                        itemsToUnload[transferable.ThingDef] = Mathf.Abs(loadAmount);
                    }
                }
            }
        }

        public void ReduceLoadAmount(ThingDef def, int amount)
        {
            if (itemsToLoad.ContainsKey(def))
            {
                itemsToLoad[def] = Mathf.Max(0, itemsToLoad[def] - amount);
                if (itemsToLoad[def] <= 0)
                {
                    itemsToLoad.Remove(def);
                }
            }
        }

        private class Dialog_LoadDrillingCargo : Window
        {
            private RKU_DrillingVehicleCargo vehicle;
            private List<TransferableOneWay> transferables;
            private TransferableOneWayWidget itemsTransfer;
            private float lastMassFlashTime = -9999f;
            private bool massUsageDirty = true;
            private float cachedMassUsage;
            private const float TitleRectHeight = 35f;
            private const float BottomAreaHeight = 55f;
            private readonly Vector2 BottomButtonSize = new Vector2(160f, 40f);
            private const float MaxCargoMass = 1000f;

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

            public Dialog_LoadDrillingCargo(RKU_DrillingVehicleCargo vehicle)
            {
                this.vehicle = vehicle;
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
                Widgets.Label(rect, "钻机货物装载");
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;

                // 显示重量信息
                CaravanUIUtility.DrawCaravanInfo(new CaravanUIUtility.CaravanInfo(MassUsage, MassCapacity, "", 0f, "", default(Pair<float, float>), default(Pair<ThingDef, float>), "", 0f, "", MassUsage, MassCapacity, ""), null, vehicle.Map.Tile, null, lastMassFlashTime, new Rect(12f, 35f, inRect.width - 24f, 40f), lerpMassColor: false);
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
                if (Widgets.ButtonText(rect2, "装载"))
                {
                    if (TryAccept())
                    {
                        Close(doCloseSound: false);
                    }
                }

                if (Widgets.ButtonText(new Rect(rect2.x - 10f - BottomButtonSize.x, rect2.y, BottomButtonSize.x, BottomButtonSize.y), "重置"))
                {
                    CalculateAndRecacheTransferables();
                }

                if (Widgets.ButtonText(new Rect(rect2.xMax + 10f, rect2.y, BottomButtonSize.x, BottomButtonSize.y), "刷新"))
                {
                    CalculateAndRecacheTransferables();
                }

                if (Widgets.ButtonText(new Rect(rect2.xMax + 10f + BottomButtonSize.x + 10f, rect2.y, BottomButtonSize.x, BottomButtonSize.y), "取消"))
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

                // 处理卸货
                foreach (TransferableOneWay transferable in transferables)
                {
                    if (transferable.CountToTransfer < 0)
                    {
                        UnloadCargo(transferable, -transferable.CountToTransfer);
                    }
                }
                // 获取当前钻机内物品数量
                Dictionary<ThingDef, int> currentCargoCounts = new Dictionary<ThingDef, int>();
                ThingOwner cargo = vehicle.GetDirectlyHeldThings();
                if (cargo != null)
                {
                    foreach (Thing thing in cargo)
                    {
                        if (!currentCargoCounts.ContainsKey(thing.def))
                        {
                            currentCargoCounts[thing.def] = 0;
                        }
                        currentCargoCounts[thing.def] += thing.stackCount;
                    }
                }

                // 处理转移
                List<TransferableOneWay> transferablesToProcess = new List<TransferableOneWay>();
                foreach (TransferableOneWay transferable in transferables)
                {
                    if (transferable.HasAnyThing)
                    {
                        int targetCount = transferable.CountToTransfer;
                        int currentCount = currentCargoCounts.TryGetValue(transferable.ThingDef, out int count) ? count : 0;

                        if (targetCount != currentCount)
                        {
                            transferablesToProcess.Add(transferable);
                        }
                    }
                }

                if (transferablesToProcess.Any())
                {
                    vehicle.UpdateItemsToLoad(transferablesToProcess, currentCargoCounts);

                    // 处理卸货
                    bool hasUnloading = transferablesToProcess.Any(t => vehicle.GetUnloadAmount(t.ThingDef) > 0);
                    if (hasUnloading)
                    {
                        UnloadItems(vehicle, transferablesToProcess);
                    }

                    // 处理装载
                    if (transferablesToProcess.Any(t => vehicle.GetLoadAmount(t.ThingDef) > 0))
                    {
                        AssignHaulJobsToColonists();
                    }
                }
                else
                {
                }

                return true;
            }

            private void UnloadItems(RKU_DrillingVehicleCargo vehicle, List<TransferableOneWay> transferablesToProcess)
            {
                ThingOwner cargo = vehicle.GetDirectlyHeldThings();
                if (cargo == null) return;

                foreach (TransferableOneWay transferable in transferablesToProcess)
                {
                    if (transferable.HasAnyThing)
                    {
                        int unloadAmount = vehicle.GetUnloadAmount(transferable.ThingDef);
                        if (unloadAmount > 0)
                        {
                            UnloadCargo(transferable, unloadAmount);
                        }
                    }
                }
            }

            private void UnloadCargo(TransferableOneWay transferable, int amountToUnload)
            {
                if (amountToUnload <= 0) return;

                ThingOwner cargo = vehicle.GetDirectlyHeldThings();
                if (cargo == null) return;

                // 对于空的transferable（钻机内有但地图上没有的物品），直接从cargo中卸货
                List<Thing> thingsToTransfer = new List<Thing>();
                foreach (Thing thing in cargo)
                {
                    if (thing.def == transferable.ThingDef)
                    {
                        thingsToTransfer.Add(thing);
                    }
                }

                TransferableUtility.Transfer(thingsToTransfer, amountToUnload, (thing, holder) =>
                {
                    GenPlace.TryPlaceThing(thing, vehicle.Position, vehicle.Map, ThingPlaceMode.Near);
                });
            }

            private void AssignHaulJobsToColonists()
            {
                // 获取所有可用的殖民者
                List<Pawn> colonists = vehicle.Map.mapPawns.FreeColonistsSpawned;
                if (colonists.Count == 0)
                {
                    Messages.Message("没有可用的殖民者来装载货物", MessageTypeDefOf.RejectInput, historical: false);
                    return;
                }

                // 直接分配装载作业，不使用lord系统
                int totalJobsAssigned = 0;
                foreach (TransferableOneWay transferable in transferables)
                {
                    if (!transferable.HasAnyThing || !vehicle.itemsToLoad.ContainsKey(transferable.ThingDef))
                        continue;

                    int remainingToTransfer = vehicle.itemsToLoad[transferable.ThingDef];

                    foreach (Thing thing in transferable.things)
                    {
                        if (remainingToTransfer <= 0)
                            break;

                        // 只搬运地图上的物品
                        if (!thing.Spawned)
                            continue;

                        // 尝试为这个物品找到合适的殖民者
                        bool jobAssigned = false;

                        // 尝试所有殖民者，直到找到一个可以执行作业的
                        for (int attempt = 0; attempt < colonists.Count && !jobAssigned; attempt++)
                        {
                            Pawn colonist = colonists.Find(c => c.CanReach(thing, Verse.AI.PathEndMode.Touch, Danger.None) &&
                                                              c.CanReach(vehicle, Verse.AI.PathEndMode.Touch, Danger.None));

                            if (colonist != null)
                            {
                                int amountToHaul = Mathf.Min(remainingToTransfer, thing.stackCount);
                                // 创建自定义的装载作业
                                Verse.AI.Job job = new Verse.AI.Job(DefOfs.RKU_LoadDrillingCargo, thing, vehicle);
                                job.count = amountToHaul;

                                if (colonist.jobs.TryTakeOrderedJob(job))
                                {
                                    jobAssigned = true;
                                    totalJobsAssigned++;
                                    remainingToTransfer -= amountToHaul;
                                }
                            }
                        }

                        if (!jobAssigned)
                        {
                            Log.Warning($"[RKU_DrillingVehicle] 无法为物品 {thing.Label} 找到可用的殖民者");
                        }
                    }
                }
            }

            private bool CheckForErrors()
            {
                // 检查是否有实际的转移操作（目标数量 != 当前数量）
                Dictionary<ThingDef, int> cargoItemCounts = new Dictionary<ThingDef, int>();
                ThingOwner cargo = vehicle.GetDirectlyHeldThings();
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

                bool hasAnyTransfer = false;
                foreach (TransferableOneWay transferable in transferables)
                {
                    if (transferable.HasAnyThing)
                    {
                        int targetCount = transferable.CountToTransfer;
                        int currentCount = cargoItemCounts.TryGetValue(transferable.ThingDef, out int count) ? count : 0;

                        if (targetCount != currentCount)
                        {
                            hasAnyTransfer = true;
                            break;
                        }
                    }
                }

                if (!hasAnyTransfer)
                {
                    Messages.Message("没有选择要转移的货物", MessageTypeDefOf.RejectInput, historical: false);
                    return false;
                }

                if (MassUsage > MassCapacity)
                {
                    FlashMass();
                    Messages.Message("货物重量超过钻机容量限制", MessageTypeDefOf.RejectInput, historical: false);
                    return false;
                }

                return true;
            }

            private void AddItemsToTransferables()
            {
                // 先统计钻机内物品的数量
                Dictionary<ThingDef, int> cargoItemCounts = new Dictionary<ThingDef, int>();
                ThingOwner cargo = vehicle.GetDirectlyHeldThings();
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

                // 只添加地图上的物品到transferables
                int mapItemsCount = 0;
                foreach (Thing item in vehicle.Map.listerThings.AllThings.Where(t => t.def.category == ThingCategory.Item && t.Position.InBounds(vehicle.Map)))
                {
                    AddToTransferables(item);
                    mapItemsCount++;
                }

                // 为每个transferable设置初始countToTransfer为钻机内对应物品的数量
                foreach (TransferableOneWay transferable in transferables)
                {
                    if (transferable.HasAnyThing && cargoItemCounts.TryGetValue(transferable.ThingDef, out int cargoCount))
                    {
                        // 使用反射设置countToTransfer，因为setter是protected的
                        typeof(TransferableOneWay).GetField("countToTransfer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(transferable, cargoCount);
                        transferable.EditBuffer = cargoCount.ToStringCached();
                    }
                }
                foreach (var kvp in cargoItemCounts)
                {
                    var existingTransferable = transferables.FirstOrDefault(t => t.ThingDef == kvp.Key);
                    if (existingTransferable == null)
                    {
                        // 地图上没有这个物品，创建一个空的transferable来显示钻机内数量
                        var transferable = new TransferableOneWay();
                        var virtualThing = ThingMaker.MakeThing(kvp.Key);
                        virtualThing.stackCount = 0; // 虚拟对象，stackCount设为0，这样MaxCount就是0
                        transferable.things.Add(virtualThing);
                        transferables.Add(transferable);
                        typeof(TransferableOneWay).GetField("countToTransfer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(transferable, kvp.Value);
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
