using System;
using System.Collections.Generic;
using RogueEssence.Data;
using RogueEssence.Menu;
using RogueElements;
using RogueEssence.Content;
using RogueEssence.LevelGen;
using RogueEssence;
using RogueEssence.Dungeon;
using RogueEssence.Dev;
using PMDC.Dev;
using PMDC.Data;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using NLua;
using RogueEssence.Script;
using System.Linq;

namespace PMDC.Dungeon
{
    // Battle events that relate to enduring attacks



    /// <summary>
    /// Event that sets the AttackEndure context state if the character is at full HP
    /// </summary>
    [Serializable]
    public class FullEndureEvent : BattleEvent
    {
        public override GameEvent Clone() { return new FullEndureEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Target.HP == context.Target.MaxHP)
                context.ContextStates.Set(new AttackEndure());
            yield break;
        }
    }

    /// <summary>
    /// Event that sets the AttackEndure context state if the character is hit by the specified skill category
    /// </summary>
    [Serializable]
    public class EndureCategoryEvent : BattleEvent
    {
        /// <summary>
        /// The affected skill category
        /// </summary>
        public BattleData.SkillCategory Category;

        public EndureCategoryEvent() { }
        public EndureCategoryEvent(BattleData.SkillCategory category)
        {
            Category = category;
        }
        protected EndureCategoryEvent(EndureCategoryEvent other)
        {
            Category = other.Category;
        }
        public override GameEvent Clone() { return new EndureCategoryEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Data.Category == Category || Category == BattleData.SkillCategory.None)
                context.ContextStates.Set(new AttackEndure());
            yield break;
        }
    }

    /// <summary>
    /// Event that sets the AttackEndure context state if the character is hit by the specified move type
    /// </summary>
    [Serializable]
    public class EndureElementEvent : BattleEvent
    {
        /// <summary>
        /// The affected move type
        /// </summary>
        [JsonConverter(typeof(ElementConverter))]
        [DataType(0, DataManager.DataType.Element, false)]
        public string Element;

        public EndureElementEvent() { Element = ""; }
        public EndureElementEvent(string element)
        {
            Element = element;
        }
        protected EndureElementEvent(EndureElementEvent other)
        {
            Element = other.Element;
        }
        public override GameEvent Clone() { return new EndureElementEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Data.Element == Element)
                context.ContextStates.Set(new AttackEndure());
            yield break;
        }
    }
}

