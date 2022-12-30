using System;
using RogueElements;
using RogueEssence.Dungeon;
using RogueEssence;
using RogueEssence.LevelGen;
using PMDC.Dungeon;
using System.Collections.Generic;
using Newtonsoft.Json;
using RogueEssence.Dev;

namespace PMDC.LevelGen
{
    /// <summary>
    /// One part of several steps used to create a switch-opened sealed room, or several thereof.
    /// This step takes the target rooms and surrounds them with unbreakable walls, with one key block used to unlock them.
    /// The filter must be able to single out the key rooms intended for this process.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class SwitchSealStep<T> : BaseSealStep<T> where T : ListMapGenContext
    {
        /// <summary>
        /// The tile that is used to block off the room.
        /// It is removed when the player pressed the switch.
        /// </summary>
        [JsonConverter(typeof(TileConverter))]
        public string SealedTile;

        /// <summary>
        /// The switch tile that unlocked the vaults.
        /// </summary>
        [JsonConverter(typeof(TileConverter))]
        public string SwitchTile;

        /// <summary>
        /// Determines if a time limit is triggered when pressing the switch.
        /// </summary>
        public bool TimeLimit;

        /// <summary>
        /// Determines the rooms that the switch can be placed in.
        /// </summary>
        public List<BaseRoomFilter> SwitchFilters { get; set; }

        public SwitchSealStep()
        {
            SwitchFilters = new List<BaseRoomFilter>();
        }

        public SwitchSealStep(string sealedTile, string switchTile, bool timeLimit) : base()
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
                        map.SetTile(loc, map.UnbreakableTerrain.Copy());
                        break;
                    default:
                        lockList.Add(loc);
                        break;
                }
            }

            foreach (Loc loc in lockList)
            {
                map.SetTile(loc, map.UnbreakableTerrain.Copy());
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
            map.GetPostProc(freeSwitchTiles[randIndex]).Status |= (PostProcType.Panel | PostProcType.Item | PostProcType.Terrain);
        }

    }
}
