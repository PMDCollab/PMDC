using System;
using RogueElements;
using RogueEssence;
using RogueEssence.Dungeon;
using RogueEssence.Data;

namespace PMDC.Dungeon
{
    [Serializable]
    public class WaitUntilAttackedPlan : AIPlan
    {
        public int StatusIndex;
        public WaitUntilAttackedPlan(AIFlags iq, int status) : base(iq)
        {
            StatusIndex = status;
        }
        protected WaitUntilAttackedPlan(WaitUntilAttackedPlan other) : base(other) { StatusIndex = other.StatusIndex; }
        public override BasePlan CreateNew() { return new WaitUntilAttackedPlan(this); }

        public override GameAction Think(Character controlledChar, bool preThink, ReRandom rand)
        {
            if (controlledChar.GetStatusEffect(StatusIndex) == null)//last targeted by someone; NOTE: specialized AI code!
                return new GameAction(GameAction.ActionType.Wait, Dir8.None);
            return null;
        }
    }
}
