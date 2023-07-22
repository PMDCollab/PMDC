using System;
using RogueElements;
using RogueEssence.Dungeon;
using RogueEssence;
using RogueEssence.LevelGen;
using PMDC.Dungeon;
using System.Collections.Generic;
using RogueEssence.Dev;
using RogueEssence.Data;
using Newtonsoft.Json;

namespace PMDC.LevelGen
{
    /// <summary>
    /// One part of several steps used to create a room sealed by terrain, or several thereof.
    /// This step takes the target rooms and surrounds them with the selected walls, with one key block used to unlock them.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class TerrainSealStep<T> : BaseSealStep<T> where T : ListMapGenContext
    {
        /// <summary>
        /// The tile that is used to block off the main entrance to the room.
        /// </summary>
        [DataType(0, DataManager.DataType.Terrain, false)]
        public string SealTerrain;

        /// <summary>
        /// The tile that is used to border the room.
        /// </summary>
        [DataType(0, DataManager.DataType.Terrain, false)]
        public string BorderTerrain;

        public TerrainSealStep()
        {
            SealTerrain = "";
            BorderTerrain = "";
        }

        public TerrainSealStep(string sealedTerrain, string borderTerrain) : base()
        {
            SealTerrain = sealedTerrain;
            BorderTerrain = borderTerrain;
        }

        protected override void PlaceBorders(T map, Dictionary<Loc, SealType> sealList)
        {
            foreach (Loc loc in sealList.Keys)
            {
                //Do nothing for unbreakables
                if (map.UnbreakableTerrain.TileEquivalent(map.GetTile(loc)))
                    continue;

                switch (sealList[loc])
                {
                    //lay down the blocks
                    case SealType.Blocked:
                        map.SetTile(loc, new Tile(BorderTerrain));
                        break;
                    case SealType.Locked:
                    case SealType.Key:
                        map.SetTile(loc, new Tile(SealTerrain));
                        break;
                }
            }
        }

    }
}
