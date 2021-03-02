using System;
using RogueEssence;
using RogueEssence.Dungeon;
using RogueEssence.Data;
using RogueEssence.Dev;
using System.Collections.Generic;

namespace PMDO.Dungeon
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
        [DataType(1, DataManager.DataType.Monster, false)]
        public List<int> Members;

        public HPChangeEvent BaseEvent;

        public FamilyHPEvent() { Members = new List<int>(); }
        public FamilyHPEvent(List<int> members, HPChangeEvent baseEvent) { Members = members; BaseEvent = baseEvent; }
        protected FamilyHPEvent(FamilyHPEvent other)
        {
            Members = new List<int>();
            Members.AddRange(other.Members);
            BaseEvent = (HPChangeEvent)other.BaseEvent.Clone();
        }
        public override GameEvent Clone() { return new FamilyHPEvent(this); }

        public override void Apply(GameEventOwner owner, Character ownerChar, ref int hpChange)
        {
            if (Members.Contains(ownerChar.BaseForm.Species))
            {
                BaseEvent.Apply(owner, ownerChar, ref hpChange);
            }
        }
    }
}
