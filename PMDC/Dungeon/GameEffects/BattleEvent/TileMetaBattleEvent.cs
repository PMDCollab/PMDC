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
    // Battle events that affect tiles, including traps, terrain, and discovery

    /// <summary>
    /// Event that sets the ground tile with the specified trap 
    /// </summary>
    [Serializable]
    public class SetTrapEvent : BattleEvent
    {
        /// <summary>
        /// The trap being added 
        /// </summary>
        [JsonConverter(typeof(TileConverter))]
        [DataType(0, DataManager.DataType.Tile, false)]
        public string TrapID;

        public SetTrapEvent() { }
        public SetTrapEvent(string trapID)
        {
            TrapID = trapID;
        }
        protected SetTrapEvent(SetTrapEvent other)
        {
            TrapID = other.TrapID;
        }
        public override GameEvent Clone() { return new SetTrapEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Tile tile = ZoneManager.Instance.CurrentMap.GetTile(context.TargetTile);
            if (tile == null)
                yield break;

            if (((TerrainData)tile.Data.GetData()).BlockType == TerrainData.Mobility.Passable && String.IsNullOrEmpty(tile.Effect.ID))
            {
                tile.Effect = new EffectTile(TrapID, true, tile.Effect.TileLoc);
                tile.Effect.Owner = ZoneManager.Instance.CurrentMap.GetTileOwner(context.User);
            }
        }
    }

    /// <summary>
    /// Event that sets the ground tile with the specified trap at the character's location 
    /// </summary>
    [Serializable]
    public class CounterTrapEvent : BattleEvent
    {
        /// <summary>
        /// The trap being added 
        /// </summary>
        [JsonConverter(typeof(TileConverter))]
        [DataType(0, DataManager.DataType.Tile, false)]
        public string TrapID;

        /// <summary>
        /// The particle VFX 
        /// </summary>
        public FiniteEmitter Emitter;

        /// <summary>
        /// The sound effect of the VFX
        /// </summary>
        [Sound(0)]
        public string Sound;


        public CounterTrapEvent() { Emitter = new EmptyFiniteEmitter(); }
        public CounterTrapEvent(string trapID, FiniteEmitter emitter, string sound)
        {
            TrapID = trapID;
            Emitter = emitter;
            Sound = sound;
        }
        protected CounterTrapEvent(CounterTrapEvent other)
        {
            TrapID = other.TrapID;
            Emitter = (FiniteEmitter)other.Emitter.Clone();
            Sound = other.Sound;
        }
        public override GameEvent Clone() { return new CounterTrapEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (!Collision.InBounds(ZoneManager.Instance.CurrentMap.Width, ZoneManager.Instance.CurrentMap.Height, context.Target.CharLoc))
                yield break;

            bool dropped = false;
            Loc baseLoc = context.Target.CharLoc;
            foreach (Dir4 dir in DirExt.VALID_DIR4)
            {
                Loc endLoc = baseLoc + dir.GetLoc();
                Tile tile = ZoneManager.Instance.CurrentMap.Tiles[endLoc.X][endLoc.Y];
                if (((TerrainData)tile.Data.GetData()).BlockType == TerrainData.Mobility.Passable && String.IsNullOrEmpty(tile.Effect.ID))
                {
                    tile.Effect = new EffectTile(TrapID, true, endLoc);
                    tile.Effect.Owner = ZoneManager.Instance.CurrentMap.GetTileOwner(context.Target);

                    GameManager.Instance.BattleSE(Sound);
                    FiniteEmitter endEmitter = (FiniteEmitter)Emitter.Clone();
                    endEmitter.SetupEmit(endLoc * GraphicsManager.TileSize + new Loc(GraphicsManager.TileSize / 2), endLoc * GraphicsManager.TileSize + new Loc(GraphicsManager.TileSize / 2), context.Target.CharDir);
                    DungeonScene.Instance.CreateAnim(endEmitter, DrawLayer.NoDraw);
                    dropped = true;
                }
            }
            if (dropped)
            {
                TileData tileData = DataManager.Instance.GetTile(TrapID);
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_SPIKE_DROPPER").ToLocal(), context.Target.GetDisplayName(false), owner.GetDisplayName(), tileData.Name.ToLocal()));
            }
        }
    }

    /// <summary>
    /// Event that triggers the effects of the trap tile
    /// </summary>
    [Serializable]
    public class TriggerTrapEvent : BattleEvent
    {
        /// <summary>
        /// The trap to ignore triggering
        /// </summary>
        [DataType(0, DataManager.DataType.Tile, false)]
        public string ExceptID;

        public TriggerTrapEvent() { }
        public TriggerTrapEvent(string exceptID) { ExceptID = exceptID; }
        public TriggerTrapEvent(TriggerTrapEvent other)
        {
            ExceptID = other.ExceptID;
        }
        public override GameEvent Clone() { return new TriggerTrapEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Tile tile = ZoneManager.Instance.CurrentMap.GetTile(context.TargetTile);
            if (tile == null)
                yield break;

            if (!String.IsNullOrEmpty(tile.Effect.ID) && tile.Effect.ID != ExceptID)
            {
                TileData entry = DataManager.Instance.GetTile(tile.Effect.GetID());
                if (entry.StepType == TileData.TriggerType.Trap)
                {
                    SingleCharContext singleContext = new SingleCharContext(context.User);
                    yield return CoroutineManager.Instance.StartCoroutine(tile.Effect.InteractWithTile(singleContext));
                }
            }
        }
    }

    /// <summary>
    /// Event that makes the trap revealed
    /// </summary>
    [Serializable]
    public class RevealTrapEvent : BattleEvent
    {
        public override GameEvent Clone() { return new RevealTrapEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Tile tile = ZoneManager.Instance.CurrentMap.GetTile(context.TargetTile);
            if (tile == null)
                yield break;

            if (!String.IsNullOrEmpty(tile.Effect.ID))
                tile.Effect.Revealed = true;
        }
    }

    /// <summary>
    /// Event that removes the trap
    /// </summary>
    [Serializable]
    public class RemoveTrapEvent : BattleEvent
    {
        public override GameEvent Clone() { return new RemoveTrapEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Tile tile = ZoneManager.Instance.CurrentMap.GetTile(context.TargetTile);
            if (tile == null)
                yield break;

            if (!String.IsNullOrEmpty(tile.Effect.ID))
            {
                TileData entry = DataManager.Instance.GetTile(tile.Effect.GetID());
                if (entry.StepType == TileData.TriggerType.Trap)
                    tile.Effect = new EffectTile(tile.Effect.TileLoc);
            }
        }
    }


    /// <summary>
    /// Event that changes terrain of one type to another type.
    /// </summary>
    [Serializable]
    public class ChangeTerrainEvent : BattleEvent
    {
        [DataType(0, DataManager.DataType.Terrain, false)]
        public string TerrainFrom;

        [DataType(0, DataManager.DataType.Terrain, false)]
        public string TerrainTo;

        public ChangeTerrainEvent()
        {
            TerrainFrom = "";
            TerrainTo = "";
        }

        public ChangeTerrainEvent(string terrainFrom, string terrainTo)
        {
            TerrainFrom = "";
            TerrainTo = "";
        }
        protected ChangeTerrainEvent(ChangeTerrainEvent other)
        {
            TerrainFrom = other.TerrainFrom;
            TerrainTo = other.TerrainTo;
        }
        public override GameEvent Clone() { return new ChangeTerrainEvent(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Tile tile = ZoneManager.Instance.CurrentMap.GetTile(context.TargetTile);
            if (tile.ID != TerrainFrom)
                yield break;

            tile.Data = new TerrainTile(TerrainTo);
            int distance = 0;
            Loc startLoc = context.TargetTile - new Loc(distance + 2);
            Loc sizeLoc = new Loc((distance + 2) * 2 + 1);
            ZoneManager.Instance.CurrentMap.MapModified(startLoc, sizeLoc);
        }
    }


    [Serializable]
    public abstract class RemoveTerrainBaseEvent : BattleEvent
    {
        /// <summary>
        /// The remove terrain SFX
        /// </summary>
        [Sound(0)]
        public string RemoveSound;

        /// <summary>
        /// The particle VFX
        /// </summary>
        public FiniteEmitter RemoveAnim;

        public RemoveTerrainBaseEvent()
        {
            RemoveAnim = new EmptyFiniteEmitter();
        }
        public RemoveTerrainBaseEvent(string removeSound, FiniteEmitter removeAnim)
            : this()
        {
            RemoveSound = removeSound;
            RemoveAnim = removeAnim;
        }
        protected RemoveTerrainBaseEvent(RemoveTerrainBaseEvent other) : this()
        {
            RemoveSound = other.RemoveSound;
            RemoveAnim = (FiniteEmitter)other.RemoveAnim.Clone();
        }

        protected abstract bool ShouldRemove(Tile tile);

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Tile tile = ZoneManager.Instance.CurrentMap.GetTile(context.TargetTile);
            if (!ShouldRemove(tile))
                yield break;

            if (context.Target == null)
            {
                GameManager.Instance.BattleSE(RemoveSound);
                FiniteEmitter emitter = (FiniteEmitter)RemoveAnim.Clone();
                emitter.SetupEmit(context.TargetTile * GraphicsManager.TileSize + new Loc(GraphicsManager.TileSize / 2), context.TargetTile * GraphicsManager.TileSize + new Loc(GraphicsManager.TileSize / 2), context.User.CharDir);
                DungeonScene.Instance.CreateAnim(emitter, DrawLayer.NoDraw);
            }

            tile.Data = new TerrainTile(DataManager.Instance.GenFloor);
            int distance = 0;
            Loc startLoc = context.TargetTile - new Loc(distance + 2);
            Loc sizeLoc = new Loc((distance + 2) * 2 + 1);
            ZoneManager.Instance.CurrentMap.MapModified(startLoc, sizeLoc);
        }
    }

    /// <summary>
    /// Event that removes the specified terrain and replaces it with a floor tile, replacing it with a floor tile
    /// </summary>
    [Serializable]
    public class RemoveTerrainEvent : RemoveTerrainBaseEvent
    {
        /// <summary>
        /// The list of terrains that can be removed
        /// </summary>
        [JsonConverter(typeof(TerrainSetConverter))]
        public HashSet<string> TileTypes;

        public RemoveTerrainEvent()
        {
            TileTypes = new HashSet<string>();
        }
        public RemoveTerrainEvent(string removeSound, FiniteEmitter removeAnim, params string[] tileTypes)
            : base(removeSound, removeAnim)
        {
            TileTypes = new HashSet<string>();
            foreach (string tileType in tileTypes)
                TileTypes.Add(tileType);
        }
        protected RemoveTerrainEvent(RemoveTerrainEvent other) : base(other)
        {
            TileTypes = new HashSet<string>();
            foreach (string tileType in other.TileTypes)
                TileTypes.Add(tileType);
        }
        public override GameEvent Clone() { return new RemoveTerrainEvent(this); }


        protected override bool ShouldRemove(Tile tile)
        {
            if (tile == null)
                return false;
            return TileTypes.Contains(tile.Data.ID);
        }
    }

    /// <summary>
    /// Event that removes the terrain if it contains one of the specified TerrainStates, replacing it with a floor tile
    /// </summary>
    [Serializable]
    public class RemoveTerrainStateEvent : RemoveTerrainBaseEvent
    {
        [StringTypeConstraint(1, typeof(TerrainState))]
        public List<FlagType> States;

        public RemoveTerrainStateEvent()
        {
            States = new List<FlagType>();
        }

        public RemoveTerrainStateEvent(string removeSound, FiniteEmitter removeAnim, params FlagType[] flagTypes)
            : base(removeSound, removeAnim)
        {
            States = new List<FlagType>();
            States.AddRange(flagTypes);
        }
        protected RemoveTerrainStateEvent(RemoveTerrainStateEvent other) : base(other)
        {
            States = new List<FlagType>();
            States.AddRange(other.States);
        }
        public override GameEvent Clone() { return new RemoveTerrainStateEvent(this); }


        protected override bool ShouldRemove(Tile tile)
        {
            if (tile == null)
                return false;

            TerrainData terrain = DataManager.Instance.GetTerrain(tile.Data.ID);

            foreach (FlagType state in States)
            {
                if (terrain.TerrainStates.Contains(state.FullType))
                    return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Event that removes the specified terrain and the area around it, replacing it with a floor tile
    /// </summary>
    [Serializable]
    public class ShatterTerrainEvent : BattleEvent
    {
        [JsonConverter(typeof(TerrainSetConverter))]
        public HashSet<string> TileTypes;

        public ShatterTerrainEvent() { TileTypes = new HashSet<string>(); }
        public ShatterTerrainEvent(params string[] tileTypes)
            : this()
        {
            foreach (string tileType in tileTypes)
                TileTypes.Add(tileType);
        }
        protected ShatterTerrainEvent(ShatterTerrainEvent other)
        {
            TileTypes = new HashSet<string>();
            foreach (string tileType in other.TileTypes)
                TileTypes.Add(tileType);
        }
        public override GameEvent Clone() { return new ShatterTerrainEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Tile tile = ZoneManager.Instance.CurrentMap.GetTile(context.TargetTile);
            if (tile == null)
                yield break;

            if (!TileTypes.Contains(tile.Data.ID))
                yield break;

            if (context.Target == null)
            {
                GameManager.Instance.BattleSE("DUN_Rollout");
                SingleEmitter emitter = new SingleEmitter(new AnimData("Rock_Smash", 2));
                emitter.SetupEmit(context.TargetTile * GraphicsManager.TileSize + new Loc(GraphicsManager.TileSize / 2), context.TargetTile * GraphicsManager.TileSize + new Loc(GraphicsManager.TileSize / 2), context.User.CharDir);
                DungeonScene.Instance.CreateAnim(emitter, DrawLayer.NoDraw);
            }

            //destroy the wall
            tile.Data = new TerrainTile(DataManager.Instance.GenFloor);
            for (int ii = 0; ii < DirExt.DIR4_COUNT; ii++)
            {
                Loc moveLoc = context.TargetTile + ((Dir4)ii).GetLoc();
                Tile sideTile = ZoneManager.Instance.CurrentMap.GetTile(moveLoc);
                if (sideTile != null && TileTypes.Contains(sideTile.Data.ID))
                    sideTile.Data = new TerrainTile(DataManager.Instance.GenFloor);
            }

            int distance = 0;
            Loc startLoc = context.TargetTile - new Loc(distance + 3);
            Loc sizeLoc = new Loc((distance + 3) * 2 + 1);
            ZoneManager.Instance.CurrentMap.MapModified(startLoc, sizeLoc);
        }
    }



    /// <summary>
    /// Event that hints all unexplored locations on the map
    /// </summary>
    [Serializable]
    public class MapOutEvent : BattleEvent
    {
        public override GameEvent Clone() { return new MapOutEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Loc testTile = context.TargetTile;
            if (!ZoneManager.Instance.CurrentMap.GetLocInMapBounds(ref testTile))
                yield break;

            if (ZoneManager.Instance.CurrentMap.DiscoveryArray[testTile.X][testTile.Y] == Map.DiscoveryState.None)
                ZoneManager.Instance.CurrentMap.DiscoveryArray[testTile.X][testTile.Y] = Map.DiscoveryState.Hinted;

        }
    }

    /// <summary>
    /// Event that hints all unexplored locations on the map within the specified radius
    /// </summary>
    [Serializable]
    public class MapOutRadiusEvent : BattleEvent
    {
        /// <summary>
        /// The radius around the user to hint
        /// </summary>
        public int Radius;

        public MapOutRadiusEvent() { }
        public MapOutRadiusEvent(int radius)
        {
            Radius = radius;
        }
        protected MapOutRadiusEvent(MapOutRadiusEvent other)
        {
            Radius = other.Radius;
        }
        public override GameEvent Clone() { return new MapOutRadiusEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {

            for (int ii = 1; ii <= 25; ii++)
            {
                int limitSquared = Radius * Radius * ii * ii / 25 / 25;
                for (int xx = -Radius; xx <= Radius; xx++)
                {
                    for (int yy = -Radius; yy <= Radius; yy++)
                    {
                        Loc diff = new Loc(xx, yy);
                        if (diff.DistSquared() < limitSquared)
                        {
                            Loc loc = context.User.CharLoc + diff;
                            if (!ZoneManager.Instance.CurrentMap.GetLocInMapBounds(ref loc))
                                continue;
                            if (ZoneManager.Instance.CurrentMap.DiscoveryArray[loc.X][loc.Y] == Map.DiscoveryState.None)
                                ZoneManager.Instance.CurrentMap.DiscoveryArray[loc.X][loc.Y] = Map.DiscoveryState.Hinted;
                        }
                    }
                }
                yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(2));
            }

        }
    }
}

