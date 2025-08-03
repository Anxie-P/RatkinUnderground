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
            // hive.canMove = false;
            pawns.ForEach(p => hive.GetDirectlyHeldThings().TryAddOrTransfer(p));
            var loc = Utils.FindRandomEdgeSpawnPosition(map);
            /*if (CellFinder.TryFindRandomEdgeCellWith(x => x.Standable(map) && x.InBounds(map),
                                                    map,
                                                    CellFinder.EdgeRoadChance_Hostile,
                                                    out var loc))*/
            GenSpawn.Spawn(hive, loc, map, WipeMode.Vanish);
            bool isHostile = parms.faction.HostileTo(Faction.OfPlayer);

            // 创建信件
            LetterDef letterDef = isHostile ? LetterDefOf.ThreatSmall : LetterDefOf.PositiveEvent;
            // 标题正文
            string label = isHostile ? "游击队袭击" : "游击队救援";
            string text = isHostile ? "袭击" : "救援";
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
