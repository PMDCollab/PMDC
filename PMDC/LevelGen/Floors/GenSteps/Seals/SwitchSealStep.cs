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
    public class SwitchSealStep<T> : BaseSealStep<T> where T : ListMapGenContext
    {
        public int SealedTile;
        public int SwitchTile;
        public bool TimeLimit;

        public List<BaseRoomFilter> SwitchFilters { get; set; }

        public SwitchSealStep()
        {
            SwitchFilters = new List<BaseRoomFilter>();
        }

        public SwitchSealStep(int sealedTile, int switchTile, bool timeLimit) : base()
        {
            SealedTile = sealedTile;
            SwitchTile = switchTile;
            TimeLimit = timeLimit;
            SwitchFilters = new List<BaseRoomFilter>();
        }

        protected override void PlaceBorders(T map, Dictionary<Loc, SealType> sealList)
        {
            List<Loc> freeSwitchTiles = new List<Loc>();

            for (int ii = 0; ii < map.RoomPlan.RoomCount; ii++)
            {
                FloorRoomPlan plan = map.RoomPlan.GetRoomPlan(ii);
                if (!BaseRoomFilter.PassesAllFilters(plan, this.SwitchFilters))
                    continue;
                freeSwitchTiles.AddRange(((IPlaceableGenContext<EffectTile>)map).GetFreeTiles(plan.RoomGen.Draw));
            }
            for (int ii = 0; ii < map.RoomPlan.HallCount; ii++)
            {
                FloorHallPlan plan = map.RoomPlan.GetHallPlan(ii);
                if (!BaseRoomFilter.PassesAllFilters(plan, this.SwitchFilters))
                    continue;
                freeSwitchTiles.AddRange(((IPlaceableGenContext<EffectTile>)map).GetFreeTiles(plan.RoomGen.Draw));
            }

            //if there's no way to open the door, there cannot be a door; give the player the treasure unguarded
            if (freeSwitchTiles.Count == 0)
                return;

            List <Loc> lockList = new List<Loc>();

            foreach (Loc loc in sealList.Keys)
            {
                switch (sealList[loc])
                {
                    case SealType.Blocked:
                        map.Tiles[loc.X][loc.Y] = (Tile)map.UnbreakableTerrain.Copy();
                        break;
                    default:
                        lockList.Add(loc);
                        break;
                }
            }

            foreach (Loc loc in lockList)
            {
                map.Tiles[loc.X][loc.Y] = (Tile)map.UnbreakableTerrain.Copy();
                EffectTile newEffect = new EffectTile(SealedTile, true, loc);
                ((IPlaceableGenContext<EffectTile>)map).PlaceItem(loc, newEffect);
            }


            EffectTile switchTile = new EffectTile(SwitchTile, true);

            if (TimeLimit)
                switchTile.Danger = true;

            TileListState state = new TileListState();
            state.Tiles = lockList;
            switchTile.TileStates.Set(state);

            int randIndex = map.Rand.Next(freeSwitchTiles.Count);

            ((IPlaceableGenContext<EffectTile>)map).PlaceItem(freeSwitchTiles[randIndex], switchTile);
        }

    }
}
