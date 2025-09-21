using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RatkinUnderground
{
    public class CompProperties_RKU_BackpackRadio : CompProperties
    {
        public ThingDef bulletDef;
        public float turretRange = 15f;
        public float wallRange = 10f;
        public int cooldownTicks = 18000; // 5分钟冷却
        public int radius = 18; // 范围
        

        public CompProperties_RKU_BackpackRadio()
        {
            compClass = typeof(Comp_RKU_BackpackRadio);
        }
    }

    public class Comp_RKU_BackpackRadio : ThingComp
    {
        private int lastBunkerBusterTick = -1;  

        public CompProperties_RKU_BackpackRadio Props => (CompProperties_RKU_BackpackRadio)props;

        public override void CompTick()
        {
            base.CompTick();
            if (parent is Apparel apparel && apparel.Wearer != null && !apparel.Wearer.IsColonist && apparel.Wearer.Faction != Faction.OfPlayer)
            {
                TryAIUseBunkerBuster();
            }
        }

        private void TryAIUseBunkerBuster()
        {
            if (lastBunkerBusterTick != -1 && Find.TickManager.TicksGame - lastBunkerBusterTick < Props.cooldownTicks)
                return;

            // 检查30格内是否有敌对墙或炮塔
            if (HasNearbyEnemyBuildings())
            {
                // 随机机会使用技能
                if (Rand.Chance(0.001f))
                {
                    IntVec3 target = FindAITarget();
                    if (target != IntVec3.Invalid)
                    {
                        LaunchBunkerBuster(target);
                        lastBunkerBusterTick = Find.TickManager.TicksGame;
                    }
                }
            }
        }

        private bool HasNearbyEnemyBuildings()
        {
            Map map = (parent as Apparel).Wearer.Map;
            if (map == null)
                return false;

            // 获取穿戴者
            if (!(parent is Apparel apparel) || apparel.Wearer == null)
                return false;

            Faction wearerFaction = apparel.Wearer.Faction;
            if (wearerFaction == null)
                return false;

            IntVec3 wearerPos = apparel.Wearer.Position;

            // 检查30格内是否有敌对炮塔
            IEnumerable<Building> enemyTurrets = map.listerBuildings.allBuildingsNonColonist
                .Where(b => b.def.building.turretGunDef != null && b.Faction != null &&
                           b.Faction.HostileTo(wearerFaction) && b.Position.DistanceTo(wearerPos) <= 30f);

            if (enemyTurrets.Any())
                return true;

            // 检查30格内是否有敌对墙
            IEnumerable<Building> enemyWalls = map.listerBuildings.allBuildingsNonColonist
                .Where(b => b.def.defName.Contains("Wall") && b.Faction != null &&
                           b.Faction.HostileTo(wearerFaction) && b.Position.DistanceTo(wearerPos) <= 30f);

            return enemyWalls.Any();
        }

        private IntVec3 FindAITarget()
        {
            Map map = (parent as Apparel).Wearer.Map;
            if (map == null)
                return IntVec3.Invalid;

            // 获取穿戴者
            if (!(parent is Apparel apparel) || apparel.Wearer == null)
                return IntVec3.Invalid;

            Faction wearerFaction = apparel.Wearer.Faction;
            if (wearerFaction == null)
                return IntVec3.Invalid;

            IntVec3 wearerPos = apparel.Wearer.Position;

            // 直接在30格范围内找敌对炮塔
            IEnumerable<Building> enemyTurrets = map.listerBuildings.allBuildingsNonColonist
                .Where(b => b.def.building.turretGunDef != null && b.Faction != null &&
                           b.Faction.HostileTo(wearerFaction) && b.Position.DistanceTo(wearerPos) <= 30f);

            if (enemyTurrets.Any())
            {
                Building randomTurret = enemyTurrets.RandomElement();
               if (CanUseAbility(randomTurret.Position, wearerFaction))
                {
                    return randomTurret.Position;
                }
            }

            // 如果没有敌对炮塔，找30格范围内敌对墙
            IEnumerable<Building> enemyWalls = map.listerBuildings.allBuildingsNonColonist
                .Where(b => b.def.defName.Contains("Wall") && b.Faction != null &&
                           b.Faction.HostileTo(wearerFaction) && b.Position.DistanceTo(wearerPos) <= 30f);

            if (enemyWalls.Any())
            {
                Building randomWall = enemyWalls.RandomElement();
                if (CanUseAbility(randomWall.Position, wearerFaction))
                {
                    return randomWall.Position;
                }
            }

            return IntVec3.Invalid;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            if (parent is Apparel apparel && apparel.Wearer != null && apparel.Wearer.IsColonist)
            {
                // 钻地炸弹技能按钮
                Command_Action bunkerBusterCommand = new Command_Action
                {
                    defaultLabel = "呼叫钻地炸弹",
                    defaultDesc = "呼叫一枚钻地炸弹到目标位置爆炸",
                    icon = ContentFinder<Texture2D>.Get("RKU_Null"),
                    hotKey = KeyBindingDefOf.Misc2,
                    action = delegate
                    {
                        CallBunkerBuster();
                    }
                };

                // 检查冷却时间
                if (lastBunkerBusterTick!=-1&&Find.TickManager.TicksGame - lastBunkerBusterTick < Props.cooldownTicks)
                {
                    bunkerBusterCommand.Disable("技能冷却中");
                }

                yield return bunkerBusterCommand;
            }
        }

        private void CallBunkerBuster()
        {
            Map map = (parent as Apparel).Wearer.Map;
            Find.Targeter.BeginTargeting(new TargetingParameters
            {
                canTargetLocations = true,
                canTargetSelf = false,
                canTargetPawns = true,
                canTargetBuildings = true,
                validator = (TargetInfo x) => CanAffect((parent as Apparel).Wearer.Position, map, (LocalTargetInfo)x)
            }, delegate (LocalTargetInfo target)
            {
                LaunchBunkerBuster(target.Cell);
                lastBunkerBusterTick = Find.TickManager.TicksGame;
            });
        }


        private bool CanUseAbility(IntVec3 targetCell, Faction userFaction = null)
        {
            Map map = (parent as Apparel).Wearer.Map;
            if (map == null)
                return false;

            // 如果没有指定阵营，使用穿戴者的阵营
            if (userFaction == null)
            {
                if (parent is Apparel apparel && apparel.Wearer != null)
                {
                    userFaction = apparel.Wearer.Faction;
                }
                else
                {
                    return false;
                }
            }

            // 检查敌对阵营的炮塔
            IEnumerable<Building> enemyTurrets = map.listerBuildings.allBuildingsNonColonist
                .Where(b => b.def.building.turretGunDef != null && b.Faction != null && b.Faction.HostileTo(userFaction));

            foreach (Building turret in enemyTurrets)
            {
                if (turret.Position.DistanceTo(targetCell) <= Props.turretRange)
                {
                    return true;
                }
            }

            // 检查敌对阵营的墙
            IEnumerable<Building> enemyWalls = map.listerBuildings.allBuildingsNonColonist
                .Where(b => b.def.defName.Contains("Wall") && b.Faction != null && b.Faction.HostileTo(userFaction));

            foreach (Building wall in enemyWalls)
            {
                if (wall.Position.DistanceTo(targetCell) <= Props.wallRange)
                {
                    return true;
                }
            }

            return false;
        }

        private IntVec3 FindBestLaunchPosition(IntVec3 targetCell)
        {
            Map map = (parent as Apparel).Wearer.Map;
            if (map == null)
                return IntVec3.Invalid;
            return CellFinder.RandomEdgeCell(map);
        }

        public bool CanAffect(IntVec3 position,Map map, LocalTargetInfo target)
        {
            IntVec3 cell = target.Cell;
            if (!cell.IsValid || map == null) return false;
            float dist = position.DistanceTo(cell);
            GenDraw.DrawRadiusRing(position, Props.radius, Color.red);
            return dist <= Props.radius;
        }

        private void LaunchBunkerBuster(IntVec3 targetCell)
        {
            Map map = (parent as Apparel).Wearer.Map;
            RKU_BunkerBusterBullet bullet = (RKU_BunkerBusterBullet)ThingMaker.MakeThing(Props.bulletDef);
            if (bullet == null)
                return;

            IntVec3 launchPos = FindBestLaunchPosition(targetCell);
            if (launchPos == IntVec3.Invalid)
            {
                Messages.Message("找不到合适的发射位置", MessageTypeDefOf.RejectInput);
                return;
            }

            bullet.Position = launchPos;
            bullet.Rotation = Rot4.FromAngleFlat((targetCell - launchPos).AngleFlat);
            GenSpawn.Spawn(bullet, launchPos, map);
            bullet.Launch(bullet, targetCell, targetCell, ProjectileHitFlags.None, false);
        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref lastBunkerBusterTick, "lastBunkerBusterTick", -1);
        }
    }
}
