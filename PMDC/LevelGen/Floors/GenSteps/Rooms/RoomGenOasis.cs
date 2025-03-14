// <copyright file="RoomGenOasis.cs" company="Audino">
// Copyright (c) Audino
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence.Dungeon;

namespace PMDC.LevelGen
{
    /// <summary>
    /// Generates a cave-like room using cellular automata, then fills it with water except an outer border.
    /// Can also spawn items on the shore.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class RoomGenOasis<T> : RoomGenCave<T>
        where T : ITiledGenContext, IPlaceableGenContext<MapItem>
    {
        public RoomGenOasis()
        {
            Treasures = new SpawnList<MapItem>();
        }

        public RoomGenOasis(RandRange width, RandRange height)
            : base(width, height)
        {
            Treasures = new SpawnList<MapItem>();
        }

        protected RoomGenOasis(RoomGenOasis<T> other) : base(other)
        {
            ItemAmount = other.ItemAmount;
            Treasures = new SpawnList<MapItem>();
            for (int ii = 0; ii < other.Treasures.Count; ii++)
                Treasures.Add(new MapItem(other.Treasures.GetSpawn(ii)), other.Treasures.GetSpawnRate(ii));
            WaterTerrain = other.WaterTerrain.Copy();
        }

        /// <summary>
        /// The amount of items to spawn.
        /// </summary>
        public int ItemAmount;

        /// <summary>
        /// Which items to spawn.
        /// </summary>
        public SpawnList<MapItem> Treasures;

        /// <summary>
        /// The terrain used for the water ring.
        /// </summary>
        public ITile WaterTerrain;

        public override RoomGen<T> Copy() => new RoomGenOasis<T>(this);

        public override void DrawOnMap(T map)
        {
            base.DrawOnMap(map);

            if (this.Draw.Width != this.Tiles.Length || this.Draw.Height != this.Tiles[0].Length)
                return;

            // place the wate
            bool[][] waterGrid = new bool[this.Draw.Width][];
            bool[][] outerWallGrid = new bool[this.Draw.Width][];
            for (int xx = 0; xx < this.Draw.Width; xx++)
            {
                outerWallGrid[xx] = new bool[this.Draw.Height];
                waterGrid[xx] = new bool[this.Draw.Height];
                for (int yy = 0; yy < this.Draw.Height; yy++)
                {
                    if (canPlaceWater(xx, yy))
                        waterGrid[xx][yy] = true;
                }
            }

            Grid.FloodFill(new Rect(0, 0, this.Draw.Width, this.Draw.Height),
                    (Loc testLoc) =>
                    {
                        return outerWallGrid[testLoc.X][testLoc.Y] || waterGrid[testLoc.X][testLoc.Y];
                    },
                    (Loc testLoc) =>
                    {
                        return outerWallGrid[testLoc.X][testLoc.Y] || waterGrid[testLoc.X][testLoc.Y];
                    },
                    (Loc fillLoc) =>
                    {
                        outerWallGrid[fillLoc.X][fillLoc.Y] = true;
                    },
                    new Loc(0, 0));

            List<Loc> freeTiles = new List<Loc>();
            //then convert all the inner tiles to water
            for (int xx = 0; xx < this.Draw.Width; xx++)
            {
                for (int yy = 0; yy < this.Draw.Height; yy++)
                {
                    if (this.Tiles[xx][yy])
                    {
                        if (!outerWallGrid[xx][yy])
                            map.SetTile(new Loc(this.Draw.X + xx, this.Draw.Y + yy), WaterTerrain.Copy());
                        else
                        {
                            //this is ground.  eligible for item.
                            freeTiles.Add(new Loc(this.Draw.X + xx, this.Draw.Y + yy));
                        }
                    }
                }
            }

            if (Treasures.Count > 0)
            {
                for (int ii = 0; ii < ItemAmount; ii++)
                {
                    MapItem item = new MapItem(Treasures.Pick(map.Rand));
                    int randIndex = map.Rand.Next(freeTiles.Count);
                    map.PlaceItem(freeTiles[randIndex], item);
                    freeTiles.RemoveAt(randIndex);
                }
            }
        }

        private bool canPlaceWater(int xx, int yy)
        {
            for (int tx = -1; tx <= 1; tx++)
            {
                for (int ty = -1; ty <= 1; ty++)
                {
                    if (!Collision.InBounds(this.Tiles.Length, this.Tiles[0].Length, new Loc(xx + tx, yy + ty)))
                        return false;
                    if (!this.Tiles[xx + tx][yy + ty])
                        return false;
                }
            }
            return true;
        }
    }
}
