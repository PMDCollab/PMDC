using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence.Dungeon;
using RogueEssence;
using RogueEssence.LevelGen;
using PMDC.Dungeon;

namespace PMDC.LevelGen
{
    [Serializable]
    public class ShopStep<T> : GenStep<T> where T : ListMapGenContext
    {
        const int MIN_SHOP_SIZE = 3;

        public int SecurityStatus;
        public SpawnList<MapItem> Items { get; set; }
        public SpawnList<ItemTheme> ItemThemes { get; set; }
        public SpawnList<MobSpawn> Mobs { get; set; }
        public MobSpawn StartMob { get; set; }
        public List<BaseRoomFilter> Filters { get; set; }

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
                if (Collision.InBounds(testPlan.RoomGen.Draw, entranceMap.GetLoc(0)))
                    continue;
                IViewPlaceableGenContext<MapGenExit> exitMap = map;
                if (Collision.InBounds(testPlan.RoomGen.Draw, exitMap.GetLoc(0)))
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
                        if (map.Tiles[testLoc.X][testLoc.Y].TileEquivalent(map.RoomTerrain) && !map.HasTileEffect(testLoc) &&
                            map.Tiles[testLoc.X][testLoc.Y + 1].TileEquivalent(map.RoomTerrain) && !map.HasTileEffect(new Loc(testLoc.X, testLoc.Y + 1)) &&
                            !map.PostProcGrid[testLoc.X][testLoc.Y].Status[(int)PostProcType.Panel] &&
                            !map.PostProcGrid[testLoc.X][testLoc.Y].Status[(int)PostProcType.Item])
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
                    EffectTile effect = new EffectTile(45, true, matLoc);
                    ((IPlaceableGenContext<EffectTile>)map).PlaceItem(matLoc, effect);
                    map.PostProcGrid[matLoc.X][matLoc.Y].Status[(int)PostProcType.Panel] = true;
                    map.PostProcGrid[matLoc.X][matLoc.Y].Status[(int)PostProcType.Item] = true;
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
                newTeam.SetRank(0);
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
