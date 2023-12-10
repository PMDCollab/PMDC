using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence.Data;
using RogueEssence.Dungeon;

namespace PMDC.LevelGen
{
    /// <summary>
    /// Sets terrain in a number of rooms to a certain value.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class RoomTerrainStep<T> : GenStep<T> where T : class, IFloorPlanGenContext
    {
        public RandRange Amount;

        public List<BaseRoomFilter> Filters { get; set; }

        /// <summary>
        /// Tile representing the water terrain to paint with.
        /// </summary>
        public ITile Terrain { get; set; }

        /// <summary>
        /// Determines which tiles are eligible to be painted on.
        /// </summary>
        public ITerrainStencil<T> TerrainStencil { get; set; }

        /// <summary>
        /// Makes halls eligible for spawn.
        /// </summary>
        public bool IncludeHalls { get; set; }

        /// <summary>
        /// Makes halls eligible for spawn.
        /// </summary>
        public bool IncludeRooms { get; set; }

        public RoomTerrainStep()
        {
            this.Filters = new List<BaseRoomFilter>();
            this.TerrainStencil = new DefaultTerrainStencil<T>();
        }

        public RoomTerrainStep(ITile terrain, RandRange amount, bool includeRooms, bool includeHalls) : this()
        {
            Terrain = terrain;
            Amount = amount;
            IncludeRooms = includeRooms;
            IncludeHalls = includeHalls;
        }

        public override void Apply(T map)
        {
            int chosenAmount = Amount.Pick(map.Rand);
            if (chosenAmount == 0)
                return;

            List<RoomHallIndex> openRooms = new List<RoomHallIndex>();
            if (this.IncludeRooms)
            {
                for (int ii = 0; ii < map.RoomPlan.RoomCount; ii++)
                {
                    if (BaseRoomFilter.PassesAllFilters(map.RoomPlan.GetRoomPlan(ii), this.Filters))
                        openRooms.Add(new RoomHallIndex(ii, false));
                }
            }

            if (this.IncludeHalls)
            {
                for (int ii = 0; ii < map.RoomPlan.HallCount; ii++)
                {
                    if (!BaseRoomFilter.PassesAllFilters(map.RoomPlan.GetHallPlan(ii), this.Filters))
                        continue;
                    openRooms.Add(new RoomHallIndex(ii, true));
                }
            }

            for (int ii = 0; ii < chosenAmount; ii++)
            {
                if (openRooms.Count > 0)
                {
                    int randIndex = map.Rand.Next(openRooms.Count);
                    IFloorRoomPlan plan;
                    if (openRooms[randIndex].IsHall)
                        plan = map.RoomPlan.GetHallPlan(openRooms[randIndex].Index);
                    else
                        plan = map.RoomPlan.GetRoomPlan(openRooms[randIndex].Index);


                    for (int xx = plan.RoomGen.Draw.X - 1; xx < plan.RoomGen.Draw.End.X + 1; xx++)
                    {
                        for (int yy = plan.RoomGen.Draw.Y - 1; yy < plan.RoomGen.Draw.End.Y + 1; yy++)
                        {
                            Loc destLoc = new Loc(xx, yy);
                            if (this.TerrainStencil.Test(map, destLoc))
                                map.TrySetTile(destLoc, this.Terrain.Copy());
                        }
                    }

                    openRooms.RemoveAt(randIndex);
                }
            }
            
        }

    }

}
