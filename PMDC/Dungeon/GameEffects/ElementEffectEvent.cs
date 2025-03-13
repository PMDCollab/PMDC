using System;
using RogueEssence.Data;
using RogueEssence;
using RogueEssence.Dungeon;
using RogueEssence.Dev;
using System.Collections.Generic;
using RogueElements;
using Newtonsoft.Json;

namespace PMDC.Dungeon
{
    [Serializable]
    public class PreTypeEvent : ElementEffectEvent
    {

        public const int N_E = 0;
        public const int NVE = 3;
        public const int NRM = 4;
        public const int S_E = 5;

        public const int N_E_2 = 5;
        public const int NVE_2 = 7;
        public const int NRM_2 = 8;
        public const int S_E_2 = 9;

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

        public static int CalculateTypeMatchup(string attackerType, string targetType)
        {
            ElementTableState table = DataManager.Instance.UniversalEvent.UniversalStates.GetWithDefault<ElementTableState>();
            return table.GetMatchup(attackerType, targetType);
        }

        public static int GetEffectivenessMult(int effectiveness)
        {
            ElementTableState table = DataManager.Instance.UniversalEvent.UniversalStates.GetWithDefault<ElementTableState>();
            return table.Effectiveness[effectiveness];
        }

        public static int GetDualEffectiveness(Character attacker, Character target, string targetElement)
        {
            return (DungeonScene.GetEffectiveness(attacker, target, targetElement, target.Element1) + DungeonScene.GetEffectiveness(attacker, target, targetElement, target.Element2));
        }

        public static int GetDualEffectiveness(Character attacker, Character target, BattleData skill)
        {
            return (DungeonScene.GetEffectiveness(attacker, target, skill, target.Element1) + DungeonScene.GetEffectiveness(attacker, target, skill, target.Element2));
        }


        public override GameEvent Clone() { return new PreTypeEvent(); }

        public override void Apply(GameEventOwner owner, Character ownerChar, string moveType, string targetType, ref int effectiveness)
        {
            effectiveness = CalculateTypeMatchup(moveType, targetType);
        }
    }

    [Serializable]
    public class FamilyMatchupEvent : ElementEffectEvent
    {
        public ElementEffectEvent BaseEvent;

        public FamilyMatchupEvent() { }
        public FamilyMatchupEvent(ElementEffectEvent baseEvent) { BaseEvent = baseEvent; }
        protected FamilyMatchupEvent(FamilyMatchupEvent other)
        {
            BaseEvent = (FamilyMatchupEvent)other.BaseEvent.Clone();
        }
        public override GameEvent Clone() { return new FamilyMatchupEvent(this); }

        public override void Apply(GameEventOwner owner, Character ownerChar, string moveType, string targetType, ref int effectiveness)
        {
            ItemData entry = DataManager.Instance.GetItem(owner.GetID());
            FamilyState family;
            if (!entry.ItemStates.TryGet<FamilyState>(out family))
                return;
            if (family.Members.Contains(ownerChar.BaseForm.Species))
                BaseEvent.Apply(owner, ownerChar, moveType, targetType, ref effectiveness);
        }
    }

    [Serializable]
    public class RemoveTypeMatchupEvent : ElementEffectEvent
    {
        [JsonConverter(typeof(ElementConverter))]
        [DataType(0, DataManager.DataType.Element, false)]
        public string Element;

        public RemoveTypeMatchupEvent() { Element = ""; }
        public RemoveTypeMatchupEvent(string element) { Element = element; }
        protected RemoveTypeMatchupEvent(RemoveTypeMatchupEvent other) { Element = other.Element; }
        public override GameEvent Clone() { return new RemoveTypeMatchupEvent(this); }

        public override void Apply(GameEventOwner owner, Character ownerChar, string moveType, string targetType, ref int effectiveness)
        {
            if (targetType == Element)
                effectiveness = PreTypeEvent.NRM;
        }
    }
    [Serializable]
    public class RemoveTypeWeaknessEvent : ElementEffectEvent
    {
        [JsonConverter(typeof(ElementConverter))]
        [DataType(0, DataManager.DataType.Element, false)]
        public string Element;

        public RemoveTypeWeaknessEvent() { Element = ""; }
        public RemoveTypeWeaknessEvent(string element) { Element = element; }
        protected RemoveTypeWeaknessEvent(RemoveTypeWeaknessEvent other) { Element = other.Element; }
        public override GameEvent Clone() { return new RemoveTypeWeaknessEvent(this); }

        public override void Apply(GameEventOwner owner, Character ownerChar, string moveType, string targetType, ref int effectiveness)
        {
            if (targetType == Element && effectiveness > PreTypeEvent.NRM)
                effectiveness = PreTypeEvent.NRM;
        }
    }
    [Serializable]
    public class NoImmunityEvent : ElementEffectEvent
    {
        public override GameEvent Clone() { return new NoImmunityEvent(); }

        public override void Apply(GameEventOwner owner, Character ownerChar, string moveType, string targetType, ref int effectiveness)
        {
            if (effectiveness == PreTypeEvent.N_E)
                effectiveness = PreTypeEvent.NRM;
        }
    }
    [Serializable]
    public class LessImmunityEvent : ElementEffectEvent
    {
        [JsonConverter(typeof(ElementConverter))]
        [DataType(0, DataManager.DataType.Element, false)]
        public string AttackElement;
        [JsonConverter(typeof(ElementConverter))]
        [DataType(0, DataManager.DataType.Element, false)]
        public string TargetElement;
        public LessImmunityEvent() { AttackElement = ""; TargetElement = ""; }
        public LessImmunityEvent(string attackElement, string targetElement) { AttackElement = attackElement; TargetElement = targetElement; }
        protected LessImmunityEvent(LessImmunityEvent other) { AttackElement = other.AttackElement; TargetElement = other.TargetElement; }
        public override GameEvent Clone() { return new LessImmunityEvent(this); }

        public override void Apply(GameEventOwner owner, Character ownerChar, string moveType, string targetType, ref int effectiveness)
        {
            if (effectiveness == PreTypeEvent.N_E && moveType == AttackElement && targetType == TargetElement)
                effectiveness = PreTypeEvent.NVE;
        }
    }
    [Serializable]
    public class NoResistanceEvent : ElementEffectEvent
    {
        public override GameEvent Clone() { return new NoResistanceEvent(); }

        public override void Apply(GameEventOwner owner, Character ownerChar, string moveType, string targetType, ref int effectiveness)
        {
            if (effectiveness == PreTypeEvent.NVE)
                effectiveness = PreTypeEvent.NRM;
        }
    }
    [Serializable]
    public class TypeImmuneEvent : ElementEffectEvent
    {
        [JsonConverter(typeof(ElementConverter))]
        [DataType(0, DataManager.DataType.Element, false)]
        public string Element;

        public TypeImmuneEvent() { Element = ""; }
        public TypeImmuneEvent(string element) { Element = element; }
        protected TypeImmuneEvent(TypeImmuneEvent other) { Element = other.Element; }
        public override GameEvent Clone() { return new TypeImmuneEvent(this); }
        public override void Apply(GameEventOwner owner, Character ownerChar, string moveType, string targetType, ref int effectiveness)
        {
            if (moveType == Element)
                effectiveness = PreTypeEvent.N_E;
        }
    }
    [Serializable]
    public class TypeVulnerableEvent : ElementEffectEvent
    {
        [JsonConverter(typeof(ElementConverter))]
        [DataType(0, DataManager.DataType.Element, false)]
        public string Element;

        public TypeVulnerableEvent() { Element = ""; }
        public TypeVulnerableEvent(string element) { Element = element; }
        protected TypeVulnerableEvent(TypeVulnerableEvent other) { Element = other.Element; }
        public override GameEvent Clone() { return new TypeVulnerableEvent(this); }
        public override void Apply(GameEventOwner owner, Character ownerChar, string moveType, string targetType, ref int effectiveness)
        {
            if (moveType == Element && effectiveness == 0)
                effectiveness = PreTypeEvent.NRM;
        }
    }
    [Serializable]
    public class ScrappyEvent : ElementEffectEvent
    {
        [JsonConverter(typeof(ElementConverter))]
        [DataType(0, DataManager.DataType.Element, false)]
        public string Element1;
        [JsonConverter(typeof(ElementConverter))]
        [DataType(0, DataManager.DataType.Element, false)]
        public string Element2;

        public ScrappyEvent() { Element1 = ""; Element2 = ""; }
        public ScrappyEvent(string element1, string element2) { Element1 = element1; Element2 = element2; }
        protected ScrappyEvent(ScrappyEvent other) { Element1 = other.Element1; Element2 = other.Element2; }
        public override GameEvent Clone() { return new ScrappyEvent(this); }
        public override void Apply(GameEventOwner owner, Character ownerChar, string moveType, string targetType, ref int effectiveness)
        {
            if ((moveType == Element1 || moveType == Element2) && effectiveness == 0)
                effectiveness = PreTypeEvent.NRM;
        }
    }
    [Serializable]
    public class TypeSuperEvent : ElementEffectEvent
    {
        [JsonConverter(typeof(ElementConverter))]
        [DataType(0, DataManager.DataType.Element, false)]
        public string Element;

        public TypeSuperEvent() { Element = ""; }
        public TypeSuperEvent(string element) { Element = element; }
        protected TypeSuperEvent(TypeSuperEvent other) { Element = other.Element; }
        public override GameEvent Clone() { return new TypeSuperEvent(this); }
        public override void Apply(GameEventOwner owner, Character ownerChar, string moveType, string targetType, ref int effectiveness)
        {
            if (targetType == Element)
                effectiveness = PreTypeEvent.S_E;
        }
    }
    [Serializable]
    public class TypeAddEvent : ElementEffectEvent
    {
        [JsonConverter(typeof(ElementConverter))]
        [DataType(0, DataManager.DataType.Element, false)]
        public string Element;

        public TypeAddEvent() { Element = ""; }
        public TypeAddEvent(string element) { Element = element; }
        protected TypeAddEvent(TypeAddEvent other) { Element = other.Element; }
        public override GameEvent Clone() { return new TypeAddEvent(this); }
        public override void Apply(GameEventOwner owner, Character ownerChar, string moveType, string targetType, ref int effectiveness)
        {
            int secondMatchup = PreTypeEvent.CalculateTypeMatchup(Element, targetType);
            if (secondMatchup == PreTypeEvent.N_E)
                effectiveness = PreTypeEvent.N_E;
            else
            {
                int diff = secondMatchup - PreTypeEvent.NRM;
                effectiveness = Math.Clamp(effectiveness + diff, PreTypeEvent.NVE, PreTypeEvent.S_E);
            }
        }
    }
    [Serializable]
    public class NormalizeEvent : ElementEffectEvent
    {
        public override GameEvent Clone() { return new NormalizeEvent(); }
        public override void Apply(GameEventOwner owner, Character ownerChar, string moveType, string targetType, ref int effectiveness)
        {
            effectiveness = PreTypeEvent.NRM;
        }
    }
    [Serializable]
    public class InverseEvent : ElementEffectEvent
    {
        public override GameEvent Clone() { return new NormalizeEvent(); }
        public override void Apply(GameEventOwner owner, Character ownerChar, string moveType, string targetType, ref int effectiveness)
        {
            if (effectiveness == PreTypeEvent.N_E || effectiveness == PreTypeEvent.NVE)
                effectiveness = PreTypeEvent.S_E;
            else if (effectiveness == PreTypeEvent.S_E)
                effectiveness = PreTypeEvent.NVE;
        }
    }




    [Serializable]
    public abstract class ShareEquipElementEvent : ElementEffectEvent
    {
        public override void Apply(GameEventOwner owner, Character ownerChar, string moveType, string targetType, ref int effectiveness)
        {
            if (!String.IsNullOrEmpty(ownerChar.EquippedItem.ID))
            {
                ItemData entry = (ItemData)ownerChar.EquippedItem.GetData();
                if (CheckEquipPassValidityEvent.CanItemEffectBePassed(entry))
                {
                    foreach (var effect in GetEvents(entry))
                        effect.Value.Apply(owner, ownerChar, moveType, targetType, ref effectiveness);
                }
            }
        }

        protected abstract PriorityList<ElementEffectEvent> GetEvents(ItemData entry);
    }

    [Serializable]
    public class ShareTargetElementEvent : ShareEquipElementEvent
    {
        public override GameEvent Clone() { return new ShareTargetElementEvent(); }

        protected override PriorityList<ElementEffectEvent> GetEvents(ItemData entry) => entry.TargetElementEffects;
    }

    [Serializable]
    public class ShareUserElementEvent : ShareEquipElementEvent
    {
        public override GameEvent Clone() { return new ShareUserElementEvent(); }

        protected override PriorityList<ElementEffectEvent> GetEvents(ItemData entry) => entry.UserElementEffects;
    }
}
