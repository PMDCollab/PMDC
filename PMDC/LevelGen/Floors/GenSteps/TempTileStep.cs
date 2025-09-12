using System;
using RogueElements;
using RogueEssence.Dungeon;
using RogueEssence;
using RogueEssence.LevelGen;
using PMDC.Dungeon;
using System.Collections.Generic;
using Newtonsoft.Json;
using RogueEssence.Dev;
using RogueEssence.Data;

namespace PMDC.LevelGen
{
    /// <summary>
    /// Places a temporary tile on the map by specifying a tile type and the map status used to count down and remove it.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class TempTileStep<T> : GenStep<T> where T : ListMapGenContext
    {
        /// <summary>
        /// The temp tile
        /// </summary>
        public IRandPicker<EffectTile> TempTile;

        /// <summary>
        /// The status to keep track of the countdown
        /// </summary>
        [JsonConverter(typeof(MapStatusConverter))]
        [DataType(0, DataManager.DataType.MapStatus, false)]
        public string TempStatus;

        /// <summary>
        /// Determines the rooms that the switch can be placed in.
        /// </summary>
        public List<BaseRoomFilter> TileFilters { get; set; }

        public TempTileStep()
        {
            TileFilters = new List<BaseRoomFilter>();
        }

        public TempTileStep(IRandPicker<EffectTile> tempTile, string tempStatus) : base()
        {
            TempTile = tempTile;
            TempStatus = tempStatus;
            TileFilters = new List<BaseRoomFilter>();
        }

        public override void Apply(T map)
        {
            //all free tiles to place switches, by room
            List<Loc> freeTiles = new List<Loc>();

            for (int ii = 0; ii < map.RoomPlan.RoomCount; ii++)
            {
                FloorRoomPlan plan = map.RoomPlan.GetRoomPlan(ii);
                if (!BaseRoomFilter.PassesAllFilters(plan, this.TileFilters))
                    continue;
                freeTiles.AddRange(((IPlaceableGenContext<EffectTile>)map).GetFreeTiles(plan.RoomGen.Draw));
            }
            for (int ii = 0; ii < map.RoomPlan.HallCount; ii++)
            {
                FloorHallPlan plan = map.RoomPlan.GetHallPlan(ii);
                if (!BaseRoomFilter.PassesAllFilters(plan, this.TileFilters))
                    continue;
                freeTiles.AddRange(((IPlaceableGenContext<EffectTile>)map).GetFreeTiles(plan.RoomGen.Draw));
            }

            if (freeTiles.Count == 0)
                return;

            int randTileIndex = map.Rand.Next(freeTiles.Count);
            Loc destLoc = freeTiles[randTileIndex];
            EffectTile switchTile = TempTile.Pick(map.Rand);

            ((IPlaceableGenContext<EffectTile>)map).PlaceItem(destLoc, new EffectTile(switchTile));
            map.GetPostProc(destLoc).Status |= (PostProcType.Panel | PostProcType.Item | PostProcType.Terrain);

            Loc entranceLoc = ((IViewPlaceableGenContext<MapGenEntrance>)map).GetLoc(0);
            int manhattanDistance = (entranceLoc - destLoc).Dist4();

            MapStatus tempStatus = new MapStatus(TempStatus);
            tempStatus.LoadFromData();
            MapLocState locState = tempStatus.StatusStates.GetWithDefault<MapLocState>();
            locState.Target = destLoc;
            MapCountDownState countdown = tempStatus.StatusStates.GetWithDefault<MapCountDownState>();
            //the player gets a time of 3x the distance rounded up to the 10.
            countdown.Counter = (manhattanDistance * 3 / 10 + 1) * 10;
            map.Map.Status.Add(TempStatus, tempStatus);
        }


    }
}
