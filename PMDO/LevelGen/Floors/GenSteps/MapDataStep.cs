using System;
using RogueEssence.Dungeon;
using System.Collections.Generic;
using RogueElements;
using RogueEssence.Data;
using RogueEssence.LevelGen;

namespace PMDO.LevelGen
{
    [Serializable]
    public class MapDataStep<T> : GenStep<T> where T : BaseMapGenContext
    {
        public string Music;
        public int TimeLimit;
        public Map.SightRange TileSight;
        public Map.SightRange CharSight;
        public int[] DefaultMapStatus;

        public MapDataStep()
        {
            Music = "";
            DefaultMapStatus = new int[1] { 0 };
        }
        public MapDataStep(string music, int timeLimit, Map.SightRange tileSight, Map.SightRange charSight)
        {
            Music = music;
            TimeLimit = timeLimit;
            TileSight = tileSight;
            CharSight = charSight;
            DefaultMapStatus = new int[1] { 0 };
        }
        public MapDataStep(string music, int timeLimit, Map.SightRange tileSight, Map.SightRange charSight, params int[] defaultStatus)
        {
            Music = music;
            TimeLimit = timeLimit;
            TileSight = tileSight;
            CharSight = charSight;
            DefaultMapStatus = defaultStatus;
        }

        public override void Apply(T map)
        {
            map.Map.Music = Music;

            MapStatus timeStatus = new MapStatus(22);
            timeStatus.LoadFromData();
            MapCountDownState timeState = timeStatus.StatusStates.GetWithDefault<MapCountDownState>();
            timeState.Counter = TimeLimit;
            map.Map.Status.Add(22, timeStatus);


            map.Map.TileSight = TileSight;
            map.Map.CharSight = CharSight;

            int chosenStatus = DefaultMapStatus[map.Rand.Next(DefaultMapStatus.Length)];
            MapStatus status = new MapStatus(chosenStatus);
            status.LoadFromData();
            int setterID = status.StatusStates.Contains<MapWeatherState>() ? 24 : 25;
            MapStatus statusSetter = new MapStatus(setterID);
            statusSetter.LoadFromData();
            MapIndexState indexState = statusSetter.StatusStates.GetWithDefault<MapIndexState>();
            indexState.Index = chosenStatus;
            map.Map.Status.Add(setterID, statusSetter);
        }
    }

}
