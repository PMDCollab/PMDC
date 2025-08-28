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
    //Battle events that trigger child battle events under some condition

    /// <summary>
    /// Event that applies if the target is not immune to the specified type
    /// </summary>
    [Serializable]
    public class CheckImmunityBattleEvent : BattleEvent
    {
        /// <summary>
        /// The type to check immunity from
        /// </summary>
        [JsonConverter(typeof(ElementConverter))]
        [DataType(0, DataManager.DataType.Element, false)]
        public string Element;

        /// <summary>
        /// Whether to affect the target or user
        /// </summary>
        public bool AffectTarget;

        /// <summary>
        /// The list of battle events applied if the condition is met
        /// </summary>
        public List<BattleEvent> BaseEvents;

        public CheckImmunityBattleEvent() { BaseEvents = new List<BattleEvent>(); Element = ""; }
        public CheckImmunityBattleEvent(string element, bool affectTarget, params BattleEvent[] effects)
        {
            Element = element;
            AffectTarget = affectTarget;
            BaseEvents = new List<BattleEvent>();
            foreach (BattleEvent effect in effects)
                BaseEvents.Add(effect);
        }
        protected CheckImmunityBattleEvent(CheckImmunityBattleEvent other)
        {
            Element = other.Element;
            AffectTarget = other.AffectTarget;
            foreach (BattleEvent battleEffect in other.BaseEvents)
                BaseEvents.Add((BattleEvent)battleEffect.Clone());
        }
        public override GameEvent Clone() { return new CheckImmunityBattleEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = (AffectTarget ? context.Target : context.User);
            if (target.Dead)
                yield break;

            int typeMatchup = PreTypeEvent.GetDualEffectiveness(null, target, Element);
            if (typeMatchup > PreTypeEvent.N_E_2)
            {
                foreach (BattleEvent battleEffect in BaseEvents)
                    yield return CoroutineManager.Instance.StartCoroutine(battleEffect.Apply(owner, ownerChar, context));
            }
        }
    }


    /// <summary>
    /// Event that activates if the battle actions contains one of the specified ContextState 
    /// </summary>
    [Serializable]
    public class ExceptionContextEvent : BattleEvent
    {
        /// <summary>
        /// The list of valid ContextState types
        /// </summary>
        [StringTypeConstraint(1, typeof(ContextState))]
        public List<FlagType> States;

        /// <summary>
        /// Whether to to check in the global context states
        /// </summary>
        public bool Global;

        /// <summary>
        /// Battle event that applies if the condition is met
        /// </summary>
        public BattleEvent BaseEvent;

        public ExceptionContextEvent() { States = new List<FlagType>(); }
        public ExceptionContextEvent(Type state, bool global, BattleEvent baseEffect) : this() { States.Add(new FlagType(state)); Global = global; BaseEvent = baseEffect; }
        protected ExceptionContextEvent(ExceptionContextEvent other) : this()
        {
            States.AddRange(other.States);
            Global = other.Global;
            BaseEvent = (BattleEvent)other.BaseEvent.Clone();
        }
        public override GameEvent Clone() { return new ExceptionContextEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            bool hasState = false;
            foreach (FlagType state in States)
            {
                if (Global ? context.GlobalContextStates.Contains(state.FullType) : context.ContextStates.Contains(state.FullType))
                    hasState = true;
            }
            if (!hasState)
                yield return CoroutineManager.Instance.StartCoroutine(BaseEvent.Apply(owner, ownerChar, context));
        }

    }


    /// <summary>
    /// Event that activates if the the user doesn't have the Infiltrator context state
    /// </summary>
    [Serializable]
    public class ExceptInfiltratorEvent : BattleEvent
    {

        /// <summary>
        /// Whether to log the Infiltrator pass through message
        /// </summary>
        public bool ExceptionMsg;

        /// <summary>
        /// The list of battle events applied if the condition is met
        /// </summary>
        public List<BattleEvent> BaseEvents;

        public ExceptInfiltratorEvent() { BaseEvents = new List<BattleEvent>(); }
        public ExceptInfiltratorEvent(bool msg, params BattleEvent[] effects)
        {
            ExceptionMsg = msg;
            BaseEvents = new List<BattleEvent>();
            foreach (BattleEvent effect in effects)
                BaseEvents.Add(effect);
        }
        protected ExceptInfiltratorEvent(ExceptInfiltratorEvent other)
        {
            ExceptionMsg = other.ExceptionMsg;
            foreach (BattleEvent battleEffect in other.BaseEvents)
                BaseEvents.Add((BattleEvent)battleEffect.Clone());
        }
        public override GameEvent Clone() { return new ExceptInfiltratorEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Infiltrator state = context.ContextStates.GetWithDefault<Infiltrator>();
            if (state == null)
            {
                foreach (BattleEvent battleEffect in BaseEvents)
                    yield return CoroutineManager.Instance.StartCoroutine(battleEffect.Apply(owner, ownerChar, context));
            }
            else if (ExceptionMsg)
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(state.Msg.ToLocal(), context.User.GetDisplayName(false), owner.GetDisplayName()));
        }
    }

    /// <summary>
    /// Event that plays if the battle actions contains one of the specified CharState 
    /// </summary>
    [Serializable]
    public class ExceptionCharStateEvent : BattleEvent
    {

        /// <summary>
        /// The list of valid CharState types
        /// </summary>
        [StringTypeConstraint(1, typeof(CharState))]
        public List<FlagType> States;

        /// <summary>
        /// Whether to affect the target or user
        /// </summary>
        public bool CheckTarget;

        /// <summary>
        /// Battle event that applies if the condition is met
        /// </summary>
        public BattleEvent BaseEvent;

        public ExceptionCharStateEvent() { States = new List<FlagType>(); }
        public ExceptionCharStateEvent(Type state, bool checkTarget, BattleEvent baseEffect) : this() { States.Add(new FlagType(state)); CheckTarget = checkTarget; BaseEvent = baseEffect; }
        protected ExceptionCharStateEvent(ExceptionCharStateEvent other) : this()
        {
            States.AddRange(other.States);
            CheckTarget = other.CheckTarget;
            BaseEvent = (BattleEvent)other.BaseEvent.Clone();
        }
        public override GameEvent Clone() { return new ExceptionCharStateEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = (CheckTarget ? context.Target : context.User);

            bool hasState = false;
            foreach (FlagType state in States)
            {
                if (target.CharStates.Contains(state.FullType))
                    hasState = true;
            }
            if (!hasState)
                yield return CoroutineManager.Instance.StartCoroutine(BaseEvent.Apply(owner, ownerChar, context));
        }

    }

    /// <summary>
    /// Event that applies if the move contains one of the specified SkillStates
    /// </summary>
    [Serializable]
    public class MoveStateNeededEvent : BattleEvent
    {

        /// <summary>
        /// The list of valid SkillStates types
        /// </summary>
        [StringTypeConstraint(1, typeof(SkillState))]
        public List<FlagType> States;

        /// <summary>
        /// The list of battle events applied if the condition is met
        /// </summary>
        public List<BattleEvent> BaseEvents;

        public MoveStateNeededEvent() { States = new List<FlagType>(); BaseEvents = new List<BattleEvent>(); }
        public MoveStateNeededEvent(Type state, params BattleEvent[] effects) : this()
        {
            States.Add(new FlagType(state));
            foreach (BattleEvent effect in effects)
                BaseEvents.Add(effect);
        }
        protected MoveStateNeededEvent(MoveStateNeededEvent other) : this()
        {
            States.AddRange(other.States);
            foreach (BattleEvent battleEffect in other.BaseEvents)
                BaseEvents.Add((BattleEvent)battleEffect.Clone());
        }
        public override GameEvent Clone() { return new MoveStateNeededEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            bool hasState = false;
            foreach (FlagType state in States)
            {
                if (context.Data.SkillStates.Contains(state.FullType))
                    hasState = true;
            }
            if (hasState)
            {
                foreach (BattleEvent battleEffect in BaseEvents)
                    yield return CoroutineManager.Instance.StartCoroutine(battleEffect.Apply(owner, ownerChar, context));
            }
            yield break;
        }
    }


    /// <summary>
    /// Item event that runs if the character is part of the evolution in FamilyState (ItemStates)
    /// </summary>
    [Serializable]
    public class FamilyBattleEvent : BattleEvent
    {

        /// <summary>
        /// Battle event that applies if the condition is met
        /// </summary>
        public BattleEvent BaseEvent;

        public FamilyBattleEvent()
        { }
        public FamilyBattleEvent(BattleEvent baseEvent)
        {
            BaseEvent = baseEvent;
        }
        protected FamilyBattleEvent(FamilyBattleEvent other)
        {
            BaseEvent = (BattleEvent)other.BaseEvent.Clone();
        }
        public override GameEvent Clone() { return new FamilyBattleEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            ItemData entry = DataManager.Instance.GetItem(owner.GetID());
            FamilyState family;
            if (!entry.ItemStates.TryGet<FamilyState>(out family))
                yield break;

            if (family.Members.Contains(ownerChar.BaseForm.Species))
                yield return CoroutineManager.Instance.StartCoroutine(BaseEvent.Apply(owner, ownerChar, context));
        }
    }


    /// <summary>
    /// Event that activates if the user's HP is below threshold
    /// </summary>
    [Serializable]
    public class PinchNeededEvent : BattleEvent
    {
        /// <summary>
        /// The denominator of the HP percentage 
        /// </summary>
        public int Denominator;

        /// <summary>
        /// Whether to affect the user or target
        /// </summary>
        public bool AffectTarget;

        /// <summary>
        /// The list of battle events applied if the condition is met
        /// </summary>
        public List<BattleEvent> BaseEvents;

        public PinchNeededEvent() { BaseEvents = new List<BattleEvent>(); }
        public PinchNeededEvent(int denominator, params BattleEvent[] effects) : this()
        {
            Denominator = denominator;
            foreach (BattleEvent effect in effects)
                BaseEvents.Add(effect);
        }
        protected PinchNeededEvent(PinchNeededEvent other) : this()
        {
            Denominator = other.Denominator;
            AffectTarget = other.AffectTarget;
            foreach (BattleEvent battleEffect in other.BaseEvents)
                BaseEvents.Add((BattleEvent)battleEffect.Clone());
        }
        public override GameEvent Clone() { return new PinchNeededEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = (AffectTarget ? context.Target : context.User);

            if (target.HP <= target.MaxHP / Math.Max(1, Denominator))
            {
                foreach (BattleEvent battleEffect in BaseEvents)
                    yield return CoroutineManager.Instance.StartCoroutine(battleEffect.Apply(owner, ownerChar, context));
            }

            yield break;
        }
    }

    /// <summary>
    /// Event that applies a battle event if the status does not contain one of the specified status states 
    /// </summary>
    [Serializable]
    public class ExceptionStatusEvent : BattleEvent
    {
        /// <summary>
        /// The list of status states to check for
        /// </summary>
        [StringTypeConstraint(1, typeof(StatusState))]
        public List<FlagType> States;

        /// <summary>
        /// Battle event that applies if the condition is met
        /// </summary>
        public BattleEvent BaseEvent;

        public ExceptionStatusEvent() { States = new List<FlagType>(); }
        public ExceptionStatusEvent(Type state, BattleEvent baseEffect) : this() { States.Add(new FlagType(state)); BaseEvent = baseEffect; }
        protected ExceptionStatusEvent(ExceptionStatusEvent other) : this()
        {
            States.AddRange(other.States);
            BaseEvent = (BattleEvent)other.BaseEvent.Clone();
        }
        public override GameEvent Clone() { return new ExceptionStatusEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            bool hasState = false;
            foreach (FlagType state in States)
            {
                if (((StatusEffect)owner).StatusStates.Contains(state.FullType))
                    hasState = true;
            }
            if (!hasState)
                yield return CoroutineManager.Instance.StartCoroutine(BaseEvent.Apply(owner, ownerChar, context));
        }

    }


    /// <summary>
    /// Event that activiates if the character has one of the specified statuses
    /// </summary>
    [Serializable]
    public class HasStatusNeededEvent : BattleEvent
    {

        /// <summary>
        /// The list of valid status IDs 
        /// </summary>
        [JsonConverter(typeof(StatusListConverter))]
        [DataType(1, DataManager.DataType.Status, false)]
        public List<string> Statuses;

        /// <summary>
        /// Whether to check the target or user
        /// </summary>
        public bool AffectTarget;

        /// <summary>
        /// The list of battle events that plays if the condition is met
        /// </summary>
        public List<BattleEvent> BaseEvents;

        public HasStatusNeededEvent() { Statuses = new List<string>(); BaseEvents = new List<BattleEvent>(); }
        public HasStatusNeededEvent(bool affectTarget, string[] statuses, params BattleEvent[] effects) : this()
        {
            AffectTarget = affectTarget;
            foreach (string statusId in statuses)
                Statuses.Add(statusId);
            foreach (BattleEvent effect in effects)
                BaseEvents.Add(effect);
        }
        protected HasStatusNeededEvent(HasStatusNeededEvent other) : this()
        {
            AffectTarget = other.AffectTarget;
            foreach (string statusId in other.Statuses)
                Statuses.Add(statusId);
            foreach (BattleEvent battleEffect in other.BaseEvents)
                BaseEvents.Add((BattleEvent)battleEffect.Clone());
        }
        public override GameEvent Clone() { return new HasStatusNeededEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = (AffectTarget ? context.Target : context.User);

            bool hasStatus = false;
            foreach (StatusEffect status in target.IterateStatusEffects())
            {
                if (Statuses.Contains(status.ID))
                {
                    hasStatus = true;
                    break;
                }
            }

            if (hasStatus)
            {
                foreach (BattleEvent battleEffect in BaseEvents)
                    yield return CoroutineManager.Instance.StartCoroutine(battleEffect.Apply(owner, ownerChar, context));
            }

            yield break;
        }
    }

    /// <summary>
    /// Event that groups multiple battle events into one event, but only applies if damage was dealt
    /// and passes the AdditionalEffectState chance check
    /// This event should be placed in OnHits
    /// </summary>
    [Serializable]
    public class AdditionalEvent : BattleEvent
    {

        /// <summary>
        /// The list of battle events that will be applied if the condition is met
        /// </summary>
        public List<BattleEvent> BaseEvents;

        public AdditionalEvent() { BaseEvents = new List<BattleEvent>(); }
        public AdditionalEvent(params BattleEvent[] effects)
        {
            BaseEvents = new List<BattleEvent>();
            foreach (BattleEvent effect in effects)
                BaseEvents.Add(effect);
        }
        protected AdditionalEvent(AdditionalEvent other) : this()
        {
            foreach (BattleEvent battleEffect in other.BaseEvents)
                BaseEvents.Add((BattleEvent)battleEffect.Clone());
        }
        public override GameEvent Clone() { return new AdditionalEvent(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {

            if (context.GetContextStateInt<DamageDealt>(0) > 0)
            {
                if (DataManager.Instance.Save.Rand.Next(100) < context.Data.SkillStates.GetWithDefault<AdditionalEffectState>().EffectChance)
                    yield return CoroutineManager.Instance.StartCoroutine(BaseEvents[DataManager.Instance.Save.Rand.Next(BaseEvents.Count)].Apply(owner, ownerChar, context));
            }
        }
    }

    /// <summary>
    /// Event that groups multiple battle events into one event, but only applies if damage was dealt
    /// and passes the AdditionalEffectState chance check
    /// This event should be placed in AfterActions
    /// </summary>
    [Serializable]
    public class AdditionalEndEvent : BattleEvent
    {
        /// <summary>
        /// The list of battle events that will be applie if the condition is metd
        /// </summary>
        public List<BattleEvent> BaseEvents;

        public AdditionalEndEvent() { BaseEvents = new List<BattleEvent>(); }
        public AdditionalEndEvent(params BattleEvent[] effects)
        {
            BaseEvents = new List<BattleEvent>();
            foreach (BattleEvent effect in effects)
                BaseEvents.Add(effect);
        }
        protected AdditionalEndEvent(AdditionalEndEvent other)
            : this()
        {
            foreach (BattleEvent battleEffect in other.BaseEvents)
                BaseEvents.Add((BattleEvent)battleEffect.Clone());
        }
        public override GameEvent Clone() { return new AdditionalEndEvent(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {

            if (context.GetContextStateInt<TotalDamageDealt>(true, 0) > 0)
            {
                if (DataManager.Instance.Save.Rand.Next(100) < context.Data.SkillStates.GetWithDefault<AdditionalEffectState>().EffectChance)
                    yield return CoroutineManager.Instance.StartCoroutine(BaseEvents[DataManager.Instance.Save.Rand.Next(BaseEvents.Count)].Apply(owner, ownerChar, context));
            }
        }
    }

    /// <summary>
    /// Event that groups multiple battle events into one event, but only applies if the target dead 
    /// </summary>
    [Serializable]
    public class TargetDeadNeededEvent : BattleEvent
    {

        /// <summary>
        /// The list of battle events that will be applie if the condition is metd
        /// </summary>
        public List<BattleEvent> BaseEvents;

        public TargetDeadNeededEvent() { BaseEvents = new List<BattleEvent>(); }
        public TargetDeadNeededEvent(params BattleEvent[] effects)
        {
            BaseEvents = new List<BattleEvent>();
            foreach (BattleEvent effect in effects)
                BaseEvents.Add(effect);
        }
        protected TargetDeadNeededEvent(TargetDeadNeededEvent other)
            : this()
        {
            foreach (BattleEvent battleEffect in other.BaseEvents)
                BaseEvents.Add((BattleEvent)battleEffect.Clone());
        }
        public override GameEvent Clone() { return new TargetDeadNeededEvent(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Target.Dead)
            {
                foreach (BattleEvent battleEffect in BaseEvents)
                    yield return CoroutineManager.Instance.StartCoroutine(battleEffect.Apply(owner, ownerChar, context));
            }
        }
    }

    /// <summary>
    /// Event that groups multiple battle events into one event, but only applies if the user knocks out the target 
    /// </summary>
    [Serializable]
    public class KnockOutNeededEvent : BattleEvent
    {
        /// <summary>
        /// The list of battle events that will be applie if the condition is metd
        /// </summary>
        public List<BattleEvent> BaseEvents;

        public KnockOutNeededEvent() { BaseEvents = new List<BattleEvent>(); }
        public KnockOutNeededEvent(params BattleEvent[] effects)
        {
            BaseEvents = new List<BattleEvent>();
            foreach (BattleEvent effect in effects)
                BaseEvents.Add(effect);
        }
        protected KnockOutNeededEvent(KnockOutNeededEvent other)
            : this()
        {
            foreach (BattleEvent battleEffect in other.BaseEvents)
                BaseEvents.Add((BattleEvent)battleEffect.Clone());
        }
        public override GameEvent Clone() { return new KnockOutNeededEvent(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int knockOuts = context.GetContextStateInt<TotalKnockouts>(true, 0);
            for (int ii = 0; ii < knockOuts; ii++)
            {
                foreach (BattleEvent battleEffect in BaseEvents)
                    yield return CoroutineManager.Instance.StartCoroutine(battleEffect.Apply(owner, ownerChar, context));
            }
        }
    }

    /// <summary>
    /// Event that groups multiple battle events into one event, but only applies
    /// if the character uses an item with the EdibleState item state 
    /// </summary>
    [Serializable]
    public class FoodNeededEvent : BattleEvent
    {
        public List<BattleEvent> BaseEvents;

        public FoodNeededEvent() { BaseEvents = new List<BattleEvent>(); }
        public FoodNeededEvent(params BattleEvent[] effects)
        {
            BaseEvents = new List<BattleEvent>();
            foreach (BattleEvent effect in effects)
                BaseEvents.Add(effect);
        }
        protected FoodNeededEvent(FoodNeededEvent other)
            : this()
        {
            foreach (BattleEvent battleEffect in other.BaseEvents)
                BaseEvents.Add((BattleEvent)battleEffect.Clone());
        }
        public override GameEvent Clone() { return new FoodNeededEvent(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Item || context.ActionType == BattleActionType.Throw)
            {
                ItemData itemData = DataManager.Instance.GetItem(context.Item.ID);
                if (itemData.ItemStates.Contains<EdibleState>())
                {
                    foreach (BattleEvent battleEffect in BaseEvents)
                        yield return CoroutineManager.Instance.StartCoroutine(battleEffect.Apply(owner, ownerChar, context));
                }
            }
        }
    }



    /// <summary>
    /// Event that groups multiple battle events into one event, but only applies if the specified map status is present 
    /// </summary>
    [Serializable]
    public class WeatherNeededEvent : BattleEvent
    {
        /// <summary>
        /// The list of battle events that will be applied if the condition is met 
        /// </summary>
        [JsonConverter(typeof(MapStatusConverter))]
        [DataType(0, DataManager.DataType.MapStatus, false)]
        public string WeatherID;
        public List<BattleEvent> BaseEvents;

        public WeatherNeededEvent() { BaseEvents = new List<BattleEvent>(); WeatherID = ""; }
        public WeatherNeededEvent(string id, params BattleEvent[] effects)
            : this()
        {
            WeatherID = id;
            foreach (BattleEvent effect in effects)
                BaseEvents.Add(effect);
        }
        protected WeatherNeededEvent(WeatherNeededEvent other) : this()
        {
            WeatherID = other.WeatherID;
            foreach (BattleEvent battleEffect in other.BaseEvents)
                BaseEvents.Add((BattleEvent)battleEffect.Clone());
        }
        public override GameEvent Clone() { return new WeatherNeededEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (ZoneManager.Instance.CurrentMap.Status.ContainsKey(WeatherID))
            {
                foreach (BattleEvent battleEffect in BaseEvents)
                    yield return CoroutineManager.Instance.StartCoroutine(battleEffect.Apply(owner, ownerChar, context));
            }
        }
    }

    /// <summary>
    /// Event that groups multiple battle events into one event, but only applies if a critical hit was landed 
    /// </summary>
    [Serializable]
    public class CritNeededEvent : BattleEvent
    {

        /// <summary>
        /// The list of battle events that will be applied if the condition is met 
        /// </summary>
        public List<BattleEvent> BaseEvents;

        public CritNeededEvent() { BaseEvents = new List<BattleEvent>(); }
        public CritNeededEvent(params BattleEvent[] effects)
        {
            BaseEvents = new List<BattleEvent>();
            foreach (BattleEvent effect in effects)
                BaseEvents.Add(effect);
        }
        protected CritNeededEvent(CritNeededEvent other)
            : this()
        {
            foreach (BattleEvent battleEffect in other.BaseEvents)
                BaseEvents.Add((BattleEvent)battleEffect.Clone());
        }
        public override GameEvent Clone() { return new CritNeededEvent(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ContextStates.Contains<AttackCrit>())
            {
                foreach (BattleEvent battleEffect in BaseEvents)
                    yield return CoroutineManager.Instance.StartCoroutine(battleEffect.Apply(owner, ownerChar, context));
            }
        }
    }

    /// <summary>
    /// Event that groups multiple battle events into one event, but only applies if the character's type matches the specified type
    /// </summary>
    [Serializable]
    public class CharElementNeededEvent : BattleEvent
    {

        /// <summary>
        /// The list of battle events that will be applied if the condition is met 
        /// </summary>
        public List<BattleEvent> BaseEvents;

        /// <summary>
        /// The type to check for 
        /// </summary>
        [JsonConverter(typeof(ElementConverter))]
        [DataType(0, DataManager.DataType.Element, false)]
        public string NeededElement;

        /// <summary>
        /// Whether to run the type check on the user or target
        /// </summary> 
        public bool AffectTarget;

        /// <summary>
        /// If set, the events will only be applied if none of the character's types match the specified type
        /// </summary>
        public bool Inverted;

        public CharElementNeededEvent() { BaseEvents = new List<BattleEvent>(); NeededElement = ""; AffectTarget = true; Inverted = false; }
        public CharElementNeededEvent(string element, bool affectTarget, bool inverted, params BattleEvent[] effects)
            : this()
        {
            NeededElement = element;
            AffectTarget = affectTarget;
            Inverted = inverted;
            foreach (BattleEvent effect in effects)
                BaseEvents.Add(effect);
        }
        protected CharElementNeededEvent(CharElementNeededEvent other)
            : this()
        {
            NeededElement = other.NeededElement;
            foreach (BattleEvent battleEffect in other.BaseEvents)
                BaseEvents.Add((BattleEvent)battleEffect.Clone());
        }
        public override GameEvent Clone() { return new CharElementNeededEvent(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character character = AffectTarget ? context.Target : context.User;
            if (Inverted ^ character.HasElement(NeededElement)) //if inverted, must not correspond. If not inverted, must correspond
            {
                foreach (BattleEvent battleEffect in BaseEvents)
                    yield return CoroutineManager.Instance.StartCoroutine(battleEffect.Apply(owner, ownerChar, context));
            }
        }
    }

    /// <summary>
    /// Event that groups multiple battle events into one event, but only applies if the move type matches the specified type 
    /// </summary>
    [Serializable]
    public class ElementNeededEvent : BattleEvent
    {
        /// <summary>
        /// The list of battle events that will be applied if the condition is met 
        /// </summary>
        public List<BattleEvent> BaseEvents;

        /// <summary>
        /// The type to check for 
        /// </summary>
        [JsonConverter(typeof(ElementConverter))]
        [DataType(0, DataManager.DataType.Element, false)]
        public string NeededElement;

        public ElementNeededEvent() { BaseEvents = new List<BattleEvent>(); NeededElement = ""; }
        public ElementNeededEvent(string element, params BattleEvent[] effects)
            : this()
        {
            NeededElement = element;
            foreach (BattleEvent effect in effects)
                BaseEvents.Add(effect);
        }
        protected ElementNeededEvent(ElementNeededEvent other)
            : this()
        {
            NeededElement = other.NeededElement;
            foreach (BattleEvent battleEffect in other.BaseEvents)
                BaseEvents.Add((BattleEvent)battleEffect.Clone());
        }
        public override GameEvent Clone() { return new ElementNeededEvent(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Data.Element == NeededElement)
            {
                foreach (BattleEvent battleEffect in BaseEvents)
                    yield return CoroutineManager.Instance.StartCoroutine(battleEffect.Apply(owner, ownerChar, context));
            }
        }
    }

    /// <summary>
    /// Event that groups multiple battle events into one event, but only applies if the action matches the skill category 
    /// </summary>
    [Serializable]
    public class CategoryNeededEvent : BattleEvent
    {
        /// <summary>
        /// The list of battle events that will be applied if the condition is met 
        /// </summary>
        public List<BattleEvent> BaseEvents;

        /// <summary>
        /// The skill category to check for 
        /// </summary>
        public BattleData.SkillCategory NeededCategory;

        public CategoryNeededEvent() { BaseEvents = new List<BattleEvent>(); }
        public CategoryNeededEvent(BattleData.SkillCategory category, params BattleEvent[] effects)
            : this()
        {
            NeededCategory = category;
            foreach (BattleEvent effect in effects)
                BaseEvents.Add(effect);
        }
        protected CategoryNeededEvent(CategoryNeededEvent other)
            : this()
        {
            NeededCategory = other.NeededCategory;
            foreach (BattleEvent battleEffect in other.BaseEvents)
                BaseEvents.Add((BattleEvent)battleEffect.Clone());
        }
        public override GameEvent Clone() { return new CategoryNeededEvent(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Data.Category == NeededCategory)
            {
                foreach (BattleEvent battleEffect in BaseEvents)
                    yield return CoroutineManager.Instance.StartCoroutine(battleEffect.Apply(owner, ownerChar, context));
            }
        }
    }

    /// <summary>
    /// Event that groups multiple battle events into one event, but only applies if an attacking move is used
    /// </summary>
    [Serializable]
    public class AttackingMoveNeededEvent : BattleEvent
    {
        /// <summary>
        /// The list of battle events that will be applied if the condition is met 
        /// </summary>
        public List<BattleEvent> BaseEvents;

        public AttackingMoveNeededEvent() { BaseEvents = new List<BattleEvent>(); }
        public AttackingMoveNeededEvent(params BattleEvent[] effects)
            : this()
        {
            foreach (BattleEvent effect in effects)
                BaseEvents.Add(effect);
        }
        protected AttackingMoveNeededEvent(AttackingMoveNeededEvent other)
            : this()
        {
            foreach (BattleEvent battleEffect in other.BaseEvents)
                BaseEvents.Add((BattleEvent)battleEffect.Clone());
        }
        public override GameEvent Clone() { return new AttackingMoveNeededEvent(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Data.Category == BattleData.SkillCategory.Physical || context.Data.Category == BattleData.SkillCategory.Magical)
            {
                foreach (BattleEvent battleEffect in BaseEvents)
                    yield return CoroutineManager.Instance.StartCoroutine(battleEffect.Apply(owner, ownerChar, context));
            }
        }
    }

    /// <summary>
    /// Event that groups multiple battle events into one event, but only applies on action
    /// </summary>
    [Serializable]
    public class OnActionEvent : BattleEvent
    {

        /// <summary>
        /// The list of battle events that will be applied if the condition is met 
        /// </summary>
        public List<BattleEvent> BaseEvents;

        public OnActionEvent() { BaseEvents = new List<BattleEvent>(); }
        public OnActionEvent(params BattleEvent[] effects)
        {
            BaseEvents = new List<BattleEvent>();
            foreach (BattleEvent effect in effects)
                BaseEvents.Add(effect);
        }
        protected OnActionEvent(OnActionEvent other)
            : this()
        {
            foreach (BattleEvent battleEffect in other.BaseEvents)
                BaseEvents.Add((BattleEvent)battleEffect.Clone());
        }
        public override GameEvent Clone() { return new OnActionEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.UsageSlot == BattleContext.FORCED_SLOT)
                yield break;

            foreach (BattleEvent battleEffect in BaseEvents)
                yield return CoroutineManager.Instance.StartCoroutine(battleEffect.Apply(owner, ownerChar, context));
        }
    }

    /// <summary>
    /// Event that groups multiple battle events into one event, but only applies when the character attacks
    /// </summary>
    [Serializable]
    public class OnAggressionEvent : BattleEvent
    {

        /// <summary>
        /// The list of battle events that will be applied if the condition is met 
        /// </summary>
        public List<BattleEvent> BaseEvents;

        public OnAggressionEvent() { BaseEvents = new List<BattleEvent>(); }
        public OnAggressionEvent(params BattleEvent[] effects)
        {
            BaseEvents = new List<BattleEvent>();
            foreach (BattleEvent effect in effects)
                BaseEvents.Add(effect);
        }
        protected OnAggressionEvent(OnAggressionEvent other)
            : this()
        {
            foreach (BattleEvent battleEffect in other.BaseEvents)
                BaseEvents.Add((BattleEvent)battleEffect.Clone());
        }
        public override GameEvent Clone() { return new OnAggressionEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.UsageSlot == BattleContext.FORCED_SLOT)
                yield break;
            if (context.ActionType != BattleActionType.Skill)
                yield break;
            foreach (BattleEvent battleEffect in BaseEvents)
                yield return CoroutineManager.Instance.StartCoroutine(battleEffect.Apply(owner, ownerChar, context));
        }
    }

    /// <summary>
    /// Event that groups multiple battle events into one event, but only applies if the character uses a move
    /// </summary>
    [Serializable]
    public class OnMoveUseEvent : BattleEvent
    {
        /// <summary>
        /// The list of battle events that will be applied if the condition is met 
        /// </summary>
        public List<BattleEvent> BaseEvents;

        public OnMoveUseEvent() { BaseEvents = new List<BattleEvent>(); }
        public OnMoveUseEvent(params BattleEvent[] effects)
        {
            BaseEvents = new List<BattleEvent>();
            foreach (BattleEvent effect in effects)
                BaseEvents.Add(effect);
        }
        protected OnMoveUseEvent(OnMoveUseEvent other)
            : this()
        {
            foreach (BattleEvent battleEffect in other.BaseEvents)
                BaseEvents.Add((BattleEvent)battleEffect.Clone());
        }
        public override GameEvent Clone() { return new OnMoveUseEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.UsageSlot == BattleContext.FORCED_SLOT)
                yield break;

            if (context.ActionType == BattleActionType.Skill && context.Data.ID != DataManager.Instance.DefaultSkill)
            {
                foreach (BattleEvent battleEffect in BaseEvents)
                    yield return CoroutineManager.Instance.StartCoroutine(battleEffect.Apply(owner, ownerChar, context));
            }
        }
    }

    /// <summary>
    /// Event that groups multiple battle events into one event, but only applies
    /// when the target matches the one of the specified alignments
    /// </summary>
    [Serializable]
    public class TargetNeededEvent : BattleEvent
    {
        /// <summary>
        /// The alignments to check for
        /// </summary>
        public Alignment Target;

        /// <summary>
        /// The list of battle events that will be applied if the condition is met 
        /// </summary>
        public List<BattleEvent> BaseEvents;

        public TargetNeededEvent() { BaseEvents = new List<BattleEvent>(); }
        public TargetNeededEvent(Alignment target, params BattleEvent[] effects)
        {
            Target = target;
            BaseEvents = new List<BattleEvent>();
            foreach (BattleEvent effect in effects)
                BaseEvents.Add(effect);
        }
        protected TargetNeededEvent(TargetNeededEvent other)
            : this()
        {
            foreach (BattleEvent battleEffect in other.BaseEvents)
                BaseEvents.Add((BattleEvent)battleEffect.Clone());
        }
        public override GameEvent Clone() { return new TargetNeededEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if ((DungeonScene.Instance.GetMatchup(context.User, context.Target) & Target) != Alignment.None)
            {
                foreach (BattleEvent battleEffect in BaseEvents)
                    yield return CoroutineManager.Instance.StartCoroutine(battleEffect.Apply(owner, ownerChar, context));
            }
        }
    }

    /// <summary>
    /// Event that groups multiple battle events into one event, but only applies when the hitbox action is a SelfAction
    /// </summary>
    [Serializable]
    public class OnSelfActionEvent : BattleEvent
    {
        /// <summary>
        /// The list of battle events that will be applied if the condition is met 
        /// </summary>
        public List<BattleEvent> BaseEvents;

        public OnSelfActionEvent() { BaseEvents = new List<BattleEvent>(); }
        public OnSelfActionEvent(params BattleEvent[] effects)
        {
            BaseEvents = new List<BattleEvent>();
            foreach (BattleEvent effect in effects)
                BaseEvents.Add(effect);
        }
        protected OnSelfActionEvent(OnSelfActionEvent other)
            : this()
        {
            foreach (BattleEvent battleEffect in other.BaseEvents)
                BaseEvents.Add((BattleEvent)battleEffect.Clone());
        }
        public override GameEvent Clone() { return new OnSelfActionEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.HitboxAction is SelfAction)
            {
                foreach (BattleEvent battleEffect in BaseEvents)
                    yield return CoroutineManager.Instance.StartCoroutine(battleEffect.Apply(owner, ownerChar, context));
            }
        }
    }



    /// <summary>
    /// Event that groups multiple battle events into one event,
    /// but only applies when the hitbox action is an item or throw action that has a berry
    /// </summary>
    [Serializable]
    public class BerryNeededEvent : BattleEvent
    {

        /// <summary>
        /// The list of battle events that will be applied if the condition is met 
        /// </summary>
        public List<BattleEvent> BaseEvents;

        public BerryNeededEvent() { BaseEvents = new List<BattleEvent>(); }
        public BerryNeededEvent(params BattleEvent[] effects)
        {
            BaseEvents = new List<BattleEvent>();
            foreach (BattleEvent effect in effects)
                BaseEvents.Add(effect);
        }
        protected BerryNeededEvent(BerryNeededEvent other)
            : this()
        {
            foreach (BattleEvent battleEffect in other.BaseEvents)
                BaseEvents.Add((BattleEvent)battleEffect.Clone());
        }
        public override GameEvent Clone() { return new BerryNeededEvent(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Item || context.ActionType == BattleActionType.Throw)
            {
                ItemData itemData = DataManager.Instance.GetItem(context.Item.ID);
                if (itemData.ItemStates.Contains<BerryState>())
                {
                    foreach (BattleEvent battleEffect in BaseEvents)
                        yield return CoroutineManager.Instance.StartCoroutine(battleEffect.Apply(owner, ownerChar, context));
                }
            }
        }
    }

    /// <summary>
    /// Event that groups multiple battle events into one event, but only applies if a StatusBattleEvent was used
    /// and its status matches one of the specified status
    /// </summary>
    [Serializable]
    public class GiveStatusNeededEvent : BattleEvent
    {

        /// <summary>
        /// The list of statuses to check for 
        /// </summary>
        [JsonConverter(typeof(StatusArrayConverter))]
        [DataType(1, DataManager.DataType.Status, false)]
        public string[] Statuses;

        /// <summary>
        /// The list of battle events that will be applied if the condition is met 
        /// </summary>
        public List<BattleEvent> BaseEvents;

        public GiveStatusNeededEvent() { BaseEvents = new List<BattleEvent>(); }
        public GiveStatusNeededEvent(string[] statuses, params BattleEvent[] effects)
        {
            Statuses = statuses;
            BaseEvents = new List<BattleEvent>();
            foreach (BattleEvent effect in effects)
                BaseEvents.Add(effect);
        }
        protected GiveStatusNeededEvent(GiveStatusNeededEvent other)
            : this()
        {
            Statuses = new string[other.Statuses.Length];
            Array.Copy(other.Statuses, Statuses, Statuses.Length);
            foreach (BattleEvent battleEffect in other.BaseEvents)
                BaseEvents.Add((BattleEvent)battleEffect.Clone());
        }
        public override GameEvent Clone() { return new GiveStatusNeededEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            bool hasStatus = false;
            foreach (BattleEvent effect in context.Data.OnHits.EnumerateInOrder())
            {
                StatusBattleEvent statusEvent = effect as StatusBattleEvent;
                if (statusEvent != null)
                {
                    foreach (string status in Statuses)
                    {
                        if (statusEvent.StatusID == status)
                        {
                            hasStatus = true;
                            break;
                        }
                    }
                }
            }
            if (hasStatus)
            {
                foreach (BattleEvent battleEffect in BaseEvents)
                    yield return CoroutineManager.Instance.StartCoroutine(battleEffect.Apply(owner, ownerChar, context));
            }
        }
    }

    /// <summary>
    /// Event that groups multiple battle events into one event, but only applies if the hitbox action is a DashAction 
    /// </summary>
    [Serializable]
    public class OnDashActionEvent : BattleEvent
    {
        /// <summary>
        /// The list of battle events that will be applied if the condition is met 
        /// </summary>
        public List<BattleEvent> BaseEvents;

        public OnDashActionEvent() { BaseEvents = new List<BattleEvent>(); }
        public OnDashActionEvent(params BattleEvent[] effects)
        {
            BaseEvents = new List<BattleEvent>();
            foreach (BattleEvent effect in effects)
                BaseEvents.Add(effect);
        }
        protected OnDashActionEvent(OnDashActionEvent other)
            : this()
        {
            foreach (BattleEvent battleEffect in other.BaseEvents)
                BaseEvents.Add((BattleEvent)battleEffect.Clone());
        }
        public override GameEvent Clone() { return new OnDashActionEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.HitboxAction is DashAction)
            {
                foreach (BattleEvent battleEffect in BaseEvents)
                    yield return CoroutineManager.Instance.StartCoroutine(battleEffect.Apply(owner, ownerChar, context));
            }
        }
    }

    /// <summary>
    /// Event that groups multiple battle events into one event, but only applies if the hitbox action is a MeleeAction or DashAction 
    /// </summary>
    [Serializable]
    public class OnMeleeActionEvent : BattleEvent
    {
        /// <summary>
        /// Whether to check for any other hitbox action instead
        /// </summary>
        public bool Invert;

        /// <summary>
        /// The list of battle events that will be applied if the condition is met 
        /// </summary>
        public List<BattleEvent> BaseEvents;

        public OnMeleeActionEvent() { BaseEvents = new List<BattleEvent>(); }
        public OnMeleeActionEvent(bool invert, params BattleEvent[] effects)
        {
            Invert = invert;
            BaseEvents = new List<BattleEvent>();
            foreach (BattleEvent effect in effects)
                BaseEvents.Add(effect);
        }
        protected OnMeleeActionEvent(OnMeleeActionEvent other)
            : this()
        {
            Invert = other.Invert;
            foreach (BattleEvent battleEffect in other.BaseEvents)
                BaseEvents.Add((BattleEvent)battleEffect.Clone());
        }
        public override GameEvent Clone() { return new OnMeleeActionEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if ((context.HitboxAction is AttackAction || context.HitboxAction is DashAction) != Invert)
            {
                foreach (BattleEvent battleEffect in BaseEvents)
                    yield return CoroutineManager.Instance.StartCoroutine(battleEffect.Apply(owner, ownerChar, context));
            }
        }
    }

    /// <summary>
    /// Event that groups multiple battle events into one event, but only applies if the move mathces one of the specfied moves
    /// </summary>
    [Serializable]
    public class SpecificSkillNeededEvent : BattleEvent
    {
        //can be used for hit-consequence effects
        /// <summary>
        /// The battle event that applies if the condition is met
        /// </summary>
        public BattleEvent BaseEvent;

        /// <summary>
        /// The list of moves to check for 
        /// </summary>
        [JsonConverter(typeof(SkillListConverter))]
        [DataType(1, DataManager.DataType.Skill, false)]
        public List<string> AcceptedMoves;

        public SpecificSkillNeededEvent() { AcceptedMoves = new List<string>(); }
        public SpecificSkillNeededEvent(BattleEvent effect, params string[] acceptableMoves)
            : this()
        {
            BaseEvent = effect;
            AcceptedMoves.AddRange(acceptableMoves);
        }
        protected SpecificSkillNeededEvent(SpecificSkillNeededEvent other)
            : this()
        {
            BaseEvent = (BattleEvent)other.BaseEvent.Clone();
            AcceptedMoves.AddRange(other.AcceptedMoves);

        }
        public override GameEvent Clone() { return new SpecificSkillNeededEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Skill && AcceptedMoves.Contains(context.Data.ID))
            {
                yield return CoroutineManager.Instance.StartCoroutine(BaseEvent.Apply(owner, ownerChar, context));
            }
        }
    }

    /// <summary>
    /// Event that groups multiple battle events into one event, but only applies if the action is a regular attack
    /// </summary>
    [Serializable]
    public class RegularAttackNeededEvent : BattleEvent
    {
        //can be used for hit-consequence effects
        /// <summary>
        /// The battle event that applies if the condition is met 
        /// </summary>
        public BattleEvent BaseEvent;

        public RegularAttackNeededEvent() { }
        public RegularAttackNeededEvent(BattleEvent effect)
            : this()
        {
            BaseEvent = effect;
        }
        protected RegularAttackNeededEvent(RegularAttackNeededEvent other)
            : this()
        {
            BaseEvent = (BattleEvent)other.BaseEvent.Clone();
        }
        public override GameEvent Clone() { return new RegularAttackNeededEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Skill && context.UsageSlot == BattleContext.DEFAULT_ATTACK_SLOT)
            {
                yield return CoroutineManager.Instance.StartCoroutine(BaseEvent.Apply(owner, ownerChar, context));
            }
        }
    }


    /// <summary>
    /// Event that groups multiple battle events into one event, but only applies if the used item has the WandState item state
    /// </summary>
    [Serializable]
    public class WandAttackNeededEvent : BattleEvent
    {
        //can be used for hit-consequence effects
        [JsonConverter(typeof(ItemListConverter))]
        [DataType(1, DataManager.DataType.Item, false)]
        public List<string> ExceptItems;

        /// <summary>
        /// The battle event that applies if the condition is met 
        /// </summary>
        public BattleEvent BaseEvent;

        public WandAttackNeededEvent() { ExceptItems = new List<string>(); }
        public WandAttackNeededEvent(List<string> exceptions, BattleEvent effect)
            : this()
        {
            ExceptItems = exceptions;
            BaseEvent = effect;
        }
        protected WandAttackNeededEvent(WandAttackNeededEvent other)
            : this()
        {
            BaseEvent = (BattleEvent)other.BaseEvent.Clone();
        }
        public override GameEvent Clone() { return new WandAttackNeededEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Item)
            {
                ItemData data = DataManager.Instance.GetItem(context.Item.ID);

                if (data.ItemStates.Contains<WandState>() && !ExceptItems.Contains(context.Item.ID))
                {
                    yield return CoroutineManager.Instance.StartCoroutine(BaseEvent.Apply(owner, ownerChar, context));
                }
            }
        }
    }

    /// <summary>
    /// Event that groups multiple battle events into one event, but only applies if an item was thrown 
    /// </summary>
    [Serializable]
    public class ThrownItemNeededEvent : BattleEvent
    {
        //can be used for hit-consequence effects
        /// <summary>
        /// The battle event that applies if the condition is met 
        /// </summary>
        public BattleEvent BaseEvent;

        public ThrownItemNeededEvent() { }
        public ThrownItemNeededEvent(BattleEvent effect)
            : this()
        {
            BaseEvent = effect;
        }
        protected ThrownItemNeededEvent(ThrownItemNeededEvent other)
            : this()
        {
            BaseEvent = (BattleEvent)other.BaseEvent.Clone();
        }
        public override GameEvent Clone() { return new ThrownItemNeededEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Throw)
            {
                yield return CoroutineManager.Instance.StartCoroutine(BaseEvent.Apply(owner, ownerChar, context));
            }
        }
    }

    /// <summary>
    /// Event that groups multiple battle events into one event, but only applies if the user lands a hit on an enemy
    /// </summary>
    [Serializable]
    public class OnHitEvent : BattleEvent
    {
        //can be used for hit-consequence effects

        /// <summary>
        /// The battle event that will be applied if the condition is met 
        /// </summary>
        public List<BattleEvent> BaseEvents;
        public bool RequireDamage;
        public bool RequireContact;
        public int Chance;

        public OnHitEvent() { BaseEvents = new List<BattleEvent>(); }
        public OnHitEvent(bool requireDamage, bool requireContact, int chance, params BattleEvent[] effects)
            : this()
        {
            RequireDamage = requireDamage;
            RequireContact = requireContact;
            Chance = chance;
            foreach (BattleEvent effect in effects)
                BaseEvents.Add(effect);
        }
        protected OnHitEvent(OnHitEvent other)
            : this()
        {
            foreach (BattleEvent battleEffect in other.BaseEvents)
                BaseEvents.Add((BattleEvent)battleEffect.Clone());
            RequireDamage = other.RequireDamage;
            RequireContact = other.RequireContact;
            Chance = other.Chance;
        }
        public override GameEvent Clone() { return new OnHitEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Trap)
                yield break;

            if ((!RequireDamage || context.GetContextStateInt<DamageDealt>(0) > 0)
                && (RequireDamage || DungeonScene.Instance.GetMatchup(context.User, context.Target) == Alignment.Foe)
                && (!RequireContact || context.Data.SkillStates.Contains<ContactState>()))
            {
                if (DataManager.Instance.Save.Rand.Next(100) <= Chance)
                    yield return CoroutineManager.Instance.StartCoroutine(BaseEvents[DataManager.Instance.Save.Rand.Next(BaseEvents.Count)].Apply(owner, ownerChar, context));
            }
        }
    }

    /// <summary>
    /// Event that groups multiple battle events into one event, but only applies if the character lands a hit on anyone
    /// </summary>
    [Serializable]
    public class OnHitAnyEvent : BattleEvent
    {
        //can be used for hit-consequence effects
        /// <summary>
        /// The list of battle events that will be applied if the condition is met 
        /// </summary>
        public List<BattleEvent> BaseEvents;

        /// <summary>
        /// Whether the hit needs to deal damage
        /// </summary>
        public bool RequireDamage;

        /// <summary>
        /// The chance for the events to apply (0, 100)
        /// </summary>
        public int Chance;

        public OnHitAnyEvent() { BaseEvents = new List<BattleEvent>(); }
        public OnHitAnyEvent(bool requireDamage, int chance, params BattleEvent[] effects)
            : this()
        {
            RequireDamage = requireDamage;
            Chance = chance;
            foreach (BattleEvent effect in effects)
                BaseEvents.Add(effect);
        }
        protected OnHitAnyEvent(OnHitAnyEvent other)
            : this()
        {
            foreach (BattleEvent battleEffect in other.BaseEvents)
                BaseEvents.Add((BattleEvent)battleEffect.Clone());
            RequireDamage = other.RequireDamage;
            Chance = other.Chance;
        }
        public override GameEvent Clone() { return new OnHitAnyEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.GetContextStateInt<AttackHitTotal>(true, 0) > 0
                && (!RequireDamage || context.GetContextStateInt<TotalDamageDealt>(true, 0) > 0))
            {
                if (DataManager.Instance.Save.Rand.Next(100) <= Chance)
                    yield return CoroutineManager.Instance.StartCoroutine(BaseEvents[DataManager.Instance.Save.Rand.Next(BaseEvents.Count)].Apply(owner, ownerChar, context));
            }
        }
    }

    /// <summary>
    /// Event that groups multiple battle events into one event, but only applies if the character is hit
    /// </summary>
    [Serializable]
    public class HitCounterEvent : BattleEvent
    {
        //can be used for hit-consequence effects
        /// <summary>
        /// The list of battle events that will be applied if the condition is met 
        /// </summary>
        public List<BattleEvent> BaseEvents;

        /// <summary>
        /// The alignments that can be affected
        /// </summary>
        public Alignment Targets;

        /// <summary>
        /// Whether the hit needs to deal damage
        /// </summary>
        public bool RequireDamage;

        /// <summary>
        /// Whether the move needs to contain the ContactState skill state 
        /// </summary>
        public bool RequireContact;

        /// <summary>
        /// Whether the move needs to contain the ContactState skill state 
        /// </summary>
        public bool RequireSurvive;

        /// <summary>
        /// The chance for the events to apply (0, 100)
        /// </summary>
        public int Chance;

        public HitCounterEvent() { BaseEvents = new List<BattleEvent>(); }
        public HitCounterEvent(Alignment targets, int chance, params BattleEvent[] effects)
            : this(targets, true, true, false, chance, effects)
        { }
        public HitCounterEvent(Alignment targets, bool requireDamage, bool requireContact, bool requireSurvive, int chance, params BattleEvent[] effects)
            : this()
        {
            Targets = targets;
            RequireDamage = requireDamage;
            RequireContact = requireContact;
            RequireSurvive = requireSurvive;
            Chance = chance;
            foreach (BattleEvent effect in effects)
                BaseEvents.Add(effect);
        }
        protected HitCounterEvent(HitCounterEvent other)
            : this()
        {
            Targets = other.Targets;
            foreach (BattleEvent battleEffect in other.BaseEvents)
                BaseEvents.Add((BattleEvent)battleEffect.Clone());
            RequireDamage = other.RequireDamage;
            RequireContact = other.RequireContact;
            RequireSurvive = other.RequireSurvive;
            Chance = other.Chance;
        }
        public override GameEvent Clone() { return new HitCounterEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType != BattleActionType.Skill)
                yield break;

            if ((DungeonScene.Instance.GetMatchup(context.Target, context.User) & Targets) != Alignment.None
                && (!RequireDamage || context.GetContextStateInt<DamageDealt>(0) > 0)
                && (!RequireContact || context.Data.SkillStates.Contains<ContactState>())
                && (!RequireSurvive || !context.Target.Dead))
            {
                if (DataManager.Instance.Save.Rand.Next(100) <= Chance)
                    yield return CoroutineManager.Instance.StartCoroutine(BaseEvents[DataManager.Instance.Save.Rand.Next(BaseEvents.Count)].Apply(owner, ownerChar, context));
            }
        }
    }

}

