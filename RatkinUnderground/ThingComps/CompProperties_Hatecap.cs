using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
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
        int minMush = 3;
        int maxMush = 15;
        List<ThingDef> randomMush => DefDatabase<ThingDef>.AllDefsListForReading
                                    .Where(td => td.ingestible != null &&
                                                 td.ingestible.foodType == FoodTypeFlags.Fungus &&
                                                 td.defName != "RKU_RawAllurecap" /*&&
                                                 td.defName != "RKU_RawHatecap"*/)
                                    .ToList();
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

        public override void PostDeSpawn(Map map, DestroyMode destroyMode)
        {
            base.PostDeSpawn(map, DestroyMode.Vanish);

            var typesCount = Rand.RangeInclusive(0, 3);
            for (int i = 0; i < typesCount; i++)
            {
                var idx = Rand.RangeInclusive(0, randomMush.Count - 1);
                var count = Rand.RangeInclusive(15, 75);
                Thing mush = ThingMaker.MakeThing(randomMush[idx]);
                mush.stackCount = count;
                GenPlace.TryPlaceThing(mush, parent.Position, map, ThingPlaceMode.Near);
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
