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
    // Battle events that relate to additional chance effects on attacking moves, and crits


    /// <summary>
    /// Event that boosts the rate in the AdditionalEffectState skill state
    /// </summary>
    [Serializable]
    public class BoostAdditionalEvent : BattleEvent
    {
        public BoostAdditionalEvent() { }
        public override GameEvent Clone() { return new BoostAdditionalEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            AdditionalEffectState state = ((BattleData)context.Data).SkillStates.GetWithDefault<AdditionalEffectState>();
            if (state != null)
                state.EffectChance *= 2;
            yield break;
        }
    }

    /// <summary>
    /// Event that sets the rate in the AdditionalEffectState skill state to 0
    /// </summary>
    [Serializable]
    public class BlockAdditionalEvent : BattleEvent
    {
        public override GameEvent Clone() { return new BlockAdditionalEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            AdditionalEffectState state = ((BattleData)context.Data).SkillStates.GetWithDefault<AdditionalEffectState>();
            if (state != null)
                state.EffectChance = 0;
            yield break;
        }
    }



    /// <summary>
    /// Event that sets the rate in the AdditionalEffectState skill state to 0 and boosts the damage multiplier
    /// </summary>
    [Serializable]
    public class SheerForceEvent : BattleEvent
    {
        public override GameEvent Clone() { return new BlockAdditionalEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            AdditionalEffectState state = ((BattleData)context.Data).SkillStates.GetWithDefault<AdditionalEffectState>();
            if (state != null)
            {
                state.EffectChance = 0;
                context.AddContextStateMult<DmgMult>(false, 4, 3);
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that modifies the critical change rate based on how effective the move is
    /// </summary>
    [Serializable]
    public class CritEffectiveEvent : BattleEvent
    {

        /// <summary>
        /// Whether to check if the move is not effective instead
        /// </summary>
        public bool Reverse;

        /// <summary>
        /// The added critical rate chance
        /// </summary>
        public int AddCrit;

        public CritEffectiveEvent() { }
        public CritEffectiveEvent(bool reverse, int addCrit)
        {
            Reverse = reverse;
            AddCrit = addCrit;
        }
        protected CritEffectiveEvent(CritEffectiveEvent other)
        {
            Reverse = other.Reverse;
            AddCrit = other.AddCrit;
        }
        public override GameEvent Clone() { return new CritEffectiveEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int typeMatchup = PreTypeEvent.GetDualEffectiveness(context.User, context.Target, context.Data);
            typeMatchup -= PreTypeEvent.NRM_2;
            if (Reverse)
                typeMatchup *= -1;
            if (typeMatchup > 0)
                context.AddContextStateInt<CritLevel>(AddCrit);

            yield break;
        }
    }


    /// <summary>
    /// Event that boosts the critical chance rate
    /// </summary>
    [Serializable]
    public class BoostCriticalEvent : BattleEvent
    {

        /// <summary>
        /// The modified critical chance rate
        /// 1 - 25%
        /// 2 - 50%
        /// 3 - 75%
        /// 4 - 100%
        /// </summary>
        public int AddCrit;

        public BoostCriticalEvent() { }
        public BoostCriticalEvent(int addCrit)
        {
            AddCrit = addCrit;
        }
        protected BoostCriticalEvent(BoostCriticalEvent other)
        {
            AddCrit = other.AddCrit;
        }
        public override GameEvent Clone() { return new BoostCriticalEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            context.AddContextStateInt<CritLevel>(AddCrit);
            yield break;
        }
    }

    /// <summary>
    /// Event that sets the critical rate chance to 0
    /// </summary>
    [Serializable]
    public class BlockCriticalEvent : BattleEvent
    {
        public override GameEvent Clone() { return new BlockCriticalEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            CritLevel critLevel = context.ContextStates.GetWithDefault<CritLevel>();
            if (critLevel != null)
                critLevel.Count = 0;
            yield break;
        }
    }
}

