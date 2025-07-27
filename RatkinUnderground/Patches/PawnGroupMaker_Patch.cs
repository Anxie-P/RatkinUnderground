using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using static UnityEngine.GraphicsBuffer;
using Verse.Noise;
using Verse.AI.Group;

namespace RatkinUnderground
{
    // 这部分是patch的原版生成袭击单位的，跳过原版的生成逻辑，将生成的小人传入到地道
    [HarmonyPatch(typeof(PawnGroupMaker), nameof(PawnGroupMaker.GeneratePawns), new[] { typeof(PawnGroupMakerParms), typeof(bool) })]
    public static class PawnGroupMaker_GeneratePawns_Patch
    {
        static bool Prefix(PawnGroupMaker __instance, PawnGroupMakerParms parms, bool errorOnZeroResults, ref IEnumerable<Pawn> __result)
        {
            // 非目标派系，走原版
            if (parms.faction?.def != DefOfs.RKU_Faction) return true;

            // 检测地图是否为空
            Map map = Find.CurrentMap;
            if (map == null)
            {
                __result = Enumerable.Empty<Pawn>();
                return false;
            }

            var kinds = parms.groupKind.Worker
                .GeneratePawnKindsExample(parms, __instance)
                ?.ToList() ?? new List<PawnKindDef>();

            var pawns = kinds.Select(k => PawnGenerator.GeneratePawn(
                    new PawnGenerationRequest(
                        k,
                        parms.faction,
                        PawnGenerationContext.NonPlayer,
                        forceGenerateNewPawn: true)))
                .Where(p => p != null && !p.Destroyed)
                .ToList();


            var hive = (RKU_TunnelHiveSpawner_Und)ThingMaker.MakeThing(DefOfs.RKU_TunnelHiveSpawner_Und);
            hive.faction = parms.faction;
            // hive.canMove = false;
            pawns.ForEach(p => hive.GetDirectlyHeldThings().TryAddOrTransfer(p));
            foreach (var p in hive.GetDirectlyHeldThings())
            {
                Log.Message($"{hive}里含有的pawn为{p}");

            }
            if (CellFinder.TryFindRandomEdgeCellWith(x => x.Standable(map) && x.InBounds(map),
                                                    map,
                                                    CellFinder.EdgeRoadChance_Hostile,
                                                    out var loc))
            {
                GenSpawn.Spawn(hive, loc, map, WipeMode.Vanish);
                bool isHostile = parms.faction.HostileTo(Faction.OfPlayer);

                // 创建信件
                LetterDef letterDef = isHostile ? LetterDefOf.ThreatSmall : LetterDefOf.PositiveEvent;
                // 标题正文
                string label = isHostile ? "游击队袭击" : "游击队救援";
                string text = isHostile ? "袭击" : "救援";
                Find.LetterStack.ReceiveLetter(label, text, letterDef, new GlobalTargetInfo(loc, map));
            }
            else
            {
                Log.Warning("未找到合适边缘点放置 hive。");
            }

            __result = Enumerable.Empty<Pawn>();
            return false;
        }
    }
}
