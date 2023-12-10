using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence.Data;
using RogueEssence.Dungeon;

namespace PMDC.LevelGen
{
    /// <summary>
    /// Sets terrain in the entire floor to a certain value.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class FloorTerrainStep<T> : GenStep<T> where T : class, ITiledGenContext
    {
        /// <summary>
        /// Tile representing the water terrain to paint with.
        /// </summary>
        public ITile Terrain { get; set; }

        /// <summary>
        /// Determines which tiles are eligible to be painted on.
        /// </summary>
        public ITerrainStencil<T> TerrainStencil { get; set; }

        public FloorTerrainStep()
        {
            this.TerrainStencil = new DefaultTerrainStencil<T>();
        }

        public FloorTerrainStep(ITile terrain) : this()
        {
            Terrain = terrain;
        }

        public override void Apply(T map)
        {
            for (int xx = 0; xx < map.Width; xx++)
            {
                for (int yy = 0; yy < map.Height; yy++)
                {
                    Loc destLoc = new Loc(xx, yy);
                    if (this.TerrainStencil.Test(map, destLoc))
                        map.TrySetTile(destLoc, this.Terrain.Copy());
                }
            }
        }

    }

}
