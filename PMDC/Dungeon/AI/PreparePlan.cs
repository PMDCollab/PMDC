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
        public PreparePlan(PreparePlan other) : base(other) { StatusIndex = other.StatusIndex; }
        public override BasePlan CreateNew() { return new PreparePlan(this); }

        public override GameAction Think(Character controlledChar, bool preThink, ReRandom rand)
        {
            Faction foeFaction = (IQ & AIFlags.NeutralFoeConflict) != AIFlags.None ? Faction.Foe : Faction.None;
            List<Character> seenCharacters = controlledChar.GetSeenCharacters(GetAcceptableTargets(), foeFaction);
            Character target = null;
            if (seenCharacters.Count > 0)
                target = seenCharacters[0];
            StatusEffect lastHit = controlledChar.GetStatusEffect(StatusIndex);
            if (lastHit != null && lastHit.TargetChar != null)
                target = lastHit.TargetChar;

            //need attack action check
            GameAction attackCommand = TryAttackChoice(rand, controlledChar, target, AttackPattern);
            if (attackCommand.Type != GameAction.ActionType.Wait)
                return attackCommand;

            return null;
        }
    }
}
