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
    /// <summary>
    /// 主要是加入奶酪
    /// </summary>
    public class RKU_IncidentWorker_FinalRaid : IncidentWorker_RaidEnemy
    {
        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!TryGenerateRaidInfo(parms, out var pawns))
            {
                return false;
            }

            TaggedString letterLabel = GetLetterLabel(parms);
            TaggedString letterText = GetLetterText(parms, pawns);
            PawnRelationUtility.Notify_PawnsSeenByPlayer_Letter(pawns, ref letterLabel, ref letterText, GetRelatedPawnsInfoLetterText(parms), informEvenIfSeenBefore: true);
            List<TargetInfo> list = new List<TargetInfo>();
            if (parms.pawnGroups != null)
            {
                List<List<Pawn>> list2 = IncidentParmsUtility.SplitIntoGroups(pawns, parms.pawnGroups);
                List<Pawn> list3 = list2.MaxBy((List<Pawn> x) => x.Count);
                if (list3.Any())
                {
                    list.Add(list3[0]);
                }

                for (int i = 0; i < list2.Count; i++)
                {
                    if (list2[i] != list3 && list2[i].Any())
                    {
                        list.Add(list2[i][0]);
                    }
                }
            }
            else if (pawns.Any())
            {
                foreach (Pawn item in pawns)
                {
                    list.Add(item);
                }
            }

            SendStandardLetter(letterLabel, letterText, GetLetterDef(), parms, list);
            if (parms.controllerPawn == null || parms.controllerPawn.Faction != Faction.OfPlayer)
            {
                parms.raidStrategy.Worker.MakeLords(parms, pawns);
            }

            LessonAutoActivator.TeachOpportunity(ConceptDefOf.EquippingWeapons, OpportunityType.Critical);
            if (!PlayerKnowledgeDatabase.IsComplete(ConceptDefOf.ShieldBelts))
            {
                for (int j = 0; j < pawns.Count; j++)
                {
                    Pawn pawn = pawns[j];
                    if (pawn.apparel != null && pawn.apparel.WornApparel.Any((Apparel ap) => ap.def == ThingDefOf.Apparel_ShieldBelt))
                    {
                        LessonAutoActivator.TeachOpportunity(ConceptDefOf.ShieldBelts, OpportunityType.Critical);
                        break;
                    }
                }
            }

            if (DebugSettings.logRaidInfo)
            {
                Log.Message($"Raid: {parms.faction.Name} ({parms.faction.def.defName}) {parms.raidArrivalMode.defName} {parms.raidStrategy.defName} c={parms.spawnCenter} p={parms.points}");
            }

            if (!parms.silent)
            {
                Find.TickManager.slower.SignalForceNormalSpeedShort();
            }

            Find.StoryWatcher.statsRecord.numRaidsEnemy++;
            parms.target.StoryState.lastRaidFaction = parms.faction;
            return true;
        }
        public new bool TryGenerateRaidInfo(IncidentParms parms, out List<Pawn> pawns, bool debugTest = false)
        {
            pawns = null;
            ResolveRaidPoints(parms);
            if (!TryResolveRaidFaction(parms))
            {
                return false;
            }

            PawnGroupKindDef groupKind = parms.pawnGroupKind ?? PawnGroupKindDefOf.Combat;
            ResolveRaidStrategy(parms, groupKind);
            ResolveRaidArriveMode(parms);
            ResolveRaidAgeRestriction(parms);
            if (!debugTest)
            {
                parms.raidStrategy.Worker.TryGenerateThreats(parms);
            }

            if (!debugTest && !parms.raidArrivalMode.Worker.TryResolveRaidSpawnCenter(parms))
            {
                return false;
            }

            float points = parms.points;
            parms.points = AdjustedRaidPoints(parms.points, parms.raidArrivalMode, parms.raidStrategy, parms.faction, groupKind, parms.raidAgeRestriction);

            if (pawns == null)
            {
                PawnGroupMakerParms defaultPawnGroupMakerParms = IncidentParmsUtility.GetDefaultPawnGroupMakerParms(groupKind, parms);
                pawns = PawnGroupMakerUtility.GeneratePawns(defaultPawnGroupMakerParms).ToList();
                //加入奶酪和蜈蚣
                Faction rFaction = Find.FactionManager.FirstFactionOfDef(DefOfs.RKU_Faction);
                Pawn centiped = PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("Mech_CentipedeGunner"));
                centiped.equipment.DestroyAllEquipment();
                ThingWithComps weapon = (ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("RKU_IronStarCannon"), null);
                weapon.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Legendary, ArtGenerationContext.Outsider);
                centiped.equipment.AddEquipment(weapon);
                centiped.Name = new NameTriple("RKU_Iron".Translate(), "RKU_IronStar".Translate(), "RKU_Star".Translate());
                centiped.health.AddHediff(DefDatabase<HediffDef>.GetNamed("RKU_IronStarHediff"));
                centiped.SetFaction(rFaction);
                pawns.Add(centiped);
                pawns.Add(rFaction.leader);
                if (pawns.Count == 0)
                {
                    return false;
                }
                parms.raidArrivalMode.Worker.Arrive(pawns, parms);
            }

            parms.pawnCount = pawns.Count;
            PostProcessSpawnedPawns(parms, pawns);
            GenerateRaidLoot(parms, points, pawns);
            return true;
        }
    }
}
