using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

namespace RatkinUnderground
{
    public class CompProperties_Mindspark : CompProperties
    {
        public CompProperties_Mindspark()
        {
            compClass = typeof(Comp_Mindspark);
        }
    }

    public class Comp_Mindspark : ThingComp,
        ISpecialMushroom
    {
        public CompProperties_Mindspark Props => (CompProperties_Mindspark)props;

        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
        {
            foreach (var opt in base.CompFloatMenuOptions(selPawn))
                yield return opt;

            string label = "食用: " + parent.LabelCap;

            if (!selPawn.CanReach(parent, PathEndMode.ClosestTouch,Danger.None))
            {
                yield return new FloatMenuOption("无法到达" + parent.LabelCap, null);
                yield break;
            }

            if (!selPawn.CanReserve(parent))
            {
                yield return new FloatMenuOption(parent.LabelCap + "已被占用", null);
                yield break;
            }

            yield return new FloatMenuOption(label, delegate
            {
                Log.Message("已执行食用");
                Job job = JobMaker.MakeJob(DefOfs.RKU_EatSpecialMushroom, parent);
                selPawn.jobs.TryTakeOrderedJob(job);
            });
        }

        public void TryAddEffect(Pawn pawn)
        {
            if (pawn?.mindState?.inspirationHandler == null) return;

            List<InspirationDef> possibleInspirations = DefDatabase<InspirationDef>.AllDefsListForReading;
            int maxLevel = pawn.skills.skills.Max(sr => sr.levelInt);
            var topSkillDefs = pawn.skills.skills
                .Where(sr => sr.levelInt >= 3)
                .OrderByDescending(sr => sr.levelInt)
                .Select(sr => sr.def)
                .ToList();
            foreach (var skill in topSkillDefs)
            {
                Log.Message($"最高技能:{skill}");
            }

            InspirationDef chosenInspiration = null;
            foreach (var skillDef in topSkillDefs)
            {
                if (InspirationMapper.SkillToInspirationMap.TryGetValue(skillDef, out var insps)
                    && insps != null && insps.Length > 0)
                {
                    // 记录并退出循环
                    foreach (var insp in insps)
                    {
                        Log.Message($"映射灵感: {insp}");
                    }
                    chosenInspiration = insps.RandomElement();
                    break;
                }
            }

            if (chosenInspiration == null)
            {
                Log.Message("chosenInspiration为空，随机灵感");
                chosenInspiration = DefDatabase<InspirationDef>.AllDefsListForReading.RandomElement();
            }
            pawn.mindState.inspirationHandler.TryStartInspiration(chosenInspiration);
        }
    }
}