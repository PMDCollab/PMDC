using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence.Data;
using RogueEssence.Dungeon;
using RogueEssence.LevelGen;

namespace PMDC.LevelGen
{
    /// <summary>
    /// Sets terrain in a room to a certain value.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class MapTileStep<T> : GenStep<T> where T : BaseMapGenContext
    {
        /// <summary>
        /// Tile representing the water terrain to paint with.
        /// </summary>
        public EffectTile Tile { get; set; }

        /// <summary>
        /// Determines which tiles are eligible to be painted on.
        /// </summary>
        public ITerrainStencil<T> TerrainStencil { get; set; }

        public MapTileStep()
        {
            this.TerrainStencil = new DefaultTerrainStencil<T>();
        }

        public MapTileStep(EffectTile tile) : this()
        {
            Tile = tile;
        }

        public override void Apply(T map)
        {
            for (int xx = 0; xx < map.Width; xx++)
            {
                for (int yy = 0; yy < map.Height; yy++)
                {
                    Loc destLoc = new Loc(xx, yy);
                    if (this.TerrainStencil.Test(map, destLoc))
                        ((IPlaceableGenContext<EffectTile>)map).PlaceItem(destLoc, new EffectTile(Tile));
                }
            }
        }

    }

}
