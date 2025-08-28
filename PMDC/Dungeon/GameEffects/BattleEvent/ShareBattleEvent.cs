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
    // Battle events that are passed from the owner of the effect to the target

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

