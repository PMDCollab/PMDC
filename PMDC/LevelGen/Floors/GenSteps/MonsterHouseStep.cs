using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence.Dungeon;
using RogueEssence;
using RogueEssence.LevelGen;
using PMDC.Dungeon;
using RogueEssence.Data;

namespace PMDC.LevelGen
{
    /// <summary>
    /// A standard monster house that appears as a room filled with treasure.
    /// When an explorer enters the premises, the monsters appear.
    /// This step chooses an existing room to put the house in.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class MonsterHouseStep<T> : MonsterHouseBaseStep<T> where T : ListMapGenContext
    {

        public MonsterHouseStep() : base() { Filters = new List<BaseRoomFilter>(); }
        public MonsterHouseStep(List<BaseRoomFilter> filters) : base() { Filters = filters; }
        public MonsterHouseStep(MonsterHouseStep<T> other) : base(other)
        {
            Filters = new List<BaseRoomFilter>();
            Filters.AddRange(other.Filters);
        }
        public override MonsterHouseBaseStep<T> CreateNew() { return new MonsterHouseStep<T>(this); }

        /// <summary>
        /// Used to filter out unwanted rooms to be used for this monster house.
        /// </summary>
        public List<BaseRoomFilter> Filters { get; set; }

        public override void Apply(T map)
        {
            if (!ItemThemes.CanPick)
                return;

            if (!MobThemes.CanPick)
                return;


            //choose a room to cram all the items in
            List<int> possibleRooms = new List<int>();
            for(int ii = 0; ii < map.RoomPlan.RoomCount; ii++)
            {
                //Monster houses will never spawn in the starting room if no monster house entrances is set
                MonsterHouseTableState mhtable = DataManager.Instance.UniversalEvent.UniversalStates.GetWithDefault<MonsterHouseTableState>();

                if (mhtable != null && mhtable.NoMonsterHouseEntrances)
                {
                    Rect curRoom = map.RoomPlan.GetRoom(ii).Draw;
                    bool skipRoom = false;
                    foreach (MapGenEntrance entrance in map.GenEntrances)
                    {
                        if (curRoom.Contains(entrance.Loc))
                        {
                            skipRoom = true;
                            break;
                        }
                    }

                    if (skipRoom)
                        continue;
                }
                
                if (!BaseRoomFilter.PassesAllFilters(map.RoomPlan.GetRoomPlan(ii), this.Filters))
                    continue;
                possibleRooms.Add(ii);
            }

            if (possibleRooms.Count == 0)
                return;

            IRoomGen room = map.RoomPlan.GetRoom(possibleRooms[map.Rand.Next(possibleRooms.Count)]);

            //determine the number of free tiles to put items on; trim the maximum item spawn accordingly (maximum <= 1/2 of free tiles)
            //determine the number of free tiles to put mobs on; trim the maximum mob spawn accordingly (maximum <= 1/2 of free tiles)
            List<Loc> itemTiles = new List<Loc>();
            int mobSpace = 0;
            for (int x = room.Draw.X; x < room.Draw.X + room.Draw.Size.X; x++)
            {
                for (int y = room.Draw.Y; y < room.Draw.Y + room.Draw.Size.Y; y++)
                {
                    Loc testLoc = new Loc(x, y);
                    if (!map.TileBlocked(testLoc))
                    {
                        if (!map.HasTileEffect(new Loc(x, y)) && (map.GetPostProc(testLoc).Status & (PostProcType.Panel | PostProcType.Item)) == PostProcType.None)
                        {
                            bool hasItem = false;
                            foreach (MapItem item in map.Items)
                            {
                                if (item.TileLoc == testLoc)
                                {
                                    hasItem = true;
                                    break;
                                }
                            }
                            if (!hasItem)
                                itemTiles.Add(testLoc);
                        }
                        bool hasMob = false;
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
                        if (!hasMob)
                            mobSpace++;
                    }
                }
            }

            //choose which item theme to work with
            ItemTheme chosenItemTheme = ItemThemes.Pick(map.Rand);

            //the item spawn list in this class dictates the items available for spawning
            //it will be queried for items that match the theme selected
            List<MapItem> chosenItems = chosenItemTheme.GenerateItems(map, Items);
            
            //place the items
            for (int ii = 0; ii < chosenItems.Count; ii++)
            {
                if (itemTiles.Count > 0)
                {
                    MapItem item = new MapItem(chosenItems[ii]);
                    int randIndex = map.Rand.Next(itemTiles.Count);
                    ((IPlaceableGenContext<MapItem>)map).PlaceItem(itemTiles[randIndex], item);
                    itemTiles.RemoveAt(randIndex);
                }
            }



            //the mob theme will be selected randomly
            MobTheme chosenMobTheme = MobThemes.Pick(map.Rand);

            //the mobs in this class are the ones that would be available when the game wants to spawn things outside of the floor's spawn list
            //it will be queried for monsters that match the theme provided
            List<MobSpawn> chosenMobs = chosenMobTheme.GenerateMobs(map, Mobs);

            //cover the room in a check that holds all of the monsters, and covers the room's bounds
            CheckIntrudeBoundsEvent check = new CheckIntrudeBoundsEvent();
            check.Bounds = room.Draw;
            {
                MonsterHouseMapEvent house = new MonsterHouseMapEvent();
                house.Bounds = room.Draw;
                
                MonsterHouseTableState mhtable = DataManager.Instance.UniversalEvent.UniversalStates.GetWithDefault<MonsterHouseTableState>();
                if (mhtable != null && mhtable.MonsterHouseWarningTile != null)
                {
                    //Make monster houses visible if set
                    for (int xx = house.Bounds.X; xx < house.Bounds.X + house.Bounds.Size.X; xx++)
                    {
                        for (int yy = house.Bounds.Y; yy < house.Bounds.Y + house.Bounds.Size.Y; yy++)
                        {
                            Loc loc = new Loc(xx, yy);
                            Tile tile = map.Tiles[xx][yy];
                            TerrainData data = (TerrainData)tile.Data.GetData();
                            if (data.BlockType == TerrainData.Mobility.Passable)
                                ((IPlaceableGenContext<EffectTile>)map).PlaceItem(loc,  new EffectTile(mhtable.MonsterHouseWarningTile, true));
                        }
                    }
                }
                
                foreach (MobSpawn mob in chosenMobs)
                {
                    MobSpawn copyMob = mob.Copy();
                    if (map.Rand.Next(ALT_COLOR_ODDS) == 0)
                    {
                        SkinTableState table = DataManager.Instance.UniversalEvent.UniversalStates.GetWithDefault<SkinTableState>();
                        copyMob.BaseForm.Skin = table.AltColor;
                    }
                    house.Mobs.Add(copyMob);
                }
                check.Effects.Add(house);
            }

            AddIntrudeStep(map, check);
        }
    }

}
