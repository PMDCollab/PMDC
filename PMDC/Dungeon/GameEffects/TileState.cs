using System;
using RogueEssence.LevelGen;
using System.Collections.Generic;
using RogueElements;
using RogueEssence;
using RogueEssence.Dev;
using RogueEssence.Dungeon;
using Newtonsoft.Json;

namespace PMDC.Dungeon
{

    [Serializable]
    public class TriggeringState : TileState
    {
        public TriggeringState() { }
        public override GameplayState Clone() { return new TriggeringState(); }
    }
    [Serializable]
    public class DestState : TileState
    {
        public SegLoc Dest;
        public bool Relative;
        public bool PreserveMusic;
        public DestState() { }
        public DestState(SegLoc dest) { Dest = dest; }
        public DestState(SegLoc dest, bool relative) { Dest = dest; Relative = relative; }
        protected DestState(DestState other) { Dest = other.Dest; Relative = other.Relative; PreserveMusic = other.PreserveMusic; }
        public override GameplayState Clone() { return new DestState(this); }
    }
    [Serializable]
    public class DangerState : TileState
    {
        public bool Danger;
        public DangerState() { }
        public DangerState(bool danger) { Danger = danger; }
        protected DangerState(DangerState other) { Danger = other.Danger; }
        public override GameplayState Clone() { return new DangerState(this); }
    }
    [Serializable]
    public class SongState : TileState
    {
        [Music(0)]
        public string Song;
        public SongState() { }
        public SongState(string song) { Song = song; }
        protected SongState(SongState other) { Song = other.Song; }
        public override GameplayState Clone() { return new SongState(this); }
    }
    [Serializable]
    public class UnlockState : TileState
    {
        [JsonConverter(typeof(ItemConverter))]
        [DataType(0, RogueEssence.Data.DataManager.DataType.Item, false)]
        public string UnlockItem;
        public UnlockState() { UnlockItem = ""; }
        public UnlockState(string unlockItem) { UnlockItem = unlockItem; }
        protected UnlockState(UnlockState other) { UnlockItem = other.UnlockItem; }
        public override GameplayState Clone() { return new UnlockState(this); }
    }
    [Serializable]
    public class NoticeState : TileState
    {
        public LocalFormat Title;
        public LocalFormat Content;
        public NoticeState() { Title = new LocalFormatSimple(); Content = new LocalFormatSimple(); }
        public NoticeState(LocalFormat content) { Title = new LocalFormatSimple(); Content = content; }
        public NoticeState(LocalFormat title, LocalFormat content) { Title = title; Content = content; }
        protected NoticeState(NoticeState other) { Title = other.Title.Clone(); Content = other.Content.Clone(); }
        public override GameplayState Clone() { return new NoticeState(this); }
    }
    [Serializable]
    public class TileListState : TileState
    {
        public List<Loc> Tiles;
        public TileListState() { Tiles = new List<Loc>(); }
        protected TileListState(TileListState other)
            : this()
        {
            foreach (Loc item in other.Tiles)
                Tiles.Add(item);
        }
        public override GameplayState Clone() { return new TileListState(this); }
    }
    [Serializable]
    public class TileReqListState : TileState
    {
        public List<Loc> Tiles;
        public TileReqListState() { Tiles = new List<Loc>(); }
        protected TileReqListState(TileReqListState other)
            : this()
        {
            foreach (Loc item in other.Tiles)
                Tiles.Add(item);
        }
        public override GameplayState Clone() { return new TileReqListState(this); }
    }
    [Serializable]
    public class ItemSpawnState : TileState
    {
        public List<MapItem> Spawns;
        public ItemSpawnState() { Spawns = new List<MapItem>(); }
        protected ItemSpawnState(ItemSpawnState other)
            : this()
        {
            foreach (MapItem item in other.Spawns)
                Spawns.Add(new MapItem(item));
        }
        public override GameplayState Clone() { return new ItemSpawnState(this); }
    }
    [Serializable]
    public class MobSpawnState : TileState
    {
        public List<MobSpawn> Spawns;
        public MobSpawnState() { Spawns = new List<MobSpawn>(); }
        protected MobSpawnState(MobSpawnState other)
            : this()
        {
            foreach (MobSpawn mob in other.Spawns)
                Spawns.Add(mob.Copy());
        }
        public override GameplayState Clone() { return new MobSpawnState(this); }
    }
    [Serializable]
    public class MapStartEventState : TileState
    {
        public PriorityList<SingleCharEvent> OnMapStarts;
        public MapStartEventState() { OnMapStarts = new PriorityList<SingleCharEvent>(); }
        protected MapStartEventState(MapStartEventState other)
            : this()
        {
            foreach (Priority priority in other.OnMapStarts.GetPriorities())
            {
                foreach (SingleCharEvent step in other.OnMapStarts.GetItems(priority))
                    OnMapStarts.Add(priority, step);
            }
        }
        public override GameplayState Clone() { return new MapStartEventState(this); }
    }
    [Serializable]
    public class ResultEventState : TileState
    {
        public List<SingleCharEvent> ResultEvents;
        public ResultEventState() { ResultEvents = new List<SingleCharEvent>(); }
        protected ResultEventState(ResultEventState other)
            : this()
        {
            foreach (SingleCharEvent mob in other.ResultEvents)
                ResultEvents.Add((SingleCharEvent)mob.Clone());
        }
        public override GameplayState Clone() { return new ResultEventState(this); }
    }
    [Serializable]
    public class BoundsState : TileState
    {
        public Rect Bounds;
        public BoundsState() { }
        public BoundsState(Rect bounds) { Bounds = bounds; }
        protected BoundsState(BoundsState other) { Bounds = other.Bounds; }
        public override GameplayState Clone() { return new BoundsState(this); }
    }

    [Serializable]
    public class TileScriptState : TileState
    {
        [RogueEssence.Dev.Sanitize(0)]
        public string Script;
        public string ArgTable;
        public TileScriptState() { Script = ""; ArgTable = "{}"; }
        public TileScriptState(string script, string argTable) { Script = script; ArgTable = argTable; }
        protected TileScriptState(TileScriptState other) { Script = other.Script; ArgTable = other.ArgTable; }
        public override GameplayState Clone() { return new TileScriptState(this); }
    }
}
