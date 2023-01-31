using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence.Data;
using RogueEssence.Content;
using RogueEssence;
using RogueEssence.Dungeon;
using RogueEssence.Dev;

namespace PMDC.Dungeon
{
    [Serializable]
    public class AutoCurseItemEvent : ItemGivenEvent
    {
        public override GameEvent Clone() { return new AutoCurseItemEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, ItemCheckContext context)
        {
            if (!context.User.EquippedItem.Cursed)
            {
                GameManager.Instance.SE(GraphicsManager.CursedSE);
                if (!context.User.CanRemoveStuck)
                    DungeonScene.Instance.LogMsg(Text.FormatKey("MSG_EQUIP_AUTOCURSE", context.User.EquippedItem.GetDisplayName(), context.User.GetDisplayName(false)));
                else
                    DungeonScene.Instance.LogMsg(Text.FormatKey("MSG_EQUIP_AUTOCURSE_AVOID", context.User.EquippedItem.GetDisplayName(), context.User.GetDisplayName(false)));
                context.User.EquippedItem.Cursed = true;
            }
            context.User.RefreshTraits();
            yield break;
        }
    }

    [Serializable]
    public class CurseWarningEvent : ItemGivenEvent
    {
        public override GameEvent Clone() { return new CurseWarningEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, ItemCheckContext context)
        {
            if (context.User.EquippedItem.Cursed && !context.User.CanRemoveStuck)
            {
                GameManager.Instance.SE(GraphicsManager.CursedSE);
                DungeonScene.Instance.LogMsg(Text.FormatKey("MSG_EQUIP_CURSED", context.User.EquippedItem.GetDisplayName(), context.User.GetDisplayName(false)));
            }
            yield break;
        }
    }

    [Serializable]
    public class CheckEquipPassValidityEvent : ItemGivenEvent
    {
        public override GameEvent Clone() { return new CheckEquipPassValidityEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, ItemCheckContext context)
        {
            if (!String.IsNullOrEmpty(context.User.EquippedItem.ID))
            {
                ItemData entry = (ItemData)context.User.EquippedItem.GetData();

                if (CanItemEffectBePassed(entry))
                    DungeonScene.Instance.LogMsg(String.Format(new StringKey("MSG_EQUIP_SHARE").ToLocal(), context.User.EquippedItem.GetDisplayName(), context.User.GetDisplayName(false)));
            }
            yield break;
        }

        public static bool CanItemEffectBePassed(ItemData entry)
        {
            //no refresh events allowed
            if (entry.OnRefresh.Count > 0)
                return false;

            //no proximity events allowed
            if (entry.ProximityEvent.Radius > -1)
                return false;

            //for every other event list, the priority must be 0
            //foreach (var effect in entry.OnEquips)
            //    if (effect.Key != Priority.Zero)
            //        return false;
            //foreach (var effect in entry.OnPickups)
            //    if (effect.Key != Priority.Zero)
            //    return false;

            foreach (var effect in entry.BeforeStatusAdds)
                if (effect.Key != Priority.Zero)
                    return false;
            foreach (var effect in entry.OnStatusAdds)
                if (effect.Key != Priority.Zero)
                    return false;
            foreach (var effect in entry.OnStatusRemoves)
                if (effect.Key != Priority.Zero)
                    return false;
            foreach (var effect in entry.OnMapStatusAdds)
                if (effect.Key != Priority.Zero)
                    return false;
            foreach (var effect in entry.OnMapStatusRemoves)
                if (effect.Key != Priority.Zero)
                    return false;

            foreach (var effect in entry.OnMapStarts)
                if (effect.Key != Priority.Zero)
                    return false;
            foreach (var effect in entry.OnTurnStarts)
                if (effect.Key != Priority.Zero)
                    return false;
            foreach (var effect in entry.OnTurnEnds)
                if (effect.Key != Priority.Zero)
                    return false;
            foreach (var effect in entry.OnMapTurnEnds)
                if (effect.Key != Priority.Zero)
                    return false;
            foreach (var effect in entry.OnWalks)
                if (effect.Key != Priority.Zero)
                    return false;
            foreach (var effect in entry.OnDeaths)
                if (effect.Key != Priority.Zero)
                    return false;

            foreach (var effect in entry.BeforeTryActions)
                if (effect.Key != Priority.Zero)
                    return false;
            foreach (var effect in entry.BeforeActions)
                if (effect.Key != Priority.Zero)
                    return false;
            foreach (var effect in entry.OnActions)
                if (effect.Key != Priority.Zero)
                    return false;
            foreach (var effect in entry.BeforeHittings)
                if (effect.Key != Priority.Zero)
                    return false;
            foreach (var effect in entry.BeforeBeingHits)
                if (effect.Key != Priority.Zero)
                    return false;
            foreach (var effect in entry.AfterHittings)
                if (effect.Key != Priority.Zero)
                    return false;
            foreach (var effect in entry.AfterBeingHits)
                if (effect.Key != Priority.Zero)
                    return false;
            foreach (var effect in entry.OnHitTiles)
                if (effect.Key != Priority.Zero)
                    return false;
            foreach (var effect in entry.AfterActions)
                if (effect.Key != Priority.Zero)
                    return false;

            foreach (var effect in entry.UserElementEffects)
                if (effect.Key != Priority.Zero)
                    return false;
            foreach (var effect in entry.TargetElementEffects)
                if (effect.Key != Priority.Zero)
                    return false;
            foreach (var effect in entry.ModifyHPs)
                if (effect.Key != Priority.Zero)
                    return false;
            foreach (var effect in entry.RestoreHPs)
                if (effect.Key != Priority.Zero)
                    return false;

            return true;
        }
    }
}
