// <copyright file="BlobWaterStep.cs" company="Audino">
// Copyright (c) Audino
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence;
using RogueEssence.Data;
using RogueEssence.Dungeon;

namespace PMDC.LevelGen
{
    /// <summary>
    /// Creates patterns of water by loading maps, and places them around the map.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class PatternWaterStep<T> : WaterStep<T>, IPatternWaterStep
        where T : class, ITiledGenContext
    {

        public PatternWaterStep()
            : base()
        {
            this.BlobStencil = new DefaultBlobStencil<T>();
        }

        public PatternWaterStep(RandRange amount, ITile terrain, ITerrainStencil<T> stencil, IBlobStencil<T> blobStencil)
            : base(terrain, stencil)
        {
            this.Amount = amount;
            this.BlobStencil = blobStencil;
        }

        /// <summary>
        /// The number of patterns to place.
        /// </summary>
        public RandRange Amount { get; set; }

        /// <summary>
        /// Map files to load.
        /// </summary>
        [RogueEssence.Dev.DataFolder(1, "Map/")]
        public SpawnList<string> Maps;

        /// <summary>
        /// Blob-wide stencil.  All-or-nothing: If the blob position passes this stencil, it is drawn.  Otherwise it is not.
        /// </summary>
        public IBlobStencil<T> BlobStencil { get; set; }

        public override void Apply(T map)
        {
            int chosenAmount = Amount.Pick(map.Rand);
            if (chosenAmount == 0 || Maps.Count == 0)
                return;

            Dictionary<string, Map> mapCache = new Dictionary<string, Map>();

            for (int ii = 0; ii < chosenAmount; ii++)
            {
                // attempt to place in 30 locations
                for (int jj = 0; jj < 30; jj++)
                {
                    string chosenPattern = Maps.Pick(map.Rand);

                    Map placeMap;
                    if (!mapCache.TryGetValue(chosenPattern, out placeMap))
                    {
                        placeMap = DataManager.Instance.GetMap(chosenPattern);
                        mapCache[chosenPattern] = placeMap;
                    }

                    // TODO: instead of transpose, just flipV and flipH with 50% for each?
                    bool transpose = (map.Rand.Next(2) == 0);
                    Loc size = placeMap.Size;
                    if (transpose)
                        size = size.Transpose();

                    int maxWidth = Math.Max(1, map.Width - size.X);
                    int maxHeight = Math.Max(1, map.Height - size.Y);
                    Loc offset = new Loc(map.Rand.Next(0, maxWidth), map.Rand.Next(0, maxHeight));
                    bool placed = this.AttemptBlob(map, placeMap, offset);

                    if (placed)
                        break;
                }
            }
        }

        public override string ToString()
        {
            return string.Format("{0}: Amt:{1} Maps:{2}", this.GetType().GetFormattedTypeName(), this.Amount.ToString(), this.Maps.Count.ToString());
        }

        protected virtual bool AttemptBlob(T map, Map placeMap, Loc offset)
        {
            bool IsBlobValid(Loc loc)
            {
                Loc srcLoc = loc - offset;
                if (Collision.InBounds(new Rect(Loc.Zero, placeMap.Size), srcLoc))
                    return !map.RoomTerrain.TileEquivalent(placeMap.Tiles[srcLoc.X][srcLoc.Y]);
                return false;
            }

            if (!this.BlobStencil.Test(map, new Rect(offset, placeMap.Size), IsBlobValid))
                return false;

            this.DrawBlob(map, new Rect(offset, placeMap.Size), IsBlobValid);
            return true;
        }
    }
}
