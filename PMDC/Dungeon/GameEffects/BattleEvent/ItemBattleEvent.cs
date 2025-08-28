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
    // Battle events related to actions using or throwing items


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
                Loc endLoc = DungeonScene.Instance.MoveShotUntilBlocked(context.User, context.Target.CharLoc, context.User.CharDir, 2, Alignment.None, false, false);
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
                    Loc endLoc = DungeonScene.Instance.MoveShotUntilBlocked(context.User, context.Target.CharLoc, context.User.CharDir, 2, Alignment.None, false, false);
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

}

