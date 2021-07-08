using System;
using RogueElements;
using RogueEssence;
using RogueEssence.Dungeon;
using RogueEssence.Data;

namespace PMDC.Dungeon
{
    [Serializable]
    public class AvoidFoesPlan : AvoidPlan
    {
        public AvoidFoesPlan(AIFlags iq) : base(iq) { }
        protected AvoidFoesPlan(AvoidFoesPlan other) : base(other) { }
        public override BasePlan CreateNew() { return new AvoidFoesPlan(this); }

        protected override bool RunFromAllies { get { return false; } }
        protected override bool RunFromFoes { get { return true; } }
        protected override bool AbortIfCornered { get { return false; } }

    }
}
