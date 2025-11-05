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

    public class IncidentWorker_LimitedInsectAttack : IncidentWorker
    {
        private const int MaxIterations = 1000;
        private const float InsectPointsFactor = 0.8f;
        private static readonly IntRange InsectSpawnDelayTicks = new IntRange(180, 180);
        private static readonly IntRange PitBurrowEmergenceDelayRangeTicks = new IntRange(420, 420);
        private static readonly LargeBuildingSpawnParms BurrowSpawnParms = new LargeBuildingSpawnParms
        {
            maxDistanceToColonyBuilding = 1f,
            minDistToEdge = 5,
            attemptNotUnderBuildings = true,
            canSpawnOnImpassable = false,
            attemptSpawnLocationType = SpawnLocationType.Outdoors
        };

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return base.CanFireNowSub(parms);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            IntVec3 center = map.Center;

            float num = parms.points * InsectPointsFactor;
            List<Thing> list = new List<Thing>();
            int num2 = 0;

            while (num > 0f)
            {
                if (!LargeBuildingCellFinder.TryFindCell(out var cell, map, BurrowSpawnParms.ForThing(DefDatabase<ThingDef>.GetNamed("DoubleBed"))))
                {
                    Log.Warning("?");
                    return false;
                }

                float num3 = Mathf.Max(num, 500f);
                Thing item = SpawnInsectsFromTunnelHive(cell, map, num3, PitBurrowEmergenceDelayRangeTicks, InsectSpawnDelayTicks);
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

        private static Thing SpawnInsectsFromTunnelHive(IntVec3 cell, Map map, float points, IntRange emergenceDelay, IntRange spawnDelay)
        {
            Log.Warning(points.ToString());
            TunnelHiveSpawner tunnelHiveSpawner = (TunnelHiveSpawner)ThingMaker.MakeThing(ThingDefOf.TunnelHiveSpawner);
            tunnelHiveSpawner.spawnHive = true;
            tunnelHiveSpawner.insectsPoints = points;
            tunnelHiveSpawner.spawnedByInfestationThingComp = false;
            tunnelHiveSpawner.spawnedTick = Find.TickManager.TicksGame + spawnDelay.RandomInRange;
            GenSpawn.Spawn(tunnelHiveSpawner, cell, map);
            return tunnelHiveSpawner;
        }

        private static List<Pawn> GetInsectsForPoints(float points, Map map)
        {
            PawnGroupMakerParms pawnGroupMakerParms = new PawnGroupMakerParms
            {
                tile = map.Tile,
                faction = Faction.OfInsects,
                points = points > 0f ? points : StorytellerUtility.DefaultThreatPointsNow(map)
            };
            return PawnGroupMakerUtility.GeneratePawns(pawnGroupMakerParms).ToList();
        }
    }
}
