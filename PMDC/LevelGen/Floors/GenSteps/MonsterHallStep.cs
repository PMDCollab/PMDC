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
    /// A monster house that occurs in hallways.
    /// The room will gradually crumble away to reveal all monsters and items.
    /// This step chooses an existing room (hallways are rooms) to put the house in.  The room must have a one-tile chokepoint to be selected.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class MonsterHallStep<T> : MonsterHouseBaseStep<T> where T : ListMapGenContext
    {
        public MonsterHallStep() : base() { Filters = new List<BaseRoomFilter>(); }
        public MonsterHallStep(Loc size, List<BaseRoomFilter> filters) : base() { Size = size; Filters = filters; }
        public MonsterHallStep(MonsterHallStep<T> other) : base(other)
        {
            Filters = new List<BaseRoomFilter>();
            Filters.AddRange(other.Filters);
        }
        public override MonsterHouseBaseStep<T> CreateNew() { return new MonsterHallStep<T>(this); }


        /// <summary>
        /// Used to filter out unwanted rooms to be used for this monster house.
        /// </summary>
        public List<BaseRoomFilter> Filters { get; set; }

        /// <summary>
        /// The final size of the room after the crumbling finishes.
        /// </summary>
        public Loc Size { get; set; }

        private Rect clampToBounds(Rect input, Rect bounds)
        {
            Rect output = input;
            //correct monsterhouse bounds within the room bounds
            if (output.Bottom > bounds.Bottom - 1)
                output.Start = new Loc(output.Start.X, output.Start.Y - (output.Bottom - (bounds.Bottom - 1)));
            if (output.Right > bounds.Right - 1)
                output.Start = new Loc(output.Start.X - (output.Right - (bounds.Right - 1)), output.Start.Y);

            if (output.Top < bounds.Top + 1)
                output.Start = new Loc(output.Start.X, bounds.Top + 1);
            if (output.Left < bounds.Left + 1)
                output.Start = new Loc(bounds.Left + 1, output.Start.Y);

            output.Size = new Loc(Math.Min(output.Width, bounds.Width - 2), Math.Min(output.Height, bounds.Height - 2));
            return output;
        }

        private void TryAddPossibleRoom(T map, List<(Loc, List<Rect>, int[])> possibleRooms, IRoomGen cand, Loc originPoint)
        {
            //verify it is not blocked
            if (map.TileBlocked(originPoint))
                return;

            //verify that it is a chokepoint
            bool chokePoint = Grid.IsChokePoint(originPoint - Loc.One, Loc.One * 3, originPoint,
                map.TileBlocked, (Loc testLoc) => { return true; });

            if (!chokePoint)
                return;
            
            //Monster halls will never spawn right where you start if no monster house entrances is set
            MonsterHouseTableState mhtable = DataManager.Instance.UniversalEvent.UniversalStates.GetWithDefault<MonsterHouseTableState>();

            if (mhtable != null && mhtable.NoMonsterHouseEntrances)
            {
                bool skipRoom = false;
                foreach (MapGenEntrance entrance in map.GenEntrances)
                {
                    if (originPoint.Equals(entrance.Loc))
                    {
                        skipRoom = true;
                        break;
                    }
                }

                if (skipRoom)
                    return;
            }
            
            //Prevents monster halls on tiles such as foliage that block light
            if (mhtable != null && mhtable.NoMonsterHallOnBlockLightTiles)
            {
                Tile originTile = map.Map.GetTile(originPoint);
                if (originTile != null && originTile.Data.GetData() != null && ((TerrainData)originTile.Data.GetData()).BlockLight)
                {
                    return;
                }
            }

            int stages = 4;
            Loc size = new Loc(Math.Max(Size.X, 1 + stages * 2), Math.Max(Size.Y, 1 + stages * 2));
            Rect potentialBounds = new Rect(originPoint - new Loc(size.X / 2, size.Y / 2), size);

            ////correct monsterhouse bounds within the room bounds
            //clampToBounds(potentialBounds, cand.Draw);

            potentialBounds = clampToBounds(potentialBounds, new Rect(0, 0, map.Width, map.Height));

            //check to see if every phase of the crumble will sufficiently remove enough walls
            //also check against unbreakable tiles; if we find any, the room is invalid
            List<Rect> phases = new List<Rect>();
            phases.Add(new Rect(originPoint, new Loc(1)));
            phases.Add(potentialBounds);

            //middle
            {
                Rect phaseFrom = phases[0];
                Rect phaseTo = phases[1];
                Rect midpoint = Rect.FromPoints(new Loc((phaseFrom.Start.X + phaseTo.Start.X) / 2, (phaseFrom.Start.Y + phaseTo.Start.Y) / 2),
                    new Loc((phaseFrom.End.X + phaseTo.End.X) / 2, (phaseFrom.End.Y + phaseTo.End.Y) / 2));
                phases.Insert(1, midpoint);
            }
            //early middle
            {
                Rect phaseFrom = phases[0];
                Rect phaseTo = phases[1];
                Rect midpoint = Rect.FromPoints(new Loc((phaseFrom.Start.X + phaseTo.Start.X) / 2, (phaseFrom.Start.Y + phaseTo.Start.Y) / 2),
                    new Loc((phaseFrom.End.X + phaseTo.End.X) / 2, (phaseFrom.End.Y + phaseTo.End.Y) / 2));
                phases.Insert(1, midpoint);
            }
            //late middle
            {
                Rect phaseFrom = phases[2];
                Rect phaseTo = phases[3];
                Rect midpoint = Rect.FromPoints(new Loc((phaseFrom.Start.X + phaseTo.Start.X) / 2, (phaseFrom.Start.Y + phaseTo.Start.Y) / 2),
                    new Loc((phaseFrom.End.X + phaseTo.End.X) / 2, (phaseFrom.End.Y + phaseTo.End.Y) / 2));
                phases.Insert(3, midpoint);
            }
            phases.RemoveAt(0);

            int[] blockedTiles = new int[phases.Count];
            bool containsImpassable = false;
            for (int yy = potentialBounds.Start.Y; yy < potentialBounds.End.Y; yy++)
            {
                for (int xx = potentialBounds.Start.X; xx < potentialBounds.End.X; xx++)
                {
                    Loc loc = new Loc(xx, yy);
                    if (map.UnbreakableTerrain.TileEquivalent(map.GetTile(loc)))
                    {
                        containsImpassable = true;
                        break;
                    }
                    else if (map.WallTerrain.TileEquivalent(map.GetTile(loc)))
                    {
                        for (int nn = 0; nn < phases.Count; nn++)
                        {
                            if (map.RoomPlan.InBounds(phases[nn], new Loc(xx, yy)))
                            {
                                blockedTiles[nn]++;
                                break;
                            }
                        }
                    }
                }
                if (containsImpassable)
                    break;
            }
            if (containsImpassable)
                return;

            bool lackingTiles = false;
            int prevTiles = 1;
            for (int nn = 0; nn < phases.Count; nn++)
            {
                int totalTiles = phases[nn].Area - prevTiles;

                if (blockedTiles[nn] < totalTiles / 2)
                {
                    lackingTiles = true;
                    break;
                }

                prevTiles = phases[nn].Area;
            }

            if (lackingTiles)
                return;

            possibleRooms.Add((originPoint, phases, blockedTiles));
        }


        public override void Apply(T map)
        {
            if (!ItemThemes.CanPick)
                return;

            if (!MobThemes.CanPick)
                return;

            //choose a room to cram all the items in
            List<(Loc center, List<Rect> phases, int[] blockedTiles)> possibleRooms = new List<(Loc, List<Rect>, int[])>();
            for (int ii = 0; ii < map.RoomPlan.HallCount; ii++)
            {
                IRoomGen cand = map.RoomPlan.GetHall(ii);
                if (!BaseRoomFilter.PassesAllFilters(map.RoomPlan.GetHallPlan(ii), this.Filters))
                    continue;

                for (int xx = cand.Draw.Start.X; xx < cand.Draw.End.X; xx++)
                {
                    for (int yy = cand.Draw.Start.Y; yy < cand.Draw.End.Y; yy++)
                        TryAddPossibleRoom(map, possibleRooms, cand, new Loc(xx, yy));
                }
            }

            if (possibleRooms.Count == 0)
                return;

            int chosenIndex = map.Rand.Next(possibleRooms.Count);
            Loc startLoc = possibleRooms[chosenIndex].center;
            List<Rect> housePhases = possibleRooms[chosenIndex].phases;
            int[] blockedPhaseTiles = possibleRooms[chosenIndex].blockedTiles;

            //determine the number of free tiles to put items on; trim the maximum item spawn accordingly (maximum <= 1/2 of free tiles)
            //determine the number of free tiles to put mobs on; trim the maximum mob spawn accordingly (maximum <= 1/2 of free tiles)
            List<Loc> itemTiles = new List<Loc>();
            Rect lastPhase = housePhases[housePhases.Count - 1];
            int mobSpace = 0;
            for (int xx = lastPhase.X; xx < lastPhase.X + lastPhase.Size.X; xx++)
            {
                for (int yy = lastPhase.Y; yy < lastPhase.Y + lastPhase.Size.Y; yy++)
                {
                    Loc testLoc = new Loc(xx, yy);
                    if (map.WallTerrain.TileEquivalent(map.GetTile(testLoc)))
                    {
                        if (!map.HasTileEffect(new Loc(xx, yy)) && (map.GetPostProc(testLoc).Status & (PostProcType.Panel | PostProcType.Item)) == PostProcType.None)
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
                        //no need to check for mobs; assume all the area is mobless
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
            int initialCount = chosenMobs.Count;
            int currentTiles = 0;
            int currentMobs = 0;
            List <List<MobSpawn>> phasedMobs = new List<List<MobSpawn>>();
            for (int ii = 0; ii < housePhases.Count; ii++)
            {
                //do center first, then go from outside to in
                //this is because center usually overdrafts, so outside needs to compensate
                int idx = ii;
                if (idx > 0)
                    idx = housePhases.Count - ii;

                currentTiles += blockedPhaseTiles[idx];
                int minAmt = 1;//all phases
                if (idx == 0)//first phase is always at least 2
                    minAmt = 2;
                int scaledTotal = initialCount * currentTiles / mobSpace;
                //int linearTotal = initialCount * (idx + 1) / housePhases.Count;
                int amount = Math.Min(Math.Max(scaledTotal - currentMobs, minAmt), blockedPhaseTiles[idx]);
                currentMobs += amount;
                List<MobSpawn> phaseList = new List<MobSpawn>();
                for (int kk = 0; kk < amount; kk++)
                {
                    if (chosenMobs.Count == 0)
                        break;
                    MobSpawn copyMob = chosenMobs[chosenMobs.Count - 1].Copy();
                    chosenMobs.RemoveAt(chosenMobs.Count - 1);
                    if (map.Rand.Next(ALT_COLOR_ODDS) == 0)
                    {
                        SkinTableState table = DataManager.Instance.UniversalEvent.UniversalStates.GetWithDefault<SkinTableState>();
                        copyMob.BaseForm.Skin = table.AltColor;
                    }
                    phaseList.Add(copyMob);
                }
                phasedMobs.Add(phaseList);
            }

            //cover the room in a check that holds all of the monsters, and covers the room's bounds
            CheckIntrudeBoundsEvent check = new CheckIntrudeBoundsEvent();
            check.Bounds = new Rect(startLoc, new Loc(1));
            {
                //fill in all wall tiles with impassable
                Rect finalPhase = housePhases[housePhases.Count - 1];
                for (int xx = finalPhase.X; xx < finalPhase.X + finalPhase.Size.X; xx++)
                {
                    for (int yy = finalPhase.Y; yy < finalPhase.Y + finalPhase.Size.Y; yy++)
                    {
                        Loc loc = new Loc(xx, yy);
                        if (map.WallTerrain.TileEquivalent(map.GetTile(loc)))
                            map.SetTile(loc, (Tile)map.UnbreakableTerrain.Copy());
                    }
                }

                //Make monster houses visible if set
                MonsterHouseTableState mhtable = DataManager.Instance.UniversalEvent.UniversalStates.GetWithDefault<MonsterHouseTableState>();

                if (mhtable != null && mhtable.MonsterHouseWarningTile != null)
                {
                    if (map.RoomTerrain.TileEquivalent(map.GetTile(startLoc)))
                    ((IPlaceableGenContext<EffectTile>)map).PlaceItem(startLoc,  new EffectTile(mhtable.MonsterHouseWarningTile, true));
                }

                MonsterHallMapEvent house = new MonsterHallMapEvent();
                house.Phases.AddRange(housePhases);
                house.Mobs.AddRange(phasedMobs);
                check.Effects.Add(house);
            }

            AddIntrudeStep(map, check);
        }
    }

}
