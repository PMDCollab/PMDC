using System;
using RogueElements;
using RogueEssence.Dungeon;
using RogueEssence;
using RogueEssence.LevelGen;
using PMDC.Dungeon;
using System.Collections.Generic;

namespace PMDC.LevelGen
{
    [Serializable]
    public abstract class BaseSealStep<T> : GenStep<T> where T : ListMapGenContext
    {
        protected enum SealType
        {
            Blocked,
            Locked,
            Key
        }

        public BaseSealStep()
        {
            this.Filters = new List<BaseRoomFilter>();
        }

        /// <summary>
        /// Determines the rooms that serve as a vault and are to be locked away.
        /// </summary>
        public List<BaseRoomFilter> Filters { get; set; }

        protected abstract void PlaceBorders(T map, Dictionary<Loc, SealType> sealList);

        public override void Apply(T map)
        {
            //Iterate every room/hall and coat the ones filtered
            List<RoomHallIndex> spawningRooms = new List<RoomHallIndex>();
            Dictionary<Loc, SealType> sealList = new Dictionary<Loc, SealType>();

            for (int ii = 0; ii < map.RoomPlan.RoomCount; ii++)
            {
                if (!BaseRoomFilter.PassesAllFilters(map.RoomPlan.GetRoomPlan(ii), this.Filters))
                    continue;
                spawningRooms.Add(new RoomHallIndex(ii, false));
            }

            for (int ii = 0; ii < map.RoomPlan.HallCount; ii++)
            {
                if (!BaseRoomFilter.PassesAllFilters(map.RoomPlan.GetHallPlan(ii), this.Filters))
                    continue;
                spawningRooms.Add(new RoomHallIndex(ii, true));
            }

            if (spawningRooms.Count == 0)
                return;

            for (int ii = 0; ii < spawningRooms.Count; ii++)
            {
                IFloorRoomPlan plan = map.RoomPlan.GetRoomHall(spawningRooms[ii]);

                //seal the sides and note edge cases
                for (int xx = plan.RoomGen.Draw.X+1; xx < plan.RoomGen.Draw.End.X-1; xx++)
                {
                    sealBorderRay(map, sealList, plan, new LocRay8(xx, plan.RoomGen.Draw.Y, Dir8.Up), Dir8.Left, Dir8.Right);
                    sealBorderRay(map, sealList, plan, new LocRay8(xx, plan.RoomGen.Draw.End.Y-1, Dir8.Down), Dir8.Left, Dir8.Right);
                }

                for (int yy = plan.RoomGen.Draw.Y+1; yy < plan.RoomGen.Draw.End.Y-1; yy++)
                {
                    sealBorderRay(map, sealList, plan, new LocRay8(plan.RoomGen.Draw.X, yy, Dir8.Left), Dir8.Up, Dir8.Down);
                    sealBorderRay(map, sealList, plan, new LocRay8(plan.RoomGen.Draw.End.X-1, yy, Dir8.Right), Dir8.Up, Dir8.Down);
                }

                //seal edge cases
                sealCornerRay(map, sealList, plan, new LocRay8(plan.RoomGen.Draw.X, plan.RoomGen.Draw.Y, Dir8.UpLeft));
                sealCornerRay(map, sealList, plan, new LocRay8(plan.RoomGen.Draw.End.X - 1, plan.RoomGen.Draw.Y, Dir8.UpRight));
                sealCornerRay(map, sealList, plan, new LocRay8(plan.RoomGen.Draw.X, plan.RoomGen.Draw.End.Y-1, Dir8.DownLeft));
                sealCornerRay(map, sealList, plan, new LocRay8(plan.RoomGen.Draw.End.X - 1, plan.RoomGen.Draw.End.Y - 1, Dir8.DownRight));
            }

            PlaceBorders(map, sealList);
        }

        /// <summary>
        /// chooses and caegorizes the tile to be sealed
        /// </summary>
        /// <param name="map"></param>
        /// <param name="sealList"></param>
        /// <param name="plan"></param>
        /// <param name="loc"></param>
        /// <param name="dir"></param>
        /// <returns>Whether it affected the tile outwards or not</returns>
        private bool sealBorderRay(T map, Dictionary<Loc, SealType> sealList, IFloorRoomPlan plan, LocRay8 locRay, Dir8 side1, Dir8 side2)
        {
            Loc forthLoc = locRay.Loc + locRay.Dir.GetLoc();

            bool hasAdjacent = false;
            bool hasCondition = false;
            for (int ii = 0; ii < plan.Adjacents.Count; ii++)
            {
                IFloorRoomPlan adjacentPlan = map.RoomPlan.GetRoomHall(plan.Adjacents[ii]);
                if (map.RoomPlan.InBounds(adjacentPlan.RoomGen.Draw, forthLoc))
                {
                    hasAdjacent = true;
                    if (BaseRoomFilter.PassesAllFilters(adjacentPlan, this.Filters))
                    {
                        hasCondition = true;
                        break;
                    }
                }
            }

            if (!hasAdjacent)
            {
                //in the case where the extending tile is within no adjacents
                //  all normal walls shall be turned into impassables
                //  everything else is saved into the lock list
                sealBorderTile(map, sealList, SealType.Locked, forthLoc);

                return true;
            }
            else if (!hasCondition)
            {
                //in the case where the extending tile is within an adjacent and that adjacent DOESNT pass filter
                //  all normal walls for the INWARD border shall be turned into impassables
                //  everything else for the INWARD border shall be saved into a key list

                if (!map.TileBlocked(forthLoc))
                    sealBorderTile(map, sealList, SealType.Key, locRay.Loc);
                else
                    sealBorderTile(map, sealList, SealType.Locked, locRay.Loc);

                //when transitioning between inward and outward
                //-when transitioning from outward to inward, the previous outward tile needs an inward check
                //-when transitioning from inward to outward, the current outward tile needs a inward check

                //in the interest of trading redundancy for simplicity, an inward block will just block the tiles to the sides
                //regardless of if they've already been blocked
                //redundancy will be handled by hashsets
                if (side1 != Dir8.None)
                {
                    Loc sideLoc = locRay.Loc + side1.GetLoc();
                    sealBorderTile(map, sealList, SealType.Locked, sideLoc);
                }
                if (side2 != Dir8.None)
                {
                    Loc sideLoc = locRay.Loc + side2.GetLoc();
                    sealBorderTile(map, sealList, SealType.Locked, sideLoc);
                }
                return false;
            }
            else
            {
                //in the case where the extending tile is within an adjacent and that adjacent passes filter
                //  do nothing and skip these tiles
                return true;
            }
        }


        private void sealCornerRay(T map, Dictionary<Loc, SealType> sealList, IFloorRoomPlan plan, LocRay8 locRay)
        {
            DirH dirH;
            DirV dirV;
            locRay.Dir.Separate(out dirH, out dirV);

            bool outwardsH = sealBorderRay(map, sealList, plan, new LocRay8(locRay.Loc, dirH.ToDir8()), dirV.ToDir8().Reverse(), Dir8.None);
            bool outwardsV = sealBorderRay(map, sealList, plan, new LocRay8(locRay.Loc, dirV.ToDir8()), dirH.ToDir8().Reverse(), Dir8.None);


            //when two directions of a corner tile face inward, or outward, or a combination of inward and outward
            //-both inward: needs to not be redundant across the two sides - handled by hashset, no action needed
            //-one inward and one outward: can coexist - no action needed
            //-both outward: needs to check the outward diagonal to see if it forces inward
            // -if it doesnt force inward, do an outward operation
            // -if it does, do an inward operation

            if (outwardsH && outwardsV)
                sealBorderRay(map, sealList, plan, locRay, Dir8.None, Dir8.None);
        }

        private void sealBorderTile(T map, Dictionary<Loc, SealType> sealList, SealType seal, Loc loc)
        {
            if (map.TileBlocked(loc))
                sealList[loc] = SealType.Blocked;
            else
            {
                SealType curSeal;
                if (sealList.TryGetValue(loc, out curSeal))
                {
                    if (curSeal < seal)
                        sealList[loc] = seal;
                }
                else
                    sealList[loc] = seal;
            }
        }
    }
}
