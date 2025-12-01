using HarmonyLib;
using Verse;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using System;
using System.Reflection;
namespace RatkinUnderground
{
    public class RKU_WorldObjectDefModExtension : DefModExtension
    {
        public WorldObjectDef worldObjectDef;
    }
    public class RKU_MapGeneratorDefModExtension : DefModExtension
    {
        public bool isEncounterMap;
        public bool isSpawnCenter;
        public bool requireDrillingVehicle = false;
    }

    /// <summary>
    /// 为了防止出现3个enter
    /// </summary>
    public class RKU_MapParentModExtension : DefModExtension
    {
        public bool spawnMap = false;
    }

    /// <summary>
    /// 钻机伤害配置
    /// </summary>
    public class RKU_DrillingBulletExtension : DefModExtension
    {
        // 移动时撞到pawn的伤害
        public DamageDef pawnCollisionDamageDef = DamageDefOf.Cut;
        public float pawnCollisionDamageAmount = 10f;
        public float pawnCollisionArmorPenetration = 0.05f;

        // 销毁时撞到pawn的伤害
        public DamageDef pawnDestroyDamageDef = DamageDefOf.Flame;
        public float pawnDestroyDamageAmount = 35f;
        public float pawnDestroyArmorPenetration = 0.4f;
        // 移动时撞到建筑的伤害
        public DamageDef buildingCollisionDamageDef = DamageDefOf.Crush;
        public float buildingCollisionDamageMultiplier = 1f; // 100%最大耐久
        public float buildingCollisionArmorPenetration = 2f;

        // 销毁时撞到建筑的伤害
        public DamageDef buildingDestroyDamageDef = DamageDefOf.Crush;
        public float buildingDestroyDamageAmount = 300f;
        public float buildingDestroyArmorPenetration = 2f;

        // 撞到建筑时对钻机造成的耐久损失
        public int vehicleDurabilityLossMin = 0;
        public int vehicleDurabilityLossMax = 7;
    }

    /// <summary>
    /// 毁灭菇伤害配置
    /// </summary>
    public class RKU_SingularitycapExtension : DefModExtension
    {
        // 爆炸伤害
        public DamageDef explosionDamageDef = DamageDefOf.Bomb;
        public float explosionRadius = 8f;
        public int explosionDamageAmount = 30;
        public float explosionArmorPenetration = 1f;

        // 对pawn的额外伤害
        public DamageDef pawnDamageDef = DamageDefOf.Bomb;
        public float pawnDamageAmount = 20f;
        public float pawnArmorPenetration = 0f;

        // 对建筑的伤害
        public DamageDef buildingDamageDef = DamageDefOf.Bomb;
        public float buildingDamageAmount = 100f;
        public float buildingArmorPenetration = 0f;

        // 对钻机的伤害（伤害值为最大耐久的百分比）
        public DamageDef drillingVehicleDamageDef = DamageDefOf.Bomb;
        public float drillingVehicleDamagePercent = 0.2f; // 20%最大耐久

        public float drillingVehicleArmorPenetration = 0f;
        public float checkRange = 4f;
        public int animationDurationTicks = 180;
        public float glowThreshold = 0.5f;
    }

    /// <summary>
    /// 毁灭地雷
    /// </summary>
    public class RKU_DestroyerMineExtension : DefModExtension
    {
        // 爆炸伤害
        public DamageDef explosionDamageDef = DamageDefOf.Bomb;
        public float explosionRadius = 8f;
        public int explosionDamageAmount = 6;
        public float explosionArmorPenetration = 1f;

        // 对pawn的伤害
        public DamageDef pawnDamageDef = DamageDefOf.Bomb;
        public float pawnDamageAmount = 20f;
        public float pawnArmorPenetration = 0f;

        // 对建筑的伤害
        public DamageDef buildingDamageDef = DamageDefOf.Bomb;
        public float buildingDamageAmount = 100f;
        public float buildingArmorPenetration = 0f;

        // 连锁爆炸范围
        public float chainExplosionRadius = 15f;

        // 缩放
        public float effecterScale = 2f;
    }
}