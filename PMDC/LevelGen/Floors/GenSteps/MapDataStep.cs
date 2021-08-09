using System;
using RogueEssence.Dungeon;
using System.Collections.Generic;
using RogueElements;
using RogueEssence.Data;
using RogueEssence.LevelGen;
using System.Text;
using RogueEssence.Dev;
using PMDC.Dungeon;

namespace PMDC.LevelGen
{
    [Serializable]
    public class MapDataStep<T> : GenStep<T> where T : BaseMapGenContext
    {
        [Music(0)]
        public string Music;
        public int TimeLimit;
        public Map.SightRange TileSight;
        public Map.SightRange CharSight;

        public MapDataStep()
        {
            Music = "";
        }
        public MapDataStep(string music, int timeLimit, Map.SightRange tileSight, Map.SightRange charSight)
        {
            Music = music;
            TimeLimit = timeLimit;
            TileSight = tileSight;
            CharSight = charSight;
        }

        public override void Apply(T map)
        {
            map.Map.Music = Music;

            if (TimeLimit > 0)
            {
                MapStatus timeStatus = new MapStatus(22);
                timeStatus.LoadFromData();
                MapCountDownState timeState = timeStatus.StatusStates.GetWithDefault<MapCountDownState>();
                timeState.Counter = TimeLimit;
                map.Map.Status.Add(22, timeStatus);
            }


            map.Map.TileSight = TileSight;
            map.Map.CharSight = CharSight;
        }


        public override string ToString()
        {
            return String.Format("{0}: Time:{1} Song:{2} TileSight:{3} CharSight:{4}", this.GetType().Name, TimeLimit, Music, TileSight, CharSight);
        }
    }

    [Serializable]
    public class DefaultMapStatusStep<T> : GenStep<T> where T : BaseMapGenContext
    {
        [DataType(0, DataManager.DataType.MapStatus, false)]
        public int SetterID;
        [DataType(1, DataManager.DataType.MapStatus, false)]
        public int[] DefaultMapStatus;

        public DefaultMapStatusStep()
        {
            DefaultMapStatus = new int[1] { 0 };
        }
        public DefaultMapStatusStep(int statusSetter, params int[] defaultStatus)
        {
            SetterID = statusSetter;
            DefaultMapStatus = defaultStatus;
        }

        public override void Apply(T map)
        {
            int chosenStatus = DefaultMapStatus[map.Rand.Next(DefaultMapStatus.Length)];
            MapStatus statusSetter = new MapStatus(SetterID);
            statusSetter.LoadFromData();
            MapIndexState indexState = statusSetter.StatusStates.GetWithDefault<MapIndexState>();
            indexState.Index = chosenStatus;
            map.Map.Status.Add(SetterID, statusSetter);
        }


        public override string ToString()
        {
            string status = String.Format("{0} Choices", DefaultMapStatus.Length);
            if (status.Length == 1)
                status = DataManager.Instance.DataIndices[DataManager.DataType.MapStatus].Entries[status[0]].Name.ToLocal();
            return String.Format("{0}: {1}", this.GetType().Name, status);
        }
    }



    [Serializable]
    public class StateMapStatusStep<T> : GenStep<T> where T : BaseMapGenContext
    {
        public int MapStatus;
        public StateCollection<MapStatusState> States;

        public StateMapStatusStep()
        {
            States = new StateCollection<MapStatusState>();
        }
        public StateMapStatusStep(int mapStatus, MapStatusState state) : this()
        {
            MapStatus = mapStatus;
            States.Set(state);
        }

        public override void Apply(T map)
        {
            MapStatus status = new MapStatus(MapStatus);
            status.LoadFromData();
            foreach(MapStatusState state in States)
                status.StatusStates.Set((MapStatusState)state.Clone());
            map.Map.Status.Add(MapStatus, status);
        }
    }
}
