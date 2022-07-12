using System;
using System.Collections.Generic;
using RogueEssence.Data;
using RogueEssence;
using RogueEssence.Dungeon;
using RogueEssence.Dev;
using RogueEssence.LevelGen;
using RogueElements;

namespace PMDC.Dungeon
{
    [Serializable]
    public class MapStatusCharEvent : MapStatusGivenEvent
    {
        public SingleCharEvent BaseEvent;

        public MapStatusCharEvent() { }
        public MapStatusCharEvent(SingleCharEvent effect)
        {
            BaseEvent = effect;
        }
        protected MapStatusCharEvent(MapStatusCharEvent other)
        {
            BaseEvent = (SingleCharEvent)other.BaseEvent.Clone();
        }
        public override GameEvent Clone() { return new MapStatusCharEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character, MapStatus status, bool msg)
        {
            if (status != owner || character == null)
                yield break;

            yield return CoroutineManager.Instance.StartCoroutine(BaseEvent.Apply(owner, ownerChar, character));
        }
    }



    [Serializable]
    public class WeatherFormeChangeEvent : MapStatusGivenEvent
    {
        public int ReqSpecies;
        public int DefaultForme;
        public Dictionary<int, int> WeatherPair;

        public WeatherFormeChangeEvent() { WeatherPair = new Dictionary<int, int>(); }
        public WeatherFormeChangeEvent(int reqSpecies, int defaultForme, Dictionary<int, int> weather)
        {
            ReqSpecies = reqSpecies;
            DefaultForme = defaultForme;
            WeatherPair = weather;
        }
        protected WeatherFormeChangeEvent(WeatherFormeChangeEvent other) : this()
        {
            ReqSpecies = other.ReqSpecies;
            DefaultForme = other.DefaultForme;

            foreach (int weather in other.WeatherPair.Keys)
                WeatherPair.Add(weather, other.WeatherPair[weather]);
        }
        public override GameEvent Clone() { return new WeatherFormeChangeEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character, MapStatus status, bool msg)
        {
            if (character == null)
                yield break;

            if (character.CurrentForm.Species != ReqSpecies)
                yield break;
            
            //get the forme it should be in
            int forme = DefaultForme;

            foreach (int weather in WeatherPair.Keys)
            {
                if (ZoneManager.Instance.CurrentMap.Status.ContainsKey(weather))
                {
                    forme = WeatherPair[weather];
                    break;
                }
            }

            if (forme != character.CurrentForm.Form)
            {
                //transform it
                character.Transform(new MonsterID(character.CurrentForm.Species, forme, character.CurrentForm.Skin, character.CurrentForm.Gender));
                DungeonScene.Instance.LogMsg(String.Format(new StringKey("MSG_FORM_CHANGE").ToLocal(), character.GetDisplayName(false)));
            }

            yield break;
        }
    }



    [Serializable]
    public class ReplaceStatusGroupEvent : MapStatusGivenEvent
    {
        [StringTypeConstraint(1, typeof(MapStatusState))]
        public List<FlagType> States;
        public bool Msg;

        public ReplaceStatusGroupEvent() { States = new List<FlagType>(); }
        public ReplaceStatusGroupEvent(Type state) : this() { States.Add(new FlagType(state)); }
        public ReplaceStatusGroupEvent(Type state, bool msg) : this() { States.Add(new FlagType(state)); Msg = msg; }
        protected ReplaceStatusGroupEvent(ReplaceStatusGroupEvent other) : this()
        {
            States.AddRange(other.States);
            Msg = other.Msg;
        }
        public override GameEvent Clone() { return new ReplaceStatusGroupEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character, MapStatus status, bool msg)
        {
            //the owner must not be the newly added status
            //TODO: String Assets owner.GetID()
            if (status.ID != ((MapStatus)owner).ID || character != null)
                yield break;

            //remove all other weather effects
            List<int> removingIDs = new List<int>();
            foreach (MapStatus removeStatus in ZoneManager.Instance.CurrentMap.Status.Values)
            {
                bool hasState = false;
                foreach (FlagType state in States)
                {
                    if (removeStatus.StatusStates.Contains(state.FullType))
                        hasState = true;
                }
                //TODO: String Assets owner.GetID()
                if (hasState && removeStatus.ID != ((MapStatus)owner).ID)
                    removingIDs.Add(removeStatus.ID);
            }
            foreach (int removeID in removingIDs)
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.RemoveMapStatus(removeID, Msg && msg));
            yield break;
        }

    }


    [Serializable]
    public class MapStatusBattleLogEvent : MapStatusGivenEvent
    {

        public StringKey Message;
        public bool Delay;

        public MapStatusBattleLogEvent() { }
        public MapStatusBattleLogEvent(StringKey message) : this(message, false) { }
        public MapStatusBattleLogEvent(StringKey message, bool delay)
        {
            Message = message;
            Delay = delay;
        }
        protected MapStatusBattleLogEvent(MapStatusBattleLogEvent other)
        {
            Message = other.Message;
            Delay = other.Delay;
        }
        public override GameEvent Clone() { return new MapStatusBattleLogEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character, MapStatus status, bool msg)
        {
            if (status != owner || character != null)
                yield break;

            if (msg)
            {
                DungeonScene.Instance.LogMsg(Message.ToLocal());
                if (Delay)
                    yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(10));
            }
        }
    }

    [Serializable]
    public class MapStatusMoveLogEvent : MapStatusGivenEvent
    {
        public StringKey Message;

        public MapStatusMoveLogEvent() { }
        public MapStatusMoveLogEvent(StringKey message)
        {
            Message = message;
        }
        protected MapStatusMoveLogEvent(MapStatusMoveLogEvent other)
        {
            Message = other.Message;
        }
        public override GameEvent Clone() { return new MapStatusMoveLogEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character, MapStatus status, bool msg)
        {
            if (status != owner || character != null)
                yield break;

            if (msg)
            {
                SkillData entry = DataManager.Instance.GetSkill(status.StatusStates.GetWithDefault<MapIndexState>().Index);
                DungeonScene.Instance.LogMsg(String.Format(Message.ToLocal(), entry.GetIconName()));
                yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(10));
            }
        }
    }

    [Serializable]
    public class MapStatusSoundEvent : MapStatusGivenEvent
    {
        [Sound(0)]
        public string Sound;

        public MapStatusSoundEvent() { }
        public MapStatusSoundEvent(string sound)
        {
            Sound = sound;
        }
        protected MapStatusSoundEvent(MapStatusSoundEvent other)
        {
            Sound = other.Sound;
        }
        public override GameEvent Clone() { return new MapStatusSoundEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character, MapStatus status, bool msg)
        {
            if (status != owner || character != null)
                yield break;

            GameManager.Instance.BattleSE(Sound);
            yield break;
        }
    }

    [Serializable]
    public class MapStatusVisibleIfCountdownEvent : MapStatusGivenEvent
    {
        public MapStatusVisibleIfCountdownEvent() { }
        public override GameEvent Clone() { return new MapStatusVisibleIfCountdownEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character, MapStatus status, bool msg)
        {
            if (status != owner || character != null)
                yield break;

            if (status.StatusStates.GetWithDefault<MapCountDownState>().Counter > -1)
                status.Hidden = false;

            yield break;
        }
    }

    [Serializable]
    public class MapStatusSpawnStartGuardsEvent : MapStatusGivenEvent
    {
        public int GuardStatus;
        public MapStatusSpawnStartGuardsEvent() { }
        public MapStatusSpawnStartGuardsEvent(int guardStatus) { GuardStatus = guardStatus; }
        public MapStatusSpawnStartGuardsEvent(MapStatusSpawnStartGuardsEvent other) { GuardStatus = other.GuardStatus; }
        public override GameEvent Clone() { return new MapStatusSpawnStartGuardsEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character, MapStatus status, bool msg)
        {
            if (status != owner || character != null)
                yield break;

            ////remove existing spawns
            //ZoneManager.Instance.CurrentMap.TeamSpawns.Clear();

            ShopSecurityState securityState = status.StatusStates.Get<ShopSecurityState>();

            ////add guard spawns
            //for (int ii = 0; ii < securityState.Security.Count; ii++)
            //{
            //    SpecificTeamSpawner post_team = new SpecificTeamSpawner(securityState.Security.GetSpawn(ii).Copy());
            //    ZoneManager.Instance.CurrentMap.TeamSpawns.Add(post_team, securityState.Security.GetSpawnRate(ii));
            //}

            //set spawn rate
            ZoneManager.Instance.CurrentMap.RespawnTime = 0;

            //set spawn max
            ZoneManager.Instance.CurrentMap.MaxFoes = 0;

            //spawn 10 times
            List<Loc> randLocs = ZoneManager.Instance.CurrentMap.GetFreeToSpawnTiles();
            for (int ii = 0; ii < 10; ii++)
            {
                if (randLocs.Count == 0)
                    break;

                int randIndex = DataManager.Instance.Save.Rand.Next(randLocs.Count);
                Loc dest = randLocs[randIndex];
                MobSpawn spawn = securityState.Security.Pick(DataManager.Instance.Save.Rand);
                yield return CoroutineManager.Instance.StartCoroutine(PeriodicSpawnEntranceGuards.PlaceGuard(spawn, dest, GuardStatus));
                randLocs.RemoveAt(randIndex);
            }

            List<Loc> exitLocs = WarpToEndEvent.FindExits();
            //spawn once specifically on the stairs
            foreach(Loc exitLoc in exitLocs)
            {
                Loc? dest = ZoneManager.Instance.CurrentMap.GetClosestTileForChar(null, exitLoc);
                if (!dest.HasValue)
                    continue;

                MobSpawn spawn = securityState.Security.Pick(DataManager.Instance.Save.Rand);
                yield return CoroutineManager.Instance.StartCoroutine(PeriodicSpawnEntranceGuards.PlaceGuard(spawn, dest.Value, GuardStatus));
            }
        }
    }


    [Serializable]
    public class MapStatusBGMEvent : MapStatusGivenEvent
    {
        [Music(0)]
        public string BGM;

        public MapStatusBGMEvent() { }
        public MapStatusBGMEvent(string bgm)
        {
            BGM = bgm;
        }
        protected MapStatusBGMEvent(MapStatusBGMEvent other)
        {
            BGM = other.BGM;
        }
        public override GameEvent Clone() { return new MapStatusBGMEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character, MapStatus status, bool msg)
        {
            if (status != owner || character != null)
                yield break;

            GameManager.Instance.BGM(BGM, true);
            yield break;
        }
    }

    [Serializable]
    public class MapStatusCombineCheckEvent : MapStatusGivenEvent
    {
        public MapStatusCombineCheckEvent() { }
        public override GameEvent Clone() { return new MapStatusCombineCheckEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character, MapStatus status, bool msg)
        {
            if (character != null)
                yield break;

            MapCheckState destChecks = ((MapStatus)owner).StatusStates.GetWithDefault<MapCheckState>();
            MapCheckState srcChecks = status.StatusStates.GetWithDefault<MapCheckState>();
            foreach (SingleCharEvent effect in srcChecks.CheckEvents)
                destChecks.CheckEvents.Add(effect);
        }
    }


    [Serializable]
    public class MapStatusRefreshEvent : MapStatusGivenEvent
    {
        public MapStatusRefreshEvent() { }
        public override GameEvent Clone() { return new MapStatusRefreshEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character, MapStatus status, bool msg)
        {
            if (character != null)
                yield break;

            if (((MapStatus)owner).StatusStates.GetWithDefault<MapCountDownState>().Counter > -1 &&
                ((MapStatus)owner).StatusStates.GetWithDefault<MapCountDownState>().Counter < status.StatusStates.GetWithDefault<MapCountDownState>().Counter)
                ((MapStatus)owner).StatusStates.GetWithDefault<MapCountDownState>().Counter = status.StatusStates.GetWithDefault<MapCountDownState>().Counter;
        }
    }

    [Serializable]
    public class MapStatusToggleEvent : MapStatusGivenEvent
    {
        public MapStatusToggleEvent() { }
        public override GameEvent Clone() { return new MapStatusToggleEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character, MapStatus status, bool msg)
        {
            if (character != null)
                yield break;

            yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.RemoveMapStatus(((MapStatus)owner).ID));
        }
    }

    [Serializable]
    public class MapStatusReplaceEvent : MapStatusGivenEvent
    {
        public MapStatusReplaceEvent() { }
        public override GameEvent Clone() { return new MapStatusReplaceEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character, MapStatus status, bool msg)
        {
            if (character != null)
                yield break;

            yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.RemoveMapStatus(status.ID));
            ZoneManager.Instance.CurrentMap.Status.Add(status.ID, status);
            status.StartEmitter(DungeonScene.Instance.Anims);
        }
    }


    [Serializable]
    public class MapStatusIgnoreEvent : MapStatusGivenEvent
    {
        public MapStatusIgnoreEvent() { }
        public override GameEvent Clone() { return new MapStatusIgnoreEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character, MapStatus status, bool msg)
        {
            yield break;
        }
    }




    [Serializable]
    public abstract class ShareEquipMapStatusEvent : MapStatusGivenEvent
    {
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character, MapStatus status, bool msg)
        {
            if (ownerChar.EquippedItem.ID > -1)
            {
                ItemData entry = (ItemData)ownerChar.EquippedItem.GetData();
                if (CheckEquipPassValidityEvent.CanItemEffectBePassed(entry))
                {
                    foreach (var effect in GetEvents(entry))
                        yield return CoroutineManager.Instance.StartCoroutine(effect.Value.Apply(owner, ownerChar, character, status, msg));
                }
            }
        }

        protected abstract PriorityList<MapStatusGivenEvent> GetEvents(ItemData entry);
    }

    [Serializable]
    public class ShareOnMapStatusAddsEvent : ShareEquipMapStatusEvent
    {
        public override GameEvent Clone() { return new ShareOnMapStatusAddsEvent(); }

        protected override PriorityList<MapStatusGivenEvent> GetEvents(ItemData entry) => entry.OnMapStatusAdds;
    }

    [Serializable]
    public class ShareOnMapStatusRemovesEvent : ShareEquipMapStatusEvent
    {
        public override GameEvent Clone() { return new ShareOnMapStatusRemovesEvent(); }

        protected override PriorityList<MapStatusGivenEvent> GetEvents(ItemData entry) => entry.OnMapStatusRemoves;
    }
}