using System;
using RogueElements;
using RogueEssence;
using RogueEssence.Dungeon;
using RogueEssence.Data;

namespace PMDC.Dungeon
{
    [Serializable]
    public class AvoidFoesCornerPlan : AvoidPlan
    {
        public AvoidFoesCornerPlan(AIFlags iq) : base(iq) { }
        protected AvoidFoesCornerPlan(AvoidFoesCornerPlan other) : base(other) { }
        public override BasePlan CreateNew() { return new AvoidFoesCornerPlan(this); }

        protected override bool RunFromAllies { get { return false; } }
        protected override bool RunFromFoes { get { return true; } }
        protected override bool AbortIfCornered { get { return true; } }

    }
}