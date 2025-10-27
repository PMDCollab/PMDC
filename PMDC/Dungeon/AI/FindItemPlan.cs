using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence;
using RogueEssence.Dungeon;
using RogueEssence.Data;

namespace PMDC.Dungeon
{
    [Serializable]
    public class FindItemPlan : ExplorePlan
    {

        public bool IncludeMoney;

        public FindItemPlan(AIFlags iq, bool includeMoney) : base(iq)
        {
            IncludeMoney = includeMoney;
        }
        protected FindItemPlan(FindItemPlan other) : base(other)
        {
            IncludeMoney = other.IncludeMoney;
        }
        public override BasePlan CreateNew() { return new FindItemPlan(this); }

        public override GameAction Think(Character controlledChar, bool preThink, IRandom rand)
        {
            if(!IncludeMoney) {
                if (controlledChar.MemberTeam is ExplorerTeam)
                {
                    ExplorerTeam explorerTeam = (ExplorerTeam)controlledChar.MemberTeam;
                    if (explorerTeam.GetInvCount() >= explorerTeam.GetMaxInvSlots(ZoneManager.Instance.CurrentZone))
                        return null;
                }
                else
                {
                    //already holding an item
                    if (!String.IsNullOrEmpty(controlledChar.EquippedItem.ID))
                        return null;
                }
            }

            return base.Think(controlledChar, preThink, rand);
        }

        protected override List<Loc> GetDestinations(Character controlledChar)
        {
            //get all tiles that are within the border of sight range, or within the border of the screen
            Loc seen = Character.GetSightDims();
            Loc mapStart = controlledChar.CharLoc - seen;
            Loc mapSize = seen * 2 + new Loc(1);
            bool moneyOnly = false; //In case inventory is full, go only after money!
            
            List<Loc> loc_list = new List<Loc>();
            
            //In case inventory is full, go only after money!
            if (controlledChar.MemberTeam is ExplorerTeam)
            {
                ExplorerTeam explorerTeam = (ExplorerTeam)controlledChar.MemberTeam;
                if (explorerTeam.GetInvCount() >= explorerTeam.GetMaxInvSlots(ZoneManager.Instance.CurrentZone))
                    moneyOnly = true;
            }
            if(moneyOnly && !IncludeMoney) return loc_list; // Should not be possible... But just to make sure...
            
            //currently, CPU sight cheats by knowing items up to the bounds, instead of individual tiles at the border of FOV.
            //fix later
            foreach (MapItem item in ZoneManager.Instance.CurrentMap.Items)
            {
                if(moneyOnly && !item.IsMoney) continue; //In case inventory is full, go only after money!
                if (item.IsMoney && !IncludeMoney) continue;
                if (!item.IsMoney && item.Price > 0) continue;
                
                if (ZoneManager.Instance.CurrentMap.InBounds(new Rect(mapStart, mapSize), item.TileLoc))
                    TryAddDest(controlledChar, loc_list, item.TileLoc);
            }
            return loc_list;
        }
    }
}
