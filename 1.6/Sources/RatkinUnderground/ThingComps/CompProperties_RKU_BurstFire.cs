using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RatkinUnderground
{
    public class CompProperties_RKU_BurstFire : CompProperties
    {
        public int burstShotCount = 25; // 连续射击次数
        public int disableDurationTicks = 600; // 10秒禁用时间（600tick）
        public int cooldownTicks = 1800; // 30秒冷却时间（1800tick）
        public int ticksBetweenShots = 0; // 每次射击之间的间隔（tick），0表示使用武器自身的冷却时间
        public string labelKey = "RKU_BurstFireLabel";
        public string descriptionKey = "RKU_BurstFireDescription";
        public string iconPath = "UI/Commands/Attack";

        public CompProperties_RKU_BurstFire()
        {
            compClass = typeof(Comp_RKU_BurstFire);
        }
    }

    public class Comp_RKU_BurstFire : ThingComp
    {
        private int lastUseTick = -1;
        private int disableUntilTick = -1;
        private int shotsRemaining = 0;
        private int nextShotTick = -1;
        private LocalTargetInfo burstTarget = LocalTargetInfo.Invalid;
        private Pawn weaponHolder = null;

        public CompProperties_RKU_BurstFire Props => (CompProperties_RKU_BurstFire)props;

        public bool IsWeaponDisabled => disableUntilTick > 0 && Find.TickManager.TicksGame < disableUntilTick;
        public bool IsBurstFiring => shotsRemaining > 0;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref lastUseTick, "lastUseTick", -1);
            Scribe_Values.Look(ref disableUntilTick, "disableUntilTick", -1);
            Scribe_Values.Look(ref shotsRemaining, "shotsRemaining", 0);
            Scribe_Values.Look(ref nextShotTick, "nextShotTick", -1);
            Scribe_TargetInfo.Look(ref burstTarget, "burstTarget");
        }

        public override void CompTick()
        {
            base.CompTick();
            if (parent is ThingWithComps weapon)
            {
                var compEquippable = weapon.TryGetComp<CompEquippable>();
                if (compEquippable != null && compEquippable.PrimaryVerb != null)
                {
                    weaponHolder = compEquippable.PrimaryVerb.Caster as Pawn;
                }
                else
                {
                    weaponHolder = null;
                }
            }

            // 处理连续射击
            if (shotsRemaining > 0 && weaponHolder != null && weaponHolder.Spawned)
            {
                ProcessBurstFire();
            }

            // 检查是否应该解除禁用状态
            if (disableUntilTick > 0 && Find.TickManager.TicksGame >= disableUntilTick)
            {
                disableUntilTick = -1;
            }
        }

        private void ProcessBurstFire()
        {
            if (weaponHolder == null || !weaponHolder.Spawned)
            {
                shotsRemaining = 0;
                nextShotTick = -1;
                return;
            }

            var weapon = parent as ThingWithComps;
            if (weapon == null)
            {
                shotsRemaining = 0;
                return;
            }

            var compEquippable = weapon.TryGetComp<CompEquippable>();
            if (compEquippable == null)
            {
                shotsRemaining = 0;
                return;
            }

            var verb = compEquippable.PrimaryVerb;
            if (verb == null)
            {
                shotsRemaining = 0;
                return;
            }

            // 检查是否到了下一次射击的时间
            if (nextShotTick > 0 && Find.TickManager.TicksGame < nextShotTick)
            {
                return;
            }

            // 如果verb正在连发中，等待连发完成
            if (verb.state == VerbState.Bursting)
            {
                // 设置下次检查时间
                nextShotTick = Find.TickManager.TicksGame + 5;
                return;
            }

            // 检查verb状态（必须在Idle状态）
            if (verb.state != VerbState.Idle)
            {
                nextShotTick = Find.TickManager.TicksGame + 5;
                return;
            }

            if (!verb.Available())
            {
                if (IsWeaponDisabled)
                {
                    shotsRemaining = 0;
                    nextShotTick = -1;
                    return;
                }
                nextShotTick = Find.TickManager.TicksGame + 5;
                return;
            }

            if (!burstTarget.IsValid || !verb.CanHitTarget(burstTarget))
            {
                shotsRemaining = 0;
                nextShotTick = -1;
                return;
            }

            // 执行射击
            bool shotFired = verb.TryStartCastOn(burstTarget);
            
            if (shotFired)
            {
                // 计算这次连发会射出多少发子弹
                int shotsPerBurst = verb.verbProps?.burstShotCount ?? 1;
                shotsRemaining -= shotsPerBurst;
                
                // 如果剩余发数小于等于0，说明已经完成所有射击
                if (shotsRemaining <= 0)
                {
                    shotsRemaining = 0;
                    // 所有射击完成，开始禁用期
                    disableUntilTick = Find.TickManager.TicksGame + Props.disableDurationTicks;
                    nextShotTick = -1;
                }
                else
                {
                    // 设置下次射击时间
                    // 在连续射击期间，使用连发完成时间作为间隔，不等待武器冷却
                    int burstDuration = (shotsPerBurst - 1) * (verb.verbProps?.ticksBetweenBurstShots ?? 5);
                    
                    // 如果配置了间隔时间，使用配置值；否则使用连发完成时间加上少量缓冲
                    int shotInterval;
                    if (Props.ticksBetweenShots > 0)
                    {
                        shotInterval = Props.ticksBetweenShots;
                    }
                    else
                    {
                        // 使用连发完成时间，加上少量缓冲（确保连发完全结束）
                        shotInterval = burstDuration + 10; // 10 ticks缓冲
                    }
                    nextShotTick = Find.TickManager.TicksGame + shotInterval;
                }
            }
            else
            {
                // 射击失败，等待后重试
                nextShotTick = Find.TickManager.TicksGame + 5;
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            // 只在武器被装备且持有者是玩家殖民者时显示
            if (weaponHolder != null && weaponHolder.IsColonist)
            {
                Command_Action burstFireCommand = new Command_Action
                {
                    defaultLabel = Props.labelKey.Translate(),
                    defaultDesc = Props.descriptionKey.Translate(),
                    icon = ContentFinder<Texture2D>.Get(Props.iconPath, false) ?? BaseContent.BadTex,
                    hotKey = KeyBindingDefOf.Misc3,
                    action = delegate
                    {
                        ActivateBurstFire();
                    }
                };

                // 检查冷却时间
                if (lastUseTick != -1 && Find.TickManager.TicksGame - lastUseTick < Props.cooldownTicks)
                {
                    int remainingTicks = Props.cooldownTicks - (Find.TickManager.TicksGame - lastUseTick);
                    string cooldownText = "RKU_BurstFireCooldown".Translate();
                    if (cooldownText == "RKU_BurstFireCooldown")
                    {
                        cooldownText = $"技能冷却中，剩余 {remainingTicks / 60f:F1} 秒";
                    }
                    burstFireCommand.Disable(cooldownText);
                }
                // 检查是否在禁用期
                else if (IsWeaponDisabled)
                {
                    int remainingTicks = disableUntilTick - Find.TickManager.TicksGame;
                    string disabledText = "RKU_BurstFireDisabled".Translate();
                    if (disabledText == "RKU_BurstFireDisabled")
                    {
                        disabledText = $"武器过热中，剩余 {remainingTicks / 60f:F1} 秒";
                    }
                    burstFireCommand.Disable(disabledText);
                }
                // 检查是否正在射击
                else if (IsBurstFiring)
                {
                    string activeText = "RKU_BurstFireActive".Translate();
                    if (activeText == "RKU_BurstFireActive")
                    {
                        activeText = $"正在连续射击中... ({shotsRemaining}发剩余)";
                    }
                    burstFireCommand.Disable(activeText);
                }

                yield return burstFireCommand;
            }
        }

        private void ActivateBurstFire()
        {
            if (weaponHolder == null || !weaponHolder.Spawned)
            {
                string msg = "RKU_BurstFireNoHolder".Translate();
                if (msg == "RKU_BurstFireNoHolder")
                {
                    msg = "武器未被装备";
                }
                Messages.Message(msg, MessageTypeDefOf.RejectInput);
                return;
            }

            var weapon = parent as ThingWithComps;
            if (weapon == null) return;

            var compEquippable = weapon.TryGetComp<CompEquippable>();
            if (compEquippable == null) return;

            var verb = compEquippable.PrimaryVerb;
            if (verb == null)
            {
                string msg = "RKU_BurstFireNoVerb".Translate();
                if (msg == "RKU_BurstFireNoVerb")
                {
                    msg = "武器没有攻击能力";
                }
                Messages.Message(msg, MessageTypeDefOf.RejectInput);
                return;
            }

            // 开始目标选择
            Find.Targeter.BeginTargeting(new TargetingParameters
            {
                canTargetPawns = true,
                canTargetBuildings = true,
                canTargetItems = false,
                canTargetLocations = false,
                mapObjectTargetsMustBeAutoAttackable = true
            }, delegate (LocalTargetInfo target)
            {
                if (verb.CanHitTarget(target))
                {
                    burstTarget = target;
                    shotsRemaining = Props.burstShotCount;
                    lastUseTick = Find.TickManager.TicksGame;
                    nextShotTick = Find.TickManager.TicksGame;
                    // 立即开始第一次射击
                    ProcessBurstFire();
                }
                else
                {
                    string msg = "RKU_BurstFireInvalidTarget".Translate();
                    if (msg == "RKU_BurstFireInvalidTarget")
                    {
                        msg = "无法攻击该目标";
                    }
                    Messages.Message(msg, MessageTypeDefOf.RejectInput);
                }
            });
        }
    }
}

