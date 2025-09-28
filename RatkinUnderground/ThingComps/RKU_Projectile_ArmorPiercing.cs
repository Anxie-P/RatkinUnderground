using Verse;
using RimWorld;
using UnityEngine;

namespace RatkinUnderground
{
    public class RKU_Projectile_ArmorPiercing : Bullet
    {
        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            base.Impact(hitThing, blockedByShield);

            if (hitThing is Pawn pawn)
            {
                Hediff armorWeaken = pawn.health.hediffSet.GetFirstHediffOfDef(DefOfs.RKU_ArmorWeaken);

                if (armorWeaken == null)
                {
                    armorWeaken = HediffMaker.MakeHediff(DefOfs.RKU_ArmorWeaken, pawn);
                    armorWeaken.Severity = 0.15f;
                    pawn.health.AddHediff(armorWeaken);
                }
                else
                {
                    armorWeaken.Severity = Mathf.Min(armorWeaken.Severity + 0.15f, 1.0f);
                }
            }
        }
    }
}