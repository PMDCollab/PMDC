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
            Character target = null;
            StatusEffect lastHit = controlledChar.GetStatusEffect(StatusIndex);
            if (lastHit != null && lastHit.TargetChar != null)
                target = lastHit.TargetChar;
            Faction foeFaction = (IQ & AIFlags.NeutralFoeConflict) != AIFlags.None ? Faction.Foe : Faction.None;
            List<Character> seenCharacters = controlledChar.GetSeenCharacters(GetAcceptableTargets(), foeFaction);
            if (target == null && seenCharacters.Count > 0)
                target = seenCharacters[0];

            //need attack action check
            if (target != null)
            {
                GameAction attackCommand = TryAttackChoice(rand, controlledChar, AttackPattern);
                if (attackCommand.Type != GameAction.ActionType.Wait)
                    return attackCommand;
            }

            return null;
        }
    }
}
