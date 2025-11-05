using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;
using Verse.Sound;
using Verse.Noise;
using RimWorld.BaseGen;
using Verse.AI.Group;

namespace RatkinUnderground
{
    public class RKU_IncidentWorker_BuildMap : IncidentWorker
    {
        private static readonly FloatRange IncidentPointsFactorRange = new FloatRange(1f, 1.7f);

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            parms.points *= IncidentPointsFactorRange.RandomInRange;
            Caravan caravan = (Caravan)parms.target;
            // Check if def and ModExtension are valid
            if (def == null)
            {
                return false;
            }

            var modExtension = def.GetModExtension<RKU_WorldObjectDefModExtension>();
            if (modExtension == null)
            {
                return false;
            }

            if (modExtension.worldObjectDef == null)
            {
                return false;
            }
            MapParent mapParent = (MapParent)WorldObjectMaker.MakeWorldObject(def.GetModExtension<RKU_WorldObjectDefModExtension>().worldObjectDef);
            mapParent.Tile = caravan.Tile;
            mapParent.SetFaction(parms.faction);
            Find.WorldObjects.Add(mapParent);
            ChoiceLetter choiceLetter = LetterMaker.MakeLetter(def.letterLabel.Translate(), def.letterText, LetterDefOf.NeutralEvent);
            Find.LetterStack.ReceiveLetter(choiceLetter.Label, choiceLetter.Text, choiceLetter.def);
            LongEventHandler.QueueLongEvent(delegate
            {
                int mapSize = 200;
                int tile = caravan.Tile;
                bool flag = Current.Game.FindMap(tile) == null;
                Map orGenerateMap = GetOrGenerateMapUtility.GetOrGenerateMap(tile, new IntVec3(mapSize, 1, mapSize), mapParent.def);
                if (flag && orGenerateMap != null)
                {
                    orGenerateMap.retainedCaravanData.Notify_GeneratedTempIncidentMapFor(caravan);
                }

                //加迷雾
                orGenerateMap.fogGrid.Refog(CellRect.WholeMap(orGenerateMap));
                CaravanEnterMapUtility.Enter(caravan, orGenerateMap, CaravanEnterMode.Edge);
            }, "GeneratingMapForNewEncounter", doAsynchronously: false, null);
            return true;
        }
    }
}
