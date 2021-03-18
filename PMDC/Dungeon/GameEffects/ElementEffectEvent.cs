using System;
using RogueEssence.Data;
using RogueEssence;
using RogueEssence.Dungeon;
using RogueEssence.Dev;
using System.Collections.Generic;

namespace PMDC.Dungeon
{
    [Serializable]
    public class PreTypeEvent : ElementEffectEvent
    {

        public static int N_E = 0;
        public static int NVE = 3;
        public static int NRM = 4;
        public static int S_E = 5;


        static readonly int[,] TypeMatchup = {{NRM,NRM,NRM,NRM,NRM,NRM,NRM,NRM,NRM,NRM,NRM,NRM,NRM,NRM,NRM,NRM,NRM,NRM,NRM},
                                        {NRM,NRM,S_E,NRM,NRM,NVE,NVE,NVE,NVE,NVE,S_E,NRM,NRM,NRM,NVE,S_E,NRM,NVE,NRM},
                                        {NRM,NRM,NVE,NRM,NRM,NVE,NVE,NRM,NRM,S_E,NRM,NRM,NRM,NRM,NRM,S_E,NRM,NRM,NRM},
                                        {NRM,NRM,NRM,S_E,NRM,N_E,NRM,NRM,NRM,NRM,NRM,NRM,NRM,NRM,NRM,NRM,NRM,NVE,NRM},
                                        {NRM,NRM,NRM,NVE,NVE,NRM,NRM,NRM,S_E,NRM,NVE,N_E,NRM,NRM,NRM,NRM,NRM,NRM,S_E},
                                        {NRM,NRM,S_E,S_E,NRM,NRM,S_E,NVE,NRM,NRM,NRM,NRM,NRM,NRM,NVE,NRM,NRM,NVE,NRM},
                                        {NRM,NVE,S_E,NRM,NRM,NVE,NRM,NRM,NVE,N_E,NRM,NRM,S_E,S_E,NVE,NVE,S_E,S_E,NRM},
                                        {NRM,S_E,NRM,NVE,NRM,NRM,NRM,NVE,NRM,NRM,S_E,NRM,S_E,NRM,NRM,NRM,NVE,S_E,NVE},
                                        {NRM,S_E,NRM,NRM,NVE,NRM,S_E,NRM,NRM,NRM,S_E,NRM,NRM,NRM,NRM,NRM,NVE,NVE,NRM},
                                        {NRM,NRM,NVE,NRM,NRM,NRM,NRM,NRM,NRM,S_E,NRM,NRM,NRM,N_E,NRM,S_E,NRM,NRM,NRM},
                                        {NRM,NVE,NRM,NVE,NRM,NRM,NRM,NVE,NVE,NRM,NVE,S_E,NRM,NRM,NVE,NRM,S_E,NVE,S_E},
                                        {NRM,NVE,NRM,NRM,S_E,NRM,NRM,S_E,N_E,NRM,NVE,NRM,NRM,NRM,S_E,NRM,S_E,S_E,NRM},
                                        {NRM,NRM,NRM,S_E,NRM,NRM,NRM,NVE,S_E,NRM,S_E,S_E,NVE,NRM,NRM,NRM,NRM,NVE,NVE},
                                        {NRM,NRM,NRM,NRM,NRM,NRM,NRM,NRM,NRM,N_E,NRM,NRM,NRM,NRM,NRM,NRM,NVE,NVE,NRM},
                                        {NRM,NRM,NRM,NRM,NRM,S_E,NRM,NRM,NRM,NVE,S_E,NVE,NRM,NRM,NVE,NRM,NVE,N_E,NRM},
                                        {NRM,NRM,N_E,NRM,NRM,NRM,S_E,NRM,NRM,NRM,NRM,NRM,NRM,NRM,S_E,NVE,NRM,NVE,NRM},
                                        {NRM,S_E,NRM,NRM,NRM,NRM,NVE,S_E,S_E,NRM,NRM,NVE,S_E,NRM,NRM,NRM,NRM,NVE,NRM},
                                        {NRM,NRM,NRM,NRM,NVE,S_E,NRM,NVE,NRM,NRM,NRM,NRM,S_E,NRM,NRM,NRM,S_E,NVE,NVE},
                                        {NRM,NRM,NRM,NVE,NRM,NRM,NRM,S_E,NRM,NRM,NVE,S_E,NRM,NRM,NRM,NRM,S_E,NRM,NVE}};

        public const int N_E_2 = 5;
        public const int NVE_2 = 7;
        public const int NRM_2 = 8;
        public const int S_E_2 = 9;

        public static readonly int[] Effectiveness = new int[11] { 0, 0, 0, 0, 0, 0, 1, 2, 4, 6, 8 };

        public static string EffectivenessToPhrase(int effectiveness)
        {
            if (effectiveness <= N_E_2)
                return new StringKey("MSG_MATCHUP_NE").ToLocal();
            if (effectiveness < NVE_2)
                return new StringKey("MSG_MATCHUP_NVE_2").ToLocal();
            else if (effectiveness == NVE_2)
                return new StringKey("MSG_MATCHUP_NVE").ToLocal();
            else if (effectiveness == S_E_2)
                return new StringKey("MSG_MATCHUP_SE").ToLocal();
            else if (effectiveness > S_E_2)
                return new StringKey("MSG_MATCHUP_SE_2").ToLocal();
            else
                return null;
        }

        public static int CalculateTypeMatchup(int attackerType, int targetType)
        {
            return TypeMatchup[attackerType, targetType];
        }


        public static int GetDualEffectiveness(Character attacker, Character target, int targetElement)
        {
            return (DungeonScene.GetEffectiveness(attacker, target, targetElement, target.Element1) + DungeonScene.GetEffectiveness(attacker, target, targetElement, target.Element2));
        }

        public static int GetDualEffectiveness(Character attacker, Character target, BattleData skill)
        {
            return (DungeonScene.GetEffectiveness(attacker, target, skill, target.Element1) + DungeonScene.GetEffectiveness(attacker, target, skill, target.Element2));
        }


        public override GameEvent Clone() { return new PreTypeEvent(); }

        public override void Apply(GameEventOwner owner, Character ownerChar, int moveType, int targetType, ref int effectiveness)
        {
            effectiveness = CalculateTypeMatchup(moveType, targetType);
        }
    }

    [Serializable]
    public class FamilyMatchupEvent : ElementEffectEvent
    {
        [DataType(1, DataManager.DataType.Monster, false)]
        public List<int> Members;

        public ElementEffectEvent BaseEvent;

        public FamilyMatchupEvent() { Members = new List<int>(); }
        public FamilyMatchupEvent(List<int> members, ElementEffectEvent baseEvent) { Members = members; BaseEvent = baseEvent; }
        protected FamilyMatchupEvent(FamilyMatchupEvent other)
        {
            Members = new List<int>();
            Members.AddRange(other.Members);
            BaseEvent = (FamilyMatchupEvent)other.BaseEvent.Clone();
        }
        public override GameEvent Clone() { return new FamilyMatchupEvent(this); }

        public override void Apply(GameEventOwner owner, Character ownerChar, int moveType, int targetType, ref int effectiveness)
        {
            if (Members.Contains(ownerChar.BaseForm.Species))
            {
                BaseEvent.Apply(owner, ownerChar, moveType, targetType, ref effectiveness);
            }
        }
    }

    [Serializable]
    public class RemoveTypeMatchupEvent : ElementEffectEvent
    {
        [DataType(0, DataManager.DataType.Element, false)]
        public int Element;

        public RemoveTypeMatchupEvent() { }
        public RemoveTypeMatchupEvent(int element) { Element = element; }
        protected RemoveTypeMatchupEvent(RemoveTypeMatchupEvent other) { Element = other.Element; }
        public override GameEvent Clone() { return new RemoveTypeMatchupEvent(this); }

        public override void Apply(GameEventOwner owner, Character ownerChar, int moveType, int targetType, ref int effectiveness)
        {
            if (targetType == Element)
                effectiveness = PreTypeEvent.NRM;
        }
    }
    [Serializable]
    public class RemoveTypeWeaknessEvent : ElementEffectEvent
    {
        [DataType(0, DataManager.DataType.Element, false)]
        public int Element;

        public RemoveTypeWeaknessEvent() { }
        public RemoveTypeWeaknessEvent(int element) { Element = element; }
        protected RemoveTypeWeaknessEvent(RemoveTypeWeaknessEvent other) { Element = other.Element; }
        public override GameEvent Clone() { return new RemoveTypeWeaknessEvent(this); }

        public override void Apply(GameEventOwner owner, Character ownerChar, int moveType, int targetType, ref int effectiveness)
        {
            if (targetType == Element && effectiveness > PreTypeEvent.NRM)
                effectiveness = PreTypeEvent.NRM;
        }
    }
    [Serializable]
    public class NoImmunityEvent : ElementEffectEvent
    {
        public override GameEvent Clone() { return new NoImmunityEvent(); }

        public override void Apply(GameEventOwner owner, Character ownerChar, int moveType, int targetType, ref int effectiveness)
        {
            if (effectiveness == PreTypeEvent.N_E)
                effectiveness = PreTypeEvent.NRM;
        }
    }
    [Serializable]
    public class LessImmunityEvent : ElementEffectEvent
    {
        [DataType(0, DataManager.DataType.Element, false)]
        public int AttackElement;
        [DataType(0, DataManager.DataType.Element, false)]
        public int TargetElement;
        public LessImmunityEvent(int attackElement, int targetElement) { AttackElement = attackElement; TargetElement = targetElement; }
        protected LessImmunityEvent(LessImmunityEvent other) { AttackElement = other.AttackElement; TargetElement = other.TargetElement; }
        public override GameEvent Clone() { return new LessImmunityEvent(this); }

        public override void Apply(GameEventOwner owner, Character ownerChar, int moveType, int targetType, ref int effectiveness)
        {
            if (effectiveness == PreTypeEvent.N_E && moveType == AttackElement && targetType == TargetElement)
                effectiveness = PreTypeEvent.NVE;
        }
    }
    [Serializable]
    public class NoResistanceEvent : ElementEffectEvent
    {
        public override GameEvent Clone() { return new NoResistanceEvent(); }

        public override void Apply(GameEventOwner owner, Character ownerChar, int moveType, int targetType, ref int effectiveness)
        {
            if (effectiveness == PreTypeEvent.NVE)
                effectiveness = PreTypeEvent.NRM;
        }
    }
    [Serializable]
    public class TypeImmuneEvent : ElementEffectEvent
    {
        [DataType(0, DataManager.DataType.Element, false)]
        public int Element;

        public TypeImmuneEvent() { }
        public TypeImmuneEvent(int element) { Element = element; }
        protected TypeImmuneEvent(TypeImmuneEvent other) { Element = other.Element; }
        public override GameEvent Clone() { return new TypeImmuneEvent(this); }
        public override void Apply(GameEventOwner owner, Character ownerChar, int moveType, int targetType, ref int effectiveness)
        {
            if (moveType == Element)
                effectiveness = PreTypeEvent.N_E;
        }
    }
    [Serializable]
    public class TypeVulnerableEvent : ElementEffectEvent
    {
        [DataType(0, DataManager.DataType.Element, false)]
        public int Element;

        public TypeVulnerableEvent() { }
        public TypeVulnerableEvent(int element) { Element = element; }
        protected TypeVulnerableEvent(TypeVulnerableEvent other) { Element = other.Element; }
        public override GameEvent Clone() { return new TypeVulnerableEvent(this); }
        public override void Apply(GameEventOwner owner, Character ownerChar, int moveType, int targetType, ref int effectiveness)
        {
            if (moveType == Element && effectiveness == 0)
                effectiveness = PreTypeEvent.NRM;
        }
    }
    [Serializable]
    public class ScrappyEvent : ElementEffectEvent
    {
        [DataType(0, DataManager.DataType.Element, false)]
        public int Element1;
        [DataType(0, DataManager.DataType.Element, false)]
        public int Element2;

        public ScrappyEvent() { }
        public ScrappyEvent(int element1, int element2) { Element1 = element1; Element2 = element2; }
        protected ScrappyEvent(ScrappyEvent other) { Element1 = other.Element1; Element2 = other.Element2; }
        public override GameEvent Clone() { return new ScrappyEvent(this); }
        public override void Apply(GameEventOwner owner, Character ownerChar, int moveType, int targetType, ref int effectiveness)
        {
            if ((moveType == Element1 || moveType == Element2) && effectiveness == 0)
                effectiveness = PreTypeEvent.NRM;
        }
    }
    [Serializable]
    public class TypeSuperEvent : ElementEffectEvent
    {
        [DataType(0, DataManager.DataType.Element, false)]
        public int Element;

        public TypeSuperEvent() { }
        public TypeSuperEvent(int element) { Element = element; }
        protected TypeSuperEvent(TypeSuperEvent other) { Element = other.Element; }
        public override GameEvent Clone() { return new TypeSuperEvent(this); }
        public override void Apply(GameEventOwner owner, Character ownerChar, int moveType, int targetType, ref int effectiveness)
        {
            if (targetType == Element)
                effectiveness = PreTypeEvent.S_E;
        }
    }
    [Serializable]
    public class NormalizeEvent : ElementEffectEvent
    {
        public override GameEvent Clone() { return new NormalizeEvent(); }
        public override void Apply(GameEventOwner owner, Character ownerChar, int moveType, int targetType, ref int effectiveness)
        {
            effectiveness = PreTypeEvent.NRM;
        }
    }
    [Serializable]
    public class InverseEvent : ElementEffectEvent
    {
        public override GameEvent Clone() { return new NormalizeEvent(); }
        public override void Apply(GameEventOwner owner, Character ownerChar, int moveType, int targetType, ref int effectiveness)
        {
            if (effectiveness == PreTypeEvent.N_E || effectiveness == PreTypeEvent.NVE)
                effectiveness = PreTypeEvent.S_E;
            else if (effectiveness == PreTypeEvent.S_E)
                effectiveness = PreTypeEvent.NVE;
        }
    }
}
