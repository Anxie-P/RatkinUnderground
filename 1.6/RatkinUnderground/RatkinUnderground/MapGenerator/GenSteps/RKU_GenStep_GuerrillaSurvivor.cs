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
            var loc = CellFinder.StandableCellNear(map.Center, map, 50);
            if (parms.sitePart == null) Log.Warning("[RKU] sitePart为空");
            if (parms.sitePart.things == null) Log.Warning("[RKU] things为空");
            if (!parms.sitePart.things.Any) Log.Warning("[RKU] things不含");
            if (parms.sitePart != null && parms.sitePart.things != null && parms.sitePart.things.Any)
            {
                Log.Message(parms.sitePart.things.FirstOrDefault().def.defName);
                singlePawnToSpawn = parms.sitePart.things.FirstOrDefault() as Pawn;
            }

            if(singlePawnToSpawn == null)
            {
                Log.Warning("[RKU] singlePawnToSpawn未正常生成，使用随机角色");
                Faction faction = Find.FactionManager.FirstFactionOfDef(DefOfs.RKU_Faction);
                PawnKindDef pawnKind = DefOfs.RKU_Scout;
                singlePawnToSpawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                    pawnKind,
                    faction,
                    PawnGenerationContext.NonPlayer,
                    -1,
                    true
                ));
                singlePawnToSpawn.health.AddHediff(HediffDef.Named("RKU_Survivor"));
                singlePawnToSpawn.guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Guest);
            }

            if (singlePawnToSpawn != null)
            {
                GenSpawn.Spawn(singlePawnToSpawn, loc, map);
                HealthUtility.DamageUntilDowned(singlePawnToSpawn);
                singlePawnToSpawn.mindState.WillJoinColonyIfRescued = true;
                // singlePawnToSpawn.health.AddHediff(HediffDef.Named("RKU_Survivor"));
                //singlePawnToSpawn.guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Guest);
                Log.Message($"{singlePawnToSpawn.Name}已生成在{loc},所在地图：{map}");

                parms.sitePart.things.Clear();
            }
            else
            {
                Log.Error("[RKU] 生成错误 singlePawnToSpawn为空");
            }
        }
    }
}
