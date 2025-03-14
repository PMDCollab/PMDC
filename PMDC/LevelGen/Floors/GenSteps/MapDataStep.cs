using System;
using RogueEssence.Dungeon;
using System.Collections.Generic;
using RogueElements;
using RogueEssence.Data;
using RogueEssence.LevelGen;
using System.Text;
using RogueEssence.Dev;
using PMDC.Dungeon;
using Newtonsoft.Json;

namespace PMDC.LevelGen
{
    /// <summary>
    /// Sets various attributes about the map.
    /// Including the music, time limit, and darkness of the floor. 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class MapDataStep<T> : GenStep<T> where T : BaseMapGenContext
    {
        /// <summary>
        /// The default map music.
        /// </summary>
        [Music(0)]
        public string Music;

        /// <summary>
        /// How many turns the player can spend on the map before an instant game over.
        /// </summary>
        public int TimeLimit;

        /// <summary>
        /// The darkness level for map exploration.
        /// </summary>
        public Map.SightRange TileSight;

        /// <summary>
        /// The darkness level for character viewing.
        /// </summary>
        public Map.SightRange CharSight;

        /// <summary>
        /// Clamps the map edges so that the camera does not scroll past them.  Does not work on wrapped-around maps.
        /// </summary>
        public bool ClampCamera;

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
                MapStatus timeStatus = new MapStatus("somethings_stirring");
                timeStatus.LoadFromData();
                MapCountDownState timeState = timeStatus.StatusStates.GetWithDefault<MapCountDownState>();
                timeState.Counter = TimeLimit;
                map.Map.Status.Add("somethings_stirring", timeStatus);
            }


            map.Map.TileSight = TileSight;
            map.Map.CharSight = CharSight;

            if (map.Map.EdgeView != BaseMap.ScrollEdge.Wrap)
                map.Map.EdgeView = ClampCamera ? BaseMap.ScrollEdge.Clamp : BaseMap.ScrollEdge.Blank;
        }


        public override string ToString()
        {
            return String.Format("{0}: Time:{1} Song:{2} TileSight:{3} CharSight:{4}", this.GetType().GetFormattedTypeName(), TimeLimit, Music, TileSight, CharSight);
        }
    }

    /// <summary>
    /// Makes the map name show up before fading in.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class MapTitleDropStep<T> : GenStep<T> where T : BaseMapGenContext
    {
        public Priority DropPriority;

        public MapTitleDropStep()
        {
        }
        public MapTitleDropStep(Priority dropPriority)
        {
            DropPriority = dropPriority;
        }

        public override void Apply(T map)
        {
            map.Map.MapEffect.OnMapStarts.Add(DropPriority, new FadeTitleEvent());
        }


        public override string ToString()
        {
            return String.Format("{0}: At:{1}", this.GetType().GetFormattedTypeName(), DropPriority);
        }
    }

    /// <summary>
    /// Sets only time limit for the map.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class MapTimeLimitStep<T> : GenStep<T> where T : BaseMapGenContext
    {
        /// <summary>
        /// How many turns the player can spend on the map before an instant game over.
        /// </summary>
        public int TimeLimit;

        public MapTimeLimitStep()
        {
        }
        public MapTimeLimitStep(int timeLimit)
        {
            TimeLimit = timeLimit;
        }

        public override void Apply(T map)
        {
            if (TimeLimit > 0)
            {
                MapStatus timeStatus = new MapStatus("somethings_stirring");
                timeStatus.LoadFromData();
                MapCountDownState timeState = timeStatus.StatusStates.GetWithDefault<MapCountDownState>();
                timeState.Counter = TimeLimit;
                map.Map.Status.Add("somethings_stirring", timeStatus);
            }
        }


        public override string ToString()
        {
            return String.Format("{0}: Time:{1}", this.GetType().GetFormattedTypeName(), TimeLimit);
        }
    }

    /// <summary>
    /// Adds a map status that is considered the "default" for that map.
    /// The map will always revert back to this status even if replaced (it will wait for the replacing status to run out).
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class DefaultMapStatusStep<T> : GenStep<T> where T : BaseMapGenContext
    {
        /// <summary>
        /// The map status used to set the default map status.
        /// </summary>
        [JsonConverter(typeof(MapStatusConverter))]
        [DataType(0, DataManager.DataType.MapStatus, false)]
        public string SetterID;

        /// <summary>
        /// The possible default map statuses.
        /// </summary>
        [JsonConverter(typeof(MapStatusArrayConverter))]
        [DataType(1, DataManager.DataType.MapStatus, false)]
        public string[] DefaultMapStatus;

        public DefaultMapStatusStep()
        {

        }
        public DefaultMapStatusStep(string statusSetter, params string[] defaultStatus)
        {
            SetterID = statusSetter;
            DefaultMapStatus = defaultStatus;
        }

        public override void Apply(T map)
        {
            string chosenStatus = DefaultMapStatus[map.Rand.Next(DefaultMapStatus.Length)];
            MapStatus statusSetter = new MapStatus(SetterID);
            statusSetter.LoadFromData();
            MapIDState indexState = statusSetter.StatusStates.GetWithDefault<MapIDState>();
            indexState.ID = chosenStatus;
            map.Map.Status.Add(SetterID, statusSetter);
        }


        public override string ToString()
        {
            if (DefaultMapStatus.Length == 1)
                return String.Format("{0}: {1}", this.GetType().GetFormattedTypeName(), DataManager.Instance.DataIndices[DataManager.DataType.MapStatus].Get(DefaultMapStatus[0]).Name.ToLocal());
            return String.Format("{0}[{1}]", this.GetType().GetFormattedTypeName(), DefaultMapStatus.Length);
        }
    }


    /// <summary>
    /// Adds a map status to the map, with the specified MapStatusStates
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class StateMapStatusStep<T> : GenStep<T> where T : BaseMapGenContext
    {
        [JsonConverter(typeof(MapStatusConverter))]
        public string MapStatus;
        public StateCollection<MapStatusState> States;

        public StateMapStatusStep()
        {
            States = new StateCollection<MapStatusState>();
        }
        public StateMapStatusStep(string mapStatus, MapStatusState state) : this()
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

    /// <summary>
    /// Sets music about the map.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class MapMusicStep<T> : GenStep<T> where T : BaseMapGenContext
    {
        /// <summary>
        /// The default map music.
        /// </summary>
        [Music(0)]
        public string Music;

        public MapMusicStep()
        {
            Music = "";
        }
        public MapMusicStep(string music)
        {
            Music = music;
        }

        public override void Apply(T map)
        {
            map.Map.Music = Music;
        }


        public override string ToString()
        {
            return String.Format("{0}: Song:{1}", this.GetType().GetFormattedTypeName(), Music);
        }
    }
}
