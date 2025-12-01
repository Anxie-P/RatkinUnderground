using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RatkinUnderground
{
    public class RKK_Projecticle_DestroyerMine : Projectile
    {
        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Map map = base.Map;
            IntVec3 position = base.Position;
            
            ThingDef mineDef = DefDatabase<ThingDef>.GetNamed("RKU_DestroyerMineBuilding");
            Thing mine = ThingMaker.MakeThing(mineDef);
            GenPlace.TryPlaceThing(mine, position, map, ThingPlaceMode.Near);
            base.Impact(hitThing);
        }
    }
} 