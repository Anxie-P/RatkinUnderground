using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RatkinUnderground
{
    public class RKU_Verb_ShootAllAtOnce : Verb_Shoot
    {
        private bool hasFiredAllShots = false;

        public override void WarmupComplete()
        {
            base.WarmupComplete();
            hasFiredAllShots = false;
        }

        protected override bool TryCastShot()
        {
            if (hasFiredAllShots)
            {
                return false;
            }

            hasFiredAllShots = true;

            if (currentTarget.HasThing && currentTarget.Thing.Map != caster.Map)
            {
                return false;
            }

            ThingDef projectile = Projectile;
            if (projectile == null)
            {
                return false;
            }

            ShootLine resultingLine;
            bool flag = TryFindShootLineFromTo(caster.Position, currentTarget, out resultingLine);
            if (verbProps.stopBurstWithoutLos && !flag)
            {
                return false;
            }

            if (base.EquipmentSource != null)
            {
                base.EquipmentSource.GetComp<CompChangeableProjectile>()?.Notify_ProjectileLaunched();
                base.EquipmentSource.GetComp<CompApparelVerbOwner_Charged>()?.UsedOnce();
            }

            lastShotTick = Find.TickManager.TicksGame;
            Thing manningPawn = caster;
            Thing equipmentSource = base.EquipmentSource;
            CompMannable compMannable = caster.TryGetComp<CompMannable>();
            if (compMannable?.ManningPawn != null)
            {
                manningPawn = compMannable.ManningPawn;
                equipmentSource = caster;
            }

            Vector3 drawPos = caster.DrawPos;

            int shotsToFire = verbProps.burstShotCount;
            for (int shotIndex = 0; shotIndex < shotsToFire; shotIndex++)
            {
                Projectile projectile2 = (Projectile)GenSpawn.Spawn(projectile, resultingLine.Source, caster.Map);

                if (equipmentSource.TryGetComp(out CompUniqueWeapon comp))
                {
                    foreach (WeaponTraitDef item in comp.TraitsListForReading)
                    {
                        if (item.damageDefOverride != null)
                        {
                            projectile2.damageDefOverride = item.damageDefOverride;
                        }

                        if (!item.extraDamages.NullOrEmpty())
                        {
                            Projectile projectile3 = projectile2;
                            if (projectile3.extraDamages == null)
                            {
                                projectile3.extraDamages = new List<ExtraDamage>();
                            }

                            projectile2.extraDamages.AddRange(item.extraDamages);
                        }
                    }
                }

                if (verbProps.ForcedMissRadius > 0.5f)
                {
                    float num = verbProps.ForcedMissRadius;
                    if (manningPawn is Pawn pawn)
                    {
                        num *= verbProps.GetForceMissFactorFor(equipmentSource, pawn);
                    }

                    float num2 = VerbUtility.CalculateAdjustedForcedMiss(num, currentTarget.Cell - caster.Position);
                    if (num2 > 0.5f)
                    {
                        IntVec3 forcedMissTarget = GetForcedMissTarget(num2);
                        if (forcedMissTarget != currentTarget.Cell)
                        {
                            ProjectileHitFlags projectileHitFlags = ProjectileHitFlags.NonTargetWorld;
                            if (Rand.Chance(0.5f))
                            {
                                projectileHitFlags = ProjectileHitFlags.All;
                            }

                            if (!canHitNonTargetPawnsNow)
                            {
                                projectileHitFlags &= ~ProjectileHitFlags.NonTargetPawns;
                            }

                            projectile2.Launch(manningPawn, drawPos, forcedMissTarget, currentTarget, projectileHitFlags, preventFriendlyFire, equipmentSource);
                            continue;
                        }
                    }
                }

                ShotReport shotReport = ShotReport.HitReportFor(caster, this, currentTarget);
                Thing randomCoverToMissInto = shotReport.GetRandomCoverToMissInto();
                ThingDef targetCoverDef = randomCoverToMissInto?.def;
                if (verbProps.canGoWild && !Rand.Chance(shotReport.AimOnTargetChance_IgnoringPosture))
                {
                    bool flyOverhead = projectile2?.def?.projectile != null && projectile2.def.projectile.flyOverhead;
                    resultingLine.ChangeDestToMissWild(shotReport.AimOnTargetChance_StandardTarget, flyOverhead, caster.Map);
                    ProjectileHitFlags projectileHitFlags2 = ProjectileHitFlags.NonTargetWorld;
                    if (Rand.Chance(0.5f) && canHitNonTargetPawnsNow)
                    {
                        projectileHitFlags2 |= ProjectileHitFlags.NonTargetPawns;
                    }

                    projectile2.Launch(manningPawn, drawPos, resultingLine.Dest, currentTarget, projectileHitFlags2, preventFriendlyFire, equipmentSource, targetCoverDef);
                    continue;
                }

                if (currentTarget.Thing != null && currentTarget.Thing.def.CanBenefitFromCover && !Rand.Chance(shotReport.PassCoverChance))
                {
                    ProjectileHitFlags projectileHitFlags3 = ProjectileHitFlags.NonTargetWorld;
                    if (canHitNonTargetPawnsNow)
                    {
                        projectileHitFlags3 |= ProjectileHitFlags.NonTargetPawns;
                    }

                    projectile2.Launch(manningPawn, drawPos, randomCoverToMissInto, currentTarget, projectileHitFlags3, preventFriendlyFire, equipmentSource, targetCoverDef);
                    continue;
                }

                ProjectileHitFlags projectileHitFlags4 = ProjectileHitFlags.IntendedTarget;
                if (canHitNonTargetPawnsNow)
                {
                    projectileHitFlags4 |= ProjectileHitFlags.NonTargetPawns;
                }

                if (!currentTarget.HasThing || currentTarget.Thing.def.Fillage == FillCategory.Full)
                {
                    projectileHitFlags4 |= ProjectileHitFlags.NonTargetWorld;
                }

                if (currentTarget.Thing != null)
                {
                    projectile2.Launch(manningPawn, drawPos, currentTarget, currentTarget, projectileHitFlags4, preventFriendlyFire, equipmentSource, targetCoverDef);
                }
                else
                {
                    projectile2.Launch(manningPawn, drawPos, resultingLine.Dest, currentTarget, projectileHitFlags4, preventFriendlyFire, equipmentSource, targetCoverDef);
                }
            }

            if (shotsToFire > 0)
            {
                if (shotsToFire > 1)
                {
                    Find.BattleLog.Add(new BattleLogEntry_RangedFire(caster, currentTarget.HasThing ? currentTarget.Thing : null, base.EquipmentSource != null ? base.EquipmentSource.def : null, projectile, true));
                }
                else
                {
                    Find.BattleLog.Add(new BattleLogEntry_RangedFire(caster, currentTarget.HasThing ? currentTarget.Thing : null, base.EquipmentSource != null ? base.EquipmentSource.def : null, projectile, false));
                }
            }

            return true;
        }
    }
}
