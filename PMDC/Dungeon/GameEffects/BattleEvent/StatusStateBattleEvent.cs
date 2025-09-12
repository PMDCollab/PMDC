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
    // Battle effects that use or modify status state




    /// <summary>
    /// Event that modifies the specified stack boost by adding the value in the StackState status state
    /// </summary>
    [Serializable]
    public class UserStatBoostEvent : BattleEvent
    {
        /// <summary>
        /// The stat to modify
        /// </summary>
        public Stat Stat;

        public UserStatBoostEvent() { }
        public UserStatBoostEvent(Stat stat)
        {
            Stat = stat;
        }
        protected UserStatBoostEvent(UserStatBoostEvent other)
        {
            Stat = other.Stat;
        }
        public override GameEvent Clone() { return new UserStatBoostEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int boost = ((StatusEffect)owner).StatusStates.GetWithDefault<StackState>().Stack;
            switch (Stat)
            {
                case Stat.Attack:
                    context.AddContextStateInt<UserAtkBoost>(boost);
                    break;
                case Stat.Defense:
                    context.AddContextStateInt<UserDefBoost>(boost);
                    break;
                case Stat.MAtk:
                    context.AddContextStateInt<UserSpAtkBoost>(boost);
                    break;
                case Stat.MDef:
                    context.AddContextStateInt<UserSpDefBoost>(boost);
                    break;
                case Stat.HitRate:
                    context.AddContextStateInt<UserAccuracyBoost>(boost);
                    break;
                case Stat.Range:
                    context.RangeMod += boost;
                    break;
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that modifies the specified stack boost by adding the value in the StackState status state
    /// </summary>
    [Serializable]
    public class TargetStatBoostEvent : BattleEvent
    {

        /// <summary>
        /// The stat to modify
        /// </summary>
        public Stat Stat;

        public TargetStatBoostEvent() { }
        public TargetStatBoostEvent(Stat stat)
        {
            Stat = stat;
        }
        protected TargetStatBoostEvent(TargetStatBoostEvent other)
        {
            Stat = other.Stat;
        }
        public override GameEvent Clone() { return new TargetStatBoostEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int boost = ((StatusEffect)owner).StatusStates.GetWithDefault<StackState>().Stack;
            switch (Stat)
            {
                case Stat.Attack:
                    context.AddContextStateInt<TargetAtkBoost>(boost);
                    break;
                case Stat.Defense:
                    context.AddContextStateInt<TargetDefBoost>(boost);
                    break;
                case Stat.MAtk:
                    context.AddContextStateInt<TargetSpAtkBoost>(boost);
                    break;
                case Stat.MDef:
                    context.AddContextStateInt<TargetSpDefBoost>(boost);
                    break;
                case Stat.DodgeRate:
                    context.AddContextStateInt<TargetEvasionBoost>(boost);
                    break;
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that modifies the specified stack boost
    /// </summary>
    [Serializable]
    public class TargetStatAddEvent : BattleEvent
    {

        /// <summary>
        /// The stat to modify
        /// </summary>
        public Stat Stat;

        /// <summary>
        /// The value to modify the stat by
        /// </summary>
        public int Mod;

        public TargetStatAddEvent() { }
        public TargetStatAddEvent(Stat stat, int mod)
        {
            Stat = stat;
            Mod = mod;
        }
        protected TargetStatAddEvent(TargetStatAddEvent other)
        {
            Stat = other.Stat;
            Mod = other.Mod;
        }
        public override GameEvent Clone() { return new TargetStatAddEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            switch (Stat)
            {
                case Stat.Attack:
                    context.AddContextStateInt<TargetAtkBoost>(Mod);
                    break;
                case Stat.Defense:
                    context.AddContextStateInt<TargetDefBoost>(Mod);
                    break;
                case Stat.MAtk:
                    context.AddContextStateInt<TargetSpAtkBoost>(Mod);
                    break;
                case Stat.MDef:
                    context.AddContextStateInt<TargetSpDefBoost>(Mod);
                    break;
                case Stat.DodgeRate:
                    context.AddContextStateInt<TargetEvasionBoost>(Mod);
                    break;
            }
            yield break;
        }
    }



    /// <summary>
    /// Event that sets the AttackedThisTurnState status state to be true, indicating that the character attacked this turn
    /// This event can only be used on statuses
    /// </summary>
    [Serializable]
    public class AttackedThisTurnEvent : BattleEvent
    {
        public override GameEvent Clone() { return new AttackedThisTurnEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            AttackedThisTurnState recent = ((StatusEffect)owner).StatusStates.GetWithDefault<AttackedThisTurnState>();
            recent.Attacked = true;
            yield break;
        }
    }




    /// <summary>
    /// Event that reverses the character's stat changes
    /// </summary>
    [Serializable]
    public class ReverseStateStatusBattleEvent : BattleEvent
    {
        /// <summary>
        /// If the status contains the one of the specified status states, then it's stack amount can be reverted
        /// </summary>
        [StringTypeConstraint(1, typeof(StatusState))]
        public List<FlagType> States;

        /// <summary>
        /// Whether to affect the target or user
        /// </summary>
        public bool AffectTarget;

        /// <summary>
        /// The message displayed in the dungeon log 
        /// </summary>
        [StringKey(0, true)]
        public StringKey Msg;

        public ReverseStateStatusBattleEvent() { States = new List<FlagType>(); }
        public ReverseStateStatusBattleEvent(Type state, bool affectTarget, StringKey msg) : this()
        {
            States.Add(new FlagType(state));
            AffectTarget = affectTarget;
            Msg = msg;
        }
        protected ReverseStateStatusBattleEvent(ReverseStateStatusBattleEvent other) : this()
        {
            States.AddRange(other.States);
            AffectTarget = other.AffectTarget;
            Msg = other.Msg;
        }
        public override GameEvent Clone() { return new ReverseStateStatusBattleEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = (AffectTarget ? context.Target : context.User);
            if (target.Dead)
                yield break;

            bool affected = false;
            foreach (StatusEffect status in target.IterateStatusEffects())
            {
                bool hasState = false;
                foreach (FlagType state in States)
                {
                    if (status.StatusStates.Contains(state.FullType))
                        hasState = true;
                }
                if (hasState)
                {
                    StackState stack = status.StatusStates.GetWithDefault<StackState>();
                    stack.Stack = stack.Stack * -1;
                    affected = true;
                }
            }
            if (affected && Msg.IsValid())
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(Msg.ToLocal(), target.GetDisplayName(false)));
        }

    }

    /// <summary>
    /// Event that decreases the counter in the status's CountDownState when the character does an action
    /// The status is removed when the countdown reaches 0
    /// This event can only be used on statuses
    /// </summary>
    [Serializable]
    public class CountDownOnActionEvent : BattleEvent
    {
        /// <summary>
        /// Whether to display the message when the status is removed 
        /// </summary>
        public bool ShowMessage;

        public CountDownOnActionEvent() { }
        public CountDownOnActionEvent(bool showMessage)
        {
            ShowMessage = showMessage;
        }
        protected CountDownOnActionEvent(CountDownOnActionEvent other)
        {
            ShowMessage = other.ShowMessage;
        }
        public override GameEvent Clone() { return new CountDownOnActionEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.UsageSlot == BattleContext.FORCED_SLOT)
                yield break;

            ((StatusEffect)owner).StatusStates.GetWithDefault<CountDownState>().Counter--;
            if (((StatusEffect)owner).StatusStates.GetWithDefault<CountDownState>().Counter <= 0)
                yield return CoroutineManager.Instance.StartCoroutine(context.User.RemoveStatusEffect(((StatusEffect)owner).ID, ShowMessage));
        }
    }

    /// <summary>
    /// Event that removes the RecentState status state
    /// This event can only be used on statuses 
    /// </summary> 
    [Serializable]
    public class RemoveRecentEvent : BattleEvent
    {
        public RemoveRecentEvent() { }
        public override GameEvent Clone() { return new RemoveRecentEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            ((StatusEffect)owner).StatusStates.Remove<RecentState>();//allow the counter to count down
            yield break;
        }
    }

    /// <summary>
    /// Event that sets the CountDownState counter to 0 if the character receives any damage
    /// </summary> 
    [Serializable]
    public class ForceWakeEvent : BattleEvent
    {
        public ForceWakeEvent() { }
        public override GameEvent Clone() { return new ForceWakeEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int damage = context.GetContextStateInt<DamageDealt>(0);
            bool hit = context.ContextStates.Contains<AttackHit>();
            bool recent = ((StatusEffect)owner).StatusStates.Contains<RecentState>();
            if (!recent && context.Target != context.User)//don't immediately count down after status is inflicted
            {
                if (damage > 0)
                {
                    //yield return CoroutineManager.Instance.StartCoroutine(context.Target.RemoveStatusEffect(((StatusEffect)owner).ID, true));
                    ((StatusEffect)owner).StatusStates.GetWithDefault<CountDownState>().Counter = 0;
                }
                else if (hit)
                    ((StatusEffect)owner).StatusStates.GetWithDefault<CountDownState>().Counter = Math.Max(((StatusEffect)owner).StatusStates.GetWithDefault<CountDownState>().Counter - 1, 0);
            }
            yield break;
        }
    }
}

