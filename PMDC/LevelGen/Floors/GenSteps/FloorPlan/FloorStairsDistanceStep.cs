using System;
using System.Collections.Generic;
using RogueElements;

namespace PMDC.LevelGen
{
    /// <summary>
    /// Adds the entrance and exit to the floor.  Is room-conscious.
    /// The algorithm will try to place them within and outside of a certain specified range in tiles.
    /// </summary>
    /// <typeparam name="TGenContext"></typeparam>
    /// <typeparam name="TEntrance"></typeparam>
    /// <typeparam name="TExit"></typeparam>
    [Serializable]
    public class FloorStairsDistanceStep<TGenContext, TEntrance, TExit> : BaseFloorStairsStep<TGenContext, TEntrance, TExit>
        where TGenContext : class, IFloorPlanGenContext, IPlaceableGenContext<TEntrance>, IPlaceableGenContext<TExit>
        where TEntrance : IEntrance
        where TExit : IExit
    {
        public FloorStairsDistanceStep()
        {
        }

        public FloorStairsDistanceStep(IntRange range, TEntrance entrance, TExit exit) : base(entrance, exit)
        {
            Distance = range;
        }

        public FloorStairsDistanceStep(IntRange range, List<TEntrance> entrances, List<TExit> exits) : base(entrances, exits)
        {
            Distance = range;
        }

        /// <summary>
        /// Range of distance in tiles that entrances and exits must be apart.  start-inclusive, end-exclusive
        /// </summary>
        public IntRange Distance { get; set; }

        /// <summary>
        /// Attempt to choose an outlet in a room with no entrance/exit, and updates their availability.  If none exists, default to a chosen room.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="map"></param>
        /// <param name="free_indices"></param>
        /// <param name="used_indices"></param>
        /// <returns></returns>
        protected override Loc? GetOutlet<T>(TGenContext map, List<int> free_indices, List<int> used_indices)
        {
            while (free_indices.Count > 0)
            {
                int roomIndex = map.Rand.Next() % free_indices.Count;
                int startRoom = free_indices[roomIndex];

                Rect startDraw = map.RoomPlan.GetRoom(startRoom).Draw;

                bool used = false;
                if (used_indices != null)
                {
                    foreach (int usedRoom in used_indices)
                    {
                        Rect usedDraw = map.RoomPlan.GetRoom(usedRoom).Draw;
                        if (!Distance.Contains((usedDraw.Start - startDraw.Start).Dist4()))
                        {
                            used = true;
                            break;
                        }
                    }
                }

                if (used)
                {
                    // if we're not on our backup list, move it to the backup list and continue on
                    if (used_indices != null)
                    {
                        free_indices.RemoveAt(roomIndex);
                        //TODO: come up with a third list for indices that can still be used in the final backup plan
                        //but must be avoided when checking existing used indices
                        //used_indices.Add(startRoom);
                        continue;
                    }
                }

                List<Loc> tiles = ((IPlaceableGenContext<T>)map).GetFreeTiles(startDraw);

                if (tiles.Count == 0)
                {
                    // this room is not suitable and never will be, remove it
                    free_indices.RemoveAt(roomIndex);
                    continue;
                }

                Loc start = tiles[map.Rand.Next(tiles.Count)];

                // if we have a used-list, transfer the index over
                if (used_indices != null)
                {
                    free_indices.RemoveAt(roomIndex);
                    used_indices.Add(startRoom);
                }

                return start;
            }

            return null;
        }
    }
}
