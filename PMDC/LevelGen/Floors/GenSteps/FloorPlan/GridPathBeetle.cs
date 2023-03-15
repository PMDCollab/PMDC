using System;
using RogueElements;

namespace PMDC.LevelGen
{
    /// <summary>
    /// Populates the empty floor plan of a map by creating a path consisting of one big room in the middle, with normal rooms connected to it.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class GridPathBeetle<T> : GridPathStartStepGeneric<T>
        where T : class, IRoomGridGenContext
    {
        /// <summary>
        /// Choose a horizontal or vertical orientation.
        /// </summary>
        public bool Vertical;

        /// <summary>
        /// The number of small rooms attached to the main large room, as a percent of the rooms possible.
        /// </summary>
        public int LegPercent;

        /// <summary>
        /// The number of connections between adjacent small rooms, as a percent of the connections possible.
        /// </summary>
        public int ConnectPercent;

        /// <summary>
        /// Allows the main body to be in a corner instead of in the center.
        /// </summary>
        public bool FromCorners;


        /// <summary>
        /// The room types that can be used for the giant room in the layout.
        /// </summary>
        public SpawnList<RoomGen<T>> GiantHallGen;

        /// <summary>
        /// Components that the giant room will be labeled with.
        /// </summary>
        public ComponentCollection LargeRoomComponents { get; set; }

        public GridPathBeetle()
            : base()
        {
            GiantHallGen = new SpawnList<RoomGen<T>>();
            LargeRoomComponents = new ComponentCollection();
        }

        public override void ApplyToPath(IRandom rand, GridPlan floorPlan)
        {
            int gapLength = Vertical ? floorPlan.GridHeight : floorPlan.GridWidth;
            int sideLength = Vertical ? floorPlan.GridWidth : floorPlan.GridHeight;

            if (gapLength < 3 || sideLength < 2)
            {
                CreateErrorPath(rand, floorPlan);
                return;
            }

            //add the body
            int chosenTier = FromCorners ? (rand.Next(2) * gapLength - 1) : rand.Next(1, gapLength - 1);

            RoomGen<T> roomGen = GiantHallGen.Pick(rand);
            if (roomGen == null)
                roomGen = GenericRooms.Pick(rand);
            floorPlan.AddRoom(new Rect(Vertical ? 0 : chosenTier, Vertical ? chosenTier : 0, Vertical ? sideLength : 1, Vertical ? 1 : sideLength), roomGen, this.LargeRoomComponents.Clone());

            GenContextDebug.DebugProgress("Center Room");

            //add the legs
            for (int ii = 0; ii < sideLength; ii++)
            {
                if (chosenTier > 0)
                {
                    if (rand.Next(100) < LegPercent)
                    {
                        int roomTier = rand.Next(0, chosenTier);
                        floorPlan.AddRoom(new Loc(Vertical ? ii : roomTier, Vertical ? roomTier : ii), GenericRooms.Pick(rand), this.RoomComponents.Clone());
                        for(int jj = roomTier; jj < chosenTier; jj++)
                            SafeAddHall(new LocRay4(new Loc(Vertical ? ii : jj, Vertical ? jj : ii), Vertical ? Dir4.Down : Dir4.Right),
                                floorPlan, GenericHalls.Pick(rand), GetDefaultGen(), this.RoomComponents, this.HallComponents, true);

                        GenContextDebug.DebugProgress("Add Leg");

                        int hasRoom = -1;
                        for (int jj = ii - 1; jj >= 0; jj--)
                        {
                            if (floorPlan.GetRoomPlan(new Loc(Vertical ? jj : roomTier, Vertical ? roomTier : jj)) != null)
                            {
                                hasRoom = jj;
                                break;
                            }
                        }
                        if (ii > 0 && hasRoom > -1)
                        {
                            if (rand.Next(100) < ConnectPercent)
                            {
                                for (int jj = ii; jj > hasRoom; jj--)
                                {
                                    SafeAddHall(new LocRay4(new Loc(Vertical ? jj : roomTier, Vertical ? roomTier : jj), Vertical ? Dir4.Left : Dir4.Up),
                                        floorPlan, GenericHalls.Pick(rand), GetDefaultGen(), this.RoomComponents, this.HallComponents, true);

                                    GenContextDebug.DebugProgress("Connect Leg");
                                }
                            }
                        }
                    }
                }
                if (chosenTier < gapLength - 1)
                {
                    if (rand.Next(100) < LegPercent)
                    {
                        int roomTier = rand.Next(chosenTier + 1, gapLength);
                        floorPlan.AddRoom(new Loc(Vertical ? ii : roomTier, Vertical ? roomTier : ii), GenericRooms.Pick(rand), this.RoomComponents.Clone());
                        for (int jj = chosenTier; jj < roomTier; jj++)
                            SafeAddHall(new LocRay4(new Loc(Vertical ? ii : jj, Vertical ? jj : ii), Vertical ? Dir4.Down : Dir4.Right),
                                floorPlan, GenericHalls.Pick(rand), GetDefaultGen(), this.RoomComponents, this.HallComponents, true);

                        GenContextDebug.DebugProgress("Add Leg");

                        int hasRoom = -1;
                        for (int jj = ii - 1; jj >= 0; jj--)
                        {
                            if (floorPlan.GetRoomPlan(new Loc(Vertical ? jj : roomTier, Vertical ? roomTier : jj)) != null)
                            {
                                hasRoom = jj;
                                break;
                            }
                        }
                        if (ii > 0 && hasRoom > -1)
                        {
                            if (rand.Next(100) < ConnectPercent)
                            {
                                for (int jj = ii; jj > hasRoom; jj--)
                                {
                                    SafeAddHall(new LocRay4(new Loc(Vertical ? jj : roomTier, Vertical ? roomTier : jj), Vertical ? Dir4.Left : Dir4.Up),
                                        floorPlan, GenericHalls.Pick(rand), GetDefaultGen(), this.RoomComponents, this.HallComponents, true);

                                    GenContextDebug.DebugProgress("Connect Leg");
                                }
                            }
                        }
                    }
                }
            }
        }

        public override string ToString()
        {
            return string.Format("{0}: Vert:{1} Leg:{2}% Connect:{2}%", this.GetType().GetFormattedTypeName(), this.Vertical, this.LegPercent, this.ConnectPercent);
        }
    }
}
