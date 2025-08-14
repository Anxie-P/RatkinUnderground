using RimWorld;
using RimWorld.SketchGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RatkinUnderground
{
    public class RKU_SketchResolver_MonumentRuin : SketchResolver
    {
        protected override bool CanResolveInt(ResolveParams parms)
        {
            return true;
        }

        protected override void ResolveInt(ResolveParams parms)
        {
            ResolveParams parms2 = parms;
            parms2.allowWood = parms.allowWood ?? false;
            if (parms2.allowedMonumentThings == null)
            {
                parms2.allowedMonumentThings = new ThingFilter();
                parms2.allowedMonumentThings.SetAllowAll(null, includeNonStorable: true);
            }

            if (ModsConfig.RoyaltyActive)
            {
                parms2.allowedMonumentThings.SetAllow(ThingDefOf.Drape, allow: false);
            }

            SketchResolverDefOf.Monument.Resolve(parms2);
            DefOfs.RKU_DamageBuildingsAndRepair.Resolve(parms);
        }
    }
}
