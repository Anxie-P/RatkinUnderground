using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RatkinUnderground
{
    public class RKU_FactionComponent : GameComponent
    {
        List<FactionDef> enemyFaction = new List<FactionDef>
        {
            FactionDef.Named("Rakinia_Warlord"),
            FactionDef.Named("Rakinia")
        };

        public RKU_FactionComponent(Game game) { }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            SetPermanentEnemies();
        }

        public override void StartedNewGame()
        {
            base.StartedNewGame();
            SetPermanentEnemies();
        }

        public override void LoadedGame()
        {
            base.LoadedGame();
            SetPermanentEnemies();
        }
        public void SetPermanentEnemies()
        {
            Faction rFaction = Find.FactionManager.FirstFactionOfDef(DefOfs.RKU_Faction);
            /*Log.Message("派系修复已挂载");*/
            if (rFaction == null) return;
            foreach (FactionDef enemy in enemyFaction)
            {
                Faction eFaction = Find.FactionManager.FirstFactionOfDef(enemy);
                /*Log.Message($"当前关系：{rFaction.RelationWith(eFaction).baseGoodwill}");*/
                eFaction.RelationWith(rFaction).baseGoodwill = -100;
                rFaction.RelationWith(eFaction).baseGoodwill = -100;
                eFaction.RelationWith(rFaction).kind = FactionRelationKind.Hostile;
                rFaction.RelationWith(eFaction).kind = FactionRelationKind.Hostile;
                /*Log.Message($"已设置{rFaction.Name}与{enemy.defName}永久敌对");
                Log.Message($"修改后的关系：{rFaction.RelationWith(eFaction).baseGoodwill}");*/
            }
        }
    }
}
