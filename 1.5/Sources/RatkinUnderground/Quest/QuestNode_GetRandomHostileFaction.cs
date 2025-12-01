using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RatkinUnderground
{
    public class QuestNode_GetRandomHostileFaction : QuestNode
    {
        [NoTranslate]

        public SlateRef<string> storeAs;

        public SlateRef<bool> allowEnemy;

        public SlateRef<bool> allowNeutral;

        public SlateRef<bool> allowAlly;

        public SlateRef<bool> allowAskerFaction;

        public SlateRef<bool?> allowPermanentEnemy;

        public SlateRef<bool> mustBePermanentEnemy;

        public SlateRef<bool> playerCantBeAttackingCurrently;

        public SlateRef<bool> peaceTalksCantExist;

        public SlateRef<bool> leaderMustBeSafe;

        public SlateRef<bool> mustHaveGoodwillRewardsEnabled;

        public SlateRef<Pawn> ofPawn;

        public SlateRef<Thing> mustBeHostileToFactionOf;

        public SlateRef<IEnumerable<Faction>> exclude;

        public SlateRef<IEnumerable<Faction>> allowedHiddenFactions;
        protected override bool TestRunInt(Slate slate)
        {
            if (slate.TryGet<Faction>(storeAs.GetValue(slate), out var existingFaction) && existingFaction != null && Find.FactionManager.AllFactions
                .Where(f => !f.defeated && f.HostileTo(Faction.OfPlayer) && !f.Hidden)
                .ToList().Contains(existingFaction))
            {
                return true;
            }

            Faction faction = GetRandomHostileFaction();
            if (faction != null)
            {
                slate.Set(storeAs.GetValue(slate), faction);
                return true;
            }

            return false;
        }

        protected override void RunInt()
        {
            Slate slate = QuestGen.slate;
            Faction faction = GetRandomHostileFaction();

            bool needNewFaction = true;
            if (QuestGen.slate.TryGet<Faction>(storeAs.GetValue(slate), out var existingFaction) && existingFaction != null)
            {
                needNewFaction = !IsGoodFaction(existingFaction, QuestGen.slate);
            }

            if (needNewFaction && faction != null)
            {
                QuestGen.slate.Set(storeAs.GetValue(slate), faction);
                if (!faction.Hidden)
                {
                    QuestPart_InvolvedFactions questPart_InvolvedFactions = new QuestPart_InvolvedFactions();
                    questPart_InvolvedFactions.factions.Add(faction);
                    QuestGen.quest.AddPart(questPart_InvolvedFactions);
                }
            }
        }

        private Faction GetRandomHostileFaction()
        {
            Faction playerFaction = Faction.OfPlayer;
            if (playerFaction == null)
                return null;

            // 优先级1：王国如果敌对
            Faction rakiniaFaction = Find.FactionManager.AllFactions
                .FirstOrDefault(f => f.def.defName == "Rakinia" && !f.defeated && f.HostileTo(playerFaction) && !f.Hidden);

            if (rakiniaFaction != null)
                return rakiniaFaction;

            // 优先级2：军阀如果敌对
            Faction warlordFaction = Find.FactionManager.AllFactions
                .FirstOrDefault(f => f.def.defName == "Rakinia_Warlord" && !f.defeated && f.HostileTo(playerFaction) && !f.Hidden);

            if (warlordFaction != null)
                return warlordFaction;

            // 优先级3：任何其他敌对派系
            var hostileFactions = Find.FactionManager.AllFactions
                .Where(f => !f.defeated && f.HostileTo(playerFaction) && !f.Hidden)
                .ToList();

            if (hostileFactions.Any())
                return hostileFactions.RandomElement();

            // 如果没有敌对派系，尝试使用永久敌对派系
            var permanentEnemyFactions = Find.FactionManager.AllFactions
                .Where(f => !f.defeated && f.def.permanentEnemy && !f.Hidden)
                .ToList();

            if (permanentEnemyFactions.Any())
                return permanentEnemyFactions.RandomElement();

            var anyFactions = Find.FactionManager.AllFactions
                .Where(f => !f.defeated && f != playerFaction && !f.Hidden)
                .ToList();

            if (anyFactions.Any())
                return anyFactions.RandomElement();

            return null;
        }

        private bool IsGoodFaction(Faction faction, Slate slate)
        {
            if (faction == null)
                return false;

            if (faction.Hidden && (allowedHiddenFactions.GetValue(slate) == null || !allowedHiddenFactions.GetValue(slate).Contains(faction)))
            {
                return false;
            }

            if (ofPawn.GetValue(slate) != null && faction != ofPawn.GetValue(slate).Faction)
            {
                return false;
            }

            if (exclude.GetValue(slate) != null && exclude.GetValue(slate).Contains(faction))
            {
                return false;
            }

            if (mustBePermanentEnemy.GetValue(slate) && !faction.def.permanentEnemy)
            {
                return false;
            }

            if (!allowEnemy.GetValue(slate) && faction.HostileTo(Faction.OfPlayer))
            {
                return false;
            }

            if (!allowNeutral.GetValue(slate) && faction.PlayerRelationKind == FactionRelationKind.Neutral)
            {
                return false;
            }

            if (!allowAlly.GetValue(slate) && faction.PlayerRelationKind == FactionRelationKind.Ally)
            {
                return false;
            }

            bool? value = allowPermanentEnemy.GetValue(slate);
            if (value.HasValue && !value.GetValueOrDefault() && faction.def.permanentEnemy)
            {
                return false;
            }

            if (playerCantBeAttackingCurrently.GetValue(slate) && SettlementUtility.IsPlayerAttackingAnySettlementOf(faction))
            {
                return false;
            }

            if (mustHaveGoodwillRewardsEnabled.GetValue(slate) && !faction.allowGoodwillRewards)
            {
                return false;
            }

            if (peaceTalksCantExist.GetValue(slate))
            {
                if (PeaceTalksExist(faction))
                {
                    return false;
                }

                string tag = QuestNode_QuestUnique.GetProcessedTag("PeaceTalks", faction);
                if (Find.QuestManager.questsInDisplayOrder.Any((Quest q) => q.tags.Contains(tag)))
                {
                    return false;
                }
            }

            if (leaderMustBeSafe.GetValue(slate) && (faction.leader == null || faction.leader.Spawned || faction.leader.IsPrisoner))
            {
                return false;
            }

            Thing value2 = mustBeHostileToFactionOf.GetValue(slate);
            if (value2 != null && value2.Faction != null && (value2.Faction == faction || !faction.HostileTo(value2.Faction)))
            {
                return false;
            }

            return true;
        }

        private bool PeaceTalksExist(Faction faction)
        {
            List<PeaceTalks> peaceTalks = Find.WorldObjects.PeaceTalks;
            for (int i = 0; i < peaceTalks.Count; i++)
            {
                if (peaceTalks[i].Faction == faction)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
