using System;
using RogueElements;
using System.Collections.Generic;
using RogueEssence;
using RogueEssence.Dungeon;
using RogueEssence.Data;

namespace PMDO.Dungeon
{
    [Serializable]
    public class PreparePlan : AIPlan
    {
        public PreparePlan() { }
        public PreparePlan(AIFlags iq, AttackChoice attackPattern) : base(iq, attackPattern) { }
        public PreparePlan(PreparePlan other) : base(other) { }
        public override BasePlan CreateNew() { return new PreparePlan(this); }

        public override GameAction Think(Character controlledChar, bool preThink, ReRandom rand)
        {
            List<Character> seenCharacters = controlledChar.GetSeenCharacters(GetAcceptableTargets());
            //need attack action check
            GameAction attackCommand = TryAttackChoice(rand, controlledChar, (seenCharacters.Count > 0) ? seenCharacters[0] : null);
            if (attackCommand.Type != GameAction.ActionType.Wait)
                return attackCommand;

            return null;
        }
    }
}
