using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence.Data;
using RogueEssence.Dungeon;

namespace PMDC.LevelGen
{
    [Serializable]
    public abstract class PatternPlacerStep<T> : GenStep<T> where T : class, IFloorPlanGenContext
    {
        public RandRange Amount;

        public SpawnList<PatternPlan> Maps;

        public List<BaseRoomFilter> Filters { get; set; }

        /// <summary>
        /// Allows halls as spawn.
        /// </summary>
        public bool IncludeHalls { get; set; }

        /// <summary>
        /// Allows terminal rooms as spawn.
        /// </summary>
        public bool AllowTerminal { get; set; }

        /// <summary>
        /// Determines which tiles are eligible to be painted on.
        /// </summary>
        public ITerrainStencil<T> TerrainStencil { get; set; }

        public PatternPlacerStep()
        {
            this.Maps = new SpawnList<PatternPlan>();
            this.Filters = new List<BaseRoomFilter>();
            this.TerrainStencil = new DefaultTerrainStencil<T>();
        }

        public override void Apply(T map)
        {
            int chosenAmount = Amount.Pick(map.Rand);
            if (chosenAmount == 0 || Maps.Count == 0)
                return;


            List<RoomHallIndex> openRooms = new List<RoomHallIndex>();
            //get all places that traps are eligible
            for (int ii = 0; ii < map.RoomPlan.RoomCount; ii++)
            {
                if (!BaseRoomFilter.PassesAllFilters(map.RoomPlan.GetRoomPlan(ii), this.Filters))
                    continue;
                if (!this.AllowTerminal && map.RoomPlan.GetAdjacents(new RoomHallIndex(ii, false)).Count <= 1)
                    continue;
                openRooms.Add(new RoomHallIndex(ii, false));
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

            Dictionary<string, Map> mapCache = new Dictionary<string, Map>();

            for (int ii = 0; ii < chosenAmount; ii++)
            {
                // add traps
                if (openRooms.Count > 0)
                {
                    int randIndex = map.Rand.Next(openRooms.Count);
                    IRoomGen room = map.RoomPlan.GetRoomHall(openRooms[randIndex]).RoomGen;
                    PatternPlan chosenPattern = Maps.Pick(map.Rand);

                    Map placeMap;
                    if (!mapCache.TryGetValue(chosenPattern.MapID, out placeMap))
                    {
                        placeMap = DataManager.Instance.GetMap(chosenPattern.MapID);
                        mapCache[chosenPattern.MapID] = placeMap;
                    }

                    // TODO: instead of transpose, just flipV and flipH with 50% for each?
                    bool transpose = (map.Rand.Next(2) == 0);
                    Loc size = placeMap.Size;
                    if (transpose)
                        size = size.Transpose();

                    bool[][] templateLocs = new bool[size.X][];
                    for (int xx = 0; xx < size.X; xx++)
                    {
                        templateLocs[xx] = new bool[size.Y];
                        for (int yy = 0; yy < size.Y; yy++)
                        {
                            if (!transpose)
                                templateLocs[xx][yy] = !map.RoomTerrain.TileEquivalent(placeMap.Tiles[xx][yy]);
                            else
                                templateLocs[xx][yy] = !map.RoomTerrain.TileEquivalent(placeMap.Tiles[yy][xx]);
                        }
                    }

                    //draw the pattern here
                    List<Loc> drawLocs = new List<Loc>();

                    //add the locs to the draw list based solely on terrain passable/impassable
                    switch (chosenPattern.Pattern)
                    {
                        case PatternPlan.PatternExtend.Single:
                            {
                                //center the placeMap on the room, and add the locs that intersect
                                Loc offset = room.Draw.Center - size / 2;
                                Rect centerRect = new Rect(offset, size);
                                for (int xx = room.Draw.X; xx < room.Draw.End.X; xx++)
                                {
                                    for (int yy = room.Draw.Y; yy < room.Draw.End.Y; yy++)
                                    {
                                        Loc destLoc = new Loc(xx, yy);
                                        if (Collision.InBounds(centerRect, destLoc))
                                        {
                                            Loc srcLoc = destLoc - centerRect.Start;
                                            if (templateLocs[srcLoc.X][srcLoc.Y])
                                                drawLocs.Add(destLoc);
                                        }
                                    }
                                }
                            }
                            break;
                        case PatternPlan.PatternExtend.Extrapolate:
                            {
                                //center the placeMap on the room, and add the locs that intersect
                                //if there is more room, extend the tiles outward
                                Loc offset = room.Draw.Center - size / 2;
                                Rect centerRect = new Rect(offset, size);
                                for (int xx = room.Draw.X; xx < room.Draw.End.X; xx++)
                                {
                                    for (int yy = room.Draw.Y; yy < room.Draw.End.Y; yy++)
                                    {
                                        Loc destLoc = new Loc(xx, yy);
                                        bool accept = false;
                                        if (Collision.InBounds(centerRect, destLoc))
                                            accept = true;
                                        else if (xx > centerRect.X && xx < centerRect.End.X - 1)
                                            accept = true;
                                        else if (yy > centerRect.Y && yy < centerRect.End.Y - 1)
                                            accept = true;
                                        else
                                        {
                                            //only diagonal extrapolations allowed at edges
                                            int x_diff = -1;
                                            if (xx < centerRect.X)
                                                x_diff = centerRect.X - xx;
                                            else if (xx >= centerRect.End.X)
                                                x_diff = xx - centerRect.End.X + 1;

                                            int y_diff = -1;
                                            if (yy < centerRect.Y)
                                                y_diff = centerRect.Y - yy;
                                            else if (yy >= centerRect.End.Y)
                                                y_diff = yy - centerRect.End.Y + 1;

                                            if (x_diff == y_diff)
                                                accept = true;
                                        }
                                        if (accept)
                                        {
                                            Loc srcLoc = Collision.ClampToBounds(centerRect, destLoc) - centerRect.Start;
                                            if (templateLocs[srcLoc.X][srcLoc.Y])
                                                drawLocs.Add(destLoc);
                                        }
                                    }
                                }
                            }
                            break;
                        case PatternPlan.PatternExtend.Repeat1D:
                            {
                                //tile the pattern horizontally, with centering
                                //or vertically, if transposed
                                Loc offset = room.Draw.Center - size / 2;
                                Rect centerRect = new Rect(offset, size);
                                for (int xx = room.Draw.X; xx < room.Draw.End.X; xx++)
                                {
                                    for (int yy = room.Draw.Y; yy < room.Draw.End.Y; yy++)
                                    {
                                        Loc destLoc = new Loc(xx, yy);
                                        bool accept = false;
                                        if (!transpose && Collision.InBounds(centerRect.Y, centerRect.Height, yy))
                                            accept = true;
                                        else if (transpose && Collision.InBounds(centerRect.X, centerRect.Width, xx))
                                            accept = true;

                                        if (accept)
                                        {
                                            Loc srcLoc = Loc.Wrap(destLoc - centerRect.Start, centerRect.Size);
                                            if (templateLocs[srcLoc.X][srcLoc.Y])
                                                drawLocs.Add(destLoc);
                                        }
                                    }
                                }
                            }
                            break;
                        case PatternPlan.PatternExtend.Repeat2D:
                            {
                                //tile the pattern on the entire room
                                Loc offset = room.Draw.Center - size / 2;
                                Rect centerRect = new Rect(offset, size);
                                for (int xx = room.Draw.X; xx < room.Draw.End.X; xx++)
                                {
                                    for (int yy = room.Draw.Y; yy < room.Draw.End.Y; yy++)
                                    {
                                        Loc destLoc = new Loc(xx, yy);
                                        Loc srcLoc = Loc.Wrap(destLoc - centerRect.Start, centerRect.Size);
                                        if (templateLocs[srcLoc.X][srcLoc.Y])
                                            drawLocs.Add(destLoc);
                                    }
                                }
                            }
                            break;
                    }

                    //then send it to the draw call
                    DrawOnLocs(map, drawLocs);

                    GenContextDebug.DebugProgress("Draw Pattern");

                    openRooms.RemoveAt(randIndex);
                }
            }
        }

        protected abstract void DrawOnLocs(T map, List<Loc> drawLocs);
    }

    [Serializable]
    public struct PatternPlan
    {
        public enum PatternExtend
        {
            Single,
            Extrapolate,
            Repeat1D,
            Repeat2D
        }

        /// <summary>
        /// Map file to load.
        /// </summary>
        [RogueEssence.Dev.DataFolder(0, "Map/")]
        public string MapID;

        public PatternExtend Pattern;

        public PatternPlan(string mapID, PatternExtend pattern)
        {
            MapID = mapID;
            Pattern = pattern;
        }
    }
}
