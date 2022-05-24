using System;
using RogueElements;
using RogueEssence.Dungeon;
using RogueEssence;
using RogueEssence.LevelGen;
using PMDC.Dungeon;
using System.Collections.Generic;

namespace PMDC.LevelGen
{
    [Serializable]
    public class KeySealStep<T> : BaseSealStep<T> where T : ListMapGenContext
    {
        public int LockedTile;
        public int KeyTile;
        public int KeyItem;

        public KeySealStep()
        {
        }

        public KeySealStep(int sealedTile, int keyTile, int keyItem) : base()
        {
            LockedTile = sealedTile;
            KeyTile = keyTile;
            KeyItem = keyItem;
        }

        protected override void PlaceBorders(T map, Dictionary<Loc, SealType> sealList)
        {
            List<Loc> keyList = new List<Loc>();
            List<Loc> lockList = new List<Loc>();

            foreach (Loc loc in sealList.Keys)
            {
                switch (sealList[loc])
                {
                    //lay down the blocks
                    case SealType.Blocked:
                        map.Tiles[loc.X][loc.Y] = (Tile)map.UnbreakableTerrain.Copy();
                        break;
                    case SealType.Locked:
                        lockList.Add(loc);
                        break;
                    case SealType.Key:
                        keyList.Add(loc);
                        break;
                }
            }


            //choose the key tile
            int keyIndex = map.Rand.Next(keyList.Count - 1);
            Loc keyLoc = keyList[keyIndex];
            keyList.RemoveAt(keyIndex);


            lockList.AddRange(keyList);

            //seal the tiles that are gated
            foreach (Loc loc in lockList)
            {
                map.Tiles[loc.X][loc.Y] = (Tile)map.UnbreakableTerrain.Copy();
                EffectTile newEffect = new EffectTile(LockedTile, true, loc);
                ((IPlaceableGenContext<EffectTile>)map).PlaceItem(loc, newEffect);
            }

            //finally, seal with a locked door
            {
                map.Tiles[keyLoc.X][keyLoc.Y] = (Tile)map.UnbreakableTerrain.Copy();
                EffectTile newEffect = new EffectTile(KeyTile, true, keyLoc);
                TileListState state = new TileListState();
                state.Tiles = lockList;
                newEffect.TileStates.Set(state);
                newEffect.TileStates.Set(new UnlockState(KeyItem));
                ((IPlaceableGenContext<EffectTile>)map).PlaceItem(keyLoc, newEffect);
            }
        }

    }
}
