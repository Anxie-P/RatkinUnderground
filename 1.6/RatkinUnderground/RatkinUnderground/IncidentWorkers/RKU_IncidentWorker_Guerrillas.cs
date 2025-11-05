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

            if (isHostile)
            {
                // 敌对情况：直接在当前地图生成
                var hive = (RKU_TunnelHiveSpawner_Und)ThingMaker.MakeThing(DefOfs.RKU_TunnelHiveSpawner_Und);
                hive.faction = faction;
                pawns.ForEach(p => hive.GetDirectlyHeldThings().TryAddOrTransfer(p));
                IntVec3 loc;
                if (!Utils.TryFindValidSpawnPosition(map, out loc))
                {
                    return false;
                }

                GenSpawn.Spawn(hive, loc, map, WipeMode.Vanish);

                LetterDef letterDef = LetterDefOf.ThreatSmall;
                string label = "RKU_GuerrillaAttackLabel".Translate();
                string text = "RKU_GuerrillaAttackText".Translate();
                Find.LetterStack.ReceiveLetter(label, text, letterDef, new GlobalTargetInfo(loc, map));
                return true;
            }
            else
            {
                // 友军情况：先选择大地图目标，再选择地图内位置
                var allyGuerrillaData = new RKU_AllyGuerrillaData
                {
                    faction = faction,
                    pawns = pawns,
                    originalMap = map
                };

                CameraJumper.TryJump(CameraJumper.GetWorldTargetOfMap(map));
                Find.WorldTargeter.BeginTargeting(
                    (GlobalTargetInfo worldTarget) => ChoseWorldTarget(worldTarget, allyGuerrillaData),
                    canTargetTiles: true,
                    null, // TargeterMouseAttachment
                    closeWorldTabWhenFinished: false
                );

                LetterDef letterDef = LetterDefOf.PositiveEvent;
                string label = "RKU_GuerrillaAllyLabel".Translate();
                string text = "RKU_GuerrillaAllyText".Translate();
                Find.LetterStack.ReceiveLetter(label, text, letterDef);
                return true;
            }
        }

        void SendStandardLetter(IncidentParms parms, List<Pawn> pawns, string labelKey, string textKey)
        {
            var lookTargets = new LookTargets(pawns);
            TaggedString label = labelKey.Translate(def.label);
            TaggedString text = textKey.Translate(parms.faction.NameColored, pawns.Count);
            SendStandardLetter(label, text, def.letterDef, parms, lookTargets);
        }

        private bool ChoseWorldTarget(GlobalTargetInfo worldTarget, RKU_AllyGuerrillaData data)
        {
            if (!worldTarget.IsValid)
            {
                Messages.Message("RKU_InvalidTargetSelected".Translate(), MessageTypeDefOf.RejectInput, false);
                return false;
            }

            Map targetMap = Current.Game.FindMap(worldTarget.Tile);
            if (targetMap == null)
            {
                Messages.Message("RKU_NoColonistsAtLocation".Translate(), MessageTypeDefOf.RejectInput, false);
                return false;
            }

            if (targetMap.mapPawns.ColonistsSpawnedCount == 0)
            {
                Messages.Message("RKU_NoColonistsAtLocation".Translate(), MessageTypeDefOf.RejectInput, false);
                return false;
            }

            // 切换到目标地图并开始选择具体位置
            CameraJumper.TryJump(targetMap.Center, targetMap);

            var targetingParams = new TargetingParameters
            {
                canTargetLocations = true,
                canTargetBuildings = false,
                canTargetPawns = false,
                validator = (target) =>
                {
                    if (!target.Cell.InBounds(targetMap)) return false;
                    return Utils.CanSpawnTunnelAt(target.Cell, targetMap);
                }
            };

            Find.Targeter.BeginTargeting(targetingParams, (LocalTargetInfo target) =>
            {
                if (target.Cell.InBounds(targetMap) && Utils.CanSpawnTunnelAt(target.Cell, targetMap))
                {
                    SpawnGuerrillaAtPosition(data, target.Cell, targetMap);
                }
            });

            return true;
        }

        private void SpawnGuerrillaAtPosition(RKU_AllyGuerrillaData data, IntVec3 position, Map targetMap)
        {
            var hive = (RKU_TunnelHiveSpawner_Und)ThingMaker.MakeThing(DefOfs.RKU_TunnelHiveSpawner_Und);
            hive.faction = data.faction;
            data.pawns.ForEach(p => hive.GetDirectlyHeldThings().TryAddOrTransfer(p));
            GenSpawn.Spawn(hive, position, targetMap, WipeMode.Vanish);

            var radioComponent = Current.Game.GetComponent<RKU_RadioGameComponent>();
            if (radioComponent != null)
            {
                radioComponent.canEmergency = true;
                radioComponent.lastEmergencyTick = 0;
            }

            string label = "RKU_GuerrillasArrivedLabel".Translate();
            string text = "RKU_GuerrillasArrivedText".Translate();
            Find.LetterStack.ReceiveLetter(label, text, LetterDefOf.PositiveEvent, new GlobalTargetInfo(position, targetMap));
        }
    }

    public class RKU_AllyGuerrillaData
    {
        public Faction faction;
        public List<Pawn> pawns;
        public Map originalMap;
    }

}
