using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace RatkinUnderground
{
    public class IncidentWorker_LimitedFleshbeastAttack : IncidentWorker
    {
        private const int MaxIterations = 100;
        private const float FleshbeastPointsFactor = 0.6f;
        private static readonly IntRange FleshbeastSpawnDelayTicks = new IntRange(180, 180);
        private static readonly IntRange PitBurrowEmergenceDelayRangeTicks = new IntRange(420, 420);
        private static readonly LargeBuildingSpawnParms BurrowSpawnParms = new LargeBuildingSpawnParms
        {
            maxDistanceToColonyBuilding = -1f,
            minDistToEdge = 10,
            attemptNotUnderBuildings = true,
            canSpawnOnImpassable = false,
            attemptSpawnLocationType = SpawnLocationType.Outdoors
        };

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            _ = (Map)parms.target;
            return base.CanFireNowSub(parms);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            IntVec3 center = map.Center;

            float num = parms.points * FleshbeastPointsFactor;
            List<Thing> list = new List<Thing>();
            int num2 = 0;

            while (num > 0f)
            {
                if (!LargeBuildingCellFinder.TryFindCell(out var cell, map, BurrowSpawnParms.ForThing(DefDatabase<ThingDef>.GetNamed("DoubleBed"))))
                {
                    return false;
                }

                float num3 = Mathf.Max(num, 500f);
                Thing item = FleshbeastUtility.SpawnFleshbeastsFromPitBurrowEmergence(cell, map, num3, PitBurrowEmergenceDelayRangeTicks, FleshbeastSpawnDelayTicks);
                list.Add(item);
                num -= num3;
                num2++;

                if (num2 > MaxIterations)
                {
                    break;
                }
            }

            SendStandardLetter(def.letterLabel, (list.Count > 1) ? def.letterTextPlural : def.letterText, def.letterDef, parms, list);
            return true;
        }
    }
}

