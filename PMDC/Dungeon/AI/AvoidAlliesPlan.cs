using System;
using RogueElements;
using RogueEssence;
using RogueEssence.Dungeon;
using RogueEssence.Data;

namespace PMDC.Dungeon
{
    [Serializable]
    public class AvoidAlliesPlan : AvoidPlan
    {

        public AvoidAlliesPlan(AIFlags iq) : base(iq) { }
        protected AvoidAlliesPlan(AvoidAlliesPlan other) : base(other) { }
        public override BasePlan CreateNew() { return new AvoidAlliesPlan(this); }

        protected override bool RunFromAllies { get { return true; } }
        protected override bool RunFromFoes { get { return false; } }
        protected override bool AbortIfCornered { get { return false; } }

    }
}
