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
        public int StatusIndex;

        public PreparePlan() { }
        public PreparePlan(AIFlags iq, AttackChoice attackPattern, int status) : base(iq)
        {
            StatusIndex = status;
            AttackPattern = attackPattern;
        }
        public PreparePlan(PreparePlan other) : base(other)
        {
            StatusIndex = other.StatusIndex;
            AttackPattern = other.AttackPattern;
        }
        public override BasePlan CreateNew() { return new PreparePlan(this); }

        public override GameAction Think(Character controlledChar, bool preThink, ReRandom rand)
        {
            bool teamPartner = (IQ & AIFlags.TeamPartner) != AIFlags.None;
            Character target = null;
            StatusEffect lastHit = controlledChar.GetStatusEffect(StatusIndex);
            if (lastHit != null && lastHit.TargetChar != null)
            {
                if (!teamPartner || teamPartnerCanAttack(lastHit.TargetChar))
                    target = lastHit.TargetChar;
            }
            if (target == null)
            {
                Faction foeFaction = (IQ & AIFlags.NeutralFoeConflict) != AIFlags.None ? Faction.Foe : Faction.None;
                List<Character> seenCharacters = controlledChar.GetSeenCharacters(GetAcceptableTargets(), foeFaction);
                foreach (Character seenChar in seenCharacters)
                {
                    if (!teamPartner || teamPartnerCanAttack(seenChar))
                        target = seenChar;
                }
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
}
