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

        public override GameAction Think(Character controlledChar, bool preThink, ReRandom rand)
        {
            bool teamPartner = (IQ & AIFlags.TeamPartner) != AIFlags.None;
            Character target = null;
            List<Character> seenCharacters = controlledChar.GetSeenCharacters(GetAcceptableTargets());
            foreach (Character seenChar in seenCharacters)
            {
                if (!teamPartner || teamPartnerCanAttack(seenChar))
                    target = seenChar;
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

        public override GameAction Think(Character controlledChar, bool preThink, ReRandom rand)
        {
            if (controlledChar.GetStatusEffect(FirstMoveStatus) != null)
                return null;

            bool teamPartner = (IQ & AIFlags.TeamPartner) != AIFlags.None;
            Character target = null;
            List<Character> seenCharacters = controlledChar.GetSeenCharacters(GetAcceptableTargets());
            foreach (Character seenChar in seenCharacters)
            {
                if (!teamPartner || teamPartnerCanAttack(seenChar))
                    target = seenChar;
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

        public override GameAction Think(Character controlledChar, bool preThink, ReRandom rand)
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
