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
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="TSpawnable"></typeparam>
    [Serializable]
    public class PatternSpawnCenterStep<T, TSpawnable> : PatternPlacerStep<T>
        where T : class, IPlaceableGenContext<TSpawnable>, IFloorPlanGenContext
        where TSpawnable : ISpawnable
    {
        public IRandPicker<TSpawnable> Spawner { get; set; }
        public IRandPicker<TSpawnable> CenterSpawner { get; set; }

        public int CenterSpawnChance;

        public PatternSpawnCenterStep() : base()
        {
        }

        protected override void DrawOnLocs(T map, IRoomGen room, List<Loc> drawLocs)
        {
            if (!Spawner.CanPick)
                return;

            Loc? centerLoc = null;
            TSpawnable centerSpawn = default(TSpawnable);
            if (CenterSpawner.CanPick)
            {
                if (map.Rand.Next(100) < CenterSpawnChance)
                {
                    Loc roomCenter = room.Draw.Center;
                    //find the center
                    foreach (Loc destLoc in drawLocs)
                    {
                        if (centerLoc == null || (destLoc - roomCenter).DistSquared() < (centerLoc.Value - roomCenter).DistSquared())
                            centerLoc = destLoc;
                    }
                    centerSpawn = CenterSpawner.Pick(map.Rand);
                }
            }

            TSpawnable spawn = Spawner.Pick(map.Rand);
            foreach (Loc destLoc in drawLocs)
            {
                if (this.TerrainStencil.Test(map, destLoc) && map.CanPlaceItem(destLoc))
                {
                    if (centerLoc != null && centerLoc.Value == destLoc)
                        map.PlaceItem(destLoc, centerSpawn);
                    else
                        map.PlaceItem(destLoc, spawn);
                }
            }
        }

    }
}
