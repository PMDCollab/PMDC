using System;
using RogueElements;
using System.Collections.Generic;
using System.Reactive.Joins;

namespace PMDC.LevelGen
{
    //TODO: Break this awful pathgen down.  Maybe AddBranch needs a GridPathBranchSpread equivalent?
    /// <summary>
    /// An awful and overspecific floor plan made specifically for one floor of one dungeon.
    /// Places a giant room somewhere in the center of the mega floor
    /// Places a connecting room directly below it
    /// Behaves like GridPathBranch starting from that connecting room.
    /// Don't use this class anywhere else.  It needs to be broken down later.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class GridPathPyramid<T> : GridPathStartStepGeneric<T>
        where T : class, IRoomGridGenContext
    {

        /// <summary>
        /// The percentage of total rooms in the grid plan that the step aims to fill.
        /// </summary>
        public RandRange RoomRatio { get; set; }

        /// <summary>
        /// The percent amount of branching paths the layout will have in relation to its straight paths.
        /// 0 = A layout without branches. (Worm)
        /// 50 = A layout that branches once for every two extensions. (Tree)
        /// 100 = A layout that branches once for every extension. (Branchier Tree)
        /// 200 = A layout that branches twice for every extension. (Fuzzy Worm)
        /// </summary>
        public RandRange BranchRatio { get; set; }


        public Loc GiantHallSize;

        /// <summary>
        /// The room types that can be used for the giant room in the layout.
        /// </summary>
        public SpawnList<RoomGen<T>> GiantHallGen;

        /// <summary>
        /// Components that the giant room will be labeled with.
        /// </summary>
        public ComponentCollection LargeRoomComponents { get; set; }

        /// <summary>
        /// Components that the rooms in the furthest direction from the giant room in a cardinal will be given, in addition to the normal components.
        /// </summary>
        public ComponentCollection CornerRoomComponents { get; set; }

        /// <summary>
        /// Prevents the step from making branches in the path, even if it would fail the space-fill quota.
        /// </summary>
        public bool NoForcedBranches { get; set; }

        public GridPathPyramid()
            : base()
        {
            GiantHallGen = new SpawnList<RoomGen<T>>();
            LargeRoomComponents = new ComponentCollection();
            CornerRoomComponents = new ComponentCollection();
        }

        public override void ApplyToPath(IRandom rand, GridPlan floorPlan)
        {
            RoomGen<T> roomGen = GiantHallGen.Pick(rand);
            if (roomGen == null)
                roomGen = GenericRooms.Pick(rand);

            Loc sizeDiff = new Loc(floorPlan.GridWidth - GiantHallSize.X, floorPlan.GridHeight - GiantHallSize.Y);

            //the pyramid is a certain size A, the floor is a certain size B
            //the placeable distance the pyramid can be between it an a wall is B - A = C
            //the max distance that all sides (of an axis) can be from the wall simultaneously is C / 2.
            //Basically, if we were to place a requirement of how far a given side can be from the wall, the maximum we can specify is C / 2
            //to give placement some wiggle room, we divide this value by 2.  This will allow for wiggle room of half the total room it can actually move in.
            //This will allow it to move while not bing potentially placed too close to the edge.
            //C / 4
            Loc giantHallLoc = new Loc(rand.Next(sizeDiff.X / 4, floorPlan.GridWidth - GiantHallSize.X - sizeDiff.X / 4),
                rand.Next(sizeDiff.Y / 4, floorPlan.GridHeight - GiantHallSize.Y - sizeDiff.Y / 4));

            Rect destRect = new Rect(giantHallLoc.X, giantHallLoc.Y, GiantHallSize.X, GiantHallSize.Y);
            floorPlan.AddRoom(destRect, roomGen, this.LargeRoomComponents.Clone());

            GenContextDebug.DebugProgress("Center Room");

            floorPlan.AddRoom(new Loc(destRect.Center.X, destRect.Bottom), GenericRooms.Pick(rand), this.RoomComponents.Clone());
            SafeAddHall(new LocRay4(new Loc(destRect.Center.X, destRect.Bottom), Dir4.Up),
                    floorPlan, GenericHalls.Pick(rand), GetDefaultGen(), this.RoomComponents, this.HallComponents, true);

            GenContextDebug.DebugProgress("Add Leg");


            //These were copied from IGridPathBranch except for 3 differences.  FIND A WAY TO NOT REPEAT THIS CODE.

            int roomsToOpen = (floorPlan.GridWidth * floorPlan.GridHeight - destRect.Width * destRect.Height) * this.RoomRatio.Pick(rand) / 100;//DIFFERENCE 1
            if (roomsToOpen < 1)
                roomsToOpen = 1;

            int addBranch = this.BranchRatio.Pick(rand);
            int roomsLeft = roomsToOpen;
            List<Loc> terminals = new List<Loc>();
            List<Loc> branchables = new List<Loc>();

            // place first room
            Loc sourceRoom = new Loc(destRect.Center.X, destRect.Bottom);//DIFFERENCE 2

            // add the room to a terminals list ONCE //DIFFERENCE 3
            terminals.Add(sourceRoom);

            GenContextDebug.DebugProgress("Start Room");

            roomsLeft--;
            int pendingBranch = 0;
            while (roomsLeft > 0)
            {
                // pop a random loc from the terminals list
                Loc newTerminal = this.PopRandomLoc(floorPlan, rand, terminals);

                // find the directions to extend to
                SpawnList<LocRay4> availableRays = this.GetExpandDirChances(floorPlan, newTerminal);

                if (availableRays.Count > 0)
                {
                    // extend the path a random direction
                    LocRay4 terminalRay = availableRays.Pick(rand);
                    this.ExpandPath(rand, floorPlan, terminalRay);
                    Loc newRoomLoc = terminalRay.Traverse(1);
                    roomsLeft--;

                    // add the new terminal location to the terminals list
                    terminals.Add(newRoomLoc);
                    if (floorPlan.RoomCount > 2)
                    {
                        if (availableRays.Count > 1)
                            branchables.Add(newTerminal);

                        pendingBranch += addBranch;
                    }
                }
                else if (terminals.Count == 0)
                {
                    if (this.NoForcedBranches)
                        break;
                    else
                        pendingBranch = 100;
                }

                while (pendingBranch >= 100 && roomsLeft > 0 && branchables.Count > 0)
                {
                    // pop a random loc from the branchables list
                    Loc newBranch = this.PopRandomLoc(floorPlan, rand, branchables);

                    // find the directions to extend to
                    SpawnList<LocRay4> availableBranchRays = this.GetExpandDirChances(floorPlan, newBranch);

                    if (availableBranchRays.Count > 0)
                    {
                        // extend the path a random direction
                        LocRay4 branchRay = availableBranchRays.Pick(rand);
                        this.ExpandPath(rand, floorPlan, branchRay);
                        Loc newRoomLoc = branchRay.Traverse(1);
                        roomsLeft--;

                        // add the new terminal location to the terminals list
                        terminals.Add(newRoomLoc);
                        if (availableBranchRays.Count > 1)
                            branchables.Add(newBranch);

                        pendingBranch -= 100;
                    }
                }

                if (terminals.Count == 0 && branchables.Count == 0)
                    break;
            }

            //mark the corners
            foreach (Dir8 dir in DirExt.VALID_DIR8)
            {
                if (dir.IsDiagonal())
                {
                    Loc diffLoc = dir.GetLoc();
                    Loc roomCenter = destRect.Center;
                    int eligibleRoom = -1;

                    while (true)
                    {
                        //get the room.  Is it eligible?  update the current eligible
                        int newRoom = floorPlan.GetRoomIndex(roomCenter);
                        if (newRoom > 0)
                        {
                            GridRoomPlan plan = floorPlan.GetRoomPlan(newRoom);
                            if (!plan.PreferHall)
                                eligibleRoom = newRoom;
                        }

                        if (Collision.InBounds(floorPlan.GridWidth, floorPlan.GridHeight, roomCenter + diffLoc))
                            roomCenter += diffLoc;
                        else
                        {
                            DirH horiz;
                            DirV vert;
                            dir.Separate(out horiz, out vert);
                            if (Collision.InBounds(floorPlan.GridWidth, floorPlan.GridHeight, roomCenter + horiz.GetLoc()))
                                roomCenter += horiz.GetLoc();
                            else if (Collision.InBounds(floorPlan.GridWidth, floorPlan.GridHeight, roomCenter + vert.GetLoc()))
                                roomCenter += vert.GetLoc();
                            else
                                break;
                        }

                    }

                    if (eligibleRoom > -1)
                    {
                        //add the component here
                        GridRoomPlan plan = floorPlan.GetRoomPlan(eligibleRoom);
                        foreach(RoomComponent cmp in CornerRoomComponents)
                            plan.Components.Set(cmp);
                    }
                }
            }



        }

        public override string ToString()
        {
            return string.Format("{0}: Fill:{1}% Branch:{2}%", this.GetType().GetFormattedTypeName(), this.RoomRatio, this.BranchRatio);
        }


        //These were copied from GridPathBranchSpread.  FIND A WAY TO NOT REPEAT THIS CODE.

        /// <summary>
        /// Gets the directions a room can expand in.
        /// </summary>
        /// <param name="floorPlan"></param>
        /// <param name="loc"></param>
        /// <returns></returns>
        protected static IEnumerable<Dir4> GetRoomExpandDirs(GridPlan floorPlan, Loc loc)
        {
            foreach (Dir4 dir in DirExt.VALID_DIR4)
            {
                Loc endLoc = loc + dir.GetLoc();
                if ((floorPlan.Wrap || Collision.InBounds(floorPlan.GridWidth, floorPlan.GridHeight, endLoc))
                    && floorPlan.GetRoomIndex(endLoc) == -1)
                    yield return dir;
            }
        }

        protected bool ExpandPath(IRandom rand, GridPlan floorPlan, LocRay4 chosenRay)
        {
            floorPlan.SetHall(chosenRay, this.GenericHalls.Pick(rand), this.HallComponents.Clone());
            floorPlan.AddRoom(chosenRay.Traverse(1), this.GenericRooms.Pick(rand), this.RoomComponents.Clone());

            GenContextDebug.DebugProgress("Added Path");
            return true;
        }

        protected virtual Loc PopRandomLoc(GridPlan floorPlan, IRandom rand, List<Loc> locs)
        {
            //choose the location with the lowest adjacencies out of 5
            int branchIdx = 0;
            Loc branch = locs[0];
            int rating = getRating(floorPlan, branch);

            for (int ii = 1; ii < locs.Count; ii++)
            {
                int newBranchIdx = ii;
                Loc newBranch = locs[newBranchIdx];
                int newRating = getRating(floorPlan, newBranch);
                if (newRating > rating)
                {
                    branchIdx = newBranchIdx;
                    branch = newBranch;
                    rating = newRating;

                    if (newRating >= 10)
                        break;
                }
            }
            locs.RemoveAt(branchIdx);
            return branch;
        }

        private int getRating(GridPlan floorPlan, Loc branch)
        {
            int rating = 0;
            foreach (Dir8 checkDir in DirExt.VALID_DIR8)
            {
                Loc checkLoc = branch + checkDir.GetLoc();
                //TODO: actually, count out of bounds as empty room
                if ((floorPlan.Wrap || Collision.InBounds(floorPlan.GridWidth, floorPlan.GridHeight, checkLoc))
                    && floorPlan.GetRoomIndex(checkLoc) == -1)
                {
                    if (checkDir.IsDiagonal())
                        rating += 1;
                    else
                        rating *= 2;
                }
            }

            return rating;
        }


        protected virtual SpawnList<LocRay4> GetExpandDirChances(GridPlan floorPlan, Loc newTerminal)
        {
            SpawnList<LocRay4> availableRays = new SpawnList<LocRay4>();
            foreach (Dir4 dir in DirExt.VALID_DIR4)
            {
                Loc endLoc = newTerminal + dir.GetLoc();
                if ((floorPlan.Wrap || Collision.InBounds(floorPlan.GridWidth, floorPlan.GridHeight, endLoc))
                    && floorPlan.GetRoomIndex(endLoc) == -1)
                {
                    //can be added, but how much free space is around this potential coordinate?
                    //become ten times more likely to be picked for every free cardinal space around this destination
                    int chance = 1;
                    foreach (Dir8 checkDir in DirExt.VALID_DIR8)
                    {
                        if (checkDir != dir.ToDir8().Reverse())
                        {
                            //TODO: actually, count out of bounds as empty room
                            Loc checkLoc = endLoc + checkDir.GetLoc();
                            if ((floorPlan.Wrap || Collision.InBounds(floorPlan.GridWidth, floorPlan.GridHeight, checkLoc))
                                && floorPlan.GetRoomIndex(checkLoc) == -1)
                            {
                                if (checkDir.IsDiagonal())
                                    chance *= 3;
                                else
                                    chance *= 10;
                            }
                        }
                    }
                    availableRays.Add(new LocRay4(newTerminal, dir), chance);
                }
            }
            return availableRays;
        }
    }
}
