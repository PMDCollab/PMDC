using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence.Data;
using RogueEssence.Dungeon;

namespace PMDC.LevelGen
{
    /// <summary>
    /// Litters placeables on a room in specified patterns.
    /// </summary>
    /// <typeparam name="TGenContext"></typeparam>
    [Serializable]
    public class PatternTerrainStep<TGenContext> : PatternPlacerStep<TGenContext>
        where TGenContext : class, ITiledGenContext, IFloorPlanGenContext
    {

        /// <summary>
        /// Tile representing the water terrain to paint with.
        /// </summary>
        public ITile Terrain { get; set; }

        public PatternTerrainStep() : base()
        {
        }

        public PatternTerrainStep(ITile terrain) : base()
        {
            Terrain = terrain;
        }

        protected override void DrawOnLocs(TGenContext map, List<Loc> drawLocs)
        {
            ITile tile = Terrain;
            foreach (Loc destLoc in drawLocs)
            {
                if (this.TerrainStencil.Test(map, destLoc))
                    map.TrySetTile(destLoc, tile.Copy());
            }
        }

    }
}
