using AlienRace;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.Noise;

namespace RatkinUnderground
{
    public class RKU_FactionComponent : GameComponent
    {
        List<FactionDef> enemyFaction = new List<FactionDef>
        {
            FactionDef.Named("Rakinia_Warlord"),
            FactionDef.Named("Rakinia")
        };

        public RKU_FactionComponent(Game game) { }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            SetPermanentEnemies();
        }

        public override void StartedNewGame()
        {
            base.StartedNewGame();
            SetPermanentEnemies();
        }

        public override void LoadedGame()
        {
            base.LoadedGame();
            SetPermanentEnemies();
        }
        public void SetPermanentEnemies()
        {
            Faction rFaction = Find.FactionManager.FirstFactionOfDef(DefOfs.RKU_Faction);
            if (rFaction == null) return;

            if (rFaction.leader != null)
            {
                rFaction.leader.Name = new NameTriple("RKU_Zigstark".Translate(), null, "RKU_Cheesecellar".Translate());
                SetFactionLeaderTitle(rFaction, "RKU_commanderTitle".Translate());
                rFaction.leader.gender=Gender.Female;

                // 检查是否是经典模式,妈的泰南
                bool allFactionsSameCulture = Find.FactionManager.AllFactions
                    .Where(f => f.ideos != null && f.ideos.PrimaryIdeo != null)
                    .Select(f => f.ideos.PrimaryIdeo)
                    .Distinct()
                    .Count() == 1;

                if (!allFactionsSameCulture)
                {
                    rFaction.ideos.PrimaryIdeo.leaderTitleMale = "RKU_commanderTitle".Translate();
                    rFaction.ideos.PrimaryIdeo.leaderTitleFemale = "RKU_commanderTitle".Translate();
                }
                
                rFaction.leader.story.Childhood = DefDatabase<AlienRace.AlienBackstoryDef>.GetNamed("Ratkin_GuerrillaCT");
                rFaction.leader.story.Adulthood = DefDatabase<AlienRace.AlienBackstoryDef>.GetNamed("RKU_GuerrillaAR");
                // 设置指挥官年龄为43岁\装备武器hedoiff
                SetPawnAge(rFaction.leader, 43);
                
                // 清空所有特性并添加坚韧和工作狂特质
                SetCommanderTraits(rFaction.leader);
                //沙。。。我真受不了了
                if (ModsConfig.IsActive("OARK.RatkinFaction.GeneExpand"))
                {
                    for (int i = 0; i < rFaction.leader.genes.GenesListForReading.Count; i++)
                    {
                        rFaction.leader.genes.RemoveGene(rFaction.leader.genes.GenesListForReading[i]);
                    }
                    rFaction.leader.genes.SetXenotype(DefDatabase<XenotypeDef>.GetNamed("OAGene_RatkinBase")) ;
                }
                rFaction.leader.story.hairDef = DefDatabase<HairDef>.GetNamed("RKU_CommanderHair");
                rFaction.leader.story.HairColor = new UnityEngine.Color(236, 222, 227);
                rFaction.leader.equipment?.DestroyAllEquipment();
                ThingWithComps weaponL = (ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("RKU_SVT40M_Elite"), null);
                weaponL.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Legendary, ArtGenerationContext.Outsider);
                rFaction.leader.equipment?.AddEquipment(weaponL);
                // 保证leader技能保底
                EnsureMinimumSkills(rFaction.leader);
                // 给rFaction.leader加入MechlinkImplant（如果存在的话）
                HediffDef mechlinkDef = DefDatabase<HediffDef>.GetNamedSilentFail("MechlinkImplant");
                if (mechlinkDef != null && !rFaction.leader.health.hediffSet.HasHediff(mechlinkDef))
                {
                    rFaction.leader.health.AddHediff(mechlinkDef);
                }
            }
            foreach (FactionDef enemy in enemyFaction)
            {
                Faction eFaction = Find.FactionManager.FirstFactionOfDef(enemy);
                eFaction.RelationWith(rFaction).baseGoodwill = -100;
                rFaction.RelationWith(eFaction).baseGoodwill = -100;
                FactionRelationKind oldKind1 = eFaction.RelationWith(rFaction).kind;
                FactionRelationKind oldKind2 = rFaction.RelationWith(eFaction).kind;
                eFaction.RelationWith(rFaction).kind = FactionRelationKind.Hostile;
                rFaction.RelationWith(eFaction).kind = FactionRelationKind.Hostile;
                eFaction.Notify_RelationKindChanged(rFaction, oldKind1, false, "", TargetInfo.Invalid, out var sentLetter1);
                rFaction.Notify_RelationKindChanged(eFaction, oldKind2, false, "", TargetInfo.Invalid, out var sentLetter2);
            }
        }

        private void SetFactionLeaderTitle(Faction faction, string title)
        {
            var leaderTitleField = typeof(Faction).GetField("leaderTitle", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (leaderTitleField != null)
            {
                leaderTitleField.SetValue(faction, title);
            }
        }

        private void SetPawnAge(Pawn pawn, int ageInYears)
        {
            if (pawn == null || pawn.ageTracker == null) return;

            // 设置生物年龄为43岁
            long currentAbsTicks = Find.TickManager.TicksAbs;
            long ticksPerYear = 3600000L; // RimWorld中每年3600000 ticks
            long birthAbsTicks = currentAbsTicks - (ageInYears * ticksPerYear);
            var birthAbsTicksField = typeof(Pawn_AgeTracker).GetField("birthAbsTicks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (birthAbsTicksField != null)
            {
                birthAbsTicksField.SetValue(pawn.ageTracker, birthAbsTicks);
            }
        }

        private void EnsureMinimumSkills(Pawn pawn)
        {
            if (pawn.skills == null)
                return;

            // 技能保底要求
            var skillRequirements = new Dictionary<SkillDef, int>
            {
                { SkillDefOf.Shooting, 15 },
                { SkillDefOf.Melee, 5 },
                { SkillDefOf.Social, 15 },
                { SkillDefOf.Intellectual, 15 },
                { SkillDefOf.Mining, 5 },
                { SkillDefOf.Crafting, 10 }
            };

            foreach (var kvp in skillRequirements)
            {
                SkillRecord skill = pawn.skills.GetSkill(kvp.Key);
                if (skill != null && skill.Level < kvp.Value)
                {
                    skill.Level = kvp.Value;
                }
            }
        }

        private void SetCommanderTraits(Pawn pawn)
        {
            if (pawn == null || pawn.story == null || pawn.story.traits == null)
                return;
            var traitsToRemove = pawn.story.traits.allTraits.ToList();
            foreach (var trait in traitsToRemove)
            {
                pawn.story.traits.RemoveTrait(trait);
            }
            TraitDef toughDef = DefDatabase<TraitDef>.GetNamedSilentFail("Tough");
            if (toughDef != null)
            {
                pawn.story.traits.GainTrait(new Trait(toughDef));
            }
            TraitDef industriousDef = DefDatabase<TraitDef>.GetNamedSilentFail("Industriousness");
            if (industriousDef != null)
            {
                pawn.story.traits.GainTrait(new Trait(industriousDef, 2));
            }
        }
    }
}
