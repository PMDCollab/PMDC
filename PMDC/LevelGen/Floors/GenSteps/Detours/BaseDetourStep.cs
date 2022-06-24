using System;
using RogueElements;
using RogueEssence.Dungeon;
using RogueEssence;
using RogueEssence.LevelGen;
using PMDC.Dungeon;
using System.Collections.Generic;

namespace PMDC.LevelGen
{
    [Serializable]
    public abstract class BaseDetourStep<T> : GenStep<T> where T : BaseMapGenContext
    {
        /// <summary>
        /// Treasures found in the detour room.
        /// </summary>
        public BulkSpawner<T, MapItem> Treasures;

        /// <summary>
        /// Tiles (such as exits or traps) found in the detour room.
        /// </summary>
        public BulkSpawner<T, EffectTile> TileTreasures;
        
        /// <summary>
        /// Enemies found in the detour room.
        /// </summary>
        public BulkSpawner<T, MobSpawn> GuardTypes;

        /// <summary>
        /// Length of the hall connecting the main path to the detour room.
        /// </summary>
        public RandRange HallLength;

        /// <summary>
        /// The possible types of room shapes.
        /// </summary>
        public SpawnList<RoomGen<T>> GenericRooms;

        public BaseDetourStep()
        {
            Treasures = new BulkSpawner<T, MapItem>();
            TileTreasures = new BulkSpawner<T, EffectTile>();
            GuardTypes = new BulkSpawner<T, MobSpawn>();
            GenericRooms = new SpawnList<RoomGen<T>>();
        }

        protected LocRay4? PlaceRoom(T map, List<LocRay4> rays, EffectTile sealingTile, List<Loc> freeTiles)
        {

            Grid.LocTest checkBlockForPlace = (Loc testLoc) =>
            {
                return !map.RoomTerrain.TileEquivalent(map.GetTile(testLoc)) && !map.UnbreakableTerrain.TileEquivalent(map.GetTile(testLoc));
            };
            //try X times to dig a passage
            for (int ii = 0; ii < 500 && rays.Count > 0; ii++)
            {
                int rayIndex = map.Rand.Next(rays.Count);
                LocRay4 ray = rays[rayIndex];
                rays.RemoveAt(rayIndex);

                Loc rayDirLoc = ray.Dir.GetLoc();
                Axis4 axis = ray.Dir.ToAxis();
                Axis4 orth = axis == Axis4.Horiz ? Axis4.Vert : Axis4.Horiz;

                int minLength = Math.Max(1, HallLength.Min);
                Rect hallBound = new Rect(ray.Loc + DirExt.AddAngles(ray.Dir, Dir4.Left).GetLoc(), new Loc(1));
                hallBound = Rect.IncludeLoc(hallBound, ray.Loc + rayDirLoc * (minLength - 1) + DirExt.AddAngles(ray.Dir, Dir4.Right).GetLoc());

                //make sure the MIN hall can tunnel unimpeded
                if (!CanPlaceRect(map, hallBound, checkBlockForPlace))
                    continue;

                for (int jj = 0; jj < 100; jj++)
                {
                    //plan the room
                    RoomGen<T> plan = GenericRooms.Pick(map.Rand).Copy();
                    Loc size = plan.ProposeSize(map.Rand);
                    plan.PrepareSize(map.Rand, size);
                    //attempt to place the bounds somewhere, anywhere, within the limitations that the room itself provides
                    List<int> candidateOpenings = new List<int>();
                    int planLength = plan.Draw.GetBorderLength(ray.Dir.Reverse());
                    for (int kk = 0; kk < planLength; kk++)
                    {
                        if (plan.GetFulfillableBorder(ray.Dir.Reverse(), kk))
                            candidateOpenings.Add(kk);
                    }

                    //as well as continue extending the hall until we hit a walkable.
                    int tunnelLen = Math.Max(1, HallLength.Pick(map.Rand));
                    Loc roomLoc = ray.Loc + rayDirLoc * tunnelLen;
                    int perpOffset = candidateOpenings[map.Rand.Next(candidateOpenings.Count)];
                    roomLoc += orth.CreateLoc(-perpOffset, 0);
                    if (rayDirLoc.GetScalar(axis) < 0)//move back the top-left of the entrance
                        roomLoc += rayDirLoc * (size.GetScalar(axis) - 1);

                    Rect roomTestBound = new Rect(roomLoc, size);
                    roomTestBound.Inflate(1, 1);
                    //make a rect for the rest of the hall
                    Rect hallExtBound = new Rect(ray.Loc + rayDirLoc * minLength + DirExt.AddAngles(ray.Dir, Dir4.Left).GetLoc(), new Loc(1));
                    hallExtBound = Rect.IncludeLoc(hallBound, ray.Loc + rayDirLoc * (tunnelLen - 1) + DirExt.AddAngles(ray.Dir, Dir4.Right).GetLoc());
                    //now that we've chosen our position, let's test it
                    if (!CanPlaceRect(map, roomTestBound, checkBlockForPlace) || !CanPlaceRect(map, hallExtBound, checkBlockForPlace)) // also test that the CHOSEN hallway can be properly sealed
                        continue; //invalid location, try another place
                    else
                    {


                        plan.SetLoc(roomLoc);

                        plan.AskBorderRange(new IntRange(perpOffset, perpOffset + 1) + roomLoc.GetScalar(orth), ray.Dir.Reverse());
                        //draw the room
                        plan.DrawOnMap(map);

                        //surround the room with bounds
                        for (int xx = roomTestBound.X; xx < roomTestBound.Right; xx++)
                        {
                            map.SetTile(new Loc(xx, roomTestBound.Y), map.UnbreakableTerrain.Copy());
                            map.SetTile(new Loc(xx, roomTestBound.End.Y - 1), map.UnbreakableTerrain.Copy());
                        }
                        for (int yy = roomTestBound.Y + 1; yy < roomTestBound.Bottom - 1; yy++)
                        {
                            map.SetTile(new Loc(roomTestBound.X, yy), map.UnbreakableTerrain.Copy());
                            map.SetTile(new Loc(roomTestBound.End.X - 1, yy), map.UnbreakableTerrain.Copy());
                        }

                        //spawn tiles, items, foes
                        List<Loc> addedTiles = ((IPlaceableGenContext<MapItem>)map).GetFreeTiles(plan.Draw);
                        freeTiles.AddRange(addedTiles);


                        //tunnel to the room
                        Loc loc = ray.Loc;
                        for (int tt = 0; tt < tunnelLen; tt++)
                        {
                            //make walkable
                            map.SetTile(loc, map.RoomTerrain.Copy());

                            //make left side unbreakable
                            Loc lLoc = loc + DirExt.AddAngles(ray.Dir, Dir4.Left).GetLoc();
                            map.SetTile(lLoc, map.UnbreakableTerrain.Copy());

                            //make right side unbreakable
                            Loc rLoc = loc + DirExt.AddAngles(ray.Dir, Dir4.Right).GetLoc();
                            map.SetTile(rLoc, map.UnbreakableTerrain.Copy());

                            loc += rayDirLoc;
                        }

                        //finally, seal with a locked door
                        map.SetTile(ray.Loc, map.UnbreakableTerrain.Copy());
                        EffectTile newEffect = new EffectTile(sealingTile, ray.Loc);
                        ((IPlaceableGenContext<EffectTile>)map).PlaceItem(ray.Loc, newEffect);

                        return ray;
                    }
                }
            }

            //DiagManager.Instance.LogInfo("Couldn't place sealed detour!");

            return null;
        }


        protected void PlaceEntities(T map, List<Loc> freeTiles)
        {
            List<EffectTile> tileTreasures = TileTreasures.GetSpawns(map);
            for (int kk = 0; kk < tileTreasures.Count && freeTiles.Count > 0; kk++)
            {
                int randIndex = map.Rand.Next(freeTiles.Count);

                EffectTile spawnedTrap = tileTreasures[kk];
                ((IPlaceableGenContext<EffectTile>)map).PlaceItem(freeTiles[randIndex], spawnedTrap);
                freeTiles.RemoveAt(randIndex);
            }

            List<MapItem> treasures = Treasures.GetSpawns(map);
            for (int kk = 0; kk < treasures.Count && freeTiles.Count > 0; kk++)
            {
                int randIndex = map.Rand.Next(freeTiles.Count);

                MapItem item = new MapItem(treasures[kk]);
                ((IPlaceableGenContext<MapItem>)map).PlaceItem(freeTiles[randIndex], item);

                freeTiles.RemoveAt(randIndex);
            }

            List<MobSpawn> guardTypes = GuardTypes.GetSpawns(map);
            for (int kk = 0; kk < guardTypes.Count && freeTiles.Count > 0; kk++)
            {
                int randIndex = map.Rand.Next(freeTiles.Count);

                MonsterTeam team = new MonsterTeam();
                Character newChar = guardTypes[kk].Spawn(team, map);
                newChar.CharLoc = freeTiles[randIndex];
                map.MapTeams.Add(newChar.MemberTeam);

                freeTiles.RemoveAt(randIndex);
            }
        }

        private bool CanPlaceRect(T map, Rect rect, Grid.LocTest checkBlock)
        {
            for (int ii = rect.Left; ii < rect.Right; ii++)
            {
                for (int jj = rect.Top; jj < rect.Bottom; jj++)
                {
                    Loc testLoc = new Loc(ii, jj);
                    if (!checkBlock(testLoc))
                        return false;
                }
            }
            return true;
        }
    }
}
