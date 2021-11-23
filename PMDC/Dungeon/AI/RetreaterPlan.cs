using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence;
using RogueEssence.Dungeon;
using RogueEssence.Data;

namespace PMDC.Dungeon
{
    [Serializable]
    public class RetreaterPlan : AvoidPlan
    {
        public int Factor;

        public RetreaterPlan(AIFlags iq, int factor) : base(iq) { Factor = factor; }
        protected RetreaterPlan(RetreaterPlan other) : base(other) { Factor = other.Factor; }
        public override BasePlan CreateNew() { return new RetreaterPlan(this); }

        protected override bool RunFromAllies { get { return false; } }
        protected override bool RunFromFoes { get { return true; } }
        protected override bool AbortIfCornered { get { return true; } }

        public override GameAction Think(Character controlledChar, bool preThink, IRandom rand)
        {
            if (controlledChar.HP * Factor < controlledChar.MaxHP)
            {
                List<Character> seenCharacters = controlledChar.GetSeenCharacters(Alignment.Foe);
                if (seenCharacters.Count == 0)
                    return null;

                if (!controlledChar.AttackOnly)
                {
                    for (int ii = 0; ii < controlledChar.Skills.Count; ii++)
                    {
                        if (controlledChar.Skills[ii].Element.SkillNum > -1 && controlledChar.Skills[ii].Element.Charges > 0 && !controlledChar.Skills[ii].Element.Sealed && controlledChar.Skills[ii].Element.Enabled)
                        {
                            if (controlledChar.Skills[ii].Element.SkillNum == 100)//Teleport; NOTE: specialized AI code!
                                return new GameAction(GameAction.ActionType.UseSkill, Dir8.None, ii);
                        }
                    }
                }

                return base.Think(controlledChar, preThink, rand);
            }

            return null;
        }
    }
}
