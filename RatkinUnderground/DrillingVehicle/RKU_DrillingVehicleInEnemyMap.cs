using Mono.Unix.Native;
using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Noise;

namespace RatkinUnderground
{
    public class RKU_DrillingVehicleInEnemyMap : Building, IThingHolder
    {
        private ThingOwner<Pawn> passengers;

        public RKU_DrillingVehicleInEnemyMap()
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
                Log.Error("passengers is null in GetDirectlyHeldThings");
                passengers = new ThingOwner<Pawn>(this);
            }
            return passengers;
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
                Command_Action command_Return = new()
                {
                    defaultLabel = "RKU.Return".Translate(),
                    hotKey = KeyBindingDefOf.Misc1,
                    icon = Resources.digIn,
                    action = () =>
                    {
                        //返回原地图
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
                yield return command_Return;
            }

            if (!Map.mapPawns.AllPawnsSpawned.Any(p => p.Faction != null && p.Faction.HostileTo(Faction.OfPlayer)) &&
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
                        RKU_DrillingVehicle drillingVehicle = (RKU_DrillingVehicle)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("RKU_DrillingVehicle"));
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