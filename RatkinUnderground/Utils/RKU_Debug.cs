using LudeonTK;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using static Mono.Security.X509.X520;

namespace RatkinUnderground
{
    public class RKU_Debug
    {
        public static class Debug_GenerateFaction
        {
            [DebugAction(
                category: "RatkinUnderground",
                name: "Generate Faction")]
            private static void GenerateUndergroundFaction()
            {
                // 手动生成派系
                var RKUDef = DefOfs.RKU_Faction;
                if (Find.FactionManager.AllFactions.Any(f => f.def == RKUDef)) return;
                
                FactionGenerator.CreateFactionAndAddToManager(RKUDef);
                
                Faction rFaction = Find.FactionManager.FirstFactionOfDef(RKUDef);
                // 设置敌对关系
                List<FactionDef> enemyFaction = new List<FactionDef>
                {
                    FactionDef.Named("Rakinia_Warlord"),
                    FactionDef.Named("Rakinia")
                };
                if (rFaction == null) return;
                foreach (FactionDef enemy in enemyFaction)
                {
                    Faction eFaction = Find.FactionManager.FirstFactionOfDef(enemy);
                    eFaction.RelationWith(rFaction).baseGoodwill = -100;
                    rFaction.RelationWith(eFaction).baseGoodwill = -100;
                    eFaction.RelationWith(rFaction).kind = FactionRelationKind.Hostile;
                    rFaction.RelationWith(eFaction).kind = FactionRelationKind.Hostile;
                }

                Log.Message($"[RatkinUnderground] Generated new faction: {RKUDef.defName}");
            }
        }


        [DebugAction(
            category: "RatkinUnderground",
            name: "Generate Specified Incident")]
        private static void GenerateSpecifiedIncident()
        {
            IncidentDef def = DefOfs.RKU_Raid;

            // 获取当前地图
            Map map = Find.CurrentMap;
            if (map == null)
            {
                Log.Message("[RatkinUnderground] 当前没有活动地图。");
                return;
            }

            // 构造事件参数
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(def.category, map);

            // 尝试执行事件
            bool success = def.Worker.TryExecute(parms);
            if (success)
                Log.Message($"[RatkinUnderground] 成功触发事件：{def.defName}");
            else
                Log.Message($"[RatkinUnderground] 触发事件失败：{def.defName}");
        }
    }
}
