using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI.Group;

namespace RatkinUnderground
{
    public class RKU_IncidentWorker_CaravanHijack : IncidentWorker
    {
        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!(parms.target is Map map)) return false;

            var component = Current.Game.GetComponent<RKU_RadioGameComponent>();
            if (component == null) return false;
            // 检查触发条件
            if (!CanHijackOccur(map))
            {
                return false;
            }

            // 获取游击队派系
            var rkuFaction = Utils.OfRKU;
            if (rkuFaction == null) return false;

            // 设置参数
            float points = 2500; // 更高的难度，因为是物资劫持

            var pawnGroupParams = new PawnGroupMakerParms
            {
                faction = rkuFaction,
                points = points,
                groupKind = PawnGroupKindDefOf.Combat,
                tile = map.Tile,
                raidStrategy = RaidStrategyDefOf.ImmediateAttack
            };

            List<Pawn> pawns = PawnGroupMakerUtility.GeneratePawns(pawnGroupParams).ToList();

            // 创建隧道生成器，使用边缘位置
            var hive = (RKU_TunnelHiveSpawner_Und)ThingMaker.MakeThing(DefOfs.RKU_TunnelHiveSpawner_Und);
            hive.faction = rkuFaction;
            pawns.ForEach(p => hive.GetDirectlyHeldThings().TryAddOrTransfer(p));

            // 在地图边缘寻找有效位置
            IntVec3 loc;
            if (!Utils.TryFindEdgePosition(map, out loc))
            {
                return false;
            }

            GenSpawn.Spawn(hive, loc, map, WipeMode.Vanish);
            return true;
        }

        /// <summary>
        /// 检查是否可以发生物资劫持事件
        /// </summary>
        private bool CanHijackOccur(Map map)
        {
            var component = Current.Game.GetComponent<RKU_RadioGameComponent>();

            // 检查与游击队的关系 (>= 30)
            var rkuFaction = Utils.OfRKU;
            if (rkuFaction == null || component.ralationshipGrade < 30)
            {
                return false;
            }

            var ratkinFaction = Find.FactionManager.FirstFactionOfDef(FactionDef.Named("Rakinia"));
            var ratkinWarlordFaction = Find.FactionManager.FirstFactionOfDef(FactionDef.Named("Rakinia_Warlord"));
            bool hasGoodRelationWithRatkin = (ratkinFaction != null && Faction.OfPlayer.RelationWith(ratkinFaction).kind!=FactionRelationKind.Hostile) ||
                                           (ratkinWarlordFaction != null && Faction.OfPlayer.RelationWith(ratkinWarlordFaction).kind != FactionRelationKind.Hostile);

            if (!hasGoodRelationWithRatkin)
            {
                return false;
            }

            // 检查是否有鼠族王国的商队在玩家基地
            bool hasRatkinCaravan = false;
            foreach (var caravan in Find.WorldObjects.Caravans)
            {
                if (caravan.Faction == ratkinFaction || caravan.Faction == ratkinWarlordFaction)
                {
                    if (caravan.Tile == map.Tile || caravan.pather.Destination == map.Tile)
                    {
                        hasRatkinCaravan = true;
                        break;
                    }
                }
            }

            if (!hasRatkinCaravan)
            {
                return false;
            }

            bool hasRatkinNoble = map.mapPawns.AllPawns.Any(pawn =>
                (pawn.Faction == ratkinFaction || pawn.Faction == ratkinWarlordFaction) &&
                IsNoblePawn(pawn));

            return hasRatkinNoble;
        }

        /// <summary>
        /// 判断pawn是否为贵族单位
        /// </summary>
        private bool IsNoblePawn(Pawn pawn)
        {
            return pawn.kindDef.defName == "RatkinKnightCommander" ||
                   pawn.kindDef.defName == "RatkinNoble";
        }
    }
}
