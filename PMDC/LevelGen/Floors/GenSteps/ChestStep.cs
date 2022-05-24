using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence.Dungeon;
using PMDC.Dungeon;
using RogueEssence;
using RogueEssence.LevelGen;

namespace PMDC.LevelGen
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
                FloorRoomPlan testPlan = map.RoomPlan.GetRoomPlan(ii);
                //if (map.RoomPlan.IsChokePoint(new RoomHallIndex(ii, false)))
                //    continue;
                if (!BaseRoomFilter.PassesAllFilters(testPlan, this.Filters))
                    continue;

                //also do not choose a room that contains the end stairs
                IViewPlaceableGenContext<MapGenExit> exitMap = (IViewPlaceableGenContext<MapGenExit>)map;
                if (Collision.InBounds(testPlan.RoomGen.Draw, exitMap.GetLoc(0)))
                    continue;

                possibleRooms.Add(ii);
            }

            if (possibleRooms.Count == 0)
                return;

            List<Loc> freeTiles = new List<Loc>();
            IRoomGen room = null;

            while (possibleRooms.Count > 0)
            {
                int chosenRoom = map.Rand.Next(possibleRooms.Count);
                room = map.RoomPlan.GetRoom(possibleRooms[chosenRoom]);

                //get all places that the chest is eligible
                freeTiles = Grid.FindTilesInBox(room.Draw.Start, room.Draw.Size, (Loc testLoc) =>
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
                if (freeTiles.Count > 0)
                    break;
                possibleRooms.RemoveAt(chosenRoom);
            }

            //can't find any free tile in any room, return
            if (freeTiles.Count == 0)
                return;

            if (!ItemThemes.CanPick)
                return;
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
            spawnedChest.TileStates.Set(new UnlockState(455));

            if (Ambush && MobThemes.CanPick)
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
            map.PostProcGrid[loc.X][loc.Y].Status[(int)PostProcType.Panel] = true;
            map.PostProcGrid[loc.X][loc.Y].Status[(int)PostProcType.Item] = true;

            GenContextDebug.DebugProgress("Placed Chest");
        }

        public override string ToString()
        {
            return "Chest Step" + (Ambush ? " Ambush" : "");
        }
    }
}
