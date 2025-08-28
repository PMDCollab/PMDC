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
    // Battle events thatdo things to items in the player's inventory or equip

    [Serializable]
    public abstract class ItemMetaEvent : BattleEvent
    {
        /// <summary>
        /// Whether to select the highest price item or not
        /// </summary>
        public bool TopDown;

        /// <summary>
        /// Whether or not the item needs to be held for the effect to work 
        /// </summary>
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
        /// </summary>
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
        /// </summary>
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

                Loc endLoc = DungeonScene.Instance.MoveShotUntilBlocked(context.User, context.Target.CharLoc, context.User.CharDir, 2, Alignment.None, false, false);
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

                BattleContext newContext = new BattleContext(BattleActionType.Throw);
                newContext.User = context.User;
                newContext.UsageSlot = BattleContext.FORCED_SLOT;

                newContext.StartDir = newContext.User.CharDir;

                //from ThrowItem
                ItemData entry = DataManager.Instance.GetItem(item.ID);
                bool defaultDmg = false;
                bool catchable = true;

                if (entry.UsageType == ItemData.UseType.None || entry.UsageType == ItemData.UseType.Treasure || entry.UsageType == ItemData.UseType.Use || entry.UsageType == ItemData.UseType.Learn || entry.UsageType == ItemData.UseType.Box || entry.UsageType == ItemData.UseType.UseOther || entry.ItemStates.Contains<RecruitState>())
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
                if (entry.MaxStack > 1)
                {
                    //TODO: Price needs to be multiplied by amount instead of dividing
                    newContext.Item.Price = context.Item.Price / newContext.Item.Amount;
                    newContext.Item.Amount = 1;
                }
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

                newContext.SetActionMsg(Text.FormatGrammar(new StringKey("MSG_KNOCK_ITEM").ToLocal(), context.User.GetDisplayName(false), context.Target.GetDisplayName(false), newContext.Item.GetDisplayName()));


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

                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.ExpendItem(context.Target, itemIndex, BattleActionType.Throw));
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
    /// Event that destroy the character's item
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
                ItemData entry = DataManager.Instance.GetItem(item.ID);
                InvItem newItem = new InvItem(item);
                if (entry.MaxStack > 1)
                    newItem.Amount = 1;

                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.ExpendItem(context.Target, itemIndex, BattleActionType.Throw));

                //destroy the item
                if (itemIndex > -1)
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_LOSE_ITEM").ToLocal(), context.Target.GetDisplayName(false), newItem.GetDisplayName()));
                else
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_LOSE_HELD_ITEM").ToLocal(), context.Target.GetDisplayName(false), newItem.GetDisplayName()));
            }
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
        /// </summary>
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

                    GameManager.Instance.SE(GraphicsManager.EquipSE);
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
                        GameManager.Instance.SE(GraphicsManager.EquipSE);
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
        /// </summary>
        public bool SilentCheck;


        [StringKey(2, false)]
        public Dictionary<ItemData.UseType, StringKey> UseMsgs;

        public UseFoeItemEvent()
        {
            UseMsgs = new Dictionary<ItemData.UseType, StringKey>();
        }
        public UseFoeItemEvent(bool topDown, bool heldOnly, string priorityItem, HashSet<FlagType> eligibles, bool affectTarget, bool silentCheck, Dictionary<ItemData.UseType, StringKey> useMsgs)
            : base(topDown, heldOnly, priorityItem, eligibles)
        {
            AffectTarget = affectTarget;
            SilentCheck = silentCheck;
            UseMsgs = useMsgs;
        }
        protected UseFoeItemEvent(UseFoeItemEvent other)
            : base(other)
        {
            AffectTarget = other.AffectTarget;
            SilentCheck = other.SilentCheck;

            UseMsgs = new Dictionary<ItemData.UseType, StringKey>();
            foreach (ItemData.UseType useType in other.UseMsgs.Keys)
                UseMsgs[useType] = other.UseMsgs[useType];
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
                    if (entry.MaxStack > 1)
                    {
                        //TODO: Price needs to be multiplied by amount instead of dividing
                        newContext.Item.Price = newContext.Item.Price / newContext.Item.Amount;
                        newContext.Item.Amount = 1;
                    }
                    newContext.HitboxAction = entry.UseAction.Clone();

                    StringKey useMsg;
                    if (UseMsgs.TryGetValue(entry.UsageType, out useMsg))
                        newContext.SetActionMsg(Text.FormatGrammar(useMsg.ToLocal(), newContext.User.GetDisplayName(false), newContext.Item.GetDisplayName()));



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
                    yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.ExpendItem(target, itemIndex, BattleActionType.Item));

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

        [OnDeserialized]
        internal void OnDeserializedMethod(StreamingContext context)
        {
            //TODO: remove on v1.1
            if (Serializer.OldVersion < new Version(0, 8, 9))
            {
                UseMsgs[ItemData.UseType.Eat] = new StringKey("MSG_STEAL_EAT");
                UseMsgs[ItemData.UseType.Drink] = new StringKey("MSG_STEAL_DRINK");
                UseMsgs[ItemData.UseType.Learn] = new StringKey("MSG_STEAL_OPERATE");
                UseMsgs[ItemData.UseType.Use] = new StringKey("MSG_STEAL_USE");
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
}

