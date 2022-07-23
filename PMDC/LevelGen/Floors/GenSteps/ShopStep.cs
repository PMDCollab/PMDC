using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence.Dungeon;
using RogueEssence;
using RogueEssence.LevelGen;
using PMDC.Dungeon;

namespace PMDC.LevelGen
{
    /// <summary>
    /// Spawns a shop somewhere in the map.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class ShopStep<T> : GenStep<T> where T : ListMapGenContext
    {
        const int MIN_SHOP_SIZE = 3;

        /// <summary>
        /// The map status used to check for thievery.
        /// </summary>
        public int SecurityStatus;

        /// <summary>
        /// The items that can be sold in the shop.
        /// This is filtered by Item Themes when generating.
        /// </summary>
        public SpawnList<MapItem> Items { get; set; }

        /// <summary>
        /// Every shop chooses a theme to populate its catalog with.
        /// This variable determines the possible themes to filter the items with.
        /// </summary>
        public SpawnList<ItemTheme> ItemThemes { get; set; }

        /// <summary>
        /// The mobs that will be spawned if the player is caught stealing.
        /// </summary>
        public SpawnList<MobSpawn> Mobs { get; set; }

        /// <summary>
        /// The mob that will run the shop.
        /// </summary>
        public MobSpawn StartMob { get; set; }

        /// <summary>
        /// Narrows down the rooms in the map that the shop can spawn in.  No boss rooms, etc.
        /// </summary>
        public List<BaseRoomFilter> Filters { get; set; }

        /// <summary>
        /// The personality of the shopkeeper.
        /// </summary>
        public int Personality;

        public ShopStep() : base()
        {
            Filters = new List<BaseRoomFilter>();
            Items = new SpawnList<MapItem>();
            ItemThemes = new SpawnList<ItemTheme>();
            Mobs = new SpawnList<MobSpawn>();
        }
        public ShopStep(List<BaseRoomFilter> filters) : this()
        {
            Filters = filters;
        }

        public override void Apply(T map)
        {
            //choose a room to cram all the items in
            List<int> possibleRooms = new List<int>();
            for(int ii = 0; ii < map.RoomPlan.RoomCount; ii++)
            {
                FloorRoomPlan testPlan = map.RoomPlan.GetRoomPlan(ii);
                if (!BaseRoomFilter.PassesAllFilters(testPlan, this.Filters))
                    continue;

                //also do not choose a room that contains the start or end
                IViewPlaceableGenContext<MapGenEntrance> entranceMap = map;
                if (map.RoomPlan.InBounds(testPlan.RoomGen.Draw, entranceMap.GetLoc(0)))
                    continue;
                IViewPlaceableGenContext<MapGenExit> exitMap = map;
                if (map.RoomPlan.InBounds(testPlan.RoomGen.Draw, exitMap.GetLoc(0)))
                    continue;

                possibleRooms.Add(ii);
            }

            FloorRoomPlan roomPlan = null;
            Rect limitRect = Rect.Empty;

            while (possibleRooms.Count > 0)
            {
                int chosenRoom = map.Rand.Next(possibleRooms.Count);
                roomPlan = map.RoomPlan.GetRoomPlan(possibleRooms[chosenRoom]);

                bool[][] eligibilityGrid = new bool[roomPlan.RoomGen.Draw.Width][];
                for (int xx = 0; xx < roomPlan.RoomGen.Draw.Width; xx++)
                {
                    eligibilityGrid[xx] = new bool[roomPlan.RoomGen.Draw.Height];
                    for (int yy = 0; yy < roomPlan.RoomGen.Draw.Height; yy++)
                    {
                        bool eligible = true;
                        Loc testLoc = roomPlan.RoomGen.Draw.Start + new Loc(xx, yy);
                        Loc frontLoc = testLoc + Dir8.Down.GetLoc();
                        if (map.RoomTerrain.TileEquivalent(map.GetTile(testLoc)) && !map.HasTileEffect(testLoc) &&
                            map.RoomTerrain.TileEquivalent(map.GetTile(frontLoc)) && !map.HasTileEffect(frontLoc) &&
                             (map.GetPostProc(testLoc).Status & (PostProcType.Panel | PostProcType.Item)) == PostProcType.None)
                        {
                            foreach (MapItem item in map.Items)
                            {
                                if (item.TileLoc == testLoc)
                                {
                                    eligible = false;
                                    break;
                                }
                            }
                        }
                        else
                            eligible = false;
                        eligibilityGrid[xx][yy] = eligible;
                    }
                }

                List <Rect> candRects = Detection.DetectNLargestRects(eligibilityGrid, 1);
                if (candRects.Count > 0)
                {
                    limitRect = new Rect(roomPlan.RoomGen.Draw.Start + candRects[0].Start, candRects[0].Size);
                    if (limitRect.Size.X >= MIN_SHOP_SIZE && limitRect.Size.Y >= MIN_SHOP_SIZE)
                        break;
                }
                possibleRooms.RemoveAt(chosenRoom);
            }

            if (limitRect.Size.X < MIN_SHOP_SIZE || limitRect.Size.Y < MIN_SHOP_SIZE)
                return;

            //randomly roll an actual rectangle within the saferect bounds: between 3x3 and the limit
            Loc rectSize = new Loc(map.Rand.Next(MIN_SHOP_SIZE, limitRect.Width + 1), map.Rand.Next(MIN_SHOP_SIZE, limitRect.Height + 1));
            Loc rectStart = new Loc(limitRect.X + map.Rand.Next(limitRect.Width - rectSize.X + 1), limitRect.Y + map.Rand.Next(limitRect.Height - rectSize.Y + 1));
            Rect safeRect = new Rect(rectStart, rectSize);

            // place the mat of the shop
            List<Loc> itemTiles = new List<Loc>();
            for (int xx = safeRect.X; xx < safeRect.End.X; xx++)
            {
                for (int yy = safeRect.Y; yy < safeRect.End.Y; yy++)
                {
                    Loc matLoc = new Loc(xx, yy);
                    itemTiles.Add(matLoc);
                    EffectTile effect = new EffectTile("area_shop", true, matLoc);
                    ((IPlaceableGenContext<EffectTile>)map).PlaceItem(matLoc, effect);
                    map.GetPostProc(matLoc).Status |= PostProcType.Panel;
                    map.GetPostProc(matLoc).Status |= PostProcType.Item;
                }
            }

            // place the map status for checking shop items and spawning security guards
            {

                MapStatus status = new MapStatus(SecurityStatus);
                status.LoadFromData();
                ShopSecurityState securityState = new ShopSecurityState();
                for (int ii = 0; ii < Mobs.Count; ii++)
                    securityState.Security.Add(Mobs.GetSpawn(ii).Copy(), Mobs.GetSpawnRate(ii));
                status.StatusStates.Set(securityState);
                status.StatusStates.Set(new MapIndexState(Personality));
                map.Map.Status.Add(SecurityStatus, status);
            }

            // place the mob running the shop
            {
                ExplorerTeam newTeam = new ExplorerTeam();
                newTeam.SetRank("normal");
                Character shopkeeper = StartMob.Spawn(newTeam, map);
                Loc randLoc = itemTiles[map.Rand.Next(itemTiles.Count)];
                ((IGroupPlaceableGenContext<TeamSpawn>)map).PlaceItems(new TeamSpawn(newTeam, true), new Loc[] { randLoc });
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

            //prevent the room from being chosen for anything else
            roomPlan.Components.Set(new NoEventRoom());
        }
    }

}
