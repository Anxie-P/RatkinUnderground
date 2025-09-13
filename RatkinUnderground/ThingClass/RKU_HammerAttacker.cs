using RimWorld;
using System;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RatkinUnderground
{
    public class RKU_HammerAttacker : ThingWithComps
    {
        private enum AnimationState
        {
            HoldA,
            Forward,
            HoldI,
            Reverse
        }
        private AnimationState currentState = AnimationState.HoldA;
        private int stateTimer = 0;
        private int currentFrame = 0; // 0=A, 1=B, 2=C, ..., 8=I
        private bool isReversing = false;

        private const int HOLD_DURATION = 360;
        private const int FRAME_SWITCH_INTERVAL = 1;
        private const int FRAME_SWITCH_INTERVALBACK = 7;
        private const int TOTAL_FRAMES = 9;
        private const float TRIGGER_CHANCE = 0.15f; // 触发概率


        public override Graphic Graphic
        {
            get
            {
                string texPath = $"Buildings/Hammer/HammerAttacker{(char)('A' + currentFrame)}";

                return GraphicDatabase.Get<Graphic_Single>(
                    texPath,
                    ShaderDatabase.Cutout,
                    this.def.graphicData.drawSize,
                    Color.white
                );
            }
        }

        public override void PostMake()
        {
            base.PostMake();
        }

        public override void Tick()
        {
            base.Tick();
            if (!Spawned) return;

            stateTimer++;

            switch (currentState)
            {
                case AnimationState.HoldA:
                    if (stateTimer >= HOLD_DURATION)
                    {
                        currentState = AnimationState.Forward;
                        stateTimer = 0;
                        currentFrame = 1;
                        DirtyMapMesh();
                    }
                    break;

                case AnimationState.Forward:
                    if (stateTimer >= FRAME_SWITCH_INTERVAL)
                    {
                        stateTimer = 0;
                        currentFrame++;
                        if (currentFrame >= 4 && currentFrame <= 6)
                        {
                            DoScreenShake(0.3f);
                        }

                        if (currentFrame >= TOTAL_FRAMES - 1)
                        {
                            currentState = AnimationState.HoldI;
                            stateTimer = 0;
                        }
                        DirtyMapMesh();
                    }
                    break;

                case AnimationState.HoldI:
                    if (stateTimer == 1)
                    {
                        // 锤子击中地面时的强烈震动
                        DoScreenShake(1.5f);
                        SoundDef.Named("RKU_Hammer").PlayOneShot(new TargetInfo(Position, Map));
                        SpawnDustEffect();
                        if (Rand.Chance(TRIGGER_CHANCE))
                        {
                            TriggerAttack();
                        }
                    }
                    if (stateTimer >= HOLD_DURATION)
                    {
                        currentState = AnimationState.Reverse;
                        stateTimer = 0;
                        currentFrame = 7;
                        DirtyMapMesh();
                    }
                    break;

                case AnimationState.Reverse:
                    if (stateTimer >= FRAME_SWITCH_INTERVALBACK)
                    {
                        stateTimer = 0;
                        currentFrame--;

                        if (currentFrame <= 0)
                        {
                            currentState = AnimationState.HoldA;
                            stateTimer = 0;
                            currentFrame = 0;
                        }
                        DirtyMapMesh();
                    }
                    break;
            }
        }

        private void TriggerAttack()
        {
            Map map = Map;
            if (map == null) return;

            IncidentDef incidentDef = null;

            if (ModsConfig.AnomalyActive&&Rand.Range(0,100)>50)
            {
                incidentDef = DefDatabase<IncidentDef>.GetNamed("RKU_LimitedFleshbeastAttack");
            }
            else
            {
                incidentDef = DefDatabase<IncidentDef>.GetNamed("RKU_LimitedInsectAttack");
        }

            if (incidentDef == null) return;

            IncidentParms parms = new IncidentParms
            {
                target = map,
                points = StorytellerUtility.DefaultThreatPointsNow(map),
                faction = Faction.OfInsects 
            };

            incidentDef.Worker.TryExecute(parms);
        }

        private void DirtyMapMesh()
        {
            if (Find.CurrentMap == Map)
            {
                Map.mapDrawer.MapMeshDirty(
                    Position,
                    MapMeshFlagDefOf.Things,
                    regenAdjacentCells: false,
                    regenAdjacentSections: false
                );
            }
        }

        private void DoScreenShake(float magnitude = 1.5f)
        {
            // 震地攻击时的屏幕震动效果
            if (Find.CameraDriver != null)
            {
                Find.CameraDriver.shaker.DoShake(magnitude);
            }
        }

        private void SpawnDustEffect()
        {
            if (!Spawned) return;
            IntVec3 basePos = Position;
            IntVec3[] dustPositions = new IntVec3[4]
            {
                basePos + new IntVec3(0, 0, 0), 
                basePos + new IntVec3(1, 0, 0), 
                basePos + new IntVec3(2, 0, 0), 
                basePos + new IntVec3(-1, 0, 0)  
            };

            // 在每个位置生成尘土效果
            foreach (IntVec3 pos in dustPositions)
            {
                if (pos.InBounds(Map))
                {
                    FleckMaker.Static(pos.ToVector3Shifted(), Map, FleckDefOf.DustPuff, 4f);
                    if (Rand.Chance(0.2f))
                    {
                        FilthMaker.TryMakeFilth(pos, Map, ThingDefOf.Filth_Dirt);
                    }
                }
            }
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            base.Destroy(mode);
            if (mode != DestroyMode.Vanish && mode != DestroyMode.Refund)
            {
                CheckFinalBattleMoraleImpact();
            }
        }

        private void CheckFinalBattleMoraleImpact()
        {
            RKU_GameCondition_FinalBattle finalBattle = Find.World.GameConditionManager.GetActiveCondition<RKU_GameCondition_FinalBattle>();
            if (finalBattle != null)
            {
                // 减少20%士气值
                finalBattle.ModifyMorale(-0.2f);
                Messages.Message("撼地装置被摧毁！游击队士气大幅降低了", MessageTypeDefOf.NegativeEvent);
            }
        }


        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref currentState, "currentState", AnimationState.HoldA);
            Scribe_Values.Look(ref stateTimer, "stateTimer", 0);
            Scribe_Values.Look(ref currentFrame, "currentFrame", 0);
            Scribe_Values.Look(ref isReversing, "isReversing", false);
        }
    }
}
