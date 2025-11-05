using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RatkinUnderground
{
    public class RKU_PawnGroupKindWorker : PawnGroupKindWorker
    {
        public override IEnumerable<PawnKindDef> GeneratePawnKindsExample(PawnGroupMakerParms parms, PawnGroupMaker groupMaker)
        {
            throw new NotImplementedException();
        }

        public override float MinPointsToGenerateAnything(PawnGroupMaker groupMaker, FactionDef faction, PawnGroupMakerParms parms = null)
        {
            throw new NotImplementedException();
        }

        protected override void GeneratePawns(PawnGroupMakerParms parms, PawnGroupMaker groupMaker, List<Pawn> outPawns, bool errorOnZeroResults = true)
        {
            if (!CanGenerateFrom(parms, groupMaker))
            {
                if (errorOnZeroResults)
                {
                    Log.Error(string.Concat("Cannot generate pawns for ", parms.faction, " with ", parms.points, ". Defaulting to a single random cheap group."));
                }

                return;
            }

            bool allowFood = parms.raidStrategy == null || parms.raidStrategy.pawnsCanBringFood || (parms.faction != null && !parms.faction.HostileTo(Faction.OfPlayer));
            Predicate<Pawn> validatorPostGear = ((parms.raidStrategy != null) ? ((Predicate<Pawn>)((Pawn p) => parms.raidStrategy.Worker.CanUsePawn(parms.points, p, outPawns))) : null);
            bool flag = false;
            foreach (PawnGenOptionWithXenotype item in PawnGroupMakerUtility.ChoosePawnGenOptionsByPoints(parms.points, groupMaker.options, parms))
            {
                PawnGenerationRequest request = new PawnGenerationRequest(item.Option.kind, parms.faction, PawnGenerationContext.NonPlayer, fixedIdeo: parms.ideo, forcedXenotype: item.Xenotype, tile: parms.tile, forceGenerateNewPawn: false, allowDead: false, allowDowned: false, canGeneratePawnRelations: true, mustBeCapableOfViolence: true, colonistRelationChanceFactor: 1f, forceAddFreeWarmLayerIfNeeded: false, allowGay: true, allowPregnant: true, allowFood: allowFood, allowAddictions: true, inhabitant: parms.inhabitants, certainlyBeenInCryptosleep: false, forceRedressWorldPawnIfFormerColonist: false, worldPawnFactionDoesntMatter: false, biocodeWeaponChance: 0f, biocodeApparelChance: 0f, extraPawnForExtraRelationChance: null, relationWithExtraPawnChanceFactor: 1f, validatorPreGear: null, validatorPostGear: validatorPostGear);
                if (parms.raidAgeRestriction != null && parms.raidAgeRestriction.Worker.ShouldApplyToKind(item.Option.kind))
                {
                    request.BiologicalAgeRange = parms.raidAgeRestriction.ageRange;
                    request.AllowedDevelopmentalStages = parms.raidAgeRestriction.developmentStage;
                }

                if (item.Option.kind.pawnGroupDevelopmentStage.HasValue)
                {
                    request.AllowedDevelopmentalStages = item.Option.kind.pawnGroupDevelopmentStage.Value;
                }

                if (!Find.Storyteller.difficulty.ChildRaidersAllowed && parms.faction != null && parms.faction.HostileTo(Faction.OfPlayer))
                {
                    request.AllowedDevelopmentalStages = DevelopmentalStage.Adult;
                }

                Pawn pawn = PawnGenerator.GeneratePawn(request);
                if (parms.forceOneDowned && !flag)
                {
                    pawn.health.forceDowned = true;
                    if (pawn.guest != null)
                    {
                        pawn.guest.Recruitable = true;
                    }

                    pawn.mindState.canFleeIndividual = false;
                    flag = true;
                }

                outPawns.Add(pawn);
            }
        }
    }
}
