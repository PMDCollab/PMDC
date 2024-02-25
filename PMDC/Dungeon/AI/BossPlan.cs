using System;
using RogueElements;
using RogueEssence;
using RogueEssence.Dungeon;
using RogueEssence.Data;
using System.Collections.Generic;

namespace PMDC.Dungeon
{
    [Serializable]
    public class BossPlan : AttackFoesPlan
    {
        public BossPlan(AIFlags iq, int attackRange, int statusRange, int selfStatusRange, AIPlan.AttackChoice attackPattern, PositionChoice positionPattern, TerrainData.Mobility restrictedMobilityTypes, bool restrictMobilityPassable) : base(iq, attackRange, statusRange, selfStatusRange, attackPattern, positionPattern, restrictedMobilityTypes, restrictMobilityPassable) { }
        protected BossPlan(BossPlan other) : base(other) { }
        public override BasePlan CreateNew() { return new BossPlan(this); }

        public override GameAction Think(Character controlledChar, bool preThink, IRandom rand, List<Character> waitingChars)
        {
            if (controlledChar.HP * 2 < controlledChar.MaxHP)
            {
                //at half health, unlock the all moves
                for (int ii = 0; ii < controlledChar.Skills.Count; ii++)
                {
                    if (!String.IsNullOrEmpty(controlledChar.Skills[ii].Element.SkillNum))
                        controlledChar.Skills[ii].Element.Enabled = true;
                }
            }

            return base.Think(controlledChar, preThink, rand, waitingChars);
        }
    }
}
