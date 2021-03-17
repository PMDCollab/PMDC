using System;
using RogueElements;
using RogueEssence;
using RogueEssence.Dungeon;
using RogueEssence.Data;

namespace PMDC.Dungeon
{
    [Serializable]
    public class BossPlan : AttackFoesPlan
    {
        public BossPlan(AIFlags iq, AIPlan.AttackChoice attackPattern) : base(iq, attackPattern) { }
        protected BossPlan(BossPlan other) : base(other) { }
        public override BasePlan CreateNew() { return new BossPlan(this); }

        public override GameAction Think(Character controlledChar, bool preThink, ReRandom rand)
        {
            if (controlledChar.HP * 2 < controlledChar.MaxHP)
            {
                //at half health, unlock the all moves
                for (int ii = 0; ii < controlledChar.Skills.Count; ii++)
                {
                    if (controlledChar.Skills[ii].Element.SkillNum > -1)
                        controlledChar.Skills[ii].Element.Enabled = true;
                }
            }
            bool hasBadStatus = false;
            foreach (StatusEffect status in controlledChar.IterateStatusEffects())
            {
                if (status.StatusStates.Contains<BadStatusState>())
                {
                    hasBadStatus = true;
                    break;
                }
            }
            if (hasBadStatus)
            {
                if (controlledChar.EquippedItem.ID == 12)//Lum Berry; NOTE: specialized AI code!
                    return new GameAction(GameAction.ActionType.UseItem, Dir8.None, -1, -1);
            }

            return base.Think(controlledChar, preThink, rand);
        }
    }
}
