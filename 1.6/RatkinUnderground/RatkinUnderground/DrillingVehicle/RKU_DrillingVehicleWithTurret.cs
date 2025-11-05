using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using UnityEngine;

namespace RatkinUnderground
{
    public class RKU_DrillingVehicleWithTurret : RKU_DrillingVehicle, IAttackTargetSearcher
    {
        // 炮塔核心组件
        protected LocalTargetInfo forcedTarget = LocalTargetInfo.Invalid;
        private LocalTargetInfo lastAttackedTarget;
        private int lastAttackTargetTick;
        private bool holdFire;
        private LocalTargetInfo currentTargetInt = LocalTargetInfo.Invalid;
        private TurretTop top;
        private Thing gun;
        protected int burstWarmupTicksLeft;
        protected int burstCooldownTicksLeft;

        public RKU_DrillingVehicleWithTurret()
        {
            passengers = new ThingOwner<Pawn>(this);
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            if (def.building.turretGunDef != null)
            {
                gun = ThingMaker.MakeThing(def.building.turretGunDef);
                var comp = gun.TryGetComp<CompEquippable>();
                if (comp != null && comp.PrimaryVerb != null)
                {
                    comp.PrimaryVerb.caster = this;
                }
            }
            top = new TurretTop(this);
            top.SetRotationFromOrientation();
            if (!respawningAfterLoad)
            {
                burstCooldownTicksLeft = (int)(def.building.turretBurstCooldownTime * 60f);
            }
            UpdateGunVerbs();
        }

        Thing IAttackTargetSearcher.Thing => this;
        Verb IAttackTargetSearcher.CurrentEffectiveVerb => AttackVerb;
        LocalTargetInfo IAttackTargetSearcher.LastAttackedTarget => lastAttackedTarget;
        int IAttackTargetSearcher.LastAttackTargetTick => lastAttackTargetTick;

        // 炮塔当前目标
        public virtual LocalTargetInfo CurrentTarget => currentTargetInt;

        // 攻击动词
        public virtual Verb AttackVerb
        {
            get
            {
                if (gun == null)
                {
                    return null;
                }
                var comp = gun.TryGetComp<CompEquippable>();
                if (comp == null)
                {
                    return null;
                }
                if (comp.PrimaryVerb == null)
                {
                    return null;
                }
                if (comp.PrimaryVerb.Caster == null)
                {
                    Log.Error($"AttackVerb: {gun.def.defName} 的 PrimaryVerb.Caster 为null！");
                }
                return comp.PrimaryVerb;
            }
        }

        // 炮塔顶部材质 - 新增属性
        public virtual Mesh TurretTopMaterial
        {
            get
            {
                GraphicData graphicData_gun = def.building.turretGunDef.graphicData;
                Mesh mesh = graphicData_gun.Graphic.MeshAt(Rotation);
                return mesh;
            }
        }

        public virtual bool Active => true; // 不需要电力

        // 暴露数据
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_TargetInfo.Look(ref currentTargetInt, "currentTarget");
            Scribe_Values.Look(ref holdFire, "holdFire", defaultValue: false);
            Scribe_TargetInfo.Look(ref forcedTarget, "forcedTarget");
            Scribe_TargetInfo.Look(ref lastAttackedTarget, "lastAttackedTarget");
            Scribe_Values.Look(ref lastAttackTargetTick, "lastAttackTargetTick", 0);
            Scribe_Deep.Look(ref gun, "turretGun");
            Scribe_Values.Look(ref burstWarmupTicksLeft, "burstWarmupTicksLeft", 0);
            Scribe_Values.Look(ref burstCooldownTicksLeft, "burstCooldownTicksLeft", 0);
        }

        protected override void Tick()
        {
            base.Tick();

            if (Active && Spawned)
            {
                // 处理连发状态
                if (AttackVerb != null && AttackVerb.state == VerbState.Bursting)
                {
                    AttackVerb.VerbTick();
                }

                if (burstWarmupTicksLeft > 0)
                {
                    burstWarmupTicksLeft--;
                    if (burstWarmupTicksLeft == 0)
                    {
                        BeginBurst();
                    }
                }
                else
                {
                    if (burstCooldownTicksLeft > 0)
                    {
                        burstCooldownTicksLeft--;
                    }
                    if (burstCooldownTicksLeft <= 0 && this.IsHashIntervalTick(10))
                    {
                        TryStartShootSomething(true);
                    }
                }
                top?.TurretTopTick();
            }
            else
            {
                ResetCurrentTarget();
            }
        }

        // 尝试开始射击
        public void TryStartShootSomething(bool canBeginBurstImmediately)
        {
            if (this.gun == null)
            {
                return;
            }
            if (!Spawned)
            {
                ResetCurrentTarget();
                return;
            }
            if (holdFire)
            {
                ResetCurrentTarget();
                return;
            }
            if (AttackVerb == null)
            {
                ResetCurrentTarget();
                return;
            }
            if (!AttackVerb.Available())
            {
                ResetCurrentTarget();
                return;
            }
            bool isValid = currentTargetInt.IsValid;
            if (forcedTarget.IsValid)
            {
                currentTargetInt = forcedTarget;
                top?.ForceFaceTarget(forcedTarget);
            }
            else
            {
                IAttackTarget newTarget = TryFindNewTarget();
                currentTargetInt = (newTarget != null)
                    ? newTarget.Thing
                    : LocalTargetInfo.Invalid;
            }

            if (currentTargetInt.IsValid)
            {
                float warmup = def.building.turretBurstWarmupTime.RandomInRange;
                if (warmup > 0f)
                {
                    burstWarmupTicksLeft = warmup.SecondsToTicks();
                }
                else if (canBeginBurstImmediately)
                {
                    BeginBurst();
                }
                else
                {
                    burstWarmupTicksLeft = 1;
                }
            }
            else
            {
                ResetCurrentTarget();
            }
        }

        // 寻找新目标
        public virtual IAttackTarget TryFindNewTarget()
        {
            IAttackTarget target = AttackTargetFinder.BestShootTargetFromCurrentPosition(
                this,
                TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable | TargetScanFlags.NeedLOSToAll,
                IsValidTarget);
            return target;
        }

        // 目标是否有效
        private bool IsValidTarget(Thing t)
        {
            if (t is Pawn pawn)
            {
                // 只攻击活着的敌对派系
                return !pawn.Dead && pawn.Faction != null && pawn.Faction.HostileTo(this.Faction);
            }
            // 也可以加上对敌对机械、建筑等的判断
            if (t.Faction != null && t.Faction.HostileTo(this.Faction))
            {
                return true;
            }
            return false;
        }

        // 重置强制目标
        private void ResetForcedTarget()
        {
            forcedTarget = LocalTargetInfo.Invalid;
            currentTargetInt = LocalTargetInfo.Invalid;
        }

        // 重置当前目标
        private void ResetCurrentTarget()
        {
            currentTargetInt = LocalTargetInfo.Invalid;
        }
        // 绘制炮塔
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(drawLoc, flip);
            if (top != null)
            {
                top.DrawTurret(drawLoc, Vector3.zero, 0f);
            }
        }

        // 绘制额外选择覆盖
        public override void DrawExtraSelectionOverlays()
        {
            base.DrawExtraSelectionOverlays();
            float range = AttackVerb?.verbProps.range ?? 0f;
            if (range < 90f)
            {
                GenDraw.DrawRadiusRing(Position, range);
            }
        }

        // 获取Gizmos
        public override IEnumerable<Gizmo> GetGizmos()
        {
            yield return new Command_Target
            {
                defaultLabel = "强制攻击",
                defaultDesc = "强制攻击",
                icon = ContentFinder<Texture2D>.Get("UI/Commands/Attack"),
                targetingParams = new TargetingParameters
                {
                    canTargetPawns = true,
                    canTargetBuildings = true,
                    canTargetItems = false,
                    canTargetLocations = false,
                    mapObjectTargetsMustBeAutoAttackable = true
                },
                action = delegate (LocalTargetInfo target)
                {
                    forcedTarget = target;
                    currentTargetInt = target;
                }
            };

            yield return new Command_Action
            {
                defaultLabel = "CommandStopForceAttack".Translate(),
                defaultDesc = "CommandStopForceAttackDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/Halt"),
                action = ResetForcedTarget
            };
            yield return new Command_Toggle
            {
                defaultLabel = "CommandHoldFire".Translate(),
                defaultDesc = "CommandHoldFireDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/HoldFire"),
                isActive = () => holdFire,
                toggleAction = () => holdFire = !holdFire
            };
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }
        }

        // 攻击目标时调用
        protected void OnAttackedTarget(LocalTargetInfo target)
        {
            lastAttackTargetTick = Find.TickManager.TicksGame;
            lastAttackedTarget = target;
        }

        private void UpdateGunVerbs()
        {
            if (gun == null) return;
            var comp = gun.TryGetComp<CompEquippable>();
            if (comp == null || comp.AllVerbs == null) return;
            foreach (var verb in comp.AllVerbs)
            {
                verb.caster = this;
                verb.castCompleteCallback = BurstComplete;
            }
        }
        protected virtual void BeginBurst()
        {
            if (AttackVerb.CanHitTarget(currentTargetInt))
            {
                // 确保使用正确的连发设置
                if (AttackVerb.verbProps != null)
                {
                    CompEquippable compEquippable = gun.TryGetComp<CompEquippable>();
                    if (compEquippable != null && compEquippable.PrimaryVerb != null)
                    {
                        VerbProperties verbProps = compEquippable.PrimaryVerb.verbProps;
                        AttackVerb.verbProps.burstShotCount = verbProps.burstShotCount;
                        AttackVerb.verbProps.ticksBetweenBurstShots = verbProps.ticksBetweenBurstShots;
                    }
                }

                AttackVerb.TryStartCastOn(currentTargetInt);
                OnAttackedTarget(currentTargetInt);
            }
            else
            {
                ResetCurrentTarget();
            }
        }

        protected void BurstComplete()
        {
            burstCooldownTicksLeft = (int)(def.building.turretBurstCooldownTime * 60f);
        }
    }
    public class TurretTop
    {
        private RKU_DrillingVehicleWithTurret parentTurret;
        private float curRotationInt;
        private int ticksUntilIdleTurn;
        private int idleTurnTicksLeft;
        private bool idleTurnClockwise;
        private const float IdleTurnDegreesPerTick = 0.26f;
        private const int IdleTurnDuration = 140;
        private const int IdleTurnIntervalMin = 150;
        private const int IdleTurnIntervalMax = 350;
        public static readonly int ArtworkRotation = -90;

        public float CurRotation
        {
            get => curRotationInt;
            set
            {
                curRotationInt = value;
                if (curRotationInt > 360f) curRotationInt -= 360f;
                if (curRotationInt < 0f) curRotationInt += 360f;
            }
        }

        public TurretTop(RKU_DrillingVehicleWithTurret parentTurret)
        {
            this.parentTurret = parentTurret;
        }

        public void SetRotationFromOrientation()
        {
            CurRotation = (parentTurret.Rotation == Rot4.North || parentTurret.Rotation == Rot4.West) ? 0 : 180;
        }

        public void ForceFaceTarget(LocalTargetInfo targ)
        {
            if (targ.IsValid)
            {
                CurRotation = (targ.Cell.ToVector3Shifted() - parentTurret.DrawPos).AngleFlat();
            }
        }

        public void TurretTopTick()
        {
            if (parentTurret.CurrentTarget.IsValid)
            {
                CurRotation = (parentTurret.CurrentTarget.Cell.ToVector3Shifted() - parentTurret.DrawPos).AngleFlat();
                ticksUntilIdleTurn = Rand.RangeInclusive(IdleTurnIntervalMin, IdleTurnIntervalMax);
            }
            else if (ticksUntilIdleTurn > 0)
            {
                ticksUntilIdleTurn--;
                if (ticksUntilIdleTurn == 0)
                {
                    idleTurnClockwise = Rand.Value < 0.5f;
                    idleTurnTicksLeft = IdleTurnDuration;
                }
            }
            else
            {
                CurRotation += idleTurnClockwise ? IdleTurnDegreesPerTick : -IdleTurnDegreesPerTick;
                idleTurnTicksLeft--;
                if (idleTurnTicksLeft <= 0)
                {
                    ticksUntilIdleTurn = Rand.RangeInclusive(IdleTurnIntervalMin, IdleTurnIntervalMax);
                }
            }
        }

        public void DrawTurret(Vector3 drawLoc, Vector3 recoilDrawOffset, float recoilAngleOffset)
        {
            // 根据本体朝向调整偏移量（朝南时反转X轴）
            float xOffset = parentTurret.def.building.turretTopOffset.x;
            if (parentTurret.Rotation == Rot4.South) xOffset = -xOffset;

            Vector3 offset = new Vector3(
                xOffset,
                0f,
                parentTurret.def.building.turretTopOffset.y);

            float drawSize = 1.8f;
            float aimAngle = parentTurret.AttackVerb?.AimAngleOverride ?? CurRotation;

            Vector3 pos = drawLoc + Altitudes.AltIncVect + offset;

            float totalRotation = ArtworkRotation + aimAngle;
            Quaternion rotation = totalRotation.ToQuat();

            Vector3 scale = new Vector3(drawSize, 1f, drawSize);
            Matrix4x4 matrix = Matrix4x4.TRS(pos, rotation, scale);
            Graphics.DrawMesh(parentTurret.TurretTopMaterial, matrix, parentTurret.def.building.turretGunDef.graphicData.Graphic.MatAt(parentTurret.Rotation), 0);
        }
    }
}