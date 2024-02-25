using System;
using RogueElements;
using RogueEssence;
using RogueEssence.Dungeon;
using RogueEssence.Data;
using System.Collections.Generic;

namespace PMDC.Dungeon
{
    [Serializable]
    public class SpamAttackPlan : AIPlan
    {
        public SpamAttackPlan() { }
        protected SpamAttackPlan(SpamAttackPlan other) : base(other) { }
        public override BasePlan CreateNew() { return new SpamAttackPlan(this); }

        public override GameAction Think(Character controlledChar, bool preThink, IRandom rand, List<Character> waitingChars)
        {
            //need attack action check
            for (int ii = 0; ii < controlledChar.Skills.Count; ii++)
            {
                if (!String.IsNullOrEmpty(controlledChar.Skills[ii].Element.SkillNum) && controlledChar.Skills[ii].Element.Charges > 0 && !controlledChar.Skills[ii].Element.Sealed && controlledChar.Skills[ii].Element.Enabled)
                    return new GameAction(GameAction.ActionType.UseSkill, Dir8.None, ii);
            }
            return null;
        }
    }
}
