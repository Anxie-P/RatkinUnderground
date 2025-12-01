using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace RatkinUnderground
{
    public class RKU_Panjandrum :Building
    {
        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if (mode == DestroyMode.KillFinalize)
            {
                // 生成一个模拟弹丸，仅用于“投射”
                var bullet = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("RKU_PanjandrumBullet")) as Projectile;
                if (bullet != null)
                {
                    GenSpawn.Spawn(bullet, Position, Map);

                    // 调用 Launch 强制其立即命中当前位置，并触发爆炸
                    bullet.Launch(
                        launcher: this,
                        usedTarget: new LocalTargetInfo(Position),
                        intendedTarget: new LocalTargetInfo(Position),
                        hitFlags: ProjectileHitFlags.None,
                        preventFriendlyFire: false
                    );
                }
            }
            base.Destroy(mode);

        }
        public override IEnumerable<Gizmo> GetGizmos()
        {
            Command_Target command_ChooseTargetInMap = new()
            {
                defaultLabel = "爆！",
                hotKey = KeyBindingDefOf.Misc1,
                icon = ContentFinder<Texture2D>.Get("UI/RKU_CallBoomber"),
                targetingParams = new TargetingParameters
                {
                    canTargetLocations = true,
                },
                action = delegate (LocalTargetInfo target)
                {
                    RKU_PanjandrumBullet projectile = (RKU_PanjandrumBullet)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("RKU_PanjandrumBullet"));
                    GenSpawn.Spawn(projectile, Position, Map);

                    LocalTargetInfo localTargetInfo = target;
                    if (target.Cell.x > Position.x)
                    {
                        localTargetInfo = new LocalTargetInfo(new IntVec3(target.Cell.x - 1, target.Cell.y, target.Cell.z));
                    }
                    projectile.Launch(this, target, localTargetInfo, ProjectileHitFlags.None, false);
                    DeSpawn();
                }
            };
            yield return command_ChooseTargetInMap;
        }
    }
}
