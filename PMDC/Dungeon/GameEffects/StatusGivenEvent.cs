using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence.Data;
using RogueEssence.Content;
using RogueEssence;
using RogueEssence.Dungeon;
using RogueEssence.Dev;
using Newtonsoft.Json;
using Avalonia.X11;
using DynamicData;

namespace PMDC.Dungeon
{
    [Serializable]
    public class ThisStatusGivenEvent : StatusGivenEvent
    {
        
        public StatusGivenEvent BaseEvent;

        public ThisStatusGivenEvent() { }
        public ThisStatusGivenEvent(StatusGivenEvent baseEffect)
        {
            BaseEvent = baseEffect;
        }
        protected ThisStatusGivenEvent(ThisStatusGivenEvent other)
        {
            BaseEvent = (StatusGivenEvent)other.BaseEvent.Clone();
        }
        public override GameEvent Clone() { return new ThisStatusGivenEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            if (context.Status != owner)
                yield break;
            
            yield return CoroutineManager.Instance.StartCoroutine(BaseEvent.Apply(owner, ownerChar, context));

        }
    }


    [Serializable]
    public class FamilyStatusEvent : StatusGivenEvent
    {
        public StatusGivenEvent BaseEvent;

        public FamilyStatusEvent() { }
        public FamilyStatusEvent(StatusGivenEvent baseEffect)
        {
            BaseEvent = baseEffect;
        }
        protected FamilyStatusEvent(FamilyStatusEvent other)
        {
            BaseEvent = (StatusGivenEvent)other.BaseEvent.Clone();
        }
        public override GameEvent Clone() { return new FamilyStatusEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            ItemData entry = DataManager.Instance.GetItem(owner.GetID());
            FamilyState family;
            if (!entry.ItemStates.TryGet<FamilyState>(out family))
                yield break;
            if (family.Members.Contains(ownerChar.BaseForm.Species))
                yield return CoroutineManager.Instance.StartCoroutine(BaseEvent.Apply(owner, ownerChar, context));
        }
    }

    [Serializable]
    public class StatusCharEvent : StatusGivenEvent
    {
        public SingleCharEvent BaseEvent;
        public bool AffectTarget;

        public StatusCharEvent() { }
        public StatusCharEvent(SingleCharEvent baseEffect, bool affectTarget)
        {
            BaseEvent = baseEffect;
            AffectTarget = affectTarget;
        }
        protected StatusCharEvent(StatusCharEvent other)
        {
            BaseEvent = (SingleCharEvent)other.BaseEvent.Clone();
            AffectTarget = other.AffectTarget;
        }
        public override GameEvent Clone() { return new StatusCharEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            SingleCharContext singleContext = new SingleCharContext(AffectTarget ? context.Target : context.User);
            yield return CoroutineManager.Instance.StartCoroutine(BaseEvent.Apply(owner, ownerChar, singleContext));
        }
    }



    [Serializable]
    public class StatusCountdownCheck : StatusGivenEvent
    {
        public StatusCountdownCheck() { }
        public override GameEvent Clone() { return new StatusCountdownCheck(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            if (owner == context.Status)
            {
                CountDownState countdown = context.Status.StatusStates.GetWithDefault<CountDownState>();

                StatusEffect existingStatus = context.Target.GetStatusEffect(context.Status.ID);
                if (existingStatus != null)
                {
                    int counter = existingStatus.StatusStates.GetWithDefault<CountDownState>().Counter;
                    countdown.Counter = counter - 1;
                }
            }
            yield break;
        }
    }


    [Serializable]
    public abstract class StatusStackCheck : StatusGivenEvent
    {
        public int Minimum;
        public int Maximum;

        protected StatusStackCheck() { }
        protected StatusStackCheck(int min, int max)
        {
            Minimum = min;
            Maximum = max;
        }
        protected StatusStackCheck(StatusStackCheck other)
        {
            Minimum = other.Minimum;
            Maximum = other.Maximum;
        }

        protected abstract string GetLimitMsg(Character target, bool upperLimit);

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            if (owner == context.Status)
            {
                int counter = 0;
                StatusEffect existingStatus = context.Target.GetStatusEffect(context.Status.ID);
                if (existingStatus != null)
                    counter = existingStatus.StatusStates.GetWithDefault<StackState>().Stack;

                StackState boost = context.Status.StatusStates.GetWithDefault<StackState>();
                int stackDiff = boost.Stack;
                if (counter + boost.Stack > Maximum)
                {
                    stackDiff = Maximum - counter;
                    if (stackDiff == 0)
                    {
                        DungeonScene.Instance.LogMsg(GetLimitMsg(context.Target, true));
                        context.CancelState.Cancel = true;
                    }
                }
                else if (counter + boost.Stack < Minimum)
                {
                    stackDiff = Minimum - counter;
                    if (stackDiff == 0)
                    {
                        DungeonScene.Instance.LogMsg(GetLimitMsg(context.Target, false));
                        context.CancelState.Cancel = true;
                    }
                }
                boost.Stack = counter + stackDiff;
                if (stackDiff != 0)
                    context.StackDiff = stackDiff;
            }
            yield break;
        }
    }
    [Serializable]
    public class StringStackCheck : StatusStackCheck
    {
        public StringKey HiLimitMsg;
        public StringKey LoLimitMsg;

        public StringStackCheck() { }
        public StringStackCheck(int min, int max, StringKey hiMsg, StringKey loMsg)
            : base(min, max)
        {
            HiLimitMsg = hiMsg;
            LoLimitMsg = loMsg;
        }
        protected StringStackCheck(StringStackCheck other)
            : base(other)
        {
            HiLimitMsg = other.HiLimitMsg;
            LoLimitMsg = other.LoLimitMsg;
        }
        public override GameEvent Clone() { return new StringStackCheck(this); }

        protected override string GetLimitMsg(Character target, bool upperLimit)
        {
            if (upperLimit)
                return Text.FormatGrammar(HiLimitMsg.ToLocal(), target.GetDisplayName(false));
            else
                return Text.FormatGrammar(LoLimitMsg.ToLocal(), target.GetDisplayName(false));
        }
    }
    [Serializable]
    public class StatStackCheck : StatusStackCheck
    {
        public Stat Stack;

        public StatStackCheck() { }
        public StatStackCheck(int min, int max, Stat stack)
            : base(min, max)
        {
            Stack = stack;
        }
        protected StatStackCheck(StatStackCheck other)
            : base(other)
        {
            Stack = other.Stack;
        }
        public override GameEvent Clone() { return new StatStackCheck(this); }

        protected override string GetLimitMsg(Character target, bool upperLimit)
        {
            if (upperLimit)
                return Text.FormatGrammar(new StringKey("MSG_BUFF_NO_MORE").ToLocal(), target.GetDisplayName(false), Stack.ToLocal());
            else
                return Text.FormatGrammar(new StringKey("MSG_BUFF_NO_LESS").ToLocal(), target.GetDisplayName(false), Stack.ToLocal());
        }
    }

    [Serializable]
    public class StatusStackMod : StatusGivenEvent
    {
        public int Mod;

        public StatusStackMod() { }
        public StatusStackMod(int mod)
        {
            Mod = mod;
        }
        protected StatusStackMod(StatusStackMod other)
        {
            Mod = other.Mod;
        }
        public override GameEvent Clone() { return new StatusStackMod(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            if (owner != context.Status)//can't check on self
            {
                if (context.Status.StatusStates.GetWithDefault<StatChangeState>() != null)
                {
                    StackState stackState = context.Status.StatusStates.GetWithDefault<StackState>();
                    if (stackState != null)
                        stackState.Stack *= Mod;
                }
            }
            yield break;
        }
    }
    [Serializable]
    public class StatusStackBoostMod : StatusGivenEvent
    {
        [StringTypeConstraint(1, typeof(CharState))]
        public List<FlagType> States;

        public int Stack;

        public StatusStackBoostMod() { States = new List<FlagType>(); }
        public StatusStackBoostMod(Type state, int stack) : this() { States.Add(new FlagType(state)); Stack = stack; }
        public StatusStackBoostMod(StatusStackBoostMod other) : this() { States.AddRange(other.States); Stack = other.Stack; }

        public override GameEvent Clone() { return new StatusStackBoostMod(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            if (owner == context.Status)//done BY the pending status
            {
                //check if the attacker has the right charstate
                bool hasState = false;
                foreach (FlagType state in States)
                {
                    if (context.User.CharStates.Contains(state.FullType))
                        hasState = true;
                }
                if (context.User != null && hasState)
                {
                    StackState stack = context.Status.StatusStates.GetWithDefault<StackState>();
                    if (stack != null)
                        stack.Stack += Stack;
                }
            }
            yield break;
        }

    }

    [Serializable]
    public class StatusCountBoostMod : StatusGivenEvent
    {
        [StringTypeConstraint(1, typeof(CharState))]
        public List<FlagType> States;

        public int Stack;

        public StatusCountBoostMod() { States = new List<FlagType>(); }
        public StatusCountBoostMod(Type state, int stack) : this() { States.Add(new FlagType(state)); Stack = stack; }
        public StatusCountBoostMod(StatusCountBoostMod other) : this() { States.AddRange(other.States); Stack = other.Stack; }

        public override GameEvent Clone() { return new StatusCountBoostMod(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            if (owner == context.Status)//done BY the pending status
            {
                //check if the attacker has the right charstate
                bool hasState = false;
                foreach (FlagType state in States)
                {
                    if (context.User.CharStates.Contains(state.FullType))
                        hasState = true;
                }
                if (context.User != null && hasState)
                {
                    CountState stack = context.Status.StatusStates.GetWithDefault<CountState>();
                    if (stack != null)
                        stack.Count += Stack;
                }
            }
            yield break;
        }

    }

    [Serializable]
    public class StatusHPBoostMod : StatusGivenEvent
    {
        [StringTypeConstraint(1, typeof(CharState))]
        public List<FlagType> States;

        public StatusHPBoostMod() { States = new List<FlagType>(); }
        public StatusHPBoostMod(Type state) : this() { States.Add(new FlagType(state)); }
        protected StatusHPBoostMod(StatusHPBoostMod other) : this() { States.AddRange(other.States); }

        public override GameEvent Clone() { return new StatusHPBoostMod(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            if (owner == context.Status)//done BY the pending status
            {
                //check if the attacker has the right charstate
                bool hasState = false;
                foreach (FlagType state in States)
                {
                    if (context.User.CharStates.Contains(state.FullType))
                        hasState = true;
                }
                if (context.User != null && hasState)
                {
                    HPState stack = context.Status.StatusStates.GetWithDefault<HPState>();
                    if (stack != null)
                        stack.HP *= 2;
                }
            }
            yield break;
        }

    }
    [Serializable]
    public class CountDownBoostMod : StatusGivenEvent
    {
        [StringTypeConstraint(1, typeof(CharState))]
        public List<FlagType> States;
        public int Numerator;
        public int Denominator;

        public CountDownBoostMod() { States = new List<FlagType>(); }
        public CountDownBoostMod(Type state, int num, int den) : this()
        {
            States.Add(new FlagType(state));
            Numerator = num;
            Denominator = den;
        }
        protected CountDownBoostMod(CountDownBoostMod other) : this()
        {
            States.AddRange(other.States);
            Numerator = other.Numerator;
            Denominator = other.Denominator;
        }
        public override GameEvent Clone() { return new CountDownBoostMod(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            if (owner == context.Status && context.User != null)// done BY pending status
            {
                //check if the attacker has the right charstate
                bool hasState = false;
                foreach (FlagType state in States)
                {
                    if (context.User.CharStates.Contains(state.FullType))
                        hasState = true;
                }
                if (hasState)
                {
                    //multiply turns, rounded up
                    CountDownState countDown = context.Status.StatusStates.GetWithDefault<CountDownState>();
                    if (countDown != null)
                    {
                        countDown.Counter *= Numerator;
                        countDown.Counter--;
                        countDown.Counter /= Denominator;
                        countDown.Counter++;
                    }
                }
            }
            yield break;
        }

    }

    [Serializable]
    public class SelfCurerEvent : StatusGivenEvent
    {
        public int Numerator;
        public int Denominator;

        public SelfCurerEvent() { }
        public SelfCurerEvent(int num, int den) : this()
        {
            Numerator = num;
            Denominator = den;
        }
        protected SelfCurerEvent(SelfCurerEvent other) : this()
        {
            Numerator = other.Numerator;
            Denominator = other.Denominator;
        }
        public override GameEvent Clone() { return new SelfCurerEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            //multiply turns, rounded down
            CountDownState countDown = context.Status.StatusStates.GetWithDefault<CountDownState>();
            if (countDown != null && context.Status.StatusStates.Contains<BadStatusState>())
            {
                int minCounter = Math.Min(2, countDown.Counter);
                countDown.Counter *= Numerator;
                countDown.Counter /= Denominator;
                if (countDown.Counter < minCounter)
                    countDown.Counter = minCounter;
            }
            yield break;
        }

    }

    [Serializable]
    public class SameStatusCheck : StatusGivenEvent
    {
        [StringKey(0, true)]
        public StringKey Message;

        public SameStatusCheck() { }
        public SameStatusCheck(StringKey message)
        {
            Message = message;
        }
        protected SameStatusCheck(SameStatusCheck other)
        {
            Message = other.Message;
        }
        public override GameEvent Clone() { return new SameStatusCheck(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            if (owner != context.Status)//can't check on self
            {
                if (context.Status.ID == ((StatusEffect)owner).ID)
                {
                    if (context.msg && Message.IsValid())
                        DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), context.Target.GetDisplayName(false), owner.GetDisplayName()));
                    context.CancelState.Cancel = true;
                }
            }
            yield break;
        }
    }
    [Serializable]
    public class SameTargetedStatusCheck : StatusGivenEvent
    {
        [StringKey(0, true)]
        public StringKey Message;

        public SameTargetedStatusCheck() { }
        public SameTargetedStatusCheck(StringKey message)
        {
            Message = message;
        }
        protected SameTargetedStatusCheck(SameTargetedStatusCheck other)
        {
            Message = other.Message;
        }
        public override GameEvent Clone() { return new SameTargetedStatusCheck(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            if (owner != context.Status)//can't check on self
            {
                if (context.Status.ID == ((StatusEffect)owner).ID)
                {
                    if (context.msg && Message.IsValid() && ((StatusEffect)owner).TargetChar != null)
                        DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), context.Target.GetDisplayName(false), ((StatusEffect)owner).TargetChar.GetDisplayName(false)));
                    context.CancelState.Cancel = true;
                }
            }
            yield break;
        }
    }
    [Serializable]
    public class OKStatusCheck : StatusGivenEvent
    {
        [StringKey(0, true)]
        public StringKey Message;

        public OKStatusCheck() { }
        public OKStatusCheck(StringKey message)
        {
            Message = message;
        }
        protected OKStatusCheck(OKStatusCheck other)
        {
            Message = other.Message;
        }
        public override GameEvent Clone() { return new OKStatusCheck(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            if (context.Status == owner)//this check is done BY the pending status only
            {
                foreach (StatusEffect status in context.Target.IterateStatusEffects())
                {
                    if (status.StatusStates.Contains<MajorStatusState>())
                    {
                        if (context.msg && Message.IsValid())
                            DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), context.Target.GetDisplayName(false)));
                        context.CancelState.Cancel = true;
                        yield break;
                    }
                }
            }
        }
    }
    [Serializable]
    public class EmptySlotStatusCheck : StatusGivenEvent
    {
        [StringKey(0, true)]
        public StringKey Message;

        public EmptySlotStatusCheck() { }
        public EmptySlotStatusCheck(StringKey message)
        {
            Message = message;
        }
        protected EmptySlotStatusCheck(EmptySlotStatusCheck other)
        {
            Message = other.Message;
        }
        public override GameEvent Clone() { return new EmptySlotStatusCheck(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            if (context.Status == owner)//this check is done BY the pending status only
            {
                int slot = context.Status.StatusStates.GetWithDefault<SlotState>().Slot;
                if (String.IsNullOrEmpty(context.Target.Skills[slot].Element.SkillNum))
                {
                    if (context.msg && Message.IsValid())
                        DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), context.Target.GetDisplayName(false)));
                    context.CancelState.Cancel = true;
                    yield break;
                }
            }
        }
    }
    [Serializable]
    public class GenderStatusCheck : StatusGivenEvent
    {
        [StringKey(0, true)]
        public StringKey Message;

        public GenderStatusCheck() { }
        public GenderStatusCheck(StringKey message)
        {
            Message = message;
        }
        protected GenderStatusCheck(GenderStatusCheck other)
        {
            Message = other.Message;
        }
        public override GameEvent Clone() { return new GenderStatusCheck(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            if (context.Status == owner)//this check is done BY the pending status only
            {
                if ((context.Target.CurrentForm.Gender == Gender.Genderless) != (context.Target.CurrentForm.Gender == context.User.CurrentForm.Gender))
                {
                    if (context.msg && Message.IsValid())
                        DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), context.Target.GetDisplayName(false), context.User.GetDisplayName(false)));
                    context.CancelState.Cancel = true;
                }
            }
            yield break;
        }
    }

    [Serializable]
    public class TypeCheck : StatusGivenEvent
    {
        [JsonConverter(typeof(ElementConverter))]
        [DataType(0, DataManager.DataType.Element, false)]
        public string Element;
        [StringKey(0, true)]
        public StringKey Message;

        public TypeCheck() { Element = ""; }
        public TypeCheck(string element, StringKey message)
        {
            Element = element;
            Message = message;
        }
        protected TypeCheck(TypeCheck other)
        {
            Element = other.Element;
            Message = other.Message;
        }
        public override GameEvent Clone() { return new TypeCheck(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            if (context.Status == owner)//this check is done BY the pending status only
            {
                if (context.Target.HasElement(Element))
                {
                    if (context.msg && Message.IsValid())
                        DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), context.Target.GetDisplayName(false)));
                    context.CancelState.Cancel = true;
                }
            }
            yield break;
        }
    }
    [Serializable]
    public class PreventStatusCheck : StatusGivenEvent
    {
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string StatusID;
        [StringKey(0, true)]
        public StringKey Message;

        public List<StatusAnimEvent> Anims;

        public PreventStatusCheck()
        {
            Anims = new List<StatusAnimEvent>();
            StatusID = "";
        }
        public PreventStatusCheck(string statusID, StringKey message)
        {
            StatusID = statusID;
            Message = message;
            Anims = new List<StatusAnimEvent>();
        }
        public PreventStatusCheck(string statusID, StringKey message, params StatusAnimEvent[] anims)
        {
            StatusID = statusID;
            Message = message;

            Anims = new List<StatusAnimEvent>();
            Anims.AddRange(anims);
        }
        protected PreventStatusCheck(PreventStatusCheck other)
        {
            StatusID = other.StatusID;
            Message = other.Message;

            Anims = new List<StatusAnimEvent>();
            foreach (StatusAnimEvent anim in other.Anims)
                Anims.Add((StatusAnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new PreventStatusCheck(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            if (owner != context.Status)//can't check on self
            {
                if (StatusID == context.Status.ID)
                {
                    if (context.msg)
                    {
                        if (Message.IsValid())
                            DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), context.Target.GetDisplayName(false), owner.GetDisplayName()));

                        foreach (StatusAnimEvent anim in Anims)
                            yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                    }
                    context.CancelState.Cancel = true;
                }
            }
            yield break;
        }
    }

    [Serializable]
    public class StateStatusCheck : StatusGivenEvent
    {
        [StringTypeConstraint(1, typeof(StatusState))]
        public List<FlagType> States;
        [StringKey(0, true)]
        public StringKey Message;
        public List<StatusAnimEvent> Anims;

        public StateStatusCheck()
        {
            States = new List<FlagType>();
            Anims = new List<StatusAnimEvent>();
        }
        public StateStatusCheck(Type state, StringKey message) : this()
        {
            States.Add(new FlagType(state));
            Message = message;
        }
        public StateStatusCheck(Type state, StringKey message, params StatusAnimEvent[] anims) : this()
        {
            States.Add(new FlagType(state));
            Message = message;
            Anims.AddRange(anims);
        }
        protected StateStatusCheck(StateStatusCheck other)
        {
            States.AddRange(other.States);
            Message = other.Message;
            Anims = new List<StatusAnimEvent>();
            foreach (StatusAnimEvent anim in other.Anims)
                Anims.Add((StatusAnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new StateStatusCheck(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            if (owner != context.Status)//can't check on self
            {
                bool hasState = false;
                foreach (FlagType state in States)
                {
                    if (context.Status.StatusStates.Contains(state.FullType))
                        hasState = true;
                }
                if (hasState)
                {
                    if (context.msg && Message.IsValid())
                    {
                        DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), context.Target.GetDisplayName(false), owner.GetDisplayName()));

                        foreach (StatusAnimEvent anim in Anims)
                            yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                    }
                    context.CancelState.Cancel = true;
                }
            }
            yield break;
        }

    }


    [Serializable]
    public abstract class StatChangeCheckBase : StatusGivenEvent
    {
        [StringKey(0, true)]
        public StringKey Message;
        public List<Stat> Stats;
        public bool Drop;
        public bool Boost;
        public bool IncludeSelf;
        public List<StatusAnimEvent> Anims;

        public StatChangeCheckBase()
        {
            Stats = new List<Stat>();
            Anims = new List<StatusAnimEvent>();
        }
        public StatChangeCheckBase(List<Stat> stats, StringKey message, bool drop, bool boost, bool includeSelf)
        {
            Stats = stats;
            Message = message;
            Drop = drop;
            Boost = boost;
            IncludeSelf = includeSelf;
            Anims = new List<StatusAnimEvent>();
        }
        public StatChangeCheckBase(List<Stat> stats, StringKey message, bool drop, bool boost, bool includeSelf, params StatusAnimEvent[] anims)
        {
            Stats = stats;
            Message = message;
            Drop = drop;
            Boost = boost;
            IncludeSelf = includeSelf;
            Anims = new List<StatusAnimEvent>();
            Anims.AddRange(anims);
        }
        protected StatChangeCheckBase(StatChangeCheckBase other)
        {
            Message = other.Message;
            Stats = new List<Stat>();
            Stats.AddRange(other.Stats);
            Drop = other.Drop;
            Boost = other.Boost;
            IncludeSelf = other.IncludeSelf;
            Anims = new List<StatusAnimEvent>();
            foreach (StatusAnimEvent anim in other.Anims)
                Anims.Add((StatusAnimEvent)anim.Clone());
        }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            if (owner != context.Status)//can't check on self
            {
                StatChangeState statChange = context.Status.StatusStates.GetWithDefault<StatChangeState>();
                if (statChange != null && (context.User != context.Target || IncludeSelf))
                {
                    bool block = false;
                    int delta = context.Status.StatusStates.GetWithDefault<StackState>().Stack;
                    if (delta < 0 && Drop || delta > 0 && Boost)
                    {
                        if (Stats.Count == 0)
                            block = true;
                        else
                        {
                            foreach (Stat statType in Stats)
                            {
                                if (statType == statChange.ChangeStat)
                                    block = true;
                            }
                        }
                    }
                    if (block)
                        yield return CoroutineManager.Instance.StartCoroutine(BlockEffect(owner, ownerChar, context));
                }
            }
            yield break;
        }

        protected abstract IEnumerator<YieldInstruction> BlockEffect(GameEventOwner owner, Character ownerChar, StatusCheckContext context);
    }

    [Serializable]
    public class StatChangeCheck : StatChangeCheckBase
    {
        public StatChangeCheck()
        {
        }
        public StatChangeCheck(List<Stat> stats, StringKey message, bool drop, bool boost, bool includeSelf)
            : base(stats, message, drop, boost, includeSelf)
        {
        }

        public StatChangeCheck(List<Stat> stats, StringKey message, bool drop, bool boost, bool includeSelf, params StatusAnimEvent[] anims)
            : base(stats, message, drop, boost, includeSelf, anims)
        {
        }
        protected StatChangeCheck(StatChangeCheck other)
            : base(other)
        {
        }
        public override GameEvent Clone() { return new StatChangeCheck(this); }

        protected override IEnumerator<YieldInstruction> BlockEffect(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            if (context.msg && Message.IsValid())
            {
                StatChangeState statChange = context.Status.StatusStates.GetWithDefault<StatChangeState>();
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), context.Target.GetDisplayName(false), owner.GetDisplayName(), statChange.ChangeStat.ToLocal()));

                foreach (StatusAnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

            }
            context.CancelState.Cancel = true;
        }
    }


    [Serializable]
    public class StatChangeReflect : StatChangeCheckBase
    {
        public StatChangeReflect()
        {
        }
        public StatChangeReflect(List<Stat> stats, StringKey message, bool drop, bool boost, bool includeSelf)
            : base(stats, message, drop, boost, includeSelf)
        {
        }

        public StatChangeReflect(List<Stat> stats, StringKey message, bool drop, bool boost, bool includeSelf, params StatusAnimEvent[] anims)
            : base(stats, message, drop, boost, includeSelf, anims)
        {
        }
        protected StatChangeReflect(StatChangeReflect other)
            : base(other)
        {
        }
        public override GameEvent Clone() { return new StatChangeReflect(this); }

        protected override IEnumerator<YieldInstruction> BlockEffect(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            if (context.User != null)
            {
                if (context.msg && Message.IsValid())
                {
                    StatChangeState statChange = context.Status.StatusStates.GetWithDefault<StatChangeState>();
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), context.Target.GetDisplayName(false), owner.GetDisplayName(), statChange.ChangeStat.ToLocal()));

                    foreach (StatusAnimEvent anim in Anims)
                        yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                }
                yield return CoroutineManager.Instance.StartCoroutine(context.User.AddStatusEffect(null, context.Status, null, false, true));
                context.CancelState.Cancel = true;
            }
        }
    }


    [Serializable]
    public class PreventAnyStatusCheck : StatusGivenEvent
    {
        public StringKey Message;
        public List<StatusAnimEvent> Anims;

        public PreventAnyStatusCheck()
        {
            Anims = new List<StatusAnimEvent>();
        }
        public PreventAnyStatusCheck(StringKey message, params StatusAnimEvent[] anims)
        {
            Message = message;
            Anims = new List<StatusAnimEvent>();
            Anims.AddRange(anims);
        }
        protected PreventAnyStatusCheck(PreventAnyStatusCheck other)
        {
            Message = other.Message;
            Anims = new List<StatusAnimEvent>();
            foreach (StatusAnimEvent anim in other.Anims)
                Anims.Add((StatusAnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new PreventAnyStatusCheck(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            if (owner != context.Status)//can't check on self
            {
                string index = ((StatusEffect)owner).StatusStates.GetWithDefault<IDState>().ID;
                if (index == context.Status.ID)
                {
                    if (context.msg)
                    {
                        DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), context.Target.GetDisplayName(false), DataManager.Instance.GetStatus(index).GetColoredName()));

                        foreach (StatusAnimEvent anim in Anims)
                            yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                    }
                    context.CancelState.Cancel = true;
                }
            }
            yield break;
        }
    }




    [Serializable]
    public class AddStatusContextStateEvent : StatusGivenEvent
    {
        public ContextState AddedState;

        public AddStatusContextStateEvent() { }
        public AddStatusContextStateEvent(ContextState state) { AddedState = state; }
        protected AddStatusContextStateEvent(AddStatusContextStateEvent other)
        {
            AddedState = other.AddedState.Clone<ContextState>();
        }
        public override GameEvent Clone() { return new AddStatusContextStateEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            context.ContextStates.Set(AddedState.Clone<ContextState>());
            yield break;
        }
    }

    [Serializable]
    public class ExceptionStatusContextEvent : StatusGivenEvent
    {
        [StringTypeConstraint(1, typeof(ContextState))]
        public List<FlagType> States;
        public StatusGivenEvent BaseEvent;

        public ExceptionStatusContextEvent() { States = new List<FlagType>(); }
        public ExceptionStatusContextEvent(Type state, StatusGivenEvent baseEffect) : this() { States.Add(new FlagType(state)); BaseEvent = baseEffect; }
        protected ExceptionStatusContextEvent(ExceptionStatusContextEvent other) : this()
        {
            States.AddRange(other.States);
            BaseEvent = (StatusGivenEvent)other.BaseEvent.Clone();
        }
        public override GameEvent Clone() { return new ExceptionStatusContextEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            bool hasState = false;
            foreach (FlagType state in States)
            {
                if (context.ContextStates.Contains(state.FullType))
                    hasState = true;
            }
            if (!hasState)
                yield return CoroutineManager.Instance.StartCoroutine(BaseEvent.Apply(owner, ownerChar, context));
        }

    }


    [Serializable]
    public class ExceptInfiltratorStatusEvent : StatusGivenEvent
    {
        public StatusGivenEvent BaseEvent;
        public bool ExceptionMsg;

        public ExceptInfiltratorStatusEvent() { }
        public ExceptInfiltratorStatusEvent(bool exceptionMsg, StatusGivenEvent baseEffect) { BaseEvent = baseEffect; ExceptionMsg = exceptionMsg; }
        protected ExceptInfiltratorStatusEvent(ExceptInfiltratorStatusEvent other)
        {
            BaseEvent = (StatusGivenEvent)other.BaseEvent.Clone();
            ExceptionMsg = other.ExceptionMsg;
        }
        public override GameEvent Clone() { return new ExceptInfiltratorStatusEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            Infiltrator state = context.ContextStates.GetWithDefault<Infiltrator>();
            if (state == null)
                yield return CoroutineManager.Instance.StartCoroutine(BaseEvent.Apply(owner, ownerChar, context));
            else if (ExceptionMsg && state.Msg.IsValid())
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(state.Msg.ToLocal(), context.User.GetDisplayName(false), owner.GetDisplayName()));
        }
    }



    [Serializable]
    public class ReplaceMajorStatusEvent : StatusGivenEvent
    {
        public ReplaceMajorStatusEvent() { }
        public override GameEvent Clone() { return new ReplaceMajorStatusEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            if (context.Status == owner)
                yield break;

            if (context.Status.StatusStates.GetWithDefault<MajorStatusState>() != null)
                yield return CoroutineManager.Instance.StartCoroutine(context.Target.RemoveStatusEffect(((StatusEffect)owner).ID, false));
        }
    }

    [Serializable]
    public class StatusBattleLogEvent : StatusGivenEvent
    {
        
        public StringKey Message;
        public bool Delay;

        public StatusBattleLogEvent() { }
        public StatusBattleLogEvent(StringKey message) : this(message, false) { }
        public StatusBattleLogEvent(StringKey message, bool delay)
        {
            Message = message;
            Delay = delay;
        }
        protected StatusBattleLogEvent(StatusBattleLogEvent other)
        {
            Message = other.Message;
            Delay = other.Delay;
        }
        public override GameEvent Clone() { return new StatusBattleLogEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            if (context.Status != owner)
                yield break;

            if (context.msg)
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), context.Target.GetDisplayName(false), owner.GetDisplayName()));
                if (Delay)
                    yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(10));
            }
        }
    }


    [Serializable]
    public class PerishStatusEvent : StatusGivenEvent
    {
        public StringKey Message;
        public bool Delay;
        public List<StatusAnimEvent> Anims;

        public PerishStatusEvent()
        {
            Anims = new List<StatusAnimEvent>();
        }
        public PerishStatusEvent(StringKey message, bool delay, params StatusAnimEvent[] anims)
        {
            Message = message;
            Delay = delay;
            Anims = new List<StatusAnimEvent>();
            Anims.AddRange(anims);
        }
        protected PerishStatusEvent(PerishStatusEvent other)
        {
            Message = other.Message;
            Delay = other.Delay;
            Anims = new List<StatusAnimEvent>();
            foreach (StatusAnimEvent anim in other.Anims)
                Anims.Add((StatusAnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new PerishStatusEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            if (context.Status != owner)
                yield break;
            if (context.User.Dead)
                yield break;
            CountDownState counter = ((StatusEffect)owner).StatusStates.GetWithDefault<CountDownState>();

            DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), context.Target.GetDisplayName(false), counter.Counter));
            if (Delay)
                yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(10));

            if (counter.Counter <= 0)
            {
                yield return CoroutineManager.Instance.StartCoroutine(context.Target.RemoveStatusEffect(((StatusEffect)owner).ID, false));

                foreach (StatusAnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));
                GameManager.Instance.BattleSE("DUN_Hit_Super_Effective");
                yield return CoroutineManager.Instance.StartCoroutine(context.Target.InflictDamage(-1));
            }
        }
    }

    [Serializable]
    public class WeatherNeededStatusEvent : StatusGivenEvent
    {
        [JsonConverter(typeof(MapStatusConverter))]
        [DataType(0, DataManager.DataType.MapStatus, false)]
        public string WeatherID;
        public List<StatusGivenEvent> BaseEvents;

        public WeatherNeededStatusEvent() { BaseEvents = new List<StatusGivenEvent>(); WeatherID = ""; }
        public WeatherNeededStatusEvent(string id, params StatusGivenEvent[] effects)
            : this()
        {
            WeatherID = id;
            foreach (StatusGivenEvent effect in effects)
                BaseEvents.Add(effect);
        }
        protected WeatherNeededStatusEvent(WeatherNeededStatusEvent other) : this()
        {
            WeatherID = other.WeatherID;
            foreach (StatusGivenEvent battleEffect in other.BaseEvents)
                BaseEvents.Add((StatusGivenEvent)battleEffect.Clone());
        }
        public override GameEvent Clone() { return new WeatherNeededStatusEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            if (ZoneManager.Instance.CurrentMap.Status.ContainsKey(WeatherID))
            {
                foreach (StatusGivenEvent battleEffect in BaseEvents)
                    yield return CoroutineManager.Instance.StartCoroutine(battleEffect.Apply(owner, ownerChar, context));
            }
        }
    }




    [Serializable]
    public class FormeNeededStatusEvent : StatusGivenEvent
    {
        public HashSet<int> Forms;
        public List<StatusGivenEvent> BaseEvents;

        public FormeNeededStatusEvent() { Forms = new HashSet<int>(); BaseEvents = new List<StatusGivenEvent>(); }
        public FormeNeededStatusEvent(StatusGivenEvent effects, params int[] forms)
            : this()
        {
            BaseEvents.Add(effects);
            foreach (int form in forms)
                Forms.Add(form);
        }
        protected FormeNeededStatusEvent(FormeNeededStatusEvent other) : this()
        {
            foreach (int form in other.Forms)
                Forms.Add(form);
            foreach (StatusGivenEvent battleEffect in other.BaseEvents)
                BaseEvents.Add((StatusGivenEvent)battleEffect.Clone());
        }
        public override GameEvent Clone() { return new FormeNeededStatusEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            if (Forms.Contains(ownerChar.CurrentForm.Form))
            {
                foreach (StatusGivenEvent battleEffect in BaseEvents)
                    yield return CoroutineManager.Instance.StartCoroutine(battleEffect.Apply(owner, ownerChar, context));
            }
        }
    }


    [Serializable]
    public class StatusAnimEvent : StatusGivenEvent
    {
        public FiniteEmitter Emitter;
        [Sound(0)]
        public string Sound;
        public int Delay;
        public bool NeedSelf;

        public StatusAnimEvent()
        {
            Emitter = new EmptyFiniteEmitter();
        }
        public StatusAnimEvent(FiniteEmitter emitter, string sound, int delay) : this(emitter, sound, delay, false) { }
        public StatusAnimEvent(FiniteEmitter emitter, string sound, int delay, bool needSelf)
        {
            Emitter = emitter;
            Sound = sound;
            Delay = delay;
            NeedSelf = needSelf;
        }
        protected StatusAnimEvent(StatusAnimEvent other)
        {
            Emitter = (FiniteEmitter)other.Emitter.Clone();
            Sound = other.Sound;
            Delay = other.Delay;
            NeedSelf = other.NeedSelf;
        }
        public override GameEvent Clone() { return new StatusAnimEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            if (NeedSelf && context.Status != owner || !context.msg)
                yield break;

            GameManager.Instance.BattleSE(Sound);
            if (!context.Target.Unidentifiable)
            {
                FiniteEmitter endEmitter = (FiniteEmitter)Emitter.Clone();
                endEmitter.SetupEmit(context.Target.MapLoc, context.Target.MapLoc, context.Target.CharDir);
                DungeonScene.Instance.CreateAnim(endEmitter, DrawLayer.NoDraw);
            }
            yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(Delay));
        }
    }



    [Serializable]
    public class StatusCharAnimEvent : StatusGivenEvent
    {
        public CharAnimData CharAnim;

        public StatusCharAnimEvent()
        {
            CharAnim = new CharAnimFrameType();
        }

        public StatusCharAnimEvent(CharAnimData charAnim)
        {
            CharAnim = charAnim;
        }
        protected StatusCharAnimEvent(StatusCharAnimEvent other)
        {
            CharAnim = other.CharAnim;
        }
        public override GameEvent Clone() { return new StatusCharAnimEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            if (context.Status != owner)
                yield break;

            StaticCharAnimation anim = CharAnim.GetCharAnim();
            anim.CharLoc = context.Target.CharLoc;
            anim.CharDir = context.Target.CharDir;

            yield return CoroutineManager.Instance.StartCoroutine(context.Target.StartAnim(anim));
        }
    }

    [Serializable]
    public class StatusEmoteEvent : StatusGivenEvent
    {
        public EmoteFX Emote;
        public bool NeedSelf;

        public StatusEmoteEvent()
        { }
        public StatusEmoteEvent(EmoteFX emote) : this(emote, false) { }
        public StatusEmoteEvent(EmoteFX emote, bool needSelf)
        {
            Emote = emote;
            NeedSelf = needSelf;
        }
        protected StatusEmoteEvent(StatusEmoteEvent other)
        {
            Emote = new EmoteFX(other.Emote);
            NeedSelf = other.NeedSelf;
        }
        public override GameEvent Clone() { return new StatusEmoteEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            if (NeedSelf && context.Status != owner || !context.msg)
                yield break;

            yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.ProcessEmoteFX(context.Target, Emote));
        }
    }


    [Serializable]
    public class RemoveTargetStatusEvent : StatusGivenEvent
    {
        //destiny knot logic
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string StatusID;
        public bool ShowMessage;

        public RemoveTargetStatusEvent() { StatusID = ""; }
        public RemoveTargetStatusEvent(string statusID, bool showMessage)
        {
            StatusID = statusID;
            ShowMessage = showMessage;
        }
        protected RemoveTargetStatusEvent(RemoveTargetStatusEvent other)
        {
            StatusID = other.StatusID;
            ShowMessage = other.ShowMessage;
        }
        public override GameEvent Clone() { return new RemoveTargetStatusEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            if (context.Status != owner)
                yield break;

            if (context.Status.TargetChar != null)
                yield return CoroutineManager.Instance.StartCoroutine(context.Status.TargetChar.RemoveStatusEffect(StatusID, false));
        }
    }


    [Serializable]
    public class TargetedBattleLogEvent : StatusGivenEvent
    {
        public StringKey Message;
        public bool Delay;

        public TargetedBattleLogEvent() { }
        public TargetedBattleLogEvent(StringKey message) : this(message, false) { }
        public TargetedBattleLogEvent(StringKey message, bool delay)
        {
            Message = message;
            Delay = delay;
        }
        protected TargetedBattleLogEvent(TargetedBattleLogEvent other)
        {
            Message = other.Message;
            Delay = other.Delay;
        }
        public override GameEvent Clone() { return new TargetedBattleLogEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            if (owner != context.Status)
                yield break;
            if (context.msg && ((StatusEffect)owner).TargetChar != null)
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), context.Target.GetDisplayName(false), ((StatusEffect)owner).TargetChar.GetDisplayName(false)));
                if (Delay)
                    yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(10));
            }
        }
    }
    [Serializable]
    public class StatusLogCategoryEvent : StatusGivenEvent
    {

        public StringKey Message;
        public bool Delay;

        public StatusLogCategoryEvent() { }
        public StatusLogCategoryEvent(StringKey message) : this(message, false) { }
        public StatusLogCategoryEvent(StringKey message, bool delay)
        {
            Message = message;
            Delay = delay;
        }
        protected StatusLogCategoryEvent(StatusLogCategoryEvent other)
        {
            Message = other.Message;
            Delay = other.Delay;
        }
        public override GameEvent Clone() { return new StatusLogCategoryEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            if (context.Status != owner)
                yield break;
            if (context.msg)
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), context.Target.GetDisplayName(false), ((StatusEffect)owner).StatusStates.GetWithDefault<CategoryState>().Category.ToLocal()));
                if (Delay)
                    yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(10));
            }
            
        }
    }
    [Serializable]
    public class StatusLogElementEvent : StatusGivenEvent
    {

        public StringKey Message;
        public bool Delay;

        public StatusLogElementEvent() { }
        public StatusLogElementEvent(StringKey message) : this(message, false) { }
        public StatusLogElementEvent(StringKey message, bool delay)
        {
            Message = message;
            Delay = delay;
        }
        protected StatusLogElementEvent(StatusLogElementEvent other)
        {
            Message = other.Message;
            Delay = other.Delay;
        }
        public override GameEvent Clone() { return new StatusLogElementEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            if (context.Status != owner)
                yield break;
            
            if (context.msg)
            {
                string elementIndex = ((StatusEffect)owner).StatusStates.GetWithDefault<ElementState>().Element;
                ElementData elementData = DataManager.Instance.GetElement(elementIndex);
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), context.Target.GetDisplayName(false), elementData.GetIconName()));
                if (Delay)
                    yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(10));
            }
            
        }
    }
    [Serializable]
    public class StatusLogStatusEvent : StatusGivenEvent
    {

        public StringKey Message;
        public bool Delay;

        public StatusLogStatusEvent() { }
        public StatusLogStatusEvent(StringKey message) : this(message, false) { }
        public StatusLogStatusEvent(StringKey message, bool delay)
        {
            Message = message;
            Delay = delay;
        }
        protected StatusLogStatusEvent(StatusLogStatusEvent other)
        {
            Message = other.Message;
            Delay = other.Delay;
        }
        public override GameEvent Clone() { return new StatusLogStatusEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            if (context.Status != owner)
                yield break;
            
            if (context.msg)
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), context.Target.GetDisplayName(false), DataManager.Instance.GetStatus(((StatusEffect)owner).StatusStates.GetWithDefault<IDState>().ID).GetColoredName()));
                if (Delay)
                    yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(10));
            }
            
        }
    }
    [Serializable]
    public class StatusLogMoveSlotEvent : StatusGivenEvent
    {

        public StringKey Message;
        public bool Delay;

        public StatusLogMoveSlotEvent() { }
        public StatusLogMoveSlotEvent(StringKey message) : this(message, false) { }
        public StatusLogMoveSlotEvent(StringKey message, bool delay)
        {
            Message = message;
            Delay = delay;
        }
        protected StatusLogMoveSlotEvent(StatusLogMoveSlotEvent other)
        {
            Message = other.Message;
            Delay = other.Delay;
        }
        public override GameEvent Clone() { return new StatusLogMoveSlotEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            if (context.Status != owner)
                yield break;

            if (context.msg)
            {
                int slot = ((StatusEffect)owner).StatusStates.GetWithDefault<SlotState>().Slot;
                SkillData entry = DataManager.Instance.GetSkill(context.Target.Skills[slot].Element.SkillNum);
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), context.Target.GetDisplayName(false), entry.GetIconName()));
                if (Delay)
                    yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(10));
            }

        }
    }
    [Serializable]
    public class StatusLogStackEvent : StatusGivenEvent
    {

        public StringKey Message;
        public bool Delay;

        public StatusLogStackEvent() { }
        public StatusLogStackEvent(StringKey message) : this(message, false) { }
        public StatusLogStackEvent(StringKey message, bool delay)
        {
            Message = message;
            Delay = delay;
        }
        protected StatusLogStackEvent(StatusLogStackEvent other)
        {
            Message = other.Message;
            Delay = other.Delay;
        }
        public override GameEvent Clone() { return new StatusLogStackEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            if (context.Status != owner)
                yield break;

            if (context.msg)
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), context.Target.GetDisplayName(false), ((StatusEffect)owner).StatusStates.GetWithDefault<StackState>().Stack));
                if (Delay)
                    yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(10));
            }

        }
    }
    [Serializable]
    public class ReportSpeedEvent : StatusGivenEvent
    {
        public bool Delay;
        
        public ReportSpeedEvent() { }
        public ReportSpeedEvent(bool delay)
        {
            Delay = delay;
        }
        protected ReportSpeedEvent(ReportSpeedEvent other)
        {
            Delay = other.Delay;
        }
        public override GameEvent Clone() { return new ReportSpeedEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            if (context.Status != owner)
                yield break;

            if (context.msg)
            {
                string speedString = new StringKey("MSG_SPEED_NORMAL").ToLocal();
                switch (context.Target.MovementSpeed)
                {
                    case -3:
                        speedString = new StringKey("MSG_SPEED_FOURTH").ToLocal();
                        break;
                    case -2:
                        speedString = new StringKey("MSG_SPEED_THIRD").ToLocal();
                        break;
                    case -1:
                        speedString = new StringKey("MSG_SPEED_HALF").ToLocal();
                        break;
                    case 1:
                        speedString = new StringKey("MSG_SPEED_DOUBLE").ToLocal();
                        break;
                    case 2:
                        speedString = new StringKey("MSG_SPEED_TRIPLE").ToLocal();
                        break;
                    case 3:
                        speedString = new StringKey("MSG_SPEED_QUADRUPLE").ToLocal();
                        break;
                }
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(speedString, context.Target.GetDisplayName(false)));
                if (Delay)
                    yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(20));
            }
        }
    }
    [Serializable]
    public class ReportStatEvent : StatusGivenEvent
    {
        public const int MAX_BUFF = 6;
        public const int MIN_BUFF = -6;

        public Stat Stat;

        public ReportStatEvent() { }
        public ReportStatEvent(Stat stat)
        {
            Stat = stat;
        }
        protected ReportStatEvent(ReportStatEvent other)
        {
            Stat = other.Stat;
        }
        public override GameEvent Clone() { return new ReportStatEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            if (context.Status != owner)
                yield break;

            if (context.msg)
            {
                string changeString = "";
                int counter = ((StatusEffect)owner).StatusStates.GetWithDefault<StackState>().Stack;
                if (counter == MIN_BUFF)
                    changeString = new StringKey("MSG_BUFF_MIN").ToLocal();
                else if (counter == MAX_BUFF)
                    changeString = new StringKey("MSG_BUFF_MAX").ToLocal();
                else
                {
                    int boost = context.StackDiff;
                    if (boost == 0)
                        changeString = new StringKey("MSG_BUFF_UNCHANGED").ToLocal();
                    else if (boost == 1)
                        changeString = new StringKey("MSG_BUFF_PLUS_1").ToLocal();
                    else if (boost == -1)
                        changeString = new StringKey("MSG_BUFF_MINUS_1").ToLocal();
                    else if (boost == 2)
                        changeString = new StringKey("MSG_BUFF_PLUS_2").ToLocal();
                    else if (boost == -2)
                        changeString = new StringKey("MSG_BUFF_MINUS_2").ToLocal();
                    else if (boost > 2)
                        changeString = new StringKey("MSG_BUFF_PLUS_3").ToLocal();
                    else if (boost < -2)
                        changeString = new StringKey("MSG_BUFF_MINUS_3").ToLocal();
                }
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(changeString, context.Target.GetDisplayName(false), Stat.ToLocal()));
                yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(20));
            }
        }
    }
    [Serializable]
    public class ReportStatRemoveEvent : StatusGivenEvent
    {
        public bool Delay;
        public Stat Stat;

        public ReportStatRemoveEvent() { }
        public ReportStatRemoveEvent(Stat stat, bool delay)
        {
            Stat = stat;
            Delay = delay;
        }
        protected ReportStatRemoveEvent(ReportStatRemoveEvent other)
        {
            Stat = other.Stat;
            Delay = other.Delay;
        }
        public override GameEvent Clone() { return new ReportStatRemoveEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            if (context.Status != owner)
                yield break;

            if (context.msg)
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_BUFF_REVERT").ToLocal(), context.Target.GetDisplayName(false), Stat.ToLocal()));
                if (Delay)
                    yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(10));
            }
        }
    }
    [Serializable]
    public class ShowStatChangeEvent : StatusGivenEvent
    {
        [Sound(0)]
        public string StatUpSound;
        [Sound(0)]
        public string StatDownSound;
        public string StatCircle;
        public string StatLines;

        public ShowStatChangeEvent() { }
        public ShowStatChangeEvent(string statUp, string statDown, string statCircle, string statLines)
        {
            StatUpSound = statUp;
            StatDownSound = statDown;
            StatCircle = statCircle;
            StatLines = statLines;
        }
        protected ShowStatChangeEvent(ShowStatChangeEvent other)
        {
            StatUpSound = other.StatUpSound;
            StatDownSound = other.StatDownSound;
            StatCircle = other.StatCircle;
            StatLines = other.StatLines;
        }
        public override GameEvent Clone() { return new ShowStatChangeEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            if (context.Status != owner)
                yield break;

            if (context.msg)
            {
                int boost = context.StackDiff;
                if (boost != 0)
                {
                    SqueezedAreaEmitter emitter;

                    if (boost > 0)
                    {
                        GameManager.Instance.BattleSE(StatUpSound);

                        if (!context.Target.Unidentifiable)
                        {
                            StaticAnim anim = new StaticAnim(new AnimData(StatCircle, 3));
                            anim.SetupEmitted(context.Target.MapLoc, -6, context.Target.CharDir);
                            DungeonScene.Instance.CreateAnim(anim, DrawLayer.Bottom);
                        }

                        emitter = new SqueezedAreaEmitter(new AnimData(StatLines, 2, Dir8.Up));
                    }
                    else
                    {
                        GameManager.Instance.BattleSE(StatDownSound);
                        emitter = new SqueezedAreaEmitter(new AnimData(StatLines, 2, Dir8.Down));
                    }

                    if (!context.Target.Unidentifiable)
                    {
                        emitter.Bursts = 3;
                        emitter.ParticlesPerBurst = 2;
                        emitter.BurstTime = 6;
                        emitter.Range = GraphicsManager.TileSize;
                        emitter.StartHeight = 0;
                        emitter.HeightSpeed = 6;
                        emitter.SetupEmit(context.Target.MapLoc, context.Target.MapLoc, context.Target.CharDir);

                        DungeonScene.Instance.CreateAnim(emitter, DrawLayer.NoDraw);
                    }
                }
            }
            yield break;
        }
    }
    [Serializable]
    public class RemoveStackZeroEvent : StatusGivenEvent
    {
        public override GameEvent Clone() { return new RemoveStackZeroEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            if (context.Status != owner)
                yield break;

            if (((StatusEffect)owner).StatusStates.GetWithDefault<StackState>().Stack == 0)
                yield return CoroutineManager.Instance.StartCoroutine(context.Target.RemoveStatusEffect(owner.GetID(), false));
        }
    }

    [Serializable]
    public class StatusSyncEvent : StatusGivenEvent
    {
        //destiny knot logic
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string StatusID;
        public StatusSyncEvent() { StatusID = ""; }
        public StatusSyncEvent(string statusID)
        {
            StatusID = statusID;
        }
        protected StatusSyncEvent(StatusSyncEvent other)
        {
            StatusID = other.StatusID;
        }
        public override GameEvent Clone() { return new StatusSyncEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            if (owner != context.Status)//can't check on self
            {
                if (context.Status.ID == StatusID && context.User != null && context.User != context.Target)
                {
                    StatusEffect newStatus = context.Status.Clone();
                    if (context.Status.TargetChar != null)
                    {
                        if (context.Status.TargetChar == context.User)
                            newStatus.TargetChar = context.Target;
                        else if (context.Status.TargetChar == context.Target)
                            newStatus.TargetChar = context.User;
                    }
                    yield return CoroutineManager.Instance.StartCoroutine(context.User.AddStatusEffect(newStatus));
                }
            }
        }
    }


    [Serializable]
    public class StateStatusShareEvent : StatusGivenEvent
    {
        [StringTypeConstraint(1, typeof(StatusState))]
        public List<FlagType> States;
        public int Range;
        public StringKey Message;


        public List<StatusAnimEvent> Anims;

        public StateStatusShareEvent()
        {
            States = new List<FlagType>();
            Anims = new List<StatusAnimEvent>();
        }
        public StateStatusShareEvent(Type state, int range, StringKey msg, params StatusAnimEvent[] anims) : this()
        {
            States.Add(new FlagType(state));
            Range = range;
            Message = msg;

            Anims.AddRange(anims);
        }
        protected StateStatusShareEvent(StateStatusShareEvent other) : this()
        {
            States.AddRange(other.States);
            Range = other.Range;
            Message = other.Message;

            foreach (StatusAnimEvent anim in other.Anims)
                Anims.Add((StatusAnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new StateStatusShareEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            if (owner != context.Status)//can't check on self
            {
                bool hasState = false;
                foreach (FlagType state in States)
                {
                    if (context.Status.StatusStates.Contains(state.FullType))
                        hasState = true;
                }
                if (hasState)
                {
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), ownerChar.GetDisplayName(false), owner.GetDisplayName()));

                    foreach (StatusAnimEvent anim in Anims)
                        yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));
                    
                    foreach (Character character in ZoneManager.Instance.CurrentMap.GetCharsInFillRect(context.Target.CharLoc, Rect.FromPointRadius(context.Target.CharLoc, Range)))
                    {
                        if (!character.Dead && DungeonScene.Instance.GetMatchup(context.Target, character) == Alignment.Foe)
                        {
                            StatusEffect newStatus = context.Status.Clone();
                            if (context.Status.TargetChar != null)
                            {
                                if (context.Status.TargetChar == character)
                                    newStatus.TargetChar = context.Target;
                                else if (context.Status.TargetChar == context.Target)
                                    newStatus.TargetChar = character;
                            }
                            yield return CoroutineManager.Instance.StartCoroutine(character.AddStatusEffect(newStatus));
                        }
                    }
                }
            }
        }
    }

    [Serializable]
    public class StateStatusSyncEvent : StatusGivenEvent
    {
        [StringTypeConstraint(1, typeof(StatusState))]
        public List<FlagType> States;
        public StringKey Message;


        public List<StatusAnimEvent> Anims;

        public StateStatusSyncEvent()
        {
            States = new List<FlagType>();
            Anims = new List<StatusAnimEvent>();
        }
        public StateStatusSyncEvent(Type state, StringKey msg, params StatusAnimEvent[] anims) : this()
        {
            States.Add(new FlagType(state));
            Message = msg;

            Anims.AddRange(anims);
        }
        protected StateStatusSyncEvent(StateStatusSyncEvent other) : this()
        {
            States.AddRange(other.States);
            Message = other.Message;

            foreach (StatusAnimEvent anim in other.Anims)
                Anims.Add((StatusAnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new StateStatusSyncEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            if (owner != context.Status)//can't check on self
            {
                bool hasState = false;
                foreach (FlagType state in States)
                {
                    if (context.Status.StatusStates.Contains(state.FullType))
                        hasState = true;
                }
                if (context.User != null && context.User != context.Target && hasState)
                {
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), ownerChar.GetDisplayName(false), owner.GetDisplayName()));

                    foreach (StatusAnimEvent anim in Anims)
                        yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                    StatusEffect newStatus = context.Status.Clone();
                    if (context.Status.TargetChar != null)
                    {
                        if (context.Status.TargetChar == context.User)
                            newStatus.TargetChar = context.Target;
                        else if (context.Status.TargetChar == context.Target)
                            newStatus.TargetChar = context.User;
                    }
                    yield return CoroutineManager.Instance.StartCoroutine(context.User.AddStatusEffect(newStatus));
                }
            }
        }
    }

    [Serializable]
    public class StatDropSyncEvent : StatusGivenEvent
    {
        [StringTypeConstraint(1, typeof(StatusState))]
        public StringKey Message;


        public List<StatusAnimEvent> Anims;

        public StatDropSyncEvent()
        {
            Anims = new List<StatusAnimEvent>();
        }
        public StatDropSyncEvent(StringKey msg, params StatusAnimEvent[] anims) : this()
        {
            Message = msg;

            Anims.AddRange(anims);
        }
        protected StatDropSyncEvent(StatDropSyncEvent other) : this()
        {
            Message = other.Message;

            foreach (StatusAnimEvent anim in other.Anims)
                Anims.Add((StatusAnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new StatDropSyncEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            if (owner != context.Status)//can't check on self
            {

                if (context.User != null && context.User != context.Target && context.Status.StatusStates.Contains<StatChangeState>() && context.StackDiff < 0)
                {
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), ownerChar.GetDisplayName(false), owner.GetDisplayName()));

                    foreach (StatusAnimEvent anim in Anims)
                        yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                    StatusEffect newStatus = context.Status.Clone();
                    StackState stack = newStatus.StatusStates.GetWithDefault<StackState>();
                    stack.Stack = context.StackDiff;
                    yield return CoroutineManager.Instance.StartCoroutine(context.User.AddStatusEffect(newStatus));
                }
            }
        }
    }


    [Serializable]
    public class StatusResponseEvent : StatusGivenEvent
    {
        //for steadfast, etc.
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string StatusID;
        public SingleCharEvent BaseEvent;
        
        public StatusResponseEvent() { StatusID = ""; }
        public StatusResponseEvent(string statusID, SingleCharEvent baseEffect)
        {
            StatusID = statusID;
            BaseEvent = baseEffect;
        }
        protected StatusResponseEvent(StatusResponseEvent other)
        {
            StatusID = other.StatusID;
            BaseEvent = other.BaseEvent;
        }
        public override GameEvent Clone() { return new StatusResponseEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            if (owner != context.Status)//can't check on self
            {
                if (context.Status.ID == StatusID)
                {
                    SingleCharContext singleContext = new SingleCharContext(context.Target);
                    yield return CoroutineManager.Instance.StartCoroutine(BaseEvent.Apply(owner, ownerChar, singleContext));
                }
            }
        }
    }

    [Serializable]
    public class StatDropResponseEvent : StatusGivenEvent
    {
        public SingleCharEvent BaseEvent;

        public StatDropResponseEvent() { }
        public StatDropResponseEvent(SingleCharEvent baseEffect)
        {
            BaseEvent = baseEffect;
        }
        protected StatDropResponseEvent(StatDropResponseEvent other)
        {
            BaseEvent = other.BaseEvent;
        }
        public override GameEvent Clone() { return new StatDropResponseEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            if (owner != context.Status)//can't check on self
            {
                if (context.Status.StatusStates.Contains<StatChangeState>())
                {
                    if (context.StackDiff < 0)
                    {
                        SingleCharContext singleContext = new SingleCharContext(context.Target);
                        DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_STAT_DROP_TRIGGER").ToLocal(), ownerChar.GetDisplayName(false), owner.GetDisplayName()));
                        yield return CoroutineManager.Instance.StartCoroutine(BaseEvent.Apply(owner, ownerChar, singleContext));
                    }
                }
            }
        }
    }




    [Serializable]
    public abstract class ShareEquipStatusEvent : StatusGivenEvent
    {
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, StatusCheckContext context)
        {
            if (!String.IsNullOrEmpty(ownerChar.EquippedItem.ID))
            {
                ItemData entry = (ItemData)ownerChar.EquippedItem.GetData();
                if (CheckEquipPassValidityEvent.CanItemEffectBePassed(entry))
                {
                    foreach (var effect in GetEvents(entry))
                        yield return CoroutineManager.Instance.StartCoroutine(effect.Value.Apply(owner, ownerChar, context));
                }
            }
        }

        protected abstract PriorityList<StatusGivenEvent> GetEvents(ItemData entry);
    }

    [Serializable]
    public class ShareBeforeStatusAddsEvent : ShareEquipStatusEvent
    {
        public override GameEvent Clone() { return new ShareBeforeStatusAddsEvent(); }

        protected override PriorityList<StatusGivenEvent> GetEvents(ItemData entry) => entry.BeforeStatusAdds;
    }

    [Serializable]
    public class ShareBeforeStatusAddingsEvent : ShareEquipStatusEvent
    {
        public override GameEvent Clone() { return new ShareBeforeStatusAddingsEvent(); }

        protected override PriorityList<StatusGivenEvent> GetEvents(ItemData entry) => entry.BeforeStatusAddings;
    }

    [Serializable]
    public class ShareOnStatusAddsEvent : ShareEquipStatusEvent
    {
        public override GameEvent Clone() { return new ShareOnStatusAddsEvent(); }

        protected override PriorityList<StatusGivenEvent> GetEvents(ItemData entry) => entry.OnStatusAdds;
    }

    [Serializable]
    public class ShareOnStatusRemovesEvent : ShareEquipStatusEvent
    {
        public override GameEvent Clone() { return new ShareOnStatusRemovesEvent(); }

        protected override PriorityList<StatusGivenEvent> GetEvents(ItemData entry) => entry.OnStatusRemoves;
    }
}
