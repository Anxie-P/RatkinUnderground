using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Analytics;
using Verse;
using Verse.AI;
using Verse.Noise;
using static RimWorld.PsychicRitualRoleDef;

namespace RatkinUnderground
{
    public class CompProperties_Hatecap : CompProperties
    {
        public int toxinRadius = 4;
        public CompProperties_Hatecap()
        {
            compClass = typeof(Comp_Hatecap);
        }
    }

    public class Comp_Hatecap : ThingComp,
        ISpecialMushroom
    {
        int tick = 0;
        GasGrid gasGrid => new(parent.Map);
        List<IntVec3> cells => GenRadial.RadialCellsAround(parent.Position, Props.toxinRadius, true)
                              .Where(c => c.InBounds(parent.Map))
                              .ToList();
        public CompProperties_Hatecap Props => (CompProperties_Hatecap)props;

        public override void CompTick()
        {
            base.CompTick();

            tick++;
            if (tick < 30) return;
            tick = 0;

            IEnumerable<Pawn> pawns = GenRadial.RadialDistinctThingsAround(parent.Position, parent.Map, Props.toxinRadius, true).OfType<Pawn>();
            foreach (var p in pawns)
            {
                if (!p.RaceProps.Humanlike ||
                    p.Dead) continue;
                SpawnToxGas();
            }
        }

        void SpawnToxGas()
        {
            foreach (var c in cells)
            {
                if (GasUtility.AnyGas(c, parent.Map, GasType.ToxGas)) continue;
                GasUtility.AddGas(c, parent.Map, GasType.ToxGas, 255);
            }
        }

        public void TryAddEffect(Pawn pawn)
        {
        }
    }

}
