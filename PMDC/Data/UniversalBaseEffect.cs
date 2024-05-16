using System;
using RogueEssence.Data;
using RogueEssence.Dungeon;
using PMDC.Dungeon;

namespace PMDC.Data
{
    public class UniversalActiveEffect : UniversalBaseEffect
    { 
        public UniversalActiveEffect() { }
        
        private T getConditionalEvent<T>(Character controlledChar, PassiveContext passiveContext, BattleEvent effect) where T : BattleEvent
        {
            if (effect is T)
                return (T)effect;

            //TODO: add other conditions  
            FamilyBattleEvent familyEffect = effect as FamilyBattleEvent;
            if (familyEffect != null)
            {
                ItemData entry = (ItemData)passiveContext.Passive.GetData();
                FamilyState family;
                if (!entry.ItemStates.TryGet<FamilyState>(out family))
                    return null;
                if (family.Members.Contains(controlledChar.BaseForm.Species))
                    return getConditionalEvent<T>(controlledChar, passiveContext, familyEffect.BaseEvent);
            }

            return null;
        }
        
        public override int GetRange(Character character, ref SkillData entry)
        {
            int rangeMod = 0;
            
            //check for passives that modify range; NOTE: specialized AI code!
            foreach (PassiveContext passive in character.IteratePassives(GameEventPriority.USER_PORT_PRIORITY))
            {
                foreach (BattleEvent effect in passive.EventData.OnActions.EnumerateInOrder())
                {
                    AddRangeEvent addRangeEvent = getConditionalEvent<AddRangeEvent>(character, passive, effect);
                    if (addRangeEvent != null)
                    {
                        rangeMod += addRangeEvent.Range;
                        continue;
                    }

                    CategoryAddRangeEvent categoryRangeEvent = getConditionalEvent<CategoryAddRangeEvent>(character, passive, effect);
                    if (categoryRangeEvent != null)
                    {
                        if (entry.Data.Category == categoryRangeEvent.Category)
                            rangeMod += categoryRangeEvent.Range;
                        continue;
                    }

                    WeatherAddRangeEvent weatherRangeEvent = getConditionalEvent<WeatherAddRangeEvent>(character, passive, effect);
                    if (weatherRangeEvent != null)
                    {
                        if (ZoneManager.Instance.CurrentMap.Status.ContainsKey(weatherRangeEvent.WeatherID))
                            rangeMod += weatherRangeEvent.Range;
                        continue;
                    }
                    
                    ElementAddRangeEvent elementRangeEvent = getConditionalEvent<ElementAddRangeEvent>(character, passive, effect);
                    if (elementRangeEvent != null)
                    {
                        if (elementRangeEvent.Elements.Contains(character.Element1) || elementRangeEvent.Elements.Contains(character.Element2))
                        {
                            rangeMod += elementRangeEvent.Range;
                        }
                        continue;
                    }
                }
            }

            rangeMod = Math.Min(Math.Max(-3, rangeMod), 3);
            return rangeMod;
        }
    }
}
