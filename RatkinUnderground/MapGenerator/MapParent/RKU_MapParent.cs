using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RatkinUnderground
{
    public class RKU_MapParent : MapParent
    {
        bool scanMap => def.GetModExtension<RKU_MapParentModExtension>().spawnMap;
        bool isSpawn = false;
        public override void PostMapGenerate()
        {
            base.PostMapGenerate();
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Caravan caravan)
        {
            if (scanMap)
            {
                foreach (var opt in base.GetFloatMenuOptions(caravan))
                    yield return opt;

                if (!caravan.IsPlayerControlled)
                    yield break;

                // =================================================================

                // 开发模式创建地图
                if (Prefs.DevMode)
                {
                    if (!isSpawn)
                    {
                        yield return new FloatMenuOption("进入地图.Dev", delegate
                        {
                            LongEventHandler.QueueLongEvent(delegate
                            {
                                try
                                {
                                    IntVec3 mapSize = new IntVec3(250, 1, 250);
                                    Map map = GetOrGenerateMapUtility.GetOrGenerateMap(this.Tile, mapSize, this.def);
                                    map.fogGrid.Refog(CellRect.WholeMap(map));
                                    CaravanEnterMapUtility.Enter(caravan, map, CaravanEnterMode.Edge, CaravanDropInventoryMode.DoNotDrop, true);

                                    isSpawn = true;
                                }
                                catch (Exception ex)
                                {
                                    Log.Error($"RKU: Enter failed for {this.def.defName} at tile {this.Tile}: {ex}");
                                    throw;
                                }
                            },
                            "GeneratingMap".Translate(),
                            doAsynchronously: true, (Exception ex) =>
                            {
                                Log.Error($"[RKU] RKU_MapParent的QueueLongEvent报错: {ex}");
                            },
                            true,
                            null
                            );
                        }, MenuOptionPriority.Default, null, null, 0f, null, null);
                    }
                }

                // =================================================================

                // 禁止非钻机进入
                if (!(caravan is RKU_DrillingVehicleOnMap))
                    yield break;

                // 进入条件
                bool canEnter = true;
                string failReason = null;

                // 未到达
                if (caravan.Tile != Tile)
                {
                    canEnter = false;
                    failReason = "未到达";

                }
                /*if (!canEnter)
                {
                    yield return new FloatMenuOption("进入地图" + " (" + failReason + ")", null, MenuOptionPriority.Default, null, null, 0f, null, null);
                    yield break;
                }*/

                // 创建地图
                if (!isSpawn)
                {
                    yield return new FloatMenuOption("进入地图", delegate
                    {
                        LongEventHandler.QueueLongEvent(delegate
                        {
                            try
                            {
                                if (!canEnter)
                                {
                                    caravan.pather.StartPath(Tile, null, true);
                                    return;
                                }
                                IntVec3 mapSize = new IntVec3(250, 1, 250);
                                Map map = GetOrGenerateMapUtility.GetOrGenerateMap(this.Tile, mapSize, this.def);
                                map.fogGrid.Refog(CellRect.WholeMap(map));
                                CaravanEnterMapUtility.Enter(caravan, map, CaravanEnterMode.Edge, CaravanDropInventoryMode.DoNotDrop, true);

                                isSpawn = true;
                            }
                            catch (Exception ex)
                            {
                                Log.Error($"RKU: Enter failed for {this.def.defName} at tile {this.Tile}: {ex}");
                                throw;
                            }
                        },
                        "GeneratingMap".Translate(),
                        doAsynchronously: true, (Exception ex) => 
                        {
                            Log.Error($"[RKU] RKU_MapParent的QueueLongEvent报错: {ex}");
                        },
                        true,                                                
                        null
                        );
                    }, MenuOptionPriority.Default, null, null, 0f, null, null);
                }
                
            }
        }
    }
}
