using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse.Noise;
using Verse;

namespace RatkinUnderground
{
    public class RKU_GenStep_ElevationFertility : GenStep
    {
        public override int SeedPart => 826504671;

        public override void Generate(Map map, GenStepParams parms)
        {
            NoiseRenderer.renderSize = new IntVec2(map.Size.x, map.Size.z);

            // 直接设置所有格子的高度为最大值
            MapGenFloatGrid elevation = MapGenerator.Elevation;
            if (elevation == null)
            {
                return;
            }
            int cellsSet = 0;
            foreach (IntVec3 allCell in map.AllCells)
            {
                elevation[allCell] = MapGenTuning.ElevationFactorImpassableMountains;
                cellsSet++;
            }
            // 设置所有格子的肥沃度为0（因为都是山）
            MapGenFloatGrid fertility = MapGenerator.Fertility;
            if (fertility == null)
            {
                return;
            }
            foreach (IntVec3 allCell in map.AllCells)
            {
                fertility[allCell] = 0f;
            }
        }
    }
}
