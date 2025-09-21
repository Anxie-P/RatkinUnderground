using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace RatkinUnderground
{
    public class LordJob_WaitForSubsidize : LordJob
    {
        private IntVec3 point;
        private float? wanderRadius;
        private float? defendRadius;
        private bool isCaravanSendable;
        private bool addFleeToil;
        private ThingDef thingDef;
        private int amount;
        private Pawn target;

        public override bool IsCaravanSendable => isCaravanSendable;

        public override bool AddFleeToil => addFleeToil;

        public LordJob_WaitForSubsidize()
        {
        }

        public LordJob_WaitForSubsidize(IntVec3 point, ThingDef thingDef, int amount, Pawn target, bool isCaravanSendable = false, bool addFleeToil = true)
        {
            this.point = point;
            this.thingDef = thingDef;
            this.amount = amount;
            this.isCaravanSendable = isCaravanSendable;
            this.addFleeToil = addFleeToil;
            this.target = target;
        }

        public override StateGraph CreateGraph()
        {
            StateGraph stateGraph = new StateGraph();
            LordToil_DefendPoint defendPoint = new LordToil_DefendPoint(point, wanderRadius: wanderRadius, defendRadius: defendRadius);
            stateGraph.AddToil(defendPoint);

            LordToil_WaitForSubsidize subsidizeToil = new LordToil_WaitForSubsidize(point, target, thingDef, amount);
            stateGraph.AddToil(subsidizeToil);
            stateGraph.StartingToil = subsidizeToil;

            Transition transitionToDefend = new Transition(subsidizeToil, defendPoint);
            transitionToDefend.AddTrigger(new Trigger_Custom(_ => subsidizeToil.itemsGivenTriggered));
            stateGraph.AddTransition(transitionToDefend);

            return stateGraph;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref thingDef, "thingDef");
            Scribe_Values.Look(ref amount, "amount");
            Scribe_Values.Look(ref target, "target");
            Scribe_Values.Look(ref point, "point");
            Scribe_Values.Look(ref wanderRadius, "wanderRadius");
            Scribe_Values.Look(ref defendRadius, "defendRadius");
            Scribe_Values.Look(ref isCaravanSendable, "isCaravanSendable", defaultValue: false);
            Scribe_Values.Look(ref addFleeToil, "addFleeToil", defaultValue: false);
        }
    }
}
