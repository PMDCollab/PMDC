using System;
using RogueElements;
using RogueEssence.Dungeon;
using System.Collections.Generic;
using RogueEssence;
using RogueEssence.LevelGen;

namespace PMDC.LevelGen
{
    /// <summary>
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
        const int MAP_HEIGHT = 6;
        const int MAP_WIDTH = 7;

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
            map.PlaceItem(new Loc(platStart.X + 1, platStart.Y), new EffectTile(33, true));
            for (int x = 0; x < platWidth; x++)
            {
                for (int y = 0; y < platHeight; y++)
                    map.GetPostProc(new Loc(platStart.X + x, platStart.Y + y)).Status[(int)PostProcType.Panel] = true;
            }
            map.SetTile(new Loc(Draw.X + 1, Draw.Y + ROOM_OFFSET), map.WallTerrain.Copy());
            map.GetPostProc(new Loc(Draw.X + 1, Draw.Y + ROOM_OFFSET)).Status[(int)PostProcType.Terrain] = true;
            map.SetTile(new Loc(Draw.X + 5, Draw.Y + ROOM_OFFSET), map.WallTerrain.Copy());
            map.GetPostProc(new Loc(Draw.X + 5, Draw.Y + ROOM_OFFSET)).Status[(int)PostProcType.Terrain] = true;


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
    }


    /// <summary>
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

        const int MAP_HEIGHT = 6;
        const int MAP_WIDTH = 5;

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
            map.PlaceItem(new Loc(platStart.X + 1, platStart.Y), new EffectTile(33, true));
            for (int x = 0; x < platWidth; x++)
            {
                for (int y = 0; y < platHeight; y++)
                    map.GetPostProc(new Loc(platStart.X + x, platStart.Y + y)).Status[(int)PostProcType.Panel] = true;
            }
            map.SetTile(new Loc(Draw.X + 2, Draw.Y + 1), map.WallTerrain.Copy());
            map.GetPostProc(new Loc(Draw.X + 2, Draw.Y + 1)).Status[(int)PostProcType.Terrain] = true;
            map.SetTile(new Loc(Draw.X + 1, Draw.Y + 4), map.WallTerrain.Copy());
            map.GetPostProc(new Loc(Draw.X + 1, Draw.Y + 4)).Status[(int)PostProcType.Terrain] = true;
            map.SetTile(new Loc(Draw.X + 3, Draw.Y + 4), map.WallTerrain.Copy());
            map.GetPostProc(new Loc(Draw.X + 3, Draw.Y + 4)).Status[(int)PostProcType.Terrain] = true;

            SetRoomBorders(map);
        }
    }
}
