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

        public override void Apply(GameEventOwner owner, Character ownerChar, ItemCheckContext context)
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
        }
    }

    [Serializable]
    public class CurseWarningEvent : ItemGivenEvent
    {
        public override GameEvent Clone() { return new CurseWarningEvent(); }

        public override void Apply(GameEventOwner owner, Character ownerChar, ItemCheckContext context)
        {
            if (context.User.EquippedItem.Cursed && !context.User.CanRemoveStuck)
            {
                GameManager.Instance.SE(GraphicsManager.CursedSE);
                DungeonScene.Instance.LogMsg(Text.FormatKey("MSG_EQUIP_CURSED", context.User.EquippedItem.GetDisplayName(), context.User.GetDisplayName(false)));
            }
        }
    }
}
