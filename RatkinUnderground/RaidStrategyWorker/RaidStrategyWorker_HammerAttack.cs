using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse.AI.Group;
using Verse;

namespace RatkinUnderground
{
    public class RaidStrategyWorker_HammerAttack : RaidStrategyWorker
    {
        protected override LordJob MakeLordJob(IncidentParms parms, Map map, List<Pawn> pawns, int raidSeed)
        {
            if (map.GameConditionManager.GetActiveCondition<RKU_GameCondition_FinalBattle>()==null) {
               return null;
            }
            if (pawns.NullOrEmpty())
            {
                return null;
            }
            IntVec3 siegeSpot = RCellFinder.FindSiegePositionFrom(parms.spawnCenter.IsValid ? parms.spawnCenter : pawns[0].PositionHeld, map);
            if (!siegeSpot.IsValid)
            {
                return null;
            }
            float num = parms.points * Rand.Range(0.2f, 0.3f);
            if (num < 60f)
            {
                num = 60f;
            }
            return new LordJob_HammerSiege(parms.faction, siegeSpot, num);
        }
        public override bool CanUseWith(IncidentParms parms, PawnGroupKindDef groupKind)
        {
            if (!base.CanUseWith(parms, groupKind))
            {
                return false;
            }

            if (parms.faction.def != DefOfs.RKU_Faction) {
                return false;
            }
            var component = Current.Game.GetComponent<RKU_RadioGameComponent>();
            bool isFinalBattleActive = false;
            foreach (Map map in Find.Maps)
            {
                if (map.IsPlayerHome)
                {
                    foreach (GameCondition activeCondition in map.gameConditionManager.ActiveConditions)
                    {
                        if (activeCondition.def.defName == "RKU_FinalBattle")
                        {
                            isFinalBattleActive = true;
                            break;
                        }
                    }
                    if (isFinalBattleActive) break;
                }
            }
            if (component == null)
            {
                return false;
            }
            else if (!isFinalBattleActive) {
                return false;
            }
           
            return parms.faction.def.canSiege;
        }
        public override bool CanUsePawnGenOption(float pointsTotal, PawnGenOption g, List<PawnGenOptionWithXenotype> chosenGroups, Faction faction = null)
        {
            if (g.kind.RaceProps.Animal)
            {
                return false;
            }
            return base.CanUsePawnGenOption(pointsTotal, g, chosenGroups, faction);
        }
    }
}