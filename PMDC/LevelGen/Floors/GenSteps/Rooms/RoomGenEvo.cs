using System;
using RogueElements;
using RogueEssence.Dungeon;
using System.Collections.Generic;
using RogueEssence;
using RogueEssence.LevelGen;
using RogueEssence.Dev;
using RogueEssence.Data;
using RogueEssence.Content;
using RogueEssence.Ground;
using PMDC.Dungeon;
using Newtonsoft.Json;

namespace PMDC.LevelGen
{
    /// <summary>
    /// THIS CLASS IS DEPRECATED.  Use RoomGenLoadEvo with a custom map using this shape instead.
    /// Generates an evolution room.  It's 7x6 in size and hardcoded to look a specific way.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class RoomGenEvo<T> : RoomGen<T> where T : ITiledGenContext, IPostProcGenContext, IPlaceableGenContext<EffectTile>
    {
        //?#####?
        //.#---#.
        //..---..
        //.......
        //.......
        //.......

        const int ROOM_OFFSET = 1;
        const int MAP_WIDTH = 7;
        const int MAP_HEIGHT = 6;

        public RoomGenEvo() { }
        public override RoomGen<T> Copy() { return new RoomGenEvo<T>(); }

        public override Loc ProposeSize(IRandom rand)
        {
            return new Loc(MAP_WIDTH, MAP_HEIGHT);
        }

        protected override void PrepareFulfillableBorders(IRandom rand)
        {
            if (Draw.Width != MAP_WIDTH || Draw.Height != MAP_HEIGHT)
            {
                foreach (Dir4 dir in DirExt.VALID_DIR4)
                {
                    for (int jj = 0; jj < FulfillableBorder[dir].Length; jj++)
                        FulfillableBorder[dir][jj] = true;
                }
            }
            else
            {
                for (int ii = 0; ii < Draw.Width; ii++)
                {
                    FulfillableBorder[Dir4.Up][ii] = ii == 0 || ii == Draw.Width-1;
                    FulfillableBorder[Dir4.Down][ii] = true;
                }

                for (int ii = 0; ii < Draw.Height; ii++)
                {
                    if (ii > 0)
                    {
                        FulfillableBorder[Dir4.Left][ii] = true;
                        FulfillableBorder[Dir4.Right][ii] = true;
                    }
                }
            }
        }

        public override void DrawOnMap(T map)
        {
            if (MAP_WIDTH != Draw.Width || MAP_HEIGHT != Draw.Height)
            {
                DrawMapDefault(map);
                return;
            }
            

            for (int x = 0; x < Draw.Width; x++)
            {
                for (int y = ROOM_OFFSET; y < Draw.Height; y++)
                    map.SetTile(new Loc(Draw.X + x, Draw.Y + y), map.RoomTerrain.Copy());
            }
            int platWidth = 3;
            int platHeight = 2;
            Loc platStart = Draw.Start + new Loc(2, ROOM_OFFSET);
            map.PlaceItem(new Loc(platStart.X + 1, platStart.Y), new EffectTile("tile_evo", true));
            //TODO: when it's possible to specify the border digging, this entire class can be deprecated
            for (int x = 0; x < platWidth; x++)
            {
                for (int y = 0; y < platHeight; y++)
                {
                    map.GetPostProc(new Loc(platStart.X + x, platStart.Y + y)).Status |= PostProcType.Panel;
                    map.GetPostProc(new Loc(platStart.X + x, platStart.Y + y)).Status |= PostProcType.Terrain;
                }
            }
            map.SetTile(new Loc(Draw.X + 1, Draw.Y + ROOM_OFFSET), map.WallTerrain.Copy());
            map.GetPostProc(new Loc(Draw.X + 1, Draw.Y + ROOM_OFFSET)).Status |= PostProcType.Terrain;
            map.SetTile(new Loc(Draw.X + 5, Draw.Y + ROOM_OFFSET), map.WallTerrain.Copy());
            map.GetPostProc(new Loc(Draw.X + 5, Draw.Y + ROOM_OFFSET)).Status |= PostProcType.Terrain;


            //dig tunnels within this room to hook up to the incoming demands
            List<IntRange> upReq = RoomSideReqs[Dir4.Up];
            bool left = false;
            bool right = false;
            for (int ii = 0; ii < upReq.Count; ii++)
            {
                bool hasLeft = upReq[ii].Contains(Draw.Start.X) && BorderToFulfill[Dir4.Up][0];
                bool hasRight = upReq[ii].Contains(Draw.End.X - 1) && BorderToFulfill[Dir4.Up][Draw.Width - 1];
                if (hasLeft && hasRight)
                {
                    if (map.Rand.Next(2) == 0)
                        left = true;
                    else
                        right = true;
                }
                else
                {
                    left |= hasLeft;
                    right |= hasRight;
                }
            }
            if (left)
                DigAtBorder(map, Dir4.Up, Draw.Start.X);
            if (right)
                DigAtBorder(map, Dir4.Up, Draw.End.X - 1);

            SetRoomBorders(map);
        }

        public override string ToString()
        {
            return string.Format("{0}", this.GetType().GetFormattedTypeName());
        }
    }


    /// <summary>
    /// THIS CLASS IS DEPRECATED.  Use RoomGenLoadEvo with a custom map using this shape instead.
    /// Generates an evolution room.  It's 5x6 in size and hardcoded to look a specific way.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class RoomGenEvoSmall<T> : PermissiveRoomGen<T> where T : ITiledGenContext, IPostProcGenContext, IPlaceableGenContext<EffectTile>
    {
        //.....
        //..#..
        //.---.
        //.---.
        //.#.#.
        //.....

        const int MAP_WIDTH = 5;
        const int MAP_HEIGHT = 6;

        public RoomGenEvoSmall() { }
        public override RoomGen<T> Copy() { return new RoomGenEvoSmall<T>(); }

        public override Loc ProposeSize(IRandom rand)
        {
            return new Loc(MAP_WIDTH, MAP_HEIGHT);
        }

        public override void DrawOnMap(T map)
        {
            if (MAP_WIDTH != Draw.Width || MAP_HEIGHT != Draw.Height)
            {
                DrawMapDefault(map);
                return;
            }

            for (int x = 0; x < Draw.Width; x++)
            {
                for (int y = 0; y < Draw.Height; y++)
                    map.SetTile(new Loc(Draw.X + x, Draw.Y + y), map.RoomTerrain.Copy());
            }
            int platWidth = 3;
            int platHeight = 2;
            Loc platStart = Draw.Start + new Loc(1, 2);
            map.PlaceItem(new Loc(platStart.X + 1, platStart.Y), new EffectTile("tile_evo", true));

            for (int x = 0; x < platWidth; x++)
            {
                for (int y = 0; y < platHeight; y++)
                {
                    map.GetPostProc(new Loc(platStart.X + x, platStart.Y + y)).Status |= PostProcType.Panel;
                    map.GetPostProc(new Loc(platStart.X + x, platStart.Y + y)).Status |= PostProcType.Terrain;
                }
            }
            map.SetTile(new Loc(Draw.X + 2, Draw.Y + 1), map.WallTerrain.Copy());
            map.GetPostProc(new Loc(Draw.X + 2, Draw.Y + 1)).Status |= PostProcType.Terrain;
            map.SetTile(new Loc(Draw.X + 1, Draw.Y + 4), map.WallTerrain.Copy());
            map.GetPostProc(new Loc(Draw.X + 1, Draw.Y + 4)).Status |= PostProcType.Terrain;
            map.SetTile(new Loc(Draw.X + 3, Draw.Y + 4), map.WallTerrain.Copy());
            map.GetPostProc(new Loc(Draw.X + 3, Draw.Y + 4)).Status |= PostProcType.Terrain;

            SetRoomBorders(map);
        }

        public override string ToString()
        {
            return string.Format("{0}", this.GetType().GetFormattedTypeName());
        }
    }



    /// <summary>
    /// Generates an evo room by loading a map as the room.
    /// Includes tiles, items, enemies, and mapstarts.
    /// Borders are specified by the walkable tile.
    /// Also, this will specifically apply postproc mask to terrain that is not the RoomTerrain
    /// Also, will apply postproc mask to the 3x2 tile area around the evo platform.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class RoomGenLoadEvo<T> : RoomGenLoadMapBase<T> where T : BaseMapGenContext
    {
        const int PLAT_WIDTH = 3;
        const int PLAT_HEIGHT = 2;
        const int PLAT_START_X = -1;
        const int PLAT_START_Y = 0;

        /// <summary>
        /// The ID of the tile used for evo.
        /// </summary>
        [JsonConverter(typeof(TileConverter))]
        [DataType(0, DataManager.DataType.Tile, false)]
        public string TriggerTile;

        public RoomGenLoadEvo()
        {
            TriggerTile = "";
        }


        protected RoomGenLoadEvo(RoomGenLoadEvo<T> other) : base(other)
        {
            this.TriggerTile = other.TriggerTile;
        }

        public override RoomGen<T> Copy() { return new RoomGenLoadEvo<T>(this); }



        public override void DrawOnMap(T map)
        {
            if (this.Draw.Width != this.roomMap.Width || this.Draw.Height != this.roomMap.Height)
            {
                this.DrawMapDefault(map);
                return;
            }

            //no copying is needed here since the map is disposed of after use

            DrawTiles(map);

            DrawDecorations(map);

            DrawItems(map);

            DrawMobs(map);

            DrawEntrances(map);

            this.FulfillRoomBorders(map, false);

            this.SetRoomBorders(map);

            for (int xx = 0; xx < Draw.Width; xx++)
            {
                for (int yy = 0; yy < Draw.Height; yy++)
                {
                    if (this.roomMap.Tiles[xx][yy].Data.ID != DataManager.Instance.GenFloor)
                        map.GetPostProc(new Loc(Draw.X + xx, Draw.Y + yy)).AddMask(new PostProcTile(PreventChanges));
                    if (this.roomMap.Tiles[xx][yy].Effect.ID == TriggerTile)
                    {
                        for (int x2 = 0; x2 < PLAT_WIDTH; x2++)
                        {
                            for (int y2 = 0; y2 < PLAT_HEIGHT; y2++)
                            {
                                Loc dest = new Loc(xx + PLAT_START_X + x2, yy + PLAT_START_Y + y2);
                                map.GetPostProc(Draw.Start + dest).AddMask(new PostProcTile(PreventChanges));
                            }
                        }
                    }
                }
            }
        }

        protected override void PrepareFulfillableBorders(IRandom rand)
        {
            // NOTE: Because the context is not passed in when preparing borders,
            // the tile ID representing an opening must be specified on this class instead.
            if (this.Draw.Width != this.roomMap.Width || this.Draw.Height != this.roomMap.Height)
            {
                foreach (Dir4 dir in DirExt.VALID_DIR4)
                {
                    for (int jj = 0; jj < this.FulfillableBorder[dir].Length; jj++)
                        this.FulfillableBorder[dir][jj] = true;
                }
            }
            else
            {
                HashSet<Dir4> satisfiedBorders = new HashSet<Dir4>();
                for (int ii = 0; ii < this.Draw.Width; ii++)
                {
                    if (this.roomMap.Tiles[ii][0].Data.ID == DataManager.Instance.GenFloor)
                    {
                        this.FulfillableBorder[Dir4.Up][ii] = true;
                        satisfiedBorders.Add(Dir4.Up);
                    }
                    if (this.roomMap.Tiles[ii][this.Draw.Height - 1].Data.ID == DataManager.Instance.GenFloor)
                    {
                        this.FulfillableBorder[Dir4.Down][ii] = true;
                        satisfiedBorders.Add(Dir4.Down);
                    }
                }

                for (int ii = 0; ii < this.Draw.Height; ii++)
                {
                    if (this.roomMap.Tiles[0][ii].Data.ID == DataManager.Instance.GenFloor)
                    {
                        this.FulfillableBorder[Dir4.Left][ii] = true;
                        satisfiedBorders.Add(Dir4.Left);
                    }
                    if (this.roomMap.Tiles[this.Draw.Width - 1][ii].Data.ID == DataManager.Instance.GenFloor)
                    {
                        this.FulfillableBorder[Dir4.Right][ii] = true;
                        satisfiedBorders.Add(Dir4.Right);
                    }
                }

                //backup plan: permit any borders that do not have unbreakables
                for (int ii = 0; ii < this.Draw.Width; ii++)
                {
                    if (!satisfiedBorders.Contains(Dir4.Up) && this.roomMap.Tiles[ii][0].Data.ID != DataManager.Instance.GenUnbreakable)
                        this.FulfillableBorder[Dir4.Up][ii] = true;

                    if (!satisfiedBorders.Contains(Dir4.Down) && this.roomMap.Tiles[ii][this.Draw.Height - 1].Data.ID != DataManager.Instance.GenUnbreakable)
                        this.FulfillableBorder[Dir4.Down][ii] = true;
                }

                for (int ii = 0; ii < this.Draw.Height; ii++)
                {
                    if (!satisfiedBorders.Contains(Dir4.Left) && this.roomMap.Tiles[0][ii].Data.ID != DataManager.Instance.GenUnbreakable)
                        this.FulfillableBorder[Dir4.Left][ii] = true;

                    if (!satisfiedBorders.Contains(Dir4.Right) && this.roomMap.Tiles[this.Draw.Width - 1][ii].Data.ID != DataManager.Instance.GenUnbreakable)
                        this.FulfillableBorder[Dir4.Right][ii] = true;
                }
            }
        }
    }
}
