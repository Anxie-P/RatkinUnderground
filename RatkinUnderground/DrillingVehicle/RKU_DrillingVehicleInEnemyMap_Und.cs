using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse.AI;
using Verse;
using RimWorld.Planet;
using Verse.AI.Group;

namespace RatkinUnderground
{
    public class RKU_DrillingVehicleInEnemyMap_Und : Building, IThingHolder
    {
        private ThingOwner<Pawn> passengers;

        int tick = 0;
        bool startLeave = false;
        List<Pawn> guerrillas = new();
        public void StartLeave(bool startLeave) => this.startLeave = startLeave;
        public void SetGuerrillas(List<Pawn> guerrillas) => this.guerrillas = guerrillas;

        public RKU_DrillingVehicleInEnemyMap_Und()
        {
            passengers = new ThingOwner<Pawn>(this);
        }

        protected override void Tick()
        {
            base.Tick();

            tick++;
            if (tick < 60) return;
            tick = 0;

            if(!startLeave) return;
            foreach(var pawn in guerrillas)
            {
                if (!(pawn.Dead || pawn.Downed) && !ContainsPassenger(pawn)) return;
            }

            Messages.Message("地鼠们已经通过钻机离开了地图", MessageTypeDefOf.NeutralEvent);
            DeSpawn();
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            // 离开地图时钻机没了,游击队离开地图
            if (startLeave)
            {
                foreach(var pawn in guerrillas)
                {
                    if(pawn==null) continue;
                    if (pawn.mindState == null) pawn.mindState = new();
                    pawn.mindState.duty = new PawnDuty(DutyDefOf.ExitMapBestAndDefendSelf);
                    pawn.mindState.duty.locomotion = LocomotionUrgency.Sprint;
                }
            }

            base.DeSpawn(mode);
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
            Scribe_Values.Look(ref tick, "tick", 0);
            Scribe_Values.Look(ref startLeave, "startLeave", false);
            Scribe_Collections.Look(ref guerrillas, "guerrillas", LookMode.Reference);
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
            yield break;
        }
    }
}
