using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence.Dungeon;
using PMDO.Dungeon;
using RogueEssence;
using RogueEssence.LevelGen;

namespace PMDO.LevelGen
{
    [Serializable]
    public class ChestStep<T> : MonsterHouseBaseStep<T> where T : ListMapGenContext
    {
        public bool Ambush;
        public ChestStep() : base()
        {
            Filters = new List<BaseRoomFilter>();
        }
        public ChestStep(bool ambush, List<BaseRoomFilter> filters)
        {
            Ambush = ambush;
            Filters = filters;
        }
        public ChestStep(ChestStep<T> other) : base(other)
        {
            Ambush = other.Ambush;
            Filters = new List<BaseRoomFilter>();
            Filters.AddRange(other.Filters);
        }
        public override MonsterHouseBaseStep<T> CreateNew() { return new ChestStep<T>(this); }
        public List<BaseRoomFilter> Filters { get; set; }

        public override void Apply(T map)
        {
            
            Grid.LocTest checkBlock = (Loc testLoc) =>
            {
                return (!map.Tiles[testLoc.X][testLoc.Y].TileEquivalent(map.RoomTerrain) || map.HasTileEffect(testLoc));
            };

            //choose a room to put the chest in
            //do not choose a room that would cause disconnection of the floor
            List<int> possibleRooms = new List<int>();
            for (int ii = 0; ii < map.RoomPlan.RoomCount; ii++)
            {
                if (!BaseRoomFilter.PassesAllFilters(map.RoomPlan.GetRoomPlan(ii), this.Filters))
                    continue;
                //if (map.RoomPlan.IsChokePoint(new RoomHallIndex(ii, false)))
                //    continue;
                IRoomGen testRoom = map.RoomPlan.GetRoom(ii);
                bool hasInterferingTile = false;
                bool hasOpenTile = false;
                for (int x = testRoom.Draw.X; x < testRoom.Draw.X + testRoom.Draw.Size.X; x++)
                {
                    for (int y = testRoom.Draw.Y; y < testRoom.Draw.Y + testRoom.Draw.Size.Y; y++)
                    {
                        //also do not choose a room that contains the end stairs (search for the non-secret stairs)
                        if (((Tile)map.Tiles[x][y]).Effect.ID == 1 || ((Tile)map.Tiles[x][y]).Effect.ID == 2)
                        {
                            hasInterferingTile = true;
                            break;
                        }
                        Loc testLoc = new Loc(x, y);
                        if (!hasOpenTile && !map.TileBlocked(testLoc) && !map.TileBlocked(new Loc(x, y + 1)))
                        {
                            if (Grid.GetForkDirs(testLoc, checkBlock, checkBlock).Count < 2)
                            {
                                if (!map.HasTileEffect(testLoc) && !map.HasTileEffect(new Loc(x, y+1)) &&
                                    !map.PostProcGrid[x][y].Status[(int)PostProcType.Panel] && !map.PostProcGrid[x][y].Status[(int)PostProcType.Item])
                                {
                                    bool hasItem = false;
                                    bool hasMob = false;
                                    foreach (MapItem item in map.Items)
                                    {
                                        if (item.TileLoc == testLoc)
                                        {
                                            hasItem = true;
                                            break;
                                        }
                                    }
                                    foreach (Team team in map.AllyTeams)
                                    {
                                        foreach (Character testChar in team.EnumerateChars())
                                        {
                                            if (testChar.CharLoc == testLoc)
                                            {
                                                hasMob = true;
                                                break;
                                            }
                                        }
                                    }
                                    foreach (Team team in map.MapTeams)
                                    {
                                        foreach (Character testChar in team.EnumerateChars())
                                        {
                                            if (testChar.CharLoc == testLoc)
                                            {
                                                hasMob = true;
                                                break;
                                            }
                                        }
                                    }
                                    if (!hasItem && !hasMob)
                                        hasOpenTile = true;
                                }
                            }
                        }
                    }
                    if (hasInterferingTile)
                        break;
                }
                if (hasInterferingTile || !hasOpenTile)
                    continue;
                possibleRooms.Add(ii);
            }

            if (possibleRooms.Count == 0)
                return;

            IRoomGen room = map.RoomPlan.GetRoom(possibleRooms[map.Rand.Next(possibleRooms.Count)]);
                        
            //get all places that the chest is eligible
            List<Loc> freeTiles = Grid.FindTilesInBox(room.Draw.Start, room.Draw.Size, (Loc testLoc) =>
            {
                if (map.Tiles[testLoc.X][testLoc.Y].TileEquivalent(map.RoomTerrain) && !map.HasTileEffect(testLoc) &&
                    map.Tiles[testLoc.X][testLoc.Y + 1].TileEquivalent(map.RoomTerrain) && !map.HasTileEffect(new Loc(testLoc.X, testLoc.Y + 1)) &&
                    !map.PostProcGrid[testLoc.X][testLoc.Y].Status[(int)PostProcType.Panel] &&
                    !map.PostProcGrid[testLoc.X][testLoc.Y].Status[(int)PostProcType.Item])
                {
                    if (Grid.GetForkDirs(testLoc, checkBlock, checkBlock).Count < 2)
                    {
                        foreach (MapItem item in map.Items)
                        {
                            if (item.TileLoc == testLoc)
                                return false;
                        }
                        foreach (Team team in map.AllyTeams)
                        {
                            foreach (Character testChar in team.EnumerateChars())
                            {
                                if (testChar.CharLoc == testLoc)
                                    return false;
                            }
                        }
                        foreach (Team team in map.MapTeams)
                        {
                            foreach (Character testChar in team.EnumerateChars())
                            {
                                if (testChar.CharLoc == testLoc)
                                    return false;
                            }
                        }
                        return true;
                    }
                }
                return false;
            });
            freeTiles.AddRange(freeTiles);


            //choose which item theme to work with
            ItemTheme chosenItemTheme = ItemThemes.Pick(map.Rand);

            //the item spawn list in this class dictates the items available for spawning
            //it will be queried for items that match the theme selected
            List<MapItem> chosenItems = chosenItemTheme.GenerateItems(map, Items);

            if (chosenItems.Count == 0)
                return;

            int randIndex = map.Rand.Next(freeTiles.Count);
            Loc loc = freeTiles[randIndex];


            EffectTile spawnedChest = new EffectTile(37, true);

            if (Ambush)
            {
                spawnedChest.Danger = true;
                //the mob theme will be selected randomly
                MobTheme chosenMobTheme = MobThemes.Pick(map.Rand);

                //the mobs in this class are the ones that would be available when the game wants to spawn things outside of the floor's spawn list
                //it will be queried for monsters that match the theme provided
                List<MobSpawn> chosenMobs = chosenMobTheme.GenerateMobs(map, Mobs);


                MobSpawnState mobSpawn = new MobSpawnState();
                foreach (MobSpawn mob in chosenMobs)
                {
                    MobSpawn copyMob = mob.Copy();
                    if (map.Rand.Next(ALT_COLOR_ODDS) == 0)
                        copyMob.BaseForm.Skin = 1;
                    mobSpawn.Spawns.Add(copyMob);
                }
                spawnedChest.TileStates.Set(mobSpawn);
            }
            
            ItemSpawnState itemSpawn = new ItemSpawnState();
            itemSpawn.Spawns = chosenItems;
            spawnedChest.TileStates.Set(itemSpawn);

            Rect wallBounds = new Rect(room.Draw.X - 1, room.Draw.Y - 1, room.Draw.Size.X + 2, room.Draw.Size.Y + 2);
            spawnedChest.TileStates.Set(new BoundsState(wallBounds));

            ((IPlaceableGenContext<EffectTile>)map).PlaceItem(loc, spawnedChest);

            GenContextDebug.DebugProgress("Placed Chest");
        }

        public override string ToString()
        {
            return "Chest Step" + (Ambush ? " Ambush" : "");
        }
    }
}
