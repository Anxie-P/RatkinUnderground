using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;

namespace RatkinUnderground
{
    public class RKU_IncidentWorker_IvanComing : IncidentWorker
    {
        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;

            List<Pawn> saboteurs = SpawnSaboteurs(map, 3);
            if (saboteurs == null || saboteurs.Count == 0) {
                return false;
            }
            AssignBombJobs(saboteurs);
            SoundDef.Named("RKU_EvanHaha").PlayOneShot(new TargetInfo(map.Center, map));
            return true;
        }

        private List<Pawn> SpawnSaboteurs(Map map, int count)
        {
            List<Pawn> saboteurs = new List<Pawn>();
            List<Building> availableTargets = FindBombTargets(map, count);

            if (availableTargets == null || availableTargets.Count == 0)
            {
                return saboteurs;
            }

            for (int i = 0; i < count; i++)
            {
                PawnKindDef pawnKind = DefOfs.RKU_Scout;
                if (pawnKind == null)
                {
                    continue;
                }

                PawnGenerationRequest request = new PawnGenerationRequest(
                    pawnKind,
                    faction: null,
                    forceGenerateNewPawn: false,
                    canGeneratePawnRelations: true,
                    allowAddictions: false
                );

                Pawn saboteur = PawnGenerator.GeneratePawn(request);
                if (saboteur == null)
                {
                    continue;
                }

                // 禁用自动攻击
                if (saboteur.mindState?.mentalStateHandler != null)
                {
                    saboteur.mindState.mentalStateHandler.neverFleeIndividual = true;
                }
                saboteur.mindState.duty = new PawnDuty(DutyDefOf.Steal);

                // 为这个破坏者选择目标（循环使用可用目标）
                Building target = availableTargets[i % availableTargets.Count];

                // 在地图边缘生成，能够到达目标的位置
                if (target != null && target.Position.IsValid && map.reachability != null && CellFinder.TryFindRandomEdgeCellWith(
                    c => c.Standable(map) && map.reachability.CanReachNonLocal(c, target, PathEndMode.Touch, TraverseMode.PassDoors, Danger.Deadly),
                    map,
                    CellFinder.EdgeRoadChance_Neutral,
                    out IntVec3 spawnPos))
                {
                    GenSpawn.Spawn(saboteur, spawnPos, map);
                    saboteurs.Add(saboteur);
                }
            }

            return saboteurs;
        }

        private List<Building> FindBombTargets(Map map, int count)
        {
            List<Building> targets = new List<Building>();

            // 寻找符合条件的房间
            List<Room> candidateRooms = map.regionGrid.allRooms
                .Where(room => room.GetStat(RoomStatDefOf.Wealth) > 1300f)
                .OrderByDescending(room => room.GetStat(RoomStatDefOf.Wealth))
                .ToList();

            if (candidateRooms.Count == 0) return targets;

            // 从每个房间中收集目标建筑
            foreach (Room room in candidateRooms)
            {
                if (targets.Count >= count) break;

                List<Building> roomBuildings = room.ContainedAndAdjacentThings
                    .OfType<Building>()
                    .Where(building =>
                        building != null &&
                        building.def != null &&
                        building.def.building != null &&
                        (HasBeauty(building) || IsProductionBuilding(building)))
                    .OrderByDescending(building => building.MarketValue)
                    .ToList();

                foreach (Building building in roomBuildings)
                {
                    if (targets.Count >= count) break;
                    if (!targets.Contains(building))
                    {
                        targets.Add(building);
                    }
                }
            }

            return targets;
        }

        private bool HasBeauty(Building building)
        {
            if (building.def.statBases == null) return false;

            var beautyStat = building.def.statBases.FirstOrDefault(o => o.stat != null && o.stat.defName == "Beauty");
            return beautyStat != null && beautyStat.value > 20;
        }

        private bool IsProductionBuilding(Building building)
        {
            if (building.def.thingCategories == null) return false;

            var productionCategory = DefDatabase<ThingCategoryDef>.GetNamed("BuildingsProduction", false);
            return productionCategory != null && building.def.thingCategories.Contains(productionCategory);
        }
        
        private void AssignBombJobs(List<Pawn> saboteurs)
        {
            List<Building> targets = FindBombTargets(saboteurs[0].Map, saboteurs.Count);

            for (int i = 0; i < saboteurs.Count; i++)
            {
                Pawn saboteur = saboteurs[i];
                Building target = targets.Count > i ? targets[i] : targets[targets.Count - 1];

                Job job = JobMaker.MakeJob(
                    DefOfs.RKU_SetC4,
                    target
                );

                // 强制开始工作
                saboteur.jobs.StartJob(job, JobCondition.InterruptForced);
            }
        }
        
        private void SendNotification(Map map, int saboteurCount, Building bombTarget)
        {
            string letterText = $"RKU_IvanComing_LetterText".Translate(saboteurCount);
            string letterLabel = "RKU_IvanComing_LetterLabel".Translate();
            
            Find.LetterStack.ReceiveLetter(
                letterLabel,
                letterText,
                LetterDefOf.ThreatSmall,
                new TargetInfo(bombTarget.Position, map)
            );
        }
    }
}