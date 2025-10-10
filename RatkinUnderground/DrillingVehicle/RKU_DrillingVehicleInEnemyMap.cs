using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RatkinUnderground
{
    public class RKU_DrillingVehicleInEnemyMap : Building, IThingHolder
    {
        private ThingOwner<Pawn> passengers;
        private ThingOwner<Thing> cargoHolder; // 货物存储
        public ThingDef originalVehicleDef; // 保存原始钻机类型
        public List<Thing> cargo = new List<Thing>(); // 保存货物

        public RKU_DrillingVehicleInEnemyMap()
        {
            passengers = new ThingOwner<Pawn>(this);
            cargoHolder = new ThingOwner<Thing>(this);
        }

        #region 乘客相关
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (passengers == null)
            {
                passengers = new ThingOwner<Pawn>(this);
            }
            if (cargoHolder == null)
            {
                cargoHolder = new ThingOwner<Thing>(this);
            }

            if (originalVehicleDef == DefOfs.RKU_DrillingVehicleCargo && cargo != null && cargo.Count > 0)
            {
                foreach (Thing thing in cargo)
                {
                    if (thing != null && !thing.Destroyed)
                    {
                        cargoHolder.TryAddOrTransfer(thing);
                    }
                }
                cargo.Clear();
            }
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            if (passengers == null)
            {
                passengers = new ThingOwner<Pawn>(this);
            }
            return passengers;
        }

        public ThingOwner GetCargoHolder()
        {
            if (cargoHolder == null)
            {
                cargoHolder = new ThingOwner<Thing>(this);
            }
            return cargoHolder;
        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            if (outChildren == null)
            {
                Log.Error("outChildren is null in GetChildHolders");
                return;
            }
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        public override void Tick()
        {
            base.Tick();
            if (passengers != null)
            {
                passengers.ThingOwnerTick();
            }
            if (cargoHolder != null)
            {
                cargoHolder.ThingOwnerTick();
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.LoadingVars && passengers == null)
            {
                passengers = new ThingOwner<Pawn>(this);
            }
            if (Scribe.mode == LoadSaveMode.LoadingVars && cargoHolder == null)
            {
                cargoHolder = new ThingOwner<Thing>(this);
            }
            Scribe_Deep.Look(ref passengers, "passengers", this);
            Scribe_Deep.Look(ref cargoHolder, "cargoHolder", this);
            Scribe_Defs.Look(ref originalVehicleDef, "originalVehicleDef");
            Scribe_Collections.Look(ref cargo, "cargo", LookMode.Deep);
        }

        public bool CanAcceptPassenger(Pawn pawn)
        {
            if (passengers == null)
            {
                Log.Error("passengers is null in CanAcceptPassenger");
                passengers = new ThingOwner<Pawn>(this);
            }
            return passengers.CanAcceptAnyOf(pawn);
        }

        public void AddPassenger(Pawn pawn)
        {
            if (passengers == null)
            {
                Log.Error("passengers is null in AddPassenger");
                passengers = new ThingOwner<Pawn>(this);
            }
            passengers.TryAddOrTransfer(pawn);
        }

        public void RemovePassenger(Pawn pawn)
        {
            if (passengers == null)
            {
                Log.Error("passengers is null in RemovePassenger");
                passengers = new ThingOwner<Pawn>(this);
            }
            passengers.Remove(pawn);
        }

        public bool ContainsPassenger(Pawn pawn)
        {
            if (passengers == null)
            {
                Log.Error("passengers is null in ContainsPassenger");
                passengers = new ThingOwner<Pawn>(this);
            }
            return passengers.Contains(pawn);
        }
        #endregion

        public override IEnumerable<Gizmo> GetGizmos()
        {
            // 如果是非玩家派系，不会显示任何gizmo
            if (!Faction.IsPlayer) yield break;

            #region 载员管理
            Command_Action command_ManagePassengers = new()
            {
                defaultLabel = "RKU.ManagePassengers".Translate(),
                hotKey = KeyBindingDefOf.Misc2,
                icon = ContentFinder<Texture2D>.Get("UI/Commands/AssignOwner"),
                action = () =>
                {
                    Find.WindowStack.Add(new Dialog_ManagePassengers(this));
                }
            };
            yield return command_ManagePassengers;
            #endregion

            #region 返回
            if (passengers.Count > 0)
            {
                Command_Action command_AddGoodWill = new()
                {
                    defaultLabel = "RKU.Drill".Translate(),
                    hotKey = KeyBindingDefOf.Misc1,
                    icon = Resources.digIn,
                    action = () =>
                    {
                        if (this.HitPoints <= 10)
                        {
                            Messages.Message($"钻机耐久过低，无法使用！", MessageTypeDefOf.NegativeEvent);
                            return;
                        }
                        // 点击时爆发烟尘效果
                        for (int i = 0; i < 15; i++)
                        {
                            IntVec3 offset = new IntVec3(Rand.Range(-1, 2), 0, Rand.Range(-1, 2));
                            FleckMaker.ThrowDustPuffThick((Position + offset).ToVector3(), Map, 2f, Color.gray);
                        }

                        // 在钻地机位置留下持续的灰尘效果
                        if (Map != null && Position.IsValid)
                        {
                            RKU_DigDust dust = (RKU_DigDust)ThingMaker.MakeThing(ThingDef.Named("RKU_DigDust"));
                            if (dust != null)
                            {
                                // 计算钻地机中心位置（1*2的建筑）
                                Vector3 centerPos = Position.ToVector3() + new Vector3(0.5f, 0, 0.5f);
                                dust.exactPosition = centerPos;
                                GenSpawn.Spawn(dust, Position, Map);
                            }
                        }

                        //出地图
                        RKU_DrillingVehicleOnMap vehicleOnMap = (RKU_DrillingVehicleOnMap)WorldObjectMaker.MakeWorldObject(DefOfs.TravelingDrillingVehicle);
                        vehicleOnMap.Tile = base.Map.Tile;
                        vehicleOnMap.SetFaction(Faction.OfPlayer);
                        vehicleOnMap.destinationTile = base.Map.Tile;
                        vehicleOnMap.hitPoints = this.HitPoints;
                        vehicleOnMap.originalVehicleDefName = originalVehicleDef?.defName ?? "RKU_DrillingVehicle";  // 保存原始钻机类型
                        Find.WorldObjects.Add(vehicleOnMap);

                        // 保存货物
                        if (originalVehicleDef == DefOfs.RKU_DrillingVehicleCargo && cargoHolder != null && cargoHolder.Count > 0)
                        {
                            vehicleOnMap.cargo = new List<Thing>();
                            foreach (Thing thing in cargoHolder)
                            {
                                if (thing != null && !thing.Destroyed)
                                {
                                    Thing copiedThing = ThingMaker.MakeThing(thing.def, thing.Stuff);
                                    copiedThing.stackCount = thing.stackCount;
                                    copiedThing.TryGetComp<CompQuality>()?.SetQuality(thing.TryGetComp<CompQuality>()?.Quality ?? QualityCategory.Normal, ArtGenerationContext.Colony);
                                    vehicleOnMap.cargo.Add(copiedThing);
                                }
                            }
                            cargoHolder.Clear();
                        }
                        // 转移载员
                        List<Pawn> passengersToTransfer = new List<Pawn>(passengers);
                        foreach (Pawn passenger in passengersToTransfer)
                        {
                            if (passenger != null && !passenger.Destroyed)
                            {
                                passengers.Remove(passenger);
                                passenger.DeSpawnOrDeselect();
                                typeof(WorldPawns).GetMethod("AddPawn", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.Invoke(Find.World.worldPawns, new object[] { passenger });
                                passenger.SetFaction(Faction.OfPlayer);
                                vehicleOnMap.AddPawn(passenger, false);
                            }
                        }

                        this.DeSpawn();
                    }
                };
                yield return command_AddGoodWill;
            }

            if (Map.mapPawns.AllPawnsSpawned.Any(p => p.IsColonist ) &&
                passengers.Count > 0)
            {
                // 将钻机钻出
                Command_Action drillOut = new Command_Action
                {
                    defaultLabel = "钻出",
                    activateSound = SoundDefOf.Tick_Tiny,
                    icon = ContentFinder<Texture2D>.Get("UI/DigOut"),
                    action = delegate
                    {
                        FleckMaker.Static(Position.ToVector3Shifted(), Map, FleckDefOf.DustPuff, 4f);
                        FleckMaker.Static(Position.ToVector3Shifted() + new Vector3(1, 0, 0), Map, FleckDefOf.DustPuff, 4f);
                        FleckMaker.Static(Position.ToVector3Shifted() + new Vector3(0.5f, 0, 0), Map, FleckDefOf.DustPuff, 4f);
                        if (Rand.Chance(0.2f))
                        {
                            FilthMaker.TryMakeFilth(Position, Map, ThingDefOf.Filth_Dirt);
                        }
                        List<Pawn> passengersToTransfer = new List<Pawn>(passengers);
                        // 使用保存的原始钻机类型
                        ThingDef vehicleDef = originalVehicleDef ?? DefDatabase<ThingDef>.GetNamed("RKU_DrillingVehicle");
                        Thing drillingVehicleThing = ThingMaker.MakeThing(vehicleDef);
                        RKU_DrillingVehicle drillingVehicle = drillingVehicleThing as RKU_DrillingVehicle;
                        if (drillingVehicle == null)
                        {
                            Log.Error($"[RKU] 无法将 {drillingVehicleThing.GetType().Name} 转换为 RKU_DrillingVehicle");
                            return;
                        }
                        foreach (var pawn in passengersToTransfer)
                        {
                            if (pawn != null && !pawn.Destroyed)
                            {
                                passengers.Remove(pawn);
                                drillingVehicle.AddPassenger(pawn);
                                pawn.SetFaction(Faction.OfPlayer);
                            }
                        }
                        drillingVehicle.HitPoints = this.HitPoints;
                        drillingVehicle.SetFaction(Faction.OfPlayer);

                        // 恢复货物
                        if (drillingVehicle is RKU_DrillingVehicleCargo cargoVehicle && cargo != null)
                        {
                            var cargoHolder = cargoVehicle.GetDirectlyHeldThings();
                            foreach (Thing thing in cargo)
                            {
                                cargoHolder.TryAdd(thing);
                            }
                            cargo.Clear();
                        }

                        GenSpawn.Spawn(drillingVehicle, Position, Map);
                    }
                };
                yield return drillOut;

            }

            #endregion
        }

        #region 浮动菜单
        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
        {
            foreach (FloatMenuOption option in base.GetFloatMenuOptions(selPawn))
            {
                yield return option;
            }

            if (selPawn.CanReach(this, PathEndMode.InteractionCell, Danger.Deadly))
            {
                if (passengers.Contains(selPawn))
                {
                    yield return new FloatMenuOption("RKU.ExitVehicle".Translate(), () =>
                    {
                        passengers.Remove(selPawn);
                        GenSpawn.Spawn(selPawn, Position, Map);
                    });
                }
                else
                {
                    yield return new FloatMenuOption("RKU.EnterVehicle".Translate(), () =>
                    {
                        Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("RKU_EnterDrillingVehicle"), this);
                        selPawn.jobs.TryTakeOrderedJob(job);
                    });
                }
            }
        }

        public override IEnumerable<FloatMenuOption> GetMultiSelectFloatMenuOptions(List<Pawn> selPawns)
        {

            foreach (FloatMenuOption option in base.GetMultiSelectFloatMenuOptions(selPawns))
            {
                yield return option;
            }
            {
                string translatedLabel = "RKU.EnterVehicle".Translate();
                // 创建pawn列表的副本，避免闭包捕获问题
                List<Pawn> capturedPawns = new List<Pawn>(selPawns);
                FloatMenuOption option = new FloatMenuOption(translatedLabel, () =>
                {
                    foreach (Pawn pawn in capturedPawns)
                    {
                        JobDef jobDef = DefDatabase<JobDef>.GetNamed("RKU_EnterDrillingVehicle");
                        Job job = JobMaker.MakeJob(jobDef, this);
                        pawn.jobs.StartJob(job, JobCondition.InterruptForced);
                    }
                });

                yield return option;
            }
        }

        #endregion
    }
}