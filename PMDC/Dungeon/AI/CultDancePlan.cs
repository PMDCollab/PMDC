using System;
using RogueElements;
using RogueEssence;
using RogueEssence.Dungeon;
using RogueEssence.Data;
using Newtonsoft.Json;
using RogueEssence.Dev;

namespace PMDC.Dungeon
{
    /// <summary>
    /// Dances around an item until a team member is attacked or the item is gone
    /// </summary>
    [Serializable]
    public class CultDancePlan : AIPlan
    {
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string StatusIndex;

        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Item, false)]
        public string ItemIndex;

        public CultDancePlan() : base()
        { }
        public CultDancePlan(AIFlags iq, string item, string status) : base(iq)
        {
            StatusIndex = status;
            ItemIndex = item;
        }
        protected CultDancePlan(CultDancePlan other) : base(other) { StatusIndex = other.StatusIndex; ItemIndex = other.ItemIndex; }
        public override BasePlan CreateNew() { return new CultDancePlan(this); }

        public override GameAction Think(Character controlledChar, bool preThink, IRandom rand)
        {
            MapItem item;
            if (goBeserk(controlledChar, out item))
                return null;

            return dance(controlledChar, preThink, item);
        }

        private GameAction dance(Character controlledChar, bool preThink, MapItem targetItem)
        {
            //move in the approximate direction of the item
            Dir8 moveDir = (targetItem.TileLoc - controlledChar.CharLoc).ApproximateDir8();

            for (int ii = 0; ii < DirExt.DIR8_COUNT; ii++)
            {
                if (canWalk(controlledChar, !preThink, targetItem, moveDir))
                    return new GameAction(GameAction.ActionType.Move, moveDir, ((IQ & AIFlags.ItemGrabber) != AIFlags.None) ? 1 : 0);
                //if you can't or the tile IS the item, try a different direction
                moveDir = DirExt.AddAngles(moveDir, Dir8.DownRight);
            }

            //last resort, just stay there
            return new GameAction(GameAction.ActionType.Wait, Dir8.None);
        }

        private bool canWalk(Character controlledChar, bool respectPeers, MapItem targetItem, Dir8 testDir)
        {
            Loc endLoc = controlledChar.CharLoc + testDir.GetLoc();

            //check to see if it's possible to move in this direction
            bool blocked = Grid.IsDirBlocked(controlledChar.CharLoc, testDir,
                (Loc testLoc) =>
                {
                    if (IsPathBlocked(controlledChar, testLoc))
                        return true;

                    if (ZoneManager.Instance.CurrentMap.WrapLoc(testLoc) != targetItem.TileLoc && respectPeers)
                    {
                        Character destChar = ZoneManager.Instance.CurrentMap.GetCharAtLoc(testLoc);
                        if (!canPassChar(controlledChar, destChar, true))
                            return true;
                    }

                    return false;
                },
                (Loc testLoc) =>
                {
                    return (ZoneManager.Instance.CurrentMap.TileBlocked(testLoc, controlledChar.Mobility, true));
                },
                1);

            //if that direction is good, send the command to move in that direction
            if (blocked)
                return false;

            //check to see if moving in this direction will get to the target item
            if (ZoneManager.Instance.CurrentMap.WrapLoc(endLoc) == targetItem.TileLoc)
                return false;

            //not blocked
            return true;
        }

        private bool goBeserk(Character controlledChar, out MapItem seeItem)
        {
            seeItem = null;
            foreach (Character chara in controlledChar.MemberTeam.Players)
            {
                if (chara.GetStatusEffect(StatusIndex) != null)
                    return true;
            }

            Loc seen = Character.GetSightDims();
            Loc mapStart = controlledChar.CharLoc - seen;
            Loc mapSize = seen * 2 + new Loc(1);
            foreach (MapItem item in ZoneManager.Instance.CurrentMap.Items)
            {
                if (item.Value == ItemIndex && ZoneManager.Instance.CurrentMap.InBounds(new Rect(mapStart, mapSize), item.TileLoc))
                {
                    seeItem = item;
                    break;
                }
            }
            return seeItem == null;
        }
    }

}
