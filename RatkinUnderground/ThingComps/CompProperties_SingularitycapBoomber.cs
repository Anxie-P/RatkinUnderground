using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using Verse;
using Verse.AI;

namespace RatkinUnderground;
public class CompProperties_SingularitycapBoomber : CompProperties
{
    public float checkRange = 5f;

    public CompProperties_SingularitycapBoomber()
    {
        compClass = typeof(Comp_SingularitycapBoomber);
    }
}
public class Comp_SingularitycapBoomber : ThingComp
{
    public CompProperties_SingularitycapBoomber Props => (CompProperties_SingularitycapBoomber)props;
    public override void CompTickRare()
    {
        base.CompTickRare();
        
        // 检查光照强度
        float glow = parent.Map.glowGrid.GroundGlowAt(parent.Position);
        if (glow > 0.6f) 
        {
            return;
        }

        // 检查附近是否有Pawn或钻机，且能接触到蘑菇
        List<Thing> nearbyTargets = new List<Thing>();
        foreach (IntVec3 cell in GenRadial.RadialCellsAround(parent.Position, Props.checkRange, true))
        {
            if (!cell.InBounds(parent.Map)) continue;

            Pawn pawn = cell.GetFirstPawn(parent.Map);
            if (pawn != null && pawn.def.race.Humanlike && !nearbyTargets.Contains(pawn))
            {
                if (pawn.Map.reachability.CanReach(pawn.Position, parent, PathEndMode.Touch, TraverseParms.For(pawn)))
                {
                    nearbyTargets.Add(pawn);
                }
            }

            // 检查钻机
            List<Thing> thingsAtCell = cell.GetThingList(parent.Map);
            foreach (Thing thing in thingsAtCell)
            {
                if (thing.def.defName == "RKU_DrillingVehicle" && !nearbyTargets.Contains(thing))
                {
                    // 只要钻机能接触到蘑菇就算
                    if (thing.Map.reachability.CanReach(thing.Position, parent, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassAllDestroyableThings)))
                    {
                        nearbyTargets.Add(thing);
                    }
                }
            }
        }

        if (nearbyTargets.Count > 0)
        {
            IntVec3 explosionCenter = parent.Position;
            GenExplosion.DoExplosion(explosionCenter,parent.Map,8, DamageDefOf.Bomb,parent,30,1);
            Effecter effecter = EffecterDefOf.Vaporize_Heatwave.Spawn();
            effecter.scale =5;
            effecter.Trigger(new TargetInfo(parent.Position, parent.Map), TargetInfo.Invalid);
            effecter.Cleanup();
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(explosionCenter, Props.checkRange, true))
            {
                if (cell.InBounds(parent.Map))
                {
                    List<Thing> thingsAtCell = cell.GetThingList(parent.Map);
                   
                    foreach (Thing thing in thingsAtCell)
                    {
                        Log.Message($"爆炸范围的物品：{thing.def.defName}");
                        if (thing is Pawn pawn)
                        {
                            DamageInfo damageInfo = new DamageInfo(DamageDefOf.Bomb, 20f, 0f, 0f, parent);
                            pawn.TakeDamage(damageInfo);
                        }
                        else if (thing is Building building)
                        {
                            DamageInfo damageInfo = new DamageInfo(DamageDefOf.Bomb, 100f, 0f, 0f, parent);
                            if (building.def.defName == "RKU_DrillingVehicle")
                            {
                                // 限制对钻机的伤害，默认最大耐久的20%，保底1血
                                var damage = building.HitPoints -= (building.MaxHitPoints * 0.2f) > 0 ? building.HitPoints : 1;
                                var damageDrill = new DamageInfo(DamageDefOf.Bomb, damage, 0f, 0f, parent);
                                building.TakeDamage(damageDrill);
                                return;
                            }
                            building.TakeDamage(damageInfo);
                        }
                    }
                }
            }
        }
    }
}