using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;
using Verse.Sound;
using Verse.Noise;
using RimWorld.BaseGen;
using Verse.AI.Group;
using static UnityEngine.GraphicsBuffer;

namespace RatkinUnderground
{
    public class RKU_IncidentWorker_ConfusedRat : IncidentWorker
    {
        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;

            // 找到地图边缘的位置
            CellFinder.TryFindRandomEdgeCellWith(
                    c => c.Standable(map),
                    map,
                    CellFinder.EdgeRoadChance_Neutral,
                    out IntVec3 spawnPosition);

            if (spawnPosition == IntVec3.Invalid)
            {
                return false;
            }

            // 生成鼠族pawn
            PawnKindDef ratkinKind = DefDatabase<PawnKindDef>.GetNamed("RatkinSubject");
            PawnGenerationRequest request = new PawnGenerationRequest(
                ratkinKind,
                null,
                PawnGenerationContext.NonPlayer,
                fixedGender: Gender.Male,
                fixedBiologicalAge: Rand.Range(15, 25),
                fixedChronologicalAge: Rand.Range(15, 25)
            );

            Pawn ratkinPawn = PawnGenerator.GeneratePawn(request);

            // 将pawn生成到地图边缘
            GenSpawn.Spawn(ratkinPawn, spawnPosition, map);
            ratkinPawn.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Wander_Psychotic);
            ratkinPawn.story.traits.GainTrait(new Trait(DefDatabase<TraitDef>.GetNamed("Tough")));
            ratkinPawn.Name = new NameTriple((ratkinPawn.Name as NameTriple).First, (ratkinPawn.Name as NameTriple).First, "Raeline".Translate());

            // 添加鼠体实验影响hediff
            HediffDef experimentHediff = DefDatabase<HediffDef>.GetNamed("RKU_CombatEfficiency");
            if (experimentHediff != null)
            {
                Hediff hediff = HediffMaker.MakeHediff(experimentHediff, ratkinPawn);
                hediff.Severity = Rand.Range(0.5f, 0.7f);
                ratkinPawn.health.AddHediff(hediff);
            }
            Find.LetterStack.ReceiveLetter(def.letterLabel, def.letterText, LetterDefOf.NeutralEvent, lookTargets: ratkinPawn);
            return true;
        }
    }
}
