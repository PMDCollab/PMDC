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
    /// <typeparam name="TSpawnable"></typeparam>
    [Serializable]
    public class PatternSpawnStep<TGenContext, TSpawnable> : PatternPlacerStep<TGenContext>
        where TGenContext : class, ISpawningGenContext<TSpawnable>, IPlaceableGenContext<TSpawnable>, IFloorPlanGenContext
        where TSpawnable : ISpawnable
    {
        public PatternSpawnStep() : base()
        {

        }

        protected override void DrawOnLocs(TGenContext map, List<Loc> drawLocs)
        {
            if (!map.Spawner.CanPick)
                return;

            TSpawnable spawn = map.Spawner.Pick(map.Rand);
            foreach (Loc destLoc in drawLocs)
            {
                if (this.TerrainStencil.Test(map, destLoc))
                    map.PlaceItem(destLoc, spawn);
            }
        }

    }
}
