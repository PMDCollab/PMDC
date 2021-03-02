using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence.Dungeon;
using RogueEssence;
using RogueEssence.LevelGen;
using PMDO.Dungeon;

namespace PMDO.LevelGen
{
    [Serializable]
    public class MonsterHallStep<T> : MonsterHouseBaseStep<T> where T : ListMapGenContext
    {
        public MonsterHallStep() : base() { Filters = new List<BaseRoomFilter>(); }
        public MonsterHallStep(List<BaseRoomFilter> filters) : base() { Filters = filters; }
        public MonsterHallStep(MonsterHallStep<T> other) : base(other)
        {
            Filters = new List<BaseRoomFilter>();
            Filters.AddRange(other.Filters);
        }
        public override MonsterHouseBaseStep<T> CreateNew() { return new MonsterHallStep<T>(this); }
        public List<BaseRoomFilter> Filters { get; set; }

        public override void Apply(T map)
        {
            //choose a room to cram all the items in
            List<(IRoomGen, Loc, List<Rect>, int[])> possibleRooms = new List<(IRoomGen, Loc, List<Rect>, int[])>();
            for (int ii = 0; ii < map.RoomPlan.HallCount; ii++)
            {
                IRoomGen cand = map.RoomPlan.GetHall(ii);
                if (!BaseRoomFilter.PassesAllFilters(map.RoomPlan.GetHallPlan(ii), this.Filters))
                    continue;
                if (cand.Draw.Width < 10 && cand.Draw.Height < 10 || cand.Draw.Width < 7 || cand.Draw.Height < 7)
                    continue;

                Loc? originPoint = Grid.FindClosestConnectedTile(cand.Draw.Start + new Loc(1), cand.Draw.Size - new Loc(2),
                    (Loc testLoc) => { return !map.TileBlocked(testLoc); },
                    (Loc testLoc) => { return false; }, (Loc testLoc) => { return false; },
                    cand.Draw.Center);

                //verify we have a walkable hall point
                if (originPoint == null)
                    continue;

                //verify that it is a chokepoint
                bool chokePoint = Grid.IsChokePoint(originPoint.Value - new Loc(1), new Loc(3), originPoint.Value,
                    map.TileBlocked, (Loc testLoc) => { return true; });

                if (!chokePoint)
                    continue;

                Rect potentialBounds = new Rect(originPoint.Value - new Loc(7), new Loc(15));

                //correct monsterhouse bounds within the room bounds
                if (potentialBounds.Bottom > cand.Draw.Bottom - 1)
                    potentialBounds.Start = new Loc(potentialBounds.Start.X, potentialBounds.Start.Y - (potentialBounds.Bottom - (cand.Draw.Bottom - 1)));
                if (potentialBounds.Right > cand.Draw.Right - 1)
                    potentialBounds.Start = new Loc(potentialBounds.Start.X - (potentialBounds.Right - (cand.Draw.Right - 1)), potentialBounds.Start.Y);

                if (potentialBounds.Top < cand.Draw.Top + 1)
                    potentialBounds.Start = new Loc(potentialBounds.Start.X, cand.Draw.Top + 1);
                if (potentialBounds.Left < cand.Draw.Left + 1)
                    potentialBounds.Start = new Loc(cand.Draw.Left + 1, potentialBounds.Start.Y);

                potentialBounds.Size = new Loc(Math.Min(potentialBounds.Width, cand.Draw.Width-2), Math.Min(potentialBounds.Height, cand.Draw.Height - 2));

                //check to see if every phase of the crumble will sufficiently remove enough walls
                //also check against unbreakable tiles; if we find any, the room is invalid
                List<Rect> phases = new List<Rect>();
                phases.Add(new Rect(originPoint.Value, new Loc(1)));
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
                        if (map.Tiles[xx][yy].TileEquivalent(map.UnbreakableTerrain))
                        {
                            containsImpassable = true;
                            break;
                        }
                        else if (map.Tiles[xx][yy].TileEquivalent(map.WallTerrain))
                        {
                            for (int nn = 0; nn < phases.Count; nn++)
                            {
                                if (Collision.InBounds(phases[nn], new Loc(xx, yy)))
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
                    continue;

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
                    continue;

                possibleRooms.Add((cand, originPoint.Value, phases, blockedTiles));
            }

            if (possibleRooms.Count == 0)
                return;

            int chosenIndex = map.Rand.Next(possibleRooms.Count);
            IRoomGen room = possibleRooms[chosenIndex].Item1;
            Loc startLoc = possibleRooms[chosenIndex].Item2;
            List<Rect> housePhases = possibleRooms[chosenIndex].Item3;
            int[] blockedPhaseTiles = possibleRooms[chosenIndex].Item4;

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
                    if (map.Tiles[xx][yy].TileEquivalent(map.WallTerrain))
                    {
                        if (!map.HasTileEffect(new Loc(xx, yy)) && !map.PostProcGrid[xx][yy].Status[(int)PostProcType.Panel] && !map.PostProcGrid[xx][yy].Status[(int)PostProcType.Item])
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
            List<List<MobSpawn>> phasedMobs = new List<List<MobSpawn>>();
            int initialCount = chosenMobs.Count;
            int currentTiles = 0;
            int currentMobs = 0;
            for (int ii = 0; ii < housePhases.Count; ii++)
            {
                currentTiles += blockedPhaseTiles[ii];
                int amount = Math.Min(Math.Max(initialCount * currentTiles / mobSpace - currentMobs, 3), blockedPhaseTiles[ii]);
                currentMobs += amount;
                List<MobSpawn> phaseList = new List<MobSpawn>();
                for (int kk = 0; kk < amount; kk++)
                {
                    if (chosenMobs.Count == 0)
                        break;
                    MobSpawn copyMob = chosenMobs[chosenMobs.Count - 1].Copy();
                    chosenMobs.RemoveAt(chosenMobs.Count - 1);
                    if (map.Rand.Next(ALT_COLOR_ODDS) == 0)
                        copyMob.BaseForm.Skin = 1;
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
                        if (map.Tiles[xx][yy].TileEquivalent(map.WallTerrain))
                            map.Tiles[xx][yy] = (Tile)map.UnbreakableTerrain.Copy();
                    }
                }


                MonsterHallMapEvent house = new MonsterHallMapEvent();
                house.Phases.AddRange(housePhases);
                house.Mobs.AddRange(phasedMobs);
                check.Effects.Add(house);
            }
            map.CheckEvents.Add(check);
        }
    }

}
