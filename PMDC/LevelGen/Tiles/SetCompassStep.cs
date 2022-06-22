using System;
using RogueElements;
using System.Collections.Generic;
using RogueEssence.LevelGen;
using RogueEssence.Dev;
using RogueEssence.Data;
using RogueEssence.Dungeon;
using PMDC.Dungeon;

namespace PMDC.LevelGen
{
    /// <summary>
    /// Orients all already-placed compass tiles to point to points of interest.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class SetCompassStep<T> : GenStep<T>
        where T : StairsMapGenContext
    {
        /// <summary>
        /// Tile used as compass.
        /// </summary>
        [DataType(0, DataManager.DataType.Tile, false)]
        public int CompassTile;

        public SetCompassStep()
        {
        }

        public SetCompassStep(int tile)
        {
            CompassTile = tile;
        }

        public override void Apply(T map)
        {
            List<Tile> compassTiles = new List<Tile>();
            List<Loc> endpointTiles = new List<Loc>();
            TileData tileData = DataManager.Instance.GetTile(CompassTile);
            CompassEvent compassEvent = null;
            foreach (SingleCharEvent effect in tileData.InteractWithTiles.EnumerateInOrder())
            {
                compassEvent = effect as CompassEvent;
                if (effect != null)
                    break;
            }

            for (int xx = 0; xx < map.Width; xx++)
            {
                for (int yy = 0; yy < map.Height; yy++)
                {
                    Loc tileLoc = new Loc(xx, yy);
                    Tile tile = map.Map.GetTile(tileLoc);
                    if (tile.Effect.ID == CompassTile)
                        compassTiles.Add(tile);
                    else if (compassEvent.EligibleTiles.Contains(tile.Effect.ID))
                        endpointTiles.Add(tileLoc);
                }
            }
            foreach (Tile compass in compassTiles)
            {
                TileListState destState = new TileListState();
                foreach (Loc loc in endpointTiles)
                    destState.Tiles.Add(loc);
                foreach (MapGenExit exit in map.GenExits)
                    destState.Tiles.Add(exit.Loc);
                compass.Effect.TileStates.Set(destState);
            }
        }
    }
}
