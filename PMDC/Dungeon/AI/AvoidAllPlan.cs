using System;
using RogueElements;
using RogueEssence;
using RogueEssence.Dungeon;
using RogueEssence.Data;

namespace PMDC.Dungeon
{
    [Serializable]
    public class AvoidAllPlan : AvoidPlan
    {
        public AvoidAllPlan(AIFlags iq, AttackChoice attackPattern) : base(iq, attackPattern) { }
        protected AvoidAllPlan(AvoidAllPlan other) : base(other) { }
        public override BasePlan CreateNew() { return new AvoidAllPlan(this); }

        protected override bool RunFromAllies { get { return true; } }
        protected override bool RunFromFoes { get { return true; } }
        protected override bool AbortIfCornered { get { return false; } }

    }

}
