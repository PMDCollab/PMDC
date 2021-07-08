using System;
using RogueElements;
using RogueEssence;
using RogueEssence.Dungeon;
using RogueEssence.Data;

namespace PMDC.Dungeon
{
    [Serializable]
    public class WaitPlan : AIPlan
    {
        public WaitPlan() { }
        public WaitPlan(AIFlags iq) : base(iq) { }
        protected WaitPlan(WaitPlan other) : base(other) { }
        public override BasePlan CreateNew() { return new WaitPlan(this); }

        public override GameAction Think(Character controlledChar, bool preThink, ReRandom rand)
        {
            return new GameAction(GameAction.ActionType.Wait, Dir8.None);
        }
    }
}
