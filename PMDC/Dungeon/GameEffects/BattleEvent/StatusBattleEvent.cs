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
    // Battle effects that apply or remove statuses

    /// <summary>
    /// Event that applies a status to the character
    /// </summary>
    [Serializable]
    public class StatusBattleEvent : BattleEvent
    {
        /// <summary>
        /// The status to apply
        /// </summary>
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string StatusID;

        /// <summary>
        /// Whether to affect the target or user 
        /// </summary>
        public bool AffectTarget;

        /// <summary>
        /// Whether the user of the status should be treated as the target 
        /// </summary>
        public bool SelfInflicted;

        /// <summary>
        /// Whether to display a message if the status fails to apply
        /// </summary>
        public bool SilentCheck;

        /// <summary>
        /// Whether to include the user of the status in the context
        /// </summary>
        public bool Anonymous;

        /// <summary>
        /// The message displayed in the dungeon log if the status is triggered
        /// </summary>
        [StringKey(0, true)]
        public StringKey TriggerMsg;

        /// <summary>
        /// The list of battle VFXs played if the condition is met
        /// </summary> 
        public List<BattleAnimEvent> Anims;

        public StatusBattleEvent() { Anims = new List<BattleAnimEvent>(); StatusID = ""; }
        public StatusBattleEvent(string statusID, bool affectTarget, bool silentCheck)
         : this(statusID, affectTarget, silentCheck, false, false) { }
        public StatusBattleEvent(string statusID, bool affectTarget, bool silentCheck, bool selfInflict, bool anonymous)
        {
            StatusID = statusID;
            AffectTarget = affectTarget;
            SelfInflicted = selfInflict;
            SilentCheck = silentCheck;
            Anonymous = anonymous;
            Anims = new List<BattleAnimEvent>();
        }
        public StatusBattleEvent(string statusID, bool affectTarget, bool silentCheck, bool selfInflict, bool anonymous, StringKey trigger, params BattleAnimEvent[] anims)
        {
            StatusID = statusID;
            AffectTarget = affectTarget;
            SelfInflicted = selfInflict;
            SilentCheck = silentCheck;
            Anonymous = anonymous;
            TriggerMsg = trigger;
            Anims = new List<BattleAnimEvent>();
            Anims.AddRange(anims);
        }
        protected StatusBattleEvent(StatusBattleEvent other)
        {
            StatusID = other.StatusID;
            AffectTarget = other.AffectTarget;
            SelfInflicted = other.SelfInflicted;
            SilentCheck = other.SilentCheck;
            Anonymous = other.Anonymous;
            TriggerMsg = other.TriggerMsg;
            Anims = new List<BattleAnimEvent>();
            foreach (BattleAnimEvent anim in other.Anims)
                Anims.Add((BattleAnimEvent)anim.Clone());

        }
        public override GameEvent Clone() { return new StatusBattleEvent(this); }

        protected virtual bool ModStatus(GameEventOwner owner, BattleContext context, Character target, Character origin, StatusEffect status) { return true; }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = (AffectTarget ? context.Target : context.User);
            Character origin = (AffectTarget ? context.User : context.Target);
            if (SelfInflicted)
                origin = target;
            if (target.Dead)
                yield break;

            yield return CoroutineManager.Instance.StartCoroutine(applyStatus(owner, ownerChar, StatusID, target, origin, context, new AbortStatus()));
        }


        protected IEnumerator<YieldInstruction> applyStatus(GameEventOwner owner, Character ownerChar, string statusID, Character target, Character origin, BattleContext context, AbortStatus cancel)
        {
            StatusEffect status = new StatusEffect(statusID);
            status.LoadFromData();
            if (((StatusData)status.GetData()).Targeted)
            {
                if (origin.Dead)
                {
                    cancel.Cancel = true;
                    yield break;
                }
                status.TargetChar = origin;
            }

            if (!ModStatus(owner, context, target, origin, status))
            {
                cancel.Cancel = true;
                yield break;
            }

            if (!TriggerMsg.IsValid())
            {
                foreach (BattleAnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                yield return CoroutineManager.Instance.StartCoroutine(target.AddStatusEffect(Anonymous ? null : origin, status, null, !SilentCheck, true));
            }
            else
            {
                StatusCheckContext statusContext = new StatusCheckContext(Anonymous ? null : origin, target, status, false);

                yield return CoroutineManager.Instance.StartCoroutine(target.BeforeStatusCheck(statusContext));
                if (statusContext.CancelState.Cancel)
                {
                    cancel.Cancel = true;
                    yield break;
                }

                DungeonScene.Instance.LogMsg(Text.FormatGrammar(TriggerMsg.ToLocal(), ownerChar.GetDisplayName(false), owner.GetDisplayName()));
                statusContext.msg = true;

                foreach (BattleAnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                yield return CoroutineManager.Instance.StartCoroutine(target.ExecuteAddStatus(statusContext));
            }
        }
    }

    /// <summary>
    /// Event that adds or substracts the stack amount of a status. 
    /// This is usually used for stat boosts stauses such as attack, defense, evasiveness, etc.
    /// </summary>
    [Serializable]
    public class StatusStackBattleEvent : StatusBattleEvent
    {
        /// <summary>
        /// The amount to add or subtract by
        /// </summary>
        public int Stack;

        public StatusStackBattleEvent() { }
        public StatusStackBattleEvent(string statusID, bool affectTarget, bool silentCheck, int stack) : this(statusID, affectTarget, silentCheck, false, false, stack) { }
        public StatusStackBattleEvent(string statusID, bool affectTarget, bool silentCheck, bool selfInflict, bool anonymous, int stack)
            : base(statusID, affectTarget, silentCheck, selfInflict, anonymous)
        {
            Stack = stack;
        }
        public StatusStackBattleEvent(string statusID, bool affectTarget, bool silentCheck, bool selfInflict, bool anonymous, int stack, StringKey trigger)
            : base(statusID, affectTarget, silentCheck, selfInflict, anonymous, trigger)
        {
            Stack = stack;
        }
        protected StatusStackBattleEvent(StatusStackBattleEvent other)
            : base(other)
        {
            Stack = other.Stack;
        }
        public override GameEvent Clone() { return new StatusStackBattleEvent(this); }

        protected override bool ModStatus(GameEventOwner owner, BattleContext context, Character target, Character origin, StatusEffect status)
        {
            status.StatusStates.Set(new StackState(Stack));
            return true;
        }
    }

    /// <summary>
    /// Event that adds the specified type to the ElementState status state when the status is applied
    /// </summary>
    [Serializable]
    public class StatusElementBattleEvent : StatusBattleEvent
    {
        /// <summary>
        /// The type to add to ElementState
        /// </summary>
        [JsonConverter(typeof(ElementConverter))]
        [DataType(0, DataManager.DataType.Element, false)]
        public string Element;

        public StatusElementBattleEvent() { Element = ""; }
        public StatusElementBattleEvent(string statusID, bool affectTarget, bool silentCheck, string element) : this(statusID, affectTarget, silentCheck, false, false, element) { }
        public StatusElementBattleEvent(string statusID, bool affectTarget, bool silentCheck, bool selfInflict, bool anonymous, string element)
            : base(statusID, affectTarget, silentCheck, selfInflict, anonymous)
        {
            Element = element;
        }
        protected StatusElementBattleEvent(StatusElementBattleEvent other)
            : base(other)
        {
            Element = other.Element;
        }
        public override GameEvent Clone() { return new StatusElementBattleEvent(this); }

        protected override bool ModStatus(GameEventOwner owner, BattleContext context, Character target, Character origin, StatusEffect status)
        {
            status.StatusStates.Set(new ElementState(Element));
            return true;
        }
    }

    /// <summary>
    /// Event that adds status states when the status is applied
    /// </summary>
    [Serializable]
    public class StatusStateBattleEvent : StatusBattleEvent
    {
        public StateCollection<StatusState> States;

        public StatusStateBattleEvent() { States = new StateCollection<StatusState>(); }
        public StatusStateBattleEvent(string statusID, bool affectTarget, bool silentCheck, StateCollection<StatusState> states) : this(statusID, affectTarget, silentCheck, false, false, states) { }
        public StatusStateBattleEvent(string statusID, bool affectTarget, bool silentCheck, bool selfInflict, bool anonymous, StateCollection<StatusState> states) : base(statusID, affectTarget, silentCheck, selfInflict, anonymous)
        {
            States = states;
        }
        protected StatusStateBattleEvent(StatusStateBattleEvent other) : base(other)
        {
            States = other.States.Clone();
        }
        public override GameEvent Clone() { return new StatusStateBattleEvent(this); }

        protected override bool ModStatus(GameEventOwner owner, BattleContext context, Character target, Character origin, StatusEffect status)
        {
            foreach (StatusState state in States)
                status.StatusStates.Set(state.Clone<StatusState>());
            return true;
        }
    }

    /// <summary>
    /// Event that sets the value in the SlotState based on what move slot was used when the status is applied
    /// </summary>
    [Serializable]
    public class StatusItemBattleEvent : StatusBattleEvent
    {
        public StatusItemBattleEvent() { }
        public StatusItemBattleEvent(string statusID, bool affectTarget, bool silentCheck) : base(statusID, affectTarget, silentCheck) { }
        protected StatusItemBattleEvent(StatusItemBattleEvent other) : base(other) { }
        public override GameEvent Clone() { return new StatusItemBattleEvent(this); }

        protected override bool ModStatus(GameEventOwner owner, BattleContext context, Character target, Character origin, StatusEffect status)
        {
            if (context.ActionType == BattleActionType.Item || context.ActionType == BattleActionType.Throw)
            {
                string itemId = context.Item.ID;

                status.StatusStates.GetWithDefault<IDState>().ID = itemId;
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Event that disables a move slot by using the value in the SlotState status state in the specified status when the status is applied
    /// </summary>
    [Serializable]
    public class DisableBattleEvent : StatusBattleEvent
    {
        /// <summary>
        /// The status containing the SlotState status state
        /// </summary>
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string LastSlotStatusID;

        /// <summary>
        /// UNUSED
        /// </summary>
        public bool RandomFallback;

        public DisableBattleEvent() { LastSlotStatusID = ""; }
        public DisableBattleEvent(string statusID, string prevMoveID)
            : this(statusID, prevMoveID, false, false, false) { }
        public DisableBattleEvent(string statusID, string prevMoveID, bool selfInflict, bool anonymous, bool randomFallback)
            : base(statusID, true, false, selfInflict, anonymous)
        {
            LastSlotStatusID = prevMoveID;
            RandomFallback = randomFallback;
        }
        protected DisableBattleEvent(DisableBattleEvent other) : base(other)
        {
            LastSlotStatusID = other.LastSlotStatusID;
            RandomFallback = other.RandomFallback;
        }
        public override GameEvent Clone() { return new DisableBattleEvent(this); }

        protected override bool ModStatus(GameEventOwner owner, BattleContext context, Character target, Character origin, StatusEffect status)
        {
            StatusEffect testStatus = target.GetStatusEffect(LastSlotStatusID);
            int lockedSlot = 0;
            if (testStatus != null)
                lockedSlot = testStatus.StatusStates.GetWithDefault<SlotState>().Slot;
            else
            {
                List<int> possibleSlots = new List<int>();
                //choose an enabled slot
                for (int ii = 0; ii < context.Target.Skills.Count; ii++)
                {
                    if (!String.IsNullOrEmpty(context.Target.Skills[ii].Element.SkillNum))
                    {
                        if (context.Target.Skills[ii].Element.Enabled)
                            possibleSlots.Add(ii);
                    }
                }

                if (possibleSlots.Count > 0)
                    lockedSlot = possibleSlots[DataManager.Instance.Save.Rand.Next(possibleSlots.Count)];
                else
                {
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_DISABLE_FAIL").ToLocal(), target.GetDisplayName(false)));
                    return false;
                }
            }

            //add disable slot based on the last slot used
            status.StatusStates.GetWithDefault<SlotState>().Slot = lockedSlot;
            return true;
        }
    }

    /// <summary>
    /// Event that sets the value in the SlotState based on what move slot was used when the status is applied
    /// </summary>
    [Serializable]
    public class CounterDisableBattleEvent : StatusBattleEvent
    {
        public CounterDisableBattleEvent() { }
        public CounterDisableBattleEvent(string statusID) : base(statusID, false, true, false, false) { }
        public CounterDisableBattleEvent(string statusID, StringKey trigger) : base(statusID, false, true, false, false, trigger) { }
        protected CounterDisableBattleEvent(CounterDisableBattleEvent other) : base(other) { }
        public override GameEvent Clone() { return new CounterDisableBattleEvent(this); }

        protected override bool ModStatus(GameEventOwner owner, BattleContext context, Character target, Character origin, StatusEffect status)
        {
            if (context.ActionType == BattleActionType.Skill && context.UsageSlot > BattleContext.DEFAULT_ATTACK_SLOT && context.UsageSlot < CharData.MAX_SKILL_SLOTS)
            {
                int lockedSlot = context.UsageSlot;

                //add disable slot based on the last slot used
                status.StatusStates.GetWithDefault<SlotState>().Slot = lockedSlot;
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Event that increases the stack amount of a status if the map status is present
    /// This is usually used for stat boosts stauses such as attack, defense, evasiveness, etc.
    /// </summary>
    [Serializable]
    public class WeatherStackEvent : StatusBattleEvent
    {
        /// <summary>
        /// The map status to check for
        /// </summary>
        [JsonConverter(typeof(MapStatusConverter))]
        [DataType(0, DataManager.DataType.MapStatus, false)]
        public string WeatherID;

        public WeatherStackEvent() { WeatherID = ""; }
        public WeatherStackEvent(string statusID, bool affectTarget, bool silentCheck, string weatherID) : base(statusID, affectTarget, silentCheck, false, false)
        {
            WeatherID = weatherID;
        }
        protected WeatherStackEvent(WeatherStackEvent other) : base(other)
        {
            WeatherID = other.WeatherID;
        }
        public override GameEvent Clone() { return new WeatherStackEvent(this); }

        protected override bool ModStatus(GameEventOwner owner, BattleContext context, Character target, Character origin, StatusEffect status)
        {
            int stack = 1;
            if (ZoneManager.Instance.CurrentMap.Status.ContainsKey(WeatherID))
                stack++;
            status.StatusStates.Set(new StackState(stack));
            return true;
        }
    }


    /// <summary>
    /// Event that sets the HPState based on the character's max HP divided by the specified denominator 
    /// This is usually used for stat boosts stauses such as attack, defense, evasiveness, etc.
    /// </summary>
    [Serializable]
    public class StatusHPBattleEvent : StatusBattleEvent
    {
        /// <summary>
        /// The value to divide the max HP by
        /// </summary>
        public int HPFraction;

        public StatusHPBattleEvent() { }
        public StatusHPBattleEvent(string statusID, bool affectTarget, bool silentCheck, bool selfInflict, bool anonymous, int hpFraction)
            : base(statusID, affectTarget, silentCheck, selfInflict, anonymous)
        {
            HPFraction = hpFraction;
        }
        protected StatusHPBattleEvent(StatusHPBattleEvent other)
            : base(other)
        {
            HPFraction = other.HPFraction;
        }
        public override GameEvent Clone() { return new StatusHPBattleEvent(this); }

        protected override bool ModStatus(GameEventOwner owner, BattleContext context, Character target, Character origin, StatusEffect status)
        {
            status.StatusStates.Set(new HPState(Math.Max(1, origin.MaxHP / HPFraction)));
            return true;
        }
    }

    /// <summary>
    /// Event that calculates the damage done to the character and sets the value in the HPState status state
    /// </summary>
    [Serializable]
    public class FutureAttackEvent : StatusBattleEvent
    {
        public FutureAttackEvent() { }
        public FutureAttackEvent(string statusID)
            : base(statusID, true, false)
        { }
        protected FutureAttackEvent(FutureAttackEvent other)
            : base(other) { }
        public override GameEvent Clone() { return new FutureAttackEvent(this); }

        protected override bool ModStatus(GameEventOwner owner, BattleContext context, Character target, Character origin, StatusEffect status)
        {
            int dmg = DamageFormulaEvent.CalculateDamageFormula(owner, context);

            status.StatusStates.Set(new HPState(Math.Max(1, dmg)));
            return true;
        }
    }

    /// <summary>
    /// Event that checks the damage done to the character and sets the value in the HPState status state 
    /// </summary>
    [Serializable]
    public class GiveContinuousDamageEvent : StatusBattleEvent
    {
        public GiveContinuousDamageEvent() { }
        public GiveContinuousDamageEvent(string statusID, bool affectTarget, bool silentCheck)
            : base(statusID, affectTarget, silentCheck, false, false) { }
        protected GiveContinuousDamageEvent(GiveContinuousDamageEvent other)
            : base(other) { }
        public override GameEvent Clone() { return new GiveContinuousDamageEvent(this); }

        protected override bool ModStatus(GameEventOwner owner, BattleContext context, Character target, Character origin, StatusEffect status)
        {
            status.StatusStates.Set(new HPState(Math.Max(context.GetContextStateInt<DamageDealt>(0), 1)));
            return true;
        }
    }

    /// <summary>
    /// Event that make the user drop the target by applying the original status and if successful, applies the alternate status
    /// </summary>
    [Serializable]
    public class SkyDropStatusBattleEvent : StatusBattleEvent
    {
        /// <summary>
        /// The alternate status to apply 
        /// </summary>
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string AltStatusID;

        public SkyDropStatusBattleEvent() { }
        public SkyDropStatusBattleEvent(string statusID, string altStatusID, bool affectTarget, bool silentCheck)
            : base(statusID, affectTarget, silentCheck, false, false) { AltStatusID = altStatusID; }
        protected SkyDropStatusBattleEvent(SkyDropStatusBattleEvent other)
            : base(other) { AltStatusID = other.AltStatusID; }
        public override GameEvent Clone() { return new SkyDropStatusBattleEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = (AffectTarget ? context.Target : context.User);
            Character origin = (AffectTarget ? context.User : context.Target);
            if (target.Dead)
                yield break;

            AbortStatus cancel = new AbortStatus();
            yield return CoroutineManager.Instance.StartCoroutine(applyStatus(owner, ownerChar, StatusID, target, origin, context, cancel));
            if (!cancel.Cancel)
                yield return CoroutineManager.Instance.StartCoroutine(applyStatus(owner, ownerChar, AltStatusID, origin, target, context, cancel));

            //This will pull the player to a proper location if it dashed onto untraversible terrain and snapped back.  it looks a bit janky though....
            LocRay8 candLoc = new LocRay8(context.StrikeStartTile, context.User.CharDir);
            Loc endLoc = context.User.CharLoc;
            for (int ii = 0; ii < context.HitboxAction.Distance; ii++)
            {
                candLoc.Traverse(1);
                if (ZoneManager.Instance.CurrentMap.GetCharAtLoc(endLoc) == null)
                    endLoc = candLoc.Loc;
            }
            context.User.CharLoc = endLoc;
        }
    }

    /// <summary>
    /// Event that applies the original status and if successful, applies the alternate status 
    /// This event can only be used on statuses
    /// </summary>
    [Serializable]
    public class CoupledStatusBattleEvent : StatusBattleEvent
    {
        /// <summary>
        /// The alternate status to apply
        /// </summary>
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string AltStatusID;

        public CoupledStatusBattleEvent() { }
        public CoupledStatusBattleEvent(string statusID, string altStatusID, bool affectTarget, bool silentCheck)
            : base(statusID, affectTarget, silentCheck, false, false) { AltStatusID = altStatusID; }
        protected CoupledStatusBattleEvent(CoupledStatusBattleEvent other)
            : base(other) { AltStatusID = other.AltStatusID; }
        public override GameEvent Clone() { return new CoupledStatusBattleEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = (AffectTarget ? context.Target : context.User);
            Character origin = (AffectTarget ? context.User : context.Target);
            if (target.Dead)
                yield break;

            AbortStatus cancel = new AbortStatus();
            yield return CoroutineManager.Instance.StartCoroutine(applyStatus(owner, ownerChar, StatusID, target, origin, context, cancel));
            if (!cancel.Cancel)
                yield return CoroutineManager.Instance.StartCoroutine(applyStatus(owner, ownerChar, AltStatusID, origin, target, context, cancel));
        }
    }



    /// <summary>
    /// Event that causes the status to spread if the character makes contact
    /// This event can only be used on statuses
    /// </summary>
    [Serializable]
    public class StatusSpreadEvent : BattleEvent
    {
        /// <summary>
        /// Whether to apply the status to the user or target
        /// </summary> 
        public bool AffectTarget;

        public StatusSpreadEvent() { }
        public StatusSpreadEvent(bool affectTarget)
        {
            AffectTarget = affectTarget;
        }
        protected StatusSpreadEvent(StatusSpreadEvent other)
        {
            AffectTarget = other.AffectTarget;
        }
        public override GameEvent Clone() { return new StatusSpreadEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = (AffectTarget ? context.Target : context.User);
            Character origin = (AffectTarget ? context.User : context.Target);
            if (target.Dead || origin.Dead)
                yield break;

            if (context.Data.SkillStates.Contains<ContactState>())
            {
                StatusEffect status = ((StatusEffect)owner).Clone();
                if (((StatusEffect)owner).TargetChar != null)
                {
                    if (((StatusEffect)owner).TargetChar == origin)
                        status.TargetChar = target;
                    else if (((StatusEffect)owner).TargetChar == target)
                        status.TargetChar = origin;
                }
                yield return CoroutineManager.Instance.StartCoroutine(target.AddStatusEffect(origin, status, null, false, true));
            }
        }
    }


    /// <summary>
    /// Event that removes the specified status
    /// </summary>
    [Serializable]
    public class RemoveStatusBattleEvent : BattleEvent
    {
        /// <summary>
        /// The status to remove
        /// </summary>
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string StatusID;
        
        /// <summary>
        /// Whether to affect the target or user
        /// </summary>
        public bool AffectTarget;

        public RemoveStatusBattleEvent() { StatusID = ""; }
        public RemoveStatusBattleEvent(string statusID, bool affectTarget)
        {
            StatusID = statusID;
            AffectTarget = affectTarget;
        }
        protected RemoveStatusBattleEvent(RemoveStatusBattleEvent other)
        {
            StatusID = other.StatusID;
            AffectTarget = other.AffectTarget;
        }
        public override GameEvent Clone() { return new RemoveStatusBattleEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = (AffectTarget ? context.Target : context.User);
            if (target.Dead)
                yield break;

            yield return CoroutineManager.Instance.StartCoroutine(target.RemoveStatusEffect(StatusID));
        }
    }

    /// <summary>
    /// Event that removes all the stat changes for the specified stat
    /// </summary>
    [Serializable]
    public class RemoveStatusStackBattleEvent : BattleEvent
    {
        /// <summary>
        /// The status affected
        /// </summary>
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string StatusID;
        
        /// <summary>
        /// Whether to affect the target or user
        /// </summary>
        public bool AffectTarget;
        
        /// <summary>
        /// If the stack amount is negative, then reset the stack amount
        /// </summary>
        public bool Negative;

        /// <summary>
        /// If the stack amount is positive, then reset the stack amount
        /// </summary>
        public bool Positive;

        public RemoveStatusStackBattleEvent() { StatusID = ""; }
        public RemoveStatusStackBattleEvent(string statusID, bool affectTarget, bool negative, bool positive)
        {
            StatusID = statusID;
            AffectTarget = affectTarget;
            Negative = negative;
            Positive = positive;
        }
        protected RemoveStatusStackBattleEvent(RemoveStatusStackBattleEvent other)
        {
            StatusID = other.StatusID;
            AffectTarget = other.AffectTarget;
            Negative = other.Negative;
            Positive = other.Positive;
        }
        public override GameEvent Clone() { return new RemoveStatusStackBattleEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = (AffectTarget ? context.Target : context.User);
            if (target.Dead)
                yield break;

            StatusEffect status = target.GetStatusEffect(StatusID);
            if (status != null)
            {
                StackState stack = status.StatusStates.GetWithDefault<StackState>();
                if (stack.Stack > 0 && Positive || stack.Stack < 0 && Negative)
                    yield return CoroutineManager.Instance.StartCoroutine(target.RemoveStatusEffect(StatusID));
            }
        }
    }
    
    /// <summary>
    /// Event that reverts the character's stat changes
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
    /// Event that removes the status if the status contains the one of the specified status states
    /// </summary>
    [Serializable]
    public class RemoveStateStatusBattleEvent : BattleEvent
    {

        /// <summary>
        /// The list of status states to check for
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
        
        /// <summary>
        /// The list of battle VFXs played if the condition is met
        /// </summary>
        public List<BattleAnimEvent> Anims;

        public RemoveStateStatusBattleEvent()
        {
            States = new List<FlagType>();
            Anims = new List<BattleAnimEvent>();
        }
        public RemoveStateStatusBattleEvent(Type state, bool affectTarget, StringKey msg, params BattleAnimEvent[] anims) : this()
        {
            States.Add(new FlagType(state));
            AffectTarget = affectTarget;
            Msg = msg;
            Anims.AddRange(anims);
        }
        protected RemoveStateStatusBattleEvent(RemoveStateStatusBattleEvent other) : this()
        {
            States.AddRange(other.States);
            AffectTarget = other.AffectTarget;
            Msg = other.Msg;
            foreach (BattleAnimEvent anim in other.Anims)
                Anims.Add((BattleAnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new RemoveStateStatusBattleEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = (AffectTarget ? context.Target : context.User);
            if (target.Dead)
                yield break;

            List<string> statuses = new List<string>();
            foreach (StatusEffect status in target.IterateStatusEffects())
            {
                bool hasState = false;
                foreach (FlagType state in States)
                {
                    if (status.StatusStates.Contains(state.FullType))
                        hasState = true;
                }
                if (hasState)
                    statuses.Add(status.ID);
            }

            if (statuses.Count > 0)
            {
                if (Msg.IsValid())
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(Msg.ToLocal(), target.GetDisplayName(false), owner.GetDisplayName()));

                foreach (BattleAnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));
            }

            foreach (string statusID in statuses)
                yield return CoroutineManager.Instance.StartCoroutine(target.RemoveStatusEffect(statusID, false));

        }

    }

    /// <summary>
    /// Event that removes its status from the user, automatically determining which ones they are
    /// This event can only be used on statuses
    /// </summary>
    [Serializable]
    public class RemoveBattleEvent : BattleEvent
    {
        /// <summary>
        /// Whether to display the message associated with this event
        /// </summary> 
        public bool ShowMessage;

        public RemoveBattleEvent() { }
        public RemoveBattleEvent(bool showMessage)
        {
            ShowMessage = showMessage;
        }
        protected RemoveBattleEvent(RemoveBattleEvent other)
        {
            ShowMessage = other.ShowMessage;
        }
        public override GameEvent Clone() { return new RemoveBattleEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            yield return CoroutineManager.Instance.StartCoroutine(ownerChar.RemoveStatusEffect(((StatusEffect)owner).ID, ShowMessage));
        }
    }


    /// <summary>
    /// Event used specifically by statuses and removes itself when the character receives damage
    /// </summary>
    [Serializable]
    public class RemoveOnDamageEvent : BattleEvent
    {
        public RemoveOnDamageEvent() { }
        public override GameEvent Clone() { return new RemoveOnDamageEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.GetContextStateInt<DamageDealt>(0) > 0)
                yield return CoroutineManager.Instance.StartCoroutine(context.Target.RemoveStatusEffect(((StatusEffect)owner).ID));
        }
    }

    /// <summary>
    /// Event that removes its status if the user does an action 
    /// This event can only be used on statuses
    /// </summary>
    [Serializable]
    public class RemoveOnActionEvent : BattleEvent
    {
        public bool ShowMessage;
        public bool WaitAnimations;

        public RemoveOnActionEvent() { }
        public RemoveOnActionEvent(bool showMessage)
        {
            ShowMessage = showMessage;
        }
        public RemoveOnActionEvent(bool showMessage, bool waitAnimations)
        {
            ShowMessage = showMessage;
            WaitAnimations = waitAnimations;
        }
        protected RemoveOnActionEvent(RemoveOnActionEvent other)
        {
            ShowMessage = other.ShowMessage;
            WaitAnimations = other.WaitAnimations;
        }
        public override GameEvent Clone() { return new RemoveOnActionEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.UsageSlot == BattleContext.FORCED_SLOT)
                yield break;

            if (WaitAnimations)
                yield return new WaitUntil(DungeonScene.Instance.AnimationsOver);

            yield return CoroutineManager.Instance.StartCoroutine(context.User.RemoveStatusEffect(((StatusEffect)owner).ID, ShowMessage));
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
    /// Event that causes the user to transfer statuses to the target
    /// </summary>
    [Serializable]
    public class TransferStatusEvent : BattleEvent
    {
        /// <summary>
        /// Whether to remove the original statuses from the user
        /// </summary>
        public bool Remove;

        /// <summary>
        /// Whether to transfer statuses that have the MajorStatusState status state
        /// </summary>
        public bool MajorStatus;


        /// <summary>
        /// Whether to transfer statuses that have the MajorStatusState status state
        /// </summary>
        public bool MinorStatus;

        /// <summary>
        /// Whether to transfer statuses that have the BadStatusState status state
        /// </summary>
        public bool BadStatus;

        /// <summary>
        /// Whether to transfer good statuses
        /// </summary>
        public bool GoodStatus;


        public TransferStatusEvent() { }
        public TransferStatusEvent(bool remove, bool majorStatus, bool minorStatus, bool badStatus, bool goodStatus)
        {
            Remove = remove;
            MajorStatus = majorStatus;
            MinorStatus = minorStatus;
            BadStatus = badStatus;
            GoodStatus = goodStatus;
        }
        protected TransferStatusEvent(TransferStatusEvent other)
        {
            Remove = other.Remove;
            MajorStatus = other.MajorStatus;
            MinorStatus = other.MinorStatus;
            BadStatus = other.BadStatus;
            GoodStatus = other.GoodStatus;
        }
        public override GameEvent Clone() { return new TransferStatusEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Target.Dead)
                yield break;

            List<StatusEffect> statuses = new List<StatusEffect>();
            foreach (StatusEffect status in context.User.IterateStatusEffects())
            {
                if (status.StatusStates.Contains<TransferStatusState>())
                {
                    bool badStatus = status.StatusStates.Contains<BadStatusState>();
                    bool majorStatus = status.StatusStates.Contains<MajorStatusState>();
                    if ((BadStatus && badStatus || GoodStatus && !badStatus) && (MajorStatus && majorStatus || MinorStatus && !majorStatus))
                        statuses.Add(status);
                }
            }

            if (statuses.Count == 0)
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_SKILL_FAILED").ToLocal()));
            else
            {
                foreach (StatusEffect status in statuses)
                {
                    StatusEffect newStatus = status.Clone();
                    if (status.TargetChar != null)
                    {
                        if (status.TargetChar == context.User)
                            newStatus.TargetChar = context.Target;
                        else if (status.TargetChar == context.Target)
                            newStatus.TargetChar = context.User;
                    }
                    if (Remove)
                        yield return CoroutineManager.Instance.StartCoroutine(context.User.RemoveStatusEffect(status.ID, false));
                    yield return CoroutineManager.Instance.StartCoroutine(context.Target.AddStatusEffect(context.User, newStatus));
                }
            }
        }
    }


    /// <summary>
    /// Event that increments the AttackHitTotal global context state
    /// </summary>
    [Serializable]
    public class AffectHighestStatBattleEvent : BattleEvent
    {
        public bool AffectTarget;
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string AtkStat;
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string DefStat;
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string SpAtkStat;
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string SpDefStat;
        public bool Anonymous;
        public int Stack;

        public AffectHighestStatBattleEvent()
        {
            AtkStat = "";
            DefStat = "";
            SpAtkStat = "";
            SpDefStat = "";
        }
        public AffectHighestStatBattleEvent(bool affectTarget, string atkStat, string defStat, string spAtkStat, string spDefStat, bool anonymous, int stack)
        {
            AffectTarget = affectTarget;
            AtkStat = atkStat;
            DefStat = defStat;
            SpAtkStat = spAtkStat;
            SpDefStat = spDefStat;
            Anonymous = anonymous;
            Stack = stack;

        }
        protected AffectHighestStatBattleEvent(AffectHighestStatBattleEvent other)
        {
            AffectTarget = other.AffectTarget;
            AtkStat = other.AtkStat;
            DefStat = other.DefStat;
            SpAtkStat = other.SpAtkStat;
            SpDefStat = other.SpDefStat;
            Anonymous = other.Anonymous;
            Stack = other.Stack;
        }
        public override GameEvent Clone() { return new AffectHighestStatBattleEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = (AffectTarget ? context.Target : context.User);
            Character origin = (AffectTarget ? context.User : context.Target);
            if (target.Dead)
                yield break;

            string highestSpecial = SpAtkStat;
            int highestSpecialValue = modStat(target.MAtk, SpAtkStat, target);
            string highestPhysical = AtkStat;
            int highestPhysicalValue = modStat(target.Atk, AtkStat, target);

            int defValue = modStat(target.Def, DefStat, target);
            if (defValue > target.Atk)
            {
                highestPhysical = DefStat;
                highestPhysicalValue = defValue;
            }

            int mDefValue = modStat(target.MDef, SpDefStat, target);
            if (mDefValue > target.MAtk)
            {
                highestSpecial = SpDefStat;
                highestSpecialValue = mDefValue;
            }

            string highestStat = highestPhysical;
            if (highestSpecialValue > highestPhysicalValue)
                highestStat = highestSpecial;

            StatusEffect setStatus = new StatusEffect(highestStat);
            setStatus.LoadFromData();
            setStatus.StatusStates.Set(new StackState(Stack));

            yield return CoroutineManager.Instance.StartCoroutine(target.AddStatusEffect(Anonymous ? null : origin, setStatus));
        }

        private int modStat(int value, string status, Character target)
        {
            //StatusEffect statChange = target.GetStatusEffect(status);
            //if (statChange != null)
            //{
            //    int stack = statChange.StatusStates.GetWithDefault<StackState>().Stack;
            //    //TODO: modify the stat based on stacking
            //}
            return value;
        }
    }

    /// <summary>
    /// Event that raises the Attack or 
    /// </summary>
    [Serializable]
    public class DownloadEvent : BattleEvent
    {
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string AtkStat;
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string SpAtkStat;

        public DownloadEvent()
        {
            AtkStat = "";
            SpAtkStat = "";
        }
        public DownloadEvent(string atkStat, string spAtkStat)
        {
            AtkStat = atkStat;
            SpAtkStat = spAtkStat;
        }
        protected DownloadEvent(DownloadEvent other)
        {
            AtkStat = other.AtkStat;
            SpAtkStat = other.SpAtkStat;
        }
        public override GameEvent Clone() { return new DownloadEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Target.Dead)
                yield break;

            string lowerStat = SpAtkStat;
            string higherStat = AtkStat;
            if (context.User.Def > context.User.MDef)
            {
                lowerStat = AtkStat;
                higherStat = SpAtkStat;
            }

            DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_DOWNLOAD").ToLocal(), context.Target.GetDisplayName(false), context.User.GetDisplayName(false)));

            StatusEffect lowerStatus = new StatusEffect(lowerStat);
            lowerStatus.LoadFromData();
            lowerStatus.StatusStates.Set(new StackState(-1));

            yield return CoroutineManager.Instance.StartCoroutine(context.Target.AddStatusEffect(ownerChar, lowerStatus, null, false, true));

            StatusEffect higherStatus = new StatusEffect(higherStat);
            higherStatus.LoadFromData();
            higherStatus.StatusStates.Set(new StackState(1));

            yield return CoroutineManager.Instance.StartCoroutine(context.Target.AddStatusEffect(ownerChar, higherStatus, null, false, true));
        }
    }

    /// <summary>
    /// Event that raises one stat by one and lowers the other stat by one
    /// </summary>
    [Serializable]
    public class RaiseOneLowerOneEvent : BattleEvent
    {
        //physical ID will get dropped when the involved attack is physical
        //special ID will get dropped when the involved attack is special
        /// <summary>
        /// The stat raised
        /// </summary>
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string RaiseID;

        /// <summary>
        /// The stat lowered
        /// </summary>
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string LowerID;

        /// <summary>
        /// The message displayed in the dungeon log
        /// </summary>
        public StringKey Message;

        public RaiseOneLowerOneEvent()
        {
            RaiseID = "";
            LowerID = "";
        }
        public RaiseOneLowerOneEvent(string raiseID, string lowerID, StringKey msg)
        {
            RaiseID = raiseID;
            LowerID = lowerID;
            Message = msg;
        }
        protected RaiseOneLowerOneEvent(RaiseOneLowerOneEvent other)
            : this()
        {
            RaiseID = other.RaiseID;
            LowerID = other.LowerID;
            Message = other.Message;
        }
        public override GameEvent Clone() { return new RaiseOneLowerOneEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), ownerChar.GetDisplayName(false)));

            StatusEffect lowerStatus = new StatusEffect(LowerID);
            lowerStatus.LoadFromData();
            lowerStatus.StatusStates.Set(new StackState(-1));

            StatusEffect higherStatus = new StatusEffect(RaiseID);
            higherStatus.LoadFromData();
            higherStatus.StatusStates.Set(new StackState(1));

            yield return CoroutineManager.Instance.StartCoroutine(ownerChar.AddStatusEffect(ownerChar, lowerStatus, null, false, true));

            yield return CoroutineManager.Instance.StartCoroutine(ownerChar.AddStatusEffect(ownerChar, higherStatus, null, false, true));
        }
    }

    /// <summary>
    /// Event that raises one stat by one and lowers the other stat by one
    /// depending on whether the involved attack is special or physical
    /// </summary>
    [Serializable]
    public class MoodyEvent : BattleEvent
    {
        //physical ID will get dropped when the involved attack is physical
        //special ID will get dropped when the involved attack is special

        /// <summary>
        /// The status raised if the involved attack is physical, otherwise lowered
        /// </summary>
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string OnPhysicalID;

        /// <summary>
        /// The status raised if the involved attack is special, otherwise lowered
        /// </summary>
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string OnSpecialID;

        public MoodyEvent()
        {
            OnPhysicalID = "";
            OnSpecialID = "";
        }
        public MoodyEvent(string onPhysical, string onSpecial)
        {
            OnPhysicalID = onPhysical;
            OnSpecialID = onSpecial;
        }
        protected MoodyEvent(MoodyEvent other)
            : this()
        {
            OnPhysicalID = other.OnPhysicalID;
            OnSpecialID = other.OnSpecialID;
        }
        public override GameEvent Clone() { return new MoodyEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            string raiseID = "";
            string dropID = "";

            if (context.Data.Category == BattleData.SkillCategory.Physical)
            {
                raiseID = OnPhysicalID;
                dropID = OnSpecialID;
            }
            else if (context.Data.Category == BattleData.SkillCategory.Magical)
            {
                raiseID = OnSpecialID;
                dropID = OnPhysicalID;
            }

            if (!String.IsNullOrEmpty(dropID) && !String.IsNullOrEmpty(raiseID))
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_MOODY").ToLocal(), ownerChar.GetDisplayName(false)));

                StatusEffect lowerStatus = new StatusEffect(dropID);
                lowerStatus.LoadFromData();
                lowerStatus.StatusStates.Set(new StackState(-1));

                yield return CoroutineManager.Instance.StartCoroutine(ownerChar.AddStatusEffect(ownerChar, lowerStatus, null, false, true));

                StatusEffect higherStatus = new StatusEffect(raiseID);
                higherStatus.LoadFromData();
                higherStatus.StatusStates.Set(new StackState(1));

                yield return CoroutineManager.Instance.StartCoroutine(ownerChar.AddStatusEffect(ownerChar, higherStatus, null, false, true));
            }
        }
    }

    /// <summary>
    /// Event that boosts a random stat when the character eats a berry
    /// </summary>
    [Serializable]
    public class BerryBoostEvent : BattleEvent
    {

        /// <summary>
        /// The list of stats to choose from
        /// </summary>
        [JsonConverter(typeof(StatusListConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public List<string> StatsToBoost;

        public BerryBoostEvent() { StatsToBoost = new List<string>(); }
        public BerryBoostEvent(params string[] effects)
        {
            StatsToBoost = new List<string>();
            foreach (string effect in effects)
                StatsToBoost.Add(effect);
        }
        protected BerryBoostEvent(BerryBoostEvent other)
            : this()
        {
            StatsToBoost.AddRange(other.StatsToBoost);
        }
        public override GameEvent Clone() { return new BerryBoostEvent(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            //TODO: decouple this with BerryNeededEvent
            if (context.ActionType == BattleActionType.Item)
            {
                ItemData itemData = DataManager.Instance.GetItem(context.Item.ID);
                if (itemData.ItemStates.Contains<BerryState>())
                {
                    string statusID = StatsToBoost[DataManager.Instance.Save.Rand.Next(StatsToBoost.Count)];

                    StatusEffect setStatus = new StatusEffect(statusID);
                    setStatus.LoadFromData();
                    setStatus.StatusStates.Set(new StackState(1));

                    yield return CoroutineManager.Instance.StartCoroutine(context.Target.AddStatusEffect(null, setStatus, null, false, true));
                }
            }
        }
    }

    /// <summary>
    /// Event that removes all stat boosts and damages the character for each stat boost
    /// Stat drops will heal the character instead
    /// </summary>
    [Serializable]
    public class AdNihiloEvent : BattleEvent
    {
        /// <summary>
        /// If the status contains the one of the specified status states, then it's stack amount will be considered in the calculation
        /// </summary>
        [StringTypeConstraint(1, typeof(StatusState))]
        public List<FlagType> States;

        /// <summary>
        /// The denominator of the damage or heal
        /// </summary>
        public int Denominator;

        /// <summary>
        /// Whether to affect the target or user
        /// </summary>
        public bool AffectTarget;

        public AdNihiloEvent() { States = new List<FlagType>(); }
        public AdNihiloEvent(Type state, int denominator, bool affectTarget) : this() { States.Add(new FlagType(state)); Denominator = denominator; AffectTarget = affectTarget; }
        protected AdNihiloEvent(AdNihiloEvent other) : this()
        {
            States.AddRange(other.States);
            Denominator = other.Denominator;
            AffectTarget = other.AffectTarget;
        }
        public override GameEvent Clone() { return new AdNihiloEvent(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = (AffectTarget ? context.Target : context.User);
            if (target.Dead)
                yield break;

            int totalChange = 0;
            foreach (StatusEffect status in target.IterateStatusEffects())
            {
                bool hasState = false;
                foreach (FlagType state in States)
                {
                    if (status.StatusStates.Contains(state.FullType))
                        hasState = true;
                }
                if (hasState)
                    totalChange += status.StatusStates.GetWithDefault<StackState>().Stack;
            }

            if (totalChange > 0)
            {
                int dmg = Math.Max(1, target.MaxHP * totalChange / Denominator);
                dmg = Math.Min(dmg, target.MaxHP / 2);

                GameManager.Instance.BattleSE("DUN_Hit_Neutral");
                SingleEmitter endEmitter = new SingleEmitter(new AnimData("Hit_Neutral", 3));

                if (!context.Target.Unidentifiable)
                {
                    endEmitter.SetupEmit(context.Target.MapLoc, context.User.MapLoc, context.Target.CharDir);
                    DungeonScene.Instance.CreateAnim(endEmitter, DrawLayer.NoDraw);
                }

                yield return CoroutineManager.Instance.StartCoroutine(target.InflictDamage(dmg));
            }
            else
            {
                int dmg = Math.Max(1, -target.MaxHP * totalChange / Denominator);
                dmg = Math.Min(dmg, target.MaxHP / 2);
                yield return CoroutineManager.Instance.StartCoroutine(target.RestoreHP(dmg));
                context.ContextStates.Set(new DamageHealedTarget(dmg));
            }
        }

    }


    /// <summary>
    /// Event that removes any statuses with the BadStatusState status state of nearby characters 
    /// </summary>
    [Serializable]
    public class HealSurroundingsEvent : BattleEvent
    {
        /// <summary>
        /// The message displayed if the bad status is cured
        /// </summary>
        public StringKey Message;

        /// <summary>
        /// The list of battle VFXs played if the condition is met
        /// </summary>
        public List<AnimEvent> Anims;

        public HealSurroundingsEvent() { Anims = new List<AnimEvent>(); }
        public HealSurroundingsEvent(StringKey msg, params AnimEvent[] anims)
        {
            Message = msg;
            Anims = new List<AnimEvent>();
            Anims.AddRange(anims);
        }
        protected HealSurroundingsEvent(HealSurroundingsEvent other)
        {
            Message = other.Message;
            Anims = new List<AnimEvent>();
            foreach (AnimEvent anim in other.Anims)
                Anims.Add((AnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new HealSurroundingsEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            foreach (Character target in ZoneManager.Instance.CurrentMap.GetCharsInFillRect(context.User.CharLoc, Rect.FromPointRadius(context.User.CharLoc, 1)))
            {
                if (!target.Dead && context.User != target)
                {
                    List<string> badStatuses = new List<string>();
                    foreach (StatusEffect status in target.IterateStatusEffects())
                    {
                        if (status.StatusStates.Contains<BadStatusState>())
                            badStatuses.Add(status.ID);
                    }

                    if (badStatuses.Count > 0)
                    {
                        DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), ownerChar.GetDisplayName(false), target.GetDisplayName(false)));

                        foreach (AnimEvent anim in Anims)
                        {
                            SingleCharContext singleContext = new SingleCharContext(target);
                            yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, singleContext));
                        }
                    }

                    foreach (string statusID in badStatuses)
                        yield return CoroutineManager.Instance.StartCoroutine(target.RemoveStatusEffect(statusID, false));
                }
            }
        }
    }


    /// <summary>
    /// Event that applies the specified status to the character and restores them to full HP
    /// </summary>
    [Serializable]
    public class RestEvent : BattleEvent
    {
        /// <summary>
        /// The status to apply
        /// </summary>
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string SleepID;

        public RestEvent() { SleepID = ""; }
        public RestEvent(string sleepID)
        {
            SleepID = sleepID;
        }
        protected RestEvent(RestEvent other)
        {
            SleepID = other.SleepID;
        }
        public override GameEvent Clone() { return new RestEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Target.Dead)
                yield break;

            StatusEffect status = new StatusEffect(SleepID);
            status.LoadFromData();

            StatusCheckContext statusContext = new StatusCheckContext(context.User, context.Target, status, true);
            yield return CoroutineManager.Instance.StartCoroutine(RestStatusCheck(statusContext));
            //manually check all factors EXCEPT for the current nonvolatile status (copy+paste the BeforeStatusCheck code)
            if (statusContext.CancelState.Cancel)
                yield break;

            //silently remove current nonvolatile status (if any), and silently give sleep status
            List<string> badStatuses = new List<string>();
            foreach (StatusEffect oldStatus in context.Target.IterateStatusEffects())
            {
                if (oldStatus.StatusStates.Contains<MajorStatusState>())
                    badStatuses.Add(oldStatus.ID);
            }
            foreach (string statusID in badStatuses)
                yield return CoroutineManager.Instance.StartCoroutine(context.Target.RemoveStatusEffect(statusID, false));

            yield return CoroutineManager.Instance.StartCoroutine(context.Target.AddStatusEffect(context.User, status, false));

            DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_REST").ToLocal(), context.Target.GetDisplayName(false)));

            //restore all HP
            yield return CoroutineManager.Instance.StartCoroutine(context.Target.RestoreHP(context.Target.MaxHP));

            context.ContextStates.Set(new DamageHealedTarget(context.Target.MaxHP));
        }

        private IEnumerator<YieldInstruction> RestStatusCheck(StatusCheckContext context)
        {
            DungeonScene.EventEnqueueFunction<StatusGivenEvent> function = (StablePriorityQueue<GameEventPriority, EventQueueElement<StatusGivenEvent>> queue, Priority maxPriority, ref Priority nextPriority) =>
            {
                //do not check pending status

                //check everything else
                foreach (PassiveContext effectContext in context.User.IteratePassives(GameEventPriority.USER_PORT_PRIORITY))
                    effectContext.AddEventsToQueue(queue, maxPriority, ref nextPriority, effectContext.EventData.BeforeStatusAddings, null);
                foreach (PassiveContext effectContext in context.Target.IteratePassives(GameEventPriority.TARGET_PORT_PRIORITY))
                    effectContext.AddEventsToQueue<StatusGivenEvent>(queue, maxPriority, ref nextPriority, effectContext.EventData.BeforeStatusAdds, null);
            };
            foreach (EventQueueElement<StatusGivenEvent> effect in DungeonScene.IterateEvents<StatusGivenEvent>(function))
            {
                yield return CoroutineManager.Instance.StartCoroutine(effect.Event.Apply(effect.Owner, effect.OwnerChar, context));
                if (context.CancelState.Cancel)
                    yield break;
            }
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

