using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace RatkinUnderground
{
    public class RKU_DrillingCargoPod : Building, IThingHolder
    {
        private ThingOwner<Thing> cargo;

        public RKU_DrillingCargoPod()
        {
            cargo = new ThingOwner<Thing>(this);
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (cargo == null)
            {
                cargo = new ThingOwner<Thing>(this);
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
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if (mode == DestroyMode.KillFinalize || mode == DestroyMode.Deconstruct)
            {
                // 释放货物
                ReleaseCargo();
            }
            base.Destroy(mode);
        }

        private void ReleaseCargo()
        {
            if (cargo == null || cargo.Count == 0) return;
            foreach (Thing thing in cargo.ToList())
            {
                if (thing != null && !thing.Destroyed)
                {
                    cargo.Remove(thing);
                    GenPlace.TryPlaceThing(thing, Position, Map, ThingPlaceMode.Near);
                }
            }
        }

        void IThingHolder.GetChildHolders(List<IThingHolder> outChildren)
        {
            if (outChildren == null)
            {
                return;
            }
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        ThingOwner GetDirectlyHeldThings()
        {
            if (cargo == null)
            {
                cargo = new ThingOwner<Thing>(this);
            }
            return cargo;
        }

        public void AddCargo(Thing thing)
        {
            if (cargo == null)
            {
                cargo = new ThingOwner<Thing>(this);
            }
            cargo.TryAddOrTransfer(thing);
        }

        ThingOwner IThingHolder.GetDirectlyHeldThings()
        {
           return GetDirectlyHeldThings();
        }
    }
}
