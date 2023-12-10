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

namespace PMDC.Dungeon
{
    /// <summary>
    /// Event that occurs before the user does an action
    /// </summary>
    [Serializable]
    public class PreActionEvent : BattleEvent
    {
        /// <summary>
        /// The status that will store the last used slot
        /// </summary>
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string LastSlotStatusID;
        
        /// <summary>
        /// The status that will store the last used move
        /// </summary>
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string LastMoveStatusID;
        
        /// <summary>
        /// The status that will store how many times the same move was used 
        /// </summary>
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string RepeatStatusID;

        public PreActionEvent()
        {
            LastSlotStatusID = "";
            LastMoveStatusID = "";
            RepeatStatusID = "";
        }
        
        public PreActionEvent(string lastSlotStatusID, string lastMoveStatusID, string repeatStatusID)
        {
            LastSlotStatusID = lastSlotStatusID;
            LastMoveStatusID = lastMoveStatusID;
            RepeatStatusID = repeatStatusID;
        }

        public PreActionEvent(PreActionEvent other)
        {
            LastSlotStatusID = other.LastSlotStatusID;
            LastMoveStatusID = other.LastMoveStatusID;
            RepeatStatusID = other.RepeatStatusID;
        }

        public override GameEvent Clone() { return new PreActionEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            //use the correct phys/special stats
            if (context.Data.Category == BattleData.SkillCategory.Physical)
                context.ContextStates.Set(new AttackerStat(context.User.Atk));
            else if (context.Data.Category == BattleData.SkillCategory.Magical)
                context.ContextStates.Set(new AttackerStat(context.User.MAtk));
            context.ContextStates.Set(new UserLevel(context.User.Level));

            if (context.ActionType == BattleActionType.Skill && context.UsageSlot > BattleContext.DEFAULT_ATTACK_SLOT && context.UsageSlot < CharData.MAX_SKILL_SLOTS)
            {
                StatusEffect lastSlotStatus = new StatusEffect(LastSlotStatusID);
                lastSlotStatus.LoadFromData();
                lastSlotStatus.StatusStates.GetWithDefault<SlotState>().Slot = context.UsageSlot;
                yield return CoroutineManager.Instance.StartCoroutine(context.User.AddStatusEffect(context.User, lastSlotStatus));

                StatusEffect testStatus = context.User.GetStatusEffect(LastMoveStatusID);
                StatusEffect repeatStatus = context.User.GetStatusEffect(RepeatStatusID);
                if (lastSlotStatus != null && repeatStatus != null &&
                    testStatus.StatusStates.GetWithDefault<IDState>().ID == context.Data.ID &&
                    repeatStatus.StatusStates.GetWithDefault<RecentState>() != null)
                {
                    //fall through
                }
                else
                {
                    //start new repetition
                    StatusEffect newRepeatStatus = new StatusEffect(RepeatStatusID);
                    newRepeatStatus.LoadFromData();
                    yield return CoroutineManager.Instance.StartCoroutine(context.User.AddStatusEffect(context.User, newRepeatStatus));
                }

                StatusEffect lastMoveStatus = new StatusEffect(LastMoveStatusID);
                lastMoveStatus.LoadFromData();
                lastMoveStatus.StatusStates.GetWithDefault<IDState>().ID = context.Data.ID;
                yield return CoroutineManager.Instance.StartCoroutine(context.User.AddStatusEffect(context.User, lastMoveStatus));
            }
            else
            {
                StatusEffect repeatStatus = context.User.GetStatusEffect(RepeatStatusID);
                if (repeatStatus != null)
                    repeatStatus.StatusStates.Remove<RecentState>();
            }
        }

        [OnDeserialized]
        internal void OnDeserializedMethod(StreamingContext context)
        {
            //TODO: remove on v1.1
            if (Serializer.OldVersion < new Version(0, 7, 16))
            {
                LastSlotStatusID = "last_used_move_slot";
                LastMoveStatusID = "last_used_move";
                RepeatStatusID = "times_move_used";
            }
        }
    }

    
    /// <summary>
    /// Event that occurs before the target takes the hit
    /// This sets the target's level which defense stat they will use 
    /// </summary>
    [Serializable]
    public class PreHitEvent : BattleEvent
    {

        public override GameEvent Clone() { return new PreHitEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Data.Category == BattleData.SkillCategory.Physical)
                context.ContextStates.Set(new TargetStat(context.Target.Def));
            else if (context.Data.Category == BattleData.SkillCategory.Magical)
                context.ContextStates.Set(new TargetStat(context.Target.MDef));
            context.ContextStates.Set(new TargetLevel(context.Target.Level));

            yield break;
        }
    }
    
    /// <summary>
    /// Event that groups multiple battle events into one event
    /// </summary>
    [Serializable]
    public class MultiBattleEvent : BattleEvent
    {
        /// <summary>
        /// The list of battle events to apply
        /// </summary>
        public List<BattleEvent> BaseEvents;

        public MultiBattleEvent() { BaseEvents = new List<BattleEvent>(); }
        public MultiBattleEvent(params BattleEvent[] effects)
            : this()
        {
            foreach (BattleEvent battleEffect in effects)
                BaseEvents.Add(battleEffect);
        }
        protected MultiBattleEvent(MultiBattleEvent other)
            : this()
        {
            foreach (BattleEvent battleEffect in other.BaseEvents)
                BaseEvents.Add((BattleEvent)battleEffect.Clone());
        }
        public override GameEvent Clone() { return new MultiBattleEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            foreach (BattleEvent battleEffect in BaseEvents)
                yield return CoroutineManager.Instance.StartCoroutine(battleEffect.Apply(owner, ownerChar, context));
        }
    }

    /// <summary>
    /// Event that calculates whether the target is hit, taking consideration into the move accuracy,
    /// the user's accuracy boost, whether the moved missed last turn, etc.
    /// </summary>
    [Serializable]
    public class AttemptHitEvent : BattleEvent
    {
        public AttemptHitEvent() { }
        public override GameEvent Clone() { return new AttemptHitEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            //see if it hits
            int accMod = context.GetContextStateMult<AccMult>().Multiply(0);
            bool hit = false;
            if (accMod == -1) //don't hit, don't say anything
            {
                GameManager.Instance.BattleSE("DUN_Miss_2");
                DungeonScene.Instance.Missed(context.Target.CharLoc);
            }
            else
            {
                if (context.Data.HitRate == -1)
                    hit = true;
                else if (context.GetContextStateMult<AccMult>().IsNeutralized())
                    hit = false;
                else
                {
                    int acc = context.Data.HitRate;
                    HitRateLevelTableState table = DataManager.Instance.UniversalEvent.UniversalStates.GetWithDefault<HitRateLevelTableState>();
                    acc = table.ApplyAccuracyMod(acc, context.GetContextStateInt<UserAccuracyBoost>(0));
                    acc /= table.AccuracyLevels[-table.MinAccuracy];
                    acc = table.ApplyEvasionMod(acc, context.GetContextStateInt<TargetEvasionBoost>(0));
                    acc /= table.EvasionLevels[-table.MinEvasion];
                    acc *= context.User.Speed;
                    acc /= context.Target.Speed;
                    acc = context.GetContextStateMult<AccMult>().Multiply(acc);

                    //MustHitNext is to ensure that no single character can miss twice in a row
                    if (context.User.MustHitNext || DataManager.Instance.Save.Rand.Next(0, 100) < acc)
                        hit = true;
                    context.User.MustHitNext = !hit;
                }

                if (hit)
                {
                    if (context.User.MemberTeam == DungeonScene.Instance.ActiveTeam)
                    {
                        if (context.ActionType == BattleActionType.Skill && context.Data.ID != DataManager.Instance.DefaultSkill)
                            context.Target.EXPMarked = true;
                        else if (context.ActionType == BattleActionType.Item)
                            context.Target.EXPMarked = true;
                        else if (context.ActionType == BattleActionType.Throw)
                        {
                            ItemData entry = DataManager.Instance.GetItem(context.Item.ID);
                            if (entry.ItemStates.Contains<RecruitState>())
                            {
                                //failed recruitment items dont count
                            }
                            else if (context.ContextStates.Contains<ItemCaught>())
                            {
                                //items that were caught don't count
                            }
                            else
                                context.Target.EXPMarked = true;
                        }
                    }
                    //play the hit animation here
                    yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.ProcessEndAnim(context.User, context.Target, context.Data));

                    context.Hit = true;
                }
                else
                {
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_MISS").ToLocal(), context.Target.GetDisplayName(false)));
                    GameManager.Instance.BattleSE("DUN_Miss");
                    DungeonScene.Instance.Missed(context.Target.CharLoc);
                }
            }

        }
    }

    /// <summary>
    /// Event that occurs when a move is used from the menu
    /// This sets the battle context data from the move and checks if the user has enough PP and the move is not disabled 
    /// </summary>
    [Serializable]
    public class PreSkillEvent : BattleEvent
    {
        public override GameEvent Clone() { return new PreSkillEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType != BattleActionType.Skill)
                yield break;
            if (context.UsageSlot == BattleContext.FORCED_SLOT)
                yield break;

            string usageIndex = DataManager.Instance.DefaultSkill;
            if (context.UsageSlot > BattleContext.DEFAULT_ATTACK_SLOT && context.UsageSlot < CharData.MAX_SKILL_SLOTS)
                usageIndex = context.User.Skills[context.UsageSlot].Element.SkillNum;

            SkillData entry = DataManager.Instance.GetSkill(usageIndex);
            context.Data = new BattleData(entry.Data);

            context.Data.ID = usageIndex;
            context.Data.DataType = DataManager.DataType.Skill;
            context.Explosion = new ExplosionData(entry.Explosion);
            context.HitboxAction = entry.HitboxAction.Clone();
            context.Item = new InvItem();
            context.Strikes = entry.Strikes;

            context.StartDir = context.User.CharDir;


            if (usageIndex == DataManager.Instance.DefaultSkill)
                context.SetActionMsg(Text.FormatGrammar(new StringKey("MSG_ATTACK_USE").ToLocal(), context.User.GetDisplayName(false)), true);
            else
                context.SetActionMsg(Text.FormatGrammar(new StringKey("MSG_SKILL_USE").ToLocal(), context.User.GetDisplayName(false), entry.GetIconName()));
            
            if (context.UsageSlot > BattleContext.DEFAULT_ATTACK_SLOT && context.UsageSlot < CharData.MAX_SKILL_SLOTS)
            {
                if (context.User.Skills[context.UsageSlot].Element.Charges <= 0)
                {
                    if (context.User == DungeonScene.Instance.FocusedCharacter)
                        DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_NO_MORE_CHARGES").ToLocal()), false, true);
                    context.CancelState.Cancel = true;
                }
                else if (context.User.Skills[context.UsageSlot].Element.Sealed)
                {
                    if (context.User == DungeonScene.Instance.FocusedCharacter)
                        DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_SKILL_SEALED").ToLocal()), false, true);
                    context.CancelState.Cancel = true;
                }
            }

            yield break;
        }
    }

    /// <summary>
    /// Event that occurs when an item is used from the menu
    /// This sets the battle context data from the item and checks if the item is sticky
    /// </summary>
    [Serializable]
    public class PreItemEvent : BattleEvent
    {
        public override GameEvent Clone() { return new PreItemEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType != BattleActionType.Item)
                yield break;
            if (context.UsageSlot == BattleContext.FORCED_SLOT)
                yield break;

            //while attack, use, and throw, will all use the same battle contexts and hitboxes,
            //they will still have different Execution methods, as well as EndEffects
            //Use Item will have its own method, which will have its own charge-up step to play the sound/animation for eating/drinking/using
            //it will still have a BeforeAction and OnAction method call

            context.StartDir = context.User.CharDir;

            InvItem item = null;
            if (context.UsageSlot > BattleContext.EQUIP_ITEM_SLOT)//item in inventory
                item = ((ExplorerTeam)context.User.MemberTeam).GetInv(context.UsageSlot);
            else if (context.UsageSlot == BattleContext.EQUIP_ITEM_SLOT)
                item = context.User.EquippedItem;
            else if (context.UsageSlot == BattleContext.FLOOR_ITEM_SLOT)
            {
                int mapSlot = ZoneManager.Instance.CurrentMap.GetItem(context.User.CharLoc);
                MapItem mapItem = ZoneManager.Instance.CurrentMap.Items[mapSlot];
                item = mapItem.MakeInvItem();
            }

            ItemData entry = DataManager.Instance.GetItem(item.ID);
            context.Data = new BattleData(entry.UseEvent);
            context.Data.ID = item.GetID();
            context.Data.DataType = DataManager.DataType.Item;
            context.Explosion = new ExplosionData(entry.Explosion);
            context.Strikes = 1;
            context.Item = new InvItem(item);
            if (entry.MaxStack > 1)
                context.Item.Amount = 1;
            context.HitboxAction = entry.UseAction.Clone();
            switch (entry.UsageType)
            {
                case ItemData.UseType.Eat:
                    {
                        context.SetActionMsg(Text.FormatGrammar(new StringKey("MSG_USE_EAT").ToLocal(), context.User.GetDisplayName(false), item.GetDisplayName()));
                        break;
                    }
                case ItemData.UseType.Drink:
                    {
                        context.SetActionMsg(Text.FormatGrammar(new StringKey("MSG_USE_DRINK").ToLocal(), context.User.GetDisplayName(false), item.GetDisplayName()));
                        break;
                    }
                case ItemData.UseType.Learn:
                    {
                        context.SetActionMsg(Text.FormatGrammar(new StringKey("MSG_USE_OPERATE").ToLocal(), context.User.GetDisplayName(false), item.GetDisplayName()));
                        break;
                    }
                case ItemData.UseType.Use:
                    {
                        context.SetActionMsg(Text.FormatGrammar(new StringKey("MSG_USE").ToLocal(), context.User.GetDisplayName(false), item.GetDisplayName()));
                        break;
                    }
            }

            if (item.Cursed)
            {
                GameManager.Instance.BattleSE("DUN_Sticky");
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_USE_CURSED").ToLocal(), item.GetDisplayName()), false, true);
                context.CancelState.Cancel = true;
            }

            yield break;
        }
    }


    /// <summary>
    /// Event that occurs when an item is thrown from the menu
    /// This sets different battle context data depending if the item is sticky, whether the item is thrown in an arc, etc
    /// </summary>
    [Serializable]
    public class PreThrowEvent : BattleEvent
    {
        public override GameEvent Clone() { return new PreThrowEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType != BattleActionType.Throw)
                yield break;
            if (context.UsageSlot == BattleContext.FORCED_SLOT)
                yield break;
            //Throw Item will have its own method, which will have its own charge-up step for throwing
            //hardcoded into the process is the removal of the item
            //the item class will have its own throw-item data for how the item will fly, which will be used as the char action on execution
            //all given targets will receive the effect of the item (if imported), or a differently specified effect (blast seed)
            //if the item hits no one, it must fall to the floor
            //if the item hits someone, and it's an AMMO type, it must fall to the floor
            //-or, it must not fall to the floor no matter what
            //The hitbox logic will take care of this?  If it does, it needs to be flexible to accomodate all specified cases
            //whenever an item hits a target, and they can catch, the target will catch the item
            //otherwise, the item will inflict its effect on the target
            //keep note: some late-game enemies will appear while holding items (chests?)
            //pierce-throwing makes items uncatchable
            //Friendly Fire: default off; when turned on, attacks that hit enemies will also hit allies and vice-versa
            //Friendly Item Fire: default on; turning it on will have thrown items hit allies as well as enemies

            context.StartDir = context.User.CharDir;

            //while attack, use, and throw, will all use the same battle contexts and hitboxes,
            //they will still have different Execution methods, as well as EndEffects
            InvItem item = null;
            if (context.UsageSlot > BattleContext.EQUIP_ITEM_SLOT)//item in inventory
                item = ((ExplorerTeam)context.User.MemberTeam).GetInv(context.UsageSlot);
            else if (context.UsageSlot == BattleContext.EQUIP_ITEM_SLOT)
                item = context.User.EquippedItem;
            else if (context.UsageSlot == BattleContext.FLOOR_ITEM_SLOT)
            {
                int mapSlot = ZoneManager.Instance.CurrentMap.GetItem(context.User.CharLoc);
                MapItem mapItem = ZoneManager.Instance.CurrentMap.Items[mapSlot];
                item = mapItem.MakeInvItem();
            }

            ItemData entry = DataManager.Instance.GetItem(item.ID);
            bool defaultDmg = false;
            bool catchable = true;


            if (entry.UsageType == ItemData.UseType.None || entry.UsageType == ItemData.UseType.Use || entry.UsageType == ItemData.UseType.Learn || entry.UsageType == ItemData.UseType.Box || entry.UsageType == ItemData.UseType.UseOther)
                defaultDmg = true;
            else if (entry.ItemStates.Contains<RecruitState>())
                catchable = false;
            //otherwise normally catchable, but depends on the target...

            if (item.Cursed)
                defaultDmg = true;

            if (defaultDmg)
            {
                //these just do damage(create a custom effect in stead of the item's effect)
                context.Data = new BattleData();
                context.Data.Element = DataManager.Instance.DefaultElement;
                context.Data.ID = item.GetID();
                context.Data.DataType = DataManager.DataType.Item;

                context.Data.Category = BattleData.SkillCategory.Physical;
                context.Data.SkillStates.Set(new BasePowerState(30));
                context.Data.OnHits.Add(-1, new DamageFormulaEvent());
            }
            else
            {
                context.Data = new BattleData(entry.UseEvent);
                context.Data.ID = item.GetID();
                context.Data.DataType = DataManager.DataType.Item;
            }

            if (catchable)
                context.Data.BeforeExplosions.Add(-5, new CatchItemSplashEvent());
            if (!entry.BreakOnThrow)
                context.Data.AfterActions.Add(-1, new LandItemEvent());
            context.Item = new InvItem(item);
            if (entry.MaxStack > 1)
                context.Item.Amount = 1;
            context.Strikes = 1;
            //create the action from scratch
            if (entry.ArcThrow)
            {
                ThrowAction action = new ThrowAction();
                action.CharAnimData = new CharAnimFrameType(42);//Rotate
                action.Coverage = ThrowAction.ArcCoverage.WideAngle;
                action.TargetAlignments = Alignment.Foe;
                action.Anim = new AnimData(entry.ThrowAnim);
                action.ItemSprite = DataManager.Instance.GetItem(item.ID).Sprite;
                BattleFX newFX = new BattleFX();
                newFX.Sound = "DUN_Throw_Start";
                action.PreActions.Add(newFX);
                action.ActionFX.Sound = "DUN_Throw_Arc";
                action.Speed = 10;
                action.Range = 6;
                context.HitboxAction = action;
                context.Explosion = new ExplosionData(entry.Explosion);
            }
            else
            {
                ProjectileAction action = new ProjectileAction();
                action.CharAnimData = new CharAnimFrameType(42);//Rotate
                action.TargetAlignments = Alignment.Friend | Alignment.Foe;
                action.Anim = new AnimData(entry.ThrowAnim);
                action.ItemSprite = DataManager.Instance.GetItem(item.ID).Sprite;
                BattleFX newFX = new BattleFX();
                newFX.Sound = "DUN_Throw_Start";
                action.PreActions.Add(newFX);
                if (entry.ItemStates.Contains<AmmoState>())
                    action.ActionFX.Sound = "DUN_Throw_Spike";
                else
                    action.ActionFX.Sound = "DUN_Throw_Something";
                action.Speed = 14;
                action.Range = 8;
                action.StopAtHit = true;
                action.StopAtWall = true;
                action.HitTiles = true;
                context.HitboxAction = action;
                context.Explosion = new ExplosionData(entry.Explosion);
                context.Explosion.TargetAlignments = Alignment.Friend | Alignment.Foe | Alignment.Self;
            }

            context.SetActionMsg(Text.FormatGrammar(new StringKey("MSG_THROW").ToLocal(), context.User.GetDisplayName(false), context.Item.GetDisplayName()));

            if (context.UsageSlot == BattleContext.EQUIP_ITEM_SLOT && context.User.EquippedItem.Cursed && !context.User.CanRemoveStuck)
            {
                GameManager.Instance.BattleSE("DUN_Sticky");
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_THROW_CURSED").ToLocal(), context.User.GetDisplayName(false), item.GetDisplayName()), false, true);
                context.CancelState.Cancel = true;
            }
        }
    }


    /// <summary>
    /// Event that converts a SingleCharEvent to a battle event
    /// </summary>
    [Serializable]
    public class BattlelessEvent : BattleEvent
    {
        /// <summary>
        /// The SingleCharEvent being converted
        /// </summary>
        public SingleCharEvent BaseEvent;
        
        /// <summary>
        /// Whether to affect the targer or user
        /// </summary>
        public bool AffectTarget;

        public BattlelessEvent() { }
        public BattlelessEvent(bool affectTarget, SingleCharEvent effect)
        {
            AffectTarget = affectTarget;
            BaseEvent = effect;
        }
        protected BattlelessEvent(BattlelessEvent other)
        {
            BaseEvent = (SingleCharEvent)other.BaseEvent.Clone();
            AffectTarget = other.AffectTarget;
        }
        public override GameEvent Clone() { return new BattlelessEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            SingleCharContext singleContext = new SingleCharContext(AffectTarget ? context.Target : context.User);
            yield return CoroutineManager.Instance.StartCoroutine(BaseEvent.Apply(owner, ownerChar, singleContext));
        }
    }

    /// <summary>
    /// Event that displays the dialogue when interacting with an NPC
    /// This event should usually be placed inside the NPC's MobSpawnInteractable spawn feature
    /// </summary>
    [Serializable]
    public class NpcDialogueBattleEvent : BattleEvent
    {
        /// <summary>
        /// The message displayed when interacting with the NPC
        /// </summary>
        public StringKey Message;
        
        /// <summary>
        /// Whether to display the speaker portrait
        /// </summary>
        public bool HideSpeaker;
        
        /// <summary>
        /// The portrait emotion
        /// </summary>
        public EmoteStyle Emote;

        public NpcDialogueBattleEvent() { }
        public NpcDialogueBattleEvent(StringKey message) : this(message, false) { }
        public NpcDialogueBattleEvent(StringKey message, bool hideSpeaker)
        {
            Message = message;
            HideSpeaker = hideSpeaker;
        }
        protected NpcDialogueBattleEvent(NpcDialogueBattleEvent other)
        {
            Message = other.Message;
            HideSpeaker = other.HideSpeaker;
            Emote = other.Emote;
        }
        public override GameEvent Clone() { return new NpcDialogueBattleEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (DataManager.Instance.CurrentReplay == null)
            {
                if (HideSpeaker)
                    yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.SetDialogue(Message.ToLocal()));
                else
                {
                    Dir8 oldDir = context.Target.CharDir;
                    context.Target.CharDir = context.User.CharDir.Reverse();
                    Character target = context.Target;
                    yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.SetDialogue(target.Appearance, target.GetDisplayName(true), Emote, true, Message.ToLocal()));
                    context.Target.CharDir = oldDir;
                }
                context.CancelState.Cancel = true;
            }
        }
    }

    /// <summary>
    /// Event that logs a StringKey message to the dungeon log 
    /// </summary>
    [Serializable]
    public class BattleLogBattleEvent : BattleEvent
    {
        /// <summary>
        /// The message displayed in the dungeon log 
        /// </summary>
        public StringKey Message;
        
        /// <summary>
        /// Whether to have short delay after displaying the message
        /// </summary>
        public bool Delay;
        
        /// <summary>
        /// Whether to use the target or user name when formatting the message
        /// </summary>
        public bool UseTarget;

        public BattleLogBattleEvent() { }
        public BattleLogBattleEvent(StringKey message) : this(message, false) { }
        public BattleLogBattleEvent(StringKey message, bool delay) : this(message, delay, false) { }
        public BattleLogBattleEvent(StringKey message, bool delay, bool useTarget)
        {
            Message = message;
            Delay = delay;
            UseTarget = useTarget;
        }
        protected BattleLogBattleEvent(BattleLogBattleEvent other)
        {
            Message = other.Message;
            Delay = other.Delay;
            UseTarget = other.UseTarget;
        }
        public override GameEvent Clone() { return new BattleLogBattleEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (UseTarget)
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), context.Target.GetDisplayName(false)));
            else
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), context.User.GetDisplayName(false)));
            if (Delay)
                yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(10));
        }
    }

    /// <summary>
    /// Event that logs a string message to the dungeon log
    /// </summary>
    [Serializable]
    public class FormatLogLocalEvent : BattleEvent
    {
        /// <summary>
        /// The message displayed in the dungeon log 
        /// </summary>
        public string Message;

        /// <summary>
        /// Whether to have short delay after displaying the message
        /// </summary>
        public bool Delay;

        public FormatLogLocalEvent() { }
        public FormatLogLocalEvent(string message) : this(message, false) { }
        public FormatLogLocalEvent(string message, bool delay)
        {
            Message = message;
            Delay = delay;
        }
        protected FormatLogLocalEvent(FormatLogLocalEvent other)
        {
            Message = other.Message;
            Delay = other.Delay;
        }
        public override GameEvent Clone() { return new FormatLogLocalEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            DungeonScene.Instance.LogMsg(Message);
            if (Delay)
                yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(10));
        }
    }

    /// <summary>
    /// Event that applies a VFX on a character
    /// </summary>
    [Serializable]
    public class BattleAnimEvent : BattleEvent
    {
        /// <summary>
        /// The particle VFX 
        /// </summary>
        public FiniteEmitter Emitter;

        //TODO: make this into BattleFX?
        /// <summary>
        /// The sound effect of the VFX
        /// </summary>
        [Sound(0)]
        public string Sound;
        
        /// <summary>
        /// Whether to apply the VFX on the target or user
        /// </summary>
        public bool AffectTarget;
        
        /// <summary>
        /// The delay after the VFX
        /// </summary>
        public int Delay;

        public BattleAnimEvent()
        {
            Emitter = new EmptyFiniteEmitter();
        }
        public BattleAnimEvent(FiniteEmitter emitter, string sound, bool affectTarget) : this(emitter, sound, affectTarget, 0) { }
        public BattleAnimEvent(FiniteEmitter emitter, string sound, bool affectTarget, int delay)
        {
            Emitter = emitter;
            Sound = sound;
            AffectTarget = affectTarget;
            Delay = delay;
        }
        protected BattleAnimEvent(BattleAnimEvent other)
        {
            Emitter = (FiniteEmitter)other.Emitter.Clone();
            Sound = other.Sound;
            AffectTarget = other.AffectTarget;
            Delay = other.Delay;
        }
        public override GameEvent Clone() { return new BattleAnimEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = (AffectTarget ? context.Target : context.User);

            GameManager.Instance.BattleSE(Sound);
            if (!target.Unidentifiable)
            {
                FiniteEmitter endEmitter = (FiniteEmitter)Emitter.Clone();
                endEmitter.SetupEmit(target.MapLoc, target.MapLoc, target.CharDir);
                DungeonScene.Instance.CreateAnim(endEmitter, DrawLayer.NoDraw);
            }
            yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(Delay));
        }
    }

    /// <summary>
    /// Event that reflects all damaging moves to nearby foes
    /// </summary>
    [Serializable]
    public class ReflectAllEvent : BattleEvent
    {

        /// <summary>
        /// The numerator of the damage reflected
        /// </summary>
        public int Numerator;
        
        /// <summary>
        /// The denominator of the damage reflected
        /// </summary>
        public int Denominator;
        
        /// <summary>
        /// Enemies within the radius will be dealt the reflected damage
        /// </summary>
        public int Range;
        
        /// <summary>
        /// The list of battle VFXs played if the condition is met
        /// </summary>
        public List<BattleAnimEvent> Anims;

        public ReflectAllEvent() { Anims = new List<BattleAnimEvent>(); }
        public ReflectAllEvent(int numerator, int denominator, int range, params BattleAnimEvent[] anims)
        {
            Numerator = numerator;
            Denominator = denominator;
            Range = range;
            Anims = new List<BattleAnimEvent>();
            Anims.AddRange(anims);
        }
        protected ReflectAllEvent(ReflectAllEvent other)
        {
            Numerator = other.Numerator;
            Denominator = other.Denominator;
            Range = other.Range;
            Anims = new List<BattleAnimEvent>();
            foreach (BattleAnimEvent anim in other.Anims)
                Anims.Add((BattleAnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new ReflectAllEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            BattleData.SkillCategory category = ((StatusEffect)owner).StatusStates.GetWithDefault<CategoryState>().Category;
            int damage = context.GetContextStateInt<DamageDealt>(0);
            if ((category == BattleData.SkillCategory.None || context.Data.Category == category) && damage > 0 && DungeonScene.Instance.GetMatchup(context.User, context.Target) != Alignment.Self)
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_REFLECT_ALL").ToLocal()));

                foreach (BattleAnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                int dmg = damage * Numerator / Denominator;

                List<Character> targets = AreaAction.GetTargetsInArea(context.Target, context.Target.CharLoc, Alignment.Foe, Range);

                for (int ii = 0; ii < targets.Count; ii++)
                {
                    int charDmg = dmg;

                    if (targets[ii].CharStates.Contains<HitAndRunState>())
                        charDmg /= 4;

                    yield return CoroutineManager.Instance.StartCoroutine(targets[ii].InflictDamage(charDmg));
                }
            }
        }
    }
    
    /// <summary>
    /// Converts the character type's to resist incoming moves
    /// </summary>
    [Serializable]
    public class Conversion2Event : BattleEvent
    {
        public override GameEvent Clone() { return new Conversion2Event(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            List<string> elements = new List<string>();
            string element = DataManager.Instance.DefaultElement;
            foreach (string key in DataManager.Instance.DataIndices[DataManager.DataType.Element].GetOrderedKeys(true))
            {
                int effectiveness = PreTypeEvent.CalculateTypeMatchup(context.Data.Element, key);
                if (effectiveness == PreTypeEvent.N_E)
                {
                    element = key;
                    break;
                }
                else if (effectiveness == PreTypeEvent.NVE)
                    elements.Add(key);
            }

            if (element == DataManager.Instance.DefaultElement && elements.Count > 0)
                element = elements[DataManager.Instance.Save.Rand.Next(0, elements.Count)];

            if (element != DataManager.Instance.DefaultElement)
                yield return CoroutineManager.Instance.StartCoroutine(context.Target.ChangeElement(element, DataManager.Instance.DefaultElement));
        }
    }
    
    /// <summary>
    /// Converts the character type's to the move last used 
    /// </summary>
    [Serializable]
    public class ConversionEvent : BattleEvent
    {
        /// <summary>
        /// Whether to affect the target or user
        /// </summary>
        public bool AffectTarget;

        public ConversionEvent() { }
        public ConversionEvent(bool affectTarget)
        {
            AffectTarget = affectTarget;
        }
        protected ConversionEvent(ConversionEvent other)
        {
            AffectTarget = other.AffectTarget;
        }
        public override GameEvent Clone() { return new ConversionEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = (AffectTarget ? context.Target : context.User);
            if (target.Dead)
                yield break;

            if (context.Data.Element != DataManager.Instance.DefaultElement && !(target.Element1 == context.Data.Element && target.Element2 == DataManager.Instance.DefaultElement))
            {
                yield return CoroutineManager.Instance.StartCoroutine(target.ChangeElement(context.Data.Element, DataManager.Instance.DefaultElement));
            }
        }
    }




    [Serializable]
    public class StanceChangeEvent : BattleEvent
    {
        /// <summary>
        /// The required species in order for this ability to activiate
        /// </summary>
        [JsonConverter(typeof(MonsterConverter))]
        [DataType(0, DataManager.DataType.Monster, false)]
        public string ReqSpecies;

        /// <summary>
        /// The move that changes the character into its Defense form
        /// </summary>
        [DataType(0, DataManager.DataType.Skill, false)]
        public string DefenseSkill;
        
        /// <summary>
        /// The defense form ID of the species
        /// </summary>
        public int DefenseForme;
        
        /// <summary>
        /// The attack form ID of the species
        /// </summary>
        public int AttackForme;

        public StanceChangeEvent() { ReqSpecies = ""; DefenseSkill = ""; }
        public StanceChangeEvent(string reqSpecies, string defenseSkill, int defenseForme, int attackForme)
        {
            ReqSpecies = reqSpecies;
            DefenseSkill = defenseSkill;
            DefenseForme = defenseForme;
            AttackForme = attackForme;
        }
        protected StanceChangeEvent(StanceChangeEvent other) : this()
        {
            ReqSpecies = other.ReqSpecies;
            DefenseSkill = other.DefenseSkill;
            DefenseForme = other.DefenseForme;
            AttackForme = other.AttackForme;
        }
        public override GameEvent Clone() { return new StanceChangeEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.User.CurrentForm.Species != ReqSpecies)
                yield break;

            if (context.ActionType == BattleActionType.Skill && context.UsageSlot > BattleContext.DEFAULT_ATTACK_SLOT && context.UsageSlot < CharData.MAX_SKILL_SLOTS)
            {
                //get the forme it should be in
                int forme = -1;

                if (context.Data.Category == BattleData.SkillCategory.Physical || context.Data.Category == BattleData.SkillCategory.Magical)
                {
                    forme = AttackForme;
                }
                else if (context.Data.ID == DefenseSkill)
                {
                    forme = DefenseForme;
                }

                if (forme != -1 && forme != context.User.CurrentForm.Form)
                {
                    //transform it
                    context.User.Transform(new MonsterID(context.User.CurrentForm.Species, forme, context.User.CurrentForm.Skin, context.User.CurrentForm.Gender));
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_FORM_CHANGE").ToLocal(), context.User.GetDisplayName(false)));
                }
            }
            yield break;
        }
    }

    [Serializable]
    public abstract class InvokeBattleEvent : BattleEvent
    {
        protected abstract BattleContext CreateContext(GameEventOwner owner, Character ownerChar, BattleContext context);
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            BattleContext newContext = CreateContext(owner, ownerChar, context);
            if (newContext == null)
                yield break;

            //beforetryaction and beforeAction need to distinguish forced effects vs willing effects for all times it's triggered
            //as a forced attack, preprocessaction also should not factor in confusion dizziness
            //examples where the distinction matters:
            //-counting down
            //-confusion dizziness
            //-certain kinds of status-based move prevention
            //-forced actions (charging moves, rampage moves, etc)

            yield return CoroutineManager.Instance.StartCoroutine(newContext.User.BeforeTryAction(newContext));
            if (newContext.CancelState.Cancel) { yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.CancelWait(newContext.User.CharLoc)); yield break; }
            yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.PreProcessAction(newContext));

            //Handle Use
            yield return CoroutineManager.Instance.StartCoroutine(newContext.User.BeforeAction(newContext));
            if (newContext.CancelState.Cancel) { yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.CancelWait(newContext.User.CharLoc)); yield break; }

            newContext.PrintActionMsg();

            yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.ExecuteAction(newContext));
            if (newContext.CancelState.Cancel) { yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.CancelWait(newContext.User.CharLoc)); yield break; }
            yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.RepeatActions(newContext));
        }
    }

    [Serializable]
    public abstract class InvokedMoveEvent : InvokeBattleEvent
    {
        protected abstract string GetInvokedMove(GameEventOwner owner, BattleContext context);
        protected override BattleContext CreateContext(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            string moveID = "";

            if (context.UsageSlot != BattleContext.FORCED_SLOT)
                moveID = GetInvokedMove(owner, context);

            if (!String.IsNullOrEmpty(moveID))
            {
                SkillData entry = DataManager.Instance.GetSkill(moveID);

                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_SKILL_CALL").ToLocal(), entry.GetIconName()));

                if (!entry.Released)
                {
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_SKILL_UNFINISHED").ToLocal()));
                    return null;
                }

                BattleContext newContext = new BattleContext(BattleActionType.Skill);
                newContext.User = context.User;
                newContext.UsageSlot = BattleContext.FORCED_SLOT;

                newContext.StartDir = newContext.User.CharDir;

                //fill effects
                newContext.Data = new BattleData(entry.Data);
                
                newContext.Data.ID = moveID;
                newContext.Data.DataType = DataManager.DataType.Skill;
                newContext.Explosion = new ExplosionData(entry.Explosion);
                newContext.HitboxAction = entry.HitboxAction.Clone();
                newContext.Strikes = entry.Strikes;
                newContext.Item = new InvItem();
                //don't set move message, just directly give the message of what the move turned into

                return newContext;
            }
            else
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_SKILL_FAILED").ToLocal()));

            return null;
        }
    }

    /// <summary>
    /// Event that makes the user use the target's strongest base power move
    /// </summary>
    [Serializable]
    public class StrongestMoveEvent : InvokedMoveEvent
    {
        public override GameEvent Clone() { return new StrongestMoveEvent(); }

        protected override string GetInvokedMove(GameEventOwner owner, BattleContext context)
        {
            int recordSlot = -1;
            int recordPower = -1;
            for (int ii = 0; ii < context.Target.Skills.Count; ii++)
            {
                if (!String.IsNullOrEmpty(context.Target.Skills[ii].Element.SkillNum))
                {
                    SkillData entry = DataManager.Instance.GetSkill(context.Target.Skills[ii].Element.SkillNum);

                    int basePower = 0;
                    if (entry.Data.Category == BattleData.SkillCategory.Status)
                        basePower = -1;
                    else
                    {
                        BasePowerState state = entry.Data.SkillStates.GetWithDefault<BasePowerState>();
                        if (state != null)
                            basePower = state.Power;
                    }
                    if (basePower > recordPower)
                    {
                        recordSlot = ii;
                        recordPower = basePower;
                    }
                }
            }

            if (recordSlot > -1)
                return context.Target.Skills[recordSlot].Element.SkillNum;
            else
                return "";
        }
    }


    /// <summary>
    /// Event that makes the user randomly use any move.
    /// </summary>
    [Serializable]
    public class RandomMoveEvent : InvokedMoveEvent
    {
        public override GameEvent Clone() { return new RandomMoveEvent(); }

        protected override string GetInvokedMove(GameEventOwner owner, BattleContext context)
        {
            List<string> releasedMoves = new List<string>();
            foreach (string key in DataManager.Instance.DataIndices[DataManager.DataType.Skill].GetOrderedKeys(true))
            {
                if (key == DataManager.Instance.DefaultSkill)
                    continue;
                if (DataManager.Instance.DataIndices[DataManager.DataType.Skill].Get(key).Released)
                    releasedMoves.Add(key);
            }

            int randIndex = DataManager.Instance.Save.Rand.Next(releasedMoves.Count);
            return releasedMoves[randIndex];
        }
    }

    /// <summary>
    /// User will more likely use a random move that benefits the team
    /// </summary>
    [Serializable]
    public class NeededMoveEvent : InvokedMoveEvent
    {
        public override GameEvent Clone() { return new NeededMoveEvent(); }

        private void tryAddMove(List<string> moves, string move)
        {
            if (DataManager.Instance.DataIndices[DataManager.DataType.Skill].Get(move).Released)
                moves.Add(move);
        }


        private void tryAddTargetMove(Character user, List<Character> seenChars, List<string> moves, string move)
        {
            int effectiveness = 0;
            SkillData skill = DataManager.Instance.GetSkill(move);
            HashSet<Loc> targetLocs = new HashSet<Loc>();
            foreach (Loc loc in skill.HitboxAction.GetPreTargets(user, user.CharDir, 0))
                targetLocs.Add(ZoneManager.Instance.CurrentMap.WrapLoc(loc));
            foreach (Character seenChar in seenChars)
            {
                if (targetLocs.Contains(seenChar.CharLoc))
                    effectiveness += PreTypeEvent.GetDualEffectiveness(user, seenChar, skill.Data.Element) - PreTypeEvent.NRM_2;
            }

            if (effectiveness > 0)
                tryAddMove(moves, move);
        }

        protected override string GetInvokedMove(GameEventOwner owner, BattleContext context)
        {
            // scroll of need style move choice

            List<Character> seenAllies = context.User.GetSeenCharacters(Alignment.Friend);

            List<List<string>> tryingCategories = new List<List<string>>();
            //conditions:
            //are you wounded?
            bool needHeal = false;
            if (context.User.HP < context.User.MaxHP * 2 / 3)
            {
                List<string> tryingMoves = new List<string>();
                tryAddMove(tryingMoves, "recover");//recover
                tryAddMove(tryingMoves, "synthesis");//synthesis
                tryAddMove(tryingMoves, "roost");//roost
                tryAddMove(tryingMoves, "slack_off");//slack off
                tryingCategories.Add(tryingMoves);
                if (context.User.HP < context.User.MaxHP / 3)
                    needHeal = true;
            }

            //are your allies wounded? 2+ separate mons needed
            int woundedAllies = 0;
            foreach (Character ally in seenAllies)
            {
                if (ally.HP < ally.MaxHP * 2 / 3)
                {
                    woundedAllies++;
                    if (ally.HP < ally.MaxHP / 3)
                        needHeal = true;
                }
            }
            if (woundedAllies >= 2)
            {
                List<string> tryingMoves = new List<string>();
                tryAddMove(tryingMoves, "moonlight");//moonlight
                tryAddMove(tryingMoves, "morning_sun");//morning sun
                tryAddMove(tryingMoves, "milk_drink");//milk drink
                tryingCategories.Add(tryingMoves);
            }

            //how about for the target?
            //are any of yours or your targetable ally stats lowered? raise stat


            //status effects?  3+ needed in party
            int badStates = 0;
            foreach (StatusEffect status in context.User.IterateStatusEffects())
            {
                if (status.StatusStates.Contains<BadStatusState>())
                    badStates++;
            }
            foreach (Character ally in seenAllies)
            {
                foreach (StatusEffect status in ally.IterateStatusEffects())
                {
                    if (status.StatusStates.Contains<BadStatusState>())
                        badStates++;
                }
            }
            if (badStates > 2)
            {
                List<string> tryingMoves = new List<string>();
                tryAddMove(tryingMoves, "heal_bell");//heal bell
                tryAddMove(tryingMoves, "refresh");//refresh
                tryingCategories.Add(tryingMoves);
            }

            if (!needHeal)
            {
                List<string> tryingMoves = new List<string>();
                //enemy is weak to a type and can die from it?  use that type move, base it on your higher stat
                //multiple enemies weak to the same type?  use that type move, base it on your higher stat
                HashSet<string> availableWeaknesses = new HashSet<string>();
                List<Character> seenFoes = context.User.GetSeenCharacters(Alignment.Foe);
                foreach (Character chara in seenFoes)
                {
                    foreach (string key in DataManager.Instance.DataIndices[DataManager.DataType.Element].GetOrderedKeys(true))
                    {
                        if (PreTypeEvent.GetDualEffectiveness(context.User, chara, key) > PreTypeEvent.NRM_2)
                            availableWeaknesses.Add(key);
                    }
                }

                foreach (string ii in availableWeaknesses)
                {
                    switch (ii)
                    {
                        case "bug":
                            {
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "attack_order");//attack order
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "x_scissor");//x-scissor
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "megahorn");//megahorn
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "bug_buzz");//bug buzz
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "signal_beam");//signal beam
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "silver_wind");//silver wind
                            }
                            break;
                        case "dark":
                            {
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "hyperspace_fury");//hyperspace fury
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "night_daze");//night daze
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "assurance");//assurance
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "night_slash");//night slash
                            }
                            break;
                        case "dragon":
                            {
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "roar_of_time");//roar of time
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "draco_meteor");//draco meteor
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "spacial_rend");//spacial rend
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "outrage");//outrage
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "dragon_claw");//dragon claw
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "dragon_tail");//dragon tail
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "dragon_rush");//dragon rush
                            }
                            break;
                        case "electric":
                            {
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "discharge");//discharge
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "volt_tackle");//volt tackle
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "zap_cannon");//zap cannon
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "thunder");//thunder
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "bolt_strike");//bolt strike
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "fusion_bolt");//fusion bolt
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "parabolic_charge");//parabolic charge
                            }
                            break;
                        case "fairy":
                            {
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "light_of_ruin");//light of ruin
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "moonblast");//moonblast
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "play_rough");//play rough
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "dazzling_gleam");//dazzling gleam
                            }
                            break;
                        case "fighting":
                            {
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "high_jump_kick");//high jump kick
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "close_combat");//close combat
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "focus_blast");//focus blast
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "cross_chop");//cross chop
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "sacred_sword");//sacred sword
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "aura_sphere");//aura sphere
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "secret_sword");//secret sword
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "drain_punch");//drain punch
                            }
                            break;
                        case "fire":
                            {
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "v_create");//v-create
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "blast_burn");//blast burn
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "eruption");//eruption
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "sacred_fire");//sacred fire
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "flare_blitz");//flare blitz
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "blue_flare");//blue flare
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "fire_blast");//fire blast
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "magma_storm");//magma storm
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "inferno");//inferno
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "heat_wave");//heat wave
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "searing_shot");//searing shot
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "fiery_dance");//fiery dance
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "fusion_flare");//fusion flare
                            }
                            break;
                        case "flying":
                            {
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "brave_bird");//brave bird
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "dragon_ascent");//dragon ascent
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "hurricane");//hurricane
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "aeroblast");//aeroblast
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "oblivion_wing");//oblivion wing
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "sky_attack");//sky attack
                            }
                            break;
                        case "ghost":
                            {
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "shadow_force");//shadow force
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "shadow_ball");//shadow ball
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "ominous_wind");//ominous wind
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "hex");//hex
                            }
                            break;
                        case "grass":
                            {
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "leaf_storm");//leaf storm
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "frenzy_plant");//frenzy plant
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "power_whip");//power whip
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "wood_hammer");//wood hammer
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "energy_ball");//energy ball
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "petal_blizzard");//petal blizzard
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "seed_bomb");//seed bomb
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "solar_beam");//solar beam
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "giga_drain");//giga drain
                            }
                            break;
                        case "ground":
                            {
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "precipice_blades");//precipice blades
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "lands_wrath");//land's wrath
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "earth_power");//earth power
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "drill_run");//drill run
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "thousand_arrows");//thousand arrows
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "thousand_waves");//thousand waves
                            }
                            break;
                        case "ice":
                            {
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "blizzard");//blizzard
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "ice_beam");//ice beam
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "icicle_crash");//icicle crash
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "icicle_spear");//icicle spear
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "ice_burn");//ice burn
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "freeze_shock");//freeze shock
                            }
                            break;
                        case "normal":
                            {
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "hyper_voice");//hyper voice
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "giga_impact");//giga impact
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "double_edge");//double-edge
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "tri_attack");//tri-attack
                            }
                            break;
                        case "poison":
                            {
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "gunk_shot");//gunk shot
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "sludge_wave");//sludge wave
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "sludge_bomb");//sludge bomb
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "cross_poison");//cross poison
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "venoshock");//venoshock
                            }
                            break;
                        case "psychic":
                            {
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "psychic");//psychic
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "hyperspace_hole");//hyperspace hole
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "psycho_boost");//psycho boost
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "psystrike");//psystrike
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "zen_headbutt");//zen headbutt
                            }
                            break;
                        case "rock":
                            {
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "head_smash");//head smash
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "rock_wrecker");//rock wrecker
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "rock_blast");//rock blast
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "power_gem");//power gem
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "ancient_power");//ancient power
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "rollout");//rollout
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "diamond_storm");//diamond storm
                            }
                            break;
                        case "steel":
                            {
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "meteor_mash");//meteor mash
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "iron_tail");//iron tail
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "flash_cannon");//flash cannon
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "iron_head");//iron head
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "magnet_bomb");//magnet bomb
                            }
                            break;
                        case "water":
                            {
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "hydro_cannon");//hydro cannon
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "hydro_pump");//hydro pump
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "origin_pulse");//origin pulse
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "water_spout");//water spout
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "steam_eruption");//steam eruption
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "crabhammer");//crabhammer
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "aqua_tail");//aqua tail
                                tryAddTargetMove(context.User, seenFoes, tryingMoves, "waterfall");//waterfall
                            }
                            break;
                    }
                }
                if (tryingMoves.Count > 0)
                    tryingCategories.Add(tryingMoves);
            }

            //are you surrounded by enemies and cannot hit them all? crowd control: dark void, spore, stun spore

            //otherwise?  give a random buff attack
            if (tryingCategories.Count == 0)
            {
                List<string> tryingMoves = new List<string>();
                if (!context.User.StatusEffects.ContainsKey("aqua_ring"))
                    tryAddMove(tryingMoves, "aqua_ring");//aqua ring
                if (!context.User.StatusEffects.ContainsKey("reflect"))
                    tryAddMove(tryingMoves, "reflect");//reflect
                if (!context.User.StatusEffects.ContainsKey("light_screen"))
                    tryAddMove(tryingMoves, "light_screen");//light screen
                if (!context.User.StatusEffects.ContainsKey("wish"))
                    tryAddMove(tryingMoves, "wish");//wish
                if (!context.User.StatusEffects.ContainsKey("mist"))
                    tryAddMove(tryingMoves, "mist");//mist
                if (!context.User.StatusEffects.ContainsKey("safeguard"))
                    tryAddMove(tryingMoves, "safeguard");//safeguard
                if (!context.User.StatusEffects.ContainsKey("magic_coat"))
                    tryAddMove(tryingMoves, "magic_coat");//magic coat
                if (!context.User.StatusEffects.ContainsKey("mirror_coat"))
                    tryAddMove(tryingMoves, "mirror_coat");//mirror coat
                if (!context.User.StatusEffects.ContainsKey("counter"))
                    tryAddMove(tryingMoves, "counter");//counter
                if (!context.User.StatusEffects.ContainsKey("metal_burst"))
                    tryAddMove(tryingMoves, "metal_burst");//metal burst
                if (!context.User.StatusEffects.ContainsKey("lucky_chant"))
                    tryAddMove(tryingMoves, "lucky_chant");//lucky chant
                if (!context.User.StatusEffects.ContainsKey("focus_energy"))
                    tryAddMove(tryingMoves, "focus_energy");//focus energy
                if (!context.User.StatusEffects.ContainsKey("sure_shot"))
                    tryAddMove(tryingMoves, "lock_on");//lock-on
                tryingCategories.Add(tryingMoves);
            }


            //threat of status effects from enemies? safeguard
            //does the enemy have an ability that covers their weakness?  gastro acid

            //does your target have unusually high stat boosts?  clear stat boosts


            //do nearby targets have a high attack/special attack?  boost defense in that side
            //are you alone with summonable friends? beat up

            if (tryingCategories.Count > 0)
            {
                //75% chance of picking a good move
                if (DataManager.Instance.Save.Rand.Next(100) < 75)
                {
                    List<string> tryingMoves = tryingCategories[DataManager.Instance.Save.Rand.Next(tryingCategories.Count)];
                    return tryingMoves[DataManager.Instance.Save.Rand.Next(tryingMoves.Count)];
                }
            }

            List<string> releasedMoves = new List<string>();
            foreach (string key in DataManager.Instance.DataIndices[DataManager.DataType.Skill].GetOrderedKeys(true))
            {
                if (key == DataManager.Instance.DefaultSkill)
                    continue;
                if (DataManager.Instance.DataIndices[DataManager.DataType.Skill].Get(key).Released)
                    releasedMoves.Add(key);
            }
            int randIndex = DataManager.Instance.Save.Rand.Next(releasedMoves.Count);
            return releasedMoves[randIndex];
        }
    }

    /// <summary>
    /// Event that makes character will use a move that depends on the map status and dungeon type
    /// </summary>
    [Serializable]
    public class NatureMoveEvent : InvokedMoveEvent
    {
        /// <summary>
        /// The move used mapped to the current map status
        /// </summary>
        [JsonConverter(typeof(MapStatusSkillDictConverter))]
        [DataType(1, DataManager.DataType.MapStatus, false)]
        [DataType(2, DataManager.DataType.Skill, false)]
        public Dictionary<string, string> TerrainPair;
        
        /// <summary>
        /// The move used mapped to the current floor's nature environment
        /// </summary>
        [JsonConverter(typeof(ElementSkillDictConverter))]
        [DataType(1, DataManager.DataType.Element, false)]
        [DataType(2, DataManager.DataType.Skill, false)]
        public Dictionary<string, string> NaturePair;

        public NatureMoveEvent()
        {
            TerrainPair = new Dictionary<string, string>();
            NaturePair = new Dictionary<string, string>();
        }
        public NatureMoveEvent(Dictionary<string, string> terrain, Dictionary<string, string> moves)
        {
            TerrainPair = terrain;
            NaturePair = moves;
        }
        protected NatureMoveEvent(NatureMoveEvent other)
            : this()
        {
            foreach (string terrain in other.TerrainPair.Keys)
                TerrainPair.Add(terrain, other.TerrainPair[terrain]);
            foreach (string element in other.NaturePair.Keys)
                NaturePair.Add(element, other.NaturePair[element]);
        }
        public override GameEvent Clone() { return new NatureMoveEvent(this); }

        protected override string GetInvokedMove(GameEventOwner owner, BattleContext context)
        {
            foreach (string terrain in TerrainPair.Keys)
            {
                if (ZoneManager.Instance.CurrentMap.Status.ContainsKey(terrain))
                    return TerrainPair[terrain];
            }

            string moveNum;
            if (NaturePair.TryGetValue(ZoneManager.Instance.CurrentMap.Element, out moveNum))
                return moveNum;
            else
                return "";
        }
    }

    /// <summary>
    /// Event that makes the user use the last used move
    /// </summary>  
    [Serializable]
    public class MirrorMoveEvent : InvokedMoveEvent
    {
        /// <summary>
        /// A status containing the move in IDState that this event will use
        /// This status should either be Last Used Effect, Last Ally Effect, Last Effect Hit By Someone Else
        /// </summary>   
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string MoveStatusID;

        public MirrorMoveEvent() { MoveStatusID = ""; }
        public MirrorMoveEvent(string prevMoveStatusID)
        {
            MoveStatusID = prevMoveStatusID;
        }
        protected MirrorMoveEvent(MirrorMoveEvent other)
        {
            MoveStatusID = other.MoveStatusID;
        }
        public override GameEvent Clone() { return new MirrorMoveEvent(this); }

        protected override string GetInvokedMove(GameEventOwner owner, BattleContext context)
        {
            StatusEffect status = context.Target.GetStatusEffect(MoveStatusID);
            if (status != null)
                return status.StatusStates.GetWithDefault<IDState>().ID;
            else
                return "";
        }
    }

    /// <summary>
    /// Event that is called as a turn-taking battle action
    /// </summary> 
    [Serializable]
    public class InvokeCustomBattleEvent : InvokeBattleEvent
    {
        /// <summary>
        /// Data on the hitbox of the attack. Controls range and targeting
        /// </summary>
        public CombatAction HitboxAction;
        
        /// <summary>
        /// Optional data to specify a splash effect on the tiles hit
        /// </summary>
        public ExplosionData Explosion;
        
        /// <summary>
        /// Events that occur with this skill.
        /// Before it's used, when it hits, after it's used, etc
        /// </summary>
        public BattleData NewData;
        
        /// <summary>
        /// The message displayed in the dungeon log 
        /// </summary>
        [StringKey(0, true)]
        public StringKey Msg;
        
        /// <summary>
        /// Whether to affect the target or user
        /// </summary>
        public bool AffectTarget;

        public InvokeCustomBattleEvent()
        {

        }

        public InvokeCustomBattleEvent(CombatAction action, ExplosionData explosion, BattleData moveData, StringKey msg, bool affectTarget = true)
        {
            HitboxAction = action;
            Explosion = explosion;
            NewData = moveData;
            Msg = msg;
            AffectTarget = affectTarget;
        }
        protected InvokeCustomBattleEvent(InvokeCustomBattleEvent other)
        {
            HitboxAction = other.HitboxAction;
            Explosion = other.Explosion;
            NewData = new BattleData(other.NewData);
            Msg = other.Msg;
            AffectTarget = other.AffectTarget;
        }
        public override GameEvent Clone() { return new InvokeCustomBattleEvent(this); }

        protected override BattleContext CreateContext(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            BattleContext newContext = new BattleContext(BattleActionType.Skill);
            newContext.User = (AffectTarget ? context.Target : context.User);
            newContext.UsageSlot = BattleContext.FORCED_SLOT;

            newContext.StartDir = newContext.User.CharDir;

            //change move effects
            newContext.Data = new BattleData(NewData);
            newContext.Data.ID = context.Data.ID;
            newContext.Data.DataType = context.Data.DataType;

            newContext.Explosion = new ExplosionData(Explosion);
            newContext.HitboxAction = HitboxAction.Clone();
            newContext.Strikes = 1;
            newContext.Item = new InvItem();

            if (Msg.IsValid())
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(Msg.ToLocal(), ownerChar.GetDisplayName(false), owner.GetDisplayName()));

            return newContext;
        }


        [OnDeserialized]
        internal void OnDeserializedMethod(StreamingContext context)
        {
            //TODO: remove on v1.1
            if (Serializer.OldVersion < new Version(0, 7, 10))
            {
                AffectTarget = true;
            }
        }
    }

    /// <summary>
    /// Event that uses a different battle action if the character is a certain type.
    /// </summary>
    [Serializable]
    public class ElementDifferentUseEvent : BattleEvent
    {
        /// <summary>
        /// The type in order for this battle action to activate
        /// </summary>
        [JsonConverter(typeof(ElementConverter))]
        [DataType(0, DataManager.DataType.Element, false)]
        public string Element;
        //also need to somehow specify alternative animations/sounds
        /// <summary>
        /// Data on the hitbox of the attack. Controls range and targeting
        /// </summary>
        public CombatAction HitboxAction;
        
        /// <summary>
        /// Optional data to specify a splash effect on the tiles hit
        /// </summary>
        public ExplosionData Explosion;
        
        /// <summary>
        /// Events that occur with this skill
        /// Before it's used, when it hits, after it's used, etc
        /// </summary>
        public BattleData NewData;

        public ElementDifferentUseEvent() { Element = ""; }
        public ElementDifferentUseEvent(string element, CombatAction action, ExplosionData explosion, BattleData moveData)
        {
            Element = element;
            HitboxAction = action;
            Explosion = explosion;
            NewData = moveData;
        }
        protected ElementDifferentUseEvent(ElementDifferentUseEvent other)
            : this()
        {
            Element = other.Element;
            HitboxAction = other.HitboxAction;
            Explosion = other.Explosion;
            NewData = new BattleData(other.NewData);
        }
        public override GameEvent Clone() { return new ElementDifferentUseEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            //different effects for element
            if (context.User.HasElement(Element))
            {
                //change hitboxaction
                context.HitboxAction = HitboxAction.Clone();

                //change explosion
                context.Explosion = new ExplosionData(Explosion);

                //change move effects
                string id = context.Data.ID;
                DataManager.DataType dataType = context.Data.DataType;
                context.Data = new BattleData(NewData);
                context.Data.ID = id;
                context.Data.DataType = dataType;
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that uses a different battle data if the target is an ally
    /// </summary>
    [Serializable]
    public class AlignmentDifferentEvent : BattleEvent
    {
        public Alignment Alignments;
        /// <summary>
        /// Events that occur with this skill
        /// Before it's used, when it hits, after it's used, etc
        /// </summary>
        public BattleData NewData;

        public AlignmentDifferentEvent() { }
        public AlignmentDifferentEvent(Alignment alignments, BattleData moveData)
        {
            Alignments = alignments;
            NewData = moveData;
        }
        protected AlignmentDifferentEvent(AlignmentDifferentEvent other)
        {
            Alignments = other.Alignments;
            NewData = new BattleData(other.NewData);
        }
        public override GameEvent Clone() { return new AlignmentDifferentEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            //different effects for allies
            if ((DungeonScene.Instance.GetMatchup(context.User, context.Target) & Alignments) != Alignment.None)
            {
                string id = context.Data.ID;
                DataManager.DataType dataType = context.Data.DataType;
                context.Data = new BattleData(NewData);
                context.Data.ID = id;
                context.Data.DataType = dataType;
            }
            yield break;
        }

        [OnDeserialized]
        internal void OnDeserializedMethod(StreamingContext context)
        {
            //TODO: remove on v1.1
            if (Serializer.OldVersion < new Version(0, 7, 21) && Alignments == Alignment.None)
            {
                Alignments = Alignment.Self | Alignment.Friend;
            }
        }
    }


    /// <summary>
    /// Event that checks whether an item can be caught and changes the battle data if so
    /// </summary>
    [Serializable]
    public class CatchableEvent : BattleEvent
    {
        /// <summary>
        /// Events that occur when the item is caught
        /// Before it's used, when it hits, after it's used, etc
        /// </summary>
        public BattleData NewData;

        public CatchableEvent() { }
        public CatchableEvent(BattleData moveData)
        {
            NewData = moveData;
        }
        protected CatchableEvent(CatchableEvent other)
        {
            NewData = new BattleData(other.NewData);
        }
        public override GameEvent Clone() { return new CatchableEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            //can't catch pierce
            if (context.HitboxAction is LinearAction && !((LinearAction)context.HitboxAction).StopAtHit)
                yield break;

            //can't catch when holding
            if (!String.IsNullOrEmpty(context.Target.EquippedItem.ID))
                yield break;

            //can't catch when inv full
            if (context.Target.MemberTeam is ExplorerTeam && ((ExplorerTeam)context.Target.MemberTeam).GetInvCount() >= ((ExplorerTeam)context.Target.MemberTeam).GetMaxInvSlots(ZoneManager.Instance.CurrentZone))
                yield break;

            if (context.Target.MemberTeam is MonsterTeam)
            {
                //can't catch if it's a wild team, and it's a use-item
                ItemData entry = DataManager.Instance.GetItem(context.Item.ID);
                //can't catch if it's a wild team, and it's an edible or ammo
                if (entry.ItemStates.Contains<EdibleState>() || entry.ItemStates.Contains<AmmoState>())
                    yield break;
            }

            context.ContextStates.Set(new ItemCaught());

            string id = context.Data.ID;
            DataManager.DataType dataType = context.Data.DataType;
            context.Data = new BattleData(NewData);
            context.Data.ID = id;
            context.Data.DataType = dataType;
        }
    }

    /// <summary>
    /// Event that changes the hitbox action
    /// </summary>
    [Serializable]
    public class ChangeActionEvent : BattleEvent
    {
        /// <summary>
        /// Data on the hitbox of the attack. Controls range and targeting
        /// </summary>
        public CombatAction NewAction;

        public ChangeActionEvent() { }
        public ChangeActionEvent(CombatAction newAction)
        {
            NewAction = newAction;
        }
        protected ChangeActionEvent(ChangeActionEvent other)
            : this()
        {
            NewAction = other.NewAction.Clone();
        }
        public override GameEvent Clone() { return new ChangeActionEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            //change hitboxaction
            context.HitboxAction = NewAction.Clone();
            yield break;
        }
    }

    /// <summary>
    /// Event that changes the battle data
    /// </summary>
    [Serializable]
    public class ChangeDataEvent : BattleEvent
    {
        /// <summary>
        /// Events that occur with this skill
        /// Before it's used, when it hits, after it's used, etc
        /// </summary>
        public BattleData NewAction;

        public ChangeDataEvent() { }
        public ChangeDataEvent(BattleData newAction)
        {
            NewAction = newAction;
        }
        protected ChangeDataEvent(ChangeDataEvent other)
            : this()
        {
            NewAction = new BattleData(other.NewAction);
        }
        public override GameEvent Clone() { return new ChangeDataEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            //change data
            context.Data = new BattleData(NewAction);
            yield break;
        }
    }

    /// <summary>
    /// Event that changes the explosion data
    /// </summary>
    [Serializable]
    public class ChangeExplosionEvent : BattleEvent
    {
        /// <summary>
        /// Optional data to specify a splash effect on the tiles hit
        /// </summary>
        public ExplosionData NewAction;

        public ChangeExplosionEvent() { }
        public ChangeExplosionEvent(ExplosionData newAction)
        {
            NewAction = newAction;
        }
        protected ChangeExplosionEvent(ChangeExplosionEvent other)
            : this()
        {
            NewAction = new ExplosionData(other.NewAction);
        }
        public override GameEvent Clone() { return new ChangeExplosionEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            //change data
            context.Explosion = new ExplosionData(NewAction);
            yield break;
        }
    }

    
    /// <summary>
    /// Event that uses different skill data depending on the stack number of the status
    /// </summary>
    [Serializable]
    public class StatusStackDifferentEvent : BattleEvent
    {
        /// <summary>
        /// The status condition to track
        /// </summary>
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string StatusID;
        
        /// <summary>
        /// The message displayed in the dungeon log if the character doesn't have this status or the stack amount does not map to a skill data
        /// </summary>
        public StringKey FailMsg;
        
        /// <summary>
        /// The stack amount mapped to a skill data
        /// </summary>
        public Dictionary<int, Tuple<CombatAction, ExplosionData, BattleData>> StackPair;

        public StatusStackDifferentEvent() { StackPair = new Dictionary<int, Tuple<CombatAction, ExplosionData, BattleData>>(); StatusID = ""; }
        public StatusStackDifferentEvent(string statusID, StringKey failMsg, Dictionary<int, Tuple<CombatAction, ExplosionData, BattleData>> stack)
        {
            StatusID = statusID;
            FailMsg = failMsg;
            StackPair = stack;
        }
        protected StatusStackDifferentEvent(StatusStackDifferentEvent other)
            : this()
        {
            StatusID = other.StatusID;
            FailMsg = other.FailMsg;
            foreach (int stack in other.StackPair.Keys)
                StackPair.Add(stack, new Tuple<CombatAction, ExplosionData, BattleData>(other.StackPair[stack].Item1.Clone(), new ExplosionData(other.StackPair[stack].Item2), new BattleData(other.StackPair[stack].Item3)));
        }
        public override GameEvent Clone() { return new StatusStackDifferentEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            StatusEffect status = context.User.GetStatusEffect(StatusID);
            if (status == null)
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(FailMsg.ToLocal(), context.User.GetDisplayName(false)));
                yield break;
            }

            StackState stack = status.StatusStates.GetWithDefault<StackState>();
            if (StackPair.ContainsKey(stack.Stack))
            {
                //change hitboxaction
                context.HitboxAction = StackPair[stack.Stack].Item1.Clone();

                //change explosion
                context.Explosion = new ExplosionData(StackPair[stack.Stack].Item2);

                //change move effects
                string id = context.Data.ID;
                DataManager.DataType dataType = context.Data.DataType;
                context.Data = new BattleData(StackPair[stack.Stack].Item3);
                context.Data.ID = id;
                context.Data.DataType = dataType;
            }
            else
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(FailMsg.ToLocal(), context.User.GetDisplayName(false)));
        }
    }


    /// <summary>
    /// Event that uses different battle data depending on map status
    /// </summary>
    [Serializable]
    public class WeatherDifferentEvent : BattleEvent
    {
        /// <summary>
        /// The map status ID mapped to a battle data
        /// </summary>
        [JsonConverter(typeof(MapStatusBattleDataDictConverter))]
        public Dictionary<string, BattleData> WeatherPair;

        public WeatherDifferentEvent() { WeatherPair = new Dictionary<string, BattleData>(); }
        public WeatherDifferentEvent(Dictionary<string, BattleData> weather)
        {
            WeatherPair = weather;
        }
        protected WeatherDifferentEvent(WeatherDifferentEvent other)
            : this()
        {
            foreach (string weather in other.WeatherPair.Keys)
                WeatherPair.Add(weather, new BattleData(other.WeatherPair[weather]));
        }
        public override GameEvent Clone() { return new WeatherDifferentEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            foreach (string weather in WeatherPair.Keys)
            {
                if (ZoneManager.Instance.CurrentMap.Status.ContainsKey(weather))
                {
                    string id = context.Data.ID;
                    DataManager.DataType dataType = context.Data.DataType;
                    context.Data = new BattleData(WeatherPair[weather]);
                    context.Data.ID = id;
                    context.Data.DataType = dataType;
                    break;
                }
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that activates if the character is hit by a super-effective move
    /// </summary>
    [Serializable]
    public class AbsorbWeaknessEvent : BattleEvent
    {
        /// <summary>
        /// The list of battle events applied if the condition is met
        /// </summary>
        public List<BattleEvent> BaseEvents;

        /// <summary>
        /// The particle VFX that plays if the condition is met
        /// </summary>
        public FiniteEmitter Emitter;
        
        /// <summary>
        /// The sound effect that plays if the condition is met
        /// </summary>
        [Sound(0)]
        public string Sound;

        public AbsorbWeaknessEvent() { BaseEvents = new List<BattleEvent>(); Emitter = new EmptyFiniteEmitter(); }
        public AbsorbWeaknessEvent(FiniteEmitter emitter, string sound, params BattleEvent[] effects)
            : this()
        {
            foreach (BattleEvent battleEffect in effects)
                BaseEvents.Add(battleEffect);
            Emitter = emitter;
            Sound = sound;
        }
        protected AbsorbWeaknessEvent(AbsorbWeaknessEvent other)
            : this()
        {
            foreach (BattleEvent battleEffect in other.BaseEvents)
                BaseEvents.Add((BattleEvent)battleEffect.Clone());
            Emitter = (FiniteEmitter)other.Emitter.Clone();
            Sound = other.Sound;
        }
        public override GameEvent Clone() { return new AbsorbWeaknessEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int typeMatchup = PreTypeEvent.GetDualEffectiveness(context.User, context.Target, context.Data);
            typeMatchup -= PreTypeEvent.NRM_2;
            if (typeMatchup > 0 && context.User != context.Target)
            {
                string id = context.Data.ID;
                DataManager.DataType dataType = context.Data.DataType;
                BattleData newData = new BattleData();
                newData.Element = context.Data.Element;
                newData.Category = context.Data.Category;
                newData.HitRate = context.Data.HitRate;
                foreach (SkillState state in context.Data.SkillStates)
                    newData.SkillStates.Set(state.Clone<SkillState>());
                //add the absorption effects
                //newData.OnHits.Add(new BattleLogBattleEvent(new StringKey(new StringKey("MSG_ABSORB").ToLocal()), false, true));
                newData.OnHits.Add(0, new BattleAnimEvent((FiniteEmitter)Emitter.Clone(), Sound, true, 10));
                foreach (BattleEvent battleEffect in BaseEvents)
                    newData.OnHits.Add(0, (BattleEvent)battleEffect.Clone());

                foreach (BattleFX fx in context.Data.IntroFX)
                    newData.IntroFX.Add(new BattleFX(fx));
                newData.HitFX = new BattleFX(context.Data.HitFX);
                context.Data = newData;
                context.Data.ID = id;
                context.Data.DataType = dataType;
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that activates if the character is hit by the chosen type
    /// </summary>
    [Serializable]
    public class AbsorbElementEvent : BattleEvent
    {
        /// <summary>
        /// The type to absorb
        /// </summary>
        [JsonConverter(typeof(ElementConverter))]
        [DataType(0, DataManager.DataType.Element, false)]
        public string AbsorbElement;
        
        
        /// <summary>
        /// Whether or not multiple 
        /// </summary>
        public bool SingleDraw;
        
        /// <summary>
        /// Whether to display the message if absorbed
        /// </summary>
        public bool GiveMsg;
        
        /// <summary>
        /// Battle events that occur if hit by the certain type
        /// </summary>
        public List<BattleEvent> BaseEvents;

        /// <summary>
        /// The particle VFX
        /// </summary>
        public FiniteEmitter Emitter;
        
        /// <summary>
        /// The sound effect that plays if hit by a super-effective move
        /// </summary>
        [Sound(0)]
        public string Sound;

        public AbsorbElementEvent() { BaseEvents = new List<BattleEvent>(); Emitter = new EmptyFiniteEmitter(); AbsorbElement = ""; }
        public AbsorbElementEvent(string element, params BattleEvent[] effects)
            : this(element, false, effects) { }
        public AbsorbElementEvent(string element, bool singleDraw, params BattleEvent[] effects)
            : this(element, false, false, new EmptyFiniteEmitter(), "", effects) { }
        public AbsorbElementEvent(string element, bool singleDraw, bool giveMsg, FiniteEmitter emitter, string sound, params BattleEvent[] effects)
            : this()
        {
            AbsorbElement = element;
            SingleDraw = singleDraw;
            GiveMsg = giveMsg;
            foreach (BattleEvent battleEffect in effects)
                BaseEvents.Add(battleEffect);
            Emitter = emitter;
            Sound = sound;
        }
        protected AbsorbElementEvent(AbsorbElementEvent other) : this()
        {
            AbsorbElement = other.AbsorbElement;
            SingleDraw = other.SingleDraw;
            foreach (BattleEvent battleEffect in other.BaseEvents)
                BaseEvents.Add((BattleEvent)battleEffect.Clone());
            Emitter = (FiniteEmitter)other.Emitter.Clone();
            Sound = other.Sound;
        }
        public override GameEvent Clone() { return new AbsorbElementEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Data.Element == AbsorbElement && context.User != context.Target)
            {
                string id = context.Data.ID;
                DataManager.DataType dataType = context.Data.DataType;
                BattleData newData = new BattleData();
                newData.Element = context.Data.Element;
                newData.Category = context.Data.Category;
                newData.HitRate = context.Data.HitRate;
                foreach (SkillState state in context.Data.SkillStates)
                    newData.SkillStates.Set(state.Clone<SkillState>());
                //add the absorption effects
                if (!SingleDraw || !context.GlobalContextStates.Contains<SingleDrawAbsorb>())
                {
                    if (GiveMsg)
                    {
                        newData.OnHits.Add(0, new FormatLogLocalEvent(Text.FormatGrammar(new StringKey("MSG_ABSORB").ToLocal(), ownerChar.GetDisplayName(false), owner.GetDisplayName()), false));
                        newData.OnHits.Add(0, new BattleAnimEvent((FiniteEmitter)Emitter.Clone(), Sound, true, 10));
                    }
                    foreach (BattleEvent battleEffect in BaseEvents)
                        newData.OnHits.Add(0, (BattleEvent)battleEffect.Clone());
                }

                foreach (BattleFX fx in context.Data.IntroFX)
                    newData.IntroFX.Add(new BattleFX(fx));
                newData.HitFX = new BattleFX(context.Data.HitFX);
                context.Data = newData;
                context.Data.ID = id;
                context.Data.DataType = dataType;
            }
            yield break;
        }
    }


    [Serializable]
    public class SetDamageEvent : BattleEvent
    {
        public BattleEvent BaseEvent;

        public List<BattleAnimEvent> Anims;

        public SetDamageEvent() { Anims = new List<BattleAnimEvent>(); }
        public SetDamageEvent(BattleEvent battleEffect, params BattleAnimEvent[] anims)
            : this()
        {
            BaseEvent = battleEffect;
            Anims.AddRange(anims);
        }
        protected SetDamageEvent(SetDamageEvent other) : this()
        {
            BaseEvent = other.BaseEvent;
            Anims = new List<BattleAnimEvent>();
            foreach (BattleAnimEvent anim in other.Anims)
                Anims.Add((BattleAnimEvent)anim.Clone());
        }

        public override GameEvent Clone() { return new SetDamageEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.User != context.Target)
            {
                BattleData newData = new BattleData(context.Data);

                foreach (BattleAnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                foreach (Priority priority in newData.OnHits.GetPriorities())
                {
                    int count = newData.OnHits.GetCountAtPriority(priority);
                    for (int jj = 0; jj < count; jj++)
                    {
                        BattleEvent effect = newData.OnHits.Get(priority, jj);
                        if (effect is DirectDamageEvent)
                            newData.OnHits.Set(priority, jj, (BattleEvent)BaseEvent.Clone());
                    }
                }

                context.Data = newData;
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that boosts/reduces the attacks of the chosen type
    /// </summary>
    [Serializable]
    public class MultiplyElementEvent : BattleEvent
    {
        /// <summary>
        /// The type affected
        /// </summary>
        [JsonConverter(typeof(ElementConverter))]
        [DataType(0, DataManager.DataType.Element, false)]
        public string MultElement;
        
        /// <summary>
        /// The numerator of the modifier
        /// </summary>
        public int Numerator;
        
        /// <summary>
        /// The denominator of the modifier
        /// </summary>
        public int Denominator;
        
        /// <summary>
        /// The list of battle VFXs played if the condition is met
        /// </summary>
        public List<BattleAnimEvent> Anims;
        
        /// <summary>
        /// Whether to display a message if the move type matches
        /// </summary>
        public bool Msg;

        public MultiplyElementEvent()
        {
            MultElement = "";
            Anims = new List<BattleAnimEvent>();
        }
        public MultiplyElementEvent(string element, int numerator, int denominator, bool msg)
        {
            MultElement = element;
            Numerator = numerator;
            Denominator = denominator;
            Msg = msg;
            Anims = new List<BattleAnimEvent>();
        }
        public MultiplyElementEvent(string element, int numerator, int denominator, bool msg, params BattleAnimEvent[] anims)
        {
            MultElement = element;
            Numerator = numerator;
            Denominator = denominator;
            Msg = msg;
            Anims = new List<BattleAnimEvent>();
            Anims.AddRange(anims);
        }
        protected MultiplyElementEvent(MultiplyElementEvent other)
        {
            MultElement = other.MultElement;
            Numerator = other.Numerator;
            Denominator = other.Denominator;
            Msg = other.Msg;
            Anims = new List<BattleAnimEvent>();
            foreach (BattleAnimEvent anim in other.Anims)
                Anims.Add((BattleAnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new MultiplyElementEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Data.Element == MultElement &&
                (context.Data.Category == BattleData.SkillCategory.Physical || context.Data.Category == BattleData.SkillCategory.Magical))
            {
                if (Msg)
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_PROTECT_WITH").ToLocal(), ownerChar.GetDisplayName(false), owner.GetDisplayName()));
                foreach (BattleAnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                context.AddContextStateMult<DmgMult>(false, Numerator, Denominator);
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that boosts/reduces the attacks of non-matching types
    /// </summary>
    [Serializable]
    public class MultiplyNotElementEvent : BattleEvent
    {
        /// <summary>
        /// The types not affected by the modifier
        /// </summary>
        [DataType(1, DataManager.DataType.Element, false)]
        public HashSet<string> NotMultElement;
        
        /// <summary>
        /// The numerator of the modifier
        /// </summary>
        public int Numerator;
        
        /// <summary>
        /// The denominator of the modifier
        /// </summary>
        public int Denominator;
        
        /// <summary>
        /// The list of battle VFXs played if the condition is met
        /// </summary>
        public List<BattleAnimEvent> Anims;
        
        /// <summary>
        /// Whether to display a message if the move type does not match
        /// </summary>
        public bool Msg;

        public MultiplyNotElementEvent()
        {
            NotMultElement = new HashSet<string>();
            Anims = new List<BattleAnimEvent>();
        }
        public MultiplyNotElementEvent(string element, int numerator, int denominator, bool msg)
        {
            NotMultElement = new HashSet<string>();
            NotMultElement.Add(element);
            Numerator = numerator;
            Denominator = denominator;
            Msg = msg;
            Anims = new List<BattleAnimEvent>();
        }
        public MultiplyNotElementEvent(string element, int numerator, int denominator, bool msg, params BattleAnimEvent[] anims)
        {
            NotMultElement = new HashSet<string>();
            NotMultElement.Add(element);
            Numerator = numerator;
            Denominator = denominator;
            Msg = msg;
            Anims = new List<BattleAnimEvent>();
            Anims.AddRange(anims);
        }
        protected MultiplyNotElementEvent(MultiplyNotElementEvent other)
        {
            NotMultElement = new HashSet<string>();
            foreach(string element in other.NotMultElement)
                NotMultElement.Add(element);
            Numerator = other.Numerator;
            Denominator = other.Denominator;
            Msg = other.Msg;
            Anims = new List<BattleAnimEvent>();
            foreach (BattleAnimEvent anim in other.Anims)
                Anims.Add((BattleAnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new MultiplyNotElementEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (!NotMultElement.Contains(context.Data.Element) && context.Data.Element != DataManager.Instance.DefaultElement &&
                (context.Data.Category == BattleData.SkillCategory.Physical || context.Data.Category == BattleData.SkillCategory.Magical))
            {
                if (Msg)
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_PROTECT_WITH").ToLocal(), ownerChar.GetDisplayName(false), owner.GetDisplayName()));
                foreach (BattleAnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                context.AddContextStateMult<DmgMult>(false, Numerator, Denominator);
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that boosts/reduces the attacks if the attack's type matches the type in ElementState (StatusState)
    /// </summary>
    [Serializable]
    public class MultiplyStatusElementEvent : BattleEvent
    {
        /// <summary>
        /// The numerator of the modifier
        /// </summary>
        public int Numerator;
        
        /// <summary>
        /// The denominator of the modifier
        /// </summary>
        public int Denominator;
        
        /// <summary>
        /// The list of battle VFXs played if the type matches
        /// </summary>
        public List<BattleAnimEvent> Anims;

        public MultiplyStatusElementEvent()
        {
            Anims = new List<BattleAnimEvent>();
        }
        public MultiplyStatusElementEvent(int numerator, int denominator)
        {
            Numerator = numerator;
            Denominator = denominator;
            Anims = new List<BattleAnimEvent>();
        }
        public MultiplyStatusElementEvent(int numerator, int denominator, params BattleAnimEvent[] anims)
        {
            Numerator = numerator;
            Denominator = denominator;
            Anims = new List<BattleAnimEvent>();
            Anims.AddRange(anims);
        }
        protected MultiplyStatusElementEvent(MultiplyStatusElementEvent other)
        {
            Numerator = other.Numerator;
            Denominator = other.Denominator;
            Anims = new List<BattleAnimEvent>();
            foreach (BattleAnimEvent anim in other.Anims)
                Anims.Add((BattleAnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new MultiplyStatusElementEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Data.Element == ((StatusEffect)owner).StatusStates.GetWithDefault<ElementState>().Element)
            {
                foreach (BattleAnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                context.AddContextStateMult<DmgMult>(false, Numerator, Denominator);
            }
            yield break;
        }
    }



    /// <summary>
    /// Event that changes the battle event depending on the total amount of team members of the same type
    /// </summary>
    [Serializable]
    public class TeamReduceEvent : BattleEvent
    {
        /// <summary>
        /// The qualifying type
        /// </summary>
        [JsonConverter(typeof(ElementConverter))]
        [DataType(0, DataManager.DataType.Element, false)]
        public string QualifyingElement;

        /// <summary>
        /// Battle event that occurs if only 1 team member has the type
        /// </summary>
        public BattleEvent Tier1Event;
        
        /// <summary>
        /// Battle event that occurs if only 2 team members has the type
        /// </summary>
        public BattleEvent Tier2Event;
        
        /// <summary>
        /// Battle event that occurs if only 3 team members has the type
        /// </summary>
        public BattleEvent Tier3Event;
        
        /// <summary>
        /// Battle event that occurs if 4 or more team members has the type
        /// </summary>
        public BattleEvent Tier4Event;

        public TeamReduceEvent() { QualifyingElement = ""; }
        public TeamReduceEvent(string element, BattleEvent tier1, BattleEvent tier2, BattleEvent tier3, BattleEvent tier4)
        {
            QualifyingElement = element;
            Tier1Event = tier1;
            Tier2Event = tier2;
            Tier3Event = tier3;
            Tier4Event = tier4;
        }
        protected TeamReduceEvent(TeamReduceEvent other)
        {
            QualifyingElement = other.QualifyingElement;
            if (Tier1Event != null)
                Tier1Event = (BattleEvent)other.Tier1Event.Clone();
            if (Tier2Event != null)
                Tier2Event = (BattleEvent)other.Tier2Event.Clone();
            if (Tier3Event != null)
                Tier3Event = (BattleEvent)other.Tier3Event.Clone();
            if (Tier4Event != null)
                Tier4Event = (BattleEvent)other.Tier4Event.Clone();
        }
        public override GameEvent Clone() { return new TeamReduceEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Target.HasElement(QualifyingElement))
            {
                Team team = context.Target.MemberTeam;
                int totalMembers = 0;
                foreach (Character member in team.EnumerateChars())
                {
                    if (member.HasElement(QualifyingElement))
                        totalMembers++;
                }
                if (totalMembers > 3 && Tier4Event != null)
                    yield return CoroutineManager.Instance.StartCoroutine(Tier4Event.Apply(owner, ownerChar, context));
                else if (totalMembers == 3 && Tier3Event != null)
                    yield return CoroutineManager.Instance.StartCoroutine(Tier3Event.Apply(owner, ownerChar, context));
                else if (totalMembers == 2 && Tier2Event != null)
                    yield return CoroutineManager.Instance.StartCoroutine(Tier2Event.Apply(owner, ownerChar, context));
                else if (totalMembers == 1 && Tier1Event != null)
                    yield return CoroutineManager.Instance.StartCoroutine(Tier1Event.Apply(owner, ownerChar, context));
            }
        }
    }

    // TODO: Remove hardcode
    /// <summary>
    /// Event that boosts/reduces an attack type of the user if their HP is low.
    /// </summary>
    [Serializable]
    public class PinchEvent : BattleEvent
    {
        
        /// <summary>
        /// The qualifying type
        /// </summary>
        [JsonConverter(typeof(ElementConverter))]
        [DataType(0, DataManager.DataType.Element, false)]
        public string PinchElement;

        public PinchEvent() { PinchElement = ""; }
        public PinchEvent(string element)
        {
            PinchElement = element;
        }
        protected PinchEvent(PinchEvent other)
        {
            PinchElement = other.PinchElement;
        }
        public override GameEvent Clone() { return new PinchEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Data.Element == PinchElement && context.User.HP <= context.User.MaxHP / 4)
                context.AddContextStateMult<DmgMult>(false, 2, 1);
            yield break;
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
    /// Event that boosts the attack if the move type is the same type as the character 
    /// </summary>
    [Serializable]
    public class AdaptabilityEvent : BattleEvent
    {
        public override GameEvent Clone() { return new AdaptabilityEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.User.HasElement(context.Data.Element))
                context.AddContextStateMult<DmgMult>(false, 5, 4);
            yield break;
        }
    }

    /// <summary>
    /// Event that sets the total strikes to be 1 if no strikes have been made
    /// Used by the move Sky Drop
    /// </summary>
    [Serializable]
    public class SingleStrikeEvent : BattleEvent
    {
        public override GameEvent Clone() { return new SingleStrikeEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.StrikesMade == 0)
                context.Strikes = 1;

            yield break;
        }
    }

    /// <summary>
    /// Event that modifies the total amount the character strikes
    /// </summary>
    [Serializable]
    public class MultiStrikeEvent : BattleEvent
    {
    
        /// <summary>
        /// The total amount of strikes
        /// </summary>
        public int StrikeMult;
        
        /// <summary>
        /// Whether to make the strikes progressively weaker
        /// </summary>
        public bool Div;

        public MultiStrikeEvent() { }
        public MultiStrikeEvent(int mult, bool div)
        {
            StrikeMult = mult;
            Div = div;
        }
        protected MultiStrikeEvent(MultiStrikeEvent other)
        {
            StrikeMult = other.StrikeMult;
            Div = other.Div;
        }
        public override GameEvent Clone() { return new MultiStrikeEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.StrikesMade == 0)
            {
                context.Strikes *= StrikeMult;
                if (Div && (context.Data.Category == BattleData.SkillCategory.Physical || context.Data.Category == BattleData.SkillCategory.Magical))
                    context.AddContextStateMult<DmgMult>(false, 1, StrikeMult);
            }
            yield break;
        }
    }


    /// <summary>
    /// UNUSED
    /// Event that causes the character to use the effects of berries twice.
    /// </summary>
    [Serializable]
    public class HarvestEvent : BattleEvent
    {
        public override GameEvent Clone() { return new HarvestEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Item && context.StrikesMade == 0)
            {
                ItemData itemData = DataManager.Instance.GetItem(context.Item.ID);
                if (itemData.ItemStates.Contains<BerryState>())
                    context.Strikes *= 2;
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that passes the affect of berries to nearby allies.
    /// </summary>
    [Serializable]
    public class BerryAoEEvent : BattleEvent
    {
    
        /// <summary>
        /// The message displayed in the dungeon log  
        /// </summary>
        public StringKey Msg;

        /// <summary>
        /// The particle VFX
        /// </summary>
        public FiniteEmitter Emitter;
        
        /// <summary>
        /// The sound effect of the VFX
        /// </summary>
        [Sound(0)]
        public string Sound;


        public BerryAoEEvent() { Emitter = new EmptyFiniteEmitter(); }
        public BerryAoEEvent(StringKey msg, FiniteEmitter emitter, string sound)
            : this()
        {
            Msg = msg;
            Emitter = emitter;
            Sound = sound;
        }
        protected BerryAoEEvent(BerryAoEEvent other)
            : this()
        {
            Emitter = (FiniteEmitter)other.Emitter.Clone();
            Sound = other.Sound;
        }
        public override GameEvent Clone() { return new BerryAoEEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Item)
            {
                ItemData itemData = DataManager.Instance.GetItem(context.Item.ID);
                if (itemData.ItemStates.Contains<BerryState>())
                {
                    context.HitboxAction = new AreaAction();
                    context.HitboxAction.TargetAlignments = (Alignment.Self | Alignment.Friend);
                    context.Explosion.ExplodeFX.Emitter = Emitter;
                    context.Explosion.ExplodeFX.Sound = Sound;
                    context.Explosion.Range = 1;
                    context.Explosion.Speed = 10;
                    context.Explosion.ExplodeFX.Delay = 30;
                    context.Explosion.TargetAlignments = (Alignment.Self | Alignment.Friend);

                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(Msg.ToLocal(), ownerChar.GetDisplayName(false), owner.GetDisplayName()));
                }
            }
            yield break;
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
    /// Event that prepares the Judgement's type and total strikes based on plates in the inventory
    /// </summary>
    [Serializable]
    public class PrepareJudgmentEvent : BattleEvent
    {
        /// <summary>
        /// The item used mapped to a type
        /// </summary>
        [JsonConverter(typeof(ItemElementDictConverter))]
        [DataType(1, DataManager.DataType.Item, false)]
        [DataType(2, DataManager.DataType.Element, false)]
        public Dictionary<string, string> TypePair;

        public PrepareJudgmentEvent() { TypePair = new Dictionary<string, string>(); }
        public PrepareJudgmentEvent(Dictionary<string, string> typePair)
        {
            TypePair = typePair;
        }
        protected PrepareJudgmentEvent(PrepareJudgmentEvent other)
            : this()
        {
            foreach (string plate in other.TypePair.Keys)
                TypePair.Add(plate, other.TypePair[plate]);
        }
        public override GameEvent Clone() { return new PrepareJudgmentEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            //check to make sure the strike number is 0
            if (context.StrikesMade == 0)
            {
                JudgmentContext judgment = new JudgmentContext();
                string heldElement;
                if (!TypePair.TryGetValue(context.User.EquippedItem.ID, out heldElement))
                    heldElement = "normal";
                judgment.Elements.Add(heldElement);

                if (context.User.MemberTeam is ExplorerTeam)
                {
                    //create a list of types to match the plates held, in a context state
                    ExplorerTeam team = (ExplorerTeam)context.User.MemberTeam;
                    for (int ii = 0; ii < team.GetInvCount(); ii++)
                    {
                        string element;
                        if (TypePair.TryGetValue(team.GetInv(ii).ID, out element))
                        {
                            //check to see if it's not on the list already
                            if (!judgment.Elements.Contains(element))
                                judgment.Elements.Add(element);
                        }
                    }
                }
                context.GlobalContextStates.Set(judgment);
                //change the strike number to match the plates in bag
                context.Strikes = judgment.Elements.Count;
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that changes the Judgement's type based on the current strikes made
    /// </summary>
    [Serializable]
    public class PassJudgmentEvent : BattleEvent
    {
        public PassJudgmentEvent() { }
        public override GameEvent Clone() { return new PassJudgmentEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            //change the type to that of the context state
            JudgmentContext judgment = context.GlobalContextStates.GetWithDefault<JudgmentContext>();
            if (judgment != null && judgment.Elements.Count > context.StrikesMade)
                context.Data.Element = judgment.Elements[context.StrikesMade];

            ElementData element = DataManager.Instance.GetElement(context.Data.Element);
            DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_SKILL_TO_ELEMENT").ToLocal(), element.GetIconName()));
            yield break;
        }
    }

    /// <summary>
    /// Event that changes a move's from one type to another
    /// </summary>
    [Serializable]
    public class ChangeMoveElementEvent : BattleEvent
    {
    
        /// <summary>
        /// The type to change from
        /// </summary>
        [JsonConverter(typeof(ElementConverter))]
        [DataType(0, DataManager.DataType.Element, false)]
        public string ElementFrom;
        
        /// <summary>
        /// The type to change to
        /// </summary>
        [JsonConverter(typeof(ElementConverter))]
        [DataType(0, DataManager.DataType.Element, false)]
        public string ElementTo;

        public ChangeMoveElementEvent() { ElementFrom = ""; ElementTo = ""; }
        public ChangeMoveElementEvent(string elementFrom, string elementTo)
        {
            ElementFrom = elementFrom;
            ElementTo = elementTo;
        }
        protected ChangeMoveElementEvent(ChangeMoveElementEvent other)
        {
            ElementFrom = other.ElementFrom;
            ElementTo = other.ElementTo;
        }
        public override GameEvent Clone() { return new ChangeMoveElementEvent(this); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (ElementFrom == DataManager.Instance.DefaultElement || context.Data.Element == ElementFrom)
                context.Data.Element = ElementTo;
            yield break;
        }
    }

    
    /// <summary>
    /// Event that sets the move type based on the type in ElementState
    /// </summary>
    [Serializable]
    public class ChangeMoveElementStateEvent : BattleEvent
    {
        public override GameEvent Clone() { return new ChangeMoveElementStateEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            context.Data.Element = ((StatusEffect)owner).StatusStates.GetWithDefault<ElementState>().Element;
            yield break;
        }
    }

    /// <summary>
    /// Event that boosts/reduces attacks of a skill category (ex: physical and special)
    /// </summary>
    [Serializable]
    public class MultiplyCategoryEvent : BattleEvent
    {
        
        /// <summary>
        /// The skill category affected
        /// </summary>
        public BattleData.SkillCategory Category;
        
        /// <summary>
        /// The numerator of the modifier
        /// </summary>
        public int Numerator;
        
        /// <summary>
        /// The denominator of the modififer
        /// </summary>
        public int Denominator;
        
        /// <summary>
        /// The list of battle VFXs played if the condition is met
        /// </summary>
        public List<BattleAnimEvent> Anims;

        public MultiplyCategoryEvent()
        {
            Anims = new List<BattleAnimEvent>();
        }
        public MultiplyCategoryEvent(BattleData.SkillCategory category, int numerator, int denominator)
        {
            Category = category;
            Numerator = numerator;
            Denominator = denominator;
            Anims = new List<BattleAnimEvent>();
        }
        public MultiplyCategoryEvent(BattleData.SkillCategory category, int numerator, int denominator, params BattleAnimEvent[] anims)
        {
            Category = category;
            Numerator = numerator;
            Denominator = denominator;
            Anims = new List<BattleAnimEvent>();
            Anims.AddRange(anims);
        }
        protected MultiplyCategoryEvent(MultiplyCategoryEvent other)
        {
            Category = other.Category;
            Numerator = other.Numerator;
            Denominator = other.Denominator;
            Anims = new List<BattleAnimEvent>();
            foreach (BattleAnimEvent anim in other.Anims)
                Anims.Add((BattleAnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new MultiplyCategoryEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Data.Category == Category || Category == BattleData.SkillCategory.None)
            {

                foreach (BattleAnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                context.AddContextStateMult<DmgMult>(false, Numerator, Denominator);
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
    /// Event that boosts/reduces attacks of a skill category of a certain type
    /// </summary>
    [Serializable]
    public class TypeSpecificMultCategoryEvent : BattleEvent
    {
        
        /// <summary>
        /// The type affected
        /// </summary>
        [JsonConverter(typeof(ElementConverter))]
        [DataType(0, DataManager.DataType.Element, false)]
        public string Element;
        
        /// <summary>
        /// Context state to prevent boost stacking
        /// </summary>
        public ContextState NoDupeState;
        
        /// <summary>
        /// The skill category affected
        /// </summary>
        public BattleData.SkillCategory Category;
        
        /// <summary>
        /// The numerator of the modifier + the denominator
        /// </summary>
        public int NumeratorAdd;
        
        /// <summary>
        /// The denominator of the modifier
        /// </summary>
        public int Denominator;

        public TypeSpecificMultCategoryEvent()
        { Element = ""; }
        public TypeSpecificMultCategoryEvent(string element, ContextState state, BattleData.SkillCategory category, int denominator, int numerator)
        {
            Element = element;
            NoDupeState = state;
            Category = category;
            NumeratorAdd = numerator;
            Denominator = denominator;
        }
        protected TypeSpecificMultCategoryEvent(TypeSpecificMultCategoryEvent other)
        {
            Element = other.Element;
            NoDupeState = other.NoDupeState.Clone<ContextState>();
            Category = other.Category;
            NumeratorAdd = other.NumeratorAdd;
            Denominator = other.Denominator;
        }
        public override GameEvent Clone() { return new TypeSpecificMultCategoryEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (ownerChar.HasElement(Element) && context.Data.Category == Category)
            {
                if (!context.ContextStates.Contains(NoDupeState.GetType()))
                {
                    context.AddContextStateMult<DmgMult>(false, NumeratorAdd + Denominator, Denominator);
                    context.ContextStates.Set(NoDupeState);
                }
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that modifies the damage multplier for multiple attacks taken in a turn.
    /// </summary>
    [Serializable]
    public class BarrageGuardEvent : BattleEvent
    {
        
        /// <summary>
        /// Status that keeps track of the move last hit
        /// This status should usually be "was_hurt_last_turn"
        /// </summary>
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string PrevHitID;
        
        /// <summary>
        /// Numerator of the modifier
        /// </summary>
        public int Numerator;
        
        /// <summary>
        /// Denominator of the modifier
        /// </summary>
        public int Denominator;
        
        /// <summary>
        /// The list of battle VFXs played if the condition is met
        /// </summary>
        public List<BattleAnimEvent> Anims;

        public BarrageGuardEvent()
        {
            Anims = new List<BattleAnimEvent>();
            PrevHitID = "";
        }
        public BarrageGuardEvent(string prevHitID, int numerator, int denominator)
        {
            PrevHitID = prevHitID;
            Numerator = numerator;
            Denominator = denominator;
            Anims = new List<BattleAnimEvent>();
        }
        public BarrageGuardEvent(string prevHitID, int numerator, int denominator, params BattleAnimEvent[] anims)
        {
            PrevHitID = prevHitID;
            Numerator = numerator;
            Denominator = denominator;
            Anims = new List<BattleAnimEvent>();
            Anims.AddRange(anims);
        }
        protected BarrageGuardEvent(BarrageGuardEvent other)
        {
            PrevHitID = other.PrevHitID;
            Numerator = other.Numerator;
            Denominator = other.Denominator;
            Anims = new List<BattleAnimEvent>();
            foreach (BattleAnimEvent anim in other.Anims)
                Anims.Add((BattleAnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new BarrageGuardEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            StatusEffect recentHitStatus = context.Target.GetStatusEffect(PrevHitID);
            if (recentHitStatus != null && DungeonScene.Instance.GetMatchup(context.User, context.Target) == Alignment.Foe)
            {
                int timesHit = recentHitStatus.StatusStates.GetWithDefault<StackState>().Stack;
                foreach (BattleAnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                context.AddContextStateMult<DmgMult>(false, Numerator, Denominator + timesHit);
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that modifies the damage multiplier for moves that have recoil damage
    /// The move must be a RecoilEvent or CrashLandEvent
    /// </summary>
    [Serializable]
    public class MultiplyRecklessEvent : BattleEvent
    {
        
        /// <summary>
        /// Numerator of the modifier
        /// </summary>
        public int Numerator;
        
        /// <summary>
        /// Denominator of the modifier
        /// </summary>
        public int Denominator;

        public MultiplyRecklessEvent() { }
        public MultiplyRecklessEvent(int numerator, int denominator)
        {
            Numerator = numerator;
            Denominator = denominator;
        }
        protected MultiplyRecklessEvent(MultiplyRecklessEvent other)
        {
            Numerator = other.Numerator;
            Denominator = other.Denominator;
        }
        public override GameEvent Clone() { return new MultiplyRecklessEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            bool reckless = false;
            foreach (BattleEvent effect in context.Data.AfterActions.EnumerateInOrder())
            {
                if (effect is RecoilEvent || effect is CrashLandEvent)
                {
                    reckless = true;
                    break;
                }
            }
            if (reckless)
                context.AddContextStateMult<DmgMult>(false, Numerator, Denominator);
            yield break;
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
    /// Event that modifies the damage multiplier if the move contains one of the specified SkillStates
    /// </summary>
    [Serializable]
    public class MultiplyMoveStateEvent : BattleEvent
    {
        
        /// <summary>
        /// The list of valid SkillStates types
        /// </summary>
        [StringTypeConstraint(1, typeof(SkillState))]
        public List<FlagType> States;
        
        /// <summary>
        /// Numerator of the modifier
        /// </summary>
        public int Numerator;
        
        /// <summary>
        /// Denominator of the modifier
        /// </summary>
        public int Denominator;

        public MultiplyMoveStateEvent() { States = new List<FlagType>(); }
        public MultiplyMoveStateEvent(Type state, int numerator, int denominator) : this()
        {
            States.Add(new FlagType(state));
            Numerator = numerator;
            Denominator = denominator;
        }
        protected MultiplyMoveStateEvent(MultiplyMoveStateEvent other) : this()
        {
            States.AddRange(other.States);
            Numerator = other.Numerator;
            Denominator = other.Denominator;
        }
        public override GameEvent Clone() { return new MultiplyMoveStateEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            bool hasState = false;
            foreach (FlagType state in States)
            {
                if (context.Data.SkillStates.Contains(state.FullType))
                    hasState = true;
            }
            if (hasState)
                context.AddContextStateMult<DmgMult>(false, Numerator, Denominator);
            yield break;
        }
    }


    /// <summary>
    /// Event that removes a SkillState from the battle data
    /// </summary>
    [Serializable]
    public class RemoveMoveStateEvent : BattleEvent
    {
        
        /// <summary>
        /// The list of SkillStates to remove
        /// </summary>
        [StringTypeConstraint(1, typeof(SkillState))]
        public List<FlagType> States;

        public RemoveMoveStateEvent() { States = new List<FlagType>(); }
        public RemoveMoveStateEvent(Type state) : this()
        {
            States.Add(new FlagType(state));
        }
        protected RemoveMoveStateEvent(RemoveMoveStateEvent other) : this()
        {
            States.AddRange(other.States);
        }
        public override GameEvent Clone() { return new RemoveMoveStateEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            foreach (FlagType state in States)
                context.Data.SkillStates.Remove(state.FullType);
            yield break;
        }
    }
    
    /// <summary>
    /// Event that modifies the damage multiplier of a skill category under a map status
    /// </summary>
    [Serializable]
    public class MultiplyCategoryInWeatherEvent : BattleEvent
    {
        
        /// <summary>
        /// The map status to check for
        /// </summary>
        [JsonConverter(typeof(MapStatusConverter))]
        [DataType(0, DataManager.DataType.MapStatus, false)]
        public string WeatherID;
        
        /// <summary>
        /// The skill category affected
        /// </summary>
        public BattleData.SkillCategory Category;
        
        /// <summary>
        /// The numerator of the modifier
        /// </summary>
        public int Numerator;
        
        /// <summary>
        /// The denominator of the modifier
        /// </summary>
        public int Denominator;

        public MultiplyCategoryInWeatherEvent() { WeatherID = ""; }
        public MultiplyCategoryInWeatherEvent(string weatherId, BattleData.SkillCategory category, int numerator, int denominator)
        {
            WeatherID = weatherId;
            Category = category;
            Numerator = numerator;
            Denominator = denominator;
        }
        protected MultiplyCategoryInWeatherEvent(MultiplyCategoryInWeatherEvent other)
        {
            WeatherID = other.WeatherID;
            Category = other.Category;
            Numerator = other.Numerator;
            Denominator = other.Denominator;
        }
        public override GameEvent Clone() { return new MultiplyCategoryInWeatherEvent(this); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (ZoneManager.Instance.CurrentMap.Status.ContainsKey(WeatherID))
            {
                if (context.Data.Category == Category || Category == BattleData.SkillCategory.None)
                    context.AddContextStateMult<DmgMult>(false, Numerator, Denominator);
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that modifies the damage multiplier of a skill category under a major status condition
    /// </summary>
    [Serializable]
    public class MultiplyCategoryInMajorStatusEvent : BattleEvent
    {
        
        /// <summary>
        /// The skill category affected
        /// </summary>
        public BattleData.SkillCategory Category;
        
        /// <summary>
        /// The numerator of the modifier
        /// </summary>
        public int Numerator;
        
        /// <summary>
        /// The denominator of the modifier
        /// </summary>
        public int Denominator;
        
        /// <summary>
        /// Whether to affect the user or target
        /// </summary>
        public bool AffectTarget;

        public MultiplyCategoryInMajorStatusEvent() { }
        public MultiplyCategoryInMajorStatusEvent(BattleData.SkillCategory category, int numerator, int denominator, bool affectTarget)
        {
            Category = category;
            Numerator = numerator;
            Denominator = denominator;
            AffectTarget = affectTarget;
        }
        protected MultiplyCategoryInMajorStatusEvent(MultiplyCategoryInMajorStatusEvent other)
        {
            Category = other.Category;
            Numerator = other.Numerator;
            Denominator = other.Denominator;
            AffectTarget = other.AffectTarget;
        }
        public override GameEvent Clone() { return new MultiplyCategoryInMajorStatusEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = (AffectTarget ? context.Target : context.User);

            if (context.Data.Category == Category || Category == BattleData.SkillCategory.None)
            {
                foreach (StatusEffect status in target.IterateStatusEffects())
                {
                    if (status.StatusStates.Contains<MajorStatusState>())
                    {
                        context.AddContextStateMult<DmgMult>(false, Numerator, Denominator);
                        break;
                    }
                }
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that modifies the damage multiplier of a skill category if not affected by the specified status condition.
    /// </summary>
    [Serializable]
    public class MultiplyCategoryWithoutStatusEvent : BattleEvent
    {
        
        /// <summary>
        /// The status condition being checked for
        /// </summary>
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string StatusID;
        
        /// <summary>
        /// The skill category affected
        /// </summary>
        public BattleData.SkillCategory Category;
        
        /// <summary>
        /// The numerator of the modifier
        /// </summary>
        public int Numerator;
        
        /// <summary>
        /// The denominator of the modifier
        /// </summary>
        public int Denominator;
        
        /// <summary>
        /// Whether to affect the user or target
        /// </summary>
        public bool AffectTarget;

        public MultiplyCategoryWithoutStatusEvent() { StatusID = ""; }
        public MultiplyCategoryWithoutStatusEvent(string statusID, BattleData.SkillCategory category, int numerator, int denominator, bool affectTarget)
        {
            StatusID = statusID;
            Category = category;
            Numerator = numerator;
            Denominator = denominator;
            AffectTarget = affectTarget;
        }
        protected MultiplyCategoryWithoutStatusEvent(MultiplyCategoryWithoutStatusEvent other)
        {
            StatusID = other.StatusID;
            Category = other.Category;
            Numerator = other.Numerator;
            Denominator = other.Denominator;
            AffectTarget = other.AffectTarget;
        }
        public override GameEvent Clone() { return new MultiplyCategoryWithoutStatusEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = (AffectTarget ? context.Target : context.User);

            if (context.Data.Category == Category || Category == BattleData.SkillCategory.None)
            {
                if (target.GetStatusEffect(StatusID) == null)
                    context.AddContextStateMult<DmgMult>(false, Numerator, Denominator);
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that modifies the damage multiplier of a skill category if affected by the specified status condition
    /// </summary>
    [Serializable]
    public class MultiplyCategoryInStatusEvent : BattleEvent
    {
        
        /// <summary>
        /// The status condition being checked for
        /// </summary>
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string StatusID;
        
        /// <summary>
        /// The skill category affected
        /// </summary>
        public BattleData.SkillCategory Category;
        
        /// <summary>
        /// The numerator of the modifier
        /// </summary>
        public int Numerator;
        
        /// <summary>
        /// The denominator of the modifier
        /// </summary>
        public int Denominator;
        
        /// <summary>
        /// Whether to affect the user or target
        /// </summary>
        public bool AffectTarget;

        public MultiplyCategoryInStatusEvent() { StatusID = ""; }
        public MultiplyCategoryInStatusEvent(string statusID, BattleData.SkillCategory category, int numerator, int denominator, bool affectTarget)
        {
            StatusID = statusID;
            Category = category;
            Numerator = numerator;
            Denominator = denominator;
            AffectTarget = affectTarget;
        }
        protected MultiplyCategoryInStatusEvent(MultiplyCategoryInStatusEvent other)
        {
            StatusID = other.StatusID;
            Category = other.Category;
            Numerator = other.Numerator;
            Denominator = other.Denominator;
            AffectTarget = other.AffectTarget;
        }
        public override GameEvent Clone() { return new MultiplyCategoryInStatusEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = (AffectTarget ? context.Target : context.User);

            if (context.Data.Category == Category || Category == BattleData.SkillCategory.None)
            {
                if (target.GetStatusEffect(StatusID) != null)
                    context.AddContextStateMult<DmgMult>(false, Numerator, Denominator);
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that makes the move never miss and always land a critical hit if the move is on its last PP
    /// </summary>
    [Serializable]
    public class BetterOddsEvent : BattleEvent
    {
        public override GameEvent Clone() { return new BetterOddsEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Skill && context.UsageSlot > BattleContext.DEFAULT_ATTACK_SLOT && context.UsageSlot < CharData.MAX_SKILL_SLOTS)
            {
                Skill move = context.User.Skills[context.UsageSlot].Element;
                if (!String.IsNullOrEmpty(move.SkillNum) && move.Charges == 0)
                {
                    context.Data.HitRate = -1;
                    context.AddContextStateInt<CritLevel>(4);
                }
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that sets the accuracy of the move
    /// </summary>
    [Serializable]
    public class SetAccuracyEvent : BattleEvent
    {
        
        /// <summary>
        /// The new accuracy
        /// </summary>
        public int Accuracy;

        public SetAccuracyEvent() { }
        public SetAccuracyEvent(int accuracy)
        {
            Accuracy = accuracy;
        }
        protected SetAccuracyEvent(SetAccuracyEvent other)
        {
            Accuracy = other.Accuracy;
        }
        public override GameEvent Clone() { return new SetAccuracyEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            context.Data.HitRate = Accuracy;
            yield break;
        }
    }

    
    /// <summary>
    /// Event that modifies the damage multiplier
    /// </summary>
    [Serializable]
    public class MultiplyDamageEvent : BattleEvent
    {
        
        /// <summary>
        /// The numerator of the modifier
        /// </summary>
        public int Numerator;
        
        /// <summary>
        /// The denominator of the modifier
        /// </summary>
        public int Denominator;
        
        /// <summary>
        /// The list of battle events that will be applied
        /// </summary>
        public List<BattleEvent> Anims;

        public MultiplyDamageEvent()
        {
            Anims = new List<BattleEvent>();
        }
        public MultiplyDamageEvent(int numerator, int denominator)
        {
            Numerator = numerator;
            Denominator = denominator;
            Anims = new List<BattleEvent>();
        }
        public MultiplyDamageEvent(int numerator, int denominator, params BattleEvent[] anims)
        {
            Numerator = numerator;
            Denominator = denominator;

            Anims = new List<BattleEvent>();
            Anims.AddRange(anims);
        }
        protected MultiplyDamageEvent(MultiplyDamageEvent other)
        {
            Numerator = other.Numerator;
            Denominator = other.Denominator;

            Anims = new List<BattleEvent>();
            foreach (BattleEvent anim in other.Anims)
                Anims.Add((BattleEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new MultiplyDamageEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Data.Category == BattleData.SkillCategory.Physical || context.Data.Category == BattleData.SkillCategory.Magical)
            {
                foreach (BattleEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                context.AddContextStateMult<DmgMult>(false, Numerator, Denominator);
            }
        }
    }

    /// <summary>
    /// Event that modifies the damage multiplier if the user's gender is the same as the target
    /// </summary>
    [Serializable]
    public class RivalryEvent : BattleEvent
    {
        public override GameEvent Clone() { return new RivalryEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.User.CurrentForm.Gender == context.Target.CurrentForm.Gender)
                context.AddContextStateMult<DmgMult>(false, 5, 4);
            yield break;
        }
    }

    /// <summary>
    /// Event that modifies the damage multiplier based on the strikes made divided by a denominator
    /// </summary>
    [Serializable]
    public class RepeatStrikeEvent : BattleEvent
    {
        /// <summary>
        /// The denominator of the modifier
        /// </summary>
        public int Denominator;

        public RepeatStrikeEvent() { }
        public RepeatStrikeEvent(int denominator)
        {
            Denominator = denominator;
        }
        protected RepeatStrikeEvent(RepeatStrikeEvent other)
        {
            Denominator = other.Denominator;
        }
        public override GameEvent Clone() { return new RepeatStrikeEvent(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            context.AddContextStateMult<DmgMult>(false, context.StrikesMade + 1, Denominator);
            yield break;
        }
    }

    /// <summary>
    /// Event that boosts the damage multiplier based on moves used consecutively until a different move is used
    /// </summary>
    [Serializable]
    public class RepeatHitEvent : BattleEvent
    {
        
        /// <summary>
        /// The status that contains the last used move in IDState status state
        /// This should usually be "last_used_move"
        /// </summary>
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string LastMoveStatusID;
        
        /// <summary>
        /// The status that contains how times a move is used in the CountDownState status state
        /// This should usually be "times_move_used"
        /// </summary>
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string MoveRepeatStatusID;
        
        /// <summary>
        /// The maximum numerator of the move calculated by the denominator + how many times the same move is used
        /// </summary>
        public int Maximum;
        
        /// <summary>
        /// The denominator of the modifier
        /// </summary>
        public int Denominator;

        /// <summary>
        /// Whether the move must be used every turn in order to count
        /// </summary>
        public bool EveryTurn;

        public RepeatHitEvent() { LastMoveStatusID = ""; MoveRepeatStatusID = ""; }
        public RepeatHitEvent(string moveStatusID, string repeatStatusID, int maximum, int denominator, bool everyTurn)
        {
            LastMoveStatusID = moveStatusID;
            MoveRepeatStatusID = repeatStatusID;
            Maximum = maximum;
            Denominator = denominator;
            EveryTurn = everyTurn;
        }
        protected RepeatHitEvent(RepeatHitEvent other)
        {
            LastMoveStatusID = other.LastMoveStatusID;
            MoveRepeatStatusID = other.MoveRepeatStatusID;
            Maximum = other.Maximum;
            Denominator = other.Denominator;
            EveryTurn = other.EveryTurn;
        }
        public override GameEvent Clone() { return new RepeatHitEvent(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            //check that the last move used is equal to this move
            StatusEffect moveStatus = context.User.GetStatusEffect(LastMoveStatusID);
            StatusEffect repeatStatus = context.User.GetStatusEffect(MoveRepeatStatusID);
            if (moveStatus == null || repeatStatus == null)
                yield break;
            if (moveStatus.StatusStates.GetWithDefault<IDState>().ID != context.Data.ID)
                yield break;
            if (!repeatStatus.StatusStates.Contains<RecentState>())
                yield break;
            if (EveryTurn && repeatStatus.StatusStates.GetWithDefault<CountDownState>().Counter > 1)
                yield break;

            int repetitions = repeatStatus.StatusStates.GetWithDefault<CountState>().Count;
            context.AddContextStateMult<DmgMult>(false, Math.Min(Maximum, Denominator + repetitions), Denominator);
        }
    }

    /// <summary>
    /// Event that boosts moves with low base power
    /// </summary>
    [Serializable]
    public class TechnicianEvent : BattleEvent
    {
        public TechnicianEvent() { }
        public override GameEvent Clone() { return new TechnicianEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            BasePowerState basePower = context.Data.SkillStates.GetWithDefault<BasePowerState>();
            if (basePower != null && basePower.Power <= 40)
                context.AddContextStateMult<DmgMult>(false, 3, 2);
            yield break;
        }
    }

    /// <summary>
    /// Event that modifies the damage multiplier based on how effective the move is
    /// </summary>
    [Serializable]
    public class MultiplyEffectiveEvent : BattleEvent
    {
        
        /// <summary>
        /// Whether to check if the move is not effective instead
        /// </summary>
        public bool Reverse;
        
        /// <summary>
        /// The numerator of the modifier
        /// </summary>
        public int Numerator;
        
        /// <summary>
        /// The denominator of the modifier
        /// </summary>
        public int Denominator;
        
        /// <summary>
        /// The list of battle VFXs played if the move type matches
        /// </summary>
        public List<BattleAnimEvent> Anims;

        public MultiplyEffectiveEvent()
        {
            Anims = new List<BattleAnimEvent>();
        }
        public MultiplyEffectiveEvent(bool reverse, int numerator, int denominator)
        {
            Reverse = reverse;
            Numerator = numerator;
            Denominator = denominator;

            Anims = new List<BattleAnimEvent>();
        }
        public MultiplyEffectiveEvent(bool reverse, int numerator, int denominator, params BattleAnimEvent[] anims)
        {
            Reverse = reverse;
            Numerator = numerator;
            Denominator = denominator;

            Anims = new List<BattleAnimEvent>();
            Anims.AddRange(anims);
        }
        protected MultiplyEffectiveEvent(MultiplyEffectiveEvent other)
        {
            Reverse = other.Reverse;
            Numerator = other.Numerator;
            Denominator = other.Denominator;

            Anims = new List<BattleAnimEvent>();
            foreach (BattleAnimEvent anim in other.Anims)
                Anims.Add((BattleAnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new MultiplyEffectiveEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int typeMatchup = PreTypeEvent.GetDualEffectiveness(context.User, context.Target, context.Data);
            typeMatchup -= PreTypeEvent.NRM_2;
            if (Reverse)
                typeMatchup *= -1;
            if (typeMatchup > 0)
            {
                foreach (BattleAnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                context.AddContextStateMult<DmgMult>(false, Numerator, Denominator);
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
    /// Event that only allows super-effective moves to hit
    /// </summary>
    [Serializable]
    public class WonderGuardEvent : BattleEvent
    {
        /// <summary>
        /// The list of battle VFXs played if the move type matches
        /// </summary>
        public List<BattleAnimEvent> Anims;

        public WonderGuardEvent()
        {
            Anims = new List<BattleAnimEvent>();
        }
        public WonderGuardEvent(params BattleAnimEvent[] anims)
        {
            Anims = new List<BattleAnimEvent>();
            Anims.AddRange(anims);
        }
        protected WonderGuardEvent(WonderGuardEvent other)
        {
            Anims = new List<BattleAnimEvent>();
            foreach (BattleAnimEvent anim in other.Anims)
                Anims.Add((BattleAnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new WonderGuardEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            //typeless attacks bypass
            if (context.Data.Element == DataManager.Instance.DefaultElement)
                yield break;

            int typeMatchup = PreTypeEvent.GetDualEffectiveness(context.User, context.Target, context.Data);
            if (typeMatchup <= PreTypeEvent.NRM_2 && (context.Data.Category == BattleData.SkillCategory.Physical || context.Data.Category == BattleData.SkillCategory.Magical))
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_PROTECT_WITH").ToLocal(), ownerChar.GetDisplayName(false), owner.GetDisplayName()));

                foreach (BattleAnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                context.AddContextStateMult<AccMult>(false, -1, 1);
            }
            yield break;
        }
    }

    /// <summary>
    /// UNUSED
    /// Event that causes the move to deal no damage if the target is not at full HP
    /// </summary>
    [Serializable]
    public class FullHPNeededEvent : BattleEvent
    {
        public override GameEvent Clone() { return new FullHPNeededEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Target.HP < context.Target.MaxHP)
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_FULL_HP_REQ").ToLocal(), context.Target.GetDisplayName(false)));
                context.AddContextStateMult<AccMult>(false, -1, 1);
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that causes the move to deal no damage if the target is part of a explorer team
    /// </summary>
    [Serializable]
    public class ExplorerImmuneEvent : BattleEvent
    {
        public override GameEvent Clone() { return new ExplorerImmuneEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Target.MemberTeam is ExplorerTeam)
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_EXPLORER_IMMUNE").ToLocal(), context.Target.GetDisplayName(false)));
                context.AddContextStateMult<DmgMult>(false, -1, 1);
            }
            yield break;
        }
    }

    /// <summary>
    /// UNUSED
    /// Event that causes the move to deal no damage if the target's level is higher than the user
    /// </summary>
    [Serializable]
    public class HigherLevelImmuneEvent : BattleEvent
    {
        public override GameEvent Clone() { return new HigherLevelImmuneEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Target.Level > context.User.Level)
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_LEVEL_IMMUNE").ToLocal(), context.Target.GetDisplayName(false)));
                context.AddContextStateMult<DmgMult>(false, -1, 1);
            }
            yield break;
        }
    }

    /// <summary>
    /// UNUSED
    /// Event that causes a move to deal no damage if it's a OHKO move
    /// </summary>
    [Serializable]
    public class OHKOImmuneEvent : BattleEvent
    {
        public override GameEvent Clone() { return new OHKOImmuneEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            bool ohko = false;
            foreach (BattleEvent effect in context.Data.OnHits.EnumerateInOrder())
            {
                if (effect is OHKODamageEvent)
                {
                    ohko = true;
                    break;
                }
            }
            if (ohko)
                context.AddContextStateMult<DmgMult>(false, 0, 1);
            yield break;
        }
    }

    /// <summary>
    /// Event that modifies the damage multiplier for explosion splash damage
    /// </summary>
    [Serializable]
    public class BlastProofEvent : BattleEvent
    {
        /// <summary>
        /// Protects the target from explosion splash damage up to this many tiles away
        /// </summary>
        public int Range;
        
        /// <summary>
        /// Numerator of the modifier
        /// </summary>
        public int Numerator;
        
        /// <summary>
        /// Denominator of the modifier
        /// </summary>
        public int Denominator;
        
        /// <summary>
        /// The list of battle VFXs played if the condition is met
        /// </summary>
        public List<BattleAnimEvent> Anims;
        
        /// <summary>
        /// Whether to log the message if the condition is met
        /// </summary>
        public bool Msg;

        public BlastProofEvent() { Anims = new List<BattleAnimEvent>(); }
        public BlastProofEvent(int range, int numerator, int denominator, bool msg, params BattleAnimEvent[] anims)
        {
            Range = range;
            Numerator = numerator;
            Denominator = denominator;
            Msg = msg;
            Anims = new List<BattleAnimEvent>();
            Anims.AddRange(anims);
        }
        protected BlastProofEvent(BlastProofEvent other)
        {
            Range = other.Range;
            Numerator = other.Numerator;
            Denominator = other.Denominator;
            Msg = other.Msg;
            Anims = new List<BattleAnimEvent>();
            foreach (BattleAnimEvent anim in other.Anims)
                Anims.Add((BattleAnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new BlastProofEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            //only block explosions
            if (context.Explosion.Range == 0)
                yield break;

            //make sure to exempt round?

            if (!ZoneManager.Instance.CurrentMap.InRange(context.ExplosionTile, context.Target.CharLoc, Range - 1))
            {
                if (Msg)
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_PROTECT_WITH").ToLocal(), ownerChar.GetDisplayName(false), owner.GetDisplayName()));
                foreach (BattleAnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                context.AddContextStateMult<DmgMult>(false, Numerator, Denominator);
                if (Numerator > 0)
                    context.AddContextStateMult<HPDmgMult>(false, Numerator, Denominator);
            }
            yield break;
        }
    }

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
    /// Event that modifies the range of the skill category affected
    /// </summary>
    [Serializable]
    public class CategoryAddRangeEvent : BattleEvent
    {
        
        /// <summary>
        /// The affected skill category
        /// </summary> 
        public BattleData.SkillCategory Category;
        
        /// <summary>
        /// The range modifer
        /// </summary>
        public int Range;

        public CategoryAddRangeEvent() { }
        public CategoryAddRangeEvent(BattleData.SkillCategory category, int range)
        {
            Category = category;
            Range = range;
        }
        protected CategoryAddRangeEvent(CategoryAddRangeEvent other)
        {
            Category = other.Category;
            Range = other.Range;
        }
        public override GameEvent Clone() { return new CategoryAddRangeEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (Category == BattleData.SkillCategory.None || context.Data.Category == Category)
                context.RangeMod += Range;
            yield break;
        }
    }

    /// <summary>
    /// Event that modifies the range of moves under a map status
    /// </summary>
    [Serializable]
    public class WeatherAddRangeEvent : BattleEvent
    {
        
        /// <summary>
        /// The map status to check for
        /// </summary>
        [JsonConverter(typeof(MapStatusConverter))]
        [DataType(0, DataManager.DataType.MapStatus, false)]
        public string WeatherID;
        
        /// <summary>
        /// The range modifer
        /// </summary>
        public int Range;

        public WeatherAddRangeEvent() { WeatherID = ""; }
        public WeatherAddRangeEvent(string weatherId, int range)
        {
            WeatherID = weatherId;
            Range = range;
        }
        protected WeatherAddRangeEvent(WeatherAddRangeEvent other)
        {
            WeatherID = other.WeatherID;
            Range = other.Range;
        }
        public override GameEvent Clone() { return new WeatherAddRangeEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (ZoneManager.Instance.CurrentMap.Status.ContainsKey(WeatherID))
                context.RangeMod += Range;
            yield break;
        }
    }


    /// <summary>
    /// Event that modifies hitbox action of moves to hit tiles
    /// </summary>
    [Serializable]
    public class MeleeHitTilesEvent : BattleEvent
    {
    
        /// <summary>
        /// USUSED
        /// </summary>
        public TileAlignment Tile;

        public MeleeHitTilesEvent() { }
        public MeleeHitTilesEvent(TileAlignment tile)
        {
            Tile = tile;
        }
        protected MeleeHitTilesEvent(MeleeHitTilesEvent other)
        {
            Tile = other.Tile;
        }
        public override GameEvent Clone() { return new MeleeHitTilesEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType != BattleActionType.Skill)
                yield break;

            if (context.HitboxAction is AttackAction)
            {
                ((AttackAction)context.HitboxAction).HitTiles = true;
                ((AttackAction)context.HitboxAction).WideAngle = AttackCoverage.FrontAndCorners;
            }
            else if (context.HitboxAction is DashAction)
            {
                context.Explosion.HitTiles = true;
                ((DashAction)context.HitboxAction).WideAngle = LineCoverage.FrontAndCorners;
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that modifies the hitbox action to pierce through enemies and walls
    /// </summary>
    [Serializable]
    public class PierceEvent : BattleEvent
    {
        /// <summary>
        /// Whether to allow moves to pierce
        /// </summary>
        public bool SkillsPierce;
        
        /// <summary>
        /// Whether to allow items to pierce
        /// </summary>
        public bool ItemsPierce;
        
        /// <summary>
        /// Whether the action can pierce through enemies
        /// </summary>
        public bool PierceEnemies;
        
        /// <summary>
        /// Whether the action can pierce through walls
        /// </summary>
        public bool PierceWalls;

        public PierceEvent() { }
        public PierceEvent(bool skills, bool items, bool enemies, bool walls)
        {
            SkillsPierce = skills;
            ItemsPierce = items;
            PierceEnemies = enemies;
            PierceWalls = walls;
        }
        protected PierceEvent(PierceEvent other)
        {
            SkillsPierce = other.SkillsPierce;
            ItemsPierce = other.ItemsPierce;
            PierceEnemies = other.PierceEnemies;
            PierceWalls = other.PierceWalls;
        }
        public override GameEvent Clone() { return new PierceEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Throw)
            {
                if (!ItemsPierce)
                    yield break;
                //can't pierce-throw edibles
                ItemData entry = DataManager.Instance.GetItem(context.Item.ID);
                if (entry.ItemStates.Contains<EdibleState>())
                    yield break;
            }
            else if (context.ActionType == BattleActionType.Skill)
            {
                if (!SkillsPierce)
                    yield break;
            }
            else
            {
                yield break;
            }

            if (context.HitboxAction is LinearAction)
            {
                if (PierceEnemies)
                    ((LinearAction)context.HitboxAction).StopAtHit = false;
                if (PierceWalls)
                    ((LinearAction)context.HitboxAction).StopAtWall = false;
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that modifies the hitbox action to stop piercing through enemies and walls
    /// </summary>
    [Serializable]
    public class NoPierceEvent : BattleEvent
    {
        /// <summary>
        /// Whether the action should stop piercing enemies
        /// </summary>
        public bool PierceEnemies;
        
        /// <summary>
        /// Whether the action should stop piercing walls
        /// </summary>
        public bool PierceWalls;

        public NoPierceEvent() { }
        public NoPierceEvent(bool enemies, bool walls)
        {
            PierceEnemies = enemies;
            PierceWalls = walls;
        }
        protected NoPierceEvent(NoPierceEvent other)
        {
            PierceEnemies = other.PierceEnemies;
            PierceWalls = other.PierceWalls;
        }
        public override GameEvent Clone() { return new NoPierceEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.HitboxAction is LinearAction)
            {
                if (PierceEnemies)
                    ((LinearAction)context.HitboxAction).StopAtHit = true;
                if (PierceWalls)
                    ((LinearAction)context.HitboxAction).StopAtWall = true;
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that modifies the amount of ray projectiles of an action
    /// </summary>
    [Serializable]
    public class SpreadProjectileEvent : BattleEvent
    {
    
        /// <summary>
        /// The ray projectile amount
        /// </summary>
        public ProjectileAction.RayCount Rays;

        public SpreadProjectileEvent() { }
        public SpreadProjectileEvent(ProjectileAction.RayCount rays)
        {
            Rays = rays;
        }
        protected SpreadProjectileEvent(SpreadProjectileEvent other)
        {
            Rays = other.Rays;
        }
        public override GameEvent Clone() { return new SpreadProjectileEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {

            if (context.HitboxAction is ProjectileAction)
            {
                ((ProjectileAction)context.HitboxAction).Rays = Rays;
            }
            yield break;
        }
    }
    
    /// <summary>
    /// UNUSED
    /// Event that makes dash or attack actions wide.
    /// </summary>
    [Serializable]
    public class MakeWideEvent : BattleEvent
    {
        public override GameEvent Clone() { return new MakeWideEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {

            if (context.HitboxAction is AttackAction)
            {
                ((AttackAction)context.HitboxAction).WideAngle = AttackCoverage.Wide;
                ((AttackAction)context.HitboxAction).CharAnimData = new CharAnimFrameType(40);//Swing
            }
            else if (context.HitboxAction is DashAction)
            {
                ((DashAction)context.HitboxAction).WideAngle = LineCoverage.Wide;
                ((DashAction)context.HitboxAction).CharAnim = 40;//Swing
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that protects the user from all moves
    /// </summary>
    [Serializable]
    public class ProtectEvent : BattleEvent
    {
        /// <summary>
        /// OBSOLETE
        /// </summary>
        [NonEdited]
        public List<BattleAnimEvent> Anims;

        /// <summary>
        /// The list of battle events applied if the condition is met
        /// </summary>
        public List<BattleEvent> Effects;

        public ProtectEvent()
        {
            Effects = new List<BattleEvent>();
        }
        public ProtectEvent(params BattleEvent[] anims)
        {
            Effects = new List<BattleEvent>();
            Effects.AddRange(anims);
        }
        protected ProtectEvent(ProtectEvent other)
        {
            Effects = new List<BattleEvent>();
            foreach (BattleEvent anim in other.Effects)
                Effects.Add((BattleEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new ProtectEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.User != context.Target)
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_PROTECT").ToLocal(), context.Target.GetDisplayName(false)));

                foreach (BattleEvent anim in Effects)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                context.AddContextStateMult<AccMult>(false, -1, 1);
            }
            yield break;
        }

        [OnDeserialized]
        internal void OnDeserializedMethod(StreamingContext context)
        {
            //TODO: remove on v1.1
            if (Serializer.OldVersion < new Version(0, 7, 15) && Anims != null)
            {
                Effects = new List<BattleEvent>();
                Effects.AddRange(Anims);
            }
        }
    }

    /// <summary>
    /// Event that modifies the damage multplier if the user has the specified status condition
    /// </summary>
    [Serializable]
    public class MultWhenMissEvent : BattleEvent
    {
        
        /// <summary>
        /// The status condition being checked for
        /// </summary>
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string StatusID;

        /// <summary>
        /// Numerator of the modifier
        /// </summary>
        public int Numerator;
        
        /// <summary>
        /// Denominator of the modifier
        /// </summary>
        public int Denominator;

        public MultWhenMissEvent() { StatusID = ""; }
        public MultWhenMissEvent(string statusID, int numerator, int denominator)
        {
            StatusID = statusID;
            Numerator = numerator;
            Denominator = denominator;
        }
        protected MultWhenMissEvent(MultWhenMissEvent other)
        {
            StatusID = other.StatusID;
            Numerator = other.Numerator;
            Denominator = other.Denominator;
        }
        public override GameEvent Clone() { return new MultWhenMissEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.User.GetStatusEffect(StatusID) != null && DungeonScene.Instance.GetMatchup(context.User, context.Target) == Alignment.Foe)
            {
                context.AddContextStateMult<DmgMult>(false, Numerator, Denominator);
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that modifies the accuracy rate if the target has the specified status condition
    /// </summary>
    [Serializable]
    public class EvasiveWhenMissEvent : BattleEvent
    {
        /// <summary>
        /// The status condition being checked for
        /// </summary>
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string StatusID;

        public EvasiveWhenMissEvent() { StatusID = ""; }
        public EvasiveWhenMissEvent(string statusID)
        {
            StatusID = statusID;
        }
        protected EvasiveWhenMissEvent(EvasiveWhenMissEvent other)
        {
            StatusID = other.StatusID;
        }
        public override GameEvent Clone() { return new EvasiveWhenMissEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Target.GetStatusEffect(StatusID) != null && DungeonScene.Instance.GetMatchup(context.User, context.Target) == Alignment.Foe)
            {
                context.AddContextStateMult<AccMult>(false, 2, 3);
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that modifies the accuracy rate if the target is below the specified HP threshold
    /// </summary>
    [Serializable]
    public class EvasiveInPinchEvent : BattleEvent
    {
        public override GameEvent Clone() { return new EvasiveInPinchEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (DungeonScene.Instance.GetMatchup(context.User, context.Target) == Alignment.Foe)
            {
                if (context.Target.HP < context.Target.MaxHP / 3)
                {
                    context.AddContextStateMult<AccMult>(false, 1, 3);
                }
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that decreases the accuracy rate the further away the distance of the action
    /// </summary>
    [Serializable]
    public class EvasiveInDistanceEvent : BattleEvent
    {
        public override GameEvent Clone() { return new EvasiveInDistanceEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Skill && context.Data.ID != DataManager.Instance.DefaultSkill)
            {
                if (DungeonScene.Instance.GetMatchup(context.User, context.Target) == Alignment.Foe)
                {
                    int diff = ZoneManager.Instance.CurrentMap.GetClosestDist8(context.StrikeStartTile, context.Target.CharLoc);
                    if (diff > 1)
                        context.AddContextStateMult<AccMult>(false, 4, 3 + diff);
                }
            }
            yield break;
        }
    }


    /// <summary>
    /// Event that decreases the accuracy rate the further away the distance of the action
    /// </summary>
    [Serializable]
    public class EvasiveCloseUpEvent : BattleEvent
    {
        public override GameEvent Clone() { return new EvasiveCloseUpEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Skill && context.Data.ID != DataManager.Instance.DefaultSkill)
            {
                if (DungeonScene.Instance.GetMatchup(context.User, context.Target) == Alignment.Foe)
                {
                    if (ZoneManager.Instance.CurrentMap.InRange(context.StrikeStartTile, context.Target.CharLoc, 1))
                        context.AddContextStateMult<AccMult>(false, 1, 2);
                }
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that causes the move to miss if the target has the specified status condition
    /// </summary>
    [Serializable]
    public class EvadeInStatusEvent : BattleEvent
    {
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string StatusID;

        public EvadeInStatusEvent() { StatusID = ""; }
        public EvadeInStatusEvent(string statusID)
        {
            StatusID = statusID;
        }
        protected EvadeInStatusEvent(EvadeInStatusEvent other)
        {
            StatusID = other.StatusID;
        }
        public override GameEvent Clone() { return new EvadeInStatusEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Skill && context.Data.ID != DataManager.Instance.DefaultSkill)
            {
                if (context.Target.GetStatusEffect(StatusID) != null && DungeonScene.Instance.GetMatchup(context.User, context.Target) == Alignment.Foe)
                {
                    if (!ZoneManager.Instance.CurrentMap.InRange(context.StrikeStartTile, context.Target.CharLoc, 1) && context.Data.HitRate > -1)
                    {
                        DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_AVOID").ToLocal(), context.Target.GetDisplayName(false), owner.GetDisplayName()));
                        context.AddContextStateMult<AccMult>(false, -1, 1);
                    }
                }
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that causes the move to miss if the user uses their strongest base power move
    /// </summary>
    [Serializable]
    public class EvadeStrongestEvent : BattleEvent
    {
        public override GameEvent Clone() { return new EvadeStrongestEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Skill && context.UsageSlot > BattleContext.DEFAULT_ATTACK_SLOT && context.UsageSlot < CharData.MAX_SKILL_SLOTS && DungeonScene.Instance.GetMatchup(context.User, context.Target) == Alignment.Foe)
            {
                int recordSlot = -1;
                int recordPower = -1;
                for (int ii = 0; ii < context.User.Skills.Count; ii++)
                {
                    if (!String.IsNullOrEmpty(context.User.Skills[ii].Element.SkillNum))
                    {
                        SkillData entry = DataManager.Instance.GetSkill(context.User.Skills[ii].Element.SkillNum);

                        int basePower = 0;
                        if (entry.Data.Category == BattleData.SkillCategory.Status)
                            basePower = -1;
                        else
                        {
                            BasePowerState state = entry.Data.SkillStates.GetWithDefault<BasePowerState>();
                            if (state != null)
                                basePower = state.Power;
                        }
                        if (basePower > recordPower)
                        {
                            recordSlot = ii;
                            recordPower = basePower;
                        }
                    }
                }

                if (context.UsageSlot == recordSlot && context.Data.HitRate > -1)
                {
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_AVOID").ToLocal(), ownerChar.GetDisplayName(false), owner.GetDisplayName()));
                    context.AddContextStateMult<AccMult>(false, -1, 1);
                }
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that causes the user's strongest super-effective move to miss
    /// </summary>
    [Serializable]
    public class EvadeStrongestEffectiveEvent : BattleEvent
    {
        public override GameEvent Clone() { return new EvadeStrongestEffectiveEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Skill && context.UsageSlot > BattleContext.DEFAULT_ATTACK_SLOT && context.UsageSlot < CharData.MAX_SKILL_SLOTS && DungeonScene.Instance.GetMatchup(context.User, context.Target) == Alignment.Foe)
            {
                int recordSlot = -1;
                int recordPower = -1;
                for (int ii = 0; ii < context.User.Skills.Count; ii++)
                {
                    if (!String.IsNullOrEmpty(context.User.Skills[ii].Element.SkillNum))
                    {
                        SkillData entry = DataManager.Instance.GetSkill(context.User.Skills[ii].Element.SkillNum);

                        int typeMatchup = PreTypeEvent.GetDualEffectiveness(context.User, context.Target, entry.Data);

                        if (typeMatchup > PreTypeEvent.NRM_2)
                        {
                            int basePower = 0;
                            if (entry.Data.Category == BattleData.SkillCategory.Status)
                                basePower = -1;
                            else
                            {
                                BasePowerState state = entry.Data.SkillStates.GetWithDefault<BasePowerState>();
                                if (state != null)
                                    basePower = state.Power;
                            }
                            if (basePower > recordPower)
                            {
                                recordSlot = ii;
                                recordPower = basePower;
                            }
                        }
                    }
                }

                if (context.UsageSlot == recordSlot && context.Data.HitRate > -1)
                {
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_AVOID").ToLocal(), ownerChar.GetDisplayName(false), owner.GetDisplayName()));
                    context.AddContextStateMult<AccMult>(false, -1, 1);
                }
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that causes the user's move to miss if it contains one of the specified SkillStates
    /// </summary>
    [Serializable]
    public class EvadeMoveStateEvent : BattleEvent
    {
        /// <summary>
        /// The list of valid SkillStates types
        /// </summary>
        [StringTypeConstraint(1, typeof(SkillState))]
        public List<FlagType> States;
        
        /// <summary>
        /// The list of battle VFXs played if the move type matches
        /// </summary>
        public List<BattleAnimEvent> Anims;

        public EvadeMoveStateEvent()
        {
            States = new List<FlagType>();
            Anims = new List<BattleAnimEvent>();
        }
        public EvadeMoveStateEvent(Type state, params BattleAnimEvent[] anims) : this()
        {
            States.Add(new FlagType(state));
            Anims.AddRange(anims);
        }
        protected EvadeMoveStateEvent(EvadeMoveStateEvent other) : this()
        {
            States.AddRange(other.States);
            foreach (BattleAnimEvent anim in other.Anims)
                Anims.Add((BattleAnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new EvadeMoveStateEvent(this); }

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
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_PROTECT_WITH").ToLocal(), ownerChar.GetDisplayName(false), owner.GetDisplayName()));

                foreach (BattleAnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                context.AddContextStateMult<AccMult>(false, -1, 1);
            }
            yield break;
        }

    }

    /// <summary>
    /// Event that causes the action to miss given the specified chance
    /// </summary>
    [Serializable]
    public class CustomHitRateEvent : BattleEvent
    {
        /// <summary>
        /// The numerator of the chance
        /// </summary>
        public int Numerator;
        
        /// <summary>
        /// The denominator of the chance
        /// </summary>
        public int Denominator;

        public CustomHitRateEvent()
        { }
        public CustomHitRateEvent(int numerator, int denominator)
        {
            Numerator = numerator;
            Denominator = denominator;
        }
        protected CustomHitRateEvent(CustomHitRateEvent other) : this()
        {
            Numerator = other.Numerator;
            Denominator = other.Denominator;
        }
        public override GameEvent Clone() { return new CustomHitRateEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Data.HitRate > -1)
            {
                if (DataManager.Instance.Save.Rand.Next(0, Denominator) < Numerator)
                {
                    context.Data.HitRate = -1;
                }
                else
                {
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_MISS").ToLocal(), context.Target.GetDisplayName(false)));
                    context.AddContextStateMult<AccMult>(false, -1, 1);
                }
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that causes the user's move to miss if the target is more than 1 tile away
    /// </summary>
    [Serializable]
    public class EvadeDistanceEvent : BattleEvent
    {
        /// <summary>
        /// Whether to check if the user is within 1 tile
        /// </summary>
        public bool Inverted;

        public EvadeDistanceEvent() { }
        public EvadeDistanceEvent(bool invert) { Inverted = invert; }
        public EvadeDistanceEvent(EvadeDistanceEvent other)
        {
            Inverted = other.Inverted;
        }

        public override GameEvent Clone() { return new EvadeDistanceEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Skill && context.Data.ID != DataManager.Instance.DefaultSkill)
            {
                if (ZoneManager.Instance.CurrentMap.InRange(context.StrikeStartTile, context.Target.CharLoc, 1) == Inverted && context.Data.HitRate > -1)
                {
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_AVOID").ToLocal(), context.Target.GetDisplayName(false), owner.GetDisplayName()));
                    context.AddContextStateMult<AccMult>(false, -1, 1);
                }
            }
            yield break;
        }
    }


    /// <summary>
    /// Event that causes the action to always hit
    /// </summary>
    [Serializable]
    public class SureShotEvent : BattleEvent
    {
        public override GameEvent Clone() { return new SureShotEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            context.Data.HitRate = -1;
            yield break;
        }
    }

    
    /// <summary>
    /// Event that causes the multi-strike moves to always hit
    /// </summary>
    [Serializable]
    public class SkillLinkEvent : BattleEvent
    {
        public override GameEvent Clone() { return new SkillLinkEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Skill && context.Strikes > 1)
                context.Data.HitRate = -1;
            yield break;
        }
    }

    /// <summary>
    /// Event that causes the user to avoid moves of the specified skill category and alignment
    /// </summary>
    [Serializable]
    public class EvadeCategoryEvent : BattleEvent
    {
        
        /// <summary>
        /// The affected alignments
        /// </summary>
        public Alignment EvadeAlignment;
        
        /// <summary>
        /// The affected skill category
        /// </summary> 
        public BattleData.SkillCategory Category;
        
        /// <summary>
        /// The list of battle VFXs played if the condition is met
        /// </summary>
        public List<BattleAnimEvent> Anims;

        public EvadeCategoryEvent()
        {
            Anims = new List<BattleAnimEvent>();
        }
        public EvadeCategoryEvent(Alignment alignment, BattleData.SkillCategory category, params BattleAnimEvent[] anims)
        {
            EvadeAlignment = alignment;
            Category = category;

            Anims = new List<BattleAnimEvent>();
            Anims.AddRange(anims);
        }
        protected EvadeCategoryEvent(EvadeCategoryEvent other)
        {
            EvadeAlignment = other.EvadeAlignment;
            Category = other.Category;
            Anims = new List<BattleAnimEvent>();
            foreach (BattleAnimEvent anim in other.Anims)
                Anims.Add((BattleAnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new EvadeCategoryEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (((DungeonScene.Instance.GetMatchup(context.User, context.Target) | EvadeAlignment) == EvadeAlignment) && context.Data.Category == Category)
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_PROTECT_WITH").ToLocal(), ownerChar.GetDisplayName(false), owner.GetDisplayName()));

                foreach (BattleAnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                context.AddContextStateMult<AccMult>(false, -1, 1);
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that causes the user to avoid damaging moves of friendly targets
    /// </summary>
    [Serializable]
    public class TelepathyEvent : BattleEvent
    {
        public override GameEvent Clone() { return new TelepathyEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            BasePowerState basePower = context.Data.SkillStates.GetWithDefault<BasePowerState>();
            if (basePower != null && DungeonScene.Instance.GetMatchup(context.User, context.Target) == Alignment.Friend)
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_AVOID").ToLocal(), ownerChar.GetDisplayName(false), owner.GetDisplayName()));
                context.AddContextStateMult<AccMult>(false, -1, 1);
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that causes the user moves to not affect friendly targets
    /// </summary>
    [Serializable]
    public class NontraitorEvent : BattleEvent
    {
        public override GameEvent Clone() { return new NontraitorEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            BasePowerState basePower = context.Data.SkillStates.GetWithDefault<BasePowerState>();
            if (basePower != null && context.ActionType == BattleActionType.Skill)
            {
                context.HitboxAction.TargetAlignments &= ~Alignment.Friend;
                context.Explosion.TargetAlignments &= ~Alignment.Friend;
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that causes damaging battle actions that hit in a straight line to not affect friendly targets
    /// </summary>
    [Serializable]
    public class GapProberEvent : BattleEvent
    {
        public override GameEvent Clone() { return new GapProberEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            BasePowerState basePower = context.Data.SkillStates.GetWithDefault<BasePowerState>();
            if (basePower != null && context.HitboxAction is LinearAction)
            {
                context.HitboxAction.TargetAlignments &= ~Alignment.Friend;
                context.Explosion.TargetAlignments &= ~Alignment.Friend;
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that modifies the accuracy rate
    /// </summary>
    [Serializable]
    public class MultiplyAccuracyEvent : BattleEvent
    {
        public int Numerator;
        public int Denominator;

        public MultiplyAccuracyEvent() { }
        public MultiplyAccuracyEvent(int numerator, int denominator)
        {
            Numerator = numerator;
            Denominator = denominator;
        }
        protected MultiplyAccuracyEvent(MultiplyAccuracyEvent other)
        {
            Numerator = other.Numerator;
            Denominator = other.Denominator;
        }
        public override GameEvent Clone() { return new MultiplyAccuracyEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            context.AddContextStateMult<AccMult>(false, Numerator, Denominator);
            yield break;
        }
    }
    
    /// <summary>
    /// Event that modifies the range
    /// </summary>
    [Serializable]
    public class AddRangeEvent : BattleEvent
    {
        /// <summary>
        /// The range modifier
        /// </summary>
        public int Range;

        public AddRangeEvent() { }
        public AddRangeEvent(int range)
        {
            Range = range;
        }
        protected AddRangeEvent(AddRangeEvent other)
        {
            Range = other.Range;
        }
        public override GameEvent Clone() { return new AddRangeEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            context.RangeMod += Range;
            yield break;
        }
    }
    
    /// <summary>
    /// Event that modifies the range if the user is the specified type
    /// </summary>
    [Serializable]
    public class ElementAddRangeEvent : BattleEvent
    {
        /// <summary>
        /// The list of valid types
        /// </summary>
        [DataType(1, DataManager.DataType.Element, false)]
        public HashSet<string> Elements;
        
        public int Range;
        
        public ElementAddRangeEvent()
        {
            Elements = new HashSet<string>();
        }
        
        public ElementAddRangeEvent(int range, HashSet<string> elements) : this()
        {
            Range = range;
            Elements = elements;
        }
        protected ElementAddRangeEvent(ElementAddRangeEvent other) : this()
        {
            Range = other.Range;
            foreach (string element in other.Elements)
                Elements.Add(element);
        }
        
        public override GameEvent Clone() { return new ElementAddRangeEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (Elements.Contains(context.User.Element1) || Elements.Contains(context.User.Element2))
            {
                context.RangeMod += Range;
            }
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
    /// Event that causes the move to miss if the range of the move is greater than the specified amount
    /// </summary>
    [Serializable]
    public class LongRangeGuardEvent : BattleEvent
    {
        public List<BattleAnimEvent> Anims;

        public LongRangeGuardEvent()
        {
            Anims = new List<BattleAnimEvent>();
        }
        public LongRangeGuardEvent(params BattleAnimEvent[] anims)
        {
            Anims = new List<BattleAnimEvent>();
            Anims.AddRange(anims);
        }
        protected LongRangeGuardEvent(LongRangeGuardEvent other)
        {
            Anims = new List<BattleAnimEvent>();
            foreach (BattleAnimEvent anim in other.Anims)
                Anims.Add((BattleAnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new LongRangeGuardEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.User != context.Target && context.HitboxAction.GetEffectiveDistance() > 2)
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_PROTECT_WITH").ToLocal(), context.Target.GetDisplayName(false), owner.GetDisplayName()));

                foreach (BattleAnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                context.AddContextStateMult<AccMult>(false, -1, 1);
            }
            yield break;
        }
    }
    
    /// <summary>
    /// Event that causes the move to miss if the move is wide or an explosion
    /// </summary>
    [Serializable]
    public class WideGuardEvent : BattleEvent
    {
        
        /// <summary>
        /// The list of battle VFXs played if the condition is met
        /// </summary>
        public List<BattleAnimEvent> Anims;

        public WideGuardEvent()
        {
            Anims = new List<BattleAnimEvent>();
        }
        public WideGuardEvent(params BattleAnimEvent[] anims)
        {
            Anims = new List<BattleAnimEvent>();
            Anims.AddRange(anims);
        }
        protected WideGuardEvent(WideGuardEvent other)
        {
            Anims = new List<BattleAnimEvent>();
            foreach (BattleAnimEvent anim in other.Anims)
                Anims.Add((BattleAnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new WideGuardEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.User != context.Target && (context.HitboxAction.IsWide() || context.Explosion.Range > 0))
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_PROTECT_WITH").ToLocal(), context.Target.GetDisplayName(false), owner.GetDisplayName()));

                foreach (BattleAnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                context.AddContextStateMult<AccMult>(false, -1, 1);
            }
            yield break;
        }
    }
    
    /// <summary>
    /// Event that modifies the battle data if the character is hit by an item
    /// </summary>
    [Serializable]
    public class ThrowItemDestroyEvent : BattleEvent
    {
        /// <summary>
        /// Events that occur when hit by an item
        /// Before it's used, when it hits, after it's used, etc
        /// </summary>
        public BattleData NewData;

        public ThrowItemDestroyEvent() { }
        public ThrowItemDestroyEvent(BattleData moveData)
        {
            NewData = moveData;
        }
        protected ThrowItemDestroyEvent(ThrowItemDestroyEvent other)
        {
            NewData = new BattleData(other.NewData);
        }
        public override GameEvent Clone() { return new ThrowItemDestroyEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Throw)
            {
                ItemData entry = DataManager.Instance.GetItem(context.Item.ID);
                if (!entry.ItemStates.Contains<RecruitState>())
                {
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_INCINERATE").ToLocal(), context.Item.GetDisplayName()));

                    string id = context.Data.ID;
                    DataManager.DataType dataType = context.Data.DataType;
                    context.Data = new BattleData(NewData);
                    context.Data.ID = id;
                    context.Data.DataType = dataType;

                    context.GlobalContextStates.Set(new ItemDestroyed());
                }
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that prevents item of dropping by setting ItemDestroyed global context state
    /// </summary>
    [Serializable]
    public class ThrowItemPreventDropEvent : BattleEvent
    {
        public override GameEvent Clone() { return new ThrowItemPreventDropEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Throw)
            {
                context.GlobalContextStates.Set(new ItemDestroyed());
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that prevents item of dropping by setting ItemDestroyed global context state
    /// </summary>
    [Serializable]
    public class DistanceDropEvent : BattleEvent
    {
        public DistanceDropEvent() { }
        public override GameEvent Clone() { return new DistanceDropEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int diff = ZoneManager.Instance.CurrentMap.GetClosestDist8(context.StrikeStartTile, context.Target.CharLoc);
            for (int ii = 0; ii < diff; ii++)
                context.AddContextStateMult<DmgMult>(false, 1, 2);
            yield break;
        }
    }

    /// <summary>
    /// Event that causes the battle action to miss if it isn't used at max distance.
    /// </summary>
    [Serializable]
    public class TipOnlyEvent : BattleEvent
    {
        public TipOnlyEvent() { }
        public override GameEvent Clone() { return new TipOnlyEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            //TODO: this breaks in small wrapped maps
            int diff = ZoneManager.Instance.CurrentMap.GetClosestDist8(context.StrikeStartTile, context.Target.CharLoc);
            if (diff != context.HitboxAction.GetEffectiveDistance())
                context.AddContextStateMult<AccMult>(false, 0, 1);
            yield break;
        }
    }

    /// <summary>
    /// Event that causes the battle action to miss if the user is the next to the target.
    /// </summary>
    [Serializable]
    public class DistanceOnlyEvent : BattleEvent
    {
        public DistanceOnlyEvent() { }
        public override GameEvent Clone() { return new DistanceOnlyEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (ZoneManager.Instance.CurrentMap.InRange(context.StrikeStartTile, context.Target.CharLoc, 1))
                context.AddContextStateMult<AccMult>(false, 0, 1);
            yield break;
        }
    }
    
    /// <summary>
    /// Event that boosts the battle action damage multiplier the further away the user is from the target
    /// </summary>
    [Serializable]
    public class TipPowerEvent : BattleEvent
    {
        public TipPowerEvent() { }
        public override GameEvent Clone() { return new TipPowerEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            //TODO: this breaks in small wrapped maps
            int diff = ZoneManager.Instance.CurrentMap.GetClosestDist8(context.StrikeStartTile, context.Target.CharLoc);
            for (int ii = 0; ii < diff; ii++)
                context.AddContextStateMult<DmgMult>(false, 2, 1);
            yield break;
        }
    }

    /// <summary>
    /// Event that reflects damage back to the user if the move type matches the specified type
    /// </summary>
    [Serializable]
    public class CounterTypeEvent : BattleEvent
    {
        /// <summary>
        /// The numerator of the damage reflected
        /// </summary>
        public int Numerator;
        
        /// <summary>
        /// The denominator of the damage reflected
        /// </summary>
        public int Denominator;
        
        /// <summary>
        /// The type reflected
        /// </summary>
        [JsonConverter(typeof(ElementConverter))]
        [DataType(0, DataManager.DataType.Element, false)]
        public string CounterElement;

        public CounterTypeEvent() { CounterElement = ""; }
        public CounterTypeEvent(string element, int numerator, int denominator)
        {
            CounterElement = element;
            Numerator = numerator;
            Denominator = denominator;
        }
        protected CounterTypeEvent(CounterTypeEvent other)
        {
            Numerator = other.Numerator;
            Denominator = other.Denominator;
            CounterElement = other.CounterElement;
        }
        public override GameEvent Clone() { return new CounterTypeEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int damage = context.GetContextStateInt<DamageDealt>(0);
            if (damage > 0 && context.ActionType == BattleActionType.Skill && (CounterElement == DataManager.Instance.DefaultElement || context.Data.Element == CounterElement) && DungeonScene.Instance.GetMatchup(context.User, context.Target) != Alignment.Self)
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_REFLECT").ToLocal()));

                int recoil = damage * Numerator / Denominator;

                if (context.User.CharStates.Contains<HitAndRunState>())
                    recoil /= 4;

                if (recoil < 1)
                    recoil = 1;
                yield return CoroutineManager.Instance.StartCoroutine(context.User.InflictDamage(recoil));
            }
        }
    }
    
    /// <summary>
    /// Event that reflects damage back to the user if the action's skill category matches the specified skill category
    /// </summary>
    [Serializable]
    public class CounterCategoryEvent : BattleEvent
    {
        /// <summary>
        /// The numerator of the damage reflected
        /// </summary>
        public int Numerator;
        
        /// <summary>
        /// The denominator of the damage reflected
        /// </summary>
        public int Denominator;
        
        /// <summary>
        /// The skill cateogory affected
        /// </summary>
        public BattleData.SkillCategory Category;
        
        /// <summary>
        /// The list of battle VFXs played if the condition is met
        /// </summary>
        public List<BattleAnimEvent> Anims;

        public CounterCategoryEvent() { Anims = new List<BattleAnimEvent>(); }
        public CounterCategoryEvent(BattleData.SkillCategory category, int numerator, int denominator, params BattleAnimEvent[] anims)
        {
            Category = category;
            Numerator = numerator;
            Denominator = denominator;
            Anims = new List<BattleAnimEvent>();
            Anims.AddRange(anims);
        }
        protected CounterCategoryEvent(CounterCategoryEvent other)
        {
            Numerator = other.Numerator;
            Denominator = other.Denominator;
            Category = other.Category;
            Anims = new List<BattleAnimEvent>();
            foreach (BattleAnimEvent anim in other.Anims)
                Anims.Add((BattleAnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new CounterCategoryEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType != BattleActionType.Skill)
                yield break;
            if (Category != BattleData.SkillCategory.None && context.Data.Category != Category)
                yield break;
            if (context.User.Dead)
                yield break;
            if (DungeonScene.Instance.GetMatchup(context.User, context.Target) == Alignment.Self)
                yield break;

            int damage = context.GetContextStateInt<DamageDealt>(0);
            if (damage > 0)
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_REFLECT").ToLocal()));

                foreach (BattleAnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                int recoil = damage * Numerator / Denominator;

                if (context.User.CharStates.Contains<HitAndRunState>())
                    recoil /= 4;

                if (recoil < 1)
                    recoil = 1;
                yield return CoroutineManager.Instance.StartCoroutine(context.User.InflictDamage(recoil));
            }
        }
    }


    /// <summary>
    /// Event that reflects damage back to the user if the battle action was a regular attack or thrown item.
    /// </summary>
    [Serializable]
    public class CounterNonSkillEvent : BattleEvent
    {
        /// <summary>
        /// The numerator of the damage reflected
        /// </summary>
        public int Numerator;
        
        /// <summary>
        /// The denominator of the damage reflected
        /// </summary>
        public int Denominator;
        
        /// <summary>
        /// The list of battle VFXs played if the condition is met
        /// </summary>
        public List<BattleAnimEvent> Anims;

        public CounterNonSkillEvent() { Anims = new List<BattleAnimEvent>(); }
        public CounterNonSkillEvent(int numerator, int denominator, params BattleAnimEvent[] anims)
        {
            Numerator = numerator;
            Denominator = denominator;
            Anims = new List<BattleAnimEvent>();
            Anims.AddRange(anims);
        }
        protected CounterNonSkillEvent(CounterNonSkillEvent other)
        {
            Numerator = other.Numerator;
            Denominator = other.Denominator;
            Anims = new List<BattleAnimEvent>();
            foreach (BattleAnimEvent anim in other.Anims)
                Anims.Add((BattleAnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new CounterNonSkillEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int damage = context.GetContextStateInt<DamageDealt>(0);
            if (damage > 0 && (context.ActionType == BattleActionType.Throw || (context.ActionType == BattleActionType.Skill && context.UsageSlot == BattleContext.DEFAULT_ATTACK_SLOT)) && DungeonScene.Instance.GetMatchup(context.User, context.Target) != Alignment.Self)
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_REFLECT_BY").ToLocal(), ownerChar.GetDisplayName(false), owner.GetDisplayName()));

                foreach (BattleAnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                int recoil = damage * Numerator / Denominator;

                if (context.User.CharStates.Contains<HitAndRunState>())
                    recoil /= 4;

                if (recoil < 1)
                    recoil = 1;
                yield return CoroutineManager.Instance.StartCoroutine(context.User.InflictDamage(recoil));
            }
        }
    }


    /// <summary>
    /// Event that reflects the HP healed back to the user
    /// </summary>
    [Serializable]
    public class CounterHealEvent : BattleEvent
    {
        /// <summary>
        /// The numerator of the HP reflected
        /// </summary>
        public int Numerator;
        
        /// <summary>
        /// The denominator of the HP reflected
        /// </summary>
        public int Denominator;
        
        /// <summary>
        /// The list of battle VFXs played if the condition is met
        /// </summary>
        public List<BattleAnimEvent> Anims;

        public CounterHealEvent() { Anims = new List<BattleAnimEvent>(); }
        public CounterHealEvent(int numerator, int denominator, params BattleAnimEvent[] anims)
        {
            Numerator = numerator;
            Denominator = denominator;
            Anims = new List<BattleAnimEvent>();
            Anims.AddRange(anims);
        }
        protected CounterHealEvent(CounterHealEvent other)
        {
            Numerator = other.Numerator;
            Denominator = other.Denominator;
            Anims = new List<BattleAnimEvent>();
            foreach (BattleAnimEvent anim in other.Anims)
                Anims.Add((BattleAnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new CounterHealEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int damage = context.GetContextStateInt<DamageHealedTarget>(0);
            if (damage > 0 && (context.ActionType == BattleActionType.Throw || context.ActionType == BattleActionType.Skill || context.ActionType == BattleActionType.Item) && DungeonScene.Instance.GetMatchup(context.User, context.Target) != Alignment.Self)
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_REFLECT_HEAL_BY").ToLocal(), ownerChar.GetDisplayName(false), owner.GetDisplayName()));

                foreach (BattleAnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                int recoil = damage * Numerator / Denominator;
                if (recoil < 1)
                    recoil = 1;
                yield return CoroutineManager.Instance.StartCoroutine(context.User.RestoreHP(recoil));
            }
        }
    }


    /// <summary>
    /// Event that allows the user to move again
    /// </summary>
    [Serializable]
    public class PreserveTurnEvent : BattleEvent
    {
        /// <summary>
        /// The message displayed in the dungeon log  
        /// </summary>
        public StringKey Msg;
        
        /// <summary>
        /// The list of battle VFXs played if the condition is met
        /// </summary>
        public List<BattleAnimEvent> Anims;

        public PreserveTurnEvent()
        {
            Anims = new List<BattleAnimEvent>();
        }
        public PreserveTurnEvent(StringKey msg, params BattleAnimEvent[] anims)
        {
            Msg = msg;
            Anims = new List<BattleAnimEvent>();
            Anims.AddRange(anims);
        }
        protected PreserveTurnEvent(PreserveTurnEvent other)
        {
            Msg = other.Msg;
            Anims = new List<BattleAnimEvent>();
            foreach (BattleAnimEvent anim in other.Anims)
                Anims.Add((BattleAnimEvent)anim.Clone());
        }

        public override GameEvent Clone() { return new PreserveTurnEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            DungeonScene.Instance.LogMsg(Text.FormatGrammar(Msg.ToLocal(), ownerChar.GetDisplayName(false), owner.GetDisplayName()));

            foreach (BattleAnimEvent anim in Anims)
                yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

            context.TurnCancel.Cancel = true;

            yield break;
        }
    }
    
    /// <summary>
    /// Event that bounces status conditions move back to the user
    /// </summary>
    [Serializable]
    public class BounceStatusEvent : BattleEvent
    {
        /// <summary>
        /// The message displayed in the dungeon log
        /// </summary>
        public StringKey Msg;
        
        /// <summary>
        /// The list of battle VFXs played if the condition is met
        /// </summary
        public List<BattleAnimEvent> Anims;

        public BounceStatusEvent()
        {
            Anims = new List<BattleAnimEvent>();
        }
        public BounceStatusEvent(StringKey msg, params BattleAnimEvent[] anims)
        {
            Msg = msg;
            Anims = new List<BattleAnimEvent>();
            Anims.AddRange(anims);
        }
        protected BounceStatusEvent(BounceStatusEvent other)
        {
            Msg = other.Msg;
            Anims = new List<BattleAnimEvent>();
            foreach (BattleAnimEvent anim in other.Anims)
                Anims.Add((BattleAnimEvent)anim.Clone());
        }

        public override GameEvent Clone() { return new BounceStatusEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Skill && context.Data.Category == BattleData.SkillCategory.Status && DungeonScene.Instance.GetMatchup(context.User, context.Target) != Alignment.Self)
            {
                bool inflictsStatus = false;
                foreach (BattleEvent effect in context.Data.OnHits.EnumerateInOrder())
                {
                    if (effect is StatusBattleEvent)
                    {
                        StatusBattleEvent giveEffect = (StatusBattleEvent)effect;
                        if (giveEffect.AffectTarget && !giveEffect.Anonymous)
                        {
                            inflictsStatus = true;
                            break;
                        }
                    }
                }
                if (inflictsStatus)
                {
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(Msg.ToLocal(), context.Target.GetDisplayName(false)));

                    foreach (BattleAnimEvent anim in Anims)
                        yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                    context.Target = context.User;
                }
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that subtracts PP from all the user's move if the target was dealt damage by a move
    /// </summary>
    [Serializable]
    public class GrudgeEvent : BattleEvent
    {
        public override GameEvent Clone() { return new GrudgeEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (DungeonScene.Instance.GetMatchup(context.User, context.Target) == Alignment.Foe && context.GetContextStateInt<DamageDealt>(0) > 0 && context.ActionType == BattleActionType.Skill
                && context.UsageSlot > BattleContext.DEFAULT_ATTACK_SLOT && context.UsageSlot < CharData.MAX_SKILL_SLOTS)
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_GRUDGE").ToLocal()));
                yield return CoroutineManager.Instance.StartCoroutine(context.User.DeductCharges(-1, 3));
            }
        }
    }
    
    /// <summary>
    /// Event that increases the user's move PP usage by the specified amount 
    /// </summary>
    [Serializable]
    public class PressureEvent : BattleEvent
    {
    
        /// <summary>
        /// The increased PP usage amount
        /// </summary>
        public int Amount;
        public PressureEvent() { }
        public PressureEvent(int amount)
        {
            Amount = amount;
        }
        protected PressureEvent(PressureEvent other)
        {
            Amount = other.Amount;
        }
        public override GameEvent Clone() { return new PressureEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (DungeonScene.Instance.GetMatchup(context.User, context.Target) == Alignment.Foe && context.ActionType == BattleActionType.Skill
                && context.UsageSlot > BattleContext.DEFAULT_ATTACK_SLOT && context.UsageSlot < CharData.MAX_SKILL_SLOTS)
            {
                if (context.User.Skills[context.UsageSlot].Element.Charges > 0)
                {
                    int deduction = Amount;
                    if (context.ContextStates.Contains<PressurePlus>())
                    {
                        deduction += 1;
                        context.ContextStates.Remove<PressurePlus>();
                    }

                    if (deduction > 0)
                    {
                        yield return CoroutineManager.Instance.StartCoroutine(context.User.DeductCharges(context.UsageSlot, deduction, true, false, true));
                        if (context.User.Skills[context.UsageSlot].Element.Charges == 0)
                            context.SkillUsedUp.Skill = context.User.Skills[context.UsageSlot].Element.SkillNum;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Event that inflicts damage to the character based on the HP in the HPState status state
    /// </summary>
    [Serializable]
    public class CurseEvent : BattleEvent
    {
        
        /// <summary>
        /// The list of battle VFXs played if the condition is met
        /// </summary>
        public List<BattleAnimEvent> Anims;

        public CurseEvent() { Anims = new List<BattleAnimEvent>(); }
        public CurseEvent(params BattleAnimEvent[] anims)
        {
            Anims = new List<BattleAnimEvent>();
            Anims.AddRange(anims);
        }
        protected CurseEvent(CurseEvent other)
        {
            Anims = new List<BattleAnimEvent>();
            foreach (BattleAnimEvent anim in other.Anims)
                Anims.Add((BattleAnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new CurseEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.User.Dead)
                yield break;
            if ((context.Data.Category == BattleData.SkillCategory.Physical || context.Data.Category == BattleData.SkillCategory.Magical)
                && DungeonScene.Instance.GetMatchup(context.User, context.Target) == Alignment.Foe
                && !context.User.CharStates.Contains<MagicGuardState>())
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_CURSE").ToLocal(), context.User.GetDisplayName(false)));

                foreach (BattleAnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                yield return CoroutineManager.Instance.StartCoroutine(context.User.InflictDamage(((StatusEffect)owner).StatusStates.GetWithDefault<HPState>().HP));
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

    /// <summary>
    /// Event that modifies the damage multiplier if the character is at full HP
    /// </summary>
    [Serializable]
    public class MultiScaleEvent : BattleEvent
    {
        /// <summary>
        /// The list of battle VFXs played if the condition is met
        /// </summary>
        public List<BattleAnimEvent> Anims;

        public MultiScaleEvent() { Anims = new List<BattleAnimEvent>(); }
        public MultiScaleEvent(params BattleAnimEvent[] anims)
        {
            Anims = new List<BattleAnimEvent>();
            Anims.AddRange(anims);
        }
        protected MultiScaleEvent(MultiScaleEvent other)
        {
            Anims = new List<BattleAnimEvent>();
            foreach (BattleAnimEvent anim in other.Anims)
                Anims.Add((BattleAnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new MultiScaleEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Target.HP == context.Target.MaxHP &&
                (context.Data.Category == BattleData.SkillCategory.Physical || context.Data.Category == BattleData.SkillCategory.Magical))
            {
                foreach (BattleAnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                context.AddContextStateMult<DmgMult>(false, 1, 2);
            }
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
    /// Event that causes the character to dodge items that contain the EdibleState item state
    /// </summary>
    [Serializable]
    public class DodgeFoodEvent : BattleEvent
    {
        /// <summary>
        /// The message displayed in the dungeon log if the condition is met
        /// </summary>
        public StringKey Message;

        public DodgeFoodEvent() { }
        public DodgeFoodEvent(StringKey message)
        {
            Message = message;
        }
        protected DodgeFoodEvent(DodgeFoodEvent other)
        {
            Message = other.Message;
        }
        public override GameEvent Clone() { return new DodgeFoodEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Item || context.ActionType == BattleActionType.Throw)
            {
                ItemData entry = DataManager.Instance.GetItem(context.Item.ID);
                if (entry.ItemStates.Contains<EdibleState>())
                {
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), context.Target.GetDisplayName(false)));
                    context.AddContextStateMult<AccMult>(false, -1, 1);
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
    /// Event that causes battle actions that target foes to also hit friendly targets and vice versa.
    /// </summary>
    [Serializable]
    public class TraitorEvent : BattleEvent
    {
        public override GameEvent Clone() { return new TraitorEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if ((context.HitboxAction.TargetAlignments & Alignment.Foe) != Alignment.None)
                context.HitboxAction.TargetAlignments |= Alignment.Friend;
            if ((context.HitboxAction.TargetAlignments & Alignment.Friend) != Alignment.None)
                context.HitboxAction.TargetAlignments |= Alignment.Foe;
            if ((context.Explosion.TargetAlignments & Alignment.Foe) != Alignment.None)
                context.Explosion.TargetAlignments |= Alignment.Friend;
            if ((context.Explosion.TargetAlignments & Alignment.Friend) != Alignment.None)
                context.Explosion.TargetAlignments |= Alignment.Foe;
            yield break;
        }
    }
    
    /// <summary>
    /// Event that forces the character to use the specified move
    /// Usually used for moves that charge up
    /// </summary>
    [Serializable]
    public class ForceMoveEvent : BattleEvent
    {
        /// <summary>
        /// The move ID
        /// </summary>
        [JsonConverter(typeof(SkillConverter))]
        [DataType(0, DataManager.DataType.Skill, false)]
        public string MoveIndex;

        public ForceMoveEvent() { MoveIndex = ""; }
        public ForceMoveEvent(string moveIndex)
        {
            MoveIndex = moveIndex;
        }
        protected ForceMoveEvent(ForceMoveEvent other)
        {
            MoveIndex = other.MoveIndex;
        }
        public override GameEvent Clone() { return new ForceMoveEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.UsageSlot == BattleContext.FORCED_SLOT)
                yield break;

            context.UsageSlot = BattleContext.FAKE_ATTACK_SLOT;

            SkillData entry = DataManager.Instance.GetSkill(MoveIndex);
            context.Data = new BattleData(entry.Data);
            context.Data.ID = MoveIndex;
            context.Data.DataType = DataManager.DataType.Skill;
            context.Explosion = new ExplosionData(entry.Explosion);
            context.HitboxAction = entry.HitboxAction.Clone();
            context.Item = new InvItem();
            context.Strikes = entry.Strikes;

            context.SetActionMsg(Text.FormatGrammar(new StringKey("MSG_SKILL_USE").ToLocal(), context.User.GetDisplayName(false), entry.GetIconName()));
        }
    }

        
    /// <summary>
    /// Event tha changes the hitbox action, explosion data, and battle data
    /// if the MoveCharge context state is not present
    /// </summary>
    [Serializable]
    public class ChargeCustomEvent : BattleEvent
    {
        /// <summary>
        /// The alternate hitbox action
        /// </summary>
        public CombatAction HitboxAction;
        
        /// <summary>
        /// The alternate explosion data
        /// </summary>
        public ExplosionData Explosion;
        
        /// <summary>
        /// The alternate battle data
        /// </summary>
        public BattleData NewData;

        public ChargeCustomEvent() { }
        public ChargeCustomEvent(CombatAction action, ExplosionData explosion, BattleData moveData)
        {
            HitboxAction = action;
            Explosion = explosion;
            NewData = moveData;
        }
        protected ChargeCustomEvent(ChargeCustomEvent other)
        {
            HitboxAction = other.HitboxAction;
            Explosion = other.Explosion;
            NewData = new BattleData(other.NewData);
        }
        public override GameEvent Clone() { return new ChargeCustomEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (!context.ContextStates.Contains<MoveCharge>())
            {
                context.Data = new BattleData(NewData);
                context.Data.ID = context.Data.ID;
                context.Data.DataType = context.Data.DataType;

                context.Explosion = new ExplosionData(Explosion);

                context.HitboxAction = HitboxAction.Clone();

                context.Item = new InvItem();
                context.Strikes = 1;

                context.SetActionMsg("");
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that sets the specified charge status and alternate hitbox action
    /// if the MoveCharge context state is not present
    /// Usually used for moves that charge up
    /// </summary>
    [Serializable]
    public class ChargeOrReleaseEvent : BattleEvent
    {
        /// <summary>
        /// The status representing the move charging up
        /// </summary>
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string ChargeStatus;
        
        /// <summary>
        /// Alternate data on the hitbox of the attack. Controls range and targeting
        /// </summary>
        public CombatAction HitboxAction;

        public ChargeOrReleaseEvent() { ChargeStatus = ""; }
        public ChargeOrReleaseEvent(string chargeStatus, CombatAction action)
        {
            ChargeStatus = chargeStatus;
            HitboxAction = action;
        }
        protected ChargeOrReleaseEvent(ChargeOrReleaseEvent other)
        {
            ChargeStatus = other.ChargeStatus;
            HitboxAction = other.HitboxAction.Clone();
        }
        public override GameEvent Clone() { return new ChargeOrReleaseEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (!context.ContextStates.Contains<MoveCharge>())
            {
                BattleData altMoveData = new BattleData();
                altMoveData.Element = DataManager.Instance.DefaultElement;
                altMoveData.Category = BattleData.SkillCategory.None;
                altMoveData.HitRate = -1;
                altMoveData.OnHits.Add(0, new StatusBattleEvent(ChargeStatus, true, false));
                altMoveData.ID = context.Data.ID;
                altMoveData.DataType = context.Data.DataType;
                context.Data = new BattleData(altMoveData);

                ExplosionData altExplosion = new ExplosionData();
                altExplosion.TargetAlignments |= Alignment.Self;
                context.Explosion = new ExplosionData(altExplosion);

                context.HitboxAction = HitboxAction.Clone();

                context.Item = new InvItem();
                context.Strikes = 1;

                context.SetActionMsg("");
            }
            yield break;
        }
    }
    
    /// <summary>
    /// Event that sets the specified charge status if the MoveCharge context state is not present
    /// Used specifically for the move Bide
    /// </summary>
    [Serializable]
    public class BideOrReleaseEvent : BattleEvent
    {
        /// <summary>
        /// The status representing the move charging up
        /// </summary>
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string ChargeStatus;
        
        /// <summary>
        /// The particle VFX
        /// </summary>
        public FiniteEmitter IntroEmitter;
        
        /// <summary>
        /// The sound effect of the VFX
        /// </summary>
        [Sound(0)]
        public string IntroSound;

        public BideOrReleaseEvent() { ChargeStatus = ""; }
        public BideOrReleaseEvent(string chargeStatus, FiniteEmitter introEmitter, string introSound)
        {
            ChargeStatus = chargeStatus;
            IntroEmitter = introEmitter;
            IntroSound = introSound;
        }
        protected BideOrReleaseEvent(BideOrReleaseEvent other)
        {
            ChargeStatus = other.ChargeStatus;
            IntroEmitter = (FiniteEmitter)other.IntroEmitter.Clone();
            IntroSound = other.IntroSound;
        }
        public override GameEvent Clone() { return new BideOrReleaseEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (!context.ContextStates.Contains<MoveBide>())
            {
                BattleData altMoveData = new BattleData();
                altMoveData.Element = DataManager.Instance.DefaultElement;
                altMoveData.Category = BattleData.SkillCategory.None;
                altMoveData.HitRate = -1;
                altMoveData.OnHits.Add(0, new StatusBattleEvent(ChargeStatus, true, false));
                altMoveData.ID = context.Data.ID;
                altMoveData.DataType = context.Data.DataType;
                context.Data = new BattleData(altMoveData);

                ExplosionData altExplosion = new ExplosionData();
                altExplosion.TargetAlignments |= Alignment.Self;
                context.Explosion = new ExplosionData(altExplosion);

                SelfAction altAction = new SelfAction();
                altAction.CharAnimData = new CharAnimFrameType(GraphicsManager.ChargeAction);
                altAction.TargetAlignments |= Alignment.Self;
                BattleFX newFX = new BattleFX();
                newFX.Emitter = (FiniteEmitter)IntroEmitter.Clone();
                newFX.Sound = IntroSound;
                altAction.PreActions.Add(newFX);
                context.HitboxAction = altAction;

                context.Item = new InvItem();
                context.Strikes = 1;

                //still declare the move
            }
            else
                context.SetActionMsg(Text.FormatGrammar(new StringKey("MSG_BIDE_ATTACK").ToLocal(), context.User.GetDisplayName(false)));
            yield break;
        }
    }
    
    /// <summary>
    /// Event that sets the specified charge status if the FollowUp context state is not present
    /// Used specifically for the moves Retaliate and Fire/Water/Grass Pledge
    /// </summary>
    [Serializable]
    public class WatchOrStrikeEvent : BattleEvent
    {
        /// <summary>
        /// The status representing the move charging up
        /// </summary>
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string ChargeStatus;
        
        /// <summary>
        /// The particle VFX
        /// </summary>
        public FiniteEmitter IntroEmitter;
        
        /// <summary>
        /// The sound effect of the VFX
        /// </summary>
        [Sound(0)]
        public string IntroSound;

        public WatchOrStrikeEvent() { ChargeStatus = ""; }
        public WatchOrStrikeEvent(string chargeStatus, FiniteEmitter introEmitter, string introSound)
        {
            ChargeStatus = chargeStatus;
            IntroEmitter = introEmitter;
            IntroSound = introSound;
        }
        protected WatchOrStrikeEvent(WatchOrStrikeEvent other)
        {
            ChargeStatus = other.ChargeStatus;
            IntroEmitter = (FiniteEmitter)other.IntroEmitter.Clone();
            IntroSound = other.IntroSound;
        }
        public override GameEvent Clone() { return new WatchOrStrikeEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (!context.ContextStates.Contains<FollowUp>())
            {
                BattleData altMoveData = new BattleData();
                altMoveData.Element = DataManager.Instance.DefaultElement;
                altMoveData.Category = BattleData.SkillCategory.None;
                altMoveData.HitRate = -1;
                altMoveData.OnHits.Add(0, new StatusBattleEvent(ChargeStatus, true, false));
                altMoveData.ID = context.Data.ID;
                altMoveData.DataType = context.Data.DataType;
                context.Data = new BattleData(altMoveData);

                ExplosionData altExplosion = new ExplosionData();
                altExplosion.TargetAlignments |= Alignment.Self;
                context.Explosion = new ExplosionData(altExplosion);

                SelfAction altAction = new SelfAction();
                altAction.CharAnimData = new CharAnimFrameType(GraphicsManager.ChargeAction);
                altAction.TargetAlignments |= Alignment.Self;
                BattleFX newFX = new BattleFX();
                newFX.Emitter = (FiniteEmitter)IntroEmitter.Clone();
                newFX.Sound = IntroSound;
                altAction.PreActions.Add(newFX);
                context.HitboxAction = altAction;

                context.Item = new InvItem();
                context.Strikes = 1;

                context.SetActionMsg("");
            }
            yield break;
        }
    }
    
    /// <summary>
    /// Event that increments the AttackHitTotal global context state,
    /// </summary>
    [Serializable]
    public class HitPostEvent : BattleEvent
    {
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string RecentHitStatusID;
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string OtherHitStatusID;
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string TargetStatusID;
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string CritStatusID;

        public HitPostEvent()
        {
            RecentHitStatusID = "";
            OtherHitStatusID = "";
            TargetStatusID = "";
            CritStatusID = "";
        }
        public HitPostEvent(string recentHitStatusID, string otherHitStatusID, string targetStatusID, string critStatusID)
        {
            RecentHitStatusID = recentHitStatusID;
            OtherHitStatusID = otherHitStatusID;
            TargetStatusID = targetStatusID;
            CritStatusID = critStatusID;
        }
        protected HitPostEvent(HitPostEvent other)
        {
            RecentHitStatusID = other.RecentHitStatusID;
            OtherHitStatusID = other.OtherHitStatusID;
            TargetStatusID = other.TargetStatusID;
            CritStatusID = other.CritStatusID;
        }
        public override GameEvent Clone() { return new HitPostEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Trap)
                yield break;

            context.ContextStates.Set(new AttackHit());
            AttackHitTotal totalHit = context.GlobalContextStates.GetWithDefault<AttackHitTotal>();
            if (totalHit != null)
                totalHit.Count++;
            else
                context.GlobalContextStates.Set(new AttackHitTotal(1));

            if (context.Target != context.User)
            {
                int dmg = context.GetContextStateInt<DamageDealt>(0);
                if (dmg > 0)
                {
                    StatusEffect recentHitStatus = context.Target.GetStatusEffect(RecentHitStatusID);
                    if (recentHitStatus == null)
                    {
                        recentHitStatus = new StatusEffect(RecentHitStatusID);
                        recentHitStatus.LoadFromData();
                        recentHitStatus.StatusStates.GetWithDefault<StackState>().Stack = 1;
                        yield return CoroutineManager.Instance.StartCoroutine(context.Target.AddStatusEffect(context.User, recentHitStatus));
                    }
                    else
                        recentHitStatus.StatusStates.GetWithDefault<StackState>().Stack = recentHitStatus.StatusStates.GetWithDefault<StackState>().Stack + 1;

                }

                if (context.ContextStates.Contains<AttackCrit>())
                {
                    StatusEffect recentCritStatus = context.User.GetStatusEffect(CritStatusID);
                    if (recentCritStatus == null)
                    {
                        recentCritStatus = new StatusEffect(CritStatusID);
                        recentCritStatus.LoadFromData();
                        recentCritStatus.StatusStates.GetWithDefault<StackState>().Stack = 1;
                        yield return CoroutineManager.Instance.StartCoroutine(context.User.AddStatusEffect(context.User, recentCritStatus));
                    }
                    else
                        recentCritStatus.StatusStates.GetWithDefault<StackState>().Stack = recentCritStatus.StatusStates.GetWithDefault<StackState>().Stack + 1;
                }

                if (context.ActionType == BattleActionType.Skill && context.Data.ID != DataManager.Instance.DefaultSkill)
                {

                    StatusEffect otherStatus = new StatusEffect(OtherHitStatusID);
                    otherStatus.LoadFromData();
                    otherStatus.StatusStates.GetWithDefault<IDState>().ID = context.Data.ID;
                    yield return CoroutineManager.Instance.StartCoroutine(context.Target.AddStatusEffect(context.User, otherStatus));
                }

                if (context.User.MemberTeam.MapFaction != context.Target.MemberTeam.MapFaction)
                {
                    StatusEffect targetStatus = new StatusEffect(TargetStatusID);
                    targetStatus.LoadFromData();
                    targetStatus.TargetChar = context.User;
                    targetStatus.StatusStates.GetWithDefault<HPState>().HP = dmg;
                    yield return CoroutineManager.Instance.StartCoroutine(context.Target.AddStatusEffect(context.User, targetStatus));
                }
            }
        }
    }
    [Serializable]
    public class UsePostEvent : BattleEvent
    {
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string RepeatStatusID;
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string AllyStatusID;
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string MissedAllID;

        public UsePostEvent()
        {
            RepeatStatusID = "";
            AllyStatusID = "";
            MissedAllID = "";
        }
        public UsePostEvent(string repeatStatusID, string allyStatusID, string missedAllID)
        {
            RepeatStatusID = repeatStatusID;
            AllyStatusID = allyStatusID;
            MissedAllID = missedAllID;
        }
        protected UsePostEvent(UsePostEvent other)
        {
            RepeatStatusID = other.RepeatStatusID;
            AllyStatusID = other.AllyStatusID;
            MissedAllID = other.MissedAllID;
        }
        public override GameEvent Clone() { return new UsePostEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Skill && context.UsageSlot > BattleContext.DEFAULT_ATTACK_SLOT && context.UsageSlot < CharData.MAX_SKILL_SLOTS)
            {
                StatusEffect repeatStatus = context.User.GetStatusEffect(RepeatStatusID);
                if (repeatStatus != null && repeatStatus.StatusStates.GetWithDefault<RecentState>() != null)
                {
                    //increment repetition
                    repeatStatus.StatusStates.GetWithDefault<CountState>().Count++;
                    //reset turn counter
                    repeatStatus.StatusStates.GetWithDefault<CountDownState>().Counter = 0;
                }


                foreach (Character ally in context.User.GetSeenCharacters(Alignment.Friend))
                {
                    StatusEffect allyStatus = new StatusEffect(AllyStatusID);
                    allyStatus.LoadFromData();
                    allyStatus.StatusStates.GetWithDefault<IDState>().ID = context.Data.ID;
                    yield return CoroutineManager.Instance.StartCoroutine(ally.AddStatusEffect(context.User, allyStatus));
                }

                if (context.GetContextStateInt<AttackHitTotal>(true, 0) == 0)
                {
                    StatusEffect missedAllStatus = new StatusEffect(MissedAllID);
                    missedAllStatus.LoadFromData();
                    yield return CoroutineManager.Instance.StartCoroutine(context.User.AddStatusEffect(context.User, missedAllStatus));
                }
                else
                {
                    yield return CoroutineManager.Instance.StartCoroutine(context.User.RemoveStatusEffect(MissedAllID, false));
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

            yield return CoroutineManager.Instance.StartCoroutine(target.AddStatusEffect(Anonymous ? null : context.User, setStatus));
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
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_MOODY").ToLocal(), context.User.GetDisplayName(false)));

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
    /// Event that causes the specified list of move to always hit while all other moves will miss.
    /// </summary>
    [Serializable]
    public class SemiInvulEvent : BattleEvent
    {
        /// <summary>
        /// The list of valid of moves that will always hit
        /// </summary>
        [JsonConverter(typeof(SkillArrayConverter))]
        [DataType(1, DataManager.DataType.Skill, false)]
        public string[] ExceptionMoves;

        public SemiInvulEvent()
        {
            ExceptionMoves = new string[0];
        }
        public SemiInvulEvent(string[] exceptionMoves)
        {
            ExceptionMoves = exceptionMoves;
        }
        protected SemiInvulEvent(SemiInvulEvent other)
        {
            ExceptionMoves = new string[other.ExceptionMoves.Length];
            other.ExceptionMoves.CopyTo(ExceptionMoves, 0);
        }
        public override GameEvent Clone() { return new SemiInvulEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            for (int ii = 0; ii < ExceptionMoves.Length; ii++)
            {
                if (context.Data.ID == ExceptionMoves[ii])
                {
                    context.Data.HitRate = -1;
                    yield break;
                }
            }
            context.AddContextStateMult<AccMult>(false, 0, 1);
        }
    }
    
    /// <summary>
    /// Event that increases the HP in the HPState status state by the damage received
    /// </summary>
    [Serializable]
    public class BideEvent : BattleEvent
    {
        public override GameEvent Clone() { return new BideEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            HPState state = ((StatusEffect)owner).StatusStates.GetWithDefault<HPState>();
            if (state != null)
                state.HP += context.GetContextStateInt<DamageDealt>(0);
            yield break;
        }
    }
    
    /// <summary>
    /// Event that unleases double the damage in HPState status state when the CountDownState status state reaches 0
    /// Used by the Biding status
    /// </summary>
    [Serializable]
    public class UnleashEvent : BattleEvent
    {
        public UnleashEvent() { }
        public override GameEvent Clone() { return new UnleashEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.UsageSlot == BattleContext.FORCED_SLOT)
                yield break;

            ((StatusEffect)owner).StatusStates.GetWithDefault<CountDownState>().Counter--;
            if (((StatusEffect)owner).StatusStates.GetWithDefault<CountDownState>().Counter <= 0)
            {
                yield return CoroutineManager.Instance.StartCoroutine(context.User.RemoveStatusEffect(((StatusEffect)owner).ID));

                HPState state = ((StatusEffect)owner).StatusStates.GetWithDefault<HPState>();
                BasePowerState basePower = context.Data.SkillStates.GetWithDefault<BasePowerState>();
                if (basePower != null && state != null)
                    basePower.Power += state.HP * 2;
            }
            else
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_STORE_ENERGY").ToLocal(), context.User.GetDisplayName(false)));
                context.CancelState.Cancel = true;
            }
        }
    }
    
    /// <summary>
    /// Event that makes the user cannot target the enemy that used the status
    /// This event can only be used in statuses
    /// </summary>
    [Serializable]
    public class CantAttackTargetEvent : BattleEvent
    {
        /// <summary>
        /// Whether to force the user to target the enemy instead
        /// </summary> 
        public bool Invert;
        
        /// <summary>
        /// The message displayed in the dungeon log if the condition is met
        /// </summary> 
        [StringKey(0, true)]
        public StringKey Message;

        public CantAttackTargetEvent() { }
        public CantAttackTargetEvent(bool invert, StringKey message)
        {
            Invert = invert;
            Message = message;
        }
        protected CantAttackTargetEvent(CantAttackTargetEvent other)
        {
            Invert = other.Invert;
            Message = other.Message;
        }
        public override GameEvent Clone() { return new CantAttackTargetEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Target != null && ((StatusEffect)owner).TargetChar != null && context.Target != context.User)
            {
                if ((((StatusEffect)owner).TargetChar == context.Target) != Invert)
                {
                    if (Message.IsValid())
                        DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), context.User.GetDisplayName(false), ((StatusEffect)owner).TargetChar.GetDisplayName(false)));
                    context.AddContextStateMult<AccMult>(false, -1, 1);
                }
            }
            yield break;
        }
    }
    
    /// <summary>
    /// Event that sets the user direction to face the enemy
    /// This event can only be used in statuses 
    /// </summary> 
    [Serializable]
    public class ForceFaceTargetEvent : BattleEvent
    {
        public override GameEvent Clone() { return new ForceFaceTargetEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            StatusEffect status = ((StatusEffect)owner);
            if (status.TargetChar != null)
            {
                Dir8 attackDir = ZoneManager.Instance.CurrentMap.GetClosestDir8(ownerChar.CharLoc, status.TargetChar.CharLoc);
                ownerChar.CharDir = attackDir;
            }
            yield break;
        }
    }
    
    /// <summary>
    /// Event that deals damage to the character if the enemy that used the status also takes damage
    /// This event can only be used in statuses  
    /// </summary> 
    [Serializable]
    public class DestinyBondEvent : BattleEvent
    {
        public DestinyBondEvent() { }
        public override GameEvent Clone() { return new DestinyBondEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int damage = context.GetContextStateInt<DamageDealt>(0);
            if (damage > 0 && ((StatusEffect)owner).TargetChar != null)
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_DESTINY_BOND").ToLocal(), context.Target.GetDisplayName(false), ((StatusEffect)owner).TargetChar.GetDisplayName(false)));

                if (((StatusEffect)owner).TargetChar.CharStates.Contains<HitAndRunState>())
                    damage /= 4;

                yield return CoroutineManager.Instance.StartCoroutine(((StatusEffect)owner).TargetChar.InflictDamage(damage));
            }
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
    /// Event that inflicts damage based on the character max HP
    /// This event can only be used on statuses 
    /// </summary>
    [Serializable]
    public class PoisonEvent : BattleEvent
    { 
        /// <summary>
        /// Whether the character is badly poisoned or not
        /// </summary>
        public bool Toxic;
        public int HPFraction;
        public int RestoreHPFraction;

        public PoisonEvent() { }
        public PoisonEvent(bool toxic, int hpFraction, int restoreHpFraction)
        {
            Toxic = toxic;
            HPFraction = hpFraction;
            RestoreHPFraction = restoreHpFraction;
        }
        protected PoisonEvent(PoisonEvent other)
        {
            Toxic = other.Toxic;
            HPFraction = other.HPFraction;
            RestoreHPFraction = other.RestoreHPFraction;
        }
        public override GameEvent Clone() { return new PoisonEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.User.Dead)
                yield break;
            if (context.UsageSlot == BattleContext.FORCED_SLOT)
                yield break;

            if (!context.User.CharStates.Contains<MagicGuardState>())
            {
                CountState countState = ((StatusEffect)owner).StatusStates.Get<CountState>();
                if (Toxic && countState.Count < HPFraction)
                    countState.Count++;
                if (context.User.CharStates.Contains<PoisonHealState>())
                {
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_POISON_HEAL").ToLocal(), context.User.GetDisplayName(false)));
                    yield return CoroutineManager.Instance.StartCoroutine(context.User.RestoreHP(Math.Max(1, context.User.MaxHP / RestoreHPFraction)));
                }
                else
                {
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_POISONED").ToLocal(), context.User.GetDisplayName(false)));
                    yield return CoroutineManager.Instance.StartCoroutine(context.User.InflictDamage(Math.Max(1, (context.User.MaxHP * countState.Count) / HPFraction)));
                }
            }
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
    /// Event that boosts the character's stat depending on the effectiveness of the specified type to the character's type.
    /// Super effective: Defense & Special Defense
    /// Not effective: Attack & Special Attack
    /// Neutral: Speed & HP
    /// Same type: Boost all stats
    /// </summary>
    [Serializable]
    public class GummiEvent : BattleEvent
    {
        
        /// <summary>
        /// The gummi type
        /// </summary>
        [JsonConverter(typeof(ElementConverter))]
        [DataType(0, DataManager.DataType.Element, false)]
        public string TargetElement;

        public GummiEvent() { TargetElement = ""; }
        public GummiEvent(string element)
        {
            TargetElement = element;
        }
        protected GummiEvent(GummiEvent other)
        {
            TargetElement = other.TargetElement;
        }
        public override GameEvent Clone() { return new GummiEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            MonsterID formData = context.Target.BaseForm;
            BaseMonsterForm form = DataManager.Instance.GetMonster(formData.Species).Forms[formData.Form];

            int typeMatchup = PreTypeEvent.CalculateTypeMatchup(TargetElement, context.Target.Element1);
            typeMatchup += PreTypeEvent.CalculateTypeMatchup(TargetElement, context.Target.Element2);

            int heal = 5;
            List<Stat> stats = new List<Stat>();
            if (TargetElement == DataManager.Instance.DefaultElement || context.Target.Element1 == TargetElement || context.Target.Element2 == TargetElement)
            {
                heal = 20;
                stats.Add(Stat.HP);
                stats.Add(Stat.Attack);
                stats.Add(Stat.Defense);
                stats.Add(Stat.MAtk);
                stats.Add(Stat.MDef);
                stats.Add(Stat.Speed);
            }
            else if (typeMatchup < PreTypeEvent.NRM_2)
            {
                heal = 10;
                stats.Add(Stat.Attack);
                stats.Add(Stat.MAtk);
            }
            else if (typeMatchup > PreTypeEvent.NRM_2)
            {
                heal = 10;
                stats.Add(Stat.Defense);
                stats.Add(Stat.MDef);
            }
            else
            {
                heal = 5;
                stats.Add(Stat.HP);
                stats.Add(Stat.Speed);
            }

            foreach (Stat stat in stats)
                AddStat(stat, context);

            if (heal > 15)
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_HUNGER_FILL").ToLocal(), context.Target.GetDisplayName(false)));
            else if (heal > 5)
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_HUNGER_FILL_MIN").ToLocal(), context.Target.GetDisplayName(false)));

            context.Target.Fullness += heal;

            if (context.Target.Fullness >= context.Target.MaxFullness)
            {
                context.Target.Fullness = context.Target.MaxFullness;
                context.Target.FullnessRemainder = 0;
            }

            yield break;
        }

        private void AddStat(Stat stat, BattleContext context)
        {
            int prevStat = 0;
            int newStat = 0;
            switch (stat)
            {
                case Stat.HP:
                    if (context.Target.MaxHPBonus < MonsterFormData.MAX_STAT_BOOST)
                    {
                        prevStat = context.Target.MaxHP;
                        context.Target.MaxHPBonus++;
                        newStat = context.Target.MaxHP;
                    }
                    break;
                case Stat.Attack:
                    if (context.Target.AtkBonus < MonsterFormData.MAX_STAT_BOOST)
                    {
                        prevStat = context.Target.BaseAtk;
                        context.Target.AtkBonus++;
                        newStat = context.Target.BaseAtk;
                    }
                    break;
                case Stat.Defense:
                    if (context.Target.DefBonus < MonsterFormData.MAX_STAT_BOOST)
                    {
                        prevStat = context.Target.BaseDef;
                        context.Target.DefBonus++;
                        newStat = context.Target.BaseDef;
                    }
                    break;
                case Stat.MAtk:
                    if (context.Target.MAtkBonus < MonsterFormData.MAX_STAT_BOOST)
                    {
                        prevStat = context.Target.BaseMAtk;
                        context.Target.MAtkBonus++;
                        newStat = context.Target.BaseMAtk;
                    }
                    break;
                case Stat.MDef:
                    if (context.Target.MDefBonus < MonsterFormData.MAX_STAT_BOOST)
                    {
                        prevStat = context.Target.BaseMDef;
                        context.Target.MDefBonus++;
                        newStat = context.Target.BaseMDef;
                    }
                    break;
                case Stat.Speed:
                    if (context.Target.SpeedBonus < MonsterFormData.MAX_STAT_BOOST)
                    {
                        prevStat = context.Target.BaseSpeed;
                        context.Target.SpeedBonus++;
                        newStat = context.Target.BaseSpeed;
                    }
                    break;
            }
            if (newStat - prevStat > 0)
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_STAT_BOOST").ToLocal(), context.Target.GetDisplayName(false), stat.ToLocal(), (newStat - prevStat).ToString()));
        }
    }
    
    /// <summary>
    /// Event that boosts the specified stat by the specified amount
    /// </summary>
    [Serializable]
    public class VitaminEvent : BattleEvent
    {
        
        /// <summary>
        /// The stat to boost
        /// </summary>
        public Stat BoostedStat;
        
        /// <summary>
        /// The boost amount 
        /// </summary>
        public int Change;

        public VitaminEvent() { }
        public VitaminEvent(Stat stat, int change)
        {
            BoostedStat = stat;
            Change = change;
        }
        protected VitaminEvent(VitaminEvent other)
        {
            BoostedStat = other.BoostedStat;
            Change = other.Change;
        }
        public override GameEvent Clone() { return new VitaminEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            bool boosted = false;
            if (BoostedStat > Stat.None)
                boosted |= boostStat(BoostedStat, context.Target);
            else
            {
                boosted |= boostStat(Stat.HP, context.Target);
                boosted |= boostStat(Stat.Attack, context.Target);
                boosted |= boostStat(Stat.Defense, context.Target);
                boosted |= boostStat(Stat.MAtk, context.Target);
                boosted |= boostStat(Stat.MDef, context.Target);
                boosted |= boostStat(Stat.Speed, context.Target);
            }
            if (!boosted)
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_NOTHING_HAPPENED").ToLocal()));
            yield break;
        }

        private bool boostStat(Stat stat, Character target)
        {
            int change = Change;

            int prevStat = 0;
            int newStat = 0;

            //continue to increment the bonus until a stat increase is seen
            switch (stat)
            {
                case Stat.HP:
                    prevStat = target.MaxHP;
                    target.MaxHPBonus = Math.Min(target.MaxHPBonus + change, MonsterFormData.MAX_STAT_BOOST);
                    while (target.MaxHP == prevStat && target.MaxHPBonus < MonsterFormData.MAX_STAT_BOOST)
                        target.MaxHPBonus++;
                    newStat = target.MaxHP;
                    break;
                case Stat.Attack:
                    prevStat = target.BaseAtk;
                    target.AtkBonus = Math.Min(target.AtkBonus + change, MonsterFormData.MAX_STAT_BOOST);
                    while (target.BaseAtk == prevStat && target.AtkBonus < MonsterFormData.MAX_STAT_BOOST)
                        target.AtkBonus++;
                    newStat = target.BaseAtk;
                    break;
                case Stat.Defense:
                    prevStat = target.BaseDef;
                    target.DefBonus = Math.Min(target.DefBonus + change, MonsterFormData.MAX_STAT_BOOST);
                    while (target.BaseDef == prevStat && target.DefBonus < MonsterFormData.MAX_STAT_BOOST)
                        target.DefBonus++;
                    newStat = target.BaseDef;
                    break;
                case Stat.MAtk:
                    prevStat = target.BaseMAtk;
                    target.MAtkBonus = Math.Min(target.MAtkBonus + change, MonsterFormData.MAX_STAT_BOOST);
                    while (target.BaseMAtk == prevStat && target.MAtkBonus < MonsterFormData.MAX_STAT_BOOST)
                        target.MAtkBonus++;
                    newStat = target.BaseMAtk;
                    break;
                case Stat.MDef:
                    prevStat = target.BaseMDef;
                    target.MDefBonus = Math.Min(target.MDefBonus + change, MonsterFormData.MAX_STAT_BOOST);
                    while (target.BaseMDef == prevStat && target.MDefBonus < MonsterFormData.MAX_STAT_BOOST)
                        target.MDefBonus++;
                    newStat = target.BaseMDef;
                    break;
                case Stat.Speed:
                    prevStat = target.BaseSpeed;
                    target.SpeedBonus = Math.Min(target.SpeedBonus + change, MonsterFormData.MAX_STAT_BOOST);
                    while (target.BaseSpeed == prevStat && target.SpeedBonus < MonsterFormData.MAX_STAT_BOOST)
                        target.SpeedBonus++;
                    newStat = target.BaseSpeed;
                    break;
            }
            if (newStat > prevStat)
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_STAT_BOOST").ToLocal(), target.GetDisplayName(false), stat.ToLocal(), (newStat - prevStat).ToString()));
                return true;
            }
            else
                return false;
        }
    }
    
    /// <summary>
    /// Event that changes the character's level by the specified amount 
    /// </summary>
    [Serializable]
    public class LevelChangeEvent : BattleEvent
    {
        /// <summary>
        /// The level change
        /// </summary> 
        public int Level;

        public LevelChangeEvent() { }
        public LevelChangeEvent(int level)
        {
            Level = level;
        }
        protected LevelChangeEvent(LevelChangeEvent other)
        {
            Level = other.Level;
        }
        public override GameEvent Clone() { return new LevelChangeEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            context.Target.EXP = 0;
            string growth = DataManager.Instance.GetMonster(context.Target.BaseForm.Species).EXPTable;
            GrowthData growthData = DataManager.Instance.GetGrowth(growth);
            if (Level < 0)
            {
                int levelsChanged = 0;
                while (levelsChanged > Level && context.Target.Level + levelsChanged > 1)
                {
                    context.Target.EXP -= growthData.GetExpToNext(context.Target.Level + levelsChanged - 1);
                    levelsChanged--;
                }
            }
            else if (Level > 0)
            {
                int levelsChanged = 0;
                while (levelsChanged < Level && context.Target.Level + levelsChanged < DataManager.Instance.Start.MaxLevel)
                {
                    context.Target.EXP += growthData.GetExpToNext(context.Target.Level + levelsChanged);
                    levelsChanged++;
                }
            }
            DungeonScene.Instance.LevelGains.Add(ZoneManager.Instance.CurrentMap.GetCharIndex(context.Target));
            yield break;
        }
    }

    /// <summary>
    /// Event that adds EXP to the character based on the damage dealt
    /// </summary>
    [Serializable]
    public class DamageEXPEvent : BattleEvent
    {
        public override GameEvent Clone() { return new DamageEXPEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int damage = context.GetContextStateInt<DamageDealt>(0);
            int gainedExp = damage * 10;
            if (gainedExp > 0)
            {
                Team playerTeam = context.User.MemberTeam;
                foreach (Character player in playerTeam.EnumerateChars())
                {
                    if (player.Level < DataManager.Instance.Start.MaxLevel)
                    {
                        player.EXP += gainedExp;
                        DungeonScene.Instance.MeterChanged(player.CharLoc, gainedExp, true);

                        string growth = DataManager.Instance.GetMonster(player.BaseForm.Species).EXPTable;
                        GrowthData growthData = DataManager.Instance.GetGrowth(growth);
                        if (player.EXP >= growthData.GetExpToNext(player.Level) || player.EXP < 0)
                            DungeonScene.Instance.LevelGains.Add(ZoneManager.Instance.CurrentMap.GetCharIndex(context.User));
                    }
                }
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that marks whether EXP can be gained from the target
    /// </summary>
    [Serializable]
    public class ToggleEXPEvent : BattleEvent
    {
        /// <summary>
        /// Whether to make target EXP marked or not
        /// </summary>
        public bool EXPMarked;

        public ToggleEXPEvent() { }
        public ToggleEXPEvent(bool exp) { EXPMarked = exp; }
        protected ToggleEXPEvent(ToggleEXPEvent other)
        {
            EXPMarked = other.EXPMarked;
        }
        public override GameEvent Clone() { return new ToggleEXPEvent(this); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            context.Target.EXPMarked = EXPMarked;
            yield break;
        }
    }

    /// <summary>
    /// Event that causes the target to drop money when defeated
    /// </summary>
    [Serializable]
    public class DefeatedMoneyEvent : BattleEvent
    {
        public override GameEvent Clone() { return new DefeatedMoneyEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            bool knockedOut = context.ContextStates.Contains<Knockout>();
            if (knockedOut)
            {
                MonsterData monsterData = DataManager.Instance.GetMonster(context.Target.BaseForm.Species);
                MonsterFormData monsterForm = (MonsterFormData)monsterData.Forms[context.Target.BaseForm.Form];
                int exp = expFormula(monsterForm.ExpYield, context.Target.Level);
                if (context.Target.MemberTeam is ExplorerTeam)
                    exp *= 2;
                int gainedMoney = exp;
                if (gainedMoney > 0)
                {
                    yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.DropMoney(gainedMoney, context.Target.CharLoc, context.Target.CharLoc));
                }
            }
        }

        private int expFormula(int expYield, int level)
        {
            return (int)((ulong)expYield * (ulong)level / 5) + 1;
        }
    }

    /// <summary>
    /// Event that causes the target to drop money based on the damage dealt
    /// </summary>
    [Serializable]
    public class DamageMoneyEvent : BattleEvent
    {
        
        /// <summary>
        /// The drop money mutliplier given by the damage dealt times the multiplier
        /// </summary>
        public int Multiplier;

        public DamageMoneyEvent() { }
        public DamageMoneyEvent(int multiplier) { Multiplier = multiplier; }
        protected DamageMoneyEvent(DamageMoneyEvent other)
        {
            Multiplier = other.Multiplier;
        }
        public override GameEvent Clone() { return new DamageMoneyEvent(this); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int damage = context.GetContextStateInt<TotalDamageDealt>(true, 0);
            int gainedMoney = damage * Multiplier;
            if (gainedMoney > 0)
            {
                foreach (Loc tile in context.StrikeLandTiles)
                    yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.DropMoney(gainedMoney, tile, tile));
            }
        }
    }

    /// <summary>
    /// Event that causes the target to drop a portion of the its money
    /// </summary>
    [Serializable]
    public class KnockMoneyEvent : BattleEvent
    {
        
        /// <summary>
        /// The money lost multipler given by the formula, (Multiplier - 1) / Multiplier
        /// </summary>
        public int Multiplier;

        public KnockMoneyEvent() { }
        public KnockMoneyEvent(int multiplier) { Multiplier = multiplier; }
        protected KnockMoneyEvent(KnockMoneyEvent other)
        {
            Multiplier = other.Multiplier;
        }
        public override GameEvent Clone() { return new KnockMoneyEvent(this); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Target.CharStates.Contains<StickyHoldState>())
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_STICKY_HOLD_MONEY").ToLocal(), context.Target.GetDisplayName(false)));
                yield break;
            }

            if (context.Target.MemberTeam is ExplorerTeam)
            {
                ExplorerTeam team = (ExplorerTeam)context.Target.MemberTeam;
                int moneyLost = team.Money - team.Money * (Multiplier - 1) / Multiplier; 

                if (moneyLost > 0)
                {
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_KNOCK_MONEY").ToLocal(), context.Target.GetDisplayName(false), Text.FormatKey("MONEY_AMOUNT", moneyLost.ToString())));
                    team.LoseMoney(context.Target, moneyLost);
                    Loc endLoc = context.Target.CharLoc + context.User.CharDir.GetLoc() * 2;
                    yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.DropMoney(moneyLost, endLoc, context.Target.CharLoc));
                }
            }
        }
    }

    /// <summary>
    /// Event that changes the move type depending on the map seed and the unique character ID
    /// </summary>
    [Serializable]
    public class HiddenPowerEvent : BattleEvent
    {
        public HiddenPowerEvent() { }
        public override GameEvent Clone() { return new HiddenPowerEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            List<string> possibleElements = new List<string>();
            foreach (string key in DataManager.Instance.DataIndices[DataManager.DataType.Element].GetOrderedKeys(true))
            {
                if (key != DataManager.Instance.DefaultElement)
                    possibleElements.Add(key);
            }
            ulong elementID = (ZoneManager.Instance.CurrentMap.Rand.FirstSeed ^ (ulong)context.User.Discriminator) % (ulong)(possibleElements.Count);
            context.Data.Element = possibleElements[(int)elementID];
            ElementData element = DataManager.Instance.GetElement(context.Data.Element);
            DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_SKILL_TO_ELEMENT").ToLocal(), element.GetIconName()));
            yield break;
        }
    }

    /// <summary>
    /// Event that changes the move to the character's primary type
    /// </summary>
    [Serializable]
    public class MatchAttackToTypeEvent : BattleEvent
    {
        public MatchAttackToTypeEvent() { }
        public override GameEvent Clone() { return new MatchAttackToTypeEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            context.Data.Element = context.User.Element1;
            yield break;
        }
    }

    /// <summary>
    /// Event that changes the move type depending on the character's held item
    /// </summary>
    [Serializable]
    public class ItemPowerEvent : BattleEvent
    {
        
        /// <summary>
        /// The item ID mapped to a type
        /// </summary>
        [JsonConverter(typeof(ItemElementDictConverter))]
        [DataType(1, DataManager.DataType.Item, false)]
        [DataType(2, DataManager.DataType.Element, false)]
        public Dictionary<string, string> ItemPair;

        public ItemPowerEvent() { ItemPair = new Dictionary<string, string>(); }
        public ItemPowerEvent(Dictionary<string, string> weather)
        {
            ItemPair = weather;
        }
        protected ItemPowerEvent(ItemPowerEvent other)
            : this()
        {
            foreach (string item in other.ItemPair.Keys)
                ItemPair.Add(item, other.ItemPair[item]);
        }
        public override GameEvent Clone() { return new ItemPowerEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            string element;
            if (ItemPair.TryGetValue(context.User.EquippedItem.ID, out element))
            {
                context.Data.Element = element;
                ElementData elementData = DataManager.Instance.GetElement(element);
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_SKILL_TO_ELEMENT").ToLocal(), elementData.GetIconName()));
                yield break;
            }
        }
    }

    /// <summary>
    /// Event that modifies the base power of the move depending on the weight of the target
    /// </summary>
    [Serializable]
    public class WeightBasePowerEvent : BattleEvent
    {
        public WeightBasePowerEvent() { }
        public override GameEvent Clone() { return new WeightBasePowerEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            BasePowerState basePower = context.Data.SkillStates.GetWithDefault<BasePowerState>();
            if (basePower != null)
            {
                MonsterFormData formData = (MonsterFormData)DataManager.Instance.GetMonster(context.Target.CurrentForm.Species).Forms[context.Target.CurrentForm.Form];
                double weight = formData.Weight;

                //light/heavy flags here
                if (context.Target.CharStates.Contains<LightWeightState>())
                    weight /= 2;
                if (context.Target.CharStates.Contains<HeavyWeightState>())
                    weight *= 2;

                if (weight > 200)
                    basePower.Power = 160;
                else if (weight > 100)
                    basePower.Power = 120;
                else if (weight > 50)
                    basePower.Power = 100;
                else if (weight > 25)
                    basePower.Power = 80;
                else if (weight > 10)
                    basePower.Power = 60;
                else
                    basePower.Power = 40;
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that boosts the power of moves depending on the total stat changes
    /// </summary>
    [Serializable]
    public class StatBasePowerEvent : BattleEvent
    {
        /// <summary>
        /// The base power for each stat change
        /// </summary>
        public int AddedPower;
        
        /// <summary>
        /// Whether to check the target or user
        /// </summary>
         public bool FromTarget;
        [JsonConverter(typeof(StatusSetConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public HashSet<string> StatChangeIDs;

        public StatBasePowerEvent() { StatChangeIDs = new HashSet<string>(); }
        public StatBasePowerEvent(int addedPower, bool fromTarget, HashSet<string> statChangeIDs)
        {
            AddedPower = addedPower;
            FromTarget = fromTarget;
            StatChangeIDs = statChangeIDs;
        }
        protected StatBasePowerEvent(StatBasePowerEvent other) : this()
        {
            AddedPower = other.AddedPower;
            FromTarget = other.FromTarget;
            foreach (string statID in other.StatChangeIDs)
                StatChangeIDs.Add(statID);
        }
        public override GameEvent Clone() { return new StatBasePowerEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character source = (FromTarget ? context.Target : context.User);
            BasePowerState basePower = context.Data.SkillStates.GetWithDefault<BasePowerState>();
            if (basePower != null)
            {
                int totalStacks = 0;

                foreach (string statID in StatChangeIDs)
                {
                    StatusEffect statChange = source.GetStatusEffect(statID);
                    if (statChange != null)
                        totalStacks += Math.Max(0, statChange.StatusStates.GetWithDefault<StackState>().Stack);
                }

                basePower.Power += AddedPower * totalStacks;
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that makes a move deal more or less damage depending on the character's HP
    /// </summary>
    [Serializable]
    public class HPBasePowerEvent : BattleEvent
    {
        
        /// <summary>
        /// The max base power of the move
        /// </summary>
        public int MaxPower;
        
        /// <summary>
        /// Whether or not the less HP the character has, the more damage 
        /// </summary>
        public bool Reverse;
        
        /// <summary>
        /// Whether to calculate the power target or user HP
        /// </summary>
        public bool FromTarget;

        public HPBasePowerEvent() { }
        public HPBasePowerEvent(int maxPower, bool reverse, bool affectTarget)
        {
            MaxPower = maxPower;
            Reverse = reverse;
            FromTarget = affectTarget;
        }
        protected HPBasePowerEvent(HPBasePowerEvent other)
        {
            MaxPower = other.MaxPower;
            Reverse = other.Reverse;
            FromTarget = other.FromTarget;
        }
        public override GameEvent Clone() { return new HPBasePowerEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character source = (FromTarget ? context.Target : context.User);
            BasePowerState basePower = context.Data.SkillStates.GetWithDefault<BasePowerState>();
            if (basePower != null)
                basePower.Power = MaxPower * (Reverse ? (source.MaxHP - source.HP) : source.HP) / source.MaxHP;
            yield break;
        }
    }


    /// <summary>
    /// Event that makes a move deal more or less damage depending on the PP amount 
    /// </summary>
    [Serializable]
    public class PPBasePowerEvent : BattleEvent
    {
        /// <summary>
        /// The max base power of the move
        /// </summary> 
        public int MaxPower;
        
        /// <summary>
        /// Whether or not the less PP, the more damage
        /// </summary>  
        public bool Reverse;
        
        /// <summary>
        /// Whether to also consider the PP of other moves
        /// </summary>  
        public bool Total;

        public PPBasePowerEvent() { }
        public PPBasePowerEvent(int maxPower, bool reverse, bool total)
        {
            MaxPower = maxPower;
            Reverse = reverse;
            Total = total;
        }
        protected PPBasePowerEvent(PPBasePowerEvent other)
        {
            MaxPower = other.MaxPower;
            Reverse = other.Reverse;
            Total = other.Total;
        }
        public override GameEvent Clone() { return new PPBasePowerEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int pp = 0;
            int maxPP = 0;

            int slot = -1;
            if (context.ActionType == BattleActionType.Skill && context.UsageSlot > BattleContext.DEFAULT_ATTACK_SLOT && context.UsageSlot < CharData.MAX_SKILL_SLOTS)
            {
                Skill move = context.User.Skills[context.UsageSlot].Element;
                if (!String.IsNullOrEmpty(move.SkillNum))
                {
                    slot = context.UsageSlot;
                    SkillData data = DataManager.Instance.GetSkill(move.SkillNum);
                    int localMax = data.BaseCharges + context.User.ChargeBoost;
                    pp += Math.Min(move.Charges + 1, localMax);
                    maxPP += localMax;
                }
            }

            if (Total)
            {
                for (int ii = 0; ii < context.User.Skills.Count; ii++)
                {
                    Skill move = context.User.Skills[ii].Element;
                    if (ii != slot && !String.IsNullOrEmpty(move.SkillNum))
                    {
                        SkillData data = DataManager.Instance.GetSkill(move.SkillNum);
                        int localMax = data.BaseCharges + context.User.ChargeBoost;
                        pp += move.Charges;
                        maxPP += localMax;
                    }
                }
            }
            BasePowerState basePower = context.Data.SkillStates.GetWithDefault<BasePowerState>();
            if (basePower != null)
            {
                if (maxPP > 0)
                    basePower.Power = MaxPower * (Reverse ? (maxPP - pp) : pp) / maxPP;
                else
                    basePower.Power = MaxPower;
            }

            yield break;
        }
    }

    /// <summary>
    /// Event that modifies the base power of the move based on the number of allies around the character 
    /// </summary>
    [Serializable]
    public class AllyBasePowerEvent : BattleEvent
    {
        
        /// <summary>
        /// Whether or not the more allies, the less damage 
        /// </summary>  
        public bool Reverse;

        public AllyBasePowerEvent() { }
        public AllyBasePowerEvent(bool reverse)
        {
            Reverse = reverse;
        }
        protected AllyBasePowerEvent(AllyBasePowerEvent other)
        {
            Reverse = other.Reverse;
        }
        public override GameEvent Clone() { return new AllyBasePowerEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            BasePowerState basePower = context.Data.SkillStates.GetWithDefault<BasePowerState>();

            if (basePower != null)
            {
                int totalAllies = 0;
                foreach (Character ally in context.User.MemberTeam.EnumerateChars())
                {
                    if (ZoneManager.Instance.CurrentMap.InRange(ally.CharLoc, context.User.CharLoc, 1))
                        totalAllies++;
                }
                if (Reverse)
                    basePower.Power = basePower.Power / totalAllies;
                else
                    basePower.Power = basePower.Power * totalAllies;
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that modifies the base power of the move based on the movement speed differences
    /// </summary>
    [Serializable]
    public class SpeedPowerEvent : BattleEvent
    {
        
        /// <summary>
        /// Whether the less movement speed the user has, the more damage
        /// </summary>  
        public bool Reverse;

        public SpeedPowerEvent() { }
        public SpeedPowerEvent(bool reverse)
        {
            Reverse = reverse;
        }
        protected SpeedPowerEvent(SpeedPowerEvent other)
        {
            Reverse = other.Reverse;
        }
        public override GameEvent Clone() { return new SpeedPowerEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            BasePowerState basePower = context.Data.SkillStates.GetWithDefault<BasePowerState>();

            if (basePower != null)
            {
                int speedDiff = context.User.MovementSpeed - context.Target.MovementSpeed;
                if (Reverse)
                    speedDiff *= -1;
                if (speedDiff > 0)
                    basePower.Power = (basePower.Power * (1 + speedDiff));
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that increases the base power of the move based on the weight ratio difference 
    /// </summary>
    [Serializable]
    public class WeightCrushBasePowerEvent : BattleEvent
    {
        public WeightCrushBasePowerEvent() { }
        public override GameEvent Clone() { return new WeightCrushBasePowerEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            BasePowerState basePower = context.Data.SkillStates.GetWithDefault<BasePowerState>();
            if (basePower != null)
            {
                MonsterFormData userForm = (MonsterFormData)DataManager.Instance.GetMonster(context.User.CurrentForm.Species).Forms[context.User.CurrentForm.Form];
                double userWeight = userForm.Weight;
                //light/heavy flags here
                if (context.User.CharStates.Contains<LightWeightState>())
                    userWeight /= 2;
                if (context.User.CharStates.Contains<HeavyWeightState>())
                    userWeight *= 2;

                MonsterFormData targetForm = (MonsterFormData)DataManager.Instance.GetMonster(context.Target.CurrentForm.Species).Forms[context.Target.CurrentForm.Form];
                double targetWeight = targetForm.Weight;
                //light/heavy flags here
                if (context.Target.CharStates.Contains<LightWeightState>())
                    targetWeight /= 2;
                if (context.Target.CharStates.Contains<HeavyWeightState>())
                    targetWeight *= 2;

                basePower.Power = 0;
                int weightRatio = (int)(userWeight / targetWeight);
                if (weightRatio > 5)
                    basePower.Power = 160;
                else if (weightRatio > 4)
                    basePower.Power = 120;
                else if (weightRatio > 3)
                    basePower.Power = 80;
                else if (weightRatio > 2)
                    basePower.Power = 40;
                else
                    basePower.Power = 20;
            }
            yield break;
        }
    }


    /// <summary>
    /// Event that modifies the damage multiplier if the target in the status matches the enemy
    /// </summary>
    [Serializable]
    public class RevengeEvent : BattleEvent
    {
        /// <summary>
        /// The status which contains the target
        /// Should usally be "last targeted by"
        /// </summary> 
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string TargetStatusID;
        
        /// <summary>
        /// Numerator of the modifier
        /// </summary>
        public int Numerator;
        
        /// <summary>
        /// Denominator of the modifier
        /// </summary>
        public int Denominator;
        
        /// <summary>
        /// Whether to affect the target or user
        /// </summary>
        public bool AffectTarget;
        
        /// <summary>
        /// Whether to display the message associated with this event
        /// </summary> 
        public bool Msg;

        public RevengeEvent() { TargetStatusID = ""; }
        public RevengeEvent(string targetStatusID, int numerator, int denominator, bool affectTarget, bool msg)
        {
            TargetStatusID = targetStatusID;
            Numerator = numerator;
            Denominator = denominator;
            AffectTarget = affectTarget;
            Msg = msg;
        }
        protected RevengeEvent(RevengeEvent other)
        {
            TargetStatusID = other.TargetStatusID;
            Numerator = other.Numerator;
            Denominator = other.Denominator;
            AffectTarget = other.AffectTarget;
            Msg = other.Msg;
        }
        public override GameEvent Clone() { return new RevengeEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = (AffectTarget ? context.Target : context.User);
            Character origin = (AffectTarget ? context.User : context.Target);
            if (target.Dead)
                yield break;

            BasePowerState basePower = context.Data.SkillStates.GetWithDefault<BasePowerState>();
            if (basePower != null)
            {
                StatusEffect status = target.GetStatusEffect(TargetStatusID);
                if (status != null && status.TargetChar == origin && (status.StatusStates.GetWithDefault<HPState>().HP > 0))
                {
                    if (Msg)
                        DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_DMG_BOOST_REVENGE").ToLocal()));
                    basePower.Power = (basePower.Power * Numerator / Denominator);
                }
            }
            yield break;
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
    /// Event that causes the battle action to miss if the character doesn't have the specified status
    /// </summary>
    [Serializable]
    public class TargetStatusNeededEvent : BattleEvent
    {
        /// <summary>
        /// The status ID to check for
        /// </summary>
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string StatusID;
        
        /// <summary>
        /// Whether to check the status on the target or user 
        /// </summary>
        public bool AffectTarget;
        
        /// <summary>
        /// The message displayed in the dungeon log if the conditon is met 
        /// </summary> 
        public StringKey Message;

        public TargetStatusNeededEvent() { StatusID = ""; }
        public TargetStatusNeededEvent(string statusID, bool affectTarget, StringKey msg)
        {
            StatusID = statusID;
            AffectTarget = affectTarget;
            Message = msg;
        }
        protected TargetStatusNeededEvent(TargetStatusNeededEvent other)
        {
            StatusID = other.StatusID;
            AffectTarget = other.AffectTarget;
            Message = other.Message;
        }
        public override GameEvent Clone() { return new TargetStatusNeededEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = (AffectTarget ? context.Target : context.User);

            if (target.GetStatusEffect(StatusID) == null)
            {
                context.AddContextStateMult<DmgMult>(false, -1, 1);
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), target.GetDisplayName(false)));
            }
            yield break;
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
    /// Event that modifies the damage multiplier if the character is inflicted with a major status condition
    /// </summary> 
    [Serializable]
    public class MajorStatusPowerEvent : BattleEvent
    {
        /// <summary>
        /// Whether to check the status on the target or user
        /// </summary> 
        public bool AffectTarget;
        
        /// <summary>
        /// The numerator of the modifier
        /// </summary>
        public int Numerator;
        
        /// <summary>
        /// The denominator of the modifier
        /// </summary>
        public int Denominator;

        public MajorStatusPowerEvent() { }
        public MajorStatusPowerEvent(bool affectTarget, int numerator, int denominator)
        {
            AffectTarget = affectTarget;
            Numerator = numerator;
            Denominator = denominator;
        }
        protected MajorStatusPowerEvent(MajorStatusPowerEvent other)
        {
            AffectTarget = other.AffectTarget;
            Numerator = other.Numerator;
            Denominator = other.Denominator;
        }
        public override GameEvent Clone() { return new MajorStatusPowerEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = (AffectTarget ? context.Target : context.User);

            BasePowerState basePower = context.Data.SkillStates.GetWithDefault<BasePowerState>();
            if (basePower != null)
            {
                foreach (StatusEffect status in target.IterateStatusEffects())
                {
                    if (status.StatusStates.Contains<MajorStatusState>())
                    {
                        if (AffectTarget)
                            DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_DMG_BOOST_ANY_STATUS_OTHER").ToLocal()));
                        else
                            DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_DMG_BOOST_ANY_STATUS").ToLocal()));
                        basePower.Power *= Numerator;
                        basePower.Power /= Denominator;
                        break;
                    }
                }
            }
            yield break;
        }
    }
    
    /// <summary>
    /// Event that modifies the base power of the move if the character is inflicted with the specified status 
    /// </summary> 
    [Serializable]
    public class StatusPowerEvent : BattleEvent
    {
        /// <summary>
        /// The status ID to check for
        /// </summary> 
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string StatusID;
        
        /// <summary>
        /// Whether to check the status on the target or user
        /// </summary> 
        public bool AffectTarget;

        public StatusPowerEvent() { StatusID = ""; }
        public StatusPowerEvent(string statusID, bool affectTarget)
        {
            StatusID = statusID;
            AffectTarget = affectTarget;
        }
        protected StatusPowerEvent(StatusPowerEvent other)
        {
            StatusID = other.StatusID;
            AffectTarget = other.AffectTarget;
        }
        public override GameEvent Clone() { return new StatusPowerEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = (AffectTarget ? context.Target : context.User);

            BasePowerState basePower = context.Data.SkillStates.GetWithDefault<BasePowerState>();
            if (basePower != null)
            {
                if (target.GetStatusEffect(StatusID) != null)
                {
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_DMG_BOOST_STATUS").ToLocal()));
                    basePower.Power *= 2;
                }
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that modifies the damage multiplier if the user's type matches the target's
    /// </summary> 
    [Serializable]
    public class SynchroTypeEvent : BattleEvent
    {
        public SynchroTypeEvent() { }
        public override GameEvent Clone() { return new SynchroTypeEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.User.Element1 != DataManager.Instance.DefaultElement && context.Target.HasElement(context.User.Element1)
                || context.User.Element2 != DataManager.Instance.DefaultElement && context.Target.HasElement(context.User.Element2))
            {

            }
            else
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_SYNCHRO_FAIL").ToLocal(), context.Target.GetDisplayName(false), context.User.GetDisplayName(false)));
                context.AddContextStateMult<DmgMult>(false, 1, 4);
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that modifies the base power of the move if the target's HP is below the specified threshold
    /// </summary> 
    [Serializable]
    public class BrineEvent : BattleEvent
    {
        public BrineEvent() { }
        public override GameEvent Clone() { return new BrineEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            BasePowerState basePower = context.Data.SkillStates.GetWithDefault<BasePowerState>();
            if (basePower != null)
            {
                if (context.Target.HP * 2 < context.Target.MaxHP)
                {
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_DMG_BOOST_LOW_HP").ToLocal()));
                    basePower.Power *= 2;
                }
            }
            yield break;
        }
    }
    
    /// <summary>
    /// Event that modifies the base power of the move if the user isn't holding an item
    /// </summary> 
    [Serializable]
    public class AcrobaticEvent : BattleEvent
    {
        public AcrobaticEvent() { }
        public override GameEvent Clone() { return new AcrobaticEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            BasePowerState basePower = context.Data.SkillStates.GetWithDefault<BasePowerState>();
            if (basePower != null)
            {
                if (String.IsNullOrEmpty(context.User.EquippedItem.ID))
                {
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_DMG_BOOST_NO_ITEM").ToLocal()));
                    basePower.Power *= 2;
                }
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that adds the specified context state 
    /// </summary> 
    [Serializable]
    public class AddContextStateEvent : BattleEvent
    {
        /// <summary>
        /// The context state to add
        /// </summary> 
        public ContextState AddedState;
        
        /// <summary>
        /// Whether to add the context state globally
        /// </summary> 
        public bool Global;

        public AddContextStateEvent() { }
        public AddContextStateEvent(ContextState state) : this(state, false) { }
        public AddContextStateEvent(ContextState state, bool global) { AddedState = state; Global = global; }
        protected AddContextStateEvent(AddContextStateEvent other)
        {
            AddedState = other.AddedState.Clone<ContextState>();
            Global = other.Global;
        }
        public override GameEvent Clone() { return new AddContextStateEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (Global)
                context.GlobalContextStates.Set(AddedState.Clone<ContextState>());
            else
                context.ContextStates.Set(AddedState.Clone<ContextState>());
            yield break;
        }
    }
    
    /// <summary>
    /// Event that displays a message to the dungeon log once
    /// </summary> 
    [Serializable]
    public class MessageOnceEvent : BattleEvent
    {
        /// <summary>
        /// The context state added to indicate if the message was displayed
        /// </summary> 
        public ContextState AddedState;
        
        /// <summary>
        /// Whether to add the context state globally 
        /// </summary> 
        public bool Global;
        
        /// <summary>
        /// Whether to display the target or user name in the message
        /// </summary> 
        public bool AffectTarget;
        
        /// <summary>
        /// The message displayed in the dungeon log if the conditon is met 
        /// </summary>  
        [StringKey(0, true)]
        public StringKey Message;
        
        /// <summary>
        /// The list of battle VFXs played if the condition is met
        /// </summary>
        public List<BattleAnimEvent> Anims;

        public MessageOnceEvent()
        {
            Anims = new List<BattleAnimEvent>();
        }
        public MessageOnceEvent(ContextState state, bool global, bool affectTarget, BattleAnimEvent anim, StringKey msg)
        {
            Anims = new List<BattleAnimEvent>();
            AddedState = state;
            Global = global;
            AffectTarget = affectTarget;
            Anims.Add(anim);
            Message = msg;
        }
        protected MessageOnceEvent(MessageOnceEvent other)
        {
            Anims = new List<BattleAnimEvent>();
            AddedState = other.AddedState.Clone<ContextState>();
            Global = other.Global;
            AffectTarget = other.AffectTarget;
            Anims.AddRange(other.Anims);
            Message = other.Message;
        }
        public override GameEvent Clone() { return new MessageOnceEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (Global)
            {
                if (context.GlobalContextStates.Contains(AddedState.GetType()))
                    yield break;
                else
                    context.GlobalContextStates.Set(AddedState.Clone<ContextState>());
            }
            else
            {
                if (context.ContextStates.Contains(AddedState.GetType()))
                    yield break;
                else
                    context.ContextStates.Set(AddedState.Clone<ContextState>());
            }

            Character target = (AffectTarget ? context.Target : context.User);

            if (Message.IsValid())
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), ownerChar.GetDisplayName(false), target.GetDisplayName(false)));

            foreach (BattleAnimEvent anim in Anims)
                yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

        }
    }

    /// <summary>
    /// Event that changes physical attack to special and vice versa
    /// </summary> 
    [Serializable]
    public class FlipCategoryEvent : BattleEvent
    {
        /// <summary>
        /// Whether the attack can change flip between categories during multi-strike moves
        /// </summary> 
        public bool MidwayCross;

        public FlipCategoryEvent() { }
        public FlipCategoryEvent(bool midway)
        {
            MidwayCross = midway;
        }
        protected FlipCategoryEvent(FlipCategoryEvent other)
        {
            MidwayCross = other.MidwayCross;
        }
        public override GameEvent Clone() { return new FlipCategoryEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.StrikesMade == 0)
            {
                if (context.Data.Category == BattleData.SkillCategory.Physical)
                    context.Data.Category = BattleData.SkillCategory.Magical;
                else if (context.Data.Category == BattleData.SkillCategory.Magical)
                    context.Data.Category = BattleData.SkillCategory.Physical;

                if (MidwayCross)
                {
                    if (context.ContextStates.Contains<CrossCategory>())
                        context.ContextStates.Remove<CrossCategory>();
                    else
                        context.ContextStates.Set(new CrossCategory());
                }
            }
            yield break;
        }
    }
    
    /// <summary>
    /// Event that uses the target's attack stat to calculate the damage
    /// </summary> 
    [Serializable]
    public class FoulPlayEvent : BattleEvent
    {
        public FoulPlayEvent() { }
        public override GameEvent Clone() { return new FoulPlayEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Data.Category == BattleData.SkillCategory.Physical)
                context.ContextStates.Set(new AttackerStat(context.Target.Atk));
            else if (context.Data.Category == BattleData.SkillCategory.Magical)
                context.ContextStates.Set(new AttackerStat(context.Target.MAtk));
            context.ContextStates.Set(new UserAtkBoost(context.GetContextStateInt<TargetAtkBoost>(0)));
            context.ContextStates.Set(new UserSpAtkBoost(context.GetContextStateInt<TargetSpAtkBoost>(0)));
            yield break;
        }
    }
    
    /// <summary>
    /// Event that ignores any stat boosts the character has
    /// </summary> 
    [Serializable]
    public class IgnoreStatsEvent : BattleEvent
    {   
        /// <summary>
        /// Whether to ignore the target or user stat boosts
        /// </summary> 
        public bool AffectTarget;

        public IgnoreStatsEvent() { }
        public IgnoreStatsEvent(bool affectTarget)
        {
            AffectTarget = affectTarget;
        }
        protected IgnoreStatsEvent(IgnoreStatsEvent other)
        {
            AffectTarget = other.AffectTarget;
        }
        public override GameEvent Clone() { return new IgnoreStatsEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (AffectTarget)
            {
                context.ContextStates.Set(new TargetAtkBoost());
                context.ContextStates.Set(new TargetSpAtkBoost());
                context.ContextStates.Set(new TargetDefBoost());
                context.ContextStates.Set(new TargetSpDefBoost());
                context.ContextStates.Set(new TargetEvasionBoost());
            }
            else
            {
                context.ContextStates.Set(new UserAtkBoost());
                context.ContextStates.Set(new UserSpAtkBoost());
                context.ContextStates.Set(new UserDefBoost());
                context.ContextStates.Set(new UserSpDefBoost());
                context.ContextStates.Set(new UserAccuracyBoost());
            }
            yield break;
        }
    }
    
    /// <summary>
    /// Event that ignores the user's accuracy descrease and target's evasive boosts
    /// </summary> 
    [Serializable]
    public class IgnoreHaxEvent : BattleEvent
    {
        public override GameEvent Clone() { return new IgnoreHaxEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            context.ContextStates.Set(new TargetEvasionBoost(Math.Min(0, context.GetContextStateInt<TargetEvasionBoost>(0))));
            context.ContextStates.Set(new UserAccuracyBoost(Math.Max(0, context.GetContextStateInt<UserAccuracyBoost>(0))));
            yield break;
        }
    }

    [Serializable]
    public abstract class DirectDamageEvent : BattleEvent
    {
        protected IEnumerator<YieldInstruction> InflictDamage(BattleContext context, int dmg)
        {
            bool fastSpeed = (DiagManager.Instance.CurSettings.BattleFlow > Settings.BattleSpeed.Fast);
            bool hasEffect = (context.Data.HitFX.Delay == 0 && context.Data.HitFX.Sound != "");//determines if a sound plays at the same frame the move hits

            if (hasEffect && fastSpeed)
            {

            }
            else
            {
                if (hasEffect)
                    yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(10, context.Target.CharLoc));
                int typeMatchup = PreTypeEvent.GetDualEffectiveness(context.User, context.Target, context.Data);

                SingleEmitter endEmitter = null;
                if (typeMatchup == PreTypeEvent.NRM_2 || fastSpeed)
                {
                    GameManager.Instance.BattleSE("DUN_Hit_Neutral");
                    endEmitter = new SingleEmitter(new AnimData("Hit_Neutral", 3));
                }
                else if (typeMatchup >= PreTypeEvent.S_E_2)
                {
                    GameManager.Instance.BattleSE("DUN_Hit_Super_Effective");
                    endEmitter = new SingleEmitter(new AnimData("Hit_Super_Effective", 3));
                }
                else
                {
                    GameManager.Instance.BattleSE("DUN_Hit_NVE");
                    endEmitter = new SingleEmitter(new AnimData("Hit_Neutral", 3));
                }

                if (!context.Target.Unidentifiable)
                {
                    endEmitter.SetupEmit(context.Target.MapLoc, context.User.MapLoc, context.Target.CharDir);
                    DungeonScene.Instance.CreateAnim(endEmitter, DrawLayer.NoDraw);
                }
            }

            bool endure = context.ContextStates.Contains<AttackEndure>();
            yield return CoroutineManager.Instance.StartCoroutine(context.Target.InflictDamage(dmg, true, endure));

            if (context.Target.HP == 0)
            {
                context.ContextStates.Set(new Knockout());
                context.AddContextStateInt<TotalKnockouts>(true, 1);
            }
        }
        protected void ReportDamage(BattleContext context, int dmg, int hpLost)
        {
            context.ContextStates.Set(new DamageDealt(dmg));
            context.AddContextStateInt<TotalDamageDealt>(true, dmg);
            context.ContextStates.Set(new HPLost(hpLost));
            context.AddContextStateInt<TotalHPLost>(true, hpLost);
        }
    }

    /// <summary>
    /// Event that OHKOs the target
    /// </summary>
    [Serializable]
    public class OHKODamageEvent : DirectDamageEvent
    {
        public OHKODamageEvent() { }
        public override GameEvent Clone() { return new OHKODamageEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int prevHP = context.Target.HP;

            int dmg = -1;

            if (!context.GetContextStateMult<DmgMult>().IsNeutralized())
            {
                int typeMatchup = PreTypeEvent.GetDualEffectiveness(context.User, context.Target, context.Data);
                if (typeMatchup <= PreTypeEvent.N_E_2)
                {
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(PreTypeEvent.EffectivenessToPhrase(typeMatchup), context.Target.GetDisplayName(false)));
                    context.AddContextStateMult<DmgMult>(false, -1, 4);
                }
            }

            int dmgMod = context.GetContextStateMult<DmgMult>().Multiply(0);
            if (dmgMod >= 0)
            {
                if (context.GetContextStateMult<DmgMult>().IsNeutralized())
                    dmg = 0;

                yield return CoroutineManager.Instance.StartCoroutine(InflictDamage(context, dmg));
            }

            int hpLost = prevHP - context.Target.HP;
            ReportDamage(context, hpLost, hpLost);
        }
    }

    [Serializable]
    public abstract class CalculatedDamageEvent : DirectDamageEvent
    {
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int damage = CalculateDamage(owner, context);

            int prevHP = context.Target.HP;
            if (damage >= 0)
                yield return CoroutineManager.Instance.StartCoroutine(InflictDamage(context, damage));

            int hpLost = prevHP - context.Target.HP;
            ReportDamage(context, Math.Max(0, damage), hpLost);
        }

        public abstract int CalculateDamage(GameEventOwner owner, BattleContext context);
    }

    /// <summary>
    /// Event that calculates the damage of the action, taking account into effectiveness, critical hits, stat boosts, and STAB
    /// </summary>
    [Serializable]
    public class DamageFormulaEvent : CalculatedDamageEvent
    {
        public DamageFormulaEvent() { }
        public override GameEvent Clone() { return new DamageFormulaEvent(); }

        public override int CalculateDamage(GameEventOwner owner, BattleContext context)
        {
            return CalculateDamageFormula(owner, context);
        }

        public static int CalculateDamageFormula(GameEventOwner owner, BattleContext context)
        {
            //PreExecuteAction: attacker attack/spAtk and level are assigned
            //in OnAction:
            //  -AttackBoost, SpAtkBoost, DefBoost, SpDefBoost, AccuracyMod are added

            //PreMoveHit: target defense/SpDef is assigned
            //in BeforeHit:
            //  -TargetAttackBoost, TargetSpAtkBoost, TargetDefenseBoost, TargetSpDefBoost, EvasionMod are added

            if (!context.GetContextStateMult<DmgMult>().IsNeutralized())
            {
                string effectivenessMsg = null;

                //modify attack based on battle tag
                int atkBoost = 0;
                int defBoost = 0;
                if (context.Data.Category == BattleData.SkillCategory.Physical || context.Data.Category == BattleData.SkillCategory.Magical)
                {
                    BattleData.SkillCategory attackCategory = context.Data.Category;
                    if (context.ContextStates.Contains<CrossCategory>())
                    {
                        if (attackCategory == BattleData.SkillCategory.Physical)
                            attackCategory = BattleData.SkillCategory.Magical;
                        else if (attackCategory == BattleData.SkillCategory.Magical)
                            attackCategory = BattleData.SkillCategory.Physical;
                    }

                    //adjust attack
                    if (attackCategory == BattleData.SkillCategory.Physical)
                        atkBoost = context.GetContextStateInt<UserAtkBoost>(0);
                    else if (attackCategory == BattleData.SkillCategory.Magical)
                        atkBoost = context.GetContextStateInt<UserSpAtkBoost>(0);

                    //adjust defense
                    if (context.Data.Category == BattleData.SkillCategory.Physical)
                        defBoost = context.GetContextStateInt<TargetDefBoost>(0);
                    else if (context.Data.Category == BattleData.SkillCategory.Magical)
                        defBoost = context.GetContextStateInt<TargetSpDefBoost>(0);
                }

                int critLevel = context.GetContextStateInt<CritLevel>(0);
                CritRateLevelTableState critTable = DataManager.Instance.UniversalEvent.UniversalStates.GetWithDefault<CritRateLevelTableState>();
                if (DataManager.Instance.Save.Rand.Next(0, 12) < critTable.GetCritChance(critLevel))
                {
                    //see if it criticals
                    if (context.User.CharStates.Contains<SnipeState>())
                        context.AddContextStateMult<DmgMult>(false, 5, 2);
                    else
                        context.AddContextStateMult<DmgMult>(false, 3, 2);

                    atkBoost = Math.Max(0, atkBoost);
                    defBoost = Math.Min(0, defBoost);

                    effectivenessMsg = Text.FormatGrammar(new StringKey("MSG_CRITICAL_HIT").ToLocal());
                    context.ContextStates.Set(new AttackCrit());
                }

                AtkDefLevelTableState dmgModTable = DataManager.Instance.UniversalEvent.UniversalStates.GetWithDefault<AtkDefLevelTableState>();
                int attackStat = dmgModTable.AtkLevelMult(context.GetContextStateInt<AttackerStat>(0), atkBoost);
                int defenseStat = Math.Max(1, dmgModTable.DefLevelMult(context.GetContextStateInt<TargetStat>(0), defBoost));

                //STAB
                if (context.User.HasElement(context.Data.Element))
                    context.AddContextStateMult<DmgMult>(false, 4, 3);

                int typeMatchup = PreTypeEvent.GetDualEffectiveness(context.User, context.Target, context.Data);
                if (typeMatchup != PreTypeEvent.NRM_2)
                {
                    if (effectivenessMsg != null)
                        effectivenessMsg += (" " + Text.FormatGrammar(PreTypeEvent.EffectivenessToPhrase(typeMatchup), context.Target.GetDisplayName(false)));
                    else
                        effectivenessMsg = Text.FormatGrammar(PreTypeEvent.EffectivenessToPhrase(typeMatchup), context.Target.GetDisplayName(false));

                    int effectiveness = PreTypeEvent.GetEffectivenessMult(typeMatchup);
                    if (effectiveness == 0)
                        effectiveness = -1;

                    context.AddContextStateMult<DmgMult>(false, effectiveness, PreTypeEvent.GetEffectivenessMult(PreTypeEvent.NRM_2));
                }

                if (effectivenessMsg != null)
                    DungeonScene.Instance.LogMsg(effectivenessMsg);

                if (context.GetContextStateMult<DmgMult>().IsNeutralized())
                    return context.GetContextStateMult<DmgMult>().Multiply(0);

                int power = context.Data.SkillStates.GetWithDefault<BasePowerState>().Power;
                int damage = context.GetContextStateMult<DmgMult>().Multiply((context.GetContextStateInt<UserLevel>(0) / 3 + 6) * attackStat * power / defenseStat / 50 * DataManager.Instance.Save.Rand.Next(90, 101) / 100);

                if (!(context.ActionType == BattleActionType.Skill && context.Data.ID == DataManager.Instance.DefaultSkill))
                    damage = Math.Max(1, damage);

                return damage;
            }
            else
                return context.GetContextStateMult<DmgMult>().Multiply(0);
        }
    }


    [Serializable]
    public abstract class FixedDamageEvent : CalculatedDamageEvent
    {
        public override int CalculateDamage(GameEventOwner owner, BattleContext context)
        {
            int damage = CalculateFixedDamage(owner, context);

            int typeMatchup = PreTypeEvent.GetDualEffectiveness(context.User, context.Target, context.Data);
            if (typeMatchup <= PreTypeEvent.N_E_2)
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(PreTypeEvent.EffectivenessToPhrase(typeMatchup), context.Target.GetDisplayName(false)));
                damage = -1;
            }

            return damage;
        }

        protected abstract int CalculateFixedDamage(GameEventOwner owner, BattleContext context);
    }

    [Serializable]
    public class BasePowerDamageEvent : FixedDamageEvent
    {
        public override GameEvent Clone() { return new BasePowerDamageEvent(); }
        protected override int CalculateFixedDamage(GameEventOwner owner, BattleContext context)
        {
            BasePowerState state = context.Data.SkillStates.GetWithDefault<BasePowerState>();
            if (state != null)
                return state.Power;
            return 0;
        }
    }

    /// <summary>
    /// Event that sets the specified damage the character will take 
    /// </summary>
    [Serializable]
    public class SpecificDamageEvent : FixedDamageEvent
    {
        /// <summary>
        /// The damage amount
        /// </summary>
        public int Damage;

        public SpecificDamageEvent() { }
        public SpecificDamageEvent(int dmg) { Damage = dmg; }
        public SpecificDamageEvent(SpecificDamageEvent other)
        {
            Damage = other.Damage;
        }

        public override GameEvent Clone() { return new SpecificDamageEvent(this); }
        protected override int CalculateFixedDamage(GameEventOwner owner, BattleContext context)
        {
            return Damage;
        }
    }

    /// <summary>
    /// Event that calculates the damage based on the character's level
    /// </summary>
    [Serializable]
    public class LevelDamageEvent : FixedDamageEvent
    {
        /// <summary>
        /// Whether to calculate with the target or user's level
        /// </summary>
        public bool AffectTarget;
        
        /// <summary>
        /// The numerator of the modifier
        /// </summary>
        public int Numerator;
        
        /// <summary>
        /// The denominator of the modifier
        /// </summary>
        public int Denominator;
        public LevelDamageEvent() { }
        public LevelDamageEvent(bool affectTarget, int numerator, int denominator)
        {
            AffectTarget = affectTarget;
            Numerator = numerator;
            Denominator = denominator;
        }
        protected LevelDamageEvent(LevelDamageEvent other)
        {
            AffectTarget = other.AffectTarget;
            Numerator = other.Numerator;
            Denominator = other.Denominator;
        }
        public override GameEvent Clone() { return new LevelDamageEvent(this); }
        protected override int CalculateFixedDamage(GameEventOwner owner, BattleContext context)
        {
            int level = (AffectTarget ? context.Target.Level : context.GetContextStateInt<UserLevel>(0));
            return level * Numerator / Denominator;
        }
    }

    /// <summary>
    /// Event that deals fixed damage depending on the target's distance from the attack and the user's level
    /// </summary>
    [Serializable]
    public class PsywaveDamageEvent : FixedDamageEvent
    {
        public override GameEvent Clone() { return new PsywaveDamageEvent(); }
        protected override int CalculateFixedDamage(GameEventOwner owner, BattleContext context)
        {
            // 1 2 1 0 1 2 1 0
            // sine wave function
            //TODO: this breaks in small wrapped maps
            int locDiff = ZoneManager.Instance.CurrentMap.GetClosestDist8(context.StrikeStartTile, context.Target.CharLoc);
            int diff = locDiff % 4;
            int power = (diff > 2) ? 1 : diff;
            return Math.Max(1, context.GetContextStateInt<UserLevel>(0) * power / 2);
        }
    }

    [Serializable]
    public class UserHPDamageEvent : FixedDamageEvent
    {
        public bool Reverse;
        public UserHPDamageEvent() { }
        public UserHPDamageEvent(bool reverse)
        {
            Reverse = reverse;
        }
        protected UserHPDamageEvent(UserHPDamageEvent other)
        {
            Reverse = other.Reverse;
        }
        public override GameEvent Clone() { return new UserHPDamageEvent(this); }
        protected override int CalculateFixedDamage(GameEventOwner owner, BattleContext context)
        {
            return Reverse ? (context.User.MaxHP - context.User.HP) : context.User.HP;
        }
    }

    /// <summary>
    /// Event that reduces the target's HP to the user's HP
    /// </summary>
    [Serializable]
    public class EndeavorEvent : FixedDamageEvent
    {
        public override GameEvent Clone() { return new EndeavorEvent(); }
        protected override int CalculateFixedDamage(GameEventOwner owner, BattleContext context)
        {
            return Math.Max(0, context.Target.HP - context.User.HP);
        }
    }

    /// <summary>
    /// Event that reduces the target's HP by half
    /// </summary>
    [Serializable]
    public class CutHPDamageEvent : FixedDamageEvent
    {
        public override GameEvent Clone() { return new CutHPDamageEvent(); }
        protected override int CalculateFixedDamage(GameEventOwner owner, BattleContext context)
        {
            return Math.Max(1, context.GetContextStateMult<HPDmgMult>().Multiply(context.Target.HP / 2));
        }
    }
    
    /// <summary>
    /// Event that reduces the target's HP by the specified HP fraction 
    /// </summary>
    [Serializable]
    public class MaxHPDamageEvent : FixedDamageEvent
    {
        public int HPFraction;

        public MaxHPDamageEvent() { }
        public MaxHPDamageEvent(int hpFraction)
        {
            HPFraction = hpFraction;
        }
        protected MaxHPDamageEvent(MaxHPDamageEvent other)
        {
            HPFraction = other.HPFraction;
        }
        public override GameEvent Clone() { return new MaxHPDamageEvent(this); }
        protected override int CalculateFixedDamage(GameEventOwner owner, BattleContext context)
        {
            return Math.Max(1, context.GetContextStateMult<HPDmgMult>().Multiply(context.Target.MaxHP / HPFraction));
        }
    }

    /// <summary>
    /// Event that groups multiple battle events into one event
    /// </summary>
    [Serializable]
    public class GroupEvent : BattleEvent
    {
        //can be used for hit-consequence effects
        /// <summary>
        /// The list of battle events that will be applied 
        /// </summary>
        public List<BattleEvent> BaseEvents;

        public GroupEvent() { BaseEvents = new List<BattleEvent>(); }
        public GroupEvent(params BattleEvent[] effects)
        {
            BaseEvents = new List<BattleEvent>();
            foreach (BattleEvent effect in effects)
                BaseEvents.Add(effect);
        }
        protected GroupEvent(GroupEvent other)
            : this()
        {
            foreach (BattleEvent battleEffect in other.BaseEvents)
                BaseEvents.Add((BattleEvent)battleEffect.Clone());
        }
        public override GameEvent Clone() { return new GroupEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            foreach (BattleEvent battleEffect in BaseEvents)
                yield return CoroutineManager.Instance.StartCoroutine(battleEffect.Apply(owner, ownerChar, context));
        }
    }
    
    /// <summary>
    /// Event that chooses a random battle event
    /// </summary>
    [Serializable]
    public class ChooseOneEvent : BattleEvent
    {
        //can be used for hit-consequence effects
        /// <summary>
        /// The list of battle events to choose from
        /// </summary>
        public List<BattleEvent> BaseEvents;

        public ChooseOneEvent() { BaseEvents = new List<BattleEvent>(); }
        public ChooseOneEvent(params BattleEvent[] effects)
        {
            BaseEvents = new List<BattleEvent>();
            foreach (BattleEvent effect in effects)
                BaseEvents.Add(effect);
        }
        protected ChooseOneEvent(ChooseOneEvent other)
            : this()
        {
            foreach (BattleEvent battleEffect in other.BaseEvents)
                BaseEvents.Add((BattleEvent)battleEffect.Clone());
        }
        public override GameEvent Clone() { return new ChooseOneEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            yield return CoroutineManager.Instance.StartCoroutine(BaseEvents[DataManager.Instance.Save.Rand.Next(BaseEvents.Count)].Apply(owner, ownerChar, context));
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
    /// Event that prevents the character from using the move if the specified map status is not present
    /// </summary> 
    [Serializable]
    public class WeatherRequiredEvent : BattleEvent
    {
        /// <summary>
        /// The status ID to check for
        /// </summary> 
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.MapStatus, false)]
        public string WeatherID;

        /// <summary>
        /// The message displayed in the dungeon log if the conditon is met 
        /// </summary> 
        public StringKey Message;

        public WeatherRequiredEvent() { WeatherID = ""; }
        public WeatherRequiredEvent(string statusID, StringKey msg)
        {
            WeatherID = statusID;
            Message = msg;
        }
        protected WeatherRequiredEvent(WeatherRequiredEvent other)
        {
            WeatherID = other.WeatherID;
            Message = other.Message;
        }
        public override GameEvent Clone() { return new WeatherRequiredEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (!ZoneManager.Instance.CurrentMap.Status.ContainsKey(WeatherID))
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), context.User.GetDisplayName(false)));
                context.CancelState.Cancel = true;
            }
            yield break;
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
    /// Event that groups multiple battle events into one event, but only applies if the target's type matches the specified type
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

        public CharElementNeededEvent() { BaseEvents = new List<BattleEvent>(); NeededElement = ""; }
        public CharElementNeededEvent(string element, params BattleEvent[] effects)
            : this()
        {
            NeededElement = element;
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
            if (context.Target.HasElement(NeededElement))
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
    /// Event that groups multiple battle events into one event, but only applies if a critical hit was landed 
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
    /// Event that makes the character return to its original position after a dash action
    /// </summary>
    [Serializable]
    public class SnapDashBackEvent : BattleEvent
    {
        public override GameEvent Clone() { return new SnapDashBackEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            DashAction dash = context.HitboxAction as DashAction;
            if (dash != null)
                dash.SnapBack = true;
            yield break;
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
            : base(statusID, affectTarget, silentCheck, false) { AltStatusID = altStatusID; }
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
            context.User.CharLoc = context.Target.CharLoc + context.User.CharDir.Reverse().GetLoc();
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
            : base(statusID, affectTarget, silentCheck, false) { AltStatusID = altStatusID; }
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
        /// Whether to display a message if the status fails to apply
        /// </summary
        public bool SilentCheck;
        
        /// <summary>
        /// Whether to include the user of the status in the context
        /// </summary
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
         : this(statusID, affectTarget, silentCheck, false) { }
        public StatusBattleEvent(string statusID, bool affectTarget, bool silentCheck, bool anonymous)
        {
            StatusID = statusID;
            AffectTarget = affectTarget;
            SilentCheck = silentCheck;
            Anonymous = anonymous;
            Anims = new List<BattleAnimEvent>();
        }
        public StatusBattleEvent(string statusID, bool affectTarget, bool silentCheck, bool anonymous, StringKey trigger, params BattleAnimEvent[] anims)
        {
            StatusID = statusID;
            AffectTarget = affectTarget;
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
        public StatusStackBattleEvent(string statusID, bool affectTarget, bool silentCheck, int stack) : this(statusID, affectTarget, silentCheck, false, stack) { }
        public StatusStackBattleEvent(string statusID, bool affectTarget, bool silentCheck, bool anonymous, int stack)
            : base(statusID, affectTarget, silentCheck, anonymous)
        {
            Stack = stack;
        }
        public StatusStackBattleEvent(string statusID, bool affectTarget, bool silentCheck, bool anonymous, int stack, StringKey trigger)
            : base(statusID, affectTarget, silentCheck, anonymous, trigger)
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
        public StatusElementBattleEvent(string statusID, bool affectTarget, bool silentCheck, string element) : this(statusID, affectTarget, silentCheck, false, element) { }
        public StatusElementBattleEvent(string statusID, bool affectTarget, bool silentCheck, bool anonymous, string element)
            : base(statusID, affectTarget, silentCheck, anonymous)
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
        public StatusStateBattleEvent(string statusID, bool affectTarget, bool silentCheck, StateCollection<StatusState> states) : this(statusID, affectTarget, silentCheck, false, states) { }
        public StatusStateBattleEvent(string statusID, bool affectTarget, bool silentCheck, bool anonymous, StateCollection<StatusState> states) : base(statusID, affectTarget, silentCheck, anonymous)
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
            : this(statusID, prevMoveID, false, false) { }
        public DisableBattleEvent(string statusID, string prevMoveID, bool anonymous, bool randomFallback)
            : base(statusID, true, false, anonymous)
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
        public CounterDisableBattleEvent(string statusID) : base(statusID, false, true, false) { }
        public CounterDisableBattleEvent(string statusID, StringKey trigger) : base(statusID, false, true, false, trigger) { }
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
        public WeatherStackEvent(string statusID, bool affectTarget, bool silentCheck, string weatherID) : base(statusID, affectTarget, silentCheck, false)
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
        public StatusHPBattleEvent(string statusID, bool affectTarget, bool silentCheck, bool anonymous, int hpFraction)
            : base(statusID, affectTarget, silentCheck, anonymous)
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
        public FutureAttackEvent(string statusID, bool affectTarget, bool silentCheck, bool anonymous)
            : base(statusID, affectTarget, silentCheck, anonymous)
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
            : this(statusID, affectTarget, silentCheck, false) { }
        public GiveContinuousDamageEvent(string statusID, bool affectTarget, bool silentCheck, bool anonymous)
            : base(statusID, affectTarget, silentCheck, anonymous) { }
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
    /// Event that maps the current map status to a battle event.
    /// If there is no match, it maps the current map type to a battle event.
    /// </summary>
    [Serializable]
    public class NatureSpecialEvent : BattleEvent
    {
        /// <summary>
        /// The map status mapped to a battle event
        /// </summary>
        [JsonConverter(typeof(MapStatusBattleEventDictConverter))]
        [DataType(0, DataManager.DataType.MapStatus, false)]
        public Dictionary<string, BattleEvent> TerrainPair;
        
        /// <summary>
        /// The type mapped to a battle event
        /// </summary>
        [JsonConverter(typeof(ElementBattleEventDictConverter))]
        [DataType(1, DataManager.DataType.Element, false)]
        public Dictionary<string, BattleEvent> NaturePair;

        public NatureSpecialEvent()
        {
            TerrainPair = new Dictionary<string, BattleEvent>();
            NaturePair = new Dictionary<string, BattleEvent>();
        }
        public NatureSpecialEvent(Dictionary<string, BattleEvent> terrain, Dictionary<string, BattleEvent> moves)
        {
            TerrainPair = terrain;
            NaturePair = moves;
        }
        protected NatureSpecialEvent(NatureSpecialEvent other)
            : this()
        {
            foreach (string terrain in other.TerrainPair.Keys)
                TerrainPair.Add(terrain, (BattleEvent)other.TerrainPair[terrain].Clone());
            foreach (string element in other.NaturePair.Keys)
                NaturePair.Add(element, (BattleEvent)other.NaturePair[element].Clone());
        }
        public override GameEvent Clone() { return new NatureSpecialEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            foreach (string terrain in TerrainPair.Keys)
            {
                if (ZoneManager.Instance.CurrentMap.Status.ContainsKey(terrain))
                {
                    yield return CoroutineManager.Instance.StartCoroutine(TerrainPair[terrain].Apply(owner, ownerChar, context));
                    yield break;
                }
            }

            BattleEvent effect;
            if (NaturePair.TryGetValue(ZoneManager.Instance.CurrentMap.Element, out effect))
                yield return CoroutineManager.Instance.StartCoroutine(effect.Apply(owner, ownerChar, context));
            else
                yield break;
        }
    } 
    
    /// <summary>
    /// Event that sets the specified map status
    /// </summary>
    [Serializable]
    public class GiveMapStatusEvent : BattleEvent
    {
        /// <summary>
        /// The map status to add
        /// </summary>
        [JsonConverter(typeof(MapStatusConverter))]
        [DataType(0, DataManager.DataType.MapStatus, false)]
        public string StatusID;
        
        /// <summary>
        /// The amount of turns the map status will last
        /// </summary>
        public int Counter;
        
        /// <summary>
        /// The message displayed in the dungeon log when the map status is added
        /// </summary>
        [StringKey(0, true)]
        public StringKey MsgOverride;

        /// <summary>
        /// If the user contains one of the specified CharStates, then the weather is extended by the multiplier
        /// </summary>
        [StringTypeConstraint(1, typeof(CharState))]
        public List<FlagType> States;

        public GiveMapStatusEvent() { States = new List<FlagType>(); StatusID = ""; }
        public GiveMapStatusEvent(string id)
        {
            States = new List<FlagType>();
            StatusID = id;
        }
        public GiveMapStatusEvent(string id, int counter)
        {
            States = new List<FlagType>();
            StatusID = id;
            Counter = counter;
        }
        public GiveMapStatusEvent(string id, int counter, StringKey msg)
        {
            States = new List<FlagType>();
            StatusID = id;
            Counter = counter;
            MsgOverride = msg;
        }
        public GiveMapStatusEvent(string id, int counter, StringKey msg, Type state)
        {
            States = new List<FlagType>();
            StatusID = id;
            Counter = counter;
            MsgOverride = msg;
            States.Add(new FlagType(state));
        }
        protected GiveMapStatusEvent(GiveMapStatusEvent other)
            : this()
        {
            StatusID = other.StatusID;
            Counter = other.Counter;
            MsgOverride = other.MsgOverride;
            States.AddRange(other.States);
        }
        public override GameEvent Clone() { return new GiveMapStatusEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            //add the map status
            MapStatus status = new MapStatus(StatusID);
            status.LoadFromData();
            if (Counter != 0)
                status.StatusStates.GetWithDefault<MapCountDownState>().Counter = Counter;

            bool hasState = false;
            foreach (FlagType state in States)
            {
                if (context.User.CharStates.Contains(state.FullType))
                    hasState = true;
            }
            if (hasState)
                status.StatusStates.GetWithDefault<MapCountDownState>().Counter = status.StatusStates.GetWithDefault<MapCountDownState>().Counter * 5;

            if (!MsgOverride.IsValid())
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.AddMapStatus(status));
            else
            {
                //message only if the status isn't already there
                MapStatus statusToCheck;
                if (!ZoneManager.Instance.CurrentMap.Status.TryGetValue(status.ID, out statusToCheck))
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(MsgOverride.ToLocal(), ownerChar.GetDisplayName(false)));
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.AddMapStatus(status, false));
            }
        }
    }


    /// <summary>
    /// Event that removes all the map statuses 
    /// </summary>
    [Serializable]
    public class RemoveWeatherEvent : BattleEvent
    {
        public override GameEvent Clone() { return new RemoveWeatherEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            //remove all other weather effects
            List<string> removingIDs = new List<string>();
            foreach (MapStatus removeStatus in ZoneManager.Instance.CurrentMap.Status.Values)
            {
                if (removeStatus.StatusStates.Contains<MapWeatherState>())
                    removingIDs.Add(removeStatus.ID);
            }
            foreach (string removeID in removingIDs)
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.RemoveMapStatus(removeID));
        }
    }

    /// <summary>
    /// Event that sets the map status depending on the user's type 
    /// </summary>
    [Serializable]
    public class TypeWeatherEvent : BattleEvent
    {
        /// <summary>
        /// The element that maps to a map status. 
        /// </summary>
        [JsonConverter(typeof(ElementMapStatusDictConverter))]
        [DataType(1, DataManager.DataType.Element, false)]
        [DataType(2, DataManager.DataType.MapStatus, false)]
        public Dictionary<string, string> WeatherPair;

        public TypeWeatherEvent() { WeatherPair = new Dictionary<string, string>(); }
        public TypeWeatherEvent(Dictionary<string, string> weather)
        {
            WeatherPair = weather;
        }
        protected TypeWeatherEvent(TypeWeatherEvent other)
            : this()
        {
            foreach (string element in other.WeatherPair.Keys)
                WeatherPair.Add(element, other.WeatherPair[element]);
        }
        public override GameEvent Clone() { return new TypeWeatherEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            string weather;
            if (WeatherPair.TryGetValue(context.User.Element1, out weather))
            {
                //add the map status
                MapStatus status = new MapStatus(weather);
                status.LoadFromData();
                status.StatusStates.GetWithDefault<MapCountDownState>().Counter = -1;
                ElementData elementData = DataManager.Instance.GetElement(context.User.Element1);
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_ELEMENT_WEATHER").ToLocal(), context.User.GetDisplayName(false), elementData.GetIconName(), ((MapStatusData)status.GetData()).GetColoredName()));
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.AddMapStatus(status));
            }
            else if (WeatherPair.TryGetValue(context.User.Element2, out weather))
            {
                //add the map status
                MapStatus status = new MapStatus(weather);
                status.LoadFromData();
                status.StatusStates.GetWithDefault<MapCountDownState>().Counter = -1;
                ElementData elementData = DataManager.Instance.GetElement(context.User.Element2);
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_ELEMENT_WEATHER").ToLocal(), context.User.GetDisplayName(false), elementData.GetIconName(), ((MapStatusData)status.GetData()).GetColoredName()));
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.AddMapStatus(status));
            }
            else//clear weather
            {
                //add the map status
                MapStatus status = new MapStatus(DataManager.Instance.DefaultMapStatus);
                status.LoadFromData();
                status.StatusStates.GetWithDefault<MapCountDownState>().Counter = -1;
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.AddMapStatus(status));
            }
        }
    }
    
    /// <summary>
    /// Event that bans the last move the character used by setting the move ID in the MapIDState
    /// </summary>
    [Serializable]
    public class BanMoveEvent : BattleEvent
    {
        /// <summary>
        /// The status that will store the move ID in MapIDState
        /// This should usually be "move_ban" 
        /// </summary>
        [JsonConverter(typeof(MapStatusConverter))]
        [DataType(0, DataManager.DataType.MapStatus, false)]
        public string BanStatusID;
        
        /// <summary>
        /// The status that contains the last used move in IDState status state
        /// This should usually be "last_used_move"
        /// </summary>
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string LastMoveStatusID;

        public BanMoveEvent() { BanStatusID = ""; LastMoveStatusID = ""; }
        public BanMoveEvent(string banStatusID, string prevMoveID)
        {
            BanStatusID = banStatusID;
            LastMoveStatusID = prevMoveID;
        }
        protected BanMoveEvent(BanMoveEvent other)
        {
            BanStatusID = other.BanStatusID;
            LastMoveStatusID = other.LastMoveStatusID;
        }
        public override GameEvent Clone() { return new BanMoveEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            StatusEffect testStatus = context.Target.GetStatusEffect(LastMoveStatusID);
            if (testStatus != null)
            {
                //add disable move based on the last move used
                string lockedMove = testStatus.StatusStates.GetWithDefault<IDState>().ID;
                //add the map status
                MapStatus status = new MapStatus(BanStatusID);
                status.LoadFromData();
                status.StatusStates.GetWithDefault<MapIDState>().ID = lockedMove;
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.AddMapStatus(status));
            }
            else
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_BAN_FAIL").ToLocal(), context.Target.GetDisplayName(false)));
        }
    }

    /// <summary>
    /// Event that makes the user learn the last used moved of the target
    /// </summary>
    [Serializable]
    public class SketchBattleEvent : BattleEvent
    {
        /// <summary>
        /// The status that contains the last used move in IDState status state
        /// This should usually be "last_used_move"
        /// </summary>
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, true)]
        public string LastMoveStatusID;

        public SketchBattleEvent() { LastMoveStatusID = ""; }
        public SketchBattleEvent(string prevMoveID)
        {
            LastMoveStatusID = prevMoveID;
        }
        protected SketchBattleEvent(SketchBattleEvent other)
        {
            LastMoveStatusID = other.LastMoveStatusID;
        }
        public override GameEvent Clone() { return new SketchBattleEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Target.Dead)
                yield break;

            if (String.IsNullOrEmpty(LastMoveStatusID))
            {
                bool learn = (context.ActionType == BattleActionType.Skill && context.UsageSlot > BattleContext.DEFAULT_ATTACK_SLOT && context.UsageSlot < CharData.MAX_SKILL_SLOTS && context.User.Skills[context.UsageSlot].BackRef > -1);
                for (int ii = CharData.MAX_SKILL_SLOTS-1; ii >= 0; ii--)
                    sketchMove(context, context.Target.BaseSkills[ii].SkillNum, ii, learn, true);
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_SKETCH").ToLocal(), context.User.GetDisplayName(false), context.Target.GetDisplayName(false)));
                yield break;
            }
            else
            {
                StatusEffect testStatus = context.Target.GetStatusEffect(LastMoveStatusID);
                if (testStatus != null && context.ActionType == BattleActionType.Skill && context.UsageSlot > BattleContext.DEFAULT_ATTACK_SLOT && context.UsageSlot < CharData.MAX_SKILL_SLOTS)
                {
                    sketchMove(context, testStatus.StatusStates.GetWithDefault<IDState>().ID, context.UsageSlot, context.User.Skills[context.UsageSlot].BackRef > -1, false);
                    yield break;
                }
            }

            DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_SKETCH_FAIL").ToLocal(), context.Target.GetDisplayName(false)));
        }

        private void sketchMove(BattleContext context, string moveIndex, int moveSlot, bool learn, bool group)
        {
            SkillData entry = null;

            if (!String.IsNullOrEmpty(moveIndex))
                entry = DataManager.Instance.GetSkill(moveIndex);

            if (!group)
            {
                foreach (BackReference<Skill> moveState in context.User.Skills)
                {
                    if (moveState.Element.SkillNum == moveIndex)
                    {
                        DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_ALREADY_HAS_SKILL").ToLocal(), context.User.GetDisplayName(false), entry.GetIconName()));
                        return;
                    }
                }
            }
            if (learn)
            {
                if (!String.IsNullOrEmpty(moveIndex))
                    context.User.ReplaceSkill(moveIndex, moveSlot, DataManager.Instance.Save.GetDefaultEnable(moveIndex));
                else
                    context.User.DeleteSkill(moveSlot);
            }
            else
                context.User.ChangeSkill(moveSlot, moveIndex, -1);
            if (!group)
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_SKETCH").ToLocal(), context.User.GetDisplayName(false), entry.GetIconName()));
        }
    }

    /// <summary>
    /// Event that makes the user learn the last used moved of the target until the next floor
    /// </summary>
    [Serializable]
    public class MimicBattleEvent : BattleEvent
    {
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string LastMoveStatusID;
        public int NewMoveCharges;

        public MimicBattleEvent() { LastMoveStatusID = ""; }
        public MimicBattleEvent(string prevMoveID, int newMoveCharges)
        {
            LastMoveStatusID = prevMoveID;
            NewMoveCharges = newMoveCharges;
        }
        protected MimicBattleEvent(MimicBattleEvent other)
        {
            NewMoveCharges = other.NewMoveCharges;
            LastMoveStatusID = other.LastMoveStatusID;
        }
        public override GameEvent Clone() { return new MimicBattleEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Target.Dead)
                yield break;

            StatusEffect testStatus = context.Target.GetStatusEffect(LastMoveStatusID);
            if (testStatus != null && context.ActionType == BattleActionType.Skill && context.UsageSlot > BattleContext.DEFAULT_ATTACK_SLOT && context.UsageSlot < CharData.MAX_SKILL_SLOTS)
            {
                string chosenMove = testStatus.StatusStates.GetWithDefault<IDState>().ID;

                SkillData entry = DataManager.Instance.GetSkill(chosenMove);

                foreach (BackReference<Skill> moveState in context.User.Skills)
                {
                    if (moveState.Element.SkillNum == chosenMove)
                    {
                        DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_ALREADY_HAS_SKILL").ToLocal(), context.User.GetDisplayName(false), entry.GetIconName()));
                        yield break;
                    }
                }
                context.User.ChangeSkill(context.UsageSlot, chosenMove, NewMoveCharges);
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_MIMIC").ToLocal(), context.User.GetDisplayName(false), entry.GetIconName()));
            }
            else
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_MIMIC_FAIL").ToLocal(), context.Target.GetDisplayName(false)));
        }
    }

    /// <summary>
    /// Event that restores the user's HP based on the damage the move dealt 
    /// </summary>
    [Serializable]
    public class HPDrainEvent : BattleEvent
    {
        /// <summary>
        /// The amount of HP restored calculated by damage dealt divided by this value
        /// </summary>
        public int DrainFraction;

        public HPDrainEvent() { }
        public HPDrainEvent(int drainFraction) { DrainFraction = drainFraction; }
        protected HPDrainEvent(HPDrainEvent other)
        {
            DrainFraction = other.DrainFraction;
        }
        public override GameEvent Clone() { return new HPDrainEvent(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int damageDone = context.GetContextStateInt<TotalDamageDealt>(true, 0);
            if (damageDone > 0)
            {
                TaintedDrain taintedDrain;
                if (context.GlobalContextStates.TryGet<TaintedDrain>(out taintedDrain))
                {
                    GameManager.Instance.BattleSE("DUN_Toxic");
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_LIQUID_OOZE").ToLocal(), context.User.GetDisplayName(false)));
                    yield return CoroutineManager.Instance.StartCoroutine(context.User.InflictDamage(Math.Max(1, damageDone * taintedDrain.Mult / DrainFraction)));
                }
                else
                    yield return CoroutineManager.Instance.StartCoroutine(context.User.RestoreHP(Math.Max(1, damageDone / DrainFraction)));
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

    public interface IHealEvent
    {
        int HPNum { get; }
        int HPDen { get; }
    }

    /// <summary>
    /// Event that heals the character based on the specified fraction of the character's max HP 
    /// Stat drops will heal the character instead
    /// </summary>
    [Serializable]
    public class RestoreHPEvent : BattleEvent, IHealEvent
    {
        /// <summary>
        /// The numerator of the fraction
        /// </summary>
        public int Numerator;
        
        /// <summary>
        /// The denominator of the fraction
        /// </summary>
        public int Denominator;
        
        /// <summary>
        /// Whether to affect the target or user 
        /// </summary>
        public bool AffectTarget;

        public int HPNum { get { return Numerator; } }
        public int HPDen { get { return Denominator; } }

        public RestoreHPEvent() { }
        public RestoreHPEvent(int numerator, int denominator, bool affectTarget) { Numerator = numerator; Denominator = denominator; AffectTarget = affectTarget; }
        protected RestoreHPEvent(RestoreHPEvent other)
        {
            Numerator = other.Numerator;
            Denominator = other.Denominator;
            AffectTarget = other.AffectTarget;
        }
        public override GameEvent Clone() { return new RestoreHPEvent(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = (AffectTarget ? context.Target : context.User);
            if (target.Dead)
                yield break;

            int dmg = target.MaxHP * Numerator / Denominator;
            yield return CoroutineManager.Instance.StartCoroutine(target.RestoreHP(dmg));
            context.ContextStates.Set(new DamageHealedTarget(dmg));
        }
    }


    /// <summary>
    /// Event that heals the character depending on the map status
    /// </summary>
    [Serializable]
    public class WeatherHPEvent : BattleEvent, IHealEvent
    {
        /// <summary>
        /// The map status mapped to a bool
        /// The bool indicates whether the heal will be boosted or reduced 
        /// </summary>
        [JsonConverter(typeof(MapStatusBoolDictConverter))]
        [DataType(1, DataManager.DataType.MapStatus, false)]
        public Dictionary<string, bool> WeatherPair;
        
        /// <summary>
        /// The numerator of the fractional heal
        /// </summary>
        public int HPDiv;

        public int HPNum { get { return HPDiv; } }
        public int HPDen { get { return 12; } }

        public WeatherHPEvent() { WeatherPair = new Dictionary<string, bool>(); }
        public WeatherHPEvent(int hpDiv, Dictionary<string, bool> weather)
        {
            HPDiv = hpDiv;
            WeatherPair = weather;
        }
        protected WeatherHPEvent(WeatherHPEvent other) : this()
        {
            HPDiv = other.HPDiv;
            foreach (string weather in other.WeatherPair.Keys)
                WeatherPair.Add(weather, other.WeatherPair[weather]);
        }
        public override GameEvent Clone() { return new WeatherHPEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int numerator = HPDiv;

            foreach (string weather in WeatherPair.Keys)
            {
                if (ZoneManager.Instance.CurrentMap.Status.ContainsKey(weather))
                {
                    if (WeatherPair[weather])
                        numerator *= 2;
                    else
                        numerator /= 2;
                    break;
                }
            }

            int dmg = context.Target.MaxHP * numerator / HPDen;
            yield return CoroutineManager.Instance.StartCoroutine(context.Target.RestoreHP(dmg));

            context.ContextStates.Set(new DamageHealedTarget(dmg));
        }
    }

    /// <summary>
    /// Event that subtracts PP from the target if the user is hit by a move
    /// </summary
    [Serializable]
    public class SpiteEvent : BattleEvent
    {
        /// <summary>
        /// The status that contains the last used move slot 
        /// This should usually be "last_used_move_slot"
        /// </summary>
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string LastSlotStatusID;
        
        /// <summary>
        /// The amount of PP to subtract
        /// </summary>
        public int PP;

        public SpiteEvent() { LastSlotStatusID = ""; }
        public SpiteEvent(string statusID, int pp) { LastSlotStatusID = statusID; PP = pp; }
        protected SpiteEvent(SpiteEvent other)
        {
            LastSlotStatusID = other.LastSlotStatusID;
            PP = other.PP;
        }
        public override GameEvent Clone() { return new SpiteEvent(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            StatusEffect status = context.Target.GetStatusEffect(LastSlotStatusID);
            if (status != null)
            {
                int slot = status.StatusStates.GetWithDefault<SlotState>().Slot;
                if (slot > -1 && slot < CharData.MAX_SKILL_SLOTS)
                    yield return CoroutineManager.Instance.StartCoroutine(context.Target.DeductCharges(slot, PP));
                else
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_NO_EFFECT").ToLocal(), context.Target.GetDisplayName(false)));
            }
            else
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_NO_EFFECT").ToLocal(), context.Target.GetDisplayName(false)));
        }
    }

    /// <summary>
    /// Event that restores PP on all move slots
    /// </summary
    [Serializable]
    public class RestorePPEvent : BattleEvent
    {
        /// <summary>
        /// The amount of PP to restore
        /// </summary>
        public int PP;

        public RestorePPEvent() { }
        public RestorePPEvent(int pp) { PP = pp; }
        protected RestorePPEvent(RestorePPEvent other)
        {
            PP = other.PP;
        }
        public override GameEvent Clone() { return new RestorePPEvent(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            yield return CoroutineManager.Instance.StartCoroutine(context.Target.RestoreCharges(PP));
        }
    }
    
    /// <summary>
    /// Event that restores or reduces the hunger of the character by the specified amount 
    /// </summary
    [Serializable]
    public class RestoreBellyEvent : BattleEvent
    {
        public const int MIN_MAX_FULLNESS = 50;
        public const int MAX_MAX_FULLNESS = 150;

        public List<BattleAnimEvent> BoostAnims;
        
        /// <summary>
        /// The amount of hunger to restore or reduce
        /// </summary>
        public int Heal;
        
        /// <summary>
        /// Whether to display the messages relating to hunger
        /// </summary>
        public bool Msg;
        
        /// <summary>
        /// The amount to increase or decrease the max hunger by
        /// </summary>
        public int AddMaxBelly;
        
        /// <summary>
        /// Whether full hunger is needed to add the max hunger amount
        /// </summary>
        public bool NeedFullBelly;

        public RestoreBellyEvent()
        {
            BoostAnims = new List<BattleAnimEvent>();
        }
        public RestoreBellyEvent(int heal, bool msg)
        {
            Heal = heal;
            Msg = msg;
            BoostAnims = new List<BattleAnimEvent>();
        }
        public RestoreBellyEvent(int heal, bool msg, int bellyPlus, bool needFull, params BattleAnimEvent[] boostAnims)
        {
            Heal = heal;
            Msg = msg;
            AddMaxBelly = bellyPlus;
            NeedFullBelly = needFull;
            BoostAnims = new List<BattleAnimEvent>();
            BoostAnims.AddRange(boostAnims);
        }
        protected RestoreBellyEvent(RestoreBellyEvent other)
        {
            Heal = other.Heal;
            Msg = other.Msg;
            AddMaxBelly = other.AddMaxBelly;
            NeedFullBelly = other.NeedFullBelly;
            BoostAnims = new List<BattleAnimEvent>();
            foreach (BattleAnimEvent anim in other.BoostAnims)
                BoostAnims.Add((BattleAnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new RestoreBellyEvent(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            bool fullBelly = (context.Target.Fullness == context.Target.MaxFullness);


            context.Target.Fullness += Heal;

            if (Heal < 0)
            {
                if (Msg)
                {
                    if (context.Target.Fullness <= 0)
                    {
                        if (context.Target.MemberTeam == DungeonScene.Instance.ActiveTeam)
                            DungeonScene.Instance.LogMsg(Text.FormatKey("MSG_HUNGER_EMPTY", context.Target.GetDisplayName(true)));
                        else
                            DungeonScene.Instance.LogMsg(Text.FormatKey("MSG_HUNGER_EMPTY_FOE", context.Target.GetDisplayName(true)));
                    }
                    else
                        DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_HUNGER_DROP").ToLocal(), context.Target.GetDisplayName(false)));
                }
                GameManager.Instance.BattleSE("DUN_Hunger");
            }
            else
            {
                if (Msg)
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_HUNGER_FILL").ToLocal(), context.Target.GetDisplayName(false)));
            }

            if (AddMaxBelly != 0 && (fullBelly || !NeedFullBelly))
            {
                if (Msg)
                {
                    if (AddMaxBelly < 0)
                        DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_MAX_HUNGER_DROP").ToLocal(), context.Target.GetDisplayName(false)));
                    else
                        DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_MAX_HUNGER_BOOST").ToLocal(), context.Target.GetDisplayName(false)));


                    foreach (BattleAnimEvent anim in BoostAnims)
                        yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));
                }
                context.Target.MaxFullness += AddMaxBelly;
                if (context.Target.MaxFullness < MIN_MAX_FULLNESS)
                    context.Target.MaxFullness = MIN_MAX_FULLNESS;
                if (context.Target.MaxFullness > MAX_MAX_FULLNESS)
                    context.Target.MaxFullness = MAX_MAX_FULLNESS;
            }

            if (context.Target.Fullness < 0)
                context.Target.Fullness = 0;
            if (context.Target.Fullness >= context.Target.MaxFullness)
            {
                context.Target.Fullness = context.Target.MaxFullness;
                context.Target.FullnessRemainder = 0;
            }

            yield break;
        }
    }
    
    
    /// <summary>
    /// Event that removes the specified status
    /// </summary
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
    /// Event that removes its status from the user 
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
            yield return CoroutineManager.Instance.StartCoroutine(context.User.RemoveStatusEffect(((StatusEffect)owner).ID, ShowMessage));
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
    /// Event that recoil damage to the user based on how much damage was dealt
    /// </summary>
    [Serializable]
    public class DamageRecoilEvent : RecoilEvent
    {
        /// <summary>
        /// The value dividing the total damage dealt representing the recoil damage
        /// </summary>
        public int Fraction;

        public DamageRecoilEvent() { }
        public DamageRecoilEvent(int damageFraction) { Fraction = damageFraction; }
        protected DamageRecoilEvent(DamageRecoilEvent other)
        {
            Fraction = other.Fraction;
        }
        public override GameEvent Clone() { return new DamageRecoilEvent(this); }

        protected override int GetRecoilDamage(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int damageDone = context.GetContextStateInt<TotalDamageDealt>(true, 0);
            return Math.Max(1, damageDone / Fraction);
        }
    }
    
    /// <summary>
    /// Event that deals recoil damage to the user if the move landed
    /// </summary>
    [Serializable]
    public class HPRecoilEvent : RecoilEvent
    {
        
        /// <summary>
        /// The value dividing the user's HP representing the recoil damage
        /// </summary>
        public int Fraction;
        
        /// <summary>
        /// Whether to use the user's max HP or current HP
        /// </summary>
        public bool MaxHP;

        public HPRecoilEvent() { }
        public HPRecoilEvent(int fraction, bool maxHP) { Fraction = fraction; MaxHP = maxHP; }
        protected HPRecoilEvent(HPRecoilEvent other)
        {
            Fraction = other.Fraction;
            MaxHP = other.MaxHP;
        }
        public override GameEvent Clone() { return new HPRecoilEvent(this); }

        protected override int GetRecoilDamage(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (MaxHP)
                return Math.Max(1, context.User.MaxHP / Fraction);
            else
                return Math.Max(1, context.User.HP / Fraction);
        }
    }


    [Serializable]
    public abstract class RecoilEvent : BattleEvent
    {
        public RecoilEvent() { }

        protected abstract int GetRecoilDamage(GameEventOwner owner, Character ownerChar, BattleContext context);

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int damageDone = context.GetContextStateInt<TotalDamageDealt>(true, 0);
            if (damageDone > 0)
            {
                if (!context.User.CharStates.Contains<NoRecoilState>() && !context.User.CharStates.Contains<MagicGuardState>())
                {
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_HIT_RECOIL").ToLocal(), context.User.GetDisplayName(false)));

                    GameManager.Instance.BattleSE("DUN_Hit_Neutral");
                    if (!context.User.Unidentifiable)
                    {
                        SingleEmitter endEmitter = new SingleEmitter(new AnimData("Hit_Neutral", 3));
                        endEmitter.SetupEmit(context.User.MapLoc, context.User.MapLoc, context.User.CharDir);
                        DungeonScene.Instance.CreateAnim(endEmitter, DrawLayer.NoDraw);
                    }

                    int recoil = GetRecoilDamage(owner, ownerChar, context);
                    yield return CoroutineManager.Instance.StartCoroutine(context.User.InflictDamage(recoil));
                }
            }
        }
    }

    /// <summary>
    /// Event that deals recoil damage to the user if the move missed
    /// </summary>
    [Serializable]
    public class CrashLandEvent : BattleEvent
    {
            
        /// <summary>
        /// The value dividing the user's max HP representing the recoil damage
        /// </summary>
        public int HPFraction;

        public CrashLandEvent() { }
        public CrashLandEvent(int damageFraction) { HPFraction = damageFraction; }
        protected CrashLandEvent(CrashLandEvent other)
        {
            HPFraction = other.HPFraction;
        }
        public override GameEvent Clone() { return new CrashLandEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.GetContextStateInt<AttackHitTotal>(true, 0) == 0)
            {
                if (!context.User.CharStates.Contains<NoRecoilState>() && !context.User.CharStates.Contains<MagicGuardState>())
                {
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_HIT_CRASH").ToLocal(), context.User.GetDisplayName(false)));
                    yield return CoroutineManager.Instance.StartCoroutine(context.User.InflictDamage(Math.Max(1, context.User.MaxHP / HPFraction)));
                }
            }
        }
    }

    /// <summary>
    /// Event that deals damage based on the specified fraction of the character's max HP 
    /// </summary>
    [Serializable]
    public class ChipDamageEvent : BattleEvent
    {
        
        /// <summary>
        /// The value dividing the character's max HP
        /// </summary>
        public int HPFraction;
        
        /// <summary>
        /// The message displayed in the dungeon log 
        /// </summary>
        [StringKey(0, true)]
        public StringKey Msg;
        
        /// <summary>
        /// Whether to play the VFX associated with this event
        /// </summary>
        public bool VFX;
        
        /// <summary>
        /// Whether to the skip the damage animation  
        /// </summary>
        public bool SkipAction;

        public ChipDamageEvent() { }
        public ChipDamageEvent(int hpFraction) { HPFraction = hpFraction; }
        public ChipDamageEvent(int hpFraction, StringKey msg) { HPFraction = hpFraction; Msg = msg; }
        public ChipDamageEvent(int hpFraction, StringKey msg, bool vfx, bool skipAction) { HPFraction = hpFraction; Msg = msg; VFX = vfx; SkipAction = skipAction; }
        protected ChipDamageEvent(ChipDamageEvent other)
        {
            HPFraction = other.HPFraction;
            Msg = other.Msg;
            VFX = other.VFX;
            SkipAction = other.SkipAction;
        }
        public override GameEvent Clone() { return new ChipDamageEvent(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.User.Dead)
                yield break;
            if (!context.User.CharStates.Contains<MagicGuardState>())
            {
                if (Msg.IsValid())
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(Msg.ToLocal(), context.User.GetDisplayName(false), owner.GetDisplayName(), ownerChar.GetDisplayName(false)));
                if (VFX)
                {
                    GameManager.Instance.BattleSE("DUN_Hit_Neutral");
                    if (!context.User.Unidentifiable)
                    {
                        SingleEmitter endEmitter = new SingleEmitter(new AnimData("Hit_Neutral", 3));
                        endEmitter.SetupEmit(context.User.MapLoc, context.User.MapLoc, context.User.CharDir);
                        DungeonScene.Instance.CreateAnim(endEmitter, DrawLayer.NoDraw);
                    }
                }
                int recoil = Math.Max(1, context.User.MaxHP / HPFraction);
                yield return CoroutineManager.Instance.StartCoroutine(context.User.InflictDamage(recoil, !SkipAction));
            }
        }
    }

    /// <summary>
    /// Event that deals damage based on the specified fraction of the character's max HP
    /// This event should only be used on trap tiles
    /// </summary>
    [Serializable]
    public class IndirectDamageEvent : BattleEvent
    {
        
        /// <summary>
        /// The value dividing the character's max HP
        /// </summary>
        public int HPFraction;

        public IndirectDamageEvent() { }
        public IndirectDamageEvent(int hpFraction) { HPFraction = hpFraction; }
        protected IndirectDamageEvent(IndirectDamageEvent other)
        {
            HPFraction = other.HPFraction;
        }
        public override GameEvent Clone() { return new IndirectDamageEvent(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (!context.Target.CharStates.Contains<MagicGuardState>())
            {
                GameManager.Instance.BattleSE("DUN_Hit_Neutral");
                if (!context.Target.Unidentifiable)
                {
                    SingleEmitter endEmitter = new SingleEmitter(new AnimData("Hit_Neutral", 3));
                    endEmitter.SetupEmit(context.Target.MapLoc, context.Target.MapLoc, context.Target.CharDir);
                    DungeonScene.Instance.CreateAnim(endEmitter, DrawLayer.NoDraw);
                }

                int dmg = Math.Max(1, context.Target.MaxHP / HPFraction);
                yield return CoroutineManager.Instance.StartCoroutine(context.Target.InflictDamage(dmg));
            }
        }
    }

    /// <summary>
    /// Event that deals damage based on the specified fraction of the character's max HP and the type effectiveness
    /// This event should only be used on trap tiles
    /// </summary>
    [Serializable]
    public class IndirectElementDamageEvent : BattleEvent
    {
        /// <summary>
        /// The matchup type
        /// </summary>
        [JsonConverter(typeof(ElementConverter))]
        [DataType(0, DataManager.DataType.Element, false)]
        public string Element;
        
        /// <summary>
        /// The value dividing the character's max HP
        /// </summary>
        public int HPFraction;

        public IndirectElementDamageEvent() { Element = ""; }
        public IndirectElementDamageEvent(string element, int hpFraction)
        {
            Element = element;
            HPFraction = hpFraction;
        }
        protected IndirectElementDamageEvent(IndirectElementDamageEvent other)
        {
            Element = other.Element;
            HPFraction = other.HPFraction;
        }
        public override GameEvent Clone() { return new IndirectElementDamageEvent(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (!context.Target.CharStates.Contains<MagicGuardState>())
            {
                int typeMatchup = PreTypeEvent.GetDualEffectiveness(null, context.Target, Element);
                int effectiveness = PreTypeEvent.GetEffectivenessMult(typeMatchup);
                if (effectiveness > 0)
                {
                    GameManager.Instance.BattleSE("DUN_Hit_Neutral");
                    if (!context.Target.Unidentifiable)
                    {
                        SingleEmitter endEmitter = new SingleEmitter(new AnimData("Hit_Neutral", 3));
                        endEmitter.SetupEmit(context.Target.MapLoc, context.Target.MapLoc, context.Target.CharDir);
                        DungeonScene.Instance.CreateAnim(endEmitter, DrawLayer.NoDraw);
                    }

                    int dmg = Math.Max(1, context.Target.MaxHP / HPFraction * effectiveness / 4);
                    yield return CoroutineManager.Instance.StartCoroutine(context.Target.InflictDamage(dmg));
                }
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
    /// Event the sets the character's HP to 1 
    /// </summary>
    [Serializable]
    public class HPTo1Event : BattleEvent
    {
        /// <summary>
        /// Whether to affect the target or user
        /// </summary>
        public bool AffectTarget;

        public HPTo1Event() { }
        public HPTo1Event(bool affectTarget) { AffectTarget = affectTarget; }
        protected HPTo1Event(HPTo1Event other)
        {
            AffectTarget = other.AffectTarget;
        }
        public override GameEvent Clone() { return new HPTo1Event(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = (AffectTarget ? context.Target : context.User);
            if (target.Dead)
                yield break;

            target.HP = 1;
            DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_HP_TO_ONE").ToLocal(), target.GetDisplayName(false)));
        }
    }

    /// <summary>
    /// Event the sets the PP of all the character's move to 1 
    /// </summary>
    [Serializable]
    public class PPTo1Event : BattleEvent
    {
        /// <summary>
        /// Whether to affect the target or user
        /// </summary>
        public bool AffectTarget;

        public PPTo1Event() { }
        public PPTo1Event(bool affectTarget) { AffectTarget = affectTarget; }
        protected PPTo1Event(PPTo1Event other)
        {
            AffectTarget = other.AffectTarget;
        }
        public override GameEvent Clone() { return new PPTo1Event(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = (AffectTarget ? context.Target : context.User);
            if (target.Dead)
                yield break;

            for (int ii = 0; ii < target.Skills.Count; ii++)
            {
                if (!String.IsNullOrEmpty(target.Skills[ii].Element.SkillNum))
                    target.SetSkillCharges(ii, 1);
            }

            DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_PP_TO_ONE").ToLocal(), target.GetDisplayName(false)));
        }
    }

    /// <summary>
    /// Event that makes the user hop by the specified distance
    /// </summary>
    [Serializable]
    public class HopEvent : BattleEvent
    {
        /// <summary>
        /// The total distance to hop
        /// </summary>
        public int Distance;
        
        /// <summary>
        /// Whether to hop forwards or backwards
        /// </summary>
        public bool Reverse;

        public HopEvent() { }
        public HopEvent(int distance, bool reverse)
        {
            Distance = distance; Reverse = reverse;
        }
        protected HopEvent(HopEvent other)
        {
            Distance = other.Distance;
            Reverse = other.Reverse;
        }
        public override GameEvent Clone() { return new HopEvent(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.User.Dead)
                yield break;
            //jump back a number of spaces
            if (context.User.CharStates.Contains<AnchorState>())
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_ANCHORED").ToLocal(), context.User.GetDisplayName(false)));
            else
            {
                Dir8 hopDir = (Reverse ? context.User.CharDir.Reverse() : context.User.CharDir);
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.JumpTo(context.User, hopDir, Distance));
            }
        }
    }

    /// <summary>
    /// Event that transport the user and nearby allies to the tile directly in front of another character or wall
    /// </summary>
    [Serializable]
    public class PounceEvent : BattleEvent
    {
        /// <summary>
        /// The radius that allies must be within in order to pounce
        /// </summary>
        public int AllyRadius;
        public PounceEvent()
        { }
        public PounceEvent(int allyRadius)
        {
            AllyRadius = allyRadius;
        }
        public PounceEvent(PounceEvent other)
        {
            AllyRadius = other.AllyRadius;
        }
        public override GameEvent Clone() { return new PounceEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = context.User;
            if (target == null || target.Dead)
                yield break;

            List<Character> allies = new List<Character>();
            //take count of allies
            if (AllyRadius > 0)
            {
                foreach (Character character in ZoneManager.Instance.CurrentMap.GetCharsInFillRect(target.CharLoc, Rect.FromPointRadius(target.CharLoc, AllyRadius)))
                {
                    if (!character.Dead && DungeonScene.Instance.GetMatchup(character, target) == Alignment.Friend)
                        allies.Add(character);
                }
            }


            yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.Pounce(target, context.User.CharDir, context.StrikeStartTile, (context.StrikeStartTile - context.TargetTile).Dist8()));

            //place the allies
            foreach (Character ally in allies)
            {
                if (ally.CharStates.Contains<AnchorState>())
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_ANCHORED").ToLocal(), ally.GetDisplayName(false)));
                else
                    yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.WarpNear(ally, context.User.CharLoc));
            }
        }
    }

    /// <summary>
    /// Event that makes the target warp in front of the user
    /// </summary>
    [Serializable]
    public class LureEvent : BattleEvent
    {
        public override GameEvent Clone() { return new LureEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = context.Target;
            if (target == null || target.Dead)
                yield break;

            //knock back a number of spaces
            if (target.CharStates.Contains<AnchorState>())
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_ANCHORED").ToLocal(), target.GetDisplayName(false)));
            else
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.WarpNear(target, context.User.CharLoc + context.User.CharDir.GetLoc()));
        }
    }

    /// <summary>
    /// Event that knocks the target back by the specified distance
    /// </summary>
    [Serializable]
    public class KnockBackEvent : BattleEvent
    {
        /// <summary>
        /// The distance to knock back
        /// </summary>
        public int Distance;

        public KnockBackEvent() { }
        public KnockBackEvent(int distance) { Distance = distance; }
        protected KnockBackEvent(KnockBackEvent other)
        {
            Distance = other.Distance;
        }
        public override GameEvent Clone() { return new KnockBackEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Target.Dead)
                yield break;

            //knock back a number of spaces
            if (context.Target.CharStates.Contains<AnchorState>())
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_ANCHORED").ToLocal(), context.Target.GetDisplayName(false)));
            else
            {
                Dir8 dir = ZoneManager.Instance.CurrentMap.GetClosestDir8(context.User.CharLoc, context.Target.CharLoc);
                if (dir == Dir8.None)
                    dir = context.User.CharDir.Reverse();
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.KnockBack(context.Target, dir, Distance));
            }
        }
    }

    /// <summary>
    /// Event that throws the target backwards by the specified distance 
    /// </summary>
    [Serializable]
    public class ThrowBackEvent : BattleEvent
    {
        /// <summary>
        /// The distance to throw the target back
        /// </summary>
        public int Distance;
        
        /// <summary>
        /// The event calculating how much damage the target will take
        /// </summary>
        public CalculatedDamageEvent HitEvent;

        public ThrowBackEvent() { }
        public ThrowBackEvent(int distance, CalculatedDamageEvent hitEvent) { Distance = distance; HitEvent = hitEvent; }
        protected ThrowBackEvent(ThrowBackEvent other)
        {
            Distance = other.Distance;
            HitEvent = (CalculatedDamageEvent)other.HitEvent.Clone();
        }
        public override GameEvent Clone() { return new ThrowBackEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Target.Dead)
                yield break;

            //knock back a number of spaces
            if (context.Target.CharStates.Contains<AnchorState>())
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_ANCHORED").ToLocal(), context.Target.GetDisplayName(false)));
            else
            {
                int damage = HitEvent.CalculateDamage(owner, context);
                ThrowTargetContext throwContext = new ThrowTargetContext(damage);
                Dir8 dir = ZoneManager.Instance.CurrentMap.GetClosestDir8(context.User.CharLoc, context.Target.CharLoc);
                if (dir == Dir8.None)
                    dir = context.User.CharDir.Reverse();
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.ThrowTo(context.Target, context.User,
                    dir, Distance, Alignment.Foe, throwContext.Hit));
            }
        }

        private class ThrowTargetContext
        {
            /// <summary>
            /// The total damage the target will take
            /// </summary>
            public int Damage;
            public ThrowTargetContext(int damage)
            {
                Damage = damage;
            }

            public IEnumerator<YieldInstruction> Hit(Character targetChar, Character attacker)
            {
                GameManager.Instance.BattleSE("DUN_Hit_Neutral");
                if (!targetChar.Unidentifiable)
                {
                    SingleEmitter endEmitter = new SingleEmitter(new AnimData("Hit_Neutral", 3));
                    endEmitter.SetupEmit(targetChar.MapLoc, attacker.MapLoc, targetChar.CharDir);
                    DungeonScene.Instance.CreateAnim(endEmitter, DrawLayer.NoDraw);
                }

                yield return CoroutineManager.Instance.StartCoroutine(targetChar.InflictDamage(Damage, true));
            }

        }

    }

    /// <summary>
    /// Event that knocks back all characters within 1-tile away by the specified distance
    /// </summary>
    [Serializable]
    public class LaunchAllEvent : BattleEvent
    {
        
        /// <summary>
        /// The distance to knock back
        /// </summary>
        public int Distance;

        public LaunchAllEvent() { }
        public LaunchAllEvent(int distance) { Distance = distance; }
        protected LaunchAllEvent(LaunchAllEvent other)
        {
            Distance = other.Distance;
        }
        public override GameEvent Clone() { return new LaunchAllEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Dir8 moveDir = context.User.CharDir;
            yield return CoroutineManager.Instance.StartCoroutine(launchTile(context.TargetTile, moveDir, Dir8.Down));
            yield return CoroutineManager.Instance.StartCoroutine(launchTile(context.TargetTile, moveDir, Dir8.DownLeft));
            yield return CoroutineManager.Instance.StartCoroutine(launchTile(context.TargetTile, moveDir, Dir8.DownRight));
            yield return CoroutineManager.Instance.StartCoroutine(launchTile(context.TargetTile, moveDir, Dir8.Left));
            yield return CoroutineManager.Instance.StartCoroutine(launchTile(context.TargetTile, moveDir, Dir8.None));
            yield return CoroutineManager.Instance.StartCoroutine(launchTile(context.TargetTile, moveDir, Dir8.Right));
            yield return CoroutineManager.Instance.StartCoroutine(launchTile(context.TargetTile, moveDir, Dir8.UpLeft));
            yield return CoroutineManager.Instance.StartCoroutine(launchTile(context.TargetTile, moveDir, Dir8.UpRight));
            yield return CoroutineManager.Instance.StartCoroutine(launchTile(context.TargetTile, moveDir, Dir8.Up));
        }

        private IEnumerator<YieldInstruction> launchTile(Loc loc, Dir8 dir, Dir8 offsetDir)
        {
            if (offsetDir != Dir8.None)
                loc = loc + DirExt.AddAngles(dir, offsetDir).GetLoc();
            Character target = ZoneManager.Instance.CurrentMap.GetCharAtLoc(loc);
            if (target != null)
            {
                //knock back a number of spaces
                if (target.CharStates.Contains<AnchorState>())
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_ANCHORED").ToLocal(), target.GetDisplayName(false)));
                else
                    yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.KnockBack(target, dir, Distance));
            }
        }
    }
    
    /// <summary>
    /// Event that warps a character and nearby allies to a random location within the specified distance
    /// </summary>
    [Serializable]
    public class RandomGroupWarpEvent : BattleEvent
    {
        /// <summary>
        /// The max warp distance 
        /// </summary>
        public int Distance;
        
        /// <summary>
        /// Whether to affect the target or user
        /// </summary>
        public bool AffectTarget;

        public RandomGroupWarpEvent() { }
        public RandomGroupWarpEvent(int distance, bool affectTarget)
        {
            Distance = distance;
            AffectTarget = affectTarget;
        }
        protected RandomGroupWarpEvent(RandomGroupWarpEvent other)
        {
            Distance = other.Distance;
            AffectTarget = other.AffectTarget;
        }
        public override GameEvent Clone() { return new RandomGroupWarpEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = (AffectTarget ? context.Target : context.User);
            if (target.Dead)
                yield break;

            if (target.CharStates.Contains<AnchorState>())
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_ANCHORED").ToLocal(), target.GetDisplayName(false)));
            else
            {
                //warp within the space
                Loc startLoc = target.CharLoc;
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.RandomWarp(target, Distance));
                foreach (Character character in ZoneManager.Instance.CurrentMap.GetCharsInFillRect(startLoc, Rect.FromPointRadius(startLoc, 1)))
                {
                    if (!character.Dead && DungeonScene.Instance.GetMatchup(character, target) == Alignment.Friend)
                    {
                        if (character.CharStates.Contains<AnchorState>())
                            DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_ANCHORED").ToLocal(), character.GetDisplayName(false)));
                        else
                            yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.WarpNear(character, target.CharLoc));
                    }
                }
            }
        }
    }

    /// <summary>
    /// Event that warps a character to a random location within the specified distance
    /// </summary>
    [Serializable]
    public class RandomWarpEvent : BattleEvent
    {
        
        /// <summary>
        /// The max warp distance 
        /// </summary>
        public int Distance;
        
        /// <summary>
        /// Whether to affect the target or user
        /// </summary>
        public bool AffectTarget;
        
        /// <summary>
        /// The message displayed in the dungeon log 
        /// </summary>
        [StringKey(0, true)]
        public StringKey TriggerMsg;

        public RandomWarpEvent() { }
        public RandomWarpEvent(int distance, bool affectTarget)
        {
            Distance = distance;
            AffectTarget = affectTarget;
        }
        public RandomWarpEvent(int distance, bool affectTarget, StringKey triggerMsg)
        {
            Distance = distance;
            AffectTarget = affectTarget;
            TriggerMsg = triggerMsg;
        }
        protected RandomWarpEvent(RandomWarpEvent other)
        {
            Distance = other.Distance;
            AffectTarget = other.AffectTarget;
        }
        public override GameEvent Clone() { return new RandomWarpEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = (AffectTarget ? context.Target : context.User);
            if (target.Dead)
                yield break;
            //warp within the space
            if (target.CharStates.Contains<AnchorState>())
            {
                if (!TriggerMsg.IsValid())
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_ANCHORED").ToLocal(), target.GetDisplayName(false)));
            }
            else
            {
                if (TriggerMsg.IsValid())
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(TriggerMsg.ToLocal(), ownerChar.GetDisplayName(false), owner.GetDisplayName()));

                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.RandomWarp(target, Distance));
            }
        }
    }

    /// <summary>
    /// Event that warps the character nearby the stairs
    /// </summary>
    [Serializable]
    public class WarpToEndEvent : BattleEvent
    {
        /// <summary>
        /// The max warp distance to check for the end point
        /// </summary>
        public int Distance;
        
        /// <summary>
        /// The max distance away the character will be from the end point
        /// </summary>
        public int DiffRange;
        
        
        public bool AffectTarget;

        
        public WarpToEndEvent() { }
        public WarpToEndEvent(int distance, int diff, bool affectTarget)
        {
            Distance = distance;
            DiffRange = diff;
            AffectTarget = affectTarget;
        }
        protected WarpToEndEvent(WarpToEndEvent other)
        {
            Distance = other.Distance;
            DiffRange = other.DiffRange;
            AffectTarget = other.AffectTarget;
        }
        public override GameEvent Clone() { return new WarpToEndEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = (AffectTarget ? context.Target : context.User);
            if (target.Dead)
                yield break;
            //warp within the space
            if (target.CharStates.Contains<AnchorState>())
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_ANCHORED").ToLocal(), target.GetDisplayName(false)));
            else
                yield return CoroutineManager.Instance.StartCoroutine(WarpToEnd(target, Distance, DiffRange));
        }


        public static List<Loc> FindExits()
        {
            List<Loc> exits = new List<Loc>();
            for (int xx = 0; xx < ZoneManager.Instance.CurrentMap.Width; xx++)
            {
                for (int yy = 0; yy < ZoneManager.Instance.CurrentMap.Height; yy++)
                {
                    Tile tile = ZoneManager.Instance.CurrentMap.Tiles[xx][yy];

                    if (tile.Effect.ID == "stairs_go_up" || tile.Effect.ID == "stairs_go_down")//TODO: remove this magic number
                        exits.Add(new Loc(xx, yy));
                }
            }
            return exits;
        }

        public static IEnumerator<YieldInstruction> WarpToEnd(Character character, int radius, int diffRange, bool msg = true)
        {
            List<Character> characters = new List<Character>();

            Loc? loc = Grid.FindClosestConnectedTile(character.CharLoc - new Loc(radius), new Loc(radius * 2 + 1),
                (Loc testLoc) =>
                {

                    Tile tile = ZoneManager.Instance.CurrentMap.GetTile(testLoc);
                    if (tile == null)
                        return false;

                    if (tile.Effect.ID == "stairs_go_up" || tile.Effect.ID == "stairs_go_down")//TODO: remove this magic number
                        return true;
                    return false;
                },
                (Loc testLoc) =>
                {
                    return ZoneManager.Instance.CurrentMap.TileBlocked(testLoc, true);
                },
                (Loc testLoc) =>
                {
                    return ZoneManager.Instance.CurrentMap.TileBlocked(testLoc, true, true);
                },
                character.CharLoc);

            if (!loc.HasValue)
            {
                if (msg)
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_NO_EXIT").ToLocal()));
            }
            else
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.WarpNear(character, loc.Value, diffRange, msg));
        }
    }

    /// <summary>
    /// Event that warps the user nearby the target
    /// </summary>
    [Serializable]
    public class WarpHereEvent : BattleEvent
    {
        /// <summary>
        /// The message displayed in the dungeon log 
        /// </summary>
        [StringKey(0, true)]
        public StringKey Msg;
        
        /// <summary>
        /// Whether to warp the target nearby the user
        /// </summary>
        public bool AffectTarget;

        public WarpHereEvent() { }
        public WarpHereEvent(StringKey msg, bool affectTarget)
        {
            Msg = msg;
            AffectTarget = affectTarget;
        }
        protected WarpHereEvent(WarpHereEvent other)
        {
            Msg = other.Msg;
            AffectTarget = other.AffectTarget;
        }
        public override GameEvent Clone() { return new WarpHereEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = (AffectTarget ? context.Target : context.User);
            Character origin = (AffectTarget ? context.User : context.Target);

            if (target.CharStates.Contains<AnchorState>())
                yield break;


            if (Msg.IsValid())
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(Msg.ToLocal(), origin.GetDisplayName(false), target.GetDisplayName(false)));
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.WarpNear(target, origin.CharLoc, false));
            }
            else
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.WarpNear(target, origin.CharLoc, true));
        }
    }

    /// <summary>
    /// Event that warps the character to one of its nearby allies
    /// </summary>
    [Serializable]
    public class WarpToAllyEvent : BattleEvent
    {
        public WarpToAllyEvent() { }
        public override GameEvent Clone() { return new WarpToAllyEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Target.CharStates.Contains<AnchorState>())
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_ANCHORED").ToLocal(), context.Target.GetDisplayName(false)));
            else
            {
                foreach (Character character in context.Target.MemberTeam.Players)
                {
                    if (character != context.Target)
                    {
                        //found a target
                        //are we already next to them?
                        if (ZoneManager.Instance.CurrentMap.InRange(character.CharLoc, context.Target.CharLoc, 1))
                            break;
                        for (int ii = 0; ii < DirRemap.FOCUSED_DIR8.Length; ii++)
                        {
                            //always warp behind the target
                            Dir8 dir = DirExt.AddAngles(DirRemap.FOCUSED_DIR8[ii], DirExt.AddAngles(character.CharDir, Dir8.Up));
                            if (!ZoneManager.Instance.CurrentMap.DirBlocked(dir, character.CharLoc, context.Target.Mobility))
                            {
                                Loc targetLoc = character.CharLoc + dir.GetLoc();
                                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.PointWarp(context.Target, targetLoc, false));
                                yield break;
                            }
                        }
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Event that warps allies to the user that are within the specified distance 
    /// </summary>
    [Serializable]
    public class WarpAlliesInEvent : BattleEvent
    {
        
        /// <summary>
        /// The max distance that allies can be summoned from
        /// </summary>
        public int Distance;
        
        /// <summary>
        /// The max amount of allies to summon
        /// </summary>
        public int Amount;
        
        /// <summary>
        /// Whether to warp the furthest allies
        /// </summary>
        public bool FarthestFirst;
        
        /// <summary>
        /// Whether to print a fail message if no allies are warped
        /// </summary>
        public bool SilentFail;
        
        /// <summary>
        /// The message displayed in the dungeon log if an ally was warped
        /// </summary>
        public StringKey Msg;

        public WarpAlliesInEvent() { }
        public WarpAlliesInEvent(int distance, int allies, bool farthestFirst, StringKey msg, bool silentFail)
        {
            Distance = distance;
            Amount = allies;
            FarthestFirst = farthestFirst;
            Msg = msg;
            SilentFail = silentFail;
        }
        protected WarpAlliesInEvent(WarpAlliesInEvent other)
        {
            Distance = other.Distance;
            Amount = other.Amount;
            FarthestFirst = other.FarthestFirst;
            Msg = other.Msg;
            SilentFail = other.SilentFail;
        }
        public override GameEvent Clone() { return new WarpAlliesInEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            StablePriorityQueue<int, Character> targets = new StablePriorityQueue<int, Character>();
            foreach (Character character in ZoneManager.Instance.CurrentMap.GetCharsInFillRect(context.Target.CharLoc, Rect.FromPointRadius(context.Target.CharLoc, Distance)))
            {
                if (!character.Dead && DungeonScene.Instance.GetMatchup(character, context.Target) == Alignment.Friend)
                    targets.Enqueue((FarthestFirst ? -1 : 1) * (character.CharLoc - context.Target.CharLoc).DistSquared(), character);
            }
            int totalWarp = 0;
            for (int ii = 0; ii < Amount && targets.Count > 0; ii++)
            {
                Character target = targets.Dequeue();
                if (target.CharStates.Contains<AnchorState>())
                    yield break;

                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.WarpNear(target, context.Target.CharLoc, false));
                if (target.MemberTeam.MapFaction != Faction.Player)
                    target.TurnUsed = true;
                totalWarp++;
            }
            if (totalWarp > 0)
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(Msg.ToLocal(), context.Target.GetDisplayName(false)));
            else if (!SilentFail)
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_NOTHING_HAPPENED").ToLocal()));
        }
    }

    /// <summary>
    /// Event that warps enemies to the user that are within the specified distance 
    /// </summary>
    [Serializable]
    public class WarpFoesToTileEvent : BattleEvent
    {
        
        /// <summary>
        /// The max amount of allies to summon
        /// </summary>
        public int Amount;
        
        /// <summary>
        /// The max distance that enemies can be summoned from
        /// </summary>
        public int Distance;

        public WarpFoesToTileEvent() { }
        public WarpFoesToTileEvent(int distance, int foes) { Distance = distance; Amount = foes; }
        protected WarpFoesToTileEvent(WarpFoesToTileEvent other) { Distance = other.Distance; Amount = other.Amount; }
        public override GameEvent Clone() { return new WarpFoesToTileEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            StablePriorityQueue<int, Character> targets = new StablePriorityQueue<int, Character>();
            foreach (Character character in ZoneManager.Instance.CurrentMap.GetCharsInFillRect(context.User.CharLoc, Rect.FromPointRadius(context.User.CharLoc, Distance)))
            {
                if (!character.Dead && DungeonScene.Instance.GetMatchup(character, context.User) == Alignment.Foe)
                    targets.Enqueue(-(character.CharLoc - context.TargetTile).DistSquared(), character);
            }
            int totalWarp = 0;
            for (int ii = 0; ii < Amount && targets.Count > 0; ii++)
            {
                Character target = targets.Dequeue();
                if (target.CharStates.Contains<AnchorState>())
                    yield break;

                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.WarpNear(target, context.TargetTile, false));
                if (target.MemberTeam.MapFaction != Faction.Player)
                    target.TurnUsed = true;
                totalWarp++;
            }
            if (totalWarp == 0)
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_NOTHING_HAPPENED").ToLocal()));
            else
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_SUMMON_FOES").ToLocal(), context.User.GetDisplayName(false)));
        }
    }

    /// <summary>
    /// Event that causes the user to swap places with the target
    /// </summary>
    [Serializable]
    public class SwitcherEvent : BattleEvent
    {
        public SwitcherEvent() { }
        public override GameEvent Clone() { return new SwitcherEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Target.CharStates.Contains<AnchorState>())
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_ANCHORED").ToLocal(), context.Target.GetDisplayName(false)));
            else
            {
                //switch the positions of the attacker and target

                CharAnimSwitch switch1Anim = new CharAnimSwitch();
                switch1Anim.FromLoc = context.User.CharLoc;
                switch1Anim.CharDir = context.User.CharDir;
                switch1Anim.ToLoc = context.Target.CharLoc;
                switch1Anim.MajorAnim = true;

                CharAnimSwitch switch2Anim = new CharAnimSwitch();
                switch2Anim.FromLoc = context.Target.CharLoc;
                switch2Anim.CharDir = context.Target.CharDir;
                switch2Anim.ToLoc = context.User.CharLoc;
                switch2Anim.MajorAnim = true;

                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.SyncActions(context.User, switch1Anim, context.Target, switch2Anim));
            }
        }
    }

    /// <summary>
    /// Event that converts an item to another item
    /// </summary>
    [Serializable]
    public class ItemRestoreEvent : BattleEvent
    {
        /// <summary>
        /// Whether or not the item needs to be held for the effect to work 
        /// </summary>
        public bool HeldOnly;

        /// <summary>
        /// The item being converted
        /// </summary>
        [JsonConverter(typeof(ItemConverter))]
        [DataType(0, DataManager.DataType.Item, false)]
        public string ItemIndex;
        
        /// <summary>
        /// The list of possible items to convert to 
        /// </summary>
        [JsonConverter(typeof(ItemListConverter))]
        [DataType(1, DataManager.DataType.Item, false)]
        public List<string> DefaultItems;
        public StringKey SuccessMsg;

        public ItemRestoreEvent() { DefaultItems = new List<string>(); }
        public ItemRestoreEvent(bool heldOnly, string itemIndex, List<string> defaultItems, StringKey successMsg)
        {
            HeldOnly = heldOnly;
            ItemIndex = itemIndex;
            SuccessMsg = successMsg;
            DefaultItems = defaultItems;
        }
        protected ItemRestoreEvent(ItemRestoreEvent other) : this()
        {
            HeldOnly = other.HeldOnly;
            ItemIndex = other.ItemIndex;
            SuccessMsg = other.SuccessMsg;
            DefaultItems.AddRange(other.DefaultItems);
        }
        public override GameEvent Clone() { return new ItemRestoreEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            //if target has a held item, and it's eligible, use it
            if (!String.IsNullOrEmpty(context.Target.EquippedItem.ID) && context.Target.EquippedItem.ID == ItemIndex)
            {
                InvItem item = context.Target.EquippedItem;

                string newItem = item.HiddenValue;
                if (String.IsNullOrEmpty(newItem))
                    newItem = DefaultItems[DataManager.Instance.Save.Rand.Next(DefaultItems.Count)];

                yield return CoroutineManager.Instance.StartCoroutine(context.Target.DequipItem());

                string oldName = item.GetDisplayName();

                //restore this item
                item.ID = newItem;
                item.HiddenValue = "";
                yield return CoroutineManager.Instance.StartCoroutine(context.Target.EquipItem(item));
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(SuccessMsg.ToLocal(), context.Target.GetDisplayName(false), oldName, item.GetDisplayName()));
            }

            if (!HeldOnly && context.Target.MemberTeam is ExplorerTeam)
            {
                ExplorerTeam team = (ExplorerTeam)context.Target.MemberTeam;
                //iterate over the inventory, restore items
                for (int ii = 0; ii < team.GetInvCount(); ii++)
                {
                    InvItem item = team.GetInv(ii);
                    if (item.ID == ItemIndex)
                    {
                        string newItem = item.HiddenValue;
                        if (String.IsNullOrEmpty(newItem))
                            newItem = DefaultItems[DataManager.Instance.Save.Rand.Next(DefaultItems.Count)];

                        InvItem oldItem = new InvItem(item);

                        item.ID = newItem;
                        item.HiddenValue = "";
                        team.UpdateInv(oldItem, item);
                        DungeonScene.Instance.LogMsg(Text.FormatGrammar(SuccessMsg.ToLocal(), context.Target.GetDisplayName(false), oldItem.GetDisplayName(), item.GetDisplayName()));
                    }
                }
            }
            yield break;
        }
    }
    
    /// <summary>
    /// Event that reduces the damage of a different type depending on the held item 
    /// </summary>
    [Serializable]
    public class PlateProtectEvent : BattleEvent
    {
        /// <summary>
        /// The item mapped to a type
        /// </summary>
        [JsonConverter(typeof(ElementItemDictConverter))]
        [DataType(1, DataManager.DataType.Element, false)]
        [DataType(2, DataManager.DataType.Item, false)]
        public Dictionary<string, string> TypePair;

        public PlateProtectEvent() { TypePair = new Dictionary<string, string>(); }
        public PlateProtectEvent(Dictionary<string, string> weather)
        {
            TypePair = weather;
        }
        protected PlateProtectEvent(PlateProtectEvent other)
            : this()
        {
            foreach (string element in other.TypePair.Keys)
                TypePair.Add(element, other.TypePair[element]);
        }
        public override GameEvent Clone() { return new PlateProtectEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Target.MemberTeam is ExplorerTeam)
            {
                string reqItem;
                if (TypePair.TryGetValue(context.Data.Element, out reqItem))
                {
                    //make sure not already protected
                    if (reqItem != context.Target.EquippedItem.ID)
                    {
                        //search the bag for the item
                        ExplorerTeam team = (ExplorerTeam)context.Target.MemberTeam;
                        for (int ii = 0; ii < team.GetInvCount(); ii++)
                        {
                            if (team.GetInv(ii).ID == reqItem && !team.GetInv(ii).Cursed)
                            {
                                context.AddContextStateMult<DmgMult>(false, 1, 2);
                                yield break;
                            }
                        }
                    }
                }
            }
        }
    }



    /// <summary>
    /// Event that spawns an enemy from a fake item
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
    
    [Serializable]
    public abstract class ItemMetaEvent : BattleEvent
    {
        /// <summary>
        /// Whether to select the highest price item or not
        /// </summary>
        public bool TopDown;
        
        /// <summary>
        /// Whether or not the item needs to be held for the effect to work 
        /// </summary
        public bool HeldOnly;

        /// <summary>
        /// The item to check for first, regardless of price 
        /// </summary>
        [JsonConverter(typeof(ItemConverter))]
        [DataType(0, DataManager.DataType.Item, false)]
        public string PriorityItem;
        
        /// <summary>
        /// If the item has one of the specified ItemStates, then it be picked
        /// </summary>
        [StringTypeConstraint(1, typeof(ItemState))]
        public HashSet<FlagType> States;

        public ItemMetaEvent() { States = new HashSet<FlagType>(); PriorityItem = ""; }
        public ItemMetaEvent(bool topDown, bool heldOnly, string priorityItem, HashSet<FlagType> eligibles)
        {
            TopDown = topDown;
            HeldOnly = heldOnly;
            PriorityItem = priorityItem;
            States = eligibles;
        }
        protected ItemMetaEvent(ItemMetaEvent other)
            : this()
        {
            TopDown = other.TopDown;
            HeldOnly = other.HeldOnly;
            PriorityItem = other.PriorityItem;
            foreach (FlagType useType in other.States)
                States.Add(useType);
        }

        protected virtual bool ItemEligible(InvItem item)
        {
            ItemData entry = DataManager.Instance.GetItem(item.ID);
            if (entry.CannotDrop)
                return false;

            if (States.Count == 0)
                return true;
            if (item.ID == PriorityItem)
                return true;
            //get item entry
            //check to see if the eligible hashlist has the item's usetype
            foreach (FlagType flag in States)
            {
                if (entry.ItemStates.Contains(flag.FullType))
                    return true;
            }
            return false;
        }

        protected int SelectItemTarget(Character targetChar)
        {
            //first check priority item
            if (!String.IsNullOrEmpty(PriorityItem))
            {
                if (targetChar.EquippedItem.ID == PriorityItem && ItemEligible(targetChar.EquippedItem))
                    return -1;

                if (!HeldOnly && targetChar.MemberTeam is ExplorerTeam)
                {
                    for (int ii = 0; ii < ((ExplorerTeam)targetChar.MemberTeam).GetInvCount(); ii++)
                    {
                        if (((ExplorerTeam)targetChar.MemberTeam).GetInv(ii).ID == PriorityItem)
                            return ii;
                    }
                }
            }

            //if target has a held item, and it's eligible, choose it
            if (!String.IsNullOrEmpty(targetChar.EquippedItem.ID) && ItemEligible(targetChar.EquippedItem))
                return -1;

            if (!HeldOnly && targetChar.MemberTeam is ExplorerTeam)
            {
                List<int> slots = new List<int>();
                //iterate over the inventory, get a list of the lowest/highest-costing eligible items
                for (int ii = 0; ii < ((ExplorerTeam)targetChar.MemberTeam).GetInvCount(); ii++)
                {
                    ItemData newEntry = DataManager.Instance.GetItem(((ExplorerTeam)targetChar.MemberTeam).GetInv(ii).ID);
                    if (ItemEligible(((ExplorerTeam)targetChar.MemberTeam).GetInv(ii)))
                    {
                        ItemData entry = null;
                        if (slots.Count > 0)
                            entry = DataManager.Instance.GetItem(((ExplorerTeam)targetChar.MemberTeam).GetInv(slots[0]).ID);
                        if (entry == null || entry.Price == newEntry.Price)
                            slots.Add(ii);
                        else if ((newEntry.Price - entry.Price) * (TopDown ? 1 : -1) > 0)
                        {
                            slots.Clear();
                            slots.Add(ii);
                        }
                    }
                }

                if (slots.Count > 0) //randomly choose one slot
                    return slots[DataManager.Instance.Save.Rand.Next(slots.Count)];
            }
            return -2;
        }
    }

    /// <summary>
    /// Event that pulls all items held by enemies to the user
    /// </summary>
    [Serializable]
    public class MugItemEvent : ItemMetaEvent
    {
        /// <summary>
        /// The message displayed in the dungeon log 
        /// </summary>
        [StringKey(0, true)]
        public StringKey Message;
        
        /// <summary>
        /// Whether to display a message if the item cannot be taken 
        /// </summary
        public bool SilentCheck;

        public MugItemEvent() { }
        public MugItemEvent(bool topDown, bool heldOnly, string priorityItem, HashSet<FlagType> eligibles, StringKey msg, bool silentCheck) : base(topDown, heldOnly, priorityItem, eligibles)
        {
            Message = msg;
            SilentCheck = silentCheck;
        }
        protected MugItemEvent(MugItemEvent other) : base(other)
        {
            Message = other.Message;
            SilentCheck = other.SilentCheck;
        }
        public override GameEvent Clone() { return new MugItemEvent(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Target.CharStates.Contains<StickyHoldState>())
            {
                if (!SilentCheck)
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_STICKY_HOLD").ToLocal(), context.Target.GetDisplayName(false)));
                yield break;
            }

            int itemIndex = SelectItemTarget(context.Target);
            if (itemIndex > -2)
            {
                if (Message.IsValid())
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), context.User.GetDisplayName(false), context.Target.GetDisplayName(false)));


                Loc? newLoc = ZoneManager.Instance.CurrentMap.FindItemlessTile(context.User.CharLoc, CharAction.MAX_RANGE, true);

                if (newLoc != null)
                {
                    InvItem item = (itemIndex > -1 ? ((ExplorerTeam)context.Target.MemberTeam).GetInv(itemIndex) : context.Target.EquippedItem);
                    //remove the item, and make it bounce in the attacker's direction
                    if (itemIndex > -1)
                        ((ExplorerTeam)context.Target.MemberTeam).RemoveFromInv(itemIndex);
                    else
                        yield return CoroutineManager.Instance.StartCoroutine(context.Target.DequipItem());

                    yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.DropMapItem(new MapItem(item), newLoc.Value, context.Target.CharLoc, true));
                }
            }
        }
    }

    /// <summary>
    /// Event that causes the character to drop their item
    /// </summary>
    [Serializable]
    public class DropItemEvent : ItemMetaEvent
    {
        /// <summary>
        /// The message displayed in the dungeon log 
        /// </summary>
        [StringKey(0, true)]
        public StringKey Message;

        /// <summary>
        /// Whether to display a message if the item cannot be dropped
        /// </summary
        public bool SilentCheck;

        public DropItemEvent() { }
        public DropItemEvent(bool topDown, bool heldOnly, string priorityItem, HashSet<FlagType> eligibles, StringKey msg, bool silentCheck) : base(topDown, heldOnly, priorityItem, eligibles)
        {
            Message = msg;
            SilentCheck = silentCheck;
        }
        protected DropItemEvent(DropItemEvent other) : base(other)
        {
            Message = other.Message;
            SilentCheck = other.SilentCheck;
        }
        public override GameEvent Clone() { return new DropItemEvent(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Target.CharStates.Contains<StickyHoldState>())
            {
                if (!SilentCheck)
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_STICKY_HOLD").ToLocal(), context.Target.GetDisplayName(false)));
                yield break;
            }

            int itemIndex = SelectItemTarget(context.Target);
            if (itemIndex > -2)
            {
                if (Message.IsValid())
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), context.User.GetDisplayName(false), context.Target.GetDisplayName(false)));
                InvItem item = (itemIndex > -1 ? ((ExplorerTeam)context.Target.MemberTeam).GetInv(itemIndex) : context.Target.EquippedItem);
                //remove the item, and make it bounce in the attacker's direction
                if (itemIndex > -1)
                    ((ExplorerTeam)context.Target.MemberTeam).RemoveFromInv(itemIndex);
                else
                    yield return CoroutineManager.Instance.StartCoroutine(context.Target.DequipItem());

                Loc endLoc = context.Target.CharLoc + context.User.CharDir.GetLoc() * 2;
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.DropItem(item, endLoc, context.Target.CharLoc));
            }
        }
    }

    /// <summary>
    /// Event that causes the target's item to fly off
    /// </summary>
    [Serializable]
    public class KnockItemEvent : ItemMetaEvent
    {
        public KnockItemEvent() { }
        public KnockItemEvent(bool topDown, bool heldOnly, string priorityItem, HashSet<FlagType> eligibles) : base(topDown, heldOnly, priorityItem, eligibles) { }
        protected KnockItemEvent(KnockItemEvent other) : base(other) { }
        public override GameEvent Clone() { return new KnockItemEvent(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Target.CharStates.Contains<StickyHoldState>())
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_STICKY_HOLD").ToLocal(), context.Target.GetDisplayName(false)));
                yield break;
            }

            int itemIndex = SelectItemTarget(context.Target);
            if (itemIndex > -2)
            {
                InvItem item = (itemIndex > -1 ? ((ExplorerTeam)context.Target.MemberTeam).GetInv(itemIndex) : context.Target.EquippedItem);
                //remove the item, and make it fly off in the attacker's direction as if it were an attack
                if (itemIndex > -1)
                    ((ExplorerTeam)context.Target.MemberTeam).RemoveFromInv(itemIndex);
                else
                    yield return CoroutineManager.Instance.StartCoroutine(context.Target.DequipItem());

                BattleContext newContext = new BattleContext(BattleActionType.Throw);
                newContext.User = context.User;
                newContext.UsageSlot = BattleContext.FORCED_SLOT;

                newContext.StartDir = newContext.User.CharDir;

                //from ThrowItem
                ItemData entry = DataManager.Instance.GetItem(item.ID);
                bool defaultDmg = false;
                bool catchable = true;

                if (entry.UsageType == ItemData.UseType.None || entry.UsageType == ItemData.UseType.Use || entry.UsageType == ItemData.UseType.Learn || entry.UsageType == ItemData.UseType.Box || entry.UsageType == ItemData.UseType.UseOther || entry.ItemStates.Contains<RecruitState>())
                    defaultDmg = true;
                else if (entry.ItemStates.Contains<EdibleState>())
                    catchable = false;

                if (item.Cursed)
                    defaultDmg = true;

                if (defaultDmg)
                {
                    //these just do damage(create a custom effect instead of the item's effect)
                    newContext.Data = new BattleData();
                    newContext.Data.Element = DataManager.Instance.DefaultElement;
                    newContext.Data.ID = item.GetID();
                    newContext.Data.DataType = DataManager.DataType.Item;

                    newContext.Data.Category = BattleData.SkillCategory.Physical;
                    newContext.Data.SkillStates.Set(new BasePowerState(40));
                    newContext.Data.OnHits.Add(-1, new DamageFormulaEvent());
                }
                else
                {
                    newContext.Data = new BattleData(entry.UseEvent);
                    newContext.Data.ID = item.GetID();
                    newContext.Data.DataType = DataManager.DataType.Item;
                }

                if (catchable)
                {
                    BattleData catchData = new BattleData();
                    catchData.Element = DataManager.Instance.DefaultElement;
                    catchData.OnHits.Add(0, new CatchItemEvent());
                    catchData.HitFX.Sound = "DUN_Equip";

                    newContext.Data.BeforeExplosions.Add(-5, new CatchItemSplashEvent());
                    newContext.Data.BeforeHits.Add(-5, new CatchableEvent(catchData));
                }
                newContext.Data.AfterActions.Add(-1, new LandItemEvent());

                newContext.Item = new InvItem(item);
                newContext.Strikes = 1;

                //the action needs to be exactly the linear throw action, but starting from the target's location
                ProjectileAction action = new ProjectileAction();
                action.HitOffset = context.Target.CharLoc - context.User.CharLoc;
                //no intro action
                action.CharAnimData = new CharAnimProcess();
                action.TargetAlignments = Alignment.Friend | Alignment.Foe;
                action.Anim = new AnimData(entry.ThrowAnim);
                action.ItemSprite = DataManager.Instance.GetItem(item.ID).Sprite;
                //no intro sound
                if (entry.ItemStates.Contains<AmmoState>())
                    action.ActionFX.Sound = "DUN_Throw_Spike";
                else
                    action.ActionFX.Sound = "DUN_Throw_Something";
                action.Speed = 14;
                action.Range = 8;
                action.StopAtHit = true;
                action.StopAtWall = true;
                newContext.HitboxAction = action;

                newContext.Explosion = new ExplosionData(entry.Explosion);
                newContext.Explosion.TargetAlignments = Alignment.Friend | Alignment.Foe | Alignment.Self;

                newContext.SetActionMsg(Text.FormatGrammar(new StringKey("MSG_KNOCK_ITEM").ToLocal(), context.User.GetDisplayName(false), context.Target.GetDisplayName(false), item.GetDisplayName()));


                //beforetryaction and beforeAction need to distinguish forced effects vs willing effects for all times it's triggered
                //as a forced attack, preprocessaction also should not factor in confusion dizziness
                //examples where the distinction matters:
                //-counting down
                //-confusion dizziness
                //-certain kinds of status-based move prevention
                //-forced actions (charging moves, rampage moves, etc)

                yield return CoroutineManager.Instance.StartCoroutine(newContext.User.BeforeTryAction(newContext));
                if (newContext.CancelState.Cancel) { yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.CancelWait(newContext.User.CharLoc)); yield break; }
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.PreProcessAction(newContext));

                //Handle Use
                yield return CoroutineManager.Instance.StartCoroutine(newContext.User.BeforeAction(newContext));
                if (newContext.CancelState.Cancel) { yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.CancelWait(newContext.User.CharLoc)); yield break; }

                newContext.PrintActionMsg();

                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.ExecuteAction(newContext));
                if (newContext.CancelState.Cancel) { yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.CancelWait(newContext.User.CharLoc)); yield break; }
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.RepeatActions(newContext));
            }
        }
    }

    /// <summary>
    /// Event that transforms the character's item to another item
    /// </summary>
    [Serializable]
    public class TransformItemEvent : ItemMetaEvent
    {
        /// <summary>
        /// The item to transform to
        /// </summary>
        [JsonConverter(typeof(ItemConverter))]
        [DataType(0, DataManager.DataType.Item, false)]
        public string NewItem;

        public TransformItemEvent() { NewItem = ""; }
        public TransformItemEvent(bool topDown, bool heldOnly, string priorityItem, string newItem, HashSet<FlagType> eligibles)
            : base(topDown, heldOnly, priorityItem, eligibles)
        {
            NewItem = newItem;
        }
        protected TransformItemEvent(TransformItemEvent other)
            : base(other)
        {
            NewItem = other.NewItem;
        }
        public override GameEvent Clone() { return new TransformItemEvent(this); }

        protected override bool ItemEligible(InvItem item)
        {
            if (item.ID == NewItem)
                return false;

            return base.ItemEligible(item);
        }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int itemIndex = SelectItemTarget(context.Target);
            if (itemIndex > -2)
            {
                InvItem item = (itemIndex > -1 ? ((ExplorerTeam)context.Target.MemberTeam).GetInv(itemIndex) : context.Target.EquippedItem);
                if (item.ID != NewItem)
                {
                    InvItem oldItem = new InvItem(item);
                    //change the item to a different number index, set that item's hidden value to the previous item
                    if (itemIndex > -1)
                    {
                        item.HiddenValue = item.ID;
                        item.ID = NewItem;
                        DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_TRANSFORM_ITEM").ToLocal(), context.Target.GetDisplayName(false),
                            oldItem.GetDisplayName(), item.GetDisplayName()));
                        ((ExplorerTeam)context.Target.MemberTeam).UpdateInv(oldItem, item);
                    }
                    else
                    {
                        yield return CoroutineManager.Instance.StartCoroutine(context.Target.DequipItem());
                        item.HiddenValue = item.ID;
                        item.ID = NewItem;
                        DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_TRANSFORM_HELD_ITEM").ToLocal(), context.Target.GetDisplayName(false),
                            oldItem.GetDisplayName(), item.GetDisplayName()));
                        yield return CoroutineManager.Instance.StartCoroutine(context.Target.EquipItem(item));
                    }
                    yield break;
                }
            }
            DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_TRANSFORM_ITEM_FAIL").ToLocal(), context.Target.GetDisplayName(false)));
        }
    }

    /// <summary>
    /// Event that makes the character's item sticky or unsticks it
    /// </summary>
    [Serializable]
    public class SetItemStickyEvent : ItemMetaEvent
    {
        /// <summary>
        /// Whether to make the item sticky or unsticks it
        /// </summary>
        public bool Sticky;

        public SetItemStickyEvent() { }
        public SetItemStickyEvent(bool topDown, bool heldOnly, string priorityItem, bool sticky, HashSet<FlagType> eligibles)
            : base(topDown, heldOnly, priorityItem, eligibles)
        {
            Sticky = sticky;
        }
        protected SetItemStickyEvent(SetItemStickyEvent other)
            : base(other)
        {
            Sticky = other.Sticky;
        }
        public override GameEvent Clone() { return new SetItemStickyEvent(this); }

        protected override bool ItemEligible(InvItem item)
        {
            if (item.Cursed == Sticky)
                return false;

            return base.ItemEligible(item);
        }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int itemIndex = SelectItemTarget(context.Target);
            if (itemIndex > -2)
            {
                InvItem item = (itemIndex > -1 ? ((ExplorerTeam)context.Target.MemberTeam).GetInv(itemIndex) : context.Target.EquippedItem);
                //(un)stick the item
                if (itemIndex > -1)
                {
                    if (item.Cursed == Sticky)
                        DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_TRANSFORM_ITEM_FAIL").ToLocal(), context.Target.GetDisplayName(false)));
                    else
                    {
                        if (Sticky)
                        {
                            GameManager.Instance.BattleSE("DUN_Sticky");
                            DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_CURSE_ITEM").ToLocal(), context.Target.GetDisplayName(false), item.GetDisplayName()));
                        }
                        else
                            DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_CLEANSE_ITEM").ToLocal(), context.Target.GetDisplayName(false), item.GetDisplayName()));
                    }
                }
                else
                {
                    if (Sticky)
                    {
                        if (item.Cursed)
                            DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_ALREADY_CURSED").ToLocal(), context.Target.GetDisplayName(false), item.GetDisplayName()));
                        else
                        {
                            GameManager.Instance.BattleSE("DUN_Sticky");
                            DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_CURSE_HELD_ITEM").ToLocal(), context.Target.GetDisplayName(false), item.GetDisplayName()));
                        }
                    }
                    else if (item.Cursed)
                        DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_CLEANSE_HELD_ITEM").ToLocal(), context.Target.GetDisplayName(false), item.GetDisplayName()));
                }
                item.Cursed = Sticky;

                if (itemIndex > -1)
                    ((ExplorerTeam)context.Target.MemberTeam).UpdateInv(item, item);
            }
            yield break;
        }
    }
    
    /// <summary>
    /// Event that destroy's the character item
    /// </summary>
    [Serializable]
    public class DestroyItemEvent : ItemMetaEvent
    {
        public DestroyItemEvent() { }
        public DestroyItemEvent(bool topDown, bool heldOnly, string priorityItem, HashSet<FlagType> eligibles) : base(topDown, heldOnly, priorityItem, eligibles) { }
        protected DestroyItemEvent(DestroyItemEvent other) : base(other) { }
        public override GameEvent Clone() { return new DestroyItemEvent(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int itemIndex = SelectItemTarget(context.Target);
            if (itemIndex > -2)
            {
                InvItem item = (itemIndex > -1 ? ((ExplorerTeam)context.Target.MemberTeam).GetInv(itemIndex) : context.Target.EquippedItem);
                //destroy the item
                if (itemIndex > -1)
                {
                    ((ExplorerTeam)context.Target.MemberTeam).RemoveFromInv(itemIndex);
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_LOSE_ITEM").ToLocal(), context.Target.GetDisplayName(false), item.GetDisplayName()));
                }
                else
                {
                    yield return CoroutineManager.Instance.StartCoroutine(context.Target.DequipItem());
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_LOSE_HELD_ITEM").ToLocal(), context.Target.GetDisplayName(false), item.GetDisplayName()));
                }
            }
            yield break;
        }
    }

    /// <summary>
    /// Event the causes the user to steal the target's item
    /// </summary>
    [Serializable]
    public class StealItemEvent : ItemMetaEvent
    {

        /// <summary>
        /// The message displayed in the dungeon log 
        /// </summary>
        public StringKey Message;
        
        /// <summary>
        /// Whether the character attacked instead steals the item
        /// </summary>
        public bool AffectTarget;
        
        /// <summary>
        /// Whether to display a message if the item cannot be dropped
        /// </summary
        public bool SilentCheck;

        public StealItemEvent() { }
        public StealItemEvent(bool topDown, bool heldOnly, string priorityItem, HashSet<FlagType> eligibles, StringKey msg, bool affectTarget, bool silentCheck)
            : base(topDown, heldOnly, priorityItem, eligibles)
        {
            Message = msg;
            AffectTarget = affectTarget;
            SilentCheck = silentCheck;
        }
        protected StealItemEvent(StealItemEvent other)
            : base(other)
        {
            Message = other.Message;
            AffectTarget = other.AffectTarget;
            SilentCheck = other.SilentCheck;
        }
        public override GameEvent Clone() { return new StealItemEvent(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = (AffectTarget ? context.Target : context.User);
            Character origin = (AffectTarget ? context.User : context.Target);

            if (origin.Dead)
                yield break;

            if (target.CharStates.Contains<StickyHoldState>())
            {
                if (!SilentCheck)
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_STICKY_HOLD").ToLocal(), target.GetDisplayName(false)));
                yield break;
            }

            //check to make sure the item can be taken off
            if (!origin.EquippedItem.Cursed || origin.CanRemoveStuck)
            {
                int itemIndex = SelectItemTarget(target);
                if (itemIndex > -2)
                {
                    InvItem item = (itemIndex > -1 ? ((ExplorerTeam)target.MemberTeam).GetInv(itemIndex) : target.EquippedItem);
                    //remove the item and give it to the attacker
                    if (itemIndex > -1)
                        ((ExplorerTeam)target.MemberTeam).RemoveFromInv(itemIndex);
                    else
                        yield return CoroutineManager.Instance.StartCoroutine(target.DequipItem());


                    //item steal animation
                    if (!target.Unidentifiable && !origin.Unidentifiable)
                    {
                        int MaxDistance = (int)Math.Sqrt((target.MapLoc - origin.MapLoc).DistSquared());
                        ItemAnim itemAnim = new ItemAnim(target.MapLoc, origin.MapLoc, DataManager.Instance.GetItem(item.ID).Sprite, MaxDistance / 2, 0);
                        DungeonScene.Instance.CreateAnim(itemAnim, DrawLayer.Normal);
                        yield return new WaitForFrames(ItemAnim.ITEM_ACTION_TIME);
                    }

                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), origin.GetDisplayName(false), item.GetDisplayName(), target.GetDisplayName(false), owner.GetDisplayName()));

                    if (origin.MemberTeam is ExplorerTeam)
                    {
                        if (((ExplorerTeam)origin.MemberTeam).GetInvCount() < ((ExplorerTeam)origin.MemberTeam).GetMaxInvSlots(ZoneManager.Instance.CurrentZone))
                        {
                            //attackers already holding an item will have the item returned to the bag
                            if (!String.IsNullOrEmpty(origin.EquippedItem.ID))
                            {
                                InvItem attackerItem = origin.EquippedItem;
                                yield return CoroutineManager.Instance.StartCoroutine(origin.DequipItem());
                                origin.MemberTeam.AddToInv(attackerItem);
                            }
                            yield return CoroutineManager.Instance.StartCoroutine(origin.EquipItem(item));
                        }
                        else
                        {
                            yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(30));
                            DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_INV_FULL").ToLocal(), origin.GetDisplayName(false), item.GetDisplayName()));
                            //if the bag is full, or there is no bag, the stolen item will slide off in the opposite direction they're facing
                            Loc endLoc = origin.CharLoc + origin.CharDir.Reverse().GetLoc();
                            yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.DropItem(item, endLoc, origin.CharLoc));
                        }
                    }
                    else
                    {
                        if (!String.IsNullOrEmpty(origin.EquippedItem.ID))
                        {
                            InvItem attackerItem = origin.EquippedItem;
                            yield return CoroutineManager.Instance.StartCoroutine(origin.DequipItem());
                            //if the user is holding an item already, the item will slide off in the opposite direction they're facing
                            Loc endLoc = origin.CharLoc + origin.CharDir.Reverse().GetLoc();
                            yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.DropItem(attackerItem, endLoc, origin.CharLoc));
                        }
                        yield return CoroutineManager.Instance.StartCoroutine(origin.EquipItem(item));
                    }
                }
                else
                {
                    if (!SilentCheck)
                        DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_STEAL_ITEM_FAIL").ToLocal(), target.GetDisplayName(false)));
                }
            }
            else
            {
                if (!SilentCheck)
                {
                    GameManager.Instance.BattleSE("DUN_Sticky");
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_STEAL_ITEM_CURSED").ToLocal(), origin.GetDisplayName(false)));
                }
            }
        }
    }
    
    /// <summary>
    /// Event that causes the user to steal the target's item and replaced the item they are currently holding
    /// </summary>
    [Serializable]
    public class BegItemEvent : ItemMetaEvent
    {
        public BegItemEvent() { }
        public BegItemEvent(bool topDown, bool heldOnly, string priorityItem, HashSet<FlagType> eligibles)
            : base(topDown, heldOnly, priorityItem, eligibles) { }
        protected BegItemEvent(BegItemEvent other)
            : base(other) { }
        public override GameEvent Clone() { return new BegItemEvent(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = context.Target;
            Character origin = context.User;

            if (origin.Dead)
                yield break;

            if (target.CharStates.Contains<StickyHoldState>())
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_STICKY_HOLD").ToLocal(), target.GetDisplayName(false)));
                yield break;
            }


            int itemIndex = SelectItemTarget(target);
            if (itemIndex > -2)
            {
                InvItem item = (itemIndex > -1 ? ((ExplorerTeam)target.MemberTeam).GetInv(itemIndex) : target.EquippedItem);
                if (itemIndex == -1 && item.Cursed && !target.CanRemoveStuck)
                {
                    GameManager.Instance.BattleSE("DUN_Sticky");
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_BESTOW_ITEM_CURSED").ToLocal(), target.GetDisplayName(false), item.GetDisplayName()));
                }
                else
                {
                    //remove the item and give it to the attacker
                    if (itemIndex > -1)
                        ((ExplorerTeam)target.MemberTeam).RemoveFromInv(itemIndex);
                    else
                        yield return CoroutineManager.Instance.StartCoroutine(target.DequipItem());

                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_GIVE_ITEM_AWAY").ToLocal(), target.GetDisplayName(false), item.GetDisplayName()));

                    //item steal animation
                    if (!target.Unidentifiable && !origin.Unidentifiable)
                    {
                        int MaxDistance = (int)Math.Sqrt((target.MapLoc - origin.MapLoc).DistSquared());
                        ItemAnim itemAnim = new ItemAnim(target.MapLoc, origin.MapLoc, DataManager.Instance.GetItem(item.ID).Sprite, MaxDistance / 2, 0);
                        DungeonScene.Instance.CreateAnim(itemAnim, DrawLayer.Normal);
                        yield return new WaitForFrames(ItemAnim.ITEM_ACTION_TIME);
                    }

                    if (!origin.EquippedItem.Cursed || origin.CanRemoveStuck)
                    {
                        if (origin.MemberTeam is ExplorerTeam)
                        {
                            if (((ExplorerTeam)origin.MemberTeam).GetInvCount() < ((ExplorerTeam)origin.MemberTeam).GetMaxInvSlots(ZoneManager.Instance.CurrentZone))
                            {
                                //attackers already holding an item will have the item returned to the bag
                                if (!String.IsNullOrEmpty(origin.EquippedItem.ID))
                                {
                                    InvItem attackerItem = origin.EquippedItem;
                                    yield return CoroutineManager.Instance.StartCoroutine(origin.DequipItem());
                                    origin.MemberTeam.AddToInv(attackerItem);
                                }
                                yield return CoroutineManager.Instance.StartCoroutine(origin.EquipItem(item));
                            }
                            else
                            {
                                yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(30));
                                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_INV_FULL").ToLocal(), origin.GetDisplayName(false), item.GetDisplayName()));
                                //if the bag is full, or there is no bag, the stolen item will slide off in the opposite direction they're facing
                                Loc endLoc = origin.CharLoc + origin.CharDir.Reverse().GetLoc();
                                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.DropItem(item, endLoc, origin.CharLoc));
                            }
                        }
                        else
                        {
                            if (!String.IsNullOrEmpty(origin.EquippedItem.ID))
                            {
                                InvItem attackerItem = origin.EquippedItem;
                                yield return CoroutineManager.Instance.StartCoroutine(origin.DequipItem());
                                //if the user is holding an item already, the item will slide off in the opposite direction they're facing
                                Loc endLoc = origin.CharLoc + origin.CharDir.Reverse().GetLoc();
                                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.DropItem(attackerItem, endLoc, origin.CharLoc));
                            }
                            yield return CoroutineManager.Instance.StartCoroutine(origin.EquipItem(item));
                        }
                    }
                    else
                    {
                        GameManager.Instance.BattleSE("DUN_Sticky");
                        DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_RECEIVE_ITEM_CURSED").ToLocal(), origin.GetDisplayName(false)));
                        //the new item will slide off in the opposite direction they're facing
                        Loc endLoc = origin.CharLoc + origin.CharDir.Reverse().GetLoc();
                        yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.DropItem(item, endLoc, origin.CharLoc));
                    }

                }
            }
            else
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_BESTOW_ITEM_FAIL").ToLocal(), target.GetDisplayName(false)));
        }
    }

    /// <summary>
    /// Event that causes the user to exchange items with the target
    /// </summary>
    [Serializable]
    public class TrickItemEvent : ItemMetaEvent
    {
        public TrickItemEvent() { }
        public TrickItemEvent(bool topDown, bool heldOnly, string priorityItem, HashSet<FlagType> eligibles) : base(topDown, heldOnly, priorityItem, eligibles) { }
        protected TrickItemEvent(TrickItemEvent other) : base(other) { }
        public override GameEvent Clone() { return new TrickItemEvent(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Target.CharStates.Contains<StickyHoldState>())
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_STICKY_HOLD").ToLocal(), context.Target.GetDisplayName(false)));
                yield break;
            }

            //takes the held/bag item of both characters and swaps them
            int attackerIndex = SelectItemTarget(context.User);
            int targetIndex = SelectItemTarget(context.Target);
            if (attackerIndex > -2 && targetIndex > -2)
            {
                InvItem attackerItem = (attackerIndex > -1 ? context.User.MemberTeam.GetInv(attackerIndex) : context.User.EquippedItem);
                InvItem targetItem = (targetIndex > -1 ? context.Target.MemberTeam.GetInv(targetIndex) : context.Target.EquippedItem);

                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_EXCHANGE_ITEM").ToLocal(), context.User.GetDisplayName(false), context.Target.GetDisplayName(false),
                    attackerItem.GetDisplayName(), targetItem.GetDisplayName()));

                if (targetIndex > -1)
                {
                    context.Target.MemberTeam.RemoveFromInv(targetIndex);
                    context.Target.MemberTeam.AddToInv(attackerItem);
                }
                else
                {
                    yield return CoroutineManager.Instance.StartCoroutine(context.Target.DequipItem());
                    yield return CoroutineManager.Instance.StartCoroutine(context.Target.EquipItem(attackerItem));
                }

                if (attackerIndex > -1)
                {
                    context.User.MemberTeam.RemoveFromInv(attackerIndex);
                    context.User.MemberTeam.AddToInv(targetItem);
                }
                else
                {
                    yield return CoroutineManager.Instance.StartCoroutine(context.User.DequipItem());
                    yield return CoroutineManager.Instance.StartCoroutine(context.User.EquipItem(targetItem));
                }
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that unsticks all the team's items. 
    /// </summary>
    [Serializable]
    public class CleanseTeamEvent : BattleEvent
    {
        public CleanseTeamEvent() { }
        public override GameEvent Clone() { return new CleanseTeamEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            //cleanse
            foreach (Character character in context.Target.MemberTeam.EnumerateChars())
            {
                if (!String.IsNullOrEmpty(character.EquippedItem.ID) && character.EquippedItem.Cursed)
                    character.EquippedItem.Cursed = false;
            }

            if (context.Target.MemberTeam is ExplorerTeam)
            {
                ExplorerTeam team = (ExplorerTeam)context.Target.MemberTeam;
                for (int ii = 0; ii < team.GetInvCount(); ii++)
                    team.GetInv(ii).Cursed = false;

                team.UpdateInv(null, null);
            }

            yield break;
        }
    }

    /// <summary>
    /// Event that causes the user to exchange items with the target, unless the inventory is full
    /// </summary>
    [Serializable]
    public class SwitchHeldItemEvent : BattleEvent
    {
        public SwitchHeldItemEvent() { }
        public override GameEvent Clone() { return new SwitchHeldItemEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Target.CharStates.Contains<StickyHoldState>())
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_STICKY_HOLD").ToLocal(), context.Target.GetDisplayName(false)));
                yield break;
            }

            InvItem attackerItem = context.User.EquippedItem;
            InvItem targetItem = context.Target.EquippedItem;

            bool attackerCannotDrop = false;
            if (!String.IsNullOrEmpty(attackerItem.ID))
            {
                ItemData entry = DataManager.Instance.GetItem(attackerItem.ID);
                attackerCannotDrop = entry.CannotDrop;
            }
            bool targetCannotDrop = false;
            if (!String.IsNullOrEmpty(targetItem.ID))
            {
                ItemData entry = DataManager.Instance.GetItem(targetItem.ID);
                targetCannotDrop = entry.CannotDrop;
            }

            if ((!String.IsNullOrEmpty(attackerItem.ID) || !String.IsNullOrEmpty(targetItem.ID)) && (!attackerCannotDrop && !targetCannotDrop))
            {
                //if it's an explorer, and their inv is full, and they're not holding anything, they cannot be given an item by the other party
                if (String.IsNullOrEmpty(attackerItem.ID) && context.User.MemberTeam is ExplorerTeam && ((ExplorerTeam)context.User.MemberTeam).GetInvCount() >= ((ExplorerTeam)context.User.MemberTeam).GetMaxInvSlots(ZoneManager.Instance.CurrentZone))
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_INV_FULL").ToLocal(), context.User.GetDisplayName(false), targetItem.GetDisplayName()));
                else if (String.IsNullOrEmpty(targetItem.ID) && context.Target.MemberTeam is ExplorerTeam && ((ExplorerTeam)context.Target.MemberTeam).GetInvCount() >= ((ExplorerTeam)context.Target.MemberTeam).GetMaxInvSlots(ZoneManager.Instance.CurrentZone))
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_INV_FULL").ToLocal(), context.Target.GetDisplayName(false), attackerItem.GetDisplayName()));
                else
                {
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_EXCHANGE_HELD_ITEM").ToLocal(), context.User.GetDisplayName(false), context.Target.GetDisplayName(false)));

                    yield return CoroutineManager.Instance.StartCoroutine(context.Target.DequipItem());
                    if (!String.IsNullOrEmpty(attackerItem.ID))
                        yield return CoroutineManager.Instance.StartCoroutine(context.Target.EquipItem(attackerItem));

                    yield return CoroutineManager.Instance.StartCoroutine(context.User.DequipItem());
                    if (!String.IsNullOrEmpty(targetItem.ID))
                        yield return CoroutineManager.Instance.StartCoroutine(context.User.EquipItem(targetItem));
                }
            }
            else
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_EXCHANGE_ITEM_FAIL").ToLocal(), context.User.GetDisplayName(false), context.Target.GetDisplayName(false)));
        }
    }

    /// <summary>
    /// Event that causes the character to use the enemy's item
    /// </summary>
    [Serializable]
    public class UseFoeItemEvent : ItemMetaEvent
    {
        /// <summary>
        /// Whether the attacker uses the held item. Otherwise, the enemy uses the attacker's held item.
        /// </summary>
        public bool AffectTarget;

        /// <summary>
        /// Whether to display a message if the item cannot be dropped
        /// </summary
        public bool SilentCheck;

        public UseFoeItemEvent() { }
        public UseFoeItemEvent(bool topDown, bool heldOnly, string priorityItem, HashSet<FlagType> eligibles, bool affectTarget, bool silentCheck)
            : base(topDown, heldOnly, priorityItem, eligibles)
        {
            AffectTarget = affectTarget;
            SilentCheck = silentCheck;
        }
        protected UseFoeItemEvent(UseFoeItemEvent other)
            : base(other)
        {
            AffectTarget = other.AffectTarget;
            SilentCheck = other.SilentCheck;
        }
        public override GameEvent Clone() { return new UseFoeItemEvent(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = (AffectTarget ? context.Target : context.User);
            Character origin = (AffectTarget ? context.User : context.Target);

            if (origin.Dead)
                yield break;

            if (target.CharStates.Contains<StickyHoldState>())
            {
                if (!SilentCheck)
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_STICKY_HOLD").ToLocal(), target.GetDisplayName(false)));
                yield break;
            }

            int itemIndex = SelectItemTarget(target);
            if (itemIndex > -2)
            {
                InvItem item = (itemIndex > -1 ? ((ExplorerTeam)target.MemberTeam).GetInv(itemIndex) : target.EquippedItem);

                if (item.Cursed)
                {
                    if (!SilentCheck)
                    {
                        GameManager.Instance.BattleSE("DUN_Sticky");
                        DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_USE_CURSED").ToLocal(), item.GetDisplayName()), false, true);
                    }
                }
                else
                {

                    BattleContext newContext = new BattleContext(BattleActionType.Item);
                    newContext.User = origin;
                    newContext.UsageSlot = BattleContext.FORCED_SLOT;

                    ItemData entry = DataManager.Instance.GetItem(item.ID);

                    newContext.StartDir = newContext.User.CharDir;
                    newContext.Data = new BattleData(entry.UseEvent);
                    newContext.Data.ID = item.GetID();
                    newContext.Data.DataType = DataManager.DataType.Item;
                    newContext.Explosion = new ExplosionData(entry.Explosion);
                    newContext.Strikes = 1;
                    newContext.Item = new InvItem(item);
                    newContext.HitboxAction = entry.UseAction.Clone();
                    switch (entry.UsageType)
                    {
                        case ItemData.UseType.Eat:
                            {
                                newContext.SetActionMsg(Text.FormatGrammar(new StringKey("MSG_STEAL_EAT").ToLocal(), newContext.User.GetDisplayName(false), item.GetDisplayName()));
                                break;
                            }
                        case ItemData.UseType.Drink:
                            {
                                newContext.SetActionMsg(Text.FormatGrammar(new StringKey("MSG_STEAL_DRINK").ToLocal(), newContext.User.GetDisplayName(false), item.GetDisplayName()));
                                break;
                            }
                        case ItemData.UseType.Learn:
                            {
                                newContext.SetActionMsg(Text.FormatGrammar(new StringKey("MSG_STEAL_OPERATE").ToLocal(), newContext.User.GetDisplayName(false), item.GetDisplayName()));
                                break;
                            }
                        case ItemData.UseType.Use:
                            {
                                newContext.SetActionMsg(Text.FormatGrammar(new StringKey("MSG_STEAL_USE").ToLocal(), newContext.User.GetDisplayName(false), item.GetDisplayName()));
                                break;
                            }
                    }


                    //if (context.CancelState.Cancel) { yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(30)); yield break; }
                    //yield return CoroutinesManager.Instance.StartCoroutine(context.User.BeforeTryAction(context));
                    //if (context.CancelState.Cancel) { yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(30)); yield break; }
                    //yield return CoroutinesManager.Instance.StartCoroutine(PreProcessAction(context));
                    newContext.StrikeStartTile = newContext.User.CharLoc;
                    ////move has been made; end-turn must be done from this point onwards

                    //HandleItemUse

                    //yield return CoroutinesManager.Instance.StartCoroutine(context.User.BeforeAction(context));
                    //if (context.CancelState.Cancel) { yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(30)); yield break; }

                    //PreExecuteItem

                    //remove the item, and have the attacker use the item as a move
                    if (itemIndex > -1)
                        ((ExplorerTeam)target.MemberTeam).RemoveFromInv(itemIndex);
                    else
                        yield return CoroutineManager.Instance.StartCoroutine(target.DequipItem());

                    newContext.PrintActionMsg();

                    //yield return CoroutinesManager.Instance.StartCoroutine(ExecuteAction(context));
                    //if (context.CancelState.Cancel) { yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(30)); yield break; }
                    //yield return CoroutinesManager.Instance.StartCoroutine(RepeatActions(context));


                    //TODO: turn this into a full move invocation, so that modifiers that stop item use can take effect
                    //for now, just give its effects to the user, as detailed below (and remove later):

                    newContext.ExplosionTile = newContext.User.CharLoc;

                    newContext.Target = newContext.User;


                    yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.ProcessEndAnim(newContext.User, newContext.Target, newContext.Data));

                    yield return CoroutineManager.Instance.StartCoroutine(newContext.Data.Hit(newContext));
                }
            }
        }
    }


    /// <summary>
    /// Event that removes the user's held item and sets the item in the context
    /// </summary>
    [Serializable]
    public class HeldItemMoveEvent : BattleEvent
    {
        public HeldItemMoveEvent() { }
        public override GameEvent Clone() { return new HeldItemMoveEvent(); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            bool attackerCannotDrop = false;
            if (!String.IsNullOrEmpty(context.User.EquippedItem.ID))
            {
                ItemData entry = DataManager.Instance.GetItem(context.User.EquippedItem.ID);
                attackerCannotDrop = entry.CannotDrop;
            }

            if (!String.IsNullOrEmpty(context.User.EquippedItem.ID) && !attackerCannotDrop)
            {
                context.Item = context.User.EquippedItem;
                yield return CoroutineManager.Instance.StartCoroutine(context.User.DequipItem());
            }
            else
            {
                context.CancelState.Cancel = true;
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_BESTOW_ITEM_FAIL").ToLocal(), context.User.GetDisplayName(false)));
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that causes the user to pass the held item to the target
    /// </summary>
    [Serializable]
    public class BestowItemEvent : BattleEvent
    {
        public BestowItemEvent() { }
        public override GameEvent Clone() { return new BestowItemEvent(); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (!String.IsNullOrEmpty(context.Target.EquippedItem.ID) && context.Target.CharStates.Contains<StickyHoldState>())
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_STICKY_HOLD").ToLocal(), context.Target.GetDisplayName(false)));

                //bestowed item slides off
                Loc endLoc = context.Target.CharLoc + context.User.CharDir.GetLoc() * 2;
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.DropItem(context.Item, endLoc, context.Target.CharLoc));
            }
            else if (!String.IsNullOrEmpty(context.Item.ID))
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_BESTOW_ITEM").ToLocal(), context.Target.GetDisplayName(false), context.Item.GetDisplayName()));

                if (!String.IsNullOrEmpty(context.Target.EquippedItem.ID))
                {
                    //held item slides off
                    InvItem heldItem = context.Target.EquippedItem;
                    yield return CoroutineManager.Instance.StartCoroutine(context.Target.DequipItem());
                    Loc endLoc = context.Target.CharLoc + context.User.CharDir.GetLoc() * 2;
                    yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.DropItem(heldItem, endLoc, context.Target.CharLoc));

                    //give the target the item
                    yield return CoroutineManager.Instance.StartCoroutine(context.Target.EquipItem(new InvItem(context.Item)));
                }
                else if (context.Target.MemberTeam is ExplorerTeam && ((ExplorerTeam)context.Target.MemberTeam).GetInvCount() >= ((ExplorerTeam)context.Target.MemberTeam).GetMaxInvSlots(ZoneManager.Instance.CurrentZone))
                {
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_INV_FULL").ToLocal(), context.Target.GetDisplayName(false), context.Item.GetDisplayName()));
                    //check if inventory is full.  If so, make the bestowed item slide off
                    Loc endLoc = context.Target.CharLoc + context.Target.CharDir.Reverse().GetLoc();
                    yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.DropItem(context.Item, endLoc, context.Target.CharLoc));

                }
                else
                {
                    //give the target the item
                    yield return CoroutineManager.Instance.StartCoroutine(context.Target.EquipItem(new InvItem(context.Item)));
                }

            }
        }
    }
    
    /// <summary>
    /// Event that causes the character to equip the item in the BattleContext
    /// </summary>
    [Serializable]
    public class CatchItemEvent : BattleEvent
    {
        public CatchItemEvent() { }
        public override GameEvent Clone() { return new CatchItemEvent(); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (!String.IsNullOrEmpty(context.Item.ID))
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_CATCH_ITEM").ToLocal(), context.Target.GetDisplayName(false), context.Item.GetDisplayName()));
                //give the target the item
                yield return CoroutineManager.Instance.StartCoroutine(context.Target.EquipItem(new InvItem(context.Item)));
            }
            yield break;
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
    /// Event that converts the character to the specified type
    /// </summary>
    [Serializable]
    public class ChangeToElementEvent : BattleEvent
    {
        
        /// <summary>
        /// The type to convert to
        /// </summary>
        [JsonConverter(typeof(ElementConverter))]
        [DataType(0, DataManager.DataType.Element, false)]
        public string TargetElement;

        public ChangeToElementEvent() { TargetElement = ""; }
        public ChangeToElementEvent(string element)
        {
            TargetElement = element;
        }
        protected ChangeToElementEvent(ChangeToElementEvent other)
        {
            TargetElement = other.TargetElement;
        }
        public override GameEvent Clone() { return new ChangeToElementEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (!(TargetElement == context.Target.Element1 && context.Target.Element2 == DataManager.Instance.DefaultElement))
                yield return CoroutineManager.Instance.StartCoroutine(context.Target.ChangeElement(TargetElement, DataManager.Instance.DefaultElement));
            else
            {
                ElementData typeData = DataManager.Instance.GetElement(TargetElement);
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_ALREADY_HAS_ELEMENT").ToLocal(), context.Target.GetDisplayName(false), typeData.GetIconName()));
            }
        }
    }

    /// <summary>
    /// Event that adds the specified type to the target's type
    /// </summary>
    [Serializable]
    public class AddElementEvent : BattleEvent
    {
        /// <summary>
        /// The type to add
        /// </summary>
        [JsonConverter(typeof(ElementConverter))]
        [DataType(0, DataManager.DataType.Element, false)]
        public string TargetElement;

        public AddElementEvent() { TargetElement = ""; }
        public AddElementEvent(string element)
        {
            TargetElement = element;
        }
        protected AddElementEvent(AddElementEvent other)
        {
            TargetElement = other.TargetElement;
        }
        public override GameEvent Clone() { return new AddElementEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (!context.Target.HasElement(TargetElement))
                yield return CoroutineManager.Instance.StartCoroutine(context.Target.ChangeElement(TargetElement, context.Target.Element1));
            else
            {
                ElementData typeData = DataManager.Instance.GetElement(TargetElement);
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_ALREADY_HAS_ELEMENT").ToLocal(), context.Target.GetDisplayName(false), typeData.GetIconName()));
            }
        }
    }
    
    /// <summary>
    /// Event that removes the specified type from the target
    /// </summary>
    [Serializable]
    public class RemoveElementEvent : BattleEvent
    {
        /// <summary>
        /// The type to remove
        /// </summary>
        [JsonConverter(typeof(ElementConverter))]
        [DataType(0, DataManager.DataType.Element, false)]
        public string TargetElement;

        public RemoveElementEvent() { TargetElement = ""; }
        public RemoveElementEvent(string element)
        {
            TargetElement = element;
        }
        protected RemoveElementEvent(RemoveElementEvent other)
        {
            TargetElement = other.TargetElement;
        }
        public override GameEvent Clone() { return new RemoveElementEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Target.Element2 == TargetElement)
                yield return CoroutineManager.Instance.StartCoroutine(context.Target.ChangeElement(context.Target.Element1, DataManager.Instance.DefaultElement, true, false));
            if (context.Target.Element1 == TargetElement)
                yield return CoroutineManager.Instance.StartCoroutine(context.Target.ChangeElement(context.Target.Element2, DataManager.Instance.DefaultElement, true, false));
        }
    }

    [Serializable]
    public class ReflectElementEvent : BattleEvent
    {
        public ReflectElementEvent() { }
        public override GameEvent Clone() { return new ReflectElementEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            yield return CoroutineManager.Instance.StartCoroutine(context.User.ChangeElement(context.Target.Element1, context.Target.Element2));
        }
    }

    /// <summary>
    /// Event that changes the character's type based on the current map status
    /// </summary>
    [Serializable]
    public class NatureElementEvent : BattleEvent
    {
    
        /// <summary>
        /// The map status mapped to a type
        /// </summary>
        [JsonConverter(typeof(MapStatusElementDictConverter))]
        [DataType(1, DataManager.DataType.MapStatus, false)]
        [DataType(2, DataManager.DataType.Element, false)]
        public Dictionary<string, string> TerrainPair;

        public NatureElementEvent()
        {
            TerrainPair = new Dictionary<string, string>();
        }
        public NatureElementEvent(Dictionary<string, string> terrain)
        {
            TerrainPair = terrain;
        }
        protected NatureElementEvent(NatureElementEvent other)
            : this()
        {
            foreach (string terrain in other.TerrainPair.Keys)
                TerrainPair.Add(terrain, other.TerrainPair[terrain]);
        }
        public override GameEvent Clone() { return new NatureElementEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            foreach (string terrain in TerrainPair.Keys)
            {
                if (ZoneManager.Instance.CurrentMap.Status.ContainsKey(terrain))
                {
                    yield return CoroutineManager.Instance.StartCoroutine(context.Target.ChangeElement(TerrainPair[terrain], DataManager.Instance.DefaultElement));
                    yield break;
                }
            }

            if (ZoneManager.Instance.CurrentMap.Element != DataManager.Instance.DefaultElement)
                yield return CoroutineManager.Instance.StartCoroutine(context.Target.ChangeElement(ZoneManager.Instance.CurrentMap.Element, DataManager.Instance.DefaultElement));
        }
    }

    /// <summary>
    /// Event that changes the character's ability to the specified ability
    /// </summary>
    [Serializable]
    public class ChangeToAbilityEvent : BattleEvent
    {
        /// <summary>
        /// The ability to change to
        /// </summary>
        [JsonConverter(typeof(IntrinsicConverter))]
        [DataType(0, DataManager.DataType.Intrinsic, false)]
        public string TargetAbility;
        
        /// <summary>
        /// Whether to affect the target or user
        /// </summary>
        public bool AffectTarget;
        
        /// <summary>
        /// Whether to display a message if the ability failed to change 
        /// </summary>
        public bool SilentCheck;

        public ChangeToAbilityEvent() { TargetAbility = ""; }
        public ChangeToAbilityEvent(string ability, bool affectTarget) : this(ability, affectTarget, false)
        { }
        public ChangeToAbilityEvent(string ability, bool affectTarget, bool silentCheck)
        {
            TargetAbility = ability;
            AffectTarget = affectTarget;
            SilentCheck = silentCheck;
        }
        protected ChangeToAbilityEvent(ChangeToAbilityEvent other)
        {
            TargetAbility = other.TargetAbility;
            AffectTarget = other.AffectTarget;
            SilentCheck = other.SilentCheck;
        }
        public override GameEvent Clone() { return new ChangeToAbilityEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = (AffectTarget ? context.Target : context.User);
            //change to ability
            if (SilentCheck && target.Intrinsics[0].Element.ID == TargetAbility)
                yield break;

            yield return CoroutineManager.Instance.StartCoroutine(target.ReplaceIntrinsic(0, TargetAbility, true, false));
        }
    }
    
    /// <summary>
    /// Event that removes the specified ability of the character
    /// </summary>
    [Serializable]
    public class RemoveAbilityEvent : BattleEvent
    {
        /// <summary>
        /// The ability to check for
        /// </summary>
        [JsonConverter(typeof(IntrinsicConverter))]
        [DataType(0, DataManager.DataType.Intrinsic, false)]
        public string TargetAbility;

        public RemoveAbilityEvent() { TargetAbility = ""; }
        public RemoveAbilityEvent(string ability)
        {
            TargetAbility = ability;
        }
        protected RemoveAbilityEvent(RemoveAbilityEvent other)
        {
            TargetAbility = other.TargetAbility;
        }
        public override GameEvent Clone() { return new RemoveAbilityEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (TargetAbility == context.Target.Intrinsics[0].Element.ID)
                yield return CoroutineManager.Instance.StartCoroutine(context.Target.ReplaceIntrinsic(0, DataManager.Instance.DefaultIntrinsic, true, false));
        }
    }

    /// <summary>
    /// Event that causes the user copy the ability of the target
    /// </summary>
    [Serializable]
    public class ReflectAbilityEvent : BattleEvent
    {
        /// <summary>
        /// Whether the target copies the ability of the user
        /// </summary>
        public bool AffectTarget;
        
        /// <summary>
        /// The message displayed in the dungeon log 
        /// </summary>
        [StringKey(0, true)]
        public StringKey Msg;

        public ReflectAbilityEvent() { }
        public ReflectAbilityEvent(bool affectTarget) { AffectTarget = affectTarget; }
        public ReflectAbilityEvent(bool affectTarget, StringKey msg) { AffectTarget = affectTarget; Msg = msg; }
        protected ReflectAbilityEvent(ReflectAbilityEvent other)
        {
            AffectTarget = other.AffectTarget;
            Msg = other.Msg;
        }
        public override GameEvent Clone() { return new ReflectAbilityEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = (AffectTarget ? context.Target : context.User);
            Character origin = (AffectTarget ? context.User : context.Target);

            if (Msg.IsValid())
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(Msg.ToLocal(), origin.GetDisplayName(false), target.GetDisplayName(false), owner.GetDisplayName()));

            //reflect ability (target to attacker, or vice versa)
            for (int ii = 0; ii < CharData.MAX_INTRINSIC_SLOTS; ii++)
                yield return CoroutineManager.Instance.StartCoroutine(target.ReplaceIntrinsic(ii, origin.Intrinsics[ii].Element.ID));
        }
    }

    /// <summary>
    /// Event that causes the user to swap abilities with the target
    /// </summary>
    [Serializable]
    public class SwapAbilityEvent : BattleEvent
    {
        public override GameEvent Clone() { return new SwapAbilityEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            List<string> abilities = new List<string>();
            foreach (BackReference<Intrinsic> ability in context.Target.Intrinsics)
                abilities.Add(ability.Element.ID);

            //reflect ability (target to attacker, or vice versa)
            for (int ii = 0; ii < CharData.MAX_INTRINSIC_SLOTS; ii++)
                yield return CoroutineManager.Instance.StartCoroutine(context.Target.ReplaceIntrinsic(ii, context.User.Intrinsics[ii].Element.ID, true, false));
            for (int ii = 0; ii < CharData.MAX_INTRINSIC_SLOTS; ii++)
                yield return CoroutineManager.Instance.StartCoroutine(context.User.ReplaceIntrinsic(ii, abilities[ii], true, false));
        }
    }

    /// <summary>
    /// Event that causes the character to swap its attack with its defense stats
    /// </summary>
    [Serializable]
    public class PowerTrickEvent : BattleEvent
    {
        public override GameEvent Clone() { return new PowerTrickEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int atk = context.Target.Atk;
            context.Target.ProxyAtk = context.Target.Def;
            context.Target.ProxyDef = atk;
            DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_STAT_SWAP").ToLocal(), context.User.GetDisplayName(false),
                Text.FormatGrammar(new StringKey("BASE_STAT").ToLocal(), Stat.Attack.ToLocal()),
                Text.FormatGrammar(new StringKey("BASE_STAT").ToLocal(), Stat.Defense.ToLocal())));
            yield break;
        }
    }
    
    /// <summary>
    /// Event that averages the defense or attack stats of the user and target
    /// </summary>
    [Serializable]
    public class StatSplitEvent : BattleEvent
    {
        /// <summary>
        /// Whether to split the attack stats instead
        /// </summary>
        public bool AttackStats;

        public StatSplitEvent() { }
        public StatSplitEvent(bool attack)
        {
            AttackStats = attack;
        }
        protected StatSplitEvent(StatSplitEvent other)
        {
            AttackStats = other.AttackStats;
        }
        public override GameEvent Clone() { return new StatSplitEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int phys = (AttackStats ? (context.User.Atk + context.Target.Atk) : (context.User.Def + context.Target.Def)) / 2;
            int spec = (AttackStats ? (context.User.MAtk + context.Target.MAtk) : (context.User.MDef + context.Target.MDef)) / 2;
            if (AttackStats)
            {
                context.User.ProxyAtk = phys;
                context.Target.ProxyAtk = phys;
                context.User.ProxyMAtk = spec;
                context.Target.ProxyMAtk = spec;
                string[] stats = new string[2];
                stats[0] = Text.FormatGrammar(new StringKey("BASE_STAT").ToLocal(), Stat.Attack.ToLocal());
                stats[1] = Text.FormatGrammar(new StringKey("BASE_STAT").ToLocal(), Stat.MAtk.ToLocal());
                string list = Text.BuildList(stats);
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_STAT_SPLIT").ToLocal(), context.User.GetDisplayName(false), list, context.Target.GetDisplayName(false)));
            }
            else
            {
                context.User.ProxyDef = phys;
                context.Target.ProxyDef = phys;
                context.User.ProxyMDef = spec;
                context.Target.ProxyMDef = spec;
                string[] stats = new string[2];
                stats[0] = Text.FormatGrammar(new StringKey("BASE_STAT").ToLocal(), Stat.Defense.ToLocal());
                stats[1] = Text.FormatGrammar(new StringKey("BASE_STAT").ToLocal(), Stat.MDef.ToLocal());
                string list = Text.BuildList(stats);
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_STAT_SPLIT").ToLocal(), context.User.GetDisplayName(false), list, context.Target.GetDisplayName(false)));
            }
            yield break;
        }
    }
    
    /// <summary>
    /// Event that causes the user to swap its speed stat with the target
    /// </summary>
    [Serializable]
    public class SpeedSwapEvent : BattleEvent
    {
        public SpeedSwapEvent() { }
        protected SpeedSwapEvent(SpeedSwapEvent other)
        {
        }
        public override GameEvent Clone() { return new SpeedSwapEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int speed = context.User.Speed;
            context.User.ProxySpeed = context.Target.Speed;
            context.Target.ProxySpeed = speed;
            DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_STAT_SWAP_OTHER").ToLocal(), context.User.GetDisplayName(false),
                Text.FormatGrammar(new StringKey("BASE_STAT").ToLocal(), Stat.Speed.ToLocal()), context.Target.GetDisplayName(false)));
            yield break;
        }
    }

    /// <summary>
    /// Event that adds the user's and target's HP, then splits the combined HP
    /// </summary>
    [Serializable]
    public class PainSplitEvent : BattleEvent
    {
        public override GameEvent Clone() { return new PainSplitEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int hp = (context.User.HP + context.Target.HP) / 2;

            context.User.HP = Math.Min(hp, context.User.MaxHP);
            context.Target.HP = Math.Min(hp, context.Target.MaxHP);
            DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_HP_SPLIT").ToLocal(), context.User.GetDisplayName(false), context.Target.GetDisplayName(false)));
            yield break;
        }
    }
    
    /// <summary>
    /// Event that causes the user to copy the stat boosts/drops of the target
    /// </summary>
    [Serializable]
    public class ReflectStatsEvent : BattleEvent
    {
        /// <summary>
        /// The list of stats to copy from the target
        /// </summary>
        [JsonConverter(typeof(StatusSetConverter))]
        [DataType(1, DataManager.DataType.Status, false)]
        public HashSet<string> StatusIDs;

        public ReflectStatsEvent() { StatusIDs = new HashSet<string>(); }
        public ReflectStatsEvent(HashSet<string> statusIDs)
        {
            StatusIDs = statusIDs;
        }
        protected ReflectStatsEvent(ReflectStatsEvent other)
            : this()
        {
            foreach (string statusID in other.StatusIDs)
                StatusIDs.Add(statusID);
        }
        public override GameEvent Clone() { return new ReflectStatsEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            foreach (string statusID in StatusIDs)
            {
                //silently remove all stat changes on the user
                yield return CoroutineManager.Instance.StartCoroutine(context.User.RemoveStatusEffect(statusID, false));
                //silently add all stat changes from target to user
                StatusEffect testStatus = context.Target.GetStatusEffect(statusID);

                if (testStatus != null)
                {
                    StatusEffect status = new StatusEffect(statusID);
                    status.LoadFromData();
                    status.StatusStates.GetWithDefault<StackState>().Stack = testStatus.StatusStates.GetWithDefault<StackState>().Stack;
                    yield return CoroutineManager.Instance.StartCoroutine(context.User.AddStatusEffect(context.User, status, false));
                }
            }
            DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_BUFF_COPY").ToLocal(), context.User.GetDisplayName(false), context.Target.GetDisplayName(false)));
        }
    }
    
    /// <summary>
    /// Event that causes the user to swap stat changes with the target
    /// </summary>
    [Serializable]
    public class SwapStatsEvent : BattleEvent
    {
        
        /// <summary>
        /// The list of stats swap with the target
        /// </summary>
        [JsonConverter(typeof(StatusSetConverter))]
        [DataType(1, DataManager.DataType.Status, false)]
        public HashSet<string> StatusIDs;

        public SwapStatsEvent() { StatusIDs = new HashSet<string>(); }
        public SwapStatsEvent(HashSet<string> statusIDs)
        {
            StatusIDs = statusIDs;
        }
        protected SwapStatsEvent(SwapStatsEvent other)
            : this()
        {
            foreach (string statusID in other.StatusIDs)
                StatusIDs.Add(statusID);
        }
        public override GameEvent Clone() { return new SwapStatsEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            foreach (string statusID in StatusIDs)
            {
                //get the stat changes of both sides
                StatusEffect userStatus = context.User.GetStatusEffect(statusID);
                StatusEffect targetStatus = context.Target.GetStatusEffect(statusID);

                int userStack = (userStatus != null) ? userStatus.StatusStates.GetWithDefault<StackState>().Stack : 0;
                int targetStack = (targetStatus != null) ? targetStatus.StatusStates.GetWithDefault<StackState>().Stack : 0;

                //remove the changes
                yield return CoroutineManager.Instance.StartCoroutine(context.User.RemoveStatusEffect(statusID, false));
                yield return CoroutineManager.Instance.StartCoroutine(context.Target.RemoveStatusEffect(statusID, false));

                //grant the changes
                if (userStack != 0)
                {
                    StatusEffect status = new StatusEffect(statusID);
                    status.LoadFromData();
                    status.StatusStates.GetWithDefault<StackState>().Stack = userStack;
                    yield return CoroutineManager.Instance.StartCoroutine(context.Target.AddStatusEffect(context.Target, status, false));
                }
                if (targetStack != 0)
                {
                    StatusEffect status = new StatusEffect(statusID);
                    status.LoadFromData();
                    status.StatusStates.GetWithDefault<StackState>().Stack = targetStack;
                    yield return CoroutineManager.Instance.StartCoroutine(context.User.AddStatusEffect(context.User, status, false));
                }
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_BUFF_SWAP").ToLocal(), context.User.GetDisplayName(false), context.Target.GetDisplayName(false), DataManager.Instance.GetStatus(statusID).GetColoredName()));
            }
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
    /// Event that restores the character back to its original form
    /// </summary>
    [Serializable]
    public class RestoreFormEvent : BattleEvent
    {
        public override GameEvent Clone() { return new RestoreFormEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Target.Dead)
                yield break;

            context.Target.RestoreForm();
        }
    }
    
    /// <summary>
    /// Event that causes the user to transform to the target
    /// </summary>
    [Serializable]
    public class TransformEvent : BattleEvent
    {
        /// <summary>
        /// Whether the target transforms to the user instead
        /// </summary>
        public bool AffectTarget;
        
        /// <summary>
        /// The transformed status
        /// </summary>
        [DataType(0, DataManager.DataType.Status, false)]
        public string StatusID;

        public int TransformCharges;

        public TransformEvent() { StatusID = ""; }
        public TransformEvent(bool affectTarget, string status, int transformCharges)
        {
            AffectTarget = affectTarget;
            StatusID = status;
            TransformCharges = transformCharges;
        }
        protected TransformEvent(TransformEvent other)
        {
            AffectTarget = other.AffectTarget;
            StatusID = other.StatusID;
            TransformCharges = other.TransformCharges;
        }
        public override GameEvent Clone() { return new TransformEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = (AffectTarget ? context.Target : context.User);
            Character user = (AffectTarget ? context.User : context.Target);

            if (target.Dead || user.Dead)
                yield break;

            StatusEffect transform = target.GetStatusEffect(StatusID);
            if (transform != null)
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_ALREADY_TRANSFORMED").ToLocal(), target.GetDisplayName(false)));
                yield break;
            }
            if (target.CurrentForm.Species == user.CurrentForm.Species)
            {
                MonsterData entry = DataManager.Instance.GetMonster(target.CurrentForm.Species);
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_ALREADY_HAS_SPECIES").ToLocal(), target.GetDisplayName(false), entry.GetColoredName()));
                yield break;
            }

            int hp = target.HP;

            target.Transform(user.CurrentForm);

            //proxy stats
            target.ProxyAtk = user.Atk;
            target.ProxyDef = user.Def;
            target.ProxyMAtk = user.MAtk;
            target.ProxyMDef = user.MDef;
            target.ProxySpeed = user.Speed;

            //ability
            for (int ii = 0; ii < CharData.MAX_INTRINSIC_SLOTS; ii++)
                yield return CoroutineManager.Instance.StartCoroutine(target.ReplaceIntrinsic(ii, user.Intrinsics[ii].Element.ID, false, false));

            //type
            yield return CoroutineManager.Instance.StartCoroutine(target.ChangeElement(user.Element1, user.Element2, false, false));

            //moves
            for (int ii = 0; ii < CharData.MAX_SKILL_SLOTS; ii++)
                target.ChangeSkill(ii, user.Skills[ii].Element.SkillNum, TransformCharges);

            //set the status
            if (!String.IsNullOrEmpty(StatusID))
            {
                StatusEffect setStatus = new StatusEffect(StatusID);
                setStatus.LoadFromData();
                setStatus.StatusStates.Set(new HPState(hp));
                yield return CoroutineManager.Instance.StartCoroutine(target.AddStatusEffect(null, setStatus, false));
            }

            DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_TRANSFORM").ToLocal(), target.GetDisplayName(false), user.GetDisplayName(false)));
        }
    }

    /// <summary>
    /// Event that devolves the target
    /// </summary>
    [Serializable]
    public class DevolveEvent : BattleEvent
    {
        /// <summary>
        /// Whether to display a message if the target cannot devolve
        /// </summary>
        public bool SilentCheck;

        [DataType(0, DataManager.DataType.Status, false)]
        public string StatusID;
        
        /// <summary>
        /// The list of battle VFXs played if the condition is met
        /// </summary>
        public List<BattleAnimEvent> Anims;
        public int TransformCharges;

        public DevolveEvent() { Anims = new List<BattleAnimEvent>(); }
        public DevolveEvent(bool silentCheck, string status, int transformCharges, params BattleAnimEvent[] anims) : this()
        {
            SilentCheck = silentCheck;
            StatusID = status;
            TransformCharges = transformCharges;
            Anims.AddRange(anims);
        }
        public DevolveEvent(DevolveEvent other) : this()
        {
            SilentCheck = other.SilentCheck;
            StatusID = other.StatusID;
            TransformCharges = other.TransformCharges;
            foreach (BattleAnimEvent anim in other.Anims)
                Anims.Add((BattleAnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new DevolveEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Target.Dead)
                yield break;

            MonsterData candidateDex = DataManager.Instance.GetMonster(context.Target.CurrentForm.Species);
            BaseMonsterForm candidateForm = candidateDex.Forms[context.Target.CurrentForm.Form];

            if (!String.IsNullOrEmpty(candidateDex.PromoteFrom))
            {
                int hp = context.Target.HP;

                string prevName = context.Target.GetDisplayName(false);
                MonsterID prevoData = context.Target.CurrentForm;
                prevoData.Species = candidateDex.PromoteFrom;
                prevoData.Form = candidateForm.PromoteForm;
                context.Target.Transform(prevoData);

                MonsterData dex = DataManager.Instance.GetMonster(context.Target.CurrentForm.Species);
                BaseMonsterForm forme = dex.Forms[context.Target.CurrentForm.Form];
                //moves
                List<string> final_moves = forme.RollLatestSkills(context.Target.Level * 1 / 2 + 1, new List<string>());
                for (int ii = 0; ii < CharData.MAX_SKILL_SLOTS; ii++)
                {
                    if (ii < final_moves.Count)
                        context.Target.ChangeSkill(ii, final_moves[ii], TransformCharges);
                    else
                        context.Target.ChangeSkill(ii, "", -1);
                }

                //set the status
                if (!String.IsNullOrEmpty(StatusID))
                {
                    StatusEffect setStatus = new StatusEffect(StatusID);
                    setStatus.LoadFromData();
                    setStatus.StatusStates.Set(new HPState(hp));
                    yield return CoroutineManager.Instance.StartCoroutine(context.Target.AddStatusEffect(null, setStatus, false));
                }

                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_DEVOLVE").ToLocal(), prevName, dex.GetColoredName()));

                foreach (BattleAnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

            }
            else
            {
                if (!SilentCheck)
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_DEVOLVE_FAIL").ToLocal(), context.Target.GetDisplayName(false)));
            }
        }
    }
    
    /// <summary>
    /// Event that turns the target into an item from the current map's spawn pool
    /// </summary>
    [Serializable]
    public class ItemizerEvent : BattleEvent
    {
        public ItemizerEvent() { }
        public override GameEvent Clone() { return new ItemizerEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Target.Dead)
                yield break;

            if (ZoneManager.Instance.CurrentMap.ItemSpawns.CanPick)
            {
                //remove the target
                yield return CoroutineManager.Instance.StartCoroutine(context.Target.DieSilent());

                //drop an item
                InvItem item = ZoneManager.Instance.CurrentMap.ItemSpawns.Pick(DataManager.Instance.Save.Rand);
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.DropItem(item, context.Target.CharLoc));
            }
            else
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_NOTHING_HAPPENED").ToLocal()));
        }
    }

    /// <summary>
    /// Event that causes the item to land where the strike hitbox ended
    /// </summary>
    [Serializable]
    public class LandItemEvent : BattleEvent
    {
        public override GameEvent Clone() { return new LandItemEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if ((context.GetContextStateInt<AttackHitTotal>(true, 0) == 0) && !context.GlobalContextStates.Contains<ItemDestroyed>())
            {
                foreach (Loc tile in context.StrikeLandTiles)
                    yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.DropItem(context.Item, tile));
            }
        }
    }

    /// <summary>
    /// Event that pulls unclaimed items on the floor to the user.
    /// </summary>
    [Serializable]
    public class TrawlEvent : BattleEvent
    {
        public override GameEvent Clone() { return new TrawlEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Dictionary<Loc, int> itemLocs = new Dictionary<Loc, int>();
            for (int ii = 0; ii < ZoneManager.Instance.CurrentMap.Items.Count; ii++)
                itemLocs.Add(ZoneManager.Instance.CurrentMap.Items[ii].TileLoc, ii);
            Loc?[] chosenItems = new Loc?[ZoneManager.Instance.CurrentMap.Items.Count];

            TerrainData.Mobility mobility = TerrainData.Mobility.Lava | TerrainData.Mobility.Water | TerrainData.Mobility.Abyss;

            Grid.AffectConnectedTiles(context.User.CharLoc - new Loc(CharAction.MAX_RANGE), new Loc(CharAction.MAX_RANGE * 2 + 1),
                (Loc effectLoc) =>
                {
                    if (ZoneManager.Instance.CurrentMap.TileBlocked(effectLoc, mobility))
                        return;

                    Loc wrapLoc = ZoneManager.Instance.CurrentMap.WrapLoc(effectLoc);
                    if (itemLocs.ContainsKey(wrapLoc))
                        chosenItems[itemLocs[wrapLoc]] = effectLoc;
                },
                (Loc testLoc) =>
                {
                    return ZoneManager.Instance.CurrentMap.TileBlocked(testLoc, true);
                },
                (Loc testLoc) =>
                {
                    return ZoneManager.Instance.CurrentMap.TileBlocked(testLoc, true, true);
                },
                context.User.CharLoc);

            for (int ii = ZoneManager.Instance.CurrentMap.Items.Count - 1; ii >= 0; ii--)
            {
                if (chosenItems[ii] != null)
                {
                    MapItem item = ZoneManager.Instance.CurrentMap.Items[ii];
                    Loc? newLoc = ZoneManager.Instance.CurrentMap.FindItemlessTile(context.User.CharLoc, CharAction.MAX_RANGE, true);
                    if (newLoc != null)
                    {
                        ItemAnim itemAnim = new ItemAnim(chosenItems[ii].Value * GraphicsManager.TileSize + new Loc(GraphicsManager.TileSize / 2), newLoc.Value * GraphicsManager.TileSize + new Loc(GraphicsManager.TileSize / 2), item.IsMoney ? GraphicsManager.MoneySprite : DataManager.Instance.GetItem(item.Value).Sprite, GraphicsManager.TileSize / 2, 1);
                        DungeonScene.Instance.CreateAnim(itemAnim, DrawLayer.Normal);
                        item.TileLoc = ZoneManager.Instance.CurrentMap.WrapLoc(newLoc.Value);
                    }
                    else
                        chosenItems[ii] = null;
                }
            }
            List<MapItem> unclaimed_items = new List<MapItem>();
            for (int ii = ZoneManager.Instance.CurrentMap.Items.Count - 1; ii >= 0; ii--)
            {
                if (chosenItems[ii] != null)
                {
                    MapItem item = ZoneManager.Instance.CurrentMap.Items[ii];
                    unclaimed_items.Add(item);
                    ZoneManager.Instance.CurrentMap.Items.RemoveAt(ii);
                }
            }
            DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_TRAWL").ToLocal(), context.User.GetDisplayName(false)));
            yield return new WaitForFrames(ItemAnim.ITEM_ACTION_TIME);
            foreach (MapItem item in unclaimed_items)
                ZoneManager.Instance.CurrentMap.Items.Add(item);
        }
    }

    /// <summary>
    /// Event that sets the character and tile sight to be clear
    /// </summary>
    [Serializable]
    public class LuminousEvent : BattleEvent
    {
        public override GameEvent Clone() { return new LuminousEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            ZoneManager.Instance.CurrentMap.CharSight = Map.SightRange.Clear;
            ZoneManager.Instance.CurrentMap.TileSight = Map.SightRange.Clear;
            yield break;
        }
    }

    /// <summary>
    /// Event that hints all unexplored locations on the map
    /// </summary>
    [Serializable]
    public class MapOutEvent : BattleEvent
    {
        public override GameEvent Clone() { return new MapOutEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Loc testTile = context.TargetTile;
            if (!ZoneManager.Instance.CurrentMap.GetLocInMapBounds(ref testTile))
                yield break;

            if (ZoneManager.Instance.CurrentMap.DiscoveryArray[testTile.X][testTile.Y] == Map.DiscoveryState.None)
                ZoneManager.Instance.CurrentMap.DiscoveryArray[testTile.X][testTile.Y] = Map.DiscoveryState.Hinted;

        }
    }
    
    /// <summary>
    /// Event that hints all unexplored locations on the map within the specified radius
    /// </summary>
    [Serializable]
    public class MapOutRadiusEvent : BattleEvent
    {
        /// <summary>
        /// The radius around the user to hint
        /// </summary>
        public int Radius;

        public MapOutRadiusEvent() { }
        public MapOutRadiusEvent(int radius)
        {
            Radius = radius;
        }
        protected MapOutRadiusEvent(MapOutRadiusEvent other)
        {
            Radius = other.Radius;
        }
        public override GameEvent Clone() { return new MapOutRadiusEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {

            for (int ii = 1; ii <= 25; ii++)
            {
                int limitSquared = Radius * Radius * ii * ii / 25 / 25;
                for (int xx = -Radius; xx <= Radius; xx++)
                {
                    for (int yy = -Radius; yy <= Radius; yy++)
                    {
                        Loc diff = new Loc(xx, yy);
                        if (diff.DistSquared() < limitSquared)
                        {
                            Loc loc = context.User.CharLoc + diff;
                            if (!ZoneManager.Instance.CurrentMap.GetLocInMapBounds(ref loc))
                                continue;
                            if (ZoneManager.Instance.CurrentMap.DiscoveryArray[loc.X][loc.Y] == Map.DiscoveryState.None)
                                ZoneManager.Instance.CurrentMap.DiscoveryArray[loc.X][loc.Y] = Map.DiscoveryState.Hinted;
                        }
                    }
                }
                yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(2));
            }

        }
    }

    /// <summary>
    /// Event that reveals the tile and queues its affects
    /// </summary>
    [Serializable]
    public class TilePostEvent : BattleEvent
    {
        public override GameEvent Clone() { return new TilePostEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Trap)
                yield break;

            if (context.Data.Category == BattleData.SkillCategory.None)
                yield break;

            Tile tile = ZoneManager.Instance.CurrentMap.GetTile(context.TargetTile);
            if (tile == null)
                yield break;

            if (!String.IsNullOrEmpty(tile.Effect.ID) && ZoneManager.Instance.CurrentMap.GetTileOwner(context.User) != tile.Effect.Owner)
            {
                TileData entry = DataManager.Instance.GetTile(tile.Effect.GetID());
                if (entry.StepType == TileData.TriggerType.Trap)
                {
                    if (context.ActionType == BattleActionType.Skill && context.Data.ID == DataManager.Instance.DefaultSkill)
                        tile.Effect.Revealed = true;
                    else if (tile.Effect.Owner != EffectTile.TileOwner.None && ZoneManager.Instance.CurrentMap.GetTileOwner(context.Target) == tile.Effect.Owner)
                    {
                        //sort of a hack, meant to prevent the following scenario:
                        //character A sets a trap underfoot and owns it, expecting the trap to not hurt it
                        //character B attacks character A, hitting the tile with the owner on it
                        //character A takes the effect of the trap

                        //this is a fall-through case
                    }
                    else
                    {
                        DungeonScene.Instance.QueueTrap(context.TargetTile);
                        //yield return CoroutineManager.Instance.StartCoroutine(tile.Effect.InteractWithTile(context.User));
                    }
                }
                else
                {
                    if (!tile.Effect.Revealed)
                    {
                        GameManager.Instance.BattleSE("DUN_Smokescreen");
                        SingleEmitter emitter = new SingleEmitter(new AnimData("Puff_Brown", 3));
                        emitter.Layer = DrawLayer.Front;
                        emitter.SetupEmit(context.TargetTile * GraphicsManager.TileSize + new Loc(GraphicsManager.TileSize / 2), context.TargetTile * GraphicsManager.TileSize + new Loc(GraphicsManager.TileSize / 2), context.User.CharDir);
                        DungeonScene.Instance.CreateAnim(emitter, DrawLayer.NoDraw);
                        tile.Effect.Revealed = true;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Event that sets the ground tile with the specified trap 
    /// </summary>
    [Serializable]
    public class SetTrapEvent : BattleEvent
    {
        /// <summary>
        /// The trap being added 
        /// </summary>
        [JsonConverter(typeof(TileConverter))]
        [DataType(0, DataManager.DataType.Tile, false)]
        public string TrapID;

        public SetTrapEvent() { }
        public SetTrapEvent(string trapID)
        {
            TrapID = trapID;
        }
        protected SetTrapEvent(SetTrapEvent other)
        {
            TrapID = other.TrapID;
        }
        public override GameEvent Clone() { return new SetTrapEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Tile tile = ZoneManager.Instance.CurrentMap.GetTile(context.TargetTile);
            if (tile == null)
                yield break;

            if (tile.Data.GetData().BlockType == TerrainData.Mobility.Passable && String.IsNullOrEmpty(tile.Effect.ID))
            {
                tile.Effect = new EffectTile(TrapID, true, tile.Effect.TileLoc);
                tile.Effect.Owner = ZoneManager.Instance.CurrentMap.GetTileOwner(context.User);
            }
        }
    }

    /// <summary>
    /// Event that sets the ground tile with the specified trap at the character's location 
    /// </summary>
    [Serializable]
    public class CounterTrapEvent : BattleEvent
    {
        /// <summary>
        /// The trap being added 
        /// </summary>
        [JsonConverter(typeof(TileConverter))]
        [DataType(0, DataManager.DataType.Tile, false)]
        public string TrapID;

        public CounterTrapEvent() { }
        public CounterTrapEvent(string trapID)
        {
            TrapID = trapID;
        }
        protected CounterTrapEvent(CounterTrapEvent other)
        {
            TrapID = other.TrapID;
        }
        public override GameEvent Clone() { return new CounterTrapEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (!Collision.InBounds(ZoneManager.Instance.CurrentMap.Width, ZoneManager.Instance.CurrentMap.Height, context.Target.CharLoc))
                yield break;

            Tile tile = ZoneManager.Instance.CurrentMap.Tiles[context.Target.CharLoc.X][context.Target.CharLoc.Y];
            if (tile.Data.GetData().BlockType == TerrainData.Mobility.Passable && String.IsNullOrEmpty(tile.Effect.ID))
            {
                tile.Effect = new EffectTile(TrapID, true, context.Target.CharLoc);
                tile.Effect.Owner = ZoneManager.Instance.CurrentMap.GetTileOwner(context.Target);
            }
        }
    }
    
    /// <summary>
    /// Event that triggers the effects of the trap tile
    /// </summary>
    [Serializable]
    public class TriggerTrapEvent : BattleEvent
    {
        /// <summary>
        /// The trap to ignore triggering
        /// </summary>
        [DataType(0, DataManager.DataType.Tile, false)]
        public string ExceptID;

        public TriggerTrapEvent() { }
        public TriggerTrapEvent(string exceptID) { ExceptID = exceptID; }
        public TriggerTrapEvent(TriggerTrapEvent other)
        {
            ExceptID = other.ExceptID;
        }
        public override GameEvent Clone() { return new TriggerTrapEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Tile tile = ZoneManager.Instance.CurrentMap.GetTile(context.TargetTile);
            if (tile == null)
                yield break;

            if (!String.IsNullOrEmpty(tile.Effect.ID) && tile.Effect.ID != ExceptID)
            {
                TileData entry = DataManager.Instance.GetTile(tile.Effect.GetID());
                if (entry.StepType == TileData.TriggerType.Trap)
                {
                    SingleCharContext singleContext = new SingleCharContext(context.User);
                    yield return CoroutineManager.Instance.StartCoroutine(tile.Effect.InteractWithTile(singleContext));
                }
            }
        }
    }

    /// <summary>
    /// Event that makes the trap revealed
    /// </summary>
    [Serializable]
    public class RevealTrapEvent : BattleEvent
    {
        public override GameEvent Clone() { return new RevealTrapEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Tile tile = ZoneManager.Instance.CurrentMap.GetTile(context.TargetTile);
            if (tile == null)
                yield break;

            if (!String.IsNullOrEmpty(tile.Effect.ID))
                tile.Effect.Revealed = true;
        }
    }

    /// <summary>
    /// Event that removes the trap
    /// </summary>
    [Serializable]
    public class RemoveTrapEvent : BattleEvent
    {
        public override GameEvent Clone() { return new RemoveTrapEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Tile tile = ZoneManager.Instance.CurrentMap.GetTile(context.TargetTile);
            if (tile == null)
                yield break;

            if (!String.IsNullOrEmpty(tile.Effect.ID))
            {
                TileData entry = DataManager.Instance.GetTile(tile.Effect.GetID());
                if (entry.StepType == TileData.TriggerType.Trap)
                    tile.Effect = new EffectTile(tile.Effect.TileLoc);
            }
        }
    }


    /// <summary>
    /// Event that changes terrain of one type to another type.
    /// </summary>
    [Serializable]
    public class ChangeTerrainEvent : BattleEvent
    {
        [DataType(0, DataManager.DataType.Terrain, false)]
        public string TerrainFrom;

        [DataType(0, DataManager.DataType.Terrain, false)]
        public string TerrainTo;

        public ChangeTerrainEvent()
        {
            TerrainFrom = "";
            TerrainTo = "";
        }

        public ChangeTerrainEvent(string terrainFrom, string terrainTo)
        {
            TerrainFrom = "";
            TerrainTo = "";
        }
        protected ChangeTerrainEvent(ChangeTerrainEvent other)
        {
            TerrainFrom = other.TerrainFrom;
            TerrainTo = other.TerrainTo;
        }
        public override GameEvent Clone() { return new ChangeTerrainEvent(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Tile tile = ZoneManager.Instance.CurrentMap.GetTile(context.TargetTile);
            if (tile.ID != TerrainFrom)
                yield break;

            tile.Data = new TerrainTile(TerrainTo);
            int distance = 0;
            Loc startLoc = context.TargetTile - new Loc(distance + 2);
            Loc sizeLoc = new Loc((distance + 2) * 2 + 1);
            ZoneManager.Instance.CurrentMap.MapModified(startLoc, sizeLoc);
        }
    }


    [Serializable]
    public abstract class RemoveTerrainBaseEvent : BattleEvent
    {
        /// <summary>
        /// The remove terrain SFX
        /// </summary>
        [Sound(0)]
        public string RemoveSound;
        
        /// <summary>
        /// The particle VFX
        /// </summary>
        public FiniteEmitter RemoveAnim;

        public RemoveTerrainBaseEvent()
        {
            RemoveAnim = new EmptyFiniteEmitter();
        }
        public RemoveTerrainBaseEvent(string removeSound, FiniteEmitter removeAnim)
            : this()
        {
            RemoveSound = removeSound;
            RemoveAnim = removeAnim;
        }
        protected RemoveTerrainBaseEvent(RemoveTerrainBaseEvent other) : this()
        {
            RemoveSound = other.RemoveSound;
            RemoveAnim = (FiniteEmitter)other.RemoveAnim.Clone();
        }

        protected abstract bool ShouldRemove(Tile tile);

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Tile tile = ZoneManager.Instance.CurrentMap.GetTile(context.TargetTile);
            if (!ShouldRemove(tile))
                yield break;

            if (context.Target == null)
            {
                GameManager.Instance.BattleSE(RemoveSound);
                FiniteEmitter emitter = (FiniteEmitter)RemoveAnim.Clone();
                emitter.SetupEmit(context.TargetTile * GraphicsManager.TileSize + new Loc(GraphicsManager.TileSize / 2), context.TargetTile * GraphicsManager.TileSize + new Loc(GraphicsManager.TileSize / 2), context.User.CharDir);
                DungeonScene.Instance.CreateAnim(emitter, DrawLayer.NoDraw);
            }

            tile.Data = new TerrainTile(DataManager.Instance.GenFloor);
            int distance = 0;
            Loc startLoc = context.TargetTile - new Loc(distance + 2);
            Loc sizeLoc = new Loc((distance + 2) * 2 + 1);
            ZoneManager.Instance.CurrentMap.MapModified(startLoc, sizeLoc);
        }
    }

    /// <summary>
    /// Event that removes the specified terrain and replaces it with a floor tile, replacing it with a floor tile
    /// </summary>
    [Serializable]
    public class RemoveTerrainEvent : RemoveTerrainBaseEvent
    {
        /// <summary>
        /// The list of terrains that can be removed
        /// </summary>
        [JsonConverter(typeof(TerrainSetConverter))]
        public HashSet<string> TileTypes;

        public RemoveTerrainEvent()
        {
            TileTypes = new HashSet<string>();
        }
        public RemoveTerrainEvent(string removeSound, FiniteEmitter removeAnim, params string[] tileTypes)
            : base(removeSound, removeAnim)
        {
            TileTypes = new HashSet<string>();
            foreach (string tileType in tileTypes)
                TileTypes.Add(tileType);
        }
        protected RemoveTerrainEvent(RemoveTerrainEvent other) : base(other)
        {
            TileTypes = new HashSet<string>();
            foreach (string tileType in other.TileTypes)
                TileTypes.Add(tileType);
        }
        public override GameEvent Clone() { return new RemoveTerrainEvent(this); }


        protected override bool ShouldRemove(Tile tile)
        {
            if (tile == null)
                return false;
            return TileTypes.Contains(tile.Data.ID);
        }
    }

    /// <summary>
    /// Event that removes the terrain if it contains one of the specified TerrainStates, replacing it with a floor tile
    /// </summary>
    [Serializable]
    public class RemoveTerrainStateEvent : RemoveTerrainBaseEvent
    {
        [StringTypeConstraint(1, typeof(TerrainState))]
        public List<FlagType> States;

        public RemoveTerrainStateEvent()
        {
            States = new List<FlagType>();
        }

        public RemoveTerrainStateEvent(string removeSound, FiniteEmitter removeAnim, params FlagType[] flagTypes)
            : base(removeSound, removeAnim)
        {
            States = new List<FlagType>();
            States.AddRange(flagTypes);
        }
        protected RemoveTerrainStateEvent(RemoveTerrainStateEvent other) : base(other)
        {
            States = new List<FlagType>();
            States.AddRange(other.States);
        }
        public override GameEvent Clone() { return new RemoveTerrainStateEvent(this); }


        protected override bool ShouldRemove(Tile tile)
        {
            if (tile == null)
                return false;

            TerrainData terrain = DataManager.Instance.GetTerrain(tile.Data.ID);
            
            foreach (FlagType state in States)
            {
                if (terrain.TerrainStates.Contains(state.FullType))
                    return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Event that removes the specified terrain and the area around it, replacing it with a floor tile
    /// </summary>
    [Serializable]
    public class ShatterTerrainEvent : BattleEvent
    {
        [JsonConverter(typeof(TerrainSetConverter))]
        public HashSet<string> TileTypes;

        public ShatterTerrainEvent() { TileTypes = new HashSet<string>(); }
        public ShatterTerrainEvent(params string[] tileTypes)
            : this()
        {
            foreach (string tileType in tileTypes)
                TileTypes.Add(tileType);
        }
        protected ShatterTerrainEvent(ShatterTerrainEvent other)
        {
            TileTypes = new HashSet<string>();
            foreach (string tileType in other.TileTypes)
                TileTypes.Add(tileType);
        }
        public override GameEvent Clone() { return new ShatterTerrainEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Tile tile = ZoneManager.Instance.CurrentMap.GetTile(context.TargetTile);
            if (tile == null)
                yield break;

            if (!TileTypes.Contains(tile.Data.ID))
                yield break;

            if (context.Target == null)
            {
                GameManager.Instance.BattleSE("DUN_Rollout");
                SingleEmitter emitter = new SingleEmitter(new AnimData("Rock_Smash", 2));
                emitter.SetupEmit(context.TargetTile * GraphicsManager.TileSize + new Loc(GraphicsManager.TileSize / 2), context.TargetTile * GraphicsManager.TileSize + new Loc(GraphicsManager.TileSize / 2), context.User.CharDir);
                DungeonScene.Instance.CreateAnim(emitter, DrawLayer.NoDraw);
            }

            //destroy the wall
            tile.Data = new TerrainTile(DataManager.Instance.GenFloor);
            for (int ii = 0; ii < DirExt.DIR4_COUNT; ii++)
            {
                Loc moveLoc = context.TargetTile + ((Dir4)ii).GetLoc();
                Tile sideTile = ZoneManager.Instance.CurrentMap.GetTile(moveLoc);
                if (sideTile != null && TileTypes.Contains(sideTile.Data.ID))
                    sideTile.Data = new TerrainTile(DataManager.Instance.GenFloor);
            }

            int distance = 0;
            Loc startLoc = context.TargetTile - new Loc(distance + 3);
            Loc sizeLoc = new Loc((distance + 3) * 2 + 1);
            ZoneManager.Instance.CurrentMap.MapModified(startLoc, sizeLoc);
        }
    }

    /// <summary>
    /// Event that destroys the item on the tile
    /// </summary>
    [Serializable]
    public class RemoveItemEvent : BattleEvent
    {
        /// <summary>
        /// Whether the item isn't destroyed if the tile has a terrain
        /// </summary>
        public bool BlockedByTerrain;

        public RemoveItemEvent()
        { }
        public RemoveItemEvent(bool blockable)
        {
            BlockedByTerrain = blockable;
        }
        protected RemoveItemEvent(RemoveItemEvent other)
        {
            BlockedByTerrain = other.BlockedByTerrain;
        }
        public override GameEvent Clone() { return new RemoveItemEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Tile tile = ZoneManager.Instance.CurrentMap.GetTile(context.TargetTile);
            if (tile == null)
                yield break;

            if (!BlockedByTerrain || tile.Data.ID == DataManager.Instance.GenFloor)
            {
                Loc wrappedLoc = ZoneManager.Instance.CurrentMap.WrapLoc(context.TargetTile);
                for (int ii = ZoneManager.Instance.CurrentMap.Items.Count - 1; ii >= 0; ii--)
                {
                    bool delete = true;
                    if (!ZoneManager.Instance.CurrentMap.Items[ii].IsMoney)
                    {
                        ItemData itemData = DataManager.Instance.GetItem(ZoneManager.Instance.CurrentMap.Items[ii].Value);
                        if (itemData.CannotDrop)
                            delete = false;
                    }
                    if (!delete)
                        continue;

                    if (ZoneManager.Instance.CurrentMap.Items[ii].TileLoc == wrappedLoc)
                        ZoneManager.Instance.CurrentMap.Items.RemoveAt(ii);
                }
            }
        }
    }
    
    /// <summary>
    /// Event that checks if the tile can be unlocked by checking if the item matches in the UnlockState tile state 
    /// </summary>
    [Serializable]
    public class KeyCheckEvent : BattleEvent
    {
        public KeyCheckEvent() { }
        public override GameEvent Clone() { return new KeyCheckEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.User.MemberTeam == DungeonScene.Instance.ActiveTeam)
            {
                if (context.User != context.User.MemberTeam.Leader)
                {
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_LEADER_ONLY_ITEM").ToLocal()));
                    context.CancelState.Cancel = true;
                }
                else
                {
                    //check if the tile in front can be unlocked
                    bool unlockable = false;
                    Loc hitLoc = context.User.CharLoc + context.User.CharDir.GetLoc();

                    Tile tile = ZoneManager.Instance.CurrentMap.GetTile(hitLoc);
                    if (tile != null && !String.IsNullOrEmpty(tile.Effect.ID))
                    {
                        TileData tileData = DataManager.Instance.GetTile(tile.Effect.ID);
                        if (tileData.StepType == TileData.TriggerType.Unlockable)
                        {
                            UnlockState unlock = tile.Effect.TileStates.GetWithDefault<UnlockState>();
                            if (unlock != null && unlock.UnlockItem == context.Item.ID)
                                unlockable = true;
                        }
                    }

                    if (!unlockable)
                    {
                        DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_KEY_MISS").ToLocal()));
                        context.CancelState.Cancel = true;
                    }
                }
            }
            else
                context.CancelState.Cancel = true;
            yield break;
        }
    }
    
    /// <summary>
    /// Event that applies the effects of the unlockable tile if the item matches in the UnlockState tile state 
    /// </summary>
    [Serializable]
    public class KeyUnlockEvent : BattleEvent
    {
        public KeyUnlockEvent() { }
        public override GameEvent Clone() { return new KeyUnlockEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Tile tile = ZoneManager.Instance.CurrentMap.GetTile(context.TargetTile);
            if (tile == null)
                yield break;

            if (!String.IsNullOrEmpty(tile.Effect.ID))
            {
                TileData entry = DataManager.Instance.GetTile(tile.Effect.GetID());
                if (entry.StepType == TileData.TriggerType.Unlockable)
                {
                    UnlockState unlock = tile.Effect.TileStates.GetWithDefault<UnlockState>();
                    if (unlock != null && unlock.UnlockItem == context.Item.ID)
                    {
                        SingleCharContext singleContext = new SingleCharContext(context.User);
                        yield return CoroutineManager.Instance.StartCoroutine(tile.Effect.InteractWithTile(singleContext));
                    }
                }
            }
        }
    }

    /// <summary>
    /// Event that teaches the user the move in the item's ItemIDState
    /// </summary>
    [Serializable]
    public class TMEvent : BattleEvent
    {
        public override GameEvent Clone() { return new TMEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            BaseMonsterForm entry = DataManager.Instance.GetMonster(context.User.BaseForm.Species).Forms[context.User.BaseForm.Form];
            ItemData item = DataManager.Instance.GetItem(owner.GetID());
            string moveIndex = "";
            ItemIDState state = item.ItemStates.GetWithDefault<ItemIDState>();
            if (state != null)
                moveIndex = state.ID;

            if (!entry.CanLearnSkill(moveIndex))
            {
                context.CancelState.Cancel = true;
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_CANT_LEARN_SKILL").ToLocal(), context.User.GetDisplayName(false)));
                yield break;
            }


            if (DataManager.Instance.CurrentReplay != null) // this block of code will never evaluate to true AND have UI read back -1 (cancel) at the same time
            {
                MoveLearnContext learn = new MoveLearnContext();
                learn.MoveLearn = moveIndex;
                learn.ReplaceSlot = DataManager.Instance.CurrentReplay.ReadUI();
                context.ContextStates.Set(learn);
            }
            else
            {
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.TryLearnSkill(context.User, moveIndex,
                (int slot) =>
                {
                    MoveLearnContext learn = new MoveLearnContext();
                    learn.MoveLearn = moveIndex;
                    learn.ReplaceSlot = slot;
                    context.ContextStates.Set(learn);
                },
                () => { context.CancelState.Cancel = true; }));

                if (!context.CancelState.Cancel)
                {
                    int slot = -1;
                    MoveLearnContext learn = context.ContextStates.GetWithDefault<MoveLearnContext>();
                    if (learn != null)
                        slot = learn.ReplaceSlot;
                    DataManager.Instance.LogUIPlay(slot);
                }
            }
        }

    }

    /// <summary>
    /// Event that prompts the user which form to change to and sets the value in SwitchFormContext
    /// </summary>
    [Serializable]
    public class FormChoiceEvent : BattleEvent
    {
        /// <summary>
        /// The required species for this event to have effect 
        /// </summary>
        [JsonConverter(typeof(MonsterConverter))]
        [DataType(0, DataManager.DataType.Monster, false)]
        public string Species;

        /// <summary>
        /// Whether to include temporary forms as an option 
        /// </summary>
        public bool IncludeTemp;

        public FormChoiceEvent() { Species = ""; }
        public FormChoiceEvent(string species) { Species = species; }
        public FormChoiceEvent(FormChoiceEvent other) { Species = other.Species; IncludeTemp = other.IncludeTemp; }
        public override GameEvent Clone() { return new FormChoiceEvent(this); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.User.MemberTeam == DungeonScene.Instance.ActiveTeam)
            {
                List<int> eligibleForms = new List<int>();
                MonsterData entry = DataManager.Instance.GetMonster(context.User.BaseForm.Species);
                if (context.User.BaseForm.Species == Species)
                {

                    for (int ii = 0; ii < entry.Forms.Count; ii++)
                    {
                        if (context.User.BaseForm.Form == ii)
                            continue;
                        BaseMonsterForm form = entry.Forms[ii];
                        if (!form.Released)
                            continue;
                        if (!IncludeTemp && form.Temporary)
                            continue;
                        eligibleForms.Add(ii);
                    }
                }

                if (eligibleForms.Count > 1)
                {
                    if (DataManager.Instance.CurrentReplay != null) // this block of code will never evaluate to true AND have UI read back -1 (cancel) at the same time
                    {
                        SwitchFormContext change = new SwitchFormContext();
                        change.Form = DataManager.Instance.CurrentReplay.ReadUI();
                        context.ContextStates.Set(change);
                    }
                    else
                    {
                        List<DialogueChoice> choices = new List<DialogueChoice>();
                        foreach (int form in eligibleForms)
                        {
                            choices.Add(new DialogueChoice(entry.Forms[form].FormName.ToLocal(), () =>
                            {
                                SwitchFormContext change = new SwitchFormContext();
                                change.Form = form;
                                context.ContextStates.Set(change);
                            }));
                        }

                        choices.Add(new DialogueChoice(Text.FormatKey("MENU_CANCEL"), () => { context.CancelState.Cancel = true; }));
                        DialogueBox question = MenuManager.Instance.CreateMultiQuestion(Text.FormatGrammar(new StringKey("DLG_WHICH_FORM").ToLocal(), context.User.GetDisplayName(true)), true, choices, 0, choices.Count - 1);

                        yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.ProcessMenuCoroutine(question));

                        if (!context.CancelState.Cancel)
                        {
                            int formNum = -1;
                            SwitchFormContext change = context.ContextStates.GetWithDefault<SwitchFormContext>();
                            if (change != null)
                                formNum = change.Form;
                            DataManager.Instance.LogUIPlay(formNum);
                        }
                    }
                }
                else if (eligibleForms.Count == 1)
                {
                    SwitchFormContext change = new SwitchFormContext();
                    change.Form = eligibleForms[0];
                    context.ContextStates.Set(change);
                }
                else
                {
                    yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.SetDialogue(Text.FormatGrammar(new StringKey("MSG_ITEM_NO_EFFECT").ToLocal(), context.User.GetDisplayName(true))));
                    context.CancelState.Cancel = true;
                }
            }
            else
                context.CancelState.Cancel = true;
        }

    }

    /// <summary>
    /// Event that deactivates the use of the item by setting its hidden value 
    /// </summary>
    [Serializable]
    public class DeactivateItemEvent : BattleEvent
    {
        public DeactivateItemEvent() { }
        public override GameEvent Clone() { return new DeactivateItemEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.UsageSlot > BattleContext.EQUIP_ITEM_SLOT)//item in inventory
            {
                InvItem item = ((ExplorerTeam)context.User.MemberTeam).GetInv(context.UsageSlot);
                item.HiddenValue = item.ID;
            }
            else if (context.UsageSlot == BattleContext.EQUIP_ITEM_SLOT)
            {
                InvItem item = context.User.EquippedItem;
                item.HiddenValue = item.ID;
            }
            else if (context.UsageSlot == BattleContext.FLOOR_ITEM_SLOT)
            {
                int mapSlot = ZoneManager.Instance.CurrentMap.GetItem(context.User.CharLoc);
                MapItem mapItem = ZoneManager.Instance.CurrentMap.Items[mapSlot];
                mapItem.HiddenValue = mapItem.Value;
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that changes the form of the user using the value in SwitchFormContext
    /// </summary>
    [Serializable]
    public class SwitchFormEvent : BattleEvent
    {
        public SwitchFormEvent() { }
        public override GameEvent Clone() { return new SwitchFormEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int form = -1;
            SwitchFormContext change = context.ContextStates.GetWithDefault<SwitchFormContext>();
            if (change != null)
                form = change.Form;
            if (form > -1)
            {
                context.User.Promote(new MonsterID(context.User.CurrentForm.Species, form, context.User.CurrentForm.Skin, context.User.CurrentForm.Gender));
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_FORM_CHANGE").ToLocal(), context.User.GetDisplayName(false)));
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that prompts the user to recall or delete moves and sets up MoveLearnContext and MoveDeleteContext
    /// </summary>
    [Serializable]
    public class LinkBoxEvent : BattleEvent
    {
        /// <summary>
        /// Whether pre-evolution moves can be relearned
        /// </summary>
        public bool IncludePreEvolutions;
        public LinkBoxEvent() { }
        public override GameEvent Clone() { return new LinkBoxEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            List<string> forgottenMoves = context.User.GetRelearnableSkills(IncludePreEvolutions);

            if (DataManager.Instance.CurrentReplay != null)// this block of code will never evaluate to true AND have UI read back -1 (cancel)
            {
                int action = DataManager.Instance.CurrentReplay.ReadUI();
                if (action == 0)
                {
                    MoveLearnContext learn = new MoveLearnContext();
                    learn.MoveLearn = forgottenMoves[DataManager.Instance.CurrentReplay.ReadUI()];
                    learn.ReplaceSlot = DataManager.Instance.CurrentReplay.ReadUI();
                    context.ContextStates.Set(learn);
                }
                else if (action == 1)
                {
                    int deleteSlot = DataManager.Instance.CurrentReplay.ReadUI();
                    context.ContextStates.Set(new MoveDeleteContext(deleteSlot));
                }
                else
                    throw new Exception("Operation must learn or delete a move.");
            }
            else
            {
                yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.ProcessMenuCoroutine(createLinkBoxDialog(context, forgottenMoves)));

                if (!context.CancelState.Cancel)
                {
                    int deleteSlot = -1;
                    MoveDeleteContext delete = context.ContextStates.GetWithDefault<MoveDeleteContext>();
                    if (delete != null)
                        deleteSlot = delete.MoveDelete;

                    string moveLearn = "";
                    int learnSlot = -1;
                    MoveLearnContext learn = context.ContextStates.GetWithDefault<MoveLearnContext>();
                    if (learn != null)
                    {
                        moveLearn = learn.MoveLearn;
                        learnSlot = learn.ReplaceSlot;
                    }

                    if (!String.IsNullOrEmpty(moveLearn))
                    {
                        DataManager.Instance.LogUIPlay(0, forgottenMoves.IndexOf(moveLearn), learnSlot);
                    }
                    else if (deleteSlot > -1)
                    {
                        DataManager.Instance.LogUIPlay(1, deleteSlot);
                    }
                    else
                        throw new Exception("Link box must learn or delete a move.");
                }
            }
        }


        private DialogueBox createLinkBoxDialog(BattleContext context, List<string> forgottenMoves)
        {
            List<DialogueChoice> choices = new List<DialogueChoice>();
            choices.Add(new DialogueChoice(Text.FormatGrammar(new StringKey("MENU_RECALL_SKILL").ToLocal()), () => { MenuManager.Instance.AddMenu(createRememberDialog(context, forgottenMoves), false); }));
            choices.Add(new DialogueChoice(Text.FormatGrammar(new StringKey("MENU_FORGET_SKILL").ToLocal()), () =>
            {
                int totalMoves = 0;
                foreach (SlotSkill move in context.User.BaseSkills)
                {
                    if (!String.IsNullOrEmpty(move.SkillNum))
                        totalMoves++;
                }
                if (totalMoves > 1)
                {
                    MenuManager.Instance.AddMenu(new SkillForgetMenu(context.User,
                        (int slot) => { context.ContextStates.Set(new MoveDeleteContext(slot)); },
                        () => { MenuManager.Instance.AddMenu(createLinkBoxDialog(context, forgottenMoves), false); }), false);
                }
                else
                    MenuManager.Instance.AddMenu(MenuManager.Instance.CreateDialogue(() => { MenuManager.Instance.AddMenu(createLinkBoxDialog(context, forgottenMoves), false); },
                    Text.FormatGrammar(new StringKey("DLG_CANT_FORGET_SKILL").ToLocal(), context.User.GetDisplayName(true))), false);

            }));
            choices.Add(new DialogueChoice(Text.FormatKey("MENU_CANCEL"), () => { context.CancelState.Cancel = true; }));
            return MenuManager.Instance.CreateMultiQuestion(Text.FormatKey("DLG_WHAT_DO"), true, choices, 0, 2);
        }

        private IInteractable createRememberDialog(BattleContext context, List<string> forgottenMoves)
        {
            if (forgottenMoves.Count > 0)
            {
                return new SkillRecallMenu(context.User, forgottenMoves.ToArray(), (int moveSlot) =>
                {
                    string moveNum = forgottenMoves[moveSlot];
                    MenuManager.Instance.NextAction = DungeonScene.TryLearnSkill(context.User, moveNum,
                        (int slot) =>
                        {
                            MoveLearnContext learn = new MoveLearnContext();
                            learn.MoveLearn = moveNum;
                            learn.ReplaceSlot = slot;
                            context.ContextStates.Set(learn);
                        },
                        () => { MenuManager.Instance.AddMenu(createRememberDialog(context, forgottenMoves), false); });
                }, () => { MenuManager.Instance.AddMenu(createLinkBoxDialog(context, forgottenMoves), false); });
            }
            else
                return MenuManager.Instance.CreateDialogue(() => { MenuManager.Instance.AddMenu(createLinkBoxDialog(context, forgottenMoves), false); },
                    Text.FormatGrammar(new StringKey("DLG_CANT_RECALL_SKILL").ToLocal(), context.User.GetDisplayName(true)));

        }

    }

    /// <summary>
    /// Event that causes the user to relearn a move using the value in MoveLearnContext 
    /// </summary>
    [Serializable]
    public class MoveLearnEvent : BattleEvent
    {
        public MoveLearnEvent() { }
        public override GameEvent Clone() { return new MoveLearnEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            string moveNum = "";
            int moveSlot = -1;
            MoveLearnContext learn = context.ContextStates.GetWithDefault<MoveLearnContext>();
            if (learn != null)
            {
                moveNum = learn.MoveLearn;
                moveSlot = learn.ReplaceSlot;
            }
            if (!String.IsNullOrEmpty(moveNum) && moveSlot > -1)
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.LearnSkillWithFanfare(context.User, moveNum, moveSlot));
        }
    }
    
    /// <summary>
    /// Event that causes the user to delete a move using the value in MoveDeleteContext 
    /// </summary>
    [Serializable]
    public class MoveDeleteEvent : BattleEvent
    {
        public MoveDeleteEvent() { }
        public override GameEvent Clone() { return new MoveDeleteEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int slot = -1;
            MoveDeleteContext delete = context.ContextStates.GetWithDefault<MoveDeleteContext>();
            if (delete != null)
                slot = delete.MoveDelete;
            if (slot > -1)
            {
                string moveNum = context.User.BaseSkills[slot].SkillNum;
                context.User.DeleteSkill(slot);
                yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.LogSkippableMsg(Text.FormatGrammar(new StringKey("DLG_FORGET_SKILL").ToLocal(), context.User.GetDisplayName(false), DataManager.Instance.GetSkill(moveNum).GetIconName()), context.User.MemberTeam));
            }
        }
    }
    
    /// <summary>
    /// Event that prompts the user to learn a new ability and sets up AbilityLearnContext 
    /// </summary>
    [Serializable]
    public class AbilityCapsuleEvent : BattleEvent
    {
        public AbilityCapsuleEvent() { }
        public override GameEvent Clone() { return new AbilityCapsuleEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.User.MemberTeam == DungeonScene.Instance.ActiveTeam)
            {
                BaseMonsterForm entry = DataManager.Instance.GetMonster(context.User.BaseForm.Species).Forms[context.User.BaseForm.Form];
                List<string> eligibleAbilities = new List<string>();

                if (entry.Intrinsic1 != DataManager.Instance.DefaultIntrinsic && context.User.BaseIntrinsics[0] != entry.Intrinsic1)
                    eligibleAbilities.Add(entry.Intrinsic1);
                if (entry.Intrinsic2 != DataManager.Instance.DefaultIntrinsic && context.User.BaseIntrinsics[0] != entry.Intrinsic2)
                    eligibleAbilities.Add(entry.Intrinsic2);
                if (entry.Intrinsic3 != DataManager.Instance.DefaultIntrinsic && context.User.BaseIntrinsics[0] != entry.Intrinsic3)
                    eligibleAbilities.Add(entry.Intrinsic3);

                if (eligibleAbilities.Count > 0)
                {
                    int chosenSlot = -1;
                    if (DataManager.Instance.CurrentReplay != null) // this block of code will never evaluate to true AND have UI read back -1 (cancel) at the same time
                        chosenSlot = DataManager.Instance.CurrentReplay.ReadUI();
                    else
                    {
                        yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.ProcessMenuCoroutine(new IntrinsicRecallMenu(context.User, eligibleAbilities.ToArray(),
                            (int abilitySlot) => { chosenSlot = abilitySlot; }, () => { context.CancelState.Cancel = true; })));

                        if (chosenSlot > -1)
                            DataManager.Instance.LogUIPlay(chosenSlot);
                    }

                    if (!context.CancelState.Cancel)
                    {
                        AbilityLearnContext learn = new AbilityLearnContext();
                        learn.AbilityLearn = eligibleAbilities[chosenSlot];
                        learn.ReplaceSlot = 0;
                        context.ContextStates.Set(learn);
                    }
                }
                else
                {
                    yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.SetDialogue(Text.FormatGrammar(new StringKey("DLG_CANT_RECALL_INTRINSIC").ToLocal(), context.User.GetDisplayName(true))));
                    context.CancelState.Cancel = true;
                }
            }
            else
                context.CancelState.Cancel = true;
        }

    }
    
    /// <summary>
    /// Event that causes the user to learn a new ability using the value in AbilityLearnContext 
    /// </summary>
    [Serializable]
    public class AbilityLearnEvent : BattleEvent
    {
        public AbilityLearnEvent() { }
        public override GameEvent Clone() { return new AbilityLearnEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            string abilityNum = "";
            int abilitySlot = -1;
            AbilityLearnContext learn = context.ContextStates.GetWithDefault<AbilityLearnContext>();
            if (learn != null)
            {
                abilityNum = learn.AbilityLearn;
                abilitySlot = learn.ReplaceSlot;
            }
            if (!String.IsNullOrEmpty(abilityNum))
            {
                GameManager.Instance.SE("Fanfare/LearnSkill");
                context.User.LearnIntrinsic(abilityNum, abilitySlot);

                yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.LogSkippableMsg(Text.FormatGrammar(new StringKey("DLG_LEARN_INTRINSIC").ToLocal(), context.User.GetDisplayName(false), DataManager.Instance.GetIntrinsic(abilityNum).GetColoredName()), context.User.MemberTeam));
            }
        }
    }
    
    /// <summary>
    /// Event that deletes the user's ability based on the value in the AbilityDeleteContext 
    /// </summary>
    [Serializable]
    public class AbilityDeleteEvent : BattleEvent
    {
        public AbilityDeleteEvent() { }
        public override GameEvent Clone() { return new AbilityDeleteEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int slot = -1;
            AbilityDeleteContext delete = context.ContextStates.GetWithDefault<AbilityDeleteContext>();
            if (delete != null)
                slot = delete.AbilityDelete;
            if (slot > -1)
            {
                string abilityNum = context.User.BaseIntrinsics[slot];
                context.User.DeleteIntrinsic(slot);

                yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.LogSkippableMsg(Text.FormatGrammar(new StringKey("DLG_FORGET_INTRINSIC").ToLocal(), context.User.GetDisplayName(false), DataManager.Instance.GetIntrinsic(abilityNum).GetColoredName()), context.User.MemberTeam));
            }
        }
    }
    
    /// <summary>
    /// Event that prompts the user which item to withdraw from the storage and sets up WithdrawStorageContext 
    /// </summary>
    [Serializable]
    public class StorageBoxEvent : BattleEvent
    {
        public StorageBoxEvent() { }
        public override GameEvent Clone() { return new StorageBoxEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.User.MemberTeam == DungeonScene.Instance.ActiveTeam)
            {
                bool hasItems = (DungeonScene.Instance.ActiveTeam.BoxStorage.Count > 0);
                foreach(string key in DungeonScene.Instance.ActiveTeam.Storage.Keys)
                {
                    if (DungeonScene.Instance.ActiveTeam.Storage[key] > 0)
                    {
                        hasItems = true;
                        break;
                    }
                }
                if (context.User != context.User.MemberTeam.Leader)
                {
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_LEADER_ONLY_ITEM").ToLocal()));
                    context.CancelState.Cancel = true;
                }
                else if (!hasItems)
                {
                    yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.SetDialogue(Text.FormatGrammar(new StringKey("DLG_STORAGE_EMPTY").ToLocal())));
                    context.CancelState.Cancel = true;
                }
                else
                {
                    if (DataManager.Instance.CurrentReplay != null) // this block of code will never evaluate to true AND have UI read back -1 (cancel) at the same time
                    {
                        bool isBox = DataManager.Instance.CurrentReplay.ReadUI() != 0;
                        string id = DataManager.Instance.CurrentReplay.ReadUIString();
                        int slot = DataManager.Instance.CurrentReplay.ReadUI();
                        context.ContextStates.Set(new WithdrawStorageContext(new WithdrawSlot(isBox, id, slot)));
                    }
                    else
                    {
                        yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.SetDialogue(Text.FormatGrammar(new StringKey("DLG_ASK_STORAGE").ToLocal())));

                        bool chose = false;
                        yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.ProcessMenuCoroutine(new WithdrawMenu(0, false,
                            (List<WithdrawSlot> slots) => { context.ContextStates.Set(new WithdrawStorageContext(slots[0])); chose = true; })));

                        if (chose)
                        {
                            WithdrawStorageContext withdraw = context.ContextStates.GetWithDefault<WithdrawStorageContext>();
                            if (withdraw != null)
                            {
                                DataManager.Instance.LogUIPlay(withdraw.WithdrawSlot.IsBox ? 1 : 0);
                                DataManager.Instance.LogUIStringPlay(withdraw.WithdrawSlot.ItemID);
                                DataManager.Instance.LogUIPlay(withdraw.WithdrawSlot.BoxSlot);
                            }
                        }
                        else
                            context.CancelState.Cancel = true;
                    }
                }
            }
            else
                context.CancelState.Cancel = true;
        }

    }
    
    /// <summary>
    /// Event that withdraws an item from storage using the value in WithdrawStorageContext
    /// </summary>
    [Serializable]
    public class WithdrawItemEvent : BattleEvent
    {
        public WithdrawItemEvent() { }
        public override GameEvent Clone() { return new WithdrawItemEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            WithdrawStorageContext withdraw = context.ContextStates.GetWithDefault<WithdrawStorageContext>();
            if (withdraw != null)
            {
                WithdrawSlot slot = withdraw.WithdrawSlot;

                if (context.User.MemberTeam == DungeonScene.Instance.ActiveTeam)
                {
                    ExplorerTeam team = (ExplorerTeam)context.User.MemberTeam;
                    InvItem item = team.TakeItems(new List<WithdrawSlot> { slot })[0];

                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_STORAGE_TAKE").ToLocal(), context.User.GetDisplayName(false), item.GetDisplayName()));
                    if (team.GetInvCount() < team.GetMaxInvSlots(ZoneManager.Instance.CurrentZone))
                        team.AddToInv(item);
                    else
                        yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.DropItem(item, context.User.CharLoc));
                }
            }
        }
    }

    /// <summary>
    /// Event that prompts the user which assembly member to add to the team the sets up WithdrawStorageContext 
    /// </summary>
    [Serializable]
    public class AssemblyBoxEvent : BattleEvent
    {
        public AssemblyBoxEvent() { }
        public override GameEvent Clone() { return new AssemblyBoxEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.User.MemberTeam == DungeonScene.Instance.ActiveTeam)
            {
                if (context.User != context.User.MemberTeam.Leader)
                {
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_LEADER_ONLY_ITEM").ToLocal()));
                    context.CancelState.Cancel = true;
                    yield break;
                }
                bool hasBench = false;
                ExplorerTeam team = ((ExplorerTeam)context.User.MemberTeam);
                foreach (Character chara in team.Assembly)
                {
                    if (!chara.Absentee)
                    {
                        hasBench = true;
                        break;
                    }
                }
                if (!hasBench)
                {
                    yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.SetDialogue(Text.FormatGrammar(new StringKey("MSG_ASSEMBLY_EMPTY").ToLocal())));
                    context.CancelState.Cancel = true;
                }
                else
                {
                    if (DataManager.Instance.CurrentReplay != null) // this block of code will never evaluate to true AND have UI read back -1 (cancel) at the same time
                    {
                        int slot = DataManager.Instance.CurrentReplay.ReadUI();
                        context.ContextStates.Set(new WithdrawAssemblyContext(slot));
                    }
                    else
                    {
                        yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.SetDialogue(Text.FormatGrammar(new StringKey("MSG_ASK_ASSEMBLY").ToLocal())));

                        yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.ProcessMenuCoroutine(new AddToTeamMenu(
                            (List<int> slots) => { context.ContextStates.Set(new WithdrawAssemblyContext(slots[0])); },
                            () => { context.CancelState.Cancel = true; })));

                        if (!context.CancelState.Cancel)
                        {
                            int slot = -1;
                            WithdrawAssemblyContext withdraw = context.ContextStates.GetWithDefault<WithdrawAssemblyContext>();
                            if (withdraw != null)
                                slot = withdraw.WithdrawSlot;
                            DataManager.Instance.LogUIPlay(slot);
                        }
                    }
                }
            }
            else
                context.CancelState.Cancel = true;
        }
    }
    
    /// <summary>
    /// Event that adds a team member from assembly using the value in WithdrawStorageContext
    /// </summary>
    [Serializable]
    public class WithdrawRecruitEvent : BattleEvent
    {
        public WithdrawRecruitEvent() { }
        public override GameEvent Clone() { return new WithdrawRecruitEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int slot = -1;
            WithdrawAssemblyContext withdraw = context.ContextStates.GetWithDefault<WithdrawAssemblyContext>();
            if (withdraw != null)
                slot = withdraw.WithdrawSlot;
            if (slot > -1)
            {
                Character member = ((ExplorerTeam)context.User.MemberTeam).Assembly[slot];
                ((ExplorerTeam)context.User.MemberTeam).Assembly.RemoveAt(slot);
                Loc? endLoc = ZoneManager.Instance.CurrentMap.GetClosestTileForChar(member, context.User.CharLoc);
                if (endLoc == null)
                    endLoc = context.User.CharLoc;
                member.CharLoc = endLoc.Value;

                GameManager.Instance.BattleSE("DUN_Send_Home");
                SingleEmitter emitter = new SingleEmitter(new BeamAnimData("Column_Yellow", 3));
                emitter.Layer = DrawLayer.Front;
                emitter.SetupEmit(member.CharLoc * GraphicsManager.TileSize + new Loc(GraphicsManager.TileSize / 2), member.CharLoc * GraphicsManager.TileSize + new Loc(GraphicsManager.TileSize / 2), member.CharDir);
                DungeonScene.Instance.CreateAnim(emitter, DrawLayer.NoDraw);
                DungeonScene.Instance.AddCharToTeam(Faction.Player, 0, false, member);
                member.Tactic = new AITactic(member.Tactic);
                member.RefreshTraits();
                member.Tactic.Initialize(member);
                ZoneManager.Instance.CurrentMap.UpdateExploration(member);

                yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.LogSkippableMsg(Text.FormatGrammar(new StringKey("MSG_ASSEMBLY_TAKE_ANY").ToLocal(), member.GetDisplayName(true))));

                yield return CoroutineManager.Instance.StartCoroutine(member.OnMapStart());

                if (DungeonScene.Instance.ActiveTeam.Players.Count > DungeonScene.Instance.ActiveTeam.GetMaxTeam(ZoneManager.Instance.CurrentZone))
                    yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.AskToSendHome());
            }
        }
    }


    [Serializable]
    public abstract class RecruitBoostEvent : BattleEvent
    {
        protected abstract int GetRecruitRate(GameEventOwner owner, Character ownerChar, BattleContext context);

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Target != null)
                context.AddContextStateInt<RecruitBoost>(GetRecruitRate(owner, ownerChar, context));
            yield break;
        }
    }
    
    /// <summary>
    /// Event that sets the additional recruitment rate, not accounting for the species join rate
    /// </summary>
    [Serializable]
    public class FlatRecruitmentEvent : RecruitBoostEvent
    {
        /// <summary>
        /// The additional recruitment rate
        /// </summary>
        public int RecruitRate;

        public FlatRecruitmentEvent() { }
        public FlatRecruitmentEvent(int recruitRate) { RecruitRate = recruitRate; }
        protected FlatRecruitmentEvent(FlatRecruitmentEvent other)
        {
            RecruitRate = other.RecruitRate;
        }
        public override GameEvent Clone() { return new FlatRecruitmentEvent(this); }

        protected override int GetRecruitRate(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            return RecruitRate;
        }
    }

    /// <summary>
    /// Event that boosts the recruitment rate if the target's type matches one of the specified type.
    /// Otherwise, it drops the recruitment rate
    /// </summary>
    [Serializable]
    public class TypeRecruitmentEvent : RecruitBoostEvent
    {
        [JsonConverter(typeof(ElementSetConverter))]
        [DataType(1, DataManager.DataType.Element, false)]
        public HashSet<string> Elements;

        public TypeRecruitmentEvent() { Elements = new HashSet<string>(); }
        public TypeRecruitmentEvent(string element) : this() { Elements.Add(element); }
        public TypeRecruitmentEvent(HashSet<string> elements) { Elements = elements; }
        protected TypeRecruitmentEvent(TypeRecruitmentEvent other)
            : this()
        {
            foreach (string element in other.Elements)
                Elements.Add(element);
        }
        public override GameEvent Clone() { return new TypeRecruitmentEvent(this); }

        protected override int GetRecruitRate(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            MonsterID formData = context.Target.BaseForm;
            BaseMonsterForm form = DataManager.Instance.GetMonster(formData.Species).Forms[formData.Form];
            if (Elements.Contains(form.Element1) || Elements.Contains(form.Element2))
                return 35;
            else
                return -50;
        }
    }
    
    /// <summary>
    /// Event that boosts the recruitment rate if the target is not the default skin. 
    /// Otherwise, it drops the recruitment rate
    /// </summary>
    [Serializable]
    public class SkinRecruitmentEvent : RecruitBoostEvent
    {
        public override GameEvent Clone() { return new SkinRecruitmentEvent(); }

        protected override int GetRecruitRate(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            MonsterID formData = context.Target.BaseForm;
            if (formData.Skin != DataManager.Instance.DefaultSkin)
                return 35;
            return -50;
        }
    }

    /// <summary>
    /// Event that modifies the recruitment rate based on the type matchup between the user and target
    /// </summary>
    [Serializable]
    public class TypeMatchupRecruitmentEvent : RecruitBoostEvent
    {
        public override GameEvent Clone() { return new TypeMatchupRecruitmentEvent(); }

        protected override int GetRecruitRate(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int matchup1 = PreTypeEvent.CalculateTypeMatchup(context.User.Element1, context.Target.Element1);
            matchup1 += PreTypeEvent.CalculateTypeMatchup(context.User.Element1, context.Target.Element2);

            int matchup2 = PreTypeEvent.CalculateTypeMatchup(context.User.Element1, context.Target.Element1);
            matchup2 += PreTypeEvent.CalculateTypeMatchup(context.User.Element1, context.Target.Element2);

            return PreTypeEvent.GetEffectivenessMult(Math.Max(matchup1, matchup2)) * 20 - 80;//between + and - 80 recruit rate
        }
    }

    /// <summary>
    /// Event that modifies the recruitment rate based on the level difference between the user and target
    /// </summary>
    [Serializable]
    public class LevelRecruitmentEvent : RecruitBoostEvent
    {
        public override GameEvent Clone() { return new LevelRecruitmentEvent(); }

        protected override int GetRecruitRate(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            return (context.User.Level - context.Target.Level);//between + and - 100, at max
        }
    }

    /// <summary>
    /// Event that attempts to recruit the target.
    /// If successful, the recruit can be nicknamed and added to the team
    /// </summary>
    [Serializable]
    public class RecruitmentEvent : BattleEvent
    {
        /// <summary>
        /// Tha lua battle script that runs when interacting with the recruit in dungeons 
        /// </summary>
        public BattleScriptEvent ActionScript;

        public RecruitmentEvent()
        { }
        public RecruitmentEvent(BattleScriptEvent scriptEvent)
        {
            ActionScript = scriptEvent;
        }

        public RecruitmentEvent(RecruitmentEvent other)
        {
            ActionScript = (BattleScriptEvent)other.ActionScript.Clone();
        }

        public override GameEvent Clone() { return new RecruitmentEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            context.Target.CharDir = context.User.CharDir.Reverse();
            if (DiagManager.Instance.CurSettings.BattleFlow < Settings.BattleSpeed.VeryFast)
            {
                EmoteData emoteData = DataManager.Instance.GetEmote("question");
                context.Target.StartEmote(new Emote(emoteData.Anim, emoteData.LocHeight, 1));
                GameManager.Instance.BattleSE("EVT_Emote_Confused");
                yield return new WaitForFrames(60);
            }


            if (context.Target.Unrecruitable || context.Target.MemberTeam is ExplorerTeam)
            {
                EmoteData emoteData = DataManager.Instance.GetEmote("angry");
                context.Target.StartEmote(new Emote(emoteData.Anim, emoteData.LocHeight, 1));
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_CANT_RECRUIT").ToLocal(), context.Target.GetDisplayName(false)));
                GameManager.Instance.BattleSE("DUN_Miss");
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.DropItem(context.Item, context.Target.CharLoc));
            }
            else if (context.Target.Level > context.User.Level + 5)
            {
                EmoteData emoteData = DataManager.Instance.GetEmote("angry");
                context.Target.StartEmote(new Emote(emoteData.Anim, emoteData.LocHeight, 1));
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_CANT_RECRUIT_LEVEL").ToLocal(), context.User.GetDisplayName(false), context.Target.GetDisplayName(false)));
                GameManager.Instance.BattleSE("DUN_Miss");
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.DropItem(context.Item, context.Target.CharLoc));
            }
            else
            {
                MonsterID formData = context.Target.BaseForm;
                int catchRate = DataManager.Instance.GetMonster(formData.Species).JoinRate;

                int totalRate = catchRate + context.GetContextStateInt<RecruitBoost>(0);
                totalRate = totalRate * (context.Target.MaxHP * 2 - context.Target.HP) / context.Target.MaxHP;

                if (totalRate <= 0)
                {
                    EmoteData emoteData = DataManager.Instance.GetEmote("angry");
                    context.Target.StartEmote(new Emote(emoteData.Anim, emoteData.LocHeight, 1));
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_CANT_RECRUIT_RATE").ToLocal(), context.Target.GetDisplayName(false)));
                    GameManager.Instance.BattleSE("DUN_Miss");
                    yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.DropItem(context.Item, context.Target.CharLoc));
                }
                else
                {
                    if (DataManager.Instance.Save.Rand.Next(100) < totalRate)
                    {
                        GameManager.Instance.Fanfare("Fanfare/JoinTeam");
                        DungeonScene.Instance.RemoveChar(context.Target);
                        AITactic tactic = DataManager.Instance.GetAITactic(DataManager.Instance.DefaultAI);
                        context.Target.Tactic = new AITactic(tactic);
                        DungeonScene.Instance.AddCharToTeam(Faction.Player, 0, false, context.Target);
                        context.Target.RefreshTraits();
                        context.Target.Tactic.Initialize(context.Target);

                        int oldFullness = context.Target.Fullness;
                        context.Target.FullRestore();
                        context.Target.Fullness = oldFullness;
                        //restore HP and status problems
                        //{
                        //    context.Target.HP = context.Target.MaxHP;

                        //    List<int> statuses = new List<int>();
                        //    foreach (StatusEffect oldStatus in context.Target.IterateStatusEffects())
                        //        statuses.Add(oldStatus.ID);

                        //    foreach (int statusID in statuses)
                        //        yield return CoroutineManager.Instance.StartCoroutine(context.Target.RemoveStatusEffect(statusID, false));
                        //}

                        foreach (BackReference<Skill> skill in context.Target.Skills)
                            skill.Element.Enabled = DataManager.Instance.Save.GetDefaultEnable(skill.Element.SkillNum);


                        context.Target.OriginalUUID = DataManager.Instance.Save.UUID;
                        context.Target.OriginalTeam = DataManager.Instance.Save.ActiveTeam.Name;
                        context.Target.MetAt = ZoneManager.Instance.CurrentMap.GetColoredName();
                        context.Target.MetLoc = new ZoneLoc(ZoneManager.Instance.CurrentZoneID, ZoneManager.Instance.CurrentMapID);
                        context.Target.ActionEvents.Clear();
                        if (ActionScript != null)
                            context.Target.ActionEvents.Add((BattleEvent)ActionScript.Clone());
                        ZoneManager.Instance.CurrentMap.UpdateExploration(context.Target);

                        EmoteData emoteData = DataManager.Instance.GetEmote("glowing");
                        context.Target.StartEmote(new Emote(emoteData.Anim, emoteData.LocHeight, 2));
                        yield return new WaitForFrames(40);

                        int poseId = 50;
                        CharSheet sheet = GraphicsManager.GetChara(context.Target.Appearance.ToCharID());
                        int fallbackIndex = sheet.GetReferencedAnimIndex(poseId);
                        if (fallbackIndex == poseId)
                            yield return CoroutineManager.Instance.StartCoroutine(context.Target.StartAnim(new CharAnimPose(context.Target.CharLoc, context.Target.CharDir, poseId, 0)));

                        //check against inventory capacity violation
                        if (!String.IsNullOrEmpty(context.Target.EquippedItem.ID) && DungeonScene.Instance.ActiveTeam.MaxInv == DungeonScene.Instance.ActiveTeam.GetInvCount())
                        {
                            InvItem item = context.Target.EquippedItem;
                            yield return CoroutineManager.Instance.StartCoroutine(context.Target.DequipItem());
                            yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.DropItem(item, context.Target.CharLoc));
                        }

                        if (DataManager.Instance.CurrentReplay == null)
                        {
                            yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.ProcessMenuCoroutine(new MemberFeaturesMenu(DungeonScene.Instance.ActiveTeam.Players.Count - 1, false, false)));

                            bool nick = false;
                            string name = "";
                            yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.ProcessMenuCoroutine(MenuManager.Instance.CreateQuestion(Text.FormatGrammar(new StringKey("MSG_ASK_NICKNAME").ToLocal()),
                                () => { nick = true; },
                                () => { })));
                            if (nick)
                                yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.ProcessMenuCoroutine(new NicknameMenu((string text) => { name = text; }, () => { })));
                            DataManager.Instance.LogUIStringPlay(name);
                            context.Target.Nickname = name;
                        }
                        else
                        {
                            //give nickname
                            context.Target.Nickname = DataManager.Instance.CurrentReplay.ReadUIString();
                        }
                        if (DungeonScene.Instance.ActiveTeam.Name != "")
                            DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_RECRUIT").ToLocal(), context.Target.GetDisplayName(true), DungeonScene.Instance.ActiveTeam.GetDisplayName()));
                        else
                            DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_RECRUIT_ANY").ToLocal(), context.Target.GetDisplayName(true)));
                        DataManager.Instance.Save.RegisterMonster(context.Target.BaseForm.Species);
                        DataManager.Instance.Save.RogueUnlockMonster(context.Target.BaseForm.Species);
                        yield return CoroutineManager.Instance.StartCoroutine(context.Target.OnMapStart());

                        //yield return new WaitForFrames(120);

                        if (DungeonScene.Instance.ActiveTeam.Players.Count > DungeonScene.Instance.ActiveTeam.GetMaxTeam(ZoneManager.Instance.CurrentZone))
                            yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.AskToSendHome());

                        yield return CoroutineManager.Instance.StartCoroutine(context.Target.StartAnim(new CharAnimIdle(context.Target.CharLoc, context.Target.CharDir)));
                    }
                    else
                    {
                        DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_RECRUIT_FAIL").ToLocal(), context.Target.GetDisplayName(false)));
                        GameManager.Instance.BattleSE("DUN_Miss");
                        yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.DropItem(context.Item, context.Target.CharLoc));
                    }
                }
            }
        }
    }

    /// <summary>
    /// Event that makes the target a neutral faction  
    /// </summary>
    [Serializable]
    public class MakeNeutralEvent : BattleEvent
    {
        
        /// <summary>
        /// Tha lua battle script that runs when interacting with the neutral in dungeons 
        /// </summary>
        public BattleScriptEvent ActionScript;

        public MakeNeutralEvent()
        { }
        public MakeNeutralEvent(BattleScriptEvent scriptEvent)
        {
            ActionScript = scriptEvent;
        }

        public MakeNeutralEvent(MakeNeutralEvent other)
        {
            ActionScript = (BattleScriptEvent)other.ActionScript.Clone();
        }

        public override GameEvent Clone() { return new MakeNeutralEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            DungeonScene.Instance.RemoveChar(context.Target);
            ExplorerTeam neutralTeam = new ExplorerTeam();
            AITactic tactic = DataManager.Instance.GetAITactic("slow_wander");
            context.Target.Tactic = new AITactic(tactic);
            neutralTeam.Players.Add(context.Target);
            DungeonScene.Instance.AddTeam(Faction.Friend, neutralTeam);
            DungeonScene.Instance.OnCharAdd(context.Target);

            context.Target.RefreshTraits();
            context.Target.Tactic.Initialize(context.Target);

            int oldFullness = context.Target.Fullness;
            context.Target.FullRestore();
            context.Target.Fullness = oldFullness;

            context.Target.ActionEvents.Clear();
            if (ActionScript != null)
                context.Target.ActionEvents.Add((BattleEvent)ActionScript.Clone());

            yield break;
        }
    }
    
    /// <summary>
    /// Event that revives all fainted party memebers
    /// </summary>
    [Serializable]
    public class ReviveAllEvent : BattleEvent
    {
        public ReviveAllEvent() { }
        public override GameEvent Clone() { return new ReviveAllEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            bool revived = false;
            foreach (Character character in context.User.MemberTeam.EnumerateChars())
            {
                if (character.Dead)
                {
                    Loc? endLoc = ZoneManager.Instance.CurrentMap.GetClosestTileForChar(character, context.User.CharLoc);
                    if (endLoc == null)
                        endLoc = context.User.CharLoc;
                    character.CharLoc = endLoc.Value;

                    character.HP = character.MaxHP;
                    character.Dead = false;
                    character.DefeatAt = "";

                    character.UpdateFrame();
                    ZoneManager.Instance.CurrentMap.UpdateExploration(character);

                    GameManager.Instance.BattleSE("DUN_Send_Home");
                    SingleEmitter emitter = new SingleEmitter(new BeamAnimData("Column_Yellow", 3));
                    emitter.Layer = DrawLayer.Front;
                    emitter.SetupEmit(character.MapLoc, character.MapLoc, character.CharDir);
                    DungeonScene.Instance.CreateAnim(emitter, DrawLayer.NoDraw);

                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_REVIVE").ToLocal(), character.GetDisplayName(false)));

                    yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(20));
                    revived = true;
                }
            }
            if (!revived)
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_REVIVE_NONE").ToLocal()));
        }
    }

    /// <summary>
    /// Event that exits out of the dungeon
    /// </summary>
    [Serializable]
    public class ExitDungeonEvent : BattleEvent
    {
        public ExitDungeonEvent() { }
        public override GameEvent Clone() { return new ExitDungeonEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.User.MemberTeam == DungeonScene.Instance.ActiveTeam)
            {
                GameManager.Instance.BGM("", true);
                yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.FadeOut(true));

                // remove all unpaid items
                for (int ii = DungeonScene.Instance.ActiveTeam.GetInvCount() - 1; ii >= 0; ii--)
                {
                    if (DungeonScene.Instance.ActiveTeam.GetInv(ii).Price > 0)
                        DungeonScene.Instance.ActiveTeam.RemoveFromInv(ii);
                }

                yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.EndSegment(GameProgress.ResultType.Escaped));
                context.CancelState.Cancel = true;
                context.TurnCancel.Cancel = true;
            }
        }
    }


    [Serializable]
    public abstract class ShareEquipBattleEvent : BattleEvent
    {
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
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

        protected abstract PriorityList<BattleEvent> GetEvents(ItemData entry);
    }

    /// <summary>
    /// Event that applies the target with the AfterActions passive effects of the original character's item
    /// This event should usually be used in proximity events
    /// </summary>
    [Serializable]
    public class ShareAfterActionsEvent : ShareEquipBattleEvent
    {
        public override GameEvent Clone() { return new ShareAfterActionsEvent(); }

        protected override PriorityList<BattleEvent> GetEvents(ItemData entry) => entry.AfterActions;
    }
    
    /// <summary>
    /// Event that applies the target with the AfterBeingHits passive effects of the original character's item
    /// This event should usually be used in proximity events
    /// </summary>
    [Serializable]
    public class ShareAfterBeingHitsEvent : ShareEquipBattleEvent
    {
        public override GameEvent Clone() { return new ShareAfterBeingHitsEvent(); }

        protected override PriorityList<BattleEvent> GetEvents(ItemData entry) => entry.AfterBeingHits;
    }

    
    /// <summary>
    /// Event that applies the target with the AfterHittings passive effects of the original character's item
    /// This event should usually be used in proximity events
    /// </summary>
    [Serializable]
    public class ShareAfterHittingsEvent : ShareEquipBattleEvent
    {
        public override GameEvent Clone() { return new ShareAfterHittingsEvent(); }

        protected override PriorityList<BattleEvent> GetEvents(ItemData entry) => entry.AfterHittings;
    }

    
    /// <summary>
    /// Event that applies the target with the BeforeActions passive effects of the original character's item
    /// This event should usually be used in proximity events
    /// </summary>
    [Serializable]
    public class ShareBeforeActionsEvent : ShareEquipBattleEvent
    {
        public override GameEvent Clone() { return new ShareBeforeActionsEvent(); }

        protected override PriorityList<BattleEvent> GetEvents(ItemData entry) => entry.BeforeActions;
    }

    /// <summary>
    /// Event that applies the target with the BeforeBeingHits passive effects of the original character's item
    /// This event should usually be used in proximity events
    /// </summary>
    [Serializable]
    public class ShareBeforeBeingHitsEvent : ShareEquipBattleEvent
    {
        public override GameEvent Clone() { return new ShareBeforeBeingHitsEvent(); }

        protected override PriorityList<BattleEvent> GetEvents(ItemData entry) => entry.BeforeBeingHits;
    }

    /// <summary>
    /// Event that applies the target with the BeforeHittings passive effects of the original character's item
    /// This event should usually be used in proximity events
    /// </summary>
    [Serializable]
    public class ShareBeforeHittingsEvent : ShareEquipBattleEvent
    {
        public override GameEvent Clone() { return new ShareBeforeHittingsEvent(); }

        protected override PriorityList<BattleEvent> GetEvents(ItemData entry) => entry.BeforeHittings;
    }

    /// <summary>
    /// Event that applies the target with the BeforeTryActions passive effects of the original character's item
    /// This event should usually be used in proximity events
    /// </summary>
    [Serializable]
    public class ShareBeforeTryActionsEvent : ShareEquipBattleEvent
    {
        public override GameEvent Clone() { return new ShareBeforeTryActionsEvent(); }

        protected override PriorityList<BattleEvent> GetEvents(ItemData entry) => entry.BeforeTryActions;
    }


    /// <summary>
    /// Event that applies the target with the OnActions passive effects of the original character's item
    /// This event should usually be used in proximity events
    /// </summary>
    [Serializable]
    public class ShareOnActionsEvent : ShareEquipBattleEvent
    {
        public override GameEvent Clone() { return new ShareOnActionsEvent(); }

        protected override PriorityList<BattleEvent> GetEvents(ItemData entry) => entry.OnActions;
    }


    /// <summary>
    /// Event that applies the target with the OnHitTiles passive effects of the original character's item
    /// This event should usually be used in proximity events
    /// </summary>
    [Serializable]
    public class ShareOnHitTilesEvent : ShareEquipBattleEvent
    {
        public override GameEvent Clone() { return new ShareOnHitTilesEvent(); }

        protected override PriorityList<BattleEvent> GetEvents(ItemData entry) => entry.OnHitTiles;
    }

}

