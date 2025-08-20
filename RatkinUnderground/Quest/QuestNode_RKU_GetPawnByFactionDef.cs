using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using UnityEngine;
using Verse;

namespace RatkinUnderground
{
    public class QuestNode_RKU_GetPawnByFactionDef : QuestNode_GetPawn
    {
        public SlateRef<IEnumerable<FactionDef>> requiredFactionDefs;

        private IEnumerable<Pawn> ExistingUsablePawns(Slate slate)
        {
            var basePawns = ExistingUsablePawnsFinder(slate);
            if (requiredFactionDefs.GetValue(slate) != null && requiredFactionDefs.GetValue(slate).Any())
            {
                return basePawns.Where(p => p.Faction != null && requiredFactionDefs.GetValue(slate).Contains(p.Faction.def));
            }
            return basePawns;
        }

        private bool TryFindFactionForPawnGeneration(Slate slate, out Faction faction)
        {
            var baseResult = TryFindFactionForPawnGenerationFinder(slate, out faction);
            if (requiredFactionDefs.GetValue(slate) != null && requiredFactionDefs.GetValue(slate).Any())
            {
                return Find.FactionManager.GetFactions(allowHidden: false, allowDefeated: false, allowNonHumanlike: false)
                    .Where(f => requiredFactionDefs.GetValue(slate).Contains(f.def) && IsGoodFactionForGeneration(f, slate))  // 自定义过滤
                    .TryRandomElementByWeight(f => f.HostileTo(Faction.OfPlayer) ? (hostileWeight.GetValue(slate) ?? 1f) : (nonHostileWeight.GetValue(slate) ?? 1f), out faction);
            }
            return baseResult;
        }

        private bool IsGoodFactionForGeneration(Faction f, Slate slate)
        {
            // 可以添加额外过滤逻辑，如果需要
            return true;
        }

        protected override bool TestRunInt(Slate slate)
        {
            //if (requiredFactionDefs.GetValue(slate) != null && requiredFactionDefs.GetValue(slate).Any() && !ExistingUsablePawns(slate).Any())
            //{
            //    if (canGeneratePawn.GetValue(slate) && !TryFindFactionForPawnGeneration(slate, out _))
            //    {
            //        return false;
            //    }
            //}
            return base.TestRunInt(slate);
        }

        protected override void RunInt()
        {
            Slate slate = QuestGen.slate;
            Pawn selectedPawn = null;

            var usablePawns = ExistingUsablePawns(slate);
            if (usablePawns.Any())
            {
                selectedPawn = usablePawns.RandomElementByWeight(p => (p.Faction != null && p.Faction.HostileTo(Faction.OfPlayer)) ? (hostileWeight.GetValue(slate) ?? 1f) : (nonHostileWeight.GetValue(slate) ?? 1f));
            }
            else if (canGeneratePawn.GetValue(slate))
            {
                Faction genFaction = null;
                if (TryFindFactionForPawnGeneration(slate, out genFaction))
                {
                    selectedPawn = GeneratePawn(slate, genFaction);
                }
            }

            if (selectedPawn != null)
            {
                QuestGen.slate.Set(storeAs.GetValue(slate), selectedPawn);
                if (selectedPawn.Faction != null && !selectedPawn.Faction.Hidden)
                {
                    QuestPart_InvolvedFactions questPart = new QuestPart_InvolvedFactions();
                    questPart.factions.Add(selectedPawn.Faction);
                    QuestGen.quest.AddPart(questPart);
                }
            }
            else
            {
                Log.Warning("未能找到或生成匹配的Pawn。");
            }

        }
        private IEnumerable<Pawn> ExistingUsablePawnsFinder(Slate slate)
        {
            return PawnsFinder.AllMapsWorldAndTemporary_Alive.Where((Pawn x) => IsGoodPawn(x, slate));
        }

        private bool TryFindFactionForPawnGenerationFinder(Slate slate, out Faction faction)
        {
            return Find.FactionManager.GetFactions(allowHidden: false, allowDefeated: false, allowNonHumanlike: false).Where(delegate (Faction x)
            {
                if (excludeFactionDefs.GetValue(slate) != null && excludeFactionDefs.GetValue(slate).Contains(x.def))
                {
                    return false;
                }

                if (mustHaveRoyalTitleInCurrentFaction.GetValue(slate) && !x.def.HasRoyalTitles)
                {
                    return false;
                }

                if (mustBeNonHostileToPlayer.GetValue(slate) && x.HostileTo(Faction.OfPlayer))
                {
                    return false;
                }

                if (((!allowPermanentEnemyFaction.GetValue(slate)) ?? true) && x.def.permanentEnemy)
                {
                    return false;
                }

                if ((int)x.def.techLevel < (int)minTechLevel.GetValue(slate))
                {
                    return false;
                }

                return (!factionMustBePermanent.GetValue(slate) || !x.temporary) ? true : false;
            }).TryRandomElementByWeight((Faction x) => x.HostileTo(Faction.OfPlayer) ? (hostileWeight.GetValue(slate) ?? 1f) : (nonHostileWeight.GetValue(slate) ?? 1f), out faction);
        }
        private bool IsGoodPawn(Pawn pawn, Slate slate)
        {
            if (mustBeFactionLeader.GetValue(slate))
            {
                Faction faction = pawn.Faction;
                if (faction == null || faction.leader != pawn || !faction.def.humanlikeFaction || faction.defeated || faction.Hidden || faction.IsPlayer || pawn.IsPrisoner)
                {
                    return false;
                }
            }

            if (pawn.Faction != null && excludeFactionDefs.GetValue(slate) != null && excludeFactionDefs.GetValue(slate).Contains(pawn.Faction.def))
            {
                return false;
            }

            if (pawn.Faction != null && (int)pawn.Faction.def.techLevel < (int)minTechLevel.GetValue(slate))
            {
                return false;
            }

            if (mustBeOfKind.GetValue(slate) != null && pawn.kindDef != mustBeOfKind.GetValue(slate))
            {
                return false;
            }

            if (mustHaveRoyalTitleInCurrentFaction.GetValue(slate) && (pawn.Faction == null || pawn.royalty == null || !pawn.royalty.HasAnyTitleIn(pawn.Faction)))
            {
                return false;
            }

            if (seniorityRange.GetValue(slate) != default(FloatRange) && (pawn.royalty?.MostSeniorTitle == null || !seniorityRange.GetValue(slate).IncludesEpsilon(pawn.royalty.MostSeniorTitle.def.seniority)))
            {
                return false;
            }

            if (factionMustBePermanent.GetValue(slate) && pawn.Faction != null && pawn.Faction.temporary)
            {
                return false;
            }

            if (mustBeWorldPawn.GetValue(slate) && !pawn.IsWorldPawn())
            {
                return false;
            }

            if (ifWorldPawnThenMustBeFree.GetValue(slate) && pawn.IsWorldPawn() && Find.WorldPawns.GetSituation(pawn) != WorldPawnSituation.Free)
            {
                return false;
            }

            if (ifWorldPawnThenMustBeFreeOrLeader.GetValue(slate) && pawn.IsWorldPawn() && Find.WorldPawns.GetSituation(pawn) != WorldPawnSituation.Free && Find.WorldPawns.GetSituation(pawn) != WorldPawnSituation.FactionLeader)
            {
                return false;
            }

            if (pawn.IsWorldPawn() && Find.WorldPawns.GetSituation(pawn) == WorldPawnSituation.ReservedByQuest)
            {
                return false;
            }

            if (mustHaveNoFaction.GetValue(slate) && pawn.Faction != null)
            {
                return false;
            }

            if (mustBeFreeColonist.GetValue(slate) && !pawn.IsFreeColonist)
            {
                return false;
            }

            if (mustBePlayerPrisoner.GetValue(slate) && !pawn.IsPrisonerOfColony)
            {
                return false;
            }

            if (mustBeNotSuspended.GetValue(slate) && pawn.Suspended)
            {
                return false;
            }

            if (mustBeNonHostileToPlayer.GetValue(slate) && (pawn.HostileTo(Faction.OfPlayer) || (pawn.Faction != null && pawn.Faction != Faction.OfPlayer && pawn.Faction.HostileTo(Faction.OfPlayer))))
            {
                return false;
            }

            bool? value = allowPermanentEnemyFaction.GetValue(slate);
            if (value.HasValue && !value.GetValueOrDefault() && pawn.Faction != null && pawn.Faction.def.permanentEnemy)
            {
                return false;
            }

            return true;
        }
        private Pawn GeneratePawn(Slate slate, Faction faction = null)
        {
            PawnKindDef result = mustBeOfKind.GetValue(slate);
            if (faction == null && !mustHaveNoFaction.GetValue(slate))
            {
                if (!TryFindFactionForPawnGeneration(slate, out faction))
                {
                    Log.Error("QuestNode_GetPawn tried generating pawn but couldn't find a proper faction for new pawn.");
                }
                else if (result == null)
                {
                    result = faction.RandomPawnKind();
                }
            }

            RoyalTitleDef fixedTitle;
            if (mustHaveRoyalTitleInCurrentFaction.GetValue(slate))
            {
                if (!seniorityRange.TryGetValue(slate, out var senRange))
                {
                    senRange = FloatRange.Zero;
                }

                IEnumerable<RoyalTitleDef> source = DefDatabase<RoyalTitleDef>.AllDefsListForReading.Where((RoyalTitleDef t) => faction.def.RoyalTitlesAllInSeniorityOrderForReading.Contains(t) && (senRange.max <= 0f || senRange.IncludesEpsilon(t.seniority)));

                fixedTitle = source.RandomElementByWeight((RoyalTitleDef t) => t.commonality);
                if (mustBeOfKind.GetValue(slate) == null && !DefDatabase<PawnKindDef>.AllDefsListForReading.Where((PawnKindDef k) => k.titleRequired != null && k.titleRequired == fixedTitle).TryRandomElement(out result))
                {
                    DefDatabase<PawnKindDef>.AllDefsListForReading.Where((PawnKindDef k) => k.titleSelectOne != null && k.titleSelectOne.Contains(fixedTitle)).TryRandomElement(out result);
                }
            }
            else
            {
                fixedTitle = null;
            }

            if (result == null)
            {
                result = DefDatabase<PawnKindDef>.AllDefsListForReading.Where((PawnKindDef kind) => kind.race.race.Humanlike).RandomElement();
            }

            Pawn pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(result, faction, PawnGenerationContext.NonPlayer, -1, forceGenerateNewPawn: true, allowDead: false, allowDowned: false, canGeneratePawnRelations: true, mustBeCapableOfViolence: false, 1f, forceAddFreeWarmLayerIfNeeded: false, allowGay: true, allowPregnant: false, allowFood: true, allowAddictions: true, inhabitant: false, certainlyBeenInCryptosleep: false, forceRedressWorldPawnIfFormerColonist: false, worldPawnFactionDoesntMatter: false, 0f, 0f, null, 1f, null, null, null, null, null, null, null, null, null, null, fixedTitle));
            Find.WorldPawns.PassToWorld(pawn);
            if (pawn.royalty != null && pawn.royalty.AllTitlesForReading.Any())
            {
                QuestPart_Hyperlinks questPart_Hyperlinks = new QuestPart_Hyperlinks();
                questPart_Hyperlinks.pawns.Add(pawn);
                QuestGen.quest.AddPart(questPart_Hyperlinks);
            }

            return pawn;
        }
    }
}