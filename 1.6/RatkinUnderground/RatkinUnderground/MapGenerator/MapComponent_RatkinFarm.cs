using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RatkinUnderground
{
    public class MapComponent_RatkinFarm : MapComponent
    {
        private List<Pawn> civilians = new List<Pawn>();
        private const int DETECTION_RADIUS = 5;

        public MapComponent_RatkinFarm(Map map) : base(map)
        {
        }

        public void RegisterCivilians(List<Pawn> newCivilians)
        {
            civilians.AddRange(newCivilians);
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (Find.TickManager.TicksGame % 60 != 0) return;

            CheckAndConvertCivilians();
        }

        private void CheckAndConvertCivilians()
        {
            var civiliansToRemove = new List<Pawn>();

            foreach (var civilian in civilians)
            {
                if (civilian == null || civilian.Destroyed || civilian.Dead)
                {
                    civiliansToRemove.Add(civilian);
                    continue;
                }
                if (IsPlayerOrGuerrillaNearby(civilian))
                {
                    ConvertToGuerrillaAndLeave(civilian);
                    civiliansToRemove.Add(civilian);
                }
            }

            foreach (var civilian in civiliansToRemove)
            {
                civilians.Remove(civilian);
            }
        }

        private bool IsPlayerOrGuerrillaNearby(Pawn civilian)
        {
            var guerrillaFaction = Find.FactionManager.FirstFactionOfDef(DefOfs.RKU_Faction);
            if (guerrillaFaction == null) return false;

            foreach (var cell in GenRadial.RadialCellsAround(civilian.Position, DETECTION_RADIUS, true))
            {
                if (!cell.InBounds(map)) continue;

                var pawns = cell.GetThingList(map).OfType<Pawn>();
                foreach (var pawn in pawns)
                {
                    if (pawn == civilian) continue;
                    // 平民检查是否为玩家殖民者或游击队成员
                    if ((pawn.Faction != null && pawn.Faction.IsPlayer) ||
                        (pawn.Faction == guerrillaFaction))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void ConvertToGuerrillaAndLeave(Pawn civilian)
        {
            var guerrillaFaction = Find.FactionManager.FirstFactionOfDef(DefOfs.RKU_Faction);
            if (guerrillaFaction == null) return;

            civilian.SetFaction(guerrillaFaction);
            var civilianHediff = civilian.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("RKU_CivilianMarker"));
            if (civilianHediff != null)
            {
                civilian.health.RemoveHediff(civilianHediff);
            }
            if (civilian.GetLord() != null)
            {
                civilian.GetLord().Notify_PawnLost(civilian, PawnLostCondition.LeftVoluntarily);
            }
        }
    }
}
