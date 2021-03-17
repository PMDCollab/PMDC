using System;
using RogueEssence;
using RogueEssence.Dungeon;

namespace PMDC.Dungeon
{

    [Serializable]
    public class UpdateIndicesEvent : SkillChangeEvent
    {
        public override GameEvent Clone() { return new UpdateIndicesEvent(); }

        public override void Apply(GameEventOwner owner, Character character, int[] moveIndices)
        {
            SlotState statusState = ((StatusEffect)owner).StatusStates.GetWithDefault<SlotState>();
            statusState.Slot = moveIndices[statusState.Slot];
            if (statusState.Slot == -1)
                character.SilentRemoveStatus(((StatusEffect)owner).ID);
        }
    }
}
