using System;
using RogueElements;
using System.Collections.Generic;
using RogueEssence;
using RogueEssence.Dungeon;
using RogueEssence.Data;

namespace PMDC.Dungeon
{
    [Serializable]
    public class PreparePlan : AIPlan
    {
        public AttackChoice AttackPattern;

        public PreparePlan() { }
        public PreparePlan(AIFlags iq, AttackChoice attackPattern) : base(iq)
        {
            AttackPattern = attackPattern;
        }
        public PreparePlan(PreparePlan other) : base(other)
        {
            AttackPattern = other.AttackPattern;
        }
        public override BasePlan CreateNew() { return new PreparePlan(this); }

        public override GameAction Think(Character controlledChar, bool preThink, IRandom rand)
        {
            bool playerSense = (IQ & AIFlags.PlayerSense) != AIFlags.None;
            Character target = null;
            foreach (Character seenChar in GetAcceptableTargets(controlledChar))
            {
                target = seenChar;
                break;
            }

            //need attack action check
            if (target != null)
            {
                GameAction attackCommand = TryAttackChoice(rand, controlledChar, AttackPattern);
                if (attackCommand.Type != GameAction.ActionType.Wait)
                    return attackCommand;
                attackCommand = TryAttackChoice(rand, controlledChar, AttackChoice.StandardAttack);
                if (attackCommand.Type != GameAction.ActionType.Wait)
                    return attackCommand;
            }

            return null;
        }
    }

    [Serializable]
    public class PrepareWithLeaderPlan : AIPlan
    {
        public AttackChoice AttackPattern;

        public PrepareWithLeaderPlan() { }
        public PrepareWithLeaderPlan(AIFlags iq, AttackChoice attackPattern) : base(iq)
        {
            AttackPattern = attackPattern;
        }
        public PrepareWithLeaderPlan(PrepareWithLeaderPlan other) : base(other)
        {
            AttackPattern = other.AttackPattern;
        }
        public override BasePlan CreateNew() { return new PrepareWithLeaderPlan(this); }

        public override GameAction Think(Character controlledChar, bool preThink, IRandom rand)
        {
            //check to see if close to highest leader
            foreach (Character testChar in controlledChar.MemberTeam.IterateByRank())
            {
                //no leader found?  don't be preparing.
                if (testChar == controlledChar)
                    return null;
                else if (controlledChar.IsInSightBounds(testChar.CharLoc))
                {
                    //only check the first leader that is within sight
                    //leader found; check if nearby
                    if (ZoneManager.Instance.CurrentMap.InRange(testChar.CharLoc, controlledChar.CharLoc, 1))
                        break;
                    else
                        return null;
                }
            }

            bool playerSense = (IQ & AIFlags.PlayerSense) != AIFlags.None;
            Character target = null;
            foreach (Character seenChar in GetAcceptableTargets(controlledChar))
            {
                target = seenChar;
                break;
            }

            //need attack action check
            if (target != null)
            {
                GameAction attackCommand = TryAttackChoice(rand, controlledChar, AttackPattern);
                if (attackCommand.Type != GameAction.ActionType.Wait)
                    return attackCommand;
                attackCommand = TryAttackChoice(rand, controlledChar, AttackChoice.StandardAttack);
                if (attackCommand.Type != GameAction.ActionType.Wait)
                    return attackCommand;
            }

            return null;
        }
    }


    [Serializable]
    public class PreBuffPlan : AIPlan
    {
        public int FirstMoveStatus;

        public PreBuffPlan() { }
        public PreBuffPlan(AIFlags iq, int firstMoveStatus) : base(iq)
        {
            FirstMoveStatus = firstMoveStatus;
        }
        public PreBuffPlan(PreBuffPlan other) : base(other)
        {
            FirstMoveStatus = other.FirstMoveStatus;
        }
        public override BasePlan CreateNew() { return new PreBuffPlan(this); }

        public override GameAction Think(Character controlledChar, bool preThink, IRandom rand)
        {
            if (controlledChar.GetStatusEffect(FirstMoveStatus) != null)
                return null;

            bool playerSense = (IQ & AIFlags.PlayerSense) != AIFlags.None;
            Character target = null;
            foreach (Character seenChar in GetAcceptableTargets(controlledChar))
            {
                target = seenChar;
                break;
            }

            //need attack action check
            if (target != null)
            {
                GameAction attackCommand = TryAttackChoice(rand, controlledChar, AttackChoice.StatusAttack);
                if (attackCommand.Type != GameAction.ActionType.Wait)
                    return attackCommand;
            }

            return null;
        }
    }




    [Serializable]
    public class LeadSkillPlan : AIPlan
    {
        public int FirstMoveStatus;

        public LeadSkillPlan() { }
        public LeadSkillPlan(AIFlags iq, int firstMoveStatus) : base(iq)
        {
            FirstMoveStatus = firstMoveStatus;
        }
        public LeadSkillPlan(LeadSkillPlan other) : base(other)
        {
            FirstMoveStatus = other.FirstMoveStatus;
        }
        public override BasePlan CreateNew() { return new LeadSkillPlan(this); }

        public override GameAction Think(Character controlledChar, bool preThink, IRandom rand)
        {
            if (controlledChar.GetStatusEffect(FirstMoveStatus) != null)
                return null;

            if (controlledChar.AttackOnly)
                return null;

            //use the first attack
            if (IsSkillUsable(controlledChar, 0))
                return new GameAction(GameAction.ActionType.UseSkill, controlledChar.CharDir, 0);

            return null;
        }
    }
}
