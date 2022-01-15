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

        public override GameAction Think(Character controlledChar, bool preThink, IRandom rand)
        {
            return new GameAction(GameAction.ActionType.Wait, Dir8.None);
        }
    }

    [Serializable]
    public class WaitWithLeaderPlan : AIPlan
    {
        public WaitWithLeaderPlan() { }
        public WaitWithLeaderPlan(AIFlags iq) : base(iq) { }
        protected WaitWithLeaderPlan(WaitWithLeaderPlan other) : base(other) { }
        public override BasePlan CreateNew() { return new WaitWithLeaderPlan(this); }

        public override GameAction Think(Character controlledChar, bool preThink, IRandom rand)
        {
            //check if there's an ally of higher rank than self visible, wait
            foreach (Character testChar in controlledChar.MemberTeam.IterateByRank())
            {
                //if we have gotten to this character, we could not find a leader
                if (testChar == controlledChar)
                    break;
                else if (controlledChar.IsInSightBounds(testChar.CharLoc))
                {
                    //if we saw our leader, we wait.
                    return new GameAction(GameAction.ActionType.Wait, Dir8.None);
                }
            }
            return null;
        }
    }
}
