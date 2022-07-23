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
    /// Adds an extra room to the layout that can only be accessed by using a key item.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class KeyDetourStep<T> : BaseDetourStep<T> where T : BaseMapGenContext
    {
        /// <summary>
        /// The tile with which to lock the room with.
        /// </summary>
        [JsonConverter(typeof(TileConverter))]
        [DataType(0, DataManager.DataType.Tile, false)]
        public string LockedTile;

        /// <summary>
        /// The item with which to unlock the room with.
        /// </summary>
        [DataType(0, DataManager.DataType.Item, false)]
        public int KeyItem;

        public KeyDetourStep()
        { }

        public KeyDetourStep(string sealedTile, int keyItem) : this()
        {
            LockedTile = sealedTile;
            KeyItem = keyItem;
        }

        public override void Apply(T map)
        {
            Grid.LocTest checkGround = (Loc testLoc) =>
            {
                return (map.RoomTerrain.TileEquivalent(map.GetTile(testLoc)) && !map.HasTileEffect(testLoc));
            };
            Grid.LocTest checkBlock = (Loc testLoc) =>
            {
                return map.WallTerrain.TileEquivalent(map.GetTile(testLoc));
            };

            List<LocRay4> rays = Detection.DetectWalls(((IViewPlaceableGenContext<MapGenEntrance>)map).GetLoc(0), new Rect(0, 0, map.Width, map.Height), checkBlock, checkGround);

            EffectTile effect = new EffectTile(LockedTile, true);
            TileListState state = new TileListState();
            effect.TileStates.Set(state);
            effect.TileStates.Set(new UnlockState(KeyItem));

            List<Loc> freeTiles = new List<Loc>();
            LocRay4? ray = PlaceRoom(map, rays, effect, freeTiles);

            if (ray != null)
                PlaceEntities(map, freeTiles);

        }

    }
}
