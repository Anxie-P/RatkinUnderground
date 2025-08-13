using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace RatkinUnderground
{
    public class JobDriver_EatMindspark : JobDriver
    {
        const TargetIndex UseTarget = TargetIndex.A;

        // 给与灵感
        void TryGiveInspiration(Pawn pawn)
        {
            if (pawn?.mindState?.inspirationHandler == null) return;

            List<InspirationDef> possibleInspirations = DefDatabase<InspirationDef>.AllDefsListForReading;
            int maxLevel = pawn.skills.skills.Max(sr => sr.levelInt);
            var topSkillDefs = pawn.skills.skills
                .Where(sr => sr.levelInt>=3)
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
                if (Utils.InspirationMapper.SkillToInspirationMap.TryGetValue(skillDef, out var insps)
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
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            Pawn pawn = this.pawn;
            LocalTargetInfo target = job.GetTarget(UseTarget);
            return pawn.Reserve(target, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(UseTarget);
            this.FailOnForbidden(UseTarget);

            yield return Toils_Goto.GotoThing(UseTarget, PathEndMode.ClosestTouch);

            int durationTicks = 240;
            int ticksLeft = durationTicks;

            Toil wait = Toils_General.Wait(durationTicks, UseTarget);

            wait.tickAction = delegate
            {
                // ticksLeft 自减，供 progress lambda 使用
                ticksLeft = Mathf.Max(0, ticksLeft - 1);

                // 每隔一段时间播放一次“咀嚼/进食”音效 —— 这里选 30 ticks（0.5s）为例
                if (pawn.IsHashIntervalTick(30))
                {
                    // 推荐使用核心 SoundDef 名：Interact_Ingest（若你 mod/版本不同，请改为相应名字）
                    var sdef = SoundDef.Named("Interact_Ingest");
                    if (sdef != null)
                    {
                        // 在镜头上播放（通用、不会因为 pawn 不在可听范围而错过）
                        SoundStarter.PlayOneShotOnCamera(sdef, pawn.Map);
                    }
                }
                // 可选：中途做取消条件检查（例如被打断时不应该继续）
                // if (pawn.Downed || pawn.InMentalState) EndCurrentToil(); // 依据需要开启
            };

            // 显示进度
            wait.WithProgressBar(UseTarget, () => 1f - (float)ticksLeft / durationTicks, false, -1);

            wait.AddFinishAction(() =>
            {
                Pawn actor = pawn;
                Thing t = job.GetTarget(UseTarget).Thing;
                if (t != null)
                {
                    Comp_Mindspark comp = t.TryGetComp<Comp_Mindspark>();
                    if (comp != null)
                    {
                        Log.Message("存在mindspark");
                    }
                    t.Destroy(DestroyMode.Vanish);
                    TryGiveInspiration(pawn);
                }
            });

            wait.defaultCompleteMode = ToilCompleteMode.Delay;

            yield return wait;
        }
    }
}
