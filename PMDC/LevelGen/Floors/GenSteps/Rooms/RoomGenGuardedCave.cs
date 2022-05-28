using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence.Dungeon;
using RogueEssence;
using RogueEssence.LevelGen;

namespace PMDC.LevelGen
{
    /// <summary>
    /// Generates a cave guarded by a single mob.  It's hardcoded to look a specific way.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class RoomGenGuardedCave<T> : RoomGen<T> where T : IUnbreakableGenContext, IPlaceableGenContext<MapItem>, IPlaceableGenContext<EffectTile>, IGroupPlaceableGenContext<TeamSpawn>, IMobSpawnMap
    {
        //ex. 2x2
        //?XXXX?
        //?X..X?
        //?X..X?
        //?X.XX?
        //?...X?
        //?...??

        //ex. 3x3
        //?XXXXX?
        //?X...X?
        //?X...X?
        //?X...X?
        //?XX.XX?
        //?X...X?
        //??...??

        public BulkSpawner<T, MapItem> Treasures;
        public BulkSpawner<T, EffectTile> TileTreasures;
        public SpawnList<MobSpawn> GuardTypes;

        public RoomGenGuardedCave()
        {
            Treasures = new BulkSpawner<T, MapItem>();
            TileTreasures = new BulkSpawner<T, EffectTile>();
            GuardTypes = new SpawnList<MobSpawn>();
        }
        protected RoomGenGuardedCave(RoomGenGuardedCave<T> other)
        {
            Treasures = other.Treasures.Copy();
            TileTreasures = other.TileTreasures.Copy();
            GuardTypes = new SpawnList<MobSpawn>();
            for (int ii = 0; ii < other.GuardTypes.Count; ii++)
                GuardTypes.Add(other.GuardTypes.GetSpawn(ii).Copy(), other.GuardTypes.GetSpawnRate(ii));
        }
        public override RoomGen<T> Copy() { return new RoomGenGuardedCave<T>(this); }

        public override Loc ProposeSize(IRandom rand)
        {
            Loc caveSize = new Loc(1);
            while (caveSize.X * caveSize.Y < Treasures.SpawnAmount + TileTreasures.SpawnAmount + 1)
            {
                if (caveSize.X > caveSize.Y)
                    caveSize.Y++;
                else
                    caveSize.X++;
            }

            return caveSize + new Loc(4);
        }

        protected override void PrepareFulfillableBorders(IRandom rand)
        {
            Loc size = ProposeSize(rand);
            if (Draw.Width != size.X || Draw.Height != size.Y)
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
                    FulfillableBorder[Dir4.Up][ii] = ii == 0 || ii == Draw.Width - 1;
                    FulfillableBorder[Dir4.Down][ii] = (ii >= Draw.Width / 2 - 1 && ii <= Draw.Width / 2 + 1);
                }


                for (int ii = 0; ii < Draw.Height; ii++)
                {
                    FulfillableBorder[Dir4.Left][ii] = (ii == Draw.Height - 1);
                    FulfillableBorder[Dir4.Right][ii] = (ii == Draw.Height - 1);
                }
            }
        }

        public override void DrawOnMap(T map)
        {
            Loc caveSize = ProposeSize(((IGenContext)map).Rand);

            if (caveSize.X != Draw.Width || caveSize.Y != Draw.Height)
            {
                DrawMapDefault(map);
                return;
            }

            //place room in a space that fits
            List<Loc> freeTiles = new List<Loc>();
            Loc caveStart = Draw.Start + new Loc(2, 1);
            for (int x = 0; x < caveSize.X - 4; x++)
            {
                for (int y = 0; y < caveSize.Y - 4; y++)
                {
                    map.SetTile(new Loc(caveStart.X + x, caveStart.Y + y), map.RoomTerrain.Copy());
                    freeTiles.Add(new Loc(caveStart.X + x, caveStart.Y + y));
                }
            }
            //place tile treasures
            List<EffectTile> tileTreasures = TileTreasures.GetSpawns(map);
            for (int ii = 0; ii < tileTreasures.Count; ii++)
            {
                int randIndex = ((IGenContext)map).Rand.Next(freeTiles.Count);
                EffectTile tile = new EffectTile(tileTreasures[ii]);
                map.PlaceItem(freeTiles[randIndex], tile);
                freeTiles.RemoveAt(randIndex);
            }
            //place item treasures
            List<MapItem> treasures = Treasures.GetSpawns(map);
            for (int ii = 0; ii < treasures.Count; ii++)
            {
                int randIndex = ((IGenContext)map).Rand.Next(freeTiles.Count);
                MapItem item = treasures[ii];
                map.PlaceItem(freeTiles[randIndex], item);
                freeTiles.RemoveAt(randIndex);
            }

            //open the passage to the treasure room
            Loc tunnel = new Loc(caveStart.X + (caveSize.X - 4) / 2, caveStart.Y + caveSize.Y - 4);
            map.SetTile(tunnel, map.RoomTerrain.Copy());

            //dig the room into the treasure room
            for (int xx = 0; xx < 3; xx++)
            {
                for (int yy = 0; yy < 2; yy++)
                    map.SetTile(new Loc(tunnel.X - 1 + xx, tunnel.Y + 1 + yy), map.RoomTerrain.Copy());
            }


            //place monsters and items
            MonsterTeam team = new MonsterTeam();
            Character newChar = GuardTypes.Pick(((IGenContext)map).Rand).Spawn(team, map);
            map.PlaceItems(new TeamSpawn(team, false), new Loc[1] { tunnel });


            //dig tunnels within this room to hook up to the incoming demands
            foreach (Dir4 dir in DirExt.VALID_DIR4)
            {
                if (dir == Dir4.Up)
                {//if approached from the top, it must be either left or right.  Dig to that side and then dig up.
                    List<IntRange> upReq = RoomSideReqs[dir];
                    bool left = false;
                    bool right = false;
                    for (int ii = 0; ii < upReq.Count; ii++)
                    {
                        bool hasLeft = upReq[ii].Contains(Draw.Start.X) && BorderToFulfill[Dir4.Up][0];
                        bool hasRight = upReq[ii].Contains(Draw.End.X - 1) && BorderToFulfill[Dir4.Up][Draw.Width - 1];
                        if (hasLeft && hasRight)
                        {
                            if (((IGenContext)map).Rand.Next(2) == 0)
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
                    {
                        DigAtBorder(map, Dir4.Left, Draw.End.Y - 1);
                        DigAtBorder(map, Dir4.Up, Draw.Start.X);
                    }
                    if (right)
                    {
                        DigAtBorder(map, Dir4.Right, Draw.End.Y - 1);
                        DigAtBorder(map, Dir4.Up, Draw.End.X - 1);
                    }
                }
                else if (dir == Dir4.Down)
                {//if approached from bottom, do nothing; we already fulfill every possibility
                }
                else
                {//if approached from the sides, dig at the bottommost Y of the room, because that was the only tile allowed.
                    if (RoomSideReqs[dir].Count > 0)
                        DigAtBorder(map, dir, Draw.End.Y - 1);
                }
            }

            for (int x = 0; x < Draw.Width; x++)
            {
                for (int y = 0; y < Draw.Height - 1; y++)
                {
                    Loc checkLoc = new Loc(Draw.X + x, Draw.Y + y);
                    if (!map.GetTile(checkLoc).TileEquivalent(map.RoomTerrain))
                        map.SetTile(checkLoc, map.UnbreakableTerrain.Copy());
                }
            }


            SetRoomBorders(map);
        }
        
    }
}
