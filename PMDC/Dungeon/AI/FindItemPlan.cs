﻿using System;
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
        public FindItemPlan(AIFlags iq) : base(iq)
        {
        }
        protected FindItemPlan(FindItemPlan other) : base(other) { }
        public override BasePlan CreateNew() { return new FindItemPlan(this); }

        public override GameAction Think(Character controlledChar, bool preThink, IRandom rand)
        {
            if (!String.IsNullOrEmpty(controlledChar.EquippedItem.ID))
                return null;

            return base.Think(controlledChar, preThink, rand);
        }

        protected override List<Loc> GetDestinations(Character controlledChar)
        {
            //get all tiles that are within the border of sight range, or within the border of the screen
            Loc seen = Character.GetSightDims();
            Loc mapStart = controlledChar.CharLoc - seen;
            Loc mapSize = seen * 2 + new Loc(1);

            List<Loc> loc_list = new List<Loc>();
            //currently, CPU sight cheats by knowing items up to the bounds, instead of individual tiles at the border of FOV.
            //fix later
            foreach (MapItem item in ZoneManager.Instance.CurrentMap.Items)
            {
                if (!item.IsMoney && ZoneManager.Instance.CurrentMap.InBounds(new Rect(mapStart, mapSize), item.TileLoc))
                    TryAddDest(controlledChar, loc_list, item.TileLoc);
            }
            return loc_list;
        }
    }
}
