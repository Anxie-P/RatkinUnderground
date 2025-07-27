using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RatkinUnderground
{
    public class RKU_DestroyerMineBuilding : Building
    {
        private int explosionRadius = 5;
        private int damageAmount = 100;
        private bool isDetonating = false;

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
                    Boom(Map);
                }
            };
        }

        public void Boom(Map map)
        {
            GenExplosion.DoExplosion(Position, map, 8, DamageDefOf.Bomb, this, 6, 1);
            Effecter effecter = EffecterDefOf.Vaporize_Heatwave.Spawn();
            effecter.scale = 2;
            effecter.Trigger(new TargetInfo(Position, map), TargetInfo.Invalid);
            effecter.Cleanup();
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(Position, 15, true))
            {
                if (cell.InBounds(map))
                {
                    List<Thing> thingsAtCell = cell.GetThingList(map);
                    for (int i = 0; i < thingsAtCell.Count; i++)
                    {
                        if (thingsAtCell[i] is Pawn pawn)
                        {
                            DamageInfo damageInfo = new DamageInfo(DamageDefOf.Bomb, 20f, 0f, 0f, this);
                            pawn.TakeDamage(damageInfo);
                        }
                        else if (thingsAtCell[i] is Building building)
                        {
                            DamageInfo damageInfo = new DamageInfo(DamageDefOf.Bomb, 100f, 0f, 0f, this);
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
            Scribe_Values.Look(ref explosionRadius, "explosionRadius", 5);
            Scribe_Values.Look(ref damageAmount, "damageAmount", 100);
            Scribe_Values.Look(ref isDetonating, "isDetonating", false);
        }
    }
}