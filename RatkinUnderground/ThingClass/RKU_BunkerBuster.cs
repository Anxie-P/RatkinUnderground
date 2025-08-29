using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RatkinUnderground
{
    public class RKU_BunkerBuster : Building
    {
        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if (mode == DestroyMode.KillFinalize)
            {
                var bullet = ThingMaker.MakeThing(DefOfs.RKU_BunkerBusterBullet) as Projectile;
                if (bullet != null)
                {
                    GenSpawn.Spawn(bullet, Position, Map);

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
                icon = null,
                targetingParams = new TargetingParameters
                {
                    canTargetLocations = true,
                },
                action = delegate (LocalTargetInfo target)
                {
                    RKU_BunkerBusterBullet projectile = (RKU_BunkerBusterBullet)ThingMaker.MakeThing(DefOfs.RKU_BunkerBusterBullet);
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
