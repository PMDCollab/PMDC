using System;
using RogueElements;
using RogueEssence;
using RogueEssence.Dungeon;
using RogueEssence.Data;

namespace PMDC.Dungeon
{
    [Serializable]
    public class WaitPeriodPlan : AIPlan
    {
        public int Turns;

        public WaitPeriodPlan(AIFlags iq, int turns) : base(iq)
        {
            Turns = turns;
        }
        protected WaitPeriodPlan(WaitPeriodPlan other) : base(other) { Turns = other.Turns; }
        public override BasePlan CreateNew() { return new WaitPeriodPlan(this); }

        public override GameAction Think(Character controlledChar, bool preThink, ReRandom rand)
        {
            if (ZoneManager.Instance.CurrentMap.MapTurns % Turns == 0)
                return null;
            return new GameAction(GameAction.ActionType.Wait, Dir8.None);
        }
    }
}
