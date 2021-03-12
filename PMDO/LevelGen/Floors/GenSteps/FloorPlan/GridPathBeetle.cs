using System;
using RogueElements;

namespace PMDO.LevelGen
{
    [Serializable]
    public class GridPathBeetle<T> : GridPathStartStepGeneric<T>
        where T : class, IRoomGridGenContext
    {
        public bool Vertical;
        public SpawnList<RoomGen<T>> GiantHallGen;
        public ComponentCollection LargeRoomComponents { get; set; }
        public int LegPercent;
        public int ConnectPercent;
        public bool FromCorners;

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
            return string.Format("{0}: Vert:{1} Leg:{2}% Connect:{2}%", this.GetType().Name, this.Vertical, this.LegPercent, this.ConnectPercent);
        }
    }
}
