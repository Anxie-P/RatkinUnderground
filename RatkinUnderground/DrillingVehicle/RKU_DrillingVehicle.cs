using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Tilemaps;
using Verse;
using Verse.AI;
using static UnityEngine.GraphicsBuffer;
using UnityEngine;

namespace RatkinUnderground
{
    public class RKU_DrillingVehicle : Building, IThingHolder
    {
        public ThingOwner<Pawn> passengers;
        public RKU_DrillingVehicle()
        {
            passengers = new ThingOwner<Pawn>(this);
        }

        #region 乘客相关
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (passengers == null)
            {
                passengers = new ThingOwner<Pawn>(this);
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

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            if (outChildren == null)
            {
                return;
            }
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.LoadingVars && passengers == null)
            {
                passengers = new ThingOwner<Pawn>(this);
            }
            Scribe_Deep.Look(ref passengers, "passengers", this);
        }

        public bool CanAcceptPassenger(Pawn pawn)
        {
            if (passengers == null)
            {
                passengers = new ThingOwner<Pawn>(this);
            }
            return passengers.CanAcceptAnyOf(pawn);
        }

        public void AddPassenger(Pawn pawn)
        {
            if (passengers == null)
            {
                passengers = new ThingOwner<Pawn>(this);
            }
            passengers.TryAddOrTransfer(pawn);
        }

        public void RemovePassenger(Pawn pawn)
        {
            if (passengers == null)
            {
                passengers = new ThingOwner<Pawn>(this);
            }
            passengers.Remove(pawn);
        }

        public bool ContainsPassenger(Pawn pawn)
        {
            if (passengers == null)
            {
                passengers = new ThingOwner<Pawn>(this);
            }
            return passengers.Contains(pawn);
        }

        #endregion

        public override IEnumerable<Gizmo> GetGizmos()
        {
            #region 地图内移动
            if (passengers.Count > 0)
            {
                Command_Target command_ChooseTargetInMap = new()
                {
                    defaultLabel = "RKU.Move".Translate(),
                    hotKey = KeyBindingDefOf.Misc1,
                    icon = Resources.dig,
                    targetingParams = new TargetingParameters
                    {
                        canTargetLocations = true,
                    },
                    action = delegate (LocalTargetInfo target)
                    {
                        if (this.HitPoints <= 10)
                        {
                            Messages.Message($"钻机耐久过低，无法使用！", MessageTypeDefOf.NegativeEvent);
                            return;
                        }
                        RKU_DrilingBullet projectile = (RKU_DrilingBullet)ThingMaker.MakeThing(DefOfs.RKU_DrillingVehicleBullet);
                        GenSpawn.Spawn(projectile, Position, Map);

                        LocalTargetInfo localTargetInfo = target;
                        if (target.Cell.x > Position.x)
                        {
                            localTargetInfo = new LocalTargetInfo(new IntVec3(target.Cell.x - 1, target.Cell.y, target.Cell.z));
                        }
                        projectile.Launch(this, target, localTargetInfo, ProjectileHitFlags.None, false);
                        projectile.vehicle = this;
                        DeSpawn();
                    }
                };
                yield return command_ChooseTargetInMap;
            }
            #endregion

            #region 载员管理
            Command_Action command_ManagePassengers = new()
            {
                defaultLabel = "RKU.ManagePassengers".Translate(),
                hotKey = KeyBindingDefOf.Misc2,
                icon = Resources.inner,
                action = () =>
                {
                    Find.WindowStack.Add(new Dialog_ManagePassengers(this));
                }
            };
            yield return command_ManagePassengers;
            #endregion

            #region 钻地！
            if (passengers.Count > 0)
            {
                Command_Action command_AddGoodWill = new()
                {
                    defaultLabel = "RKU.Drill".Translate(),
                    hotKey = KeyBindingDefOf.Misc1,
                    icon = Resources.dig,
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
                        // 转移载员
                        List<Pawn> passengersToTransfer = new List<Pawn>(passengers);
                        foreach (Pawn passenger in passengersToTransfer)
                        {
                            if (passenger != null && !passenger.Destroyed)
                            {
                                passengers.Remove(passenger);
                                if (!passenger.IsWorldPawn())
                                {
                                    Find.WorldPawns.PassToWorld(passenger, PawnDiscardDecideMode.KeepForever);
                                }
                                vehicleOnMap.pawns.TryAddOrTransfer(passenger);
                                passenger.SetFaction(Faction.OfPlayer);
                            }
                        }

                        Find.WorldObjects.Add(vehicleOnMap);
                        this.DeSpawn();
                    }
                };
                yield return command_AddGoodWill;
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

            // 检查是否有任何选中的 pawn 可以到达车辆
            if (selPawns.Any(p => p.CanReach(this, PathEndMode.Touch, Danger.Deadly)))
            {
                yield return new FloatMenuOption("RKU.EnterVehicle".Translate(), () =>
                {
                    foreach (Pawn pawn in selPawns)
                    {
                        Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("RKU_EnterDrillingVehicle"), this);
                        pawn.jobs.TryTakeOrderedJob(job);
                    }
                });
            }
        }
        #endregion
    }
}
