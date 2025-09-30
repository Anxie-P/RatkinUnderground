using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Tilemaps;
using Verse;
using Verse.Grammar;

namespace RatkinUnderground
{
    public class SitePartWorker_GuerrillaSurvivor : SitePartWorker
    {

        public override void Notify_GeneratedByQuestGen(SitePart part, Slate slate, List<Rule> outExtraDescriptionRules, Dictionary<string, string> outExtraDescriptionConstants)
        {
            base.Notify_GeneratedByQuestGen(part, slate, outExtraDescriptionRules, outExtraDescriptionConstants);
            Log.Message("[RKU] 已执行SitePartWorker_GuerrillaSurvivor");

            PawnGenerationRequest request = new PawnGenerationRequest(PawnKindDef.Named("RKU_Scout"), Find.FactionManager.FirstFactionOfDef(DefOfs.RKU_Faction), PawnGenerationContext.NonPlayer, part.site.Tile, forceGenerateNewPawn: false, allowDead: false, allowDowned: false, canGeneratePawnRelations: true, mustBeCapableOfViolence: false, 75f, forceAddFreeWarmLayerIfNeeded: true, allowGay: true, allowPregnant: false, allowFood: true, allowAddictions: true, inhabitant: false, certainlyBeenInCryptosleep: false, forceRedressWorldPawnIfFormerColonist: true, worldPawnFactionDoesntMatter: true);
            if (Find.Storyteller.difficulty.ChildrenAllowed)
            {
                request.AllowedDevelopmentalStages |= DevelopmentalStage.Child;
            }
            
            Pawn pawn = PawnGenerator.GeneratePawn(request);
            // 击倒不死亡，我称为泰南的仁慈，但这任务写起来跟赤石一样难过
            HealthUtility.DamageUntilDowned(pawn);
            pawn.guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Guest);

            // Pawn pawn = PawnGenerator.GeneratePawn(PawnKindDef.Named("RKU_Scout"), Find.FactionManager.FirstFactionOfDef(DefOfs.RKU_Faction));
            if (part.things == null)
            {
                part.things = new ThingOwner<Pawn>(part, oneStackOnly: true);
            }
            part.things.TryAdd(pawn);
            PawnRelationUtility.Notify_PawnsSeenByPlayer(Gen.YieldSingle(pawn), out var pawnRelationsInfo, informEvenIfSeenBefore: true, writeSeenPawnsNames: false);
            string output = (pawnRelationsInfo.NullOrEmpty() ? "" : ((string)("\n\n" + "PawnHasTheseRelationshipsWithColonists".Translate(pawn.LabelShort, pawn) + "\n\n" + pawnRelationsInfo)));
            slate.Set("guerrilla", pawn);
            outExtraDescriptionRules.Add(new Rule_String("prisonerFullRelationInfo", output));
        }
        
        public override string GetPostProcessedThreatLabel(Site site, SitePart sitePart)
        {
            string text = base.GetPostProcessedThreatLabel(site, sitePart);
            if (sitePart.things != null && sitePart.things.Any)
            {
                text = text + ": " + sitePart.things[0].LabelShortCap;
            }
            if (site.HasWorldObjectTimeout)
            {
                text += " (" + "DurationLeft".Translate(site.WorldObjectTimeoutTicksLeft.ToStringTicksToPeriod()) + ")";
            }
            return text;
        }
    }
}
