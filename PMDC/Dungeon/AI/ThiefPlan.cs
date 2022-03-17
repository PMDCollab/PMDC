using System;
using RogueElements;
using RogueEssence;
using RogueEssence.Dungeon;
using RogueEssence.Data;

namespace PMDC.Dungeon
{
    /// <summary>
    /// will attack players if its original item is their current item, or if its has no item
    /// will use dash moves to get away, if given the chance
    /// runs away using A*
    /// does not pick up items
    /// </summary>
    [Serializable]
    public class ThiefPlan : AvoidPlan
    {
        //for thieves and switcheroo thieves
        
        private int origItem;
        public ThiefPlan(AIFlags iq) : base(iq)
        {
            origItem = -1;
        }
        protected ThiefPlan(ThiefPlan other) : base(other) { }
        public override BasePlan CreateNew() { return new ThiefPlan(this); }

        protected override bool RunFromAllies { get { return false; } }
        protected override bool RunFromFoes { get { return true; } }
        protected override bool AbortIfCornered { get { return true; } }

        public override void Initialize(Character controlledChar)
        {
            origItem = controlledChar.EquippedItem.ID;
            base.Initialize(controlledChar);
        }

        public override GameAction Think(Character controlledChar, bool preThink, IRandom rand)
        {
            if (controlledChar.EquippedItem.ID != origItem && controlledChar.EquippedItem.ID > -1)//we have a held item that is different now
                return base.Think(controlledChar, preThink, rand);

            return null;
        }
    }
}
