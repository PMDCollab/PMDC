using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence.Dungeon;
using PMDC.Dungeon;
using RogueEssence.LevelGen;

namespace PMDC.LevelGen
{
    /// <summary>
    /// Spawns objects in randomly chosen rooms.  Once a room is chosen, it and adjacent rooms cannot be chosen.
    /// Large rooms have the same probability as small rooms.
    /// </summary>
    /// <typeparam name="TGenContext"></typeparam>
    /// <typeparam name="TSpawnable"></typeparam>
    [Serializable]
    public class SpacedRoomSpawnStep<TGenContext, TSpawnable> : RoomSpawnStep<TGenContext, TSpawnable>
        where TGenContext : class, IFloorPlanGenContext, IPlaceableGenContext<TSpawnable>
        where TSpawnable : ISpawnable
    {
        public SpacedRoomSpawnStep()
            : base()
        {
        }

        public SpacedRoomSpawnStep(IStepSpawner<TGenContext, TSpawnable> spawn, bool includeHalls = false)
            : base(spawn)
        {
            this.IncludeHalls = includeHalls;
        }

        /// <summary>
        /// Makes halls eligible for spawn.
        /// </summary>
        public bool IncludeHalls { get; set; }

        public override void DistributeSpawns(TGenContext map, List<TSpawnable> spawns)
        {
            HashSet<RoomHallIndex> takenRooms = new HashSet<RoomHallIndex>();

            // random per room, not per-tile
            var spawningRooms = new SpawnList<RoomHallIndex>();
            var remainingRooms = new SpawnList<RoomHallIndex>();

            for (int ii = 0; ii < map.RoomPlan.RoomCount; ii++)
            {
                if (!BaseRoomFilter.PassesAllFilters(map.RoomPlan.GetRoomPlan(ii), this.Filters))
                    continue;
                spawningRooms.Add(new RoomHallIndex(ii, false), 10);
            }

            if (this.IncludeHalls)
            {
                for (int ii = 0; ii < map.RoomPlan.HallCount; ii++)
                {
                    if (!BaseRoomFilter.PassesAllFilters(map.RoomPlan.GetHallPlan(ii), this.Filters))
                        continue;
                    spawningRooms.Add(new RoomHallIndex(ii, true), 10);
                }
            }

            while (spawningRooms.Count > 0 && spawns.Count > 0)
            {
                int randIndex = spawningRooms.PickIndex(map.Rand);
                RoomHallIndex roomIndex = spawningRooms.GetSpawn(randIndex);

                if (takenRooms.Contains(roomIndex))
                {
                    spawningRooms.RemoveAt(randIndex);
                    remainingRooms.Add(roomIndex, 10);
                    continue;
                }

                // try to spawn the item
                if (this.SpawnInRoom(map, roomIndex, spawns[spawns.Count - 1]))
                {
                    GenContextDebug.DebugProgress("Placed Object");

                    // remove the item spawn
                    spawns.RemoveAt(spawns.Count - 1);

                    spawningRooms.RemoveAt(randIndex);
                    takenRooms.Add(roomIndex);

                    //add adjacents to the takenRooms
                    List<RoomHallIndex> adjacent = map.RoomPlan.GetRoomHall(roomIndex).Adjacents;
                    for (int ii = 0; ii < adjacent.Count; ii++)
                        takenRooms.Add(adjacent[ii]);

                    if (!roomIndex.IsHall)
                    {
                        List<int> adjacentRooms = map.RoomPlan.GetAdjacentRooms(roomIndex.Index);
                        for (int ii = 0; ii < adjacentRooms.Count; ii++)
                            takenRooms.Add(new RoomHallIndex(ii, false));
                    }
                }
                else
                {
                    spawningRooms.RemoveAt(randIndex);
                }
            }

            //backup plan; spawn in remaining rooms
            this.SpawnRandInCandRooms(map, spawningRooms, spawns, 100);
        }

        public override string ToString()
        {
            return string.Format("{0}<{1}>: WithHalls:{2}", this.GetType().Name, typeof(TSpawnable).Name, this.IncludeHalls);
        }
    }
}
