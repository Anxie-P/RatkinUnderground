using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace RatkinUnderground
{
    public class RKU_GenStep_GuerrillaSurvivor : GenStep
    {
        public override int SeedPart => 478234789;

        public override void Generate(Map map, GenStepParams parms)
        {
            Pawn singlePawnToSpawn = null;
            MapGenerator.rootsToUnfog.AddRange(map.AllCells);
            var loc = CellFinder.RandomSpawnCellForPawnNear(map.Center, map, 10);
            if (parms.sitePart != null && parms.sitePart.things != null && parms.sitePart.things.Any)
            {
                singlePawnToSpawn = parms.sitePart.things.FirstOrDefault() as Pawn;
            }

            if (singlePawnToSpawn != null)
            {

                singlePawnToSpawn.mindState.WillJoinColonyIfRescued = true;
                GenSpawn.Spawn(singlePawnToSpawn, loc, map);
                Log.Message($"{singlePawnToSpawn.Name}已生成在{loc},所在地图：{map}");
            }
            else
            {
                Log.Error("[RKU] 生成错误");
            }
        }
    }
}
