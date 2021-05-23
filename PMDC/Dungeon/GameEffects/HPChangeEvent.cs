using System;
using RogueEssence;
using RogueEssence.Dungeon;
using RogueEssence.Data;
using RogueEssence.Dev;
using System.Collections.Generic;

namespace PMDC.Dungeon
{

    [Serializable]
    public class HealMultEvent : HPChangeEvent
    {
        public int Numerator;
        public int Denominator;
        
        public HealMultEvent() { }
        public HealMultEvent(int numerator, int denominator)
        {
            Numerator = numerator;
            Denominator = denominator;
        }
        protected HealMultEvent(HealMultEvent other)
        {
            Numerator = other.Numerator;
            Denominator = other.Denominator;
        }
        public override GameEvent Clone() { return new HealMultEvent(this); }

        public override void Apply(GameEventOwner owner, Character ownerChar, ref int hpChange)
        {
            if (hpChange > 0)
            {
                hpChange *= Numerator;
                hpChange /= Denominator;
            }
        }
    }


    [Serializable]
    public class FamilyHPEvent : HPChangeEvent
    {
        public HPChangeEvent BaseEvent;

        public FamilyHPEvent() { }
        public FamilyHPEvent(HPChangeEvent baseEvent) { BaseEvent = baseEvent; }
        protected FamilyHPEvent(FamilyHPEvent other)
        {
            BaseEvent = (HPChangeEvent)other.BaseEvent.Clone();
        }
        public override GameEvent Clone() { return new FamilyHPEvent(this); }

        public override void Apply(GameEventOwner owner, Character ownerChar, ref int hpChange)
        {
            ItemData entry = DataManager.Instance.GetItem(owner.GetID());
            FamilyState family;
            if (!entry.ItemStates.TryGet<FamilyState>(out family))
                return;

            if (family.Members.Contains(ownerChar.BaseForm.Species))
                BaseEvent.Apply(owner, ownerChar, ref hpChange);
        }
    }
}
