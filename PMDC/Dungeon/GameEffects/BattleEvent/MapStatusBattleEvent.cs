using System;
using System.Collections.Generic;
using RogueEssence.Data;
using RogueEssence.Menu;
using RogueElements;
using RogueEssence.Content;
using RogueEssence.LevelGen;
using RogueEssence;
using RogueEssence.Dungeon;
using RogueEssence.Dev;
using PMDC.Dev;
using PMDC.Data;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using NLua;
using RogueEssence.Script;
using System.Linq;

namespace PMDC.Dungeon
{
    // Battle events related to map statuses



    /// <summary>
    /// Event that sets the specified map status
    /// </summary>
    [Serializable]
    public class GiveMapStatusEvent : BattleEvent
    {
        /// <summary>
        /// The map status to add
        /// </summary>
        [JsonConverter(typeof(MapStatusConverter))]
        [DataType(0, DataManager.DataType.MapStatus, false)]
        public string StatusID;

        /// <summary>
        /// The amount of turns the map status will last
        /// </summary>
        public int Counter;

        /// <summary>
        /// The message displayed in the dungeon log when the map status is added
        /// </summary>
        [StringKey(0, true)]
        public StringKey MsgOverride;

        /// <summary>
        /// If the user contains one of the specified CharStates, then the weather is extended by the multiplier
        /// </summary>
        [StringTypeConstraint(1, typeof(CharState))]
        public List<FlagType> States;

        public GiveMapStatusEvent() { States = new List<FlagType>(); StatusID = ""; }
        public GiveMapStatusEvent(string id)
        {
            States = new List<FlagType>();
            StatusID = id;
        }
        public GiveMapStatusEvent(string id, int counter)
        {
            States = new List<FlagType>();
            StatusID = id;
            Counter = counter;
        }
        public GiveMapStatusEvent(string id, int counter, StringKey msg)
        {
            States = new List<FlagType>();
            StatusID = id;
            Counter = counter;
            MsgOverride = msg;
        }
        public GiveMapStatusEvent(string id, int counter, StringKey msg, Type state)
        {
            States = new List<FlagType>();
            StatusID = id;
            Counter = counter;
            MsgOverride = msg;
            States.Add(new FlagType(state));
        }
        protected GiveMapStatusEvent(GiveMapStatusEvent other)
            : this()
        {
            StatusID = other.StatusID;
            Counter = other.Counter;
            MsgOverride = other.MsgOverride;
            States.AddRange(other.States);
        }
        public override GameEvent Clone() { return new GiveMapStatusEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            //add the map status
            MapStatus status = new MapStatus(StatusID);
            status.LoadFromData();
            if (Counter != 0)
                status.StatusStates.GetWithDefault<MapCountDownState>().Counter = Counter;

            bool hasState = false;
            foreach (FlagType state in States)
            {
                if (context.User.CharStates.Contains(state.FullType))
                    hasState = true;
            }
            if (hasState)
                status.StatusStates.GetWithDefault<MapCountDownState>().Counter = status.StatusStates.GetWithDefault<MapCountDownState>().Counter * 5;

            if (!MsgOverride.IsValid())
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.AddMapStatus(status));
            else
            {
                //message only if the status isn't already there
                MapStatus statusToCheck;
                if (!ZoneManager.Instance.CurrentMap.Status.TryGetValue(status.ID, out statusToCheck))
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(MsgOverride.ToLocal(), ownerChar.GetDisplayName(false), owner.GetDisplayName()));
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.AddMapStatus(status, false));
            }
        }
    }


    /// <summary>
    /// Event that removes all the map statuses 
    /// </summary>
    [Serializable]
    public class RemoveWeatherEvent : BattleEvent
    {
        public override GameEvent Clone() { return new RemoveWeatherEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            //remove all other weather effects
            List<string> removingIDs = new List<string>();
            foreach (MapStatus removeStatus in ZoneManager.Instance.CurrentMap.Status.Values)
            {
                if (removeStatus.StatusStates.Contains<MapWeatherState>())
                    removingIDs.Add(removeStatus.ID);
            }
            foreach (string removeID in removingIDs)
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.RemoveMapStatus(removeID));
        }
    }

    /// <summary>
    /// Event that sets the map status depending on the user's type 
    /// </summary>
    [Serializable]
    public class TypeWeatherEvent : BattleEvent
    {
        /// <summary>
        /// The element that maps to a map status. 
        /// </summary>
        [JsonConverter(typeof(ElementMapStatusDictConverter))]
        [DataType(1, DataManager.DataType.Element, false)]
        [DataType(2, DataManager.DataType.MapStatus, false)]
        public Dictionary<string, string> WeatherPair;

        public TypeWeatherEvent() { WeatherPair = new Dictionary<string, string>(); }
        public TypeWeatherEvent(Dictionary<string, string> weather)
        {
            WeatherPair = weather;
        }
        protected TypeWeatherEvent(TypeWeatherEvent other)
            : this()
        {
            foreach (string element in other.WeatherPair.Keys)
                WeatherPair.Add(element, other.WeatherPair[element]);
        }
        public override GameEvent Clone() { return new TypeWeatherEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            string weather;
            if (WeatherPair.TryGetValue(context.User.Element1, out weather))
            {
                //add the map status
                MapStatus status = new MapStatus(weather);
                status.LoadFromData();
                status.StatusStates.GetWithDefault<MapCountDownState>().Counter = -1;
                ElementData elementData = DataManager.Instance.GetElement(context.User.Element1);
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_ELEMENT_WEATHER").ToLocal(), context.User.GetDisplayName(false), elementData.GetIconName(), ((MapStatusData)status.GetData()).GetColoredName()));
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.AddMapStatus(status));
            }
            else if (WeatherPair.TryGetValue(context.User.Element2, out weather))
            {
                //add the map status
                MapStatus status = new MapStatus(weather);
                status.LoadFromData();
                status.StatusStates.GetWithDefault<MapCountDownState>().Counter = -1;
                ElementData elementData = DataManager.Instance.GetElement(context.User.Element2);
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_ELEMENT_WEATHER").ToLocal(), context.User.GetDisplayName(false), elementData.GetIconName(), ((MapStatusData)status.GetData()).GetColoredName()));
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.AddMapStatus(status));
            }
            else//clear weather
            {
                //add the map status
                MapStatus status = new MapStatus(DataManager.Instance.DefaultMapStatus);
                status.LoadFromData();
                status.StatusStates.GetWithDefault<MapCountDownState>().Counter = -1;
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.AddMapStatus(status));
            }
        }
    }

    /// <summary>
    /// Event that bans the last move the character used by setting the move ID in the MapIDState
    /// </summary>
    [Serializable]
    public class BanMoveEvent : BattleEvent
    {
        /// <summary>
        /// The status that will store the move ID in MapIDState
        /// This should usually be "move_ban" 
        /// </summary>
        [JsonConverter(typeof(MapStatusConverter))]
        [DataType(0, DataManager.DataType.MapStatus, false)]
        public string BanStatusID;

        /// <summary>
        /// The status that contains the last used move in IDState status state
        /// This should usually be "last_used_move"
        /// </summary>
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string LastMoveStatusID;

        public BanMoveEvent() { BanStatusID = ""; LastMoveStatusID = ""; }
        public BanMoveEvent(string banStatusID, string prevMoveID)
        {
            BanStatusID = banStatusID;
            LastMoveStatusID = prevMoveID;
        }
        protected BanMoveEvent(BanMoveEvent other)
        {
            BanStatusID = other.BanStatusID;
            LastMoveStatusID = other.LastMoveStatusID;
        }
        public override GameEvent Clone() { return new BanMoveEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            StatusEffect testStatus = context.Target.GetStatusEffect(LastMoveStatusID);
            if (testStatus != null)
            {
                //add disable move based on the last move used
                string lockedMove = testStatus.StatusStates.GetWithDefault<IDState>().ID;
                //add the map status
                MapStatus status = new MapStatus(BanStatusID);
                status.LoadFromData();
                status.StatusStates.GetWithDefault<MapIDState>().ID = lockedMove;
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.AddMapStatus(status));
            }
            else
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_BAN_FAIL").ToLocal(), context.Target.GetDisplayName(false)));
        }
    }


}

