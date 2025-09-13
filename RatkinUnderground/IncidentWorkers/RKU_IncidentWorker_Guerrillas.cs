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
    public class RKU_IncidentWorker_Guerrillas : IncidentWorker
    {
        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!(parms.target is Map map)) return false;

            var factionDef = DefOfs.RKU_Faction;
            var faction = Find.FactionManager.FirstFactionOfDef(factionDef); ;

            float points = 2000;

            var pawnGroupParams = new PawnGroupMakerParms
            {
                faction = faction,
                points = points,
                groupKind = PawnGroupKindDefOf.Combat,
                tile = map.Tile,
                raidStrategy = parms.raidStrategy
            };

            List<Pawn> pawns = PawnGroupMakerUtility.GeneratePawns(pawnGroupParams).ToList();

            var hive = (RKU_TunnelHiveSpawner_Und)ThingMaker.MakeThing(DefOfs.RKU_TunnelHiveSpawner_Und);
            hive.faction = faction;
            pawns.ForEach(p => hive.GetDirectlyHeldThings().TryAddOrTransfer(p));
            IntVec3 loc;
            if (!Utils.TryFindValidSpawnPosition(map, out loc))
            {
                return false;
            }

            GenSpawn.Spawn(hive, loc, map, WipeMode.Vanish);
            bool isHostile = parms.faction.HostileTo(Faction.OfPlayer);

            RKU_RadioGameComponent radioComponent = Current.Game.GetComponent<RKU_RadioGameComponent>();

            // 根据关系等级确定事件类型
            if (radioComponent != null && radioComponent.ralationshipGrade < -25)
            {
                isHostile = true;
                Faction rkuFaction = Find.FactionManager.FirstFactionOfDef(FactionDef.Named("RKU_Faction"));
                if (rkuFaction != null)
                {
                    FactionRelationKind kind2 = Utils.OfRKU.RelationWith(Faction.OfPlayer).kind;
                    Utils.OfRKU.RelationWith(Faction.OfPlayer).kind = FactionRelationKind.Hostile;
                    Faction.OfPlayer.RelationWith(Utils.OfRKU).kind = FactionRelationKind.Hostile;
                    Utils.OfRKU.Notify_RelationKindChanged(Faction.OfPlayer, kind2, false, "", TargetInfo.Invalid, out var sentLetter);
                    Faction.OfPlayer.Notify_RelationKindChanged(Utils.OfRKU, kind2, false, "", TargetInfo.Invalid, out sentLetter);
                }
            }
            LetterDef letterDef = isHostile ? LetterDefOf.ThreatSmall : LetterDefOf.PositiveEvent;
            string label = "鼠族隧道游击队";
            string text = isHostile ? "侦测到地下活动，一支鼠族地下游击队正在进攻殖民地" : "侦测到地下活动，一支鼠族地下游击队正在赶来协助殖民地防守";
            Find.LetterStack.ReceiveLetter(label, text, letterDef, new GlobalTargetInfo(loc, map));
            return true;
        }

        void SendStandardLetter(IncidentParms parms, List<Pawn> pawns, string labelKey, string textKey)
        {
            var lookTargets = new LookTargets(pawns);
            TaggedString label = labelKey.Translate(def.label);
            TaggedString text = textKey.Translate(parms.faction.NameColored, pawns.Count);
            SendStandardLetter(label, text, def.letterDef, parms, lookTargets);
        }
    }
}
