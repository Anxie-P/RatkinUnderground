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
            float points = 2500;

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
            bool isHostile = parms.faction.HostileTo(Faction.OfPlayer);
            RKU_RadioGameComponent radioComponent = Current.Game.GetComponent<RKU_RadioGameComponent>();
            // 根据关系等级确定事件类型
            if (radioComponent != null && radioComponent.ralationshipGrade < -25)
            {
                isHostile = true;
                if (rkuFaction != null)
                {
                    FactionRelationKind kind2 = Utils.OfRKU.RelationWith(Faction.OfPlayer).kind;
                    Utils.OfRKU.RelationWith(Faction.OfPlayer).kind = FactionRelationKind.Hostile;
                    Faction.OfPlayer.RelationWith(Utils.OfRKU).kind = FactionRelationKind.Hostile;
                    Utils.OfRKU.Notify_RelationKindChanged(Faction.OfPlayer, kind2, false, "", TargetInfo.Invalid, out var sentLetter);
                    Faction.OfPlayer.Notify_RelationKindChanged(Utils.OfRKU, kind2, false, "", TargetInfo.Invalid, out sentLetter);
                }
            }
            LetterDef letterDef =  LetterDefOf.NeutralEvent;
            string label = "游击队物资劫持";
            string text = "游击队侦察到你的基地里有鼠族王国的一支由贵族带领的商队。他们决定趁机发动突袭，劫持这些珍贵的物资。如果你不予阻止，会得到游击队员们的感谢";
            Find.LetterStack.ReceiveLetter(label, text, letterDef, new GlobalTargetInfo(loc, map));
            GenSpawn.Spawn(hive, loc, map, WipeMode.Vanish);
            component.ralationshipGrade += 10;
            return true;
        }

        void SendStandardLetter(IncidentParms parms, List<Pawn> pawns, string labelKey, string textKey)
        {
            var lookTargets = new LookTargets(pawns);
            TaggedString label = labelKey.Translate(def.label);
            TaggedString text = textKey.Translate(parms.faction.NameColored, pawns.Count);
            SendStandardLetter(label, text, def.letterDef, parms, lookTargets);
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
            bool hasGoodRelationWithRatkin = (ratkinFaction != null && Faction.OfPlayer.RelationWith(ratkinFaction).kind != FactionRelationKind.Hostile); 
            if (!hasGoodRelationWithRatkin)
            {
                return false;
            }
            bool hasRatkinNoble = map.mapPawns.AllPawnsSpawned.Any(pawn =>
                (pawn.Faction == ratkinFaction) &&
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
