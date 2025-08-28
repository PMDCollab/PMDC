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
    // Battle events that can cancel the action

    /// <summary>
    /// Event that cancels the action (intended to be used with -Needed events)
    /// </summary>
    [Serializable]
    public class CancelActionEvent : BattleEvent
    {
        /// <summary>
        /// The message displayed in the dungeon log 
        /// </summary>
        [StringKey(0, true)]
        public StringKey Message;

        public CancelActionEvent() { }
        public CancelActionEvent(StringKey message) : this()
        {
            Message = message;
        }
        protected CancelActionEvent(CancelActionEvent other) : this()
        {
            Message = other.Message;
        }
        public override GameEvent Clone() { return new CancelActionEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (Message.IsValid())
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), context.User.GetDisplayName(false)));
            context.CancelState.Cancel = true;
            yield break;
        }
    }

    /// <summary>
    /// Event that prevents the character from doing certain battle action types
    /// </summary>
    [Serializable]
    public class PreventActionEvent : BattleEvent
    {
        /// <summary>
        /// The list of battle action type that the character cannot do
        /// </summary>
        public HashSet<BattleActionType> Actions;

        /// <summary>
        /// The message displayed in the dungeon log 
        /// </summary>
        [StringKey(0, true)]
        public StringKey Message;

        public PreventActionEvent() { Actions = new HashSet<BattleActionType>(); }
        public PreventActionEvent(StringKey message, params BattleActionType[] actions) : this()
        {
            Message = message;
            foreach (BattleActionType actionType in actions)
                Actions.Add(actionType);
        }
        protected PreventActionEvent(PreventActionEvent other) : this()
        {
            Message = other.Message;
            foreach (BattleActionType actionType in other.Actions)
                Actions.Add(actionType);
        }
        public override GameEvent Clone() { return new PreventActionEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.UsageSlot == BattleContext.FORCED_SLOT)
                yield break;

            if (!Actions.Contains(context.ActionType))
                yield break;

            if (Message.IsValid())
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), context.User.GetDisplayName(false)));
            context.CancelState.Cancel = true;
        }
    }

    /// <summary>
    /// Event that prevents the character from using items unless the item contains the one of the specified item states
    /// </summary>
    [Serializable]
    public class PreventItemActionEvent : BattleEvent
    {

        /// <summary>
        /// The list of valid ItemState types
        /// </summary>
        [StringTypeConstraint(1, typeof(ItemState))]
        public HashSet<FlagType> ExceptTypes;

        /// <summary>
        /// The message displayed in the dungeon log if the condition is not met
        /// </summary>
        [StringKey(0, true)]
        public StringKey Message;

        public PreventItemActionEvent() { ExceptTypes = new HashSet<FlagType>(); }
        public PreventItemActionEvent(StringKey message, params FlagType[] exceptTypes) : this()
        {
            Message = message;
            foreach (FlagType useType in exceptTypes)
                ExceptTypes.Add(useType);
        }
        protected PreventItemActionEvent(PreventItemActionEvent other)
        {
            Message = other.Message;
            foreach (FlagType useType in other.ExceptTypes)
                ExceptTypes.Add(useType);
        }
        public override GameEvent Clone() { return new PreventItemActionEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Item)
            {
                if (context.ActionType == BattleActionType.Item)
                {
                    ItemData entry = DataManager.Instance.GetItem(context.Item.ID);
                    foreach (FlagType flag in ExceptTypes)
                    {
                        if (entry.ItemStates.Contains(flag.FullType))
                            yield break;
                    }
                }

                if (Message.IsValid())
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), context.User.GetDisplayName(false)));
                context.CancelState.Cancel = true;
            }
        }
    }

    /// <summary>
    /// Event that prevents the character from using items if they are paralyzed
    /// unless the item contains the one of the specified item states
    /// </summary>
    [Serializable]
    public class PreventItemParalysisEvent : BattleEvent
    {

        /// <summary>
        /// The list of valid ItemState types
        /// </summary>
        [StringTypeConstraint(1, typeof(ItemState))]
        public HashSet<FlagType> ExceptTypes;

        /// <summary>
        /// The message displayed in the dungeon log if the condition is not met
        /// </summary>
        [StringKey(0, true)]
        public StringKey Message;

        public PreventItemParalysisEvent() { ExceptTypes = new HashSet<FlagType>(); }
        public PreventItemParalysisEvent(StringKey message, params FlagType[] exceptTypes) : this()
        {
            Message = message;
            foreach (FlagType useType in exceptTypes)
                ExceptTypes.Add(useType);
        }
        protected PreventItemParalysisEvent(PreventItemParalysisEvent other)
        {
            Message = other.Message;
            foreach (FlagType useType in other.ExceptTypes)
                ExceptTypes.Add(useType);
        }
        public override GameEvent Clone() { return new PreventItemParalysisEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Item || context.ActionType == BattleActionType.Throw)
            {
                if (context.ActionType == BattleActionType.Item)
                {
                    ItemData entry = DataManager.Instance.GetItem(context.Item.ID);
                    foreach (FlagType flag in ExceptTypes)
                    {
                        if (entry.ItemStates.Contains(flag.FullType))
                            yield break;
                    }
                }

                ParalyzeState para = ((StatusEffect)owner).StatusStates.GetWithDefault<ParalyzeState>();
                if (para.Recent)
                {
                    if (Message.IsValid())
                        DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), context.User.GetDisplayName(false)));
                    context.CancelState.Cancel = true;
                }
            }
        }
    }

    /// <summary>
    /// Event that prevents the character from using items if the item contains the one of the specified item states
    /// </summary>
    [Serializable]
    public class PreventItemUseEvent : BattleEvent
    {

        /// <summary>
        /// The list of valid ItemState types
        /// </summary>
        [StringTypeConstraint(1, typeof(ItemState))]
        public HashSet<FlagType> UseTypes;

        /// <summary>
        /// The message displayed in the dungeon log if the condition is met 
        /// </summary>
        [StringKey(0, true)]
        public StringKey Message;

        public PreventItemUseEvent() { UseTypes = new HashSet<FlagType>(); }
        public PreventItemUseEvent(StringKey message, params FlagType[] useTypes) : this()
        {
            Message = message;
            foreach (FlagType useType in useTypes)
                UseTypes.Add(useType);
        }
        protected PreventItemUseEvent(PreventItemUseEvent other) : this()
        {
            Message = other.Message;
            foreach (FlagType useType in other.UseTypes)
                UseTypes.Add(useType);
        }
        public override GameEvent Clone() { return new PreventItemUseEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Item)
            {
                ItemData entry = DataManager.Instance.GetItem(context.Item.ID);
                bool canceled = false;
                if (UseTypes.Count == 0)
                    canceled = true;
                foreach (FlagType flag in UseTypes)
                {
                    if (entry.ItemStates.Contains(flag.FullType))
                    {
                        canceled = true;
                        break;
                    }
                }

                if (canceled)
                {
                    if (Message.IsValid())
                        DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), context.User.GetDisplayName(false)));
                    context.CancelState.Cancel = true;
                }
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that prevents the character from using the item if the item's HiddenValue is set
    /// </summary>
    [Serializable]
    public class CheckItemActiveEvent : BattleEvent
    {
        public CheckItemActiveEvent() { }
        public override GameEvent Clone() { return new CheckItemActiveEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (!String.IsNullOrEmpty(context.Item.HiddenValue))
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_ITEM_CANT_USE_NOW").ToLocal()));
                context.CancelState.Cancel = true;
            }
            yield break;
        }

    }

    /// <summary>
    /// Event that prevents the character from using certain items
    /// </summary>
    [Serializable]
    public class PreventItemIndexEvent : BattleEvent
    {
        /// <summary>
        /// The list of items the character cannot use
        /// </summary>
        [JsonConverter(typeof(ItemListConverter))]
        [DataType(1, DataManager.DataType.Item, false)]
        public List<string> UseTypes;

        /// <summary>
        /// The message displayed in the dungeon log if the character cannot use the item
        /// </summary>
        [StringKey(0, true)]
        public StringKey Message;

        public PreventItemIndexEvent() { UseTypes = new List<string>(); }
        public PreventItemIndexEvent(StringKey message, params string[] useTypes)
        {
            Message = message;
            UseTypes = new List<string>();
            UseTypes.AddRange(useTypes);
        }
        protected PreventItemIndexEvent(PreventItemIndexEvent other) : this()
        {
            Message = other.Message;
            foreach (string useType in other.UseTypes)
                UseTypes.Add(useType);
        }
        public override GameEvent Clone() { return new PreventItemIndexEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Item)
            {
                if (UseTypes.Contains(context.Item.ID))
                {
                    if (Message.IsValid())
                        DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), context.User.GetDisplayName(false)));
                    context.CancelState.Cancel = true;
                }
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that prevents the character from taking action if they are below the specified HP threshold
    /// </summary>
    [Serializable]
    public class HPActionCheckEvent : BattleEvent
    {
        /// <summary>
        /// The HP threshold given as 1/HPFraction
        /// </summary>
        public int HPFraction;

        public HPActionCheckEvent() { }
        public HPActionCheckEvent(int hpFraction)
        {
            HPFraction = hpFraction;
        }
        protected HPActionCheckEvent(HPActionCheckEvent other)
        {
            HPFraction = other.HPFraction;
        }
        public override GameEvent Clone() { return new HPActionCheckEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.User.HP <= context.User.MaxHP / HPFraction)
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_HP_NEEDED").ToLocal(), context.User.GetDisplayName(false)));
                context.CancelState.Cancel = true;
            }
            yield break;
        }
    }


    /// <summary>
    /// Event that prevents the character from taking action 
    /// This event can only be used in statuses  
    /// </summary> 
    [Serializable]
    public class SleepEvent : BattleEvent
    {
        public override GameEvent Clone() { return new SleepEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.UsageSlot == BattleContext.FORCED_SLOT)
                yield break;

            if (context.ActionType == BattleActionType.Item)
            {
                ItemData entry = DataManager.Instance.GetItem(context.Item.ID);
                if (entry.ItemStates.Contains<CurerState>())
                    yield break;
            }

            if (((StatusEffect)owner).StatusStates.GetWithDefault<CountDownState>().Counter > 0)
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_ASLEEP").ToLocal(), context.User.GetDisplayName(false)));
            if (!context.ContextStates.Contains<SleepAttack>())
                context.CancelState.Cancel = true;
        }
    }

    /// <summary>
    /// Event that displays a message if the character does not have the BoundAttack context state
    /// </summary> 
    [Serializable]
    public class BoundEvent : BattleEvent
    {
        [StringKey(0, true)]
        public StringKey Message;

        public BoundEvent() { }
        public BoundEvent(StringKey message)
        {
            Message = message;
        }
        protected BoundEvent(BoundEvent other)
        {
            Message = other.Message;
        }
        public override GameEvent Clone() { return new BoundEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.UsageSlot == BattleContext.FORCED_SLOT)
                yield break;

            if (!context.ContextStates.Contains<BoundAttack>())
            {
                if (Message.IsValid())
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), context.User.GetDisplayName(false)));
                context.CancelState.Cancel = true;
            }
        }
    }


    /// <summary>
    /// Event that deals damage based on the value in the HPState status state and skips the character's turn
    /// This event can only be used on statuses 
    /// </summary>
    [Serializable]
    public class WrapTrapEvent : BattleEvent
    {
        /// <summary>
        /// The message displayed in the dungeon log 
        /// </summary>
        [StringKey(0, true)]
        public StringKey Message;

        /// <summary>
        /// The list of battle VFXs played when the character is trapped
        /// </summary>
        public List<AnimEvent> Anims;

        /// <summary>
        /// The animation index played when the character is trapped
        /// </summary>
        [FrameType(0, false)]
        public int CharAnim;

        public WrapTrapEvent() { Anims = new List<AnimEvent>(); }
        public WrapTrapEvent(StringKey message, int animType, params AnimEvent[] anims)
        {
            Message = message;
            CharAnim = animType;
            Anims = new List<AnimEvent>();
            Anims.AddRange(anims);
        }
        protected WrapTrapEvent(WrapTrapEvent other)
        {
            Message = other.Message;
            Anims = new List<AnimEvent>();
            foreach (AnimEvent anim in other.Anims)
                Anims.Add((AnimEvent)anim.Clone());
            CharAnim = other.CharAnim;
        }
        public override GameEvent Clone() { return new WrapTrapEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.UsageSlot == BattleContext.FORCED_SLOT)
                yield break;

            Character targetChar = ownerChar;
            StatusEffect status = (StatusEffect)owner;
            if (!targetChar.CharStates.Contains<MagicGuardState>())
            {
                if (Message.IsValid())
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), context.User.GetDisplayName(false)));

                CharAnimAction chargeAnim = new CharAnimAction(context.User.CharLoc, context.User.CharDir, CharAnim);
                yield return CoroutineManager.Instance.StartCoroutine(context.User.StartAnim(chargeAnim));

                foreach (AnimEvent anim in Anims)
                {
                    SingleCharContext singleContext = new SingleCharContext(targetChar);
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, singleContext));
                }

                int trapdmg = status.StatusStates.GetWithDefault<HPState>().HP;
                yield return CoroutineManager.Instance.StartCoroutine(targetChar.InflictDamage(trapdmg));
            }
            context.CancelState.Cancel = true;
        }
    }

    /// <summary>
    /// Used specifically for the freeze status
    /// </summary>
    [Serializable]
    public class FreezeEvent : BattleEvent
    {
        public override GameEvent Clone() { return new FreezeEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.UsageSlot == BattleContext.FORCED_SLOT)
                yield break;

            if (context.Data.Element == "fire")
            {
                yield return CoroutineManager.Instance.StartCoroutine(context.User.RemoveStatusEffect(((StatusEffect)owner).ID));
                yield break;
            }
            if (context.ActionType == BattleActionType.Item)
            {
                ItemData entry = DataManager.Instance.GetItem(context.Item.ID);
                if (entry.ItemStates.Contains<CurerState>())
                    yield break;
            }


            if (((StatusEffect)owner).StatusStates.GetWithDefault<CountDownState>().Counter > 0)
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_FROZEN").ToLocal(), context.User.GetDisplayName(false)));
            context.CancelState.Cancel = true;
        }
    }

    /// <summary>
    /// Event that thaws the character if the targeting move is a fire-type 
    /// Otherwise, that move will miss
    /// This event can only be used on statuses 
    /// </summary> 
    [Serializable]
    public class ThawEvent : BattleEvent
    {
        public override GameEvent Clone() { return new ThawEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Data.Element == "fire")
            {
                yield return CoroutineManager.Instance.StartCoroutine(context.Target.RemoveStatusEffect(((StatusEffect)owner).ID));
                yield break;
            }

            if (context.ContextStates.Contains<CureAttack>())
                yield break;

            if (context.Data.Category != BattleData.SkillCategory.None)
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_FROZEN").ToLocal(), context.Target.GetDisplayName(false)));
                context.AddContextStateMult<AccMult>(false, -1, 1);
            }
        }
    }

    /// <summary>
    /// Event that prevents the character from taking action if the ParalyzeState is recent
    /// This event can only be used on statuses 
    /// </summary> 
    [Serializable]
    public class ParalysisEvent : BattleEvent
    {
        /// <summary>
        /// The list of battle VFXs played if the condition is met
        /// </summary>
        public List<BattleAnimEvent> Anims;

        public ParalysisEvent()
        {
            Anims = new List<BattleAnimEvent>();
        }
        public ParalysisEvent(params BattleAnimEvent[] anims)
        {
            Anims = new List<BattleAnimEvent>();
            Anims.AddRange(anims);
        }
        protected ParalysisEvent(ParalysisEvent other)
        {
            Anims = new List<BattleAnimEvent>();
            foreach (BattleAnimEvent anim in other.Anims)
                Anims.Add((BattleAnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new ParalysisEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.UsageSlot == BattleContext.FORCED_SLOT)
                yield break;

            if (context.ActionType == BattleActionType.Item)
            {
                ItemData entry = DataManager.Instance.GetItem(context.Item.ID);
                if (entry.ItemStates.Contains<CurerState>())
                    yield break;
            }

            ParalyzeState para = ((StatusEffect)owner).StatusStates.GetWithDefault<ParalyzeState>();
            if (para.Recent)
            {
                foreach (BattleAnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_PARALYZED").ToLocal(), context.User.GetDisplayName(false)));
                yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(30));
                context.CancelState.Cancel = true;
            }

        }
    }


    /// <summary>
    /// Event that prevents the character from using the move if the specified status is not present
    /// </summary> 
    [Serializable]
    public class StatusNeededEvent : BattleEvent
    {
        /// <summary>
        /// The status ID to check for
        /// </summary> 
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string StatusID;

        /// <summary>
        /// The message displayed in the dungeon log if the conditon is met 
        /// </summary> 
        public StringKey Message;

        public StatusNeededEvent() { StatusID = ""; }
        public StatusNeededEvent(string statusID, StringKey msg)
        {
            StatusID = statusID;
            Message = msg;
        }
        protected StatusNeededEvent(StatusNeededEvent other)
        {
            StatusID = other.StatusID;
            Message = other.Message;
        }
        public override GameEvent Clone() { return new StatusNeededEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.User.GetStatusEffect(StatusID) == null)
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), context.User.GetDisplayName(false)));
                context.CancelState.Cancel = true;
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that prevents the character from using the move if the specified map status is not present
    /// </summary> 
    [Serializable]
    public class WeatherRequiredEvent : BattleEvent
    {
        /// <summary>
        /// The status IDs to check for
        /// </summary> 
        [JsonConverter(typeof(SkillListConverter))]
        [DataType(1, DataManager.DataType.MapStatus, false)]
        public List<string> AcceptedWeather;

        /// <summary>
        /// The message displayed in the dungeon log if the conditon is not met 
        /// </summary> 
        public StringKey Message;

        public WeatherRequiredEvent() { AcceptedWeather = new List<string>(); }
        public WeatherRequiredEvent(StringKey msg, params string[] statusIDs)
        {
            AcceptedWeather = new List<string>();
            AcceptedWeather.AddRange(statusIDs);
            Message = msg;
        }
        protected WeatherRequiredEvent(WeatherRequiredEvent other)
        {
            AcceptedWeather = new List<string>();
            AcceptedWeather.AddRange(other.AcceptedWeather);
            Message = other.Message;
        }
        public override GameEvent Clone() { return new WeatherRequiredEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            foreach (string weatherId in AcceptedWeather)
            {
                if (ZoneManager.Instance.CurrentMap.Status.ContainsKey(weatherId))
                    yield break;
            }

            DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), context.User.GetDisplayName(false)));
            context.CancelState.Cancel = true;
        }
    }



    /// <summary>
    /// Event that spawns an enemy from a fake item if someone attempted to use it.
    /// This should only be used in a MapEffectStep
    /// </summary>
    [Serializable]
    public class FakeItemBattleEvent : BattleEvent
    {
        /// <summary>
        /// The fake item mapped to an enemy
        /// </summary>
        [JsonConverter(typeof(ItemFakeTableConverter))]
        public Dictionary<ItemFake, MobSpawn> SpawnTable;

        public FakeItemBattleEvent()
        {
            SpawnTable = new Dictionary<ItemFake, MobSpawn>();
        }

        public FakeItemBattleEvent(Dictionary<ItemFake, MobSpawn> spawnTable)
        {
            this.SpawnTable = spawnTable;
        }

        public FakeItemBattleEvent(FakeItemBattleEvent other)
        {
            this.SpawnTable = new Dictionary<ItemFake, MobSpawn>();
            foreach (ItemFake fake in other.SpawnTable.Keys)
                this.SpawnTable.Add(fake, other.SpawnTable[fake].Copy());
        }

        public override GameEvent Clone() { return new FakeItemBattleEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            ItemFake fake = new ItemFake(context.Item.ID, context.Item.HiddenValue);
            MobSpawn spawn;
            if (SpawnTable.TryGetValue(fake, out spawn))
            {
                if (context.UsageSlot == BattleContext.FLOOR_ITEM_SLOT)
                {
                    int mapSlot = ZoneManager.Instance.CurrentMap.GetItem(context.User.CharLoc);
                    ZoneManager.Instance.CurrentMap.Items.RemoveAt(mapSlot);
                }
                else if (context.UsageSlot == BattleContext.EQUIP_ITEM_SLOT)
                    context.User.SilentDequipItem();
                else
                    context.User.MemberTeam.RemoveFromInv(context.UsageSlot);

                yield return CoroutineManager.Instance.StartCoroutine(FakeItemEvent.SpawnFake(context.User, context.Item, spawn));

                //cancel the operation
                context.CancelState.Cancel = true;
            }
        }
    }
}

