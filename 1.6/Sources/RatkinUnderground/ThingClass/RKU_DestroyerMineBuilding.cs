using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RatkinUnderground
{
    public class RKU_DestroyerMineBuilding : Building
    {
        private bool isDetonating = false;

        private RKU_DestroyerMineExtension Extension => def.GetModExtension<RKU_DestroyerMineExtension>();


        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            yield return new Command_Action
            {
                defaultLabel = "手动引爆",
                defaultDesc = "手动引爆地雷，造成大范围爆炸伤害",
                icon = ContentFinder<Texture2D>.Get("UI/Info"),
                action = () =>
                {
                    if (Map != null)
                    {
                        Boom(Map);
                    }
                }
            };
        }

        public void Boom(Map map)
        {
            var extension = Extension;
            if (extension == null)
            {
                Log.Error("[RKU] RKU_DestroyerMineBuilding的def缺少RKU_DestroyerMineExtension扩展");
                return;
            }

            isDetonating = true;
            GenExplosion.DoExplosion(Position, map, extension.explosionRadius, extension.explosionDamageDef, this, extension.explosionDamageAmount, extension.explosionArmorPenetration);
            Effecter effecter = EffecterDefOf.Vaporize_Heatwave.Spawn();
            effecter.scale = extension.effecterScale;
            effecter.Trigger(new TargetInfo(Position, map), TargetInfo.Invalid);
            effecter.Cleanup();
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(Position, extension.chainExplosionRadius, true))
            {
                if (cell.InBounds(map))
                {
                    List<Thing> thingsAtCell = cell.GetThingList(map);
                    for (int i = 0; i < thingsAtCell.Count; i++)
                    {
                        if (thingsAtCell[i] != null && thingsAtCell[i] is RKU_DestroyerMineBuilding mine) {
                            if (mine!=null&&!mine.isDetonating) {
                                mine.Boom(thingsAtCell[i].Map);
                            }
                        }
                        if (thingsAtCell[i] != null && thingsAtCell[i] is Pawn pawn)
                        {
                            DamageInfo damageInfo = new DamageInfo(extension.pawnDamageDef, extension.pawnDamageAmount, extension.pawnArmorPenetration, -1f, this);
                            pawn.TakeDamage(damageInfo);
                        }
                        else if (thingsAtCell[i]!=null&&thingsAtCell[i] is Building building)
                        {
                            DamageInfo damageInfo = new DamageInfo(extension.buildingDamageDef, extension.buildingDamageAmount, extension.buildingArmorPenetration, -1f, this);
                            building.TakeDamage(damageInfo);
                        }
                    }
                }
            }
            if (!Destroyed)
            {
                base.Destroy();
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref isDetonating, "isDetonating", false);
        }
    }
}