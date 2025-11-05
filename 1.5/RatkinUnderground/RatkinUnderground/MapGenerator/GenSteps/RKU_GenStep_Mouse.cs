using RimWorld;
using System;
using System.Collections.Generic;
using Verse;

namespace RatkinUnderground
{
    public class RKU_GenStep_Mouse : GenStep
    {
        public float mouseSpawnChancePerSoilCell = 0.001f; 
        public float ratToRabbitChance = 0.01f; 

        public override int SeedPart => 123456789; // 随机种子部分

        public override void Generate(Map map, GenStepParams parms)
        {
            if (map == null) return;

            foreach (IntVec3 cell in map.AllCells)
            {
                if (IsSoilTerrain(cell, map))
                {
                    if (Rand.Value < mouseSpawnChancePerSoilCell)
                    {
                        TrySpawnMouse(cell, map);
                    }
                }
            }
        }
        private bool IsSoilTerrain(IntVec3 cell, Map map)
        {
            TerrainDef terrain = map.terrainGrid.TerrainAt(cell);
            if (terrain == null) return false;
            return terrain.defName.Contains("Soil") ||
                   terrain == TerrainDefOf.Soil;
        }
        private void TrySpawnMouse(IntVec3 cell, Map map)
        {
            if (!cell.InBounds(map)) return;
            if (cell.GetEdifice(map) != null) return;
            if (cell.GetFirstPawn(map) != null) return;

            ThingDef animalDef = (Rand.Range(0,1f) < ratToRabbitChance) ? ThingDef.Named("Hare") : ThingDef.Named("Rat");

            Pawn animal = PawnGenerator.GeneratePawn(animalDef.race.AnyPawnKind, null);
            if (animal != null)
            {
                GenSpawn.Spawn(animal, cell, map);
                if (animal.mindState != null)
                {
                    animal.mindState.mentalStateHandler.Reset();
                }
            }
        }
    }
}
