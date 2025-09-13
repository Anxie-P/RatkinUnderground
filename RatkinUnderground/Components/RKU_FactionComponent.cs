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
            if (rFaction == null) return;
            foreach (FactionDef enemy in enemyFaction)
            {
                Faction eFaction = Find.FactionManager.FirstFactionOfDef(enemy);
                eFaction.RelationWith(rFaction).baseGoodwill = -100;
                rFaction.RelationWith(eFaction).baseGoodwill = -100;
                FactionRelationKind oldKind1 = eFaction.RelationWith(rFaction).kind;
                FactionRelationKind oldKind2 = rFaction.RelationWith(eFaction).kind;
                eFaction.RelationWith(rFaction).kind = FactionRelationKind.Hostile;
                rFaction.RelationWith(eFaction).kind = FactionRelationKind.Hostile;
                eFaction.Notify_RelationKindChanged(rFaction, oldKind1, false, "", TargetInfo.Invalid, out var sentLetter1);
                rFaction.Notify_RelationKindChanged(eFaction, oldKind2, false, "", TargetInfo.Invalid, out var sentLetter2);
            }
        }
    }
}
