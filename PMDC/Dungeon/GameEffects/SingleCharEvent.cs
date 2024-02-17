using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence.Data;
using RogueEssence.LevelGen;
using RogueEssence.Content;
using RogueEssence.Menu;
using RogueEssence.Ground;
using RogueEssence;
using RogueEssence.Dungeon;
using RogueEssence.Dev;
using PMDC.Data;
using NLua;
using RogueEssence.Script;
using System.Linq;
using Newtonsoft.Json;
using System.Runtime.Serialization;
using System.IO;
using PMDC.LevelGen;

namespace PMDC.Dungeon
{
    [Serializable]
    public class CountDownEvent : SingleCharEvent
    {
        public List<SingleCharEvent> Effects;

        public CountDownEvent() { Effects = new List<SingleCharEvent>(); }
        public CountDownEvent(List<SingleCharEvent> effects)
        {
            Effects = effects;
        }
        protected CountDownEvent(CountDownEvent other)
        {
            Effects = new List<SingleCharEvent>();
            foreach (SingleCharEvent effect in other.Effects)
                Effects.Add((SingleCharEvent)effect.Clone());
        }
        public override GameEvent Clone() { return new CountDownEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            ((StatusEffect)owner).StatusStates.GetWithDefault<CountDownState>().Counter--;
            if (((StatusEffect)owner).StatusStates.GetWithDefault<CountDownState>().Counter <= 0)
            {
                for (int ii = 0; ii < Effects.Count; ii++)
                    yield return CoroutineManager.Instance.StartCoroutine(Effects[ii].Apply(owner, ownerChar, context));
            }
        }
    }

    [Serializable]
    public class WaitAnimsOverEvent : SingleCharEvent
    {
        public override GameEvent Clone() { return new WaitAnimsOverEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            yield return new WaitUntil(DungeonScene.Instance.AnimationsOver);
        }
    }

    [Serializable]
    public class RespawnFromRandomEvent : RespawnBaseEvent
    {
        /// <summary>
        /// The radius from the player characters from which to spawn.
        /// </summary>
        public int Radius;

        public RespawnFromRandomEvent() { }
        public RespawnFromRandomEvent(int maxFoes, int respawnTime) : base(maxFoes, respawnTime)
        { }
        protected RespawnFromRandomEvent(RespawnFromRandomEvent other) : base(other)
        { Radius = other.Radius; }
        public override GameEvent Clone() { return new RespawnFromRandomEvent(this); }

        public override List<Character> RespawnMob()
        {
            List<Character> respawns = new List<Character>();
            if (ZoneManager.Instance.CurrentMap.TeamSpawns.CanPick)
            {
                for (int ii = 0; ii < 10; ii++)
                {
                    Team newTeam = ZoneManager.Instance.CurrentMap.TeamSpawns.Pick(ZoneManager.Instance.CurrentMap.Rand).Spawn(ZoneManager.Instance.CurrentMap);
                    if (newTeam == null)
                        continue;

                    Loc trialLoc;
                    if (Radius <= 0)
                        trialLoc = new Loc(ZoneManager.Instance.CurrentMap.Rand.Next(ZoneManager.Instance.CurrentMap.Width), ZoneManager.Instance.CurrentMap.Rand.Next(ZoneManager.Instance.CurrentMap.Height));
                    else
                    {
                        // choose a random location period within radius of the player
                        Character centerChara = ZoneManager.Instance.CurrentMap.ActiveTeam.Leader;
                        trialLoc = new Loc(ZoneManager.Instance.CurrentMap.Rand.Next(centerChara.CharLoc.X - Radius, centerChara.CharLoc.X + Radius), ZoneManager.Instance.CurrentMap.Rand.Next(centerChara.CharLoc.Y - Radius, centerChara.CharLoc.Y + Radius));

                        //make this wrap-friendly
                        if (!ZoneManager.Instance.CurrentMap.GetLocInMapBounds(ref trialLoc))
                            trialLoc = Collision.ClampToBounds(ZoneManager.Instance.CurrentMap.Width, ZoneManager.Instance.CurrentMap.Height, trialLoc);

                    }
                    //but not too close

                    //find a way to place all members- needs to fit all of them in, or else fail the spawn
                    Grid.LocTest checkOpen = (Loc testLoc) =>
                    {
                        if (ZoneManager.Instance.CurrentMap.TileBlocked(testLoc))
                            return false;

                        foreach (Character character in ZoneManager.Instance.CurrentMap.ActiveTeam.Players)
                        {
                            if (character.IsInSightBounds(testLoc))
                                return false;
                        }

                        Character locChar = ZoneManager.Instance.CurrentMap.GetCharAtLoc(testLoc);
                        if (locChar != null)
                            return false;
                        return true;
                    };
                    Grid.LocTest checkBlock = (Loc testLoc) =>
                    {
                        return ZoneManager.Instance.CurrentMap.TileBlocked(testLoc, true);
                    };
                    Grid.LocTest checkDiagBlock = (Loc testLoc) =>
                    {
                        return ZoneManager.Instance.CurrentMap.TileBlocked(testLoc, true, true);
                    };

                    List<Loc> resultLocs = new List<Loc>();
                    foreach (Loc loc in Grid.FindClosestConnectedTiles(new Loc(), new Loc(ZoneManager.Instance.CurrentMap.Width, ZoneManager.Instance.CurrentMap.Height),
                        checkOpen, checkBlock, checkDiagBlock, trialLoc, newTeam.Players.Count))
                    {
                        resultLocs.Add(loc);
                    }

                    if (resultLocs.Count >= newTeam.Players.Count + newTeam.Guests.Count)
                    {
                        for (int jj = 0; jj < newTeam.Players.Count; jj++)
                            newTeam.Players[jj].CharLoc = resultLocs[jj];
                        for (int jj = 0; jj < newTeam.Guests.Count; jj++)
                            newTeam.Guests[jj].CharLoc = resultLocs[newTeam.Players.Count + jj];

                        ZoneManager.Instance.CurrentMap.MapTeams.Add(newTeam);

                        foreach (Character member in newTeam.EnumerateChars())
                        {
                            member.RefreshTraits();
                            respawns.Add(member);
                        }
                        break;
                    }
                }
            }
            return respawns;
        }
    }


    [Serializable]
    public class RespawnFromEligibleEvent : RespawnBaseEvent
    {

        public RespawnFromEligibleEvent() { }
        public RespawnFromEligibleEvent(int maxFoes, int respawnTime) : base(maxFoes, respawnTime)
        { }
        protected RespawnFromEligibleEvent(RespawnFromEligibleEvent other) : base(other)
        { }
        public override GameEvent Clone() { return new RespawnFromEligibleEvent(this); }

        public override List<Character> RespawnMob()
        {
            return Respawn();
        }

        public static List<Character> Respawn()
        {
            List<Character> respawns = new List<Character>();
            if (ZoneManager.Instance.CurrentMap.TeamSpawns.CanPick)
            {
                List<Loc> freeTiles = ZoneManager.Instance.CurrentMap.GetFreeToSpawnTiles();
                if (freeTiles.Count > 0)
                {
                    for (int ii = 0; ii < 10; ii++)
                    {
                        Team newTeam = ZoneManager.Instance.CurrentMap.TeamSpawns.Pick(ZoneManager.Instance.CurrentMap.Rand).Spawn(ZoneManager.Instance.CurrentMap);
                        if (newTeam == null)
                            continue;
                        Loc trialLoc = freeTiles[ZoneManager.Instance.CurrentMap.Rand.Next(freeTiles.Count)];
                        //find a way to place all members- needs to fit all of them in, or else fail the spawn

                        Grid.LocTest checkOpen = (Loc testLoc) =>
                        {
                            if (ZoneManager.Instance.CurrentMap.TileBlocked(testLoc))
                                return false;

                            foreach (Character character in ZoneManager.Instance.CurrentMap.ActiveTeam.Players)
                            {
                                if (character.IsInSightBounds(testLoc))
                                    return false;
                            }

                            Character locChar = ZoneManager.Instance.CurrentMap.GetCharAtLoc(testLoc);
                            if (locChar != null)
                                return false;
                            return true;
                        };
                        Grid.LocTest checkBlock = (Loc testLoc) =>
                        {
                            return ZoneManager.Instance.CurrentMap.TileBlocked(testLoc, true);
                        };
                        Grid.LocTest checkDiagBlock = (Loc testLoc) =>
                        {
                            return ZoneManager.Instance.CurrentMap.TileBlocked(testLoc, true, true);
                        };

                        List<Loc> resultLocs = new List<Loc>();
                        foreach (Loc loc in Grid.FindClosestConnectedTiles(new Loc(), new Loc(ZoneManager.Instance.CurrentMap.Width, ZoneManager.Instance.CurrentMap.Height),
                            checkOpen, checkBlock, checkDiagBlock, trialLoc, newTeam.Players.Count))
                        {
                            resultLocs.Add(loc);
                        }


                        if (resultLocs.Count >= newTeam.Players.Count + newTeam.Guests.Count)
                        {
                            for (int jj = 0; jj < newTeam.Players.Count; jj++)
                                newTeam.Players[jj].CharLoc = resultLocs[jj];
                            for (int jj = 0; jj < newTeam.Guests.Count; jj++)
                                newTeam.Guests[jj].CharLoc = resultLocs[newTeam.Players.Count + jj];

                            ZoneManager.Instance.CurrentMap.MapTeams.Add(newTeam);

                            foreach (Character member in newTeam.EnumerateChars())
                            {
                                member.RefreshTraits();
                                respawns.Add(member);
                            }
                            break;
                        }
                    }
                }
            }
            return respawns;
        }
    }

    [Serializable]
    public abstract class RespawnBaseEvent : SingleCharEvent
    {
        /// <summary>
        /// The limit to the number of enemies on the map.  If this number is reached or exceeded, no more respawns will occur.
        /// </summary>
        public int MaxFoes;

        /// <summary>
        /// The amount of time it takes for a new enemy team to respawn, in turns.
        /// </summary>
        public int RespawnTime;

        public RespawnBaseEvent() { }
        public RespawnBaseEvent(int maxFoes, int respawnTime)
        {
            MaxFoes = maxFoes;
            RespawnTime = respawnTime;
        }
        protected RespawnBaseEvent(RespawnBaseEvent other)
        {
            MaxFoes = other.MaxFoes;
            RespawnTime = other.RespawnTime;
        }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (context.User != null)
                yield break;

            //Map Respawns
            if (RespawnTime > 0 && (ZoneManager.Instance.CurrentMap.MapTurns + 1) % RespawnTime == 0)
            {
                int totalFoes = 0;
                foreach (Team team in ZoneManager.Instance.CurrentMap.MapTeams)
                {
                    foreach (Character chara in team.Players)
                    {
                        if (!chara.Dead)
                            totalFoes++;
                    }
                }
                if (totalFoes < MaxFoes)
                {
                    List<Character> respawns = RespawnMob();
                    foreach (Character respawn in respawns)
                    {
                        respawn.Tactic.Initialize(respawn);
                        if (!respawn.Dead)
                        {
                            yield return CoroutineManager.Instance.StartCoroutine(respawn.OnMapStart());
                            ZoneManager.Instance.CurrentMap.UpdateExploration(respawn);
                        }
                    }
                }
            }
        }

        public abstract List<Character> RespawnMob();
    }


    [Serializable]
    public class DespawnRadiusEvent : SingleCharEvent
    {
        /// <summary>
        /// The maximum radius from a player that enemies are allowed to remain.  Go farther than this when the check occurs, and the enemy despawns.
        /// </summary>
        public int Radius;

        /// <summary>
        /// The amount of time it takes for a new enemy team to respawn, in turns.
        /// </summary>
        public int DespawnTime;

        public DespawnRadiusEvent() { }
        public DespawnRadiusEvent(int radius, int despawnTime)
        {
            Radius = radius;
            DespawnTime = despawnTime;
        }
        protected DespawnRadiusEvent(DespawnRadiusEvent other)
        {
            Radius = other.Radius;
            DespawnTime = other.DespawnTime;
        }
        public override GameEvent Clone() { return new DespawnRadiusEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (context.User != null)
                yield break;

            //Map Despawns
            if (DespawnTime > 0 && (ZoneManager.Instance.CurrentMap.MapTurns + 1) % DespawnTime == 0)
            {
                for (int ii = ZoneManager.Instance.CurrentMap.MapTeams.Count - 1; ii >= 0; ii--)
                {
                    Team team = ZoneManager.Instance.CurrentMap.MapTeams[ii];
                    for (int jj = team.Players.Count - 1; jj >= 0; jj--)
                    {
                        Character chara = team.Players[jj];

                        bool keep = false;
                        foreach (Character player in ZoneManager.Instance.CurrentMap.ActiveTeam.Players)
                        {
                            if (player.Dead)
                                continue;

                            if ((player.CharLoc - chara.CharLoc).Dist8() <= Radius)
                            {
                                keep = true;
                                break;
                            }
                        }

                        if (!keep)
                            yield return CoroutineManager.Instance.StartCoroutine(chara.DieSilent());
                    }
                }
            }
        }
    }

    [Serializable]
    public class FamilySingleEvent : SingleCharEvent
    {
        public SingleCharEvent BaseEvent;

        public FamilySingleEvent()
        { }
        public FamilySingleEvent(SingleCharEvent baseEvent)
        {
            BaseEvent = baseEvent;
        }
        protected FamilySingleEvent(FamilySingleEvent other)
        {
            BaseEvent = (SingleCharEvent)other.BaseEvent.Clone();
        }
        public override GameEvent Clone() { return new FamilySingleEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            ItemData entry = DataManager.Instance.GetItem(owner.GetID());
            FamilyState family;
            if (!entry.ItemStates.TryGet<FamilyState>(out family))
                yield break;
            if (family.Members.Contains(ownerChar.BaseForm.Species))
                yield return CoroutineManager.Instance.StartCoroutine(BaseEvent.Apply(owner, ownerChar, context));
        }
    }

    [Serializable]
    public class TerrainNeededEvent : SingleCharEvent
    {
        [JsonConverter(typeof(TerrainConverter))]
        [DataType(0, DataManager.DataType.Terrain, false)]
        public string Terrain;

        public SingleCharEvent BaseEvent;

        public TerrainNeededEvent()
        { }
        public TerrainNeededEvent(string terrain, SingleCharEvent baseEvent)
        {
            Terrain = terrain;
            BaseEvent = baseEvent;
        }
        protected TerrainNeededEvent(TerrainNeededEvent other)
        {
            Terrain = other.Terrain;
            BaseEvent = (SingleCharEvent)other.BaseEvent.Clone();
        }
        public override GameEvent Clone() { return new TerrainNeededEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            Tile tile = ZoneManager.Instance.CurrentMap.Tiles[context.User.CharLoc.X][context.User.CharLoc.Y];
            if (tile.ID == Terrain)
            {
                yield return CoroutineManager.Instance.StartCoroutine(BaseEvent.Apply(owner, ownerChar, context));
            }
        }
    }

    [Serializable]
    public class CountDownRemoveEvent : SingleCharEvent
    {
        public bool ShowMessage;

        public CountDownRemoveEvent() { }
        public CountDownRemoveEvent(bool showMessage)
        {
            ShowMessage = showMessage;
        }
        protected CountDownRemoveEvent(CountDownRemoveEvent other)
        {
            ShowMessage = other.ShowMessage;
        }
        public override GameEvent Clone() { return new CountDownRemoveEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (((StatusEffect)owner).StatusStates.GetWithDefault<CountDownState>().Counter < 0)
                yield break;
            //if (((StatusEffect)owner).StatusStates.Get<RecentState>() != null)
            //    yield break;

            ((StatusEffect)owner).StatusStates.GetWithDefault<CountDownState>().Counter--;
            if (((StatusEffect)owner).StatusStates.GetWithDefault<CountDownState>().Counter <= 0)
                yield return CoroutineManager.Instance.StartCoroutine(context.User.RemoveStatusEffect(((StatusEffect)owner).ID, ShowMessage));
        }
    }

    [Serializable]
    public class CountUpEvent : SingleCharEvent
    {
        public override GameEvent Clone() { return new CountUpEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            ((StatusEffect)owner).StatusStates.GetWithDefault<CountDownState>().Counter++;
            yield break;
        }
    }

    [Serializable]
    public class HealEvent : SingleCharEvent
    {
        

        public HealEvent() { }
        public override GameEvent Clone() { return new HealEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            yield return CoroutineManager.Instance.StartCoroutine(context.User.RestoreHP(((StatusEffect)owner).StatusStates.GetWithDefault<HPState>().HP));
        }
    }
    [Serializable]
    public class DamageAreaEvent : SingleCharEvent
    {
        public int Range;
        public List<AnimEvent> Anims;

        public DamageAreaEvent()
        {
            Anims = new List<AnimEvent>();
        }
        public DamageAreaEvent(int range, params AnimEvent[] anims)
        {
            Range = range;
            Anims = new List<AnimEvent>();
            Anims.AddRange(anims);
        }
        protected DamageAreaEvent(DamageAreaEvent other)
        {
            Range = other.Range;
            Anims = new List<AnimEvent>();
            foreach (AnimEvent anim in other.Anims)
                Anims.Add((AnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new DamageAreaEvent(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            yield return new WaitUntil(DungeonScene.Instance.AnimationsOver);

            foreach (AnimEvent anim in Anims)
                yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

            if (!context.User.Dead)
            {
                List<Character> eligibleTargets = new List<Character>();
                foreach (Character target in ZoneManager.Instance.CurrentMap.GetCharsInFillRect(context.User.CharLoc, Rect.FromPointRadius(context.User.CharLoc, Range)))
                {
                    if (DungeonScene.Instance.GetMatchup(context.User, target) != Alignment.Foe)
                        eligibleTargets.Add(target);
                }
                foreach(Character target in eligibleTargets)
                    yield return CoroutineManager.Instance.StartCoroutine(target.InflictDamage(((StatusEffect)owner).StatusStates.GetWithDefault<HPState>().HP));
            }
        }
    }

    [Serializable]
    public class CheckNullTargetEvent : SingleCharEvent
    {
        public bool ShowMessage;

        public CheckNullTargetEvent() { }
        public CheckNullTargetEvent(bool showMessage)
        {
            ShowMessage = showMessage;
        }
        protected CheckNullTargetEvent(CheckNullTargetEvent other)
        {
            ShowMessage = other.ShowMessage;
        }
        public override GameEvent Clone() { return new CheckNullTargetEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (((StatusEffect)owner).TargetChar == null)
                yield return CoroutineManager.Instance.StartCoroutine(context.User.RemoveStatusEffect(((StatusEffect)owner).ID, ShowMessage));
        }
    }

    [Serializable]
    public class SoundEvent : SingleCharEvent
    {
        public string Sound;
        
        public override GameEvent Clone() { return new SoundEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            GameManager.Instance.SE(Sound);
            yield break;
        }
    }
    [Serializable]
    public class RemoveEvent : SingleCharEvent
    {
        public bool ShowMessage;

        public RemoveEvent() { }
        public RemoveEvent(bool showMessage)
        {
            ShowMessage = showMessage;
        }
        protected RemoveEvent(RemoveEvent other)
        {
            ShowMessage = other.ShowMessage;
        }
        public override GameEvent Clone() { return new RemoveEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            yield return CoroutineManager.Instance.StartCoroutine(context.User.RemoveStatusEffect(((StatusEffect)owner).ID, ShowMessage));
        }
    }
    [Serializable]
    public class BattleLogEvent : SingleCharEvent
    {
        public StringKey Message;

        public BattleLogEvent() { }
        public BattleLogEvent(StringKey message)
        {
            Message = message;
        }
        protected BattleLogEvent(BattleLogEvent other)
        {
            Message = other.Message;
        }
        public override GameEvent Clone() { return new BattleLogEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (context.User != null)
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), context.User.GetDisplayName(false)));
            else
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal()));
            yield break;
        }
    }
    [Serializable]
    public class BattleLogOwnerEvent : SingleCharEvent
    {
        public StringKey Message;

        public BattleLogOwnerEvent() { }
        public BattleLogOwnerEvent(StringKey message)
        {
            Message = message;
        }
        protected BattleLogOwnerEvent(BattleLogOwnerEvent other)
        {
            Message = other.Message;
        }
        public override GameEvent Clone() { return new BattleLogOwnerEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), context.User.GetDisplayName(false), owner.GetDisplayName()));
            yield break;
        }
    }


    [Serializable]
    public class AnimEvent : SingleCharEvent
    {
        public FiniteEmitter Emitter;
        [Sound(0)]
        public string Sound;
        public int Delay;

        public AnimEvent()
        {
            Emitter = new EmptyFiniteEmitter();
        }
        public AnimEvent(FiniteEmitter emitter, string sound) : this(emitter, sound, 0) { }
        public AnimEvent(FiniteEmitter emitter, string sound, int delay)
        {
            Emitter = emitter;
            Sound = sound;
            Delay = delay;
        }
        protected AnimEvent(AnimEvent other)
        {
            Emitter = (FiniteEmitter)other.Emitter.Clone();
            Sound = other.Sound;
            Delay = other.Delay;
        }
        public override GameEvent Clone() { return new AnimEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            GameManager.Instance.BattleSE(Sound);

            if (context.User != null && !context.User.Unidentifiable)
            {
                FiniteEmitter endEmitter = (FiniteEmitter)Emitter.Clone();
                endEmitter.SetupEmit(context.User.MapLoc, context.User.MapLoc, context.User.CharDir);
                DungeonScene.Instance.CreateAnim(endEmitter, DrawLayer.NoDraw);
            }
            yield return new WaitForFrames(Delay);
        }
    }

    [Serializable]
    public class FractionDamageEvent : SingleCharEvent
    {
        /// <summary>
        /// How much HP damage to inflict as a fraction of the target's total HP.
        /// </summary>
        public int HPFraction;
        public string Message;

        public FractionDamageEvent() { }
        public FractionDamageEvent(int hpFraction, string message)
        {
            HPFraction = hpFraction;
            Message = message;
        }
        protected FractionDamageEvent(FractionDamageEvent other)
        {
            HPFraction = other.HPFraction;
            Message = other.Message;
        }
        public override GameEvent Clone() { return new FractionDamageEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (Message != null)
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message, context.User.GetDisplayName(false)));
            yield return CoroutineManager.Instance.StartCoroutine(context.User.InflictDamage(Math.Max(1, context.User.MaxHP / HPFraction)));
        }
    }

    [Serializable]
    public class FractionHealEvent : SingleCharEvent
    {
        /// <summary>
        /// How much HP to heal as a fraction of the target's total HP.
        /// </summary>
        public int HPFraction;
        [StringKey(0, true)]
        public StringKey Message;

        public FractionHealEvent() { }
        public FractionHealEvent(int hpFraction, StringKey message)
        {
            HPFraction = hpFraction;
            Message = message;
        }
        protected FractionHealEvent(FractionHealEvent other)
        {
            HPFraction = other.HPFraction;
            Message = other.Message;
        }
        public override GameEvent Clone() { return new FractionHealEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (context.User.Dead)
                yield break;

            if (context.User.HP < context.User.MaxHP)
            {
                if (Message.IsValid())
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), context.User.GetDisplayName(false), owner.GetDisplayName()));
                yield return CoroutineManager.Instance.StartCoroutine(context.User.RestoreHP(Math.Max(1, context.User.MaxHP / HPFraction), false));
            }
        }
    }

    [Serializable]
    public class RemoveLocTerrainEvent : SingleCharEvent
    {
        [JsonConverter(typeof(TerrainSetConverter))]
        public HashSet<string> TileTypes;

        public RemoveLocTerrainEvent() { TileTypes = new HashSet<string>(); }
        public RemoveLocTerrainEvent(params string[] tileTypes)
            : this()
        {
            foreach (string tileType in tileTypes)
                TileTypes.Add(tileType);
        }
        protected RemoveLocTerrainEvent(RemoveLocTerrainEvent other)
        {
            TileTypes = new HashSet<string>();
            foreach (string tileType in other.TileTypes)
                TileTypes.Add(tileType);
        }
        public override GameEvent Clone() { return new RemoveLocTerrainEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (!Collision.InBounds(ZoneManager.Instance.CurrentMap.Width, ZoneManager.Instance.CurrentMap.Height, context.User.CharLoc))
                yield break;

            Tile tile = ZoneManager.Instance.CurrentMap.Tiles[context.User.CharLoc.X][context.User.CharLoc.Y];
            if (TileTypes.Contains(tile.Data.ID))
            {
                tile.Data = new TerrainTile(DataManager.Instance.GenFloor);
                int distance = 0;
                Loc startLoc = context.User.CharLoc - new Loc(distance + 2);
                Loc sizeLoc = new Loc((distance + 2) * 2 + 1);
                ZoneManager.Instance.CurrentMap.MapModified(startLoc, sizeLoc);
            }
        }
    }

    [Serializable]
    public class RemoveLocTrapEvent : SingleCharEvent
    {
        public override GameEvent Clone() { return new RemoveLocTrapEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (!Collision.InBounds(ZoneManager.Instance.CurrentMap.Width, ZoneManager.Instance.CurrentMap.Height, context.User.CharLoc))
                yield break;

            Tile tile = ZoneManager.Instance.CurrentMap.Tiles[context.User.CharLoc.X][context.User.CharLoc.Y];
            if (!String.IsNullOrEmpty(tile.Effect.ID))
            {
                TileData entry = DataManager.Instance.GetTile(tile.Effect.GetID());
                if (entry.StepType == TileData.TriggerType.Trap)
                    tile.Effect = new EffectTile(tile.Effect.TileLoc);
            }
        }
    }

    [Serializable]
    public class SingleMapStatusExceptEvent : SingleCharEvent
    {
        [JsonConverter(typeof(MapStatusListConverter))]
        [DataType(1, DataManager.DataType.MapStatus, false)]
        public List<string> States;

        public SingleCharEvent BaseEvent;


        public SingleMapStatusExceptEvent() { States = new List<string>(); }
        public SingleMapStatusExceptEvent(string mapStatus, SingleCharEvent baseEvent) : this()
        {
            States.Add(mapStatus);
            BaseEvent = baseEvent;
        }
        public SingleMapStatusExceptEvent(SingleMapStatusExceptEvent other) : this()
        {
            States.AddRange(other.States);
            BaseEvent = (SingleCharEvent)other.BaseEvent.Clone();
        }

        public override GameEvent Clone() { return new SingleMapStatusExceptEvent(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            //check if the attacker has the right charstate
            bool hasState = false;
            foreach (string state in States)
            {
                if (ZoneManager.Instance.CurrentMap.Status.ContainsKey(state))
                    hasState = true;
            }
            if (!hasState)
                yield return CoroutineManager.Instance.StartCoroutine(BaseEvent.Apply(owner, ownerChar, context));
        }

    }

    [Serializable]
    public class SingleExceptEvent : SingleCharEvent
    {
        [StringTypeConstraint(1, typeof(CharState))]
        public List<FlagType> States;

        public SingleCharEvent BaseEvent;


        public SingleExceptEvent() { States = new List<FlagType>(); }
        public SingleExceptEvent(Type state, SingleCharEvent baseEvent) : this()
        {
            States.Add(new FlagType(state));
            BaseEvent = baseEvent;
        }
        public SingleExceptEvent(SingleExceptEvent other) : this()
        {
            States.AddRange(other.States);
            BaseEvent = (SingleCharEvent)other.BaseEvent.Clone();
        }

        public override GameEvent Clone() { return new SingleExceptEvent(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            //check if the attacker has the right charstate
            bool hasState = false;
            foreach (FlagType state in States)
            {
                if (context.User.CharStates.Contains(state.FullType))
                    hasState = true;
            }
            if (!hasState)
            {
                yield return CoroutineManager.Instance.StartCoroutine(BaseEvent.Apply(owner, ownerChar, context));
            }
        }

    }

    [Serializable]
    public class GiveStatusEvent : SingleCharEvent
    {
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string StatusID;
        public bool SilentCheck;
        [SubGroup]
        public StateCollection<StatusState> States;
        [StringKey(0, true)]
        public StringKey TriggerMsg;
        [Sound(0)]
        public string TriggerSound;
        public FiniteEmitter TriggerEmitter;

        public GiveStatusEvent() { States = new StateCollection<StatusState>(); StatusID = ""; }
        public GiveStatusEvent(string statusID, StateCollection<StatusState> states) : this(statusID, states, false) { }
        public GiveStatusEvent(string statusID, StateCollection<StatusState> states, bool silentCheck)
        {
            StatusID = statusID;
            States = states;
            SilentCheck = silentCheck;
            TriggerSound = "";
            TriggerEmitter = new EmptyFiniteEmitter();
        }
        public GiveStatusEvent(string statusID, StateCollection<StatusState> states, bool silentCheck, StringKey trigger)
        {
            StatusID = statusID;
            States = states;
            SilentCheck = silentCheck;
            TriggerMsg = trigger;
            TriggerEmitter = new EmptyFiniteEmitter();
        }
        public GiveStatusEvent(string statusID, StateCollection<StatusState> states, bool silentCheck, StringKey trigger, string triggerSound, FiniteEmitter emitter)
        {
            StatusID = statusID;
            States = states;
            SilentCheck = silentCheck;
            TriggerMsg = trigger;
            TriggerSound = triggerSound;
            TriggerEmitter = emitter;
        }
        protected GiveStatusEvent(GiveStatusEvent other)
        {
            StatusID = other.StatusID;
            States = other.States.Clone();
            SilentCheck = other.SilentCheck;
            TriggerMsg = other.TriggerMsg;
            TriggerSound = other.TriggerSound;
            TriggerEmitter = (FiniteEmitter)other.TriggerEmitter.Clone();
        }
        public override GameEvent Clone() { return new GiveStatusEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            StatusEffect status = new StatusEffect(StatusID);
            status.LoadFromData();
            foreach (StatusState state in States)
                status.StatusStates.Set(state.Clone<StatusState>());

            if (!TriggerMsg.IsValid() && TriggerSound == "")
                yield return CoroutineManager.Instance.StartCoroutine(context.User.AddStatusEffect(null, status, null, !SilentCheck, true));
            else
            {
                StatusCheckContext statusContext = new StatusCheckContext(null, context.User, status, false);

                yield return CoroutineManager.Instance.StartCoroutine(context.User.BeforeStatusCheck(statusContext));
                if (statusContext.CancelState.Cancel)
                    yield break;

                if (TriggerMsg.IsValid())
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(TriggerMsg.ToLocal(), ownerChar.GetDisplayName(false), owner.GetDisplayName()));
                statusContext.msg = true;

                GameManager.Instance.BattleSE(TriggerSound);

                if (!context.User.Unidentifiable)
                {
                    FiniteEmitter endEmitter = (FiniteEmitter)TriggerEmitter.Clone();
                    endEmitter.SetupEmit(context.User.MapLoc, context.User.MapLoc, context.User.CharDir);
                    DungeonScene.Instance.CreateAnim(endEmitter, DrawLayer.NoDraw);
                }

                yield return CoroutineManager.Instance.StartCoroutine(context.User.ExecuteAddStatus(statusContext));
            }
        }
    }
    [Serializable]
    public class RemoveStatusEvent : SingleCharEvent
    {
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string StatusID;

        public RemoveStatusEvent() { StatusID = ""; }
        public RemoveStatusEvent(string statusID)
        {
            StatusID = statusID;
        }
        protected RemoveStatusEvent(RemoveStatusEvent other)
        {
            StatusID = other.StatusID;
        }
        public override GameEvent Clone() { return new RemoveStatusEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            yield return CoroutineManager.Instance.StartCoroutine(context.User.RemoveStatusEffect(StatusID));
        }
    }


    [Serializable]
    public class InvokeAttackEvent : SingleCharEvent
    {
        public CombatAction HitboxAction;
        public ExplosionData Explosion;
        public BattleData NewData;
        [StringKey(0, true)]
        public StringKey Msg;

        public InvokeAttackEvent() { }
        public InvokeAttackEvent(CombatAction action, ExplosionData explosion, BattleData moveData, StringKey msg)
        {
            HitboxAction = action;
            Explosion = explosion;
            NewData = moveData;
            Msg = msg;
        }
        protected InvokeAttackEvent(InvokeAttackEvent other)
        {
            HitboxAction = other.HitboxAction;
            Explosion = other.Explosion;
            NewData = new BattleData(other.NewData);
            Msg = other.Msg;
        }
        public override GameEvent Clone() { return new InvokeAttackEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            CharAnimation standAnim = new CharAnimIdle(context.User.CharLoc, context.User.CharDir);
            standAnim.MajorAnim = true;
            yield return CoroutineManager.Instance.StartCoroutine(context.User.StartAnim(standAnim));

            BattleContext newContext = new BattleContext(BattleActionType.Trap);
            newContext.User = context.User;
            newContext.UsageSlot = BattleContext.FORCED_SLOT;

            newContext.StartDir = newContext.User.CharDir;

            //change move effects
            newContext.Data = new BattleData(NewData);
            newContext.Data.ID = owner.GetID();
            newContext.Data.DataType = DataManager.DataType.Intrinsic;

            newContext.Explosion = new ExplosionData(Explosion);
            newContext.HitboxAction = HitboxAction.Clone();
            newContext.Strikes = 1;
            newContext.Item = new InvItem();

            if (Msg.IsValid())
                newContext.SetActionMsg(Text.FormatGrammar(Msg.ToLocal(), newContext.User.GetDisplayName(false)));

            //process the attack
            yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.PreProcessAction(newContext));

            //Handle Use
            yield return CoroutineManager.Instance.StartCoroutine(newContext.User.BeforeAction(newContext));
            if (newContext.CancelState.Cancel) { yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.CancelWait(newContext.User.CharLoc)); yield break; }

            newContext.PrintActionMsg();

            yield return CoroutineManager.Instance.StartCoroutine(LocalExecuteAction(newContext));
            if (newContext.CancelState.Cancel) { yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.CancelWait(newContext.User.CharLoc)); yield break; }
            yield return CoroutineManager.Instance.StartCoroutine(TrapRepeatActions(newContext));
        }

        public IEnumerator<YieldInstruction> LocalExecuteAction(BattleContext baseContext)
        {
            BattleContext context = new BattleContext(baseContext, true);

            yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.PerformAction(context));
            if (context.CancelState.Cancel) yield break;
        }


        public IEnumerator<YieldInstruction> TrapRepeatActions(BattleContext context)
        {
            //increment for multistrike
            context.StrikesMade++;
            while (context.StrikesMade < context.Strikes)
            {
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.PreProcessAction(context));
                yield return CoroutineManager.Instance.StartCoroutine(LocalExecuteAction(context));

                context.StrikesMade++;
            }
        }
    }


    [Serializable]
    public class WeatherFormeEvent : SingleCharEvent
    {
        [JsonConverter(typeof(MonsterConverter))]
        [DataType(0, DataManager.DataType.Monster, false)]
        public string ReqSpecies;
        public int DefaultForme;
        [JsonConverter(typeof(MapStatusIntDictConverter))]
        [DataType(1, DataManager.DataType.MapStatus, false)]
        public Dictionary<string, int> WeatherPair;

        public WeatherFormeEvent() { WeatherPair = new Dictionary<string, int>(); ReqSpecies = ""; }
        public WeatherFormeEvent(string reqSpecies, int defaultForme, Dictionary<string, int> weather)
        {
            ReqSpecies = reqSpecies;
            DefaultForme = defaultForme;
            WeatherPair = weather;
        }
        protected WeatherFormeEvent(WeatherFormeEvent other) : this()
        {
            ReqSpecies = other.ReqSpecies;
            DefaultForme = other.DefaultForme;

            foreach (string weather in other.WeatherPair.Keys)
                WeatherPair.Add(weather, other.WeatherPair[weather]);
        }
        public override GameEvent Clone() { return new WeatherFormeEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (context.User == null)
                yield break;

            if (context.User.CurrentForm.Species != ReqSpecies)
                yield break;

            //get the forme it should be in
            int forme = DefaultForme;

            foreach (string weather in WeatherPair.Keys)
            {
                if (ZoneManager.Instance.CurrentMap.Status.ContainsKey(weather))
                {
                    forme = WeatherPair[weather];
                    break;
                }
            }

            if (forme != context.User.CurrentForm.Form)
            {
                //transform it
                context.User.Transform(new MonsterID(context.User.CurrentForm.Species, forme, context.User.CurrentForm.Skin, context.User.CurrentForm.Gender));
            }

            yield break;
        }
    }




    [Serializable]
    public class MeteorFormeEvent : SingleCharEvent
    {
        [JsonConverter(typeof(MonsterConverter))]
        [DataType(0, DataManager.DataType.Monster, false)]
        public string ReqSpecies;
        public int ResultForme;
        public int FormeMult;
        public int PercentHP;
        public bool Below;

        public MeteorFormeEvent() { ReqSpecies = ""; }
        public MeteorFormeEvent(string reqSpecies, int resultForme, int formeMult, int percentHp, bool below)
        {
            ReqSpecies = reqSpecies;
            ResultForme = resultForme;
            FormeMult = formeMult;
            PercentHP = percentHp;
            Below = below;
        }
        protected MeteorFormeEvent(MeteorFormeEvent other) : this()
        {
            ReqSpecies = other.ReqSpecies;
            ResultForme = other.ResultForme;
            FormeMult = other.FormeMult;
            PercentHP = other.PercentHP;
            Below = other.Below;
        }
        public override GameEvent Clone() { return new MeteorFormeEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (context.User == null)
                yield break;

            if (context.User.CurrentForm.Species != ReqSpecies)
                yield break;

            //get the forme it should be in
            bool compareThrough = false;
            if (Below)
                compareThrough = (context.User.HP * 100 <= context.User.MaxHP * PercentHP);
            else
                compareThrough = (context.User.HP * 100 > context.User.MaxHP * PercentHP);

            if (ResultForme != context.User.CurrentForm.Form / FormeMult && compareThrough)
            {
                //transform it
                context.User.Transform(new MonsterID(context.User.CurrentForm.Species, context.User.CurrentForm.Form % FormeMult + ResultForme * FormeMult, context.User.CurrentForm.Skin, context.User.CurrentForm.Gender));
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_FORM_CHANGE").ToLocal(), context.User.GetDisplayName(false)));
            }

            yield break;
        }
    }


    [Serializable]
    public class PreDeathEvent : SingleCharEvent
    {
        public PreDeathEvent() { }
        public override GameEvent Clone() { return new PreDeathEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            int animTime = 10 + GameManager.Instance.ModifyBattleSpeed(50, context.User.CharLoc);

            if (context.User.MemberTeam == DungeonScene.Instance.ActiveTeam)
            {
                CharAnimDefeated defeatAnim = new CharAnimDefeated();
                defeatAnim.CharLoc = context.User.CharLoc;
                defeatAnim.CharDir = context.User.CharDir;
                defeatAnim.MajorAnim = true;
                defeatAnim.AnimTime = animTime;
                yield return CoroutineManager.Instance.StartCoroutine(context.User.StartAnim(defeatAnim));
                DungeonScene.Instance.LogMsg(Text.FormatKey("MSG_DEFEAT", context.User.GetDisplayName(true)));
            }
            else
            {
                CharAnimDefeated defeatAnim = new CharAnimDefeated();
                defeatAnim.CharLoc = context.User.CharLoc;
                defeatAnim.CharDir = context.User.CharDir;
                defeatAnim.MajorAnim = true;
                defeatAnim.AnimTime = animTime;
                yield return CoroutineManager.Instance.StartCoroutine(context.User.StartAnim(defeatAnim));
                DungeonScene.Instance.LogMsg(Text.FormatKey("MSG_DEFEAT_FOE", context.User.GetDisplayName(true)));

            }

            yield return new WaitForFrames(animTime - 1);
        }
    }

    [Serializable]
    public class SetDeathEvent : SingleCharEvent
    {
        public SetDeathEvent() { }
        public override GameEvent Clone() { return new SetDeathEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            context.User.HP = 0;
            context.User.Dead = true;

            //if (DataManager.Instance.CurrentReplay != null)
            //{
            //    using (StreamWriter writer = new StreamWriter(DiagManager.LOG_PATH + "Encounter.txt", true))
            //    {
            //        if (context.User.MemberTeam != DataManager.Instance.Save.ActiveTeam)
            //            writer.WriteLine(String.Format("{0} {1} {2}", ZoneManager.Instance.CurrentMapID.ID, ZoneManager.Instance.CurrentMap.MapTurns, DataManager.Instance.Save.TotalTurns));
            //    }
            //}

            yield break;
        }
    }

    [Serializable]
    public class SetTrapSingleEvent : SingleCharEvent
    {
        [JsonConverter(typeof(TileConverter))]
        [DataType(0, DataManager.DataType.Tile, false)]
        public string TrapID;

        public SetTrapSingleEvent() { }
        public SetTrapSingleEvent(string trapID)
        {
            TrapID = trapID;
        }
        protected SetTrapSingleEvent(SetTrapSingleEvent other)
        {
            TrapID = other.TrapID;
        }
        public override GameEvent Clone() { return new SetTrapSingleEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            Tile tile = ZoneManager.Instance.CurrentMap.GetTile(context.User.CharLoc);
            if (tile == null)
                yield break;

            if (tile.Data.GetData().BlockType == TerrainData.Mobility.Passable && String.IsNullOrEmpty(tile.Effect.ID))
            {
                tile.Effect = new EffectTile(TrapID, true, tile.Effect.TileLoc);
                tile.Effect.Owner = ZoneManager.Instance.CurrentMap.GetTileOwner(context.User);
            }
        }
    }

    [Serializable]
    public abstract class HandoutExpEvent : SingleCharEvent
    {
        /// <summary>
        /// 
        /// </summary>
        public bool IgnoreMark;

        protected HandoutExpEvent() { }

        protected HandoutExpEvent(HandoutExpEvent other) { IgnoreMark = other.IgnoreMark; }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (!context.User.Dead)
                yield break;


            if (context.User.MemberTeam == DungeonScene.Instance.ActiveTeam)
                yield return new WaitForFrames(60);
            else
            {
                if (context.User.EXPMarked || IgnoreMark)
                {
                    if (context.User.MemberTeam is ExplorerTeam)
                    {
                        //TODO: hand out EXP only when the final member is defeated
                    }
                    else
                    {
                        MonsterData monsterData = DataManager.Instance.GetMonster(context.User.BaseForm.Species);
                        MonsterFormData monsterForm = (MonsterFormData)monsterData.Forms[context.User.BaseForm.Form];
                        //if (DataManager.Instance.CurrentReplay != null)
                        //{
                        //    string filename = Path.GetFileNameWithoutExtension(DataManager.Instance.CurrentReplay.RecordDir);
                        //    using (StreamWriter writer = new StreamWriter(DiagManager.LOG_PATH + "EXP_"+ filename + ".txt", true))
                        //        writer.WriteLine(String.Format("{0},{1},{2},{3},{4}", ZoneManager.Instance.CurrentZoneID, ZoneManager.Instance.CurrentMapID.Segment, ZoneManager.Instance.CurrentMapID.ID, monsterForm.ExpYield, context.User.Level));
                        //}

                        for (int ii = 0; ii < DungeonScene.Instance.ActiveTeam.Players.Count; ii++)
                        {
                            if (ii >= DungeonScene.Instance.GainedEXP.Count)
                                DungeonScene.Instance.GainedEXP.Add(0);

                            Character recipient = DungeonScene.Instance.ActiveTeam.Players[ii];
                            int effectiveLevel = recipient.Level;
                            string growth = DataManager.Instance.GetMonster(recipient.BaseForm.Species).EXPTable;
                            GrowthData growthData = DataManager.Instance.GetGrowth(growth);
                            while (effectiveLevel < DataManager.Instance.Start.MaxLevel && recipient.EXP + DungeonScene.Instance.GainedEXP[ii] >= growthData.GetExpTo(recipient.Level, effectiveLevel + 1))
                                effectiveLevel++;

                            int exp = GetExp(monsterForm.ExpYield, context.User.Level, effectiveLevel);
                            DungeonScene.Instance.GainedEXP[ii] += exp;
                        }
                        for (int ii = 0; ii < DungeonScene.Instance.ActiveTeam.Assembly.Count; ii++)
                        {
                            if (!DungeonScene.Instance.ActiveTeam.Assembly[ii].Absentee)
                            {
                                int exp = GetExp(monsterForm.ExpYield, context.User.Level, DungeonScene.Instance.ActiveTeam.Assembly[ii].Level);
                                handoutAssemblyExp(DungeonScene.Instance.ActiveTeam.Assembly[ii], exp);
                            }
                        }
                    }
                }
                DataManager.Instance.Save.SeenMonster(context.User.BaseForm.Species);
            }
        }

        private void handoutAssemblyExp(Character player, int totalExp)
        {
            if (!player.Dead && player.Level < DataManager.Instance.Start.MaxLevel)
            {
                player.EXP += totalExp;

                string growth = DataManager.Instance.GetMonster(player.BaseForm.Species).EXPTable;
                GrowthData growthData = DataManager.Instance.GetGrowth(growth);
                while (player.EXP >= growthData.GetExpToNext(player.Level))
                {
                    player.EXP -= growthData.GetExpToNext(player.Level);
                    player.Level++;

                    if (player.Level >= DataManager.Instance.Start.MaxLevel)
                    {
                        player.EXP = 0;
                        break;
                    }
                }
            }
        }

        public abstract int GetExp(int expYield, int defeatedLv, int recipientLv);
    }

    /// <summary>
    /// EXP handed out for defeating an enemy is scaled based on the enemy's level.
    /// BaseEXP * Numerator * Level / Denominator + 1
    /// </summary>
    [Serializable]
    public class HandoutScaledExpEvent : HandoutExpEvent
    {
        public int Numerator;
        public int Denominator;
        public HandoutScaledExpEvent() { }
        public HandoutScaledExpEvent(int numerator, int denominator, int levelBuffer) { Numerator = numerator; Denominator = denominator; }
        protected HandoutScaledExpEvent(HandoutScaledExpEvent other) : base(other)
        {
            this.Numerator = other.Numerator;
            this.Denominator = other.Denominator;
        }
        public override GameEvent Clone() { return new HandoutScaledExpEvent(this); }

        public override int GetExp(int expYield, int defeatedLv, int recipientLv)
        {
            return (int)((ulong)expYield * (ulong)Numerator * (ulong)defeatedLv / (ulong)Denominator) + 1;
        }
    }

    /// <summary>
    /// EXP handed out for defeating an enemy is equal to its base EXP yield without any changes.
    /// </summary>
    [Serializable]
    public class HandoutConstantExpEvent : HandoutExpEvent
    {
        public HandoutConstantExpEvent() { }
        protected HandoutConstantExpEvent(HandoutConstantExpEvent other) : base(other)
        { }
        public override GameEvent Clone() { return new HandoutConstantExpEvent(this); }

        public override int GetExp(int expYield, int defeatedLv, int recipientLv)
        {
            return expYield;
        }
    }


    /// <summary>
    /// Uses one formula when the recipient's level is at or lower than the defeated level
    /// And another when it's higher
    /// </summary>
    [Serializable]
    public class HandoutPiecewiseExpEvent : HandoutExpEvent
    {
        /// <summary>
        /// Minimum level for target's level to be counted at
        /// </summary>
        public int ScaleMin;

        /// <summary>
        /// Added level for the target to be counted at
        /// </summary>
        public int ScaleAdd;

        public HandoutExpEvent UnderleveledHandout;

        public HandoutExpEvent OverleveledHandout;

        public HandoutPiecewiseExpEvent() { }
        public HandoutPiecewiseExpEvent(int scaleMin, int scaleAdd, HandoutExpEvent lowHandout, HandoutExpEvent highHandout)
        {
            ScaleMin = scaleMin;
            ScaleAdd = scaleAdd;
            UnderleveledHandout = lowHandout;
            OverleveledHandout = highHandout;
        }
        protected HandoutPiecewiseExpEvent(HandoutPiecewiseExpEvent other) : base(other)
        {
            ScaleMin = other.ScaleMin;
            ScaleAdd = other.ScaleAdd;
            UnderleveledHandout = (HandoutExpEvent)other.UnderleveledHandout.Clone();
            OverleveledHandout = (HandoutExpEvent)other.OverleveledHandout.Clone();
        }
        public override GameEvent Clone() { return new HandoutPiecewiseExpEvent(this); }

        public override int GetExp(int expYield, int defeatedLv, int recipientLv)
        {
            int recipientScaleLv = Math.Max(ScaleMin, recipientLv);
            int scaleLevel = Math.Max(ScaleMin, defeatedLv + ScaleAdd);
            if (scaleLevel >= recipientScaleLv)
                return UnderleveledHandout.GetExp(expYield, scaleLevel, recipientScaleLv);
            else
                return OverleveledHandout.GetExp(expYield, scaleLevel, recipientScaleLv);
        }

    }

    /// <summary>
    /// EXP handed out to each team member is scaled based on the team member's level relative to the defeated enemy's level.
    /// BaseEXP * Numerator * (2 * EnemyLv + LevelBuffer) ^ PowerCurve / (EnemyLv + PlayerLv + LevelBuffer) ^ PowerCurve / Denominator + 1
    /// </summary>
    [Serializable]
    public class HandoutRelativeExpEvent : HandoutExpEvent
    {
        /// <summary>
        /// Numerator of ratio
        /// </summary>
        public int Numerator;

        /// <summary>
        /// Denominator for ratio
        /// </summary>
        public int Denominator;

        /// <summary>
        /// Number to add to numerator and denominator to buffer the ratio
        /// </summary>
        public int LevelBuffer;

        /// <summary>
        /// Exponent when underleveled
        /// </summary>
        public int PowerCurve;


        public HandoutRelativeExpEvent() { }
        public HandoutRelativeExpEvent(int numerator, int denominator, int levelBuffer, int powerCurve)
        {
            Numerator = numerator;
            Denominator = denominator;
            LevelBuffer = levelBuffer;
            PowerCurve = powerCurve;
        }
        protected HandoutRelativeExpEvent(HandoutRelativeExpEvent other) : base(other)
        {
            this.Numerator = other.Numerator;
            this.Denominator = other.Denominator;
            this.LevelBuffer = other.LevelBuffer;
            this.PowerCurve = other.PowerCurve;
        }
        public override GameEvent Clone() { return new HandoutRelativeExpEvent(this); }

        public override int GetExp(int expYield, int defeatedLv, int recipientLv)
        {
            int multNum = 2 * defeatedLv + LevelBuffer;
            int multDen = recipientLv + defeatedLv + LevelBuffer;
            ulong exp = (ulong)expYield * (ulong)Numerator * (ulong)defeatedLv;
            for (int ii = 0; ii < PowerCurve; ii++)
                exp *= (ulong)multNum;
            for (int ii = 0; ii < PowerCurve; ii++)
                exp /= (ulong)multDen;
            exp /= (ulong)Denominator;

            return (int)exp + 1;
        }

        [OnDeserialized]
        internal void OnDeserializedMethod(StreamingContext context)
        {
            //TODO: delete in v1.1
            if (PowerCurve == 0 && Serializer.OldVersion < new Version(0, 7, 0))
                PowerCurve = 3;
        }
    }

    /// <summary>
    /// EXP handed out to each team member is scaled based on the team member's level relative to the defeated enemy's level.
    /// BaseEXP * Numerator * LevelBuffer / (PlayerLv - EnemyLv + LevelBuffer) / Denominator + 1
    /// This means it cannot be applied to situations where PlayerLv is LevelBuffer levels lower than EnemyLv due to div by 0
    /// </summary>
    [Serializable]
    public class HandoutHarmonicExpEvent : HandoutExpEvent
    {
        /// <summary>
        /// Numerator of ratio
        /// </summary>
        public int Numerator;

        /// <summary>
        /// Denominator for ratio
        /// </summary>
        public int Denominator;

        /// <summary>
        /// Number to add to numerator and denominator to buffer the ratio
        /// </summary>
        public int LevelBuffer;

        public HandoutHarmonicExpEvent() { }
        public HandoutHarmonicExpEvent(int numerator, int denominator, int levelBuffer)
        {
            Numerator = numerator;
            Denominator = denominator;
            LevelBuffer = levelBuffer;
        }
        protected HandoutHarmonicExpEvent(HandoutHarmonicExpEvent other) : base(other)
        {
            this.Numerator = other.Numerator;
            this.Denominator = other.Denominator;
            this.LevelBuffer = other.LevelBuffer;
        }
        public override GameEvent Clone() { return new HandoutHarmonicExpEvent(this); }

        public override int GetExp(int expYield, int defeatedLv, int recipientLv)
        {
            int levelDiff = recipientLv - defeatedLv;
            ulong exp = (ulong)expYield * (ulong)Numerator * (ulong)defeatedLv * (ulong)LevelBuffer;
            exp /= (ulong)(LevelBuffer + levelDiff);
            exp /= (ulong)Denominator;

            return (int)exp + 1;
        }
    }


    /// <summary>
    /// EXP handed out to each team member is scaled based on the team member's level relative to the defeated enemy's level.
    /// BaseEXP * Numerator * (EnemyLv - PlayerLv + LevelBuffer) / (LevelBuffer) / Denominator + 1
    /// Will drop to 0 if PlayerLv is LevelBuffer levels higher than EnemyLv
    /// </summary>
    [Serializable]
    public class HandoutStackExpEvent : HandoutExpEvent
    {
        /// <summary>
        /// Numerator of ratio
        /// </summary>
        public int Numerator;

        /// <summary>
        /// Denominator for ratio
        /// </summary>
        public int Denominator;

        /// <summary>
        /// Number to add to numerator and denominator to buffer the ratio
        /// </summary>
        public int LevelBuffer;

        public HandoutStackExpEvent() { }
        public HandoutStackExpEvent(int numerator, int denominator, int levelBuffer)
        {
            Numerator = numerator;
            Denominator = denominator;
            LevelBuffer = levelBuffer;
        }
        protected HandoutStackExpEvent(HandoutStackExpEvent other) : base(other)
        {
            this.Numerator = other.Numerator;
            this.Denominator = other.Denominator;
            this.LevelBuffer = other.LevelBuffer;
        }
        public override GameEvent Clone() { return new HandoutStackExpEvent(this); }

        public override int GetExp(int expYield, int defeatedLv, int recipientLv)
        {
            int levelDiff = defeatedLv - recipientLv;
            ulong exp = (ulong)expYield * (ulong)Numerator * (ulong)defeatedLv * (ulong)(levelDiff + LevelBuffer);
            exp /= (ulong)LevelBuffer;
            exp /= (ulong)Denominator;

            return (int)exp + 1;
        }
    }


    [Serializable]
    public class ImpostorReviveEvent : SingleCharEvent
    {
        [JsonConverter(typeof(IntrinsicConverter))]
        [DataType(0, DataManager.DataType.Intrinsic, false)]
        public string AbilityID;
        [DataType(0, DataManager.DataType.Status, false)]
        public string StatusID;
        public ImpostorReviveEvent() { AbilityID = ""; StatusID = ""; }
        public ImpostorReviveEvent(string abilityID, string statusID) { AbilityID = abilityID; StatusID = statusID; }
        protected ImpostorReviveEvent(ImpostorReviveEvent other) { this.AbilityID = other.AbilityID; this.StatusID = other.StatusID; }
        public override GameEvent Clone() { return new ImpostorReviveEvent(this); }
        
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (!context.User.Dead)
                yield break;
            
            StatusEffect transform = context.User.GetStatusEffect(StatusID);
            if (transform == null)
                yield break;
            
            foreach (string id in context.User.BaseIntrinsics)
            {
                if (id == AbilityID)
                {
                    context.User.OnRemove();
                    context.User.HP = Math.Max(transform.StatusStates.GetWithDefault<HPState>().HP / 2, 1);
                    context.User.Dead = false;
                    context.User.DefeatAt = "";

                    //smoke poof
                    GameManager.Instance.BattleSE("DUN_Substitute");
                    SingleEmitter emitter = new SingleEmitter(new AnimData("Puff_Green", 3));
                    emitter.SetupEmit(context.User.MapLoc, context.User.MapLoc, context.User.CharDir);
                    DungeonScene.Instance.CreateAnim(emitter, DrawLayer.NoDraw);

                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_IMPOSTER").ToLocal(), context.User.GetDisplayName(false)));

                    yield break;
                }
            }
        }
    }

    [Serializable]
    public class MercyReviveEvent : SingleCharEvent
    {
        public bool AskToUse;
        public bool AffectPlayers;
        public bool AffectEnemies;
        public MercyReviveEvent() { }
        public MercyReviveEvent(bool askToUse, bool affectPlayers, bool affectEnemies)
        {
            AskToUse = askToUse;
            AffectPlayers = affectPlayers;
            AffectEnemies = affectEnemies;
        }
        protected MercyReviveEvent(MercyReviveEvent other)
        {
            AskToUse = other.AskToUse;
            AffectPlayers = other.AffectPlayers;
            AffectEnemies = other.AffectEnemies;
        }
        public override GameEvent Clone() { return new MercyReviveEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (!context.User.Dead)
                yield break;
            if (context.User.MemberTeam == DungeonScene.Instance.ActiveTeam && !AffectPlayers)
                yield break;
            if (context.User.MemberTeam != DungeonScene.Instance.ActiveTeam && !AffectEnemies)
                yield break;

            int choseRevive = 0;
            if (AskToUse && context.User.MemberTeam == DungeonScene.Instance.ActiveTeam)
            {
                if (DataManager.Instance.CurrentReplay != null)
                    choseRevive = DataManager.Instance.CurrentReplay.ReadUI();
                else
                {
                    yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.ProcessMenuCoroutine(MenuManager.Instance.CreateQuestion(Text.FormatGrammar(new StringKey("DLG_ASK_FREE_REVIVE").ToLocal(), context.User.GetDisplayName(false)), true, () => { choseRevive = 1; }, () => { choseRevive = 0; })));

                    DataManager.Instance.LogUIPlay(choseRevive);
                }
            }

            if (choseRevive != 0)
            {
                context.User.OnRemove();
                context.User.HP = context.User.MaxHP;
                context.User.Dead = false;
                context.User.DefeatAt = "";

                GameManager.Instance.BattleSE("DUN_Send_Home");
                SingleEmitter emitter = new SingleEmitter(new BeamAnimData("Column_Yellow", 3));
                emitter.Layer = DrawLayer.Front;
                emitter.SetupEmit(context.User.MapLoc, context.User.MapLoc, context.User.CharDir);
                DungeonScene.Instance.CreateAnim(emitter, DrawLayer.NoDraw);

                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_REVIVE").ToLocal(), context.User.GetDisplayName(false)));

            }
        }
    }



    [Serializable]
    public class AutoReviveEvent : SingleCharEvent
    {
        /// <summary>
        /// Asks to use the item.  Can be refused.
        /// </summary>
        public bool AskToUse;

        /// <summary>
        /// For ask to use only: defaults to automatically choosing yes for players and enemies that know how to use it.
        /// </summary>
        public bool DefaultYes;


        [JsonConverter(typeof(ItemConverter))]
        [DataType(0, DataManager.DataType.Item, false)]
        public string ChangeTo;

        public AutoReviveEvent() { ChangeTo = ""; }
        public AutoReviveEvent(bool askToUse, string changeTo)
        {
            AskToUse = askToUse;
            ChangeTo = changeTo;
        }
        protected AutoReviveEvent(AutoReviveEvent other)
        {
            AskToUse = other.AskToUse;
            DefaultYes = other.DefaultYes;
            ChangeTo = other.ChangeTo;
        }
        public override GameEvent Clone() { return new AutoReviveEvent(this); }

        private bool isAutoReviveItem(string itemId)
        {
            ItemData entry = DataManager.Instance.GetItem(itemId);

            foreach(SingleCharEvent effect in entry.OnDeaths.EnumerateInOrder())
            {
                if (effect is AutoReviveEvent)
                {
                    if (((AutoReviveEvent)effect).AskToUse == AskToUse)
                        return true;
                }
            }
            return false;
        }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (!context.User.Dead)
                yield break;

            string useIndex = "";
            int useSlot = BattleContext.NO_ITEM_SLOT;

            if (context.User.MemberTeam is ExplorerTeam)
            {
                ExplorerTeam team = context.User.MemberTeam as ExplorerTeam;
                List<string> candKeys = new List<string>();
                Dictionary<string, int> candidateItems = new Dictionary<string, int>();
                if (!String.IsNullOrEmpty(context.User.EquippedItem.ID) && !context.User.EquippedItem.Cursed)
                {
                    if (isAutoReviveItem(context.User.EquippedItem.ID))
                    {
                        candKeys.Add(context.User.EquippedItem.ID);
                        candidateItems.Add(context.User.EquippedItem.ID, BattleContext.EQUIP_ITEM_SLOT);
                    }
                }

                //iterate over the inventory, get a list of the lowest/highest-costing eligible items
                for (int ii = 0; ii < team.GetInvCount(); ii++)
                {
                    InvItem item = team.GetInv(ii);
                    if (!candidateItems.ContainsKey(item.ID))
                    {
                        if (isAutoReviveItem(item.ID) && !item.Cursed)
                        {
                            candKeys.Add(item.ID);
                            candidateItems.Add(item.ID, ii);
                        }
                    }
                }

                if (AskToUse && context.User.MemberTeam == DungeonScene.Instance.ActiveTeam)
                {
                    if (candidateItems.Count > 0)
                    {
                        if (DataManager.Instance.CurrentReplay != null)
                        {
                            int uiIndex = DataManager.Instance.CurrentReplay.ReadUI();
                            if (uiIndex > -1)
                            {
                                useIndex = candKeys[uiIndex];
                                useSlot = candidateItems[useIndex];
                            }
                        }
                        else if (DefaultYes)
                        {
                            useIndex = candKeys[0];
                            useSlot = candidateItems[useIndex];
                        }
                        else
                        {
                            int uiIndex = -1;
                            List<DialogueChoice> choices = new List<DialogueChoice>();
                            for (int ii = 0; ii < candKeys.Count; ii++)
                            {
                                int idx = ii;
                                string itemId = candKeys[ii];
                                ItemData entry = DataManager.Instance.GetItem(itemId);
                                choices.Add(new DialogueChoice(entry.GetIconName(), () =>
                                {
                                    uiIndex = idx;
                                    useIndex = itemId;
                                    useSlot = candidateItems[itemId];
                                }));
                            }
                            choices.Add(new DialogueChoice(Text.FormatKey("MENU_CANCEL"), () =>
                            {
                                uiIndex = -1;
                                useIndex = "";
                                useSlot = -1;
                            }));

                            yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.ProcessMenuCoroutine(MenuManager.Instance.CreateMultiQuestion(Text.FormatGrammar(new StringKey("DLG_ASK_REVIVE").ToLocal()), true, choices, 0, choices.Count - 1)));

                            DataManager.Instance.LogUIPlay(uiIndex);
                        }
                    }
                }
                else
                {
                    //use the reviver if the monster is an item master, or if the reviver doesn't ask to use
                    AIPlan plan = (AIPlan)context.User.Tactic.Plans[0];
                    if (!AskToUse || (plan.IQ & AIFlags.ItemMaster) != AIFlags.None)
                    {
                        foreach (string itemId in candidateItems.Keys)
                        {
                            useIndex = itemId;
                            useSlot = candidateItems[itemId];
                            break;
                        }
                    }
                }
            }
            else
            {
                AIPlan plan = (AIPlan)context.User.Tactic.Plans[0];
                if (!AskToUse || (plan.IQ & AIFlags.ItemMaster) != AIFlags.None)
                {
                    if (!String.IsNullOrEmpty(context.User.EquippedItem.ID) && !context.User.EquippedItem.Cursed)
                    {
                        if (isAutoReviveItem(context.User.EquippedItem.ID))
                        {
                            useIndex = context.User.EquippedItem.ID;
                            useSlot = BattleContext.EQUIP_ITEM_SLOT;
                        }
                    }
                }
            }

            if (!String.IsNullOrEmpty(useIndex))
            {
                context.User.OnRemove();
                context.User.HP = context.User.MaxHP;
                context.User.Dead = false;
                context.User.DefeatAt = "";

                GameManager.Instance.BattleSE("DUN_Send_Home");
                SingleEmitter emitter = new SingleEmitter(new BeamAnimData("Column_Yellow", 3));
                emitter.Layer = DrawLayer.Front;
                emitter.SetupEmit(context.User.MapLoc, context.User.MapLoc, context.User.CharDir);
                DungeonScene.Instance.CreateAnim(emitter, DrawLayer.NoDraw);

                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_REVIVE").ToLocal(), context.User.GetDisplayName(false)));


                ItemData entry = DataManager.Instance.GetItem(useIndex);

                string changeTo = "";
                foreach (SingleCharEvent effect in entry.OnDeaths.EnumerateInOrder())
                {
                    if (effect is AutoReviveEvent)
                    {
                        changeTo = ((AutoReviveEvent)effect).ChangeTo;
                        break;
                    }
                }

                //change the revival item to a plain item
                //find the first one not sticky
                //if target has a held item, and it's eligible, use it
                if (useSlot == BattleContext.EQUIP_ITEM_SLOT)
                {
                    if (!String.IsNullOrEmpty(changeTo))
                    {
                        context.User.EquippedItem.ID = ChangeTo;
                        yield return CoroutineManager.Instance.StartCoroutine(context.User.EquipItem(context.User.EquippedItem));
                    }
                    else
                        yield return CoroutineManager.Instance.StartCoroutine(context.User.DequipItem());
                }
                else if (context.User.MemberTeam is ExplorerTeam)
                {
                    ExplorerTeam team = (ExplorerTeam)context.User.MemberTeam;
                    if (!String.IsNullOrEmpty(changeTo))
                    {
                        InvItem oldItem = new InvItem(team.GetInv(useSlot).ID);
                        team.GetInv(useSlot).ID = ChangeTo;
                        team.UpdateInv(oldItem, team.GetInv(useSlot));
                    }
                    else
                        team.RemoveFromInv(useSlot);
                }

            }
        }
    }


    [Serializable]
    public class PartialTrapEvent : SingleCharEvent
    {
        [StringKey(0, true)]
        public StringKey Message;
        public List<AnimEvent> Anims;

        public PartialTrapEvent()
        {
            Anims = new List<AnimEvent>();
        }
        public PartialTrapEvent(StringKey message, params AnimEvent[] anims)
        {
            Message = message;
            Anims = new List<AnimEvent>();
            Anims.AddRange(anims);
        }
        protected PartialTrapEvent(PartialTrapEvent other)
        {
            Message = other.Message;
            Anims = new List<AnimEvent>();
            foreach (AnimEvent anim in other.Anims)
                Anims.Add((AnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new PartialTrapEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (context.User.Dead)
                yield break;
            if (context.User.CharStates.Contains<MagicGuardState>())
                yield break;
            
            if (Message.IsValid())
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), context.User.GetDisplayName(false), owner.GetDisplayName()));

            foreach (AnimEvent anim in Anims)
                yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

            int stack = ((StatusEffect)owner).StatusStates.GetWithDefault<CountState>().Count;
            int trapdmg = Math.Max(1, context.User.MaxHP * stack / 16);
            yield return CoroutineManager.Instance.StartCoroutine(context.User.InflictDamage(trapdmg));
            
        }
    }

    [Serializable]
    public class NightmareEvent : SingleCharEvent
    {
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string SleepID;
        public int Denominator;
        public StringKey Msg;
        public List<AnimEvent> Anims;

        public NightmareEvent()
        {
            Anims = new List<AnimEvent>();
            SleepID = "";
        }
        public NightmareEvent(string sleepID, int denominator, StringKey msg, params AnimEvent[] anims)
        {
            SleepID = sleepID;
            Denominator = denominator;
            Msg = msg;
            Anims = new List<AnimEvent>();
            Anims.AddRange(anims);
        }
        protected NightmareEvent(NightmareEvent other)
        {
            SleepID = other.SleepID;
            Denominator = other.Denominator;
            Msg = other.Msg;
            Anims = new List<AnimEvent>();
            foreach (AnimEvent anim in other.Anims)
                Anims.Add((AnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new NightmareEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (context.User.Dead)
                yield break;

            StatusEffect sleep = context.User.GetStatusEffect(SleepID);
            if (sleep != null)
            {
                if (Denominator < 0 && context.User.HP >= context.User.MaxHP)
                    yield break;

                DungeonScene.Instance.LogMsg(Text.FormatGrammar(Msg.ToLocal(), context.User.GetDisplayName(false), owner.GetDisplayName(), ownerChar.GetDisplayName(false)));

                foreach (AnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                if (Denominator < 0)
                    yield return CoroutineManager.Instance.StartCoroutine(context.User.RestoreHP(Math.Max(1, context.User.MaxHP / -Denominator), false));
                else
                    yield return CoroutineManager.Instance.StartCoroutine(context.User.InflictDamage(Math.Max(1, context.User.MaxHP / Denominator)));
            }
        }
    }

    [Serializable]
    public class LeechSeedEvent : SingleCharEvent
    {
        public StringKey Message;
        public int Range;
        public int HPFraction;

        public LeechSeedEvent(StringKey message, int range, int hpFraction)
        {
            Message = message;
            Range = range;
            HPFraction = hpFraction;
        }
        
        public LeechSeedEvent() { }
        
        protected LeechSeedEvent(LeechSeedEvent other)
        {
            Message = other.Message;
            Range = other.Range;
            HPFraction = other.HPFraction;
        }
        public override GameEvent Clone() { return new LeechSeedEvent(this); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (context.User.CharStates.Contains<MagicGuardState>())
                yield break;
            if (context.User.Dead)
                yield break;
                    
            //check for someone within X tiles away; if there's no one, then remove the status
            List<Character> targets = AreaAction.GetTargetsInArea(context.User, context.User.CharLoc, Alignment.Foe, Range);
            int lowestDist = Int32.MaxValue;
            Character target = null;
            for (int ii = 0; ii < targets.Count; ii++)
            {
                int newDist = (targets[ii].CharLoc - context.User.CharLoc).DistSquared();
                if (newDist < lowestDist)
                {
                    target = targets[ii];
                    lowestDist = newDist;
                }
            }

            if (target == null)
                yield return CoroutineManager.Instance.StartCoroutine(context.User.RemoveStatusEffect(((StatusEffect)owner).ID));
            else
            {
                int seeddmg = Math.Max(1, context.User.MaxHP / HPFraction);

                DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), context.User.GetDisplayName(false)));
                
                GameManager.Instance.BattleSE("DUN_Hit_Neutral");
                if (!context.User.Unidentifiable)
                {
                    SingleEmitter endEmitter = new SingleEmitter(new AnimData("Hit_Neutral", 3));
                    endEmitter.SetupEmit(context.User.MapLoc, context.User.MapLoc, context.User.CharDir);
                    DungeonScene.Instance.CreateAnim(endEmitter, DrawLayer.NoDraw);
                }

                yield return CoroutineManager.Instance.StartCoroutine(context.User.InflictDamage(seeddmg, false));

                DrainDamageState drainDamage;
                if (context.User.CharStates.TryGet<DrainDamageState>(out drainDamage))
                {
                    GameManager.Instance.BattleSE("DUN_Toxic");
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_LIQUID_OOZE").ToLocal(), target.GetDisplayName(false)));
                    yield return CoroutineManager.Instance.StartCoroutine(target.InflictDamage(seeddmg * drainDamage.Mult, false));
                }
                else if (target.HP < target.MaxHP)
                {
                    yield return CoroutineManager.Instance.StartCoroutine(target.RestoreHP(seeddmg, false));
                }
            }
        }
    }
    [Serializable]
    public class PursuitEvent : SingleCharEvent
    {
        public PursuitEvent() { }
        public override GameEvent Clone() { return new PursuitEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            Character chaser = ownerChar;
            if (chaser != null && !ZoneManager.Instance.CurrentMap.InRange(context.User.CharLoc, chaser.CharLoc, 1))
            {
                if (chaser.CharStates.Contains<AnchorState>())
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_CHASE_ANCHOR").ToLocal(), chaser.GetDisplayName(false), context.User.GetDisplayName(false)));
                else
                {
                    for (int ii = 0; ii < DirRemap.FOCUSED_DIR8.Length; ii++)
                    {
                        Dir8 dir = DirExt.AddAngles(DirRemap.FOCUSED_DIR8[ii], context.User.CharDir);
                        if (!ZoneManager.Instance.CurrentMap.DirBlocked(dir, context.User.CharLoc, chaser.Mobility))
                        {
                            Loc targetLoc = context.User.CharLoc + dir.GetLoc();
                            yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.PointWarp(chaser, targetLoc, false));
                            DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_CHASE").ToLocal(), chaser.GetDisplayName(false), context.User.GetDisplayName(false)));
                            break;
                        }
                    }
                }
            }
        }
    }
    [Serializable]
    public class EarlyBirdEvent : SingleCharEvent
    {
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string SleepID;
        public EarlyBirdEvent() { SleepID = ""; }
        public EarlyBirdEvent(string sleepID)
        {
            SleepID = sleepID;
        }
        protected EarlyBirdEvent(EarlyBirdEvent other)
        {
            SleepID = other.SleepID;
        }
        public override GameEvent Clone() { return new EarlyBirdEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            StatusEffect status = context.User.GetStatusEffect(SleepID);
            if (status != null)
            {
                CountDownState countdownState = status.StatusStates.GetWithDefault<CountDownState>();
                if (countdownState != null)
                    countdownState.Counter = 0;
            }
            yield break;
        }
    }
    [Serializable]
    public class BurnEvent : SingleCharEvent
    {
        public int HPFraction;
        
        public BurnEvent() { }
        public BurnEvent(int hpFraction)
        {
            HPFraction = hpFraction;
        }
        protected BurnEvent(BurnEvent other)
        {
            HPFraction = other.HPFraction;
        }
        public override GameEvent Clone() { return new BurnEvent(this); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (context.User.Dead)
                yield break;

            AttackedThisTurnState recent = ((StatusEffect)owner).StatusStates.GetWithDefault<AttackedThisTurnState>();
            if (recent.Attacked && !context.User.CharStates.Contains<HeatproofState>() && !context.User.CharStates.Contains<MagicGuardState>())
            {
                yield return CoroutineManager.Instance.StartCoroutine(context.User.InflictDamage(Math.Max(1, context.User.MaxHP / HPFraction), false));
                recent.Attacked = false;
            }
        }
    }


    [Serializable]
    public class WalkedThisTurnEvent : SingleCharEvent
    {
        public bool AffectNonFocused;

        public WalkedThisTurnEvent() { }
        public WalkedThisTurnEvent(bool affectNonFocused)
        {
            AffectNonFocused = affectNonFocused;
        }
        protected WalkedThisTurnEvent(WalkedThisTurnEvent other)
        {
            AffectNonFocused = other.AffectNonFocused;
        }
        public override GameEvent Clone() { return new WalkedThisTurnEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (AffectNonFocused || DungeonScene.Instance.CurrentCharacter == context.User)
            {
                WalkedThisTurnState recent = ((StatusEffect)owner).StatusStates.GetWithDefault<WalkedThisTurnState>();
                recent.Walked = true;
            }

            yield break;
        }
    }

    [Serializable]
    public class PoisonSingleEvent : SingleCharEvent
    {
        public bool Toxic;
        public bool AffectNonFocused;
        public int HPFraction;
        public int RestoreHPFraction;
        
        public PoisonSingleEvent() { }
        public PoisonSingleEvent(bool toxic, bool affectNonFocused, int hpFraction, int restoreHpFraction)
        {
            Toxic = toxic;
            AffectNonFocused = affectNonFocused;
            HPFraction = hpFraction;
            RestoreHPFraction = restoreHpFraction;
        }
        protected PoisonSingleEvent(PoisonSingleEvent other)
        {
            Toxic = other.Toxic;
            AffectNonFocused = other.AffectNonFocused;
            HPFraction = other.HPFraction;
            RestoreHPFraction = other.RestoreHPFraction;
        }
        public override GameEvent Clone() { return new PoisonSingleEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (context.User.Dead)
                yield break;

            if (!context.User.CharStates.Contains<MagicGuardState>() && (AffectNonFocused || DungeonScene.Instance.CurrentCharacter == context.User))
            {
                CountState countState = ((StatusEffect)owner).StatusStates.Get<CountState>();
                if (Toxic && countState.Count < HPFraction)
                    countState.Count++;
                if (context.User.CharStates.Contains<PoisonHealState>())
                {
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_POISON_HEAL").ToLocal(), context.User.GetDisplayName(false)));
                    yield return CoroutineManager.Instance.StartCoroutine(context.User.RestoreHP(Math.Max(1, context.User.MaxHP / RestoreHPFraction)));
                }
                else
                {
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_POISONED").ToLocal(), context.User.GetDisplayName(false)));
                    yield return CoroutineManager.Instance.StartCoroutine(context.User.InflictDamage(Math.Max(1, (context.User.MaxHP * countState.Count) / HPFraction)));
                }
            }
        }
    }

    [Serializable]
    public class PoisonEndEvent : SingleCharEvent
    {
        public bool Toxic;
        public bool ReducedDamage;
        public int HPFraction;
        public int HealHPFraction;

        public PoisonEndEvent() { }
        public PoisonEndEvent(bool toxic, bool reduce, int hpFraction, int healHpFraction)
        {
            Toxic = toxic;
            ReducedDamage = reduce;
            HPFraction = hpFraction;
            HealHPFraction = healHpFraction;
        }
        protected PoisonEndEvent(PoisonEndEvent other)
        {
            Toxic = other.Toxic;
            ReducedDamage = other.ReducedDamage;
            HPFraction = other.HPFraction;
        }
        public override GameEvent Clone() { return new PoisonEndEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (context.User.Dead)
                yield break;

            AttackedThisTurnState recentAttack = ((StatusEffect)owner).StatusStates.GetWithDefault<AttackedThisTurnState>();
            WalkedThisTurnState recentWalk = ((StatusEffect)owner).StatusStates.GetWithDefault<WalkedThisTurnState>();
            if (!recentAttack.Attacked && !recentWalk.Walked && !context.User.CharStates.Contains<MagicGuardState>())
            {
                CountState countState = ((StatusEffect)owner).StatusStates.Get<CountState>();
                if (Toxic && countState.Count < HPFraction)
                    countState.Count++;
                if (context.User.CharStates.Contains<PoisonHealState>())
                {
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_POISON_HEAL").ToLocal(), context.User.GetDisplayName(false)));
                    yield return CoroutineManager.Instance.StartCoroutine(context.User.RestoreHP(Math.Max(1, context.User.MaxHP / HealHPFraction)));
                }
                else
                {
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_POISONED").ToLocal(), context.User.GetDisplayName(false)));
                    int ticks = countState.Count;
                    if (ReducedDamage)
                        ticks--;
                    yield return CoroutineManager.Instance.StartCoroutine(context.User.InflictDamage(Math.Max(1, (context.User.MaxHP * ticks) / HPFraction)));
                }
            }
            recentAttack.Attacked = false;
            recentWalk.Walked = false;
        }
    }

    [Serializable]
    public class AlternateParalysisEvent : SingleCharEvent
    {
        public override GameEvent Clone() { return new AlternateParalysisEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            ParalyzeState para = ((StatusEffect)owner).StatusStates.GetWithDefault<ParalyzeState>();
            para.Recent = !para.Recent;
            context.User.RefreshTraits();
            yield break;
        }
    }

    [Serializable]
    public class MaxHPNeededEvent : SingleCharEvent
    {
        public SingleCharEvent BaseEvent;

        public MaxHPNeededEvent() { }
        public MaxHPNeededEvent(SingleCharEvent baseEffect) { BaseEvent = baseEffect; }
        protected MaxHPNeededEvent(MaxHPNeededEvent other)
        {
            BaseEvent = (SingleCharEvent)other.BaseEvent.Clone();
        }
        public override GameEvent Clone() { return new MaxHPNeededEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (context.User.HP == context.User.MaxHP)
                yield return CoroutineManager.Instance.StartCoroutine(BaseEvent.Apply(owner, ownerChar, context));
        }
    }

    [Serializable]
    public class WeatherNeededSingleEvent : SingleCharEvent
    {
        [JsonConverter(typeof(MapStatusConverter))]
        public string WeatherID;
        public SingleCharEvent BaseEvent;

        public WeatherNeededSingleEvent() { WeatherID = ""; }
        public WeatherNeededSingleEvent(string id, SingleCharEvent baseEffect) { WeatherID = id; BaseEvent = baseEffect; }
        protected WeatherNeededSingleEvent(WeatherNeededSingleEvent other)
        {
            WeatherID = other.WeatherID;
            BaseEvent = (SingleCharEvent)other.BaseEvent.Clone();
        }
        public override GameEvent Clone() { return new WeatherNeededSingleEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (ZoneManager.Instance.CurrentMap.Status.ContainsKey(WeatherID))
                yield return CoroutineManager.Instance.StartCoroutine(BaseEvent.Apply(owner, ownerChar, context));
        }
    }

    [Serializable]
    public class RegeneratorEvent : SingleCharEvent
    {
        public int Range;
        public int HPFraction;

        public RegeneratorEvent() { }
        public RegeneratorEvent(int range, int hpFraction) { 
            Range = range;
            HPFraction = hpFraction;
        }
        protected RegeneratorEvent(RegeneratorEvent other)
        {
            Range = other.Range;
            HPFraction = other.HPFraction;
        }
        public override GameEvent Clone() { return new RegeneratorEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (context.User.Dead)
                yield break;

            foreach (Character target in ZoneManager.Instance.CurrentMap.GetCharsInFillRect(context.User.CharLoc, Rect.FromPointRadius(context.User.CharLoc, Range)))
            {
                if (!target.Dead && DungeonScene.Instance.GetMatchup(context.User, target) == Alignment.Foe)
                    yield break;
            }
            if (context.User.HP < context.User.MaxHP)
                yield return CoroutineManager.Instance.StartCoroutine(context.User.RestoreHP(Math.Max(1, context.User.MaxHP / HPFraction), false));
        }
    }

    [Serializable]
    public class RoyalVeilEvent : SingleCharEvent
    {
        public int Range;
        public int HealHPFraction;

        public RoyalVeilEvent() { }

        public RoyalVeilEvent(int range, int healHpFraction)
        {
            Range = range;
            HealHPFraction = healHpFraction;
        }
        protected RoyalVeilEvent(RoyalVeilEvent other)
        {
            Range = other.Range;
            HealHPFraction = other.HealHPFraction;
        }
        public override GameEvent Clone() { return new RoyalVeilEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (context.User.HP == context.User.MaxHP)
            {
                foreach (Character target in ZoneManager.Instance.CurrentMap.GetCharsInFillRect(context.User.CharLoc, Rect.FromPointRadius(context.User.CharLoc, Range)))
                {
                    if (!target.Dead && DungeonScene.Instance.GetMatchup(context.User, target) == Alignment.Friend)
                    {
                        if (target.HP < target.MaxHP)
                            yield return CoroutineManager.Instance.StartCoroutine(target.RestoreHP(Math.Max(1, target.MaxHP / HealHPFraction), false));
                    }
                }

            }
        }
    }

    [Serializable]
    public class ChanceEvent : SingleCharEvent
    {
        public int Chance;
        public SingleCharEvent BaseEvent;

        public ChanceEvent() { }
        public ChanceEvent(int chance, SingleCharEvent baseEffect) { Chance = chance;  BaseEvent = baseEffect; }
        protected ChanceEvent(ChanceEvent other)
        {
            Chance = other.Chance;
            BaseEvent = (SingleCharEvent)other.BaseEvent.Clone();
        }
        public override GameEvent Clone() { return new ChanceEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (DataManager.Instance.Save.Rand.Next(100) < Chance)
                yield return CoroutineManager.Instance.StartCoroutine(BaseEvent.Apply(owner, ownerChar, context));
        }
    }

    [Serializable]
    public class CureAllEvent : SingleCharEvent
    {
        [StringKey(0, true)]
        public StringKey Message;
        public List<AnimEvent> Anims;

        public CureAllEvent()
        {
            Anims = new List<AnimEvent>();
        }
        public CureAllEvent(StringKey msg, params AnimEvent[] anims)
        {
            Message = msg;
            Anims = new List<AnimEvent>();
            Anims.AddRange(anims);
        }
        protected CureAllEvent(CureAllEvent other)
        {
            Message = other.Message;
            Anims = new List<AnimEvent>();
            foreach (AnimEvent anim in other.Anims)
                Anims.Add((AnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new CureAllEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            List<string> badStatuses = new List<string>();
            foreach (StatusEffect status in context.User.IterateStatusEffects())
            {
                if (status.StatusStates.Contains<BadStatusState>())
                    badStatuses.Add(status.ID);
            }

            if (badStatuses.Count > 0)
            {
                if (Message.IsValid())
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), context.User.GetDisplayName(false), owner.GetDisplayName()));

                foreach (AnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));
            }

            foreach (string statusID in badStatuses)
                yield return CoroutineManager.Instance.StartCoroutine(context.User.RemoveStatusEffect(statusID, false));

        }
    }


    [Serializable]
    public class AllyReviverEvent : SingleCharEvent
    {
        public AllyReviverEvent() { }
        public override GameEvent Clone() { return new AllyReviverEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            foreach (Character member in context.User.MemberTeam.EnumerateChars())
            {
                if (member.Dead)
                {
                    Loc? endLoc = ZoneManager.Instance.CurrentMap.GetClosestTileForChar(member, context.User.CharLoc);
                    if (endLoc == null)
                        endLoc = context.User.CharLoc;
                    member.CharLoc = endLoc.Value;

                    member.HP = 1;
                    member.Dead = false;
                    member.DefeatAt = "";

                    ZoneManager.Instance.CurrentMap.UpdateExploration(member);

                    GameManager.Instance.BattleSE("DUN_Send_Home");
                    SingleEmitter emitter = new SingleEmitter(new BeamAnimData("Column_Yellow", 3));
                    emitter.Layer = DrawLayer.Front;
                    emitter.SetupEmit(member.MapLoc, member.MapLoc, member.CharDir);
                    DungeonScene.Instance.CreateAnim(emitter, DrawLayer.NoDraw);

                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_REVIVE").ToLocal(), member.GetDisplayName(false)));

                    yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(20));

                    //revive only 1
                    break;
                }
            }
        }
    }


    [Serializable]
    public class CompassEvent : SingleCharEvent
    {
        public FiniteEmitter Emitter;

        /// <summary>
        /// Tiles eligible to be pointed to.
        /// </summary>
        [JsonConverter(typeof(TileListConverter))]
        [DataType(1, DataManager.DataType.Tile, false)]
        public List<string> EligibleTiles;

        public CompassEvent()
        {
            Emitter = new EmptyFiniteEmitter();
            EligibleTiles = new List<string>();
        }
        public CompassEvent(FiniteEmitter emitter, params string[] eligibles)
        {
            Emitter = emitter;
            EligibleTiles = new List<string>();
            EligibleTiles.AddRange(eligibles);
        }
        protected CompassEvent(CompassEvent other)
        {
            Emitter = other.Emitter;
        }
        public override GameEvent Clone() { return new CompassEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            EffectTile effectTile = (EffectTile)owner;
            TileListState destState = effectTile.TileStates.GetWithDefault<TileListState>();

            if (destState == null)
                yield break;

            CharAnimation standAnim = new CharAnimIdle(context.User.CharLoc, context.User.CharDir);
            standAnim.MajorAnim = true;
            yield return CoroutineManager.Instance.StartCoroutine(context.User.StartAnim(standAnim));

            GameManager.Instance.BattleSE("DUN_Tile_Step");
            effectTile.Revealed = true;

            TileData entry = DataManager.Instance.GetTile(owner.GetID());
            DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_TILE_CHECK").ToLocal(), context.User.GetDisplayName(false), entry.Name.ToLocal()));

            yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(30));

            DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_TREASURE_SENSOR").ToLocal()));

            foreach (Loc loc in destState.Tiles)
            {
                Tile tile = ZoneManager.Instance.CurrentMap.GetTile(loc);
                if (!EligibleTiles.Contains(tile.Effect.ID))
                    continue;

                Dir8 stairsDir = ZoneManager.Instance.CurrentMap.ApproximateClosestDir8(context.User.CharLoc, loc);
                if (stairsDir == Dir8.None)
                    continue;

                FiniteEmitter endEmitter = (FiniteEmitter)Emitter.Clone();
                endEmitter.SetupEmit(context.User.MapLoc + stairsDir.GetLoc() * 16, context.User.MapLoc + stairsDir.GetLoc() * 16, stairsDir);
                DungeonScene.Instance.CreateAnim(endEmitter, DrawLayer.NoDraw);
            }

            yield break;
        }
    }


    [Serializable]
    public class StairSensorEvent : SingleCharEvent
    {
        [JsonConverter(typeof(MapStatusConverter))]
        [DataType(0, DataManager.DataType.MapStatus, false)]
        public string SniffedStatusID;
        public FiniteEmitter Emitter;

        public StairSensorEvent()
        {
            Emitter = new EmptyFiniteEmitter();
            SniffedStatusID = "";
        }
        public StairSensorEvent(string sniffedID, FiniteEmitter emitter)
        {
            SniffedStatusID = sniffedID;
            Emitter = emitter;
        }
        protected StairSensorEvent(StairSensorEvent other)
        {
            SniffedStatusID = other.SniffedStatusID;
            Emitter = other.Emitter;
        }
        public override GameEvent Clone() { return new StairSensorEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (!ZoneManager.Instance.CurrentMap.Status.ContainsKey(SniffedStatusID))
            {
                Loc? loc = Grid.FindClosestConnectedTile(context.User.CharLoc - new Loc(CharAction.MAX_RANGE), new Loc(CharAction.MAX_RANGE * 2 + 1),
                    (Loc testLoc) => {

                        Tile tile = ZoneManager.Instance.CurrentMap.GetTile(testLoc);
                        if (tile == null)
                            return false;

                        if (tile.Effect.ID == "stairs_go_up" || tile.Effect.ID == "stairs_go_down")//TODO: remove this magic number
                            return true;
                        return false;
                    },
                    (Loc testLoc) => {
                        return ZoneManager.Instance.CurrentMap.TileBlocked(testLoc, true);
                    },
                    (Loc testLoc) => {
                        return ZoneManager.Instance.CurrentMap.TileBlocked(testLoc, true, true);
                    },
                    context.User.CharLoc);

                if (loc != null && loc != context.User.CharLoc)
                {
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_STAIR_SENSOR").ToLocal(), ownerChar.GetDisplayName(false), owner.GetDisplayName()));

                    Dir8 stairsDir = ZoneManager.Instance.CurrentMap.ApproximateClosestDir8(context.User.CharLoc, loc.Value);

                    FiniteEmitter endEmitter = (FiniteEmitter)Emitter.Clone();
                    endEmitter.SetupEmit(context.User.MapLoc + stairsDir.GetLoc() * 16, context.User.MapLoc + stairsDir.GetLoc() * 16, stairsDir);
                    DungeonScene.Instance.CreateAnim(endEmitter, DrawLayer.NoDraw);

                }

                MapStatus status = new MapStatus(SniffedStatusID);
                status.LoadFromData();
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.AddMapStatus(status, false));
            }
        }
    }


    [Serializable]
    public class HintTempTileEvent : SingleCharEvent
    {
        public FiniteEmitter Emitter;
        public StringKey HintMsg;

        public HintTempTileEvent() { }
        public HintTempTileEvent(StringKey msg, FiniteEmitter emitter)
        {
            HintMsg = msg;
            Emitter = emitter;
        }
        public HintTempTileEvent(HintTempTileEvent other)
        {
            HintMsg = other.HintMsg;
            Emitter = other.Emitter;
        }
        public override GameEvent Clone() { return new HintTempTileEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (context.User == null)
            {
                Character targetChar = DungeonScene.Instance.ActiveTeam.Leader;
                MapLocState locState = ((MapStatus)owner).StatusStates.GetWithDefault<MapLocState>();
                if (locState != null)
                {
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(HintMsg.ToLocal()));

                    Dir8 stairsDir = ZoneManager.Instance.CurrentMap.ApproximateClosestDir8(targetChar.CharLoc, locState.Target);

                    FiniteEmitter endEmitter = (FiniteEmitter)Emitter.Clone();
                    endEmitter.SetupEmit(targetChar.MapLoc + stairsDir.GetLoc() * 16, targetChar.MapLoc + stairsDir.GetLoc() * 16, stairsDir);
                    DungeonScene.Instance.CreateAnim(endEmitter, DrawLayer.NoDraw);
                }
            }
            yield break;
        }
    }

    [Serializable]
    public class AcuteSnifferEvent : SingleCharEvent
    {
        [JsonConverter(typeof(MapStatusConverter))]
        [DataType(0, DataManager.DataType.MapStatus, false)]
        public string SniffedStatusID;
        public List<AnimEvent> Anims;

        public AcuteSnifferEvent()
        {
            Anims = new List<AnimEvent>();
            SniffedStatusID = "";
        }
        public AcuteSnifferEvent(string sniffedID, params AnimEvent[] anims)
        {
            SniffedStatusID = sniffedID;
            Anims = new List<AnimEvent>();
            Anims.AddRange(anims);
        }
        protected AcuteSnifferEvent(AcuteSnifferEvent other)
        {
            SniffedStatusID = other.SniffedStatusID;
            Anims = new List<AnimEvent>();
            foreach (AnimEvent anim in other.Anims)
                Anims.Add((AnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new AcuteSnifferEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (!ZoneManager.Instance.CurrentMap.Status.ContainsKey(SniffedStatusID))
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_ACUTE_SNIFFER").ToLocal(), ownerChar.GetDisplayName(false), owner.GetDisplayName(), ZoneManager.Instance.CurrentMap.Items.Count));

                foreach (AnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                MapStatus status = new MapStatus(SniffedStatusID);
                status.LoadFromData();
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.AddMapStatus(status, false));
            }
        }
    }



    [Serializable]
    public class MapSurveyorEvent : SingleCharEvent
    {
        [JsonConverter(typeof(MapStatusConverter))]
        [DataType(0, DataManager.DataType.MapStatus, false)]
        public string SniffedStatusID;
        public int Radius;

        public MapSurveyorEvent()
        { SniffedStatusID = ""; }
        public MapSurveyorEvent(string sniffedID, int radius)
        {
            SniffedStatusID = sniffedID;
            Radius = radius;
        }
        protected MapSurveyorEvent(MapSurveyorEvent other)
        {
            SniffedStatusID = other.SniffedStatusID;
            Radius = other.Radius;
        }
        public override GameEvent Clone() { return new MapSurveyorEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (!ZoneManager.Instance.CurrentMap.Status.ContainsKey(SniffedStatusID))
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
                    yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(4));
                }


                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_MAP_SURVEYOR").ToLocal(), ownerChar.GetDisplayName(false), owner.GetDisplayName()));
            }
        }
    }



    [Serializable]
    public class RevealAllEvent : SingleCharEvent
    {
        public RevealAllEvent() { }
        public override GameEvent Clone() { return new RevealAllEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            for (int xx = 0; xx < ZoneManager.Instance.CurrentMap.Width; xx++)
            {
                for (int yy = 0; yy < ZoneManager.Instance.CurrentMap.Height; yy++)
                {
                    ZoneManager.Instance.CurrentMap.DiscoveryArray[xx][yy] = Map.DiscoveryState.Traversed;
                }
            }
            yield break;
        }
    }


    [Serializable]
    public class GiveMapStatusSingleEvent : SingleCharEvent
    {
        [JsonConverter(typeof(MapStatusConverter))]
        [DataType(0, DataManager.DataType.MapStatus, false)]
        public string StatusID;
        public int Counter;
        [StringKey(0, true)]
        public StringKey MsgOverride;

        public GiveMapStatusSingleEvent() { StatusID = ""; }
        public GiveMapStatusSingleEvent(string id)
        {
            StatusID = id;
        }
        public GiveMapStatusSingleEvent(string id, int counter)
        {
            StatusID = id;
            Counter = counter;
        }
        public GiveMapStatusSingleEvent(string id, int counter, StringKey msg)
        {
            StatusID = id;
            Counter = counter;
            MsgOverride = msg;
        }
        protected GiveMapStatusSingleEvent(GiveMapStatusSingleEvent other)
            : this()
        {
            StatusID = other.StatusID;
            Counter = other.Counter;
            MsgOverride = other.MsgOverride;
        }
        public override GameEvent Clone() { return new GiveMapStatusSingleEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            //add the map status
            MapStatus status = new MapStatus(StatusID);
            status.LoadFromData();
            if (Counter != 0)
                status.StatusStates.GetWithDefault<MapCountDownState>().Counter = Counter;

            if (!MsgOverride.IsValid())
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.AddMapStatus(status));
            else
            {
                //message only if the status isn't already there
                MapStatus statusToCheck;
                if (!ZoneManager.Instance.CurrentMap.Status.TryGetValue(status.ID, out statusToCheck))
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(MsgOverride.ToLocal(), ownerChar.GetDisplayName(false)));
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.AddMapStatus(status, false));
            }
        }
    }


    [Serializable]
    public class PickupEvent : SingleCharEvent
    {
        public int Chance;
        public List<AnimEvent> Anims;

        public PickupEvent()
        {
            Anims = new List<AnimEvent>();
        }
        public PickupEvent(int chance, params AnimEvent[] anims)
        {
            Chance = chance;
            Anims = new List<AnimEvent>();
            Anims.AddRange(anims);
        }
        protected PickupEvent(PickupEvent other)
        {
            Anims = new List<AnimEvent>();
            Chance = other.Chance;
            foreach (AnimEvent anim in other.Anims)
                Anims.Add((AnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new PickupEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            //do not activate if already holding an item
            if (!String.IsNullOrEmpty(context.User.EquippedItem.ID))
                yield break;

            //do not activate if inv is full
            if (context.User.MemberTeam is ExplorerTeam)
            {
                if (((ExplorerTeam)context.User.MemberTeam).GetMaxInvSlots(ZoneManager.Instance.CurrentZone) <= context.User.MemberTeam.GetInvCount())
                    yield break;
            }

            if (ZoneManager.Instance.CurrentMap.MapTurns == 0 && ZoneManager.Instance.CurrentMap.ItemSpawns.Spawns.CanPick && DataManager.Instance.Save.Rand.Next(100) < Chance)
            {
                InvItem item = ZoneManager.Instance.CurrentMap.ItemSpawns.Pick(DataManager.Instance.Save.Rand);

                //Actually, we'll just let you pickup an autocurse item and get stuck
                //ItemData entry = DataManager.Instance.GetItem(item.ID);
                //if (!entry.Cursed)
                //{
                //item.Cursed = false;
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_PICKUP").ToLocal(), context.User.GetDisplayName(false), item.GetDisplayName()), false, false, context.User, null);

                foreach (AnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                yield return CoroutineManager.Instance.StartCoroutine(context.User.EquipItem(item));
                //}
            }

        }
    }

    [Serializable]
    public class GatherEvent : SingleCharEvent
    {
        [DataType(1, DataManager.DataType.Item, false)]
        public List<string> GatherItems;
        public int Chance;
        public List<AnimEvent> Anims;

        public GatherEvent()
        {
            Anims = new List<AnimEvent>();
            GatherItems = new List<string>();
        }
        public GatherEvent(List<string> gatherItem, int chance, params AnimEvent[] anims)
        {
            GatherItems = gatherItem;
            Chance = chance;
            Anims = new List<AnimEvent>();
            Anims.AddRange(anims);
        }
        protected GatherEvent(GatherEvent other)
        {
            Anims = new List<AnimEvent>();
            Chance = other.Chance;
            foreach (AnimEvent anim in other.Anims)
                Anims.Add((AnimEvent)anim.Clone());
            GatherItems = new List<string>();
            GatherItems.AddRange(other.GatherItems);
        }
        public override GameEvent Clone() { return new GatherEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            //do not activate if already holding an item
            if (!String.IsNullOrEmpty(context.User.EquippedItem.ID))
                yield break;

            //do not activate if inv is full
            if (context.User.MemberTeam is ExplorerTeam)
            {
                if (((ExplorerTeam)context.User.MemberTeam).GetMaxInvSlots(ZoneManager.Instance.CurrentZone) <= context.User.MemberTeam.GetInvCount())
                    yield break;
            }

            if (ZoneManager.Instance.CurrentMap.MapTurns == 0 && DataManager.Instance.Save.Rand.Next(100) < Chance)
            {
                string gatherItem = GatherItems[DataManager.Instance.Save.Rand.Next(GatherItems.Count)];
                InvItem invItem = new InvItem(gatherItem);
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_PICKUP").ToLocal(), context.User.GetDisplayName(false), invItem.GetDisplayName()), false, false, context.User, null);

                foreach (AnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                yield return CoroutineManager.Instance.StartCoroutine(context.User.EquipItem(invItem));
            }
        }
    }

    [Serializable]
    public class DeepBreathEvent : SingleCharEvent
    {
        public bool RestoreAll;

        public DeepBreathEvent() { }

        public DeepBreathEvent(bool restoreAll)
        {
            RestoreAll = restoreAll;
        }

        public override GameEvent Clone() { return new DeepBreathEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            int minSlot = -1;
            int minAmount = 100;
            bool canRecover = false;
            for (int ii = 0; ii < context.User.Skills.Count; ii++)
            {
                if (!String.IsNullOrEmpty(context.User.Skills[ii].Element.SkillNum))
                {
                    SkillData data = DataManager.Instance.GetSkill(context.User.Skills[ii].Element.SkillNum);
                    if (context.User.Skills[ii].Element.Charges < data.BaseCharges + context.User.ChargeBoost)
                    {
                        if (context.User.Skills[ii].Element.Charges < minAmount)
                        {
                            minSlot = ii;
                            minAmount = context.User.Skills[ii].Element.Charges;
                        }
                        canRecover = true;
                    }
                }
            }

            if (RestoreAll)
            {
                if (canRecover)
                {
                    yield return CoroutineManager.Instance.StartCoroutine(context.User.RestoreCharges(-1, 1, true, false));
                }
            }
            else
            {
                if (minSlot > -1)
                {
                    yield return CoroutineManager.Instance.StartCoroutine(context.User.RestoreCharges(minSlot, 1, true, false));
                }
            }
        }
    }


    [Serializable]
    public class PlateElementEvent : SingleCharEvent
    {
        [JsonConverter(typeof(ItemElementDictConverter))]
        [DataType(1, DataManager.DataType.Item, false)]
        [DataType(2, DataManager.DataType.Element, false)]
        public Dictionary<string, string> TypePair;

        public PlateElementEvent() { TypePair = new Dictionary<string, string>(); }
        public PlateElementEvent(Dictionary<string, string> typePair)
        {
            TypePair = typePair;
        }
        protected PlateElementEvent(PlateElementEvent other)
            : this()
        {
            foreach (string plate in other.TypePair.Keys)
                TypePair.Add(plate, other.TypePair[plate]);
        }
        public override GameEvent Clone() { return new PlateElementEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            string element;
            if (!TypePair.TryGetValue(context.User.EquippedItem.ID, out element))
                element = "normal";

            if (!(context.User.Element1 == element && context.User.Element2 == DataManager.Instance.DefaultElement))
                yield return CoroutineManager.Instance.StartCoroutine(context.User.ChangeElement(element, DataManager.Instance.DefaultElement));
        }
    }

    [Serializable]
    public class GiveIllusionEvent : SingleCharEvent
    {
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string IllusionID;

        public GiveIllusionEvent() { IllusionID = ""; }
        public GiveIllusionEvent(string illusionID)
        {
            IllusionID = illusionID;
        }
        protected GiveIllusionEvent(GiveIllusionEvent other)
        {
            IllusionID = other.IllusionID;
        }
        public override GameEvent Clone() { return new GiveIllusionEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (ZoneManager.Instance.CurrentMap.TeamSpawns.CanPick)
            {
                TeamSpawner spawner = ZoneManager.Instance.CurrentMap.TeamSpawns.Pick(DataManager.Instance.Save.Rand);
                SpawnList<MobSpawn> candidateSpawns = spawner.GetPossibleSpawns();
                List<MonsterID> candidateSpecies = new List<MonsterID>();
                foreach (var spawn in candidateSpawns)
                {
                    MonsterID id = spawn.Spawn.BaseForm;
                    if (id.Species != context.User.BaseForm.Species)
                        candidateSpecies.Add(id);
                }

                if (candidateSpecies.Count > 0)
                {
                    StatusEffect status = new StatusEffect(IllusionID);
                    status.LoadFromData();
                    MonsterID id = candidateSpecies[DataManager.Instance.Save.Rand.Next(candidateSpecies.Count)];
                    id.Form = Math.Max(0, id.Form);
                    id.Skin = String.IsNullOrEmpty(id.Skin) ? DataManager.Instance.DefaultSkin : id.Skin;
                    if (id.Gender == Gender.Unknown)
                    {
                        MonsterData monData = DataManager.Instance.GetMonster(id.Species);
                        BaseMonsterForm form = monData.Forms[id.Form];
                        id.Gender = form.RollGender(DataManager.Instance.Save.Rand);
                    }
                    status.StatusStates.Set(new MonsterIDState(id));
                    if (context.User.MemberTeam == DungeonScene.Instance.ActiveTeam)
                        DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_ILLUSION_START").ToLocal(), context.User.GetDisplayName(true)));
                    yield return CoroutineManager.Instance.StartCoroutine(context.User.AddStatusEffect(status));
                }
            }
        }
    }

    [Serializable]
    public class WeatherAlignedEvent : SingleCharEvent
    {
        [JsonConverter(typeof(MapStatusConverter))]
        public string BadWeatherID;
        [JsonConverter(typeof(MapStatusConverter))]
        public string GoodWeatherID;

        public WeatherAlignedEvent() { BadWeatherID = ""; GoodWeatherID = ""; }
        public WeatherAlignedEvent(string badId, string goodId) { BadWeatherID = badId; GoodWeatherID = goodId; }
        protected WeatherAlignedEvent(WeatherAlignedEvent other)
        {
            BadWeatherID = other.BadWeatherID;
            GoodWeatherID = other.GoodWeatherID;
        }
        public override GameEvent Clone() { return new WeatherAlignedEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            int hp = Math.Max(1, context.User.MaxHP / 12);
            if (ZoneManager.Instance.CurrentMap.Status.ContainsKey(BadWeatherID))
            {
                if (context.User.CharStates.Contains<MagicGuardState>())
                    yield break;

                MapStatus status = ZoneManager.Instance.CurrentMap.Status[BadWeatherID];
                if (status.StatusStates.GetWithDefault<MapTickState>().Counter % 5 == 0)
                    yield return CoroutineManager.Instance.StartCoroutine(context.User.InflictDamage(hp, false));
            }
            else if (ZoneManager.Instance.CurrentMap.Status.ContainsKey(GoodWeatherID))
            {
                if (context.User.HP < context.User.MaxHP)
                {
                    MapStatus status = ZoneManager.Instance.CurrentMap.Status[GoodWeatherID];
                    if (status.StatusStates.GetWithDefault<MapTickState>().Counter % 5 == 0)
                        yield return CoroutineManager.Instance.StartCoroutine(context.User.RestoreHP(hp, false));
                }
            }

        }
    }



    [Serializable]
    public class WeatherFormeSingleEvent : SingleCharEvent
    {
        [JsonConverter(typeof(MonsterConverter))]
        [DataType(0, DataManager.DataType.Monster, false)]
        public string ReqSpecies;
        public int DefaultForme;
        [JsonConverter(typeof(MapStatusIntDictConverter))]
        public Dictionary<string, int> WeatherPair;

        public List<AnimEvent> Anims;

        public WeatherFormeSingleEvent() { WeatherPair = new Dictionary<string, int>(); ReqSpecies = ""; Anims = new List<AnimEvent>(); }
        public WeatherFormeSingleEvent(string reqSpecies, int defaultForme, Dictionary<string, int> weather, params AnimEvent[] anims)
        {
            ReqSpecies = reqSpecies;
            DefaultForme = defaultForme;
            WeatherPair = weather;
            Anims = new List<AnimEvent>();
            Anims.AddRange(anims);
        }
        protected WeatherFormeSingleEvent(WeatherFormeSingleEvent other) : this()
        {
            ReqSpecies = other.ReqSpecies;
            DefaultForme = other.DefaultForme;

            foreach (string weather in other.WeatherPair.Keys)
                WeatherPair.Add(weather, other.WeatherPair[weather]);

            Anims = new List<AnimEvent>();
            foreach (AnimEvent anim in other.Anims)
                Anims.Add((AnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new WeatherFormeSingleEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (context.User == null)
                yield break;

            if (context.User.CurrentForm.Species != ReqSpecies)
                yield break;

            //get the forme it should be in
            int forme = DefaultForme;

            foreach (string weather in WeatherPair.Keys)
            {
                if (ZoneManager.Instance.CurrentMap.Status.ContainsKey(weather))
                {
                    forme = WeatherPair[weather];
                    break;
                }
            }

            if (forme < 0)
                yield break;

            if (forme != context.User.CurrentForm.Form)
            {
                //transform it
                context.User.Transform(new MonsterID(context.User.CurrentForm.Species, forme, context.User.CurrentForm.Skin, context.User.CurrentForm.Gender));

                foreach (AnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_FORM_CHANGE").ToLocal(), context.User.GetDisplayName(false)));
            }

            yield break;
        }
    }



    [Serializable]
    public class WeatherDamageEvent : SingleCharEvent
    {
        [StringTypeConstraint(1, typeof(CharState))]
        public List<FlagType> States;
        [JsonConverter(typeof(ElementSetConverter))]
        [DataType(1, DataManager.DataType.Element, false)]
        public HashSet<string> ExceptionElements;

        public WeatherDamageEvent() { States = new List<FlagType>(); ExceptionElements = new HashSet<string>(); }
        public WeatherDamageEvent(Type[] state, params string[] elements)
            : this()
        {
            foreach (Type stateType in state)
                States.Add(new FlagType(stateType));
            foreach (string element in elements)
                ExceptionElements.Add(element);
        }
        protected WeatherDamageEvent(WeatherDamageEvent other)
            : this()
        {
            States.AddRange(other.States);
            foreach (string element in other.ExceptionElements)
                ExceptionElements.Add(element);
        }
        public override GameEvent Clone() { return new WeatherDamageEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (context.User != null && ((MapStatus)owner).StatusStates.GetWithDefault<MapTickState>().Counter % 5 == 0)
            {
                foreach (FlagType state in States)
                {
                    if (context.User.CharStates.Contains(state.FullType))
                        yield break;
                }

                foreach (string element in ExceptionElements)
                {
                    if (context.User.HasElement(element))
                        yield break;
                }

                yield return CoroutineManager.Instance.StartCoroutine(context.User.InflictDamage(Math.Max(1, context.User.MaxHP / 12), false));
            }
        }

    }

    [Serializable]
    public class WeatherHealEvent : SingleCharEvent
    {
        [JsonConverter(typeof(ElementSetConverter))]
        [DataType(1, DataManager.DataType.Element, false)]
        public HashSet<string> ExceptionElements;

        public WeatherHealEvent() { ExceptionElements = new HashSet<string>(); }
        public WeatherHealEvent(params string[] elements)
            : this()
        {
            foreach (string element in elements)
                ExceptionElements.Add(element);
        }
        protected WeatherHealEvent(WeatherHealEvent other)
            : this()
        {
            foreach (string element in other.ExceptionElements)
                ExceptionElements.Add(element);
        }
        public override GameEvent Clone() { return new WeatherHealEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (context.User != null && ((MapStatus)owner).StatusStates.GetWithDefault<MapTickState>().Counter % 5 == 0)
            {
                foreach (string element in ExceptionElements)
                {
                    if (context.User.HasElement(element))
                        yield break;
                }

                if (context.User.HP < context.User.MaxHP)
                    yield return CoroutineManager.Instance.StartCoroutine(context.User.RestoreHP(Math.Max(1, context.User.MaxHP / 12), false));
            }
        }
    }

    [Serializable]
    public class TeamHungerEvent : SingleCharEvent
    {
        public int HungerAmount;

        public TeamHungerEvent() { }
        public TeamHungerEvent(int amt)
        {
            HungerAmount = amt;
        }
        protected TeamHungerEvent(TeamHungerEvent other)
        {
            HungerAmount = other.HungerAmount;
        }
        public override GameEvent Clone() { return new TeamHungerEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (context.User.MemberTeam is ExplorerTeam)
                context.User.FullnessRemainder += HungerAmount;
            yield break;
        }
    }


    [Serializable]
    public class WeatherFillEvent : SingleCharEvent
    {
        public WeatherFillEvent() { }
        public override GameEvent Clone() { return new WeatherFillEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (context.User == null)
            {
                bool hasWeather = false;
                foreach (MapStatus removeStatus in ZoneManager.Instance.CurrentMap.Status.Values)
                {
                    if (removeStatus.StatusStates.Contains<MapWeatherState>())
                        hasWeather = true;
                }
                if (!hasWeather)
                {
                    MapIDState weatherIndex = ((MapStatus)owner).StatusStates.GetWithDefault<MapIDState>();
                    if (weatherIndex != null)
                    {
                        MapStatus status = new MapStatus(weatherIndex.ID);
                        status.LoadFromData();
                        status.StatusStates.GetWithDefault<MapCountDownState>().Counter = -1;
                        yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.AddMapStatus(status));
                    }
                }
            }
        }
    }


    [Serializable]
    public class MapStatusFillEvent : SingleCharEvent
    {
        public MapStatusFillEvent() { }
        public override GameEvent Clone() { return new MapStatusFillEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (context.User == null)
            {
                MapIDState weatherIndex = ((MapStatus)owner).StatusStates.GetWithDefault<MapIDState>();
                if (weatherIndex != null)
                {
                    bool hasWeather = false;
                    foreach (MapStatus removeStatus in ZoneManager.Instance.CurrentMap.Status.Values)
                    {
                        if (removeStatus.ID == weatherIndex.ID)
                            hasWeather = true;
                    }
                    if (!hasWeather)
                    {
                        MapStatus status = new MapStatus(weatherIndex.ID);
                        status.LoadFromData();
                        MapCountDownState countdown = status.StatusStates.GetWithDefault<MapCountDownState>();
                        if (countdown != null)
                            countdown.Counter = -1;
                        yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.AddMapStatus(status));
                    }
                }
            }
        }
    }

    [Serializable]
    public class MapStatusCountDownEvent : SingleCharEvent
    {
        public MapStatusCountDownEvent() { }
        public override GameEvent Clone() { return new MapStatusCountDownEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (context.User == null)
            {
                MapCountDownState countdown = ((MapStatus)owner).StatusStates.GetWithDefault<MapCountDownState>();
                if (countdown != null && countdown.Counter > -1)
                {
                    countdown.Counter--;
                    if (countdown.Counter <= 0)
                        yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.RemoveMapStatus(owner.GetID()));
                }
            }
        }
    }

    [Serializable]
    public class MapTickEvent : SingleCharEvent
    {
        public MapTickEvent() { }
        public override GameEvent Clone() { return new MapTickEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (context.User == null)
            {
                MapTickState countdown = ((MapStatus)owner).StatusStates.GetWithDefault<MapTickState>();
                countdown.Counter = (countdown.Counter + 1) % 10;
            }
            yield break;
        }
    }

    [Serializable]
    public class TimeLimitEvent : SingleCharEvent
    {
        public const int WARN_1 = 300;
        public const int WARN_2 = 200;
        public const int WARN_3 = 100;

        public FiniteEmitter Emitter;
        [Music(0)]
        public string BGM;
        public StringKey Warning1;
        public StringKey Warning2;
        public StringKey Warning3;
        public StringKey TimeOut;
        [Sound(0)]
        public string WarningSE1;
        [Sound(0)]
        public string WarningSE2;
        [Sound(0)]
        public string WarningSE3;
        [Sound(0)]
        public string TimeOutSE;

        public TimeLimitEvent()
        {
            Emitter = new EmptyFiniteEmitter();
            BGM = "";
            WarningSE1 = "";
            WarningSE2 = "";
            WarningSE3 = "";
            TimeOutSE = "";
        }

        protected TimeLimitEvent(TimeLimitEvent other)
        {
            Emitter = other.Emitter;
            BGM = other.BGM;
            Warning1 = other.Warning1;
            Warning2 = other.Warning2;
            Warning3 = other.Warning3;
            TimeOut = other.TimeOut;
            WarningSE1 = other.WarningSE1;
            WarningSE2 = other.WarningSE2;
            WarningSE3 = other.WarningSE3;
            TimeOutSE = other.TimeOutSE;
        }
        public override GameEvent Clone() { return new TimeLimitEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (context.User == null)
            {
                MapCountDownState countdown = ((MapStatus)owner).StatusStates.GetWithDefault<MapCountDownState>();
                if (countdown != null && countdown.Counter > -1)
                {
                    countdown.Counter--;
                    if (countdown.Counter == WARN_1)
                    {
                        ((MapStatus)owner).Hidden = false;
                        DungeonScene.Instance.LogMsg(Text.FormatGrammar(Warning1.ToLocal()));

                        FiniteEmitter endEmitter = (FiniteEmitter)Emitter.Clone();
                        endEmitter.SetupEmit(DungeonScene.Instance.GetFocusedMapLoc(), DungeonScene.Instance.GetFocusedMapLoc(), Dir8.None);
                        DungeonScene.Instance.CreateAnim(endEmitter, DrawLayer.NoDraw);

                        if (GameManager.Instance.Song != BGM)
                        {
                            //GameManager.Instance.BGM("", true);
                            GameManager.Instance.Fanfare("Battle/" + WarningSE1);
                            yield return new WaitForFrames(180);
                            //GameManager.Instance.BGM("C04. Wind.ogg", true);
                        }
                        else
                            GameManager.Instance.BattleSE(WarningSE1);
                    }
                    else if (countdown.Counter == WARN_2)
                    {
                        DungeonScene.Instance.LogMsg(Text.FormatGrammar(Warning2.ToLocal()));

                        FiniteEmitter endEmitter = (FiniteEmitter)Emitter.Clone();
                        endEmitter.SetupEmit(DungeonScene.Instance.GetFocusedMapLoc(), DungeonScene.Instance.GetFocusedMapLoc(), Dir8.None);
                        DungeonScene.Instance.CreateAnim(endEmitter, DrawLayer.NoDraw);
                        
                        if (GameManager.Instance.Song != BGM)
                        {
                            GameManager.Instance.Fanfare("Battle/" + WarningSE2);
                            yield return new WaitForFrames(180);
                        }
                        else
                            GameManager.Instance.BattleSE(WarningSE2);
                    }
                    else if (countdown.Counter == WARN_3)
                    {
                        DungeonScene.Instance.LogMsg(Text.FormatGrammar(Warning3.ToLocal()));

                        FiniteEmitter endEmitter = (FiniteEmitter)Emitter.Clone();
                        endEmitter.SetupEmit(DungeonScene.Instance.GetFocusedMapLoc(), DungeonScene.Instance.GetFocusedMapLoc(), Dir8.None);
                        DungeonScene.Instance.CreateAnim(endEmitter, DrawLayer.NoDraw);

                        if (GameManager.Instance.Song != BGM)
                        {
                            GameManager.Instance.Fanfare("Battle/" + WarningSE3);
                            yield return new WaitForFrames(180);
                        }
                        else
                            GameManager.Instance.BattleSE(WarningSE3);
                    }
                    else if (countdown.Counter <= 0)
                    {
                        yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.RemoveMapStatus(owner.GetID()));
                        DungeonScene.Instance.LogMsg(Text.FormatGrammar(TimeOut.ToLocal()));

                        FiniteEmitter endEmitter = (FiniteEmitter)Emitter.Clone();
                        endEmitter.SetupEmit(DungeonScene.Instance.GetFocusedMapLoc(), DungeonScene.Instance.GetFocusedMapLoc(), Dir8.None);
                        DungeonScene.Instance.CreateAnim(endEmitter, DrawLayer.NoDraw);

                        GameManager.Instance.BGM("", true);
                        GameManager.Instance.Fanfare("Battle/" + TimeOutSE);
                        yield return new WaitForFrames(90);
                        yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.FadeOut(true, 120));
                        yield return new WaitForFrames(90);
                        yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.EndSegment(GameProgress.ResultType.TimedOut));
                    }
                }
            }
        }
    }


    [Serializable]
    public class RevealSecretEvent : SingleCharEvent
    {

        public RevealSecretEvent() { }
        public override GameEvent Clone() { return new RevealSecretEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            EffectTile effectTile = (EffectTile)owner;

            if (!effectTile.Revealed)
            {
                GameManager.Instance.BattleSE("DUN_Smokescreen");
                SingleEmitter emitter = new SingleEmitter(new AnimData("Puff_Brown", 3));
                emitter.Layer = DrawLayer.Front;
                emitter.SetupEmit(effectTile.MapLoc, effectTile.MapLoc, context.User.CharDir);
                DungeonScene.Instance.CreateAnim(emitter, DrawLayer.NoDraw);
                effectTile.Revealed = true;

                yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(30));
            }
        }
    }




    [Serializable]
    public class AskUnlockEvent : SingleCharEvent
    {
        public AskUnlockEvent() { }
        public override GameEvent Clone() { return new AskUnlockEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (context.User == DungeonScene.Instance.ActiveTeam.Leader)
            {
                UnlockState unlock = ((EffectTile)owner).TileStates.GetWithDefault<UnlockState>();
                if (unlock == null)
                    yield break;

                int itemSlot = -2;

                if (context.User.EquippedItem.ID == unlock.UnlockItem && !context.User.EquippedItem.Cursed)
                    itemSlot = BattleContext.EQUIP_ITEM_SLOT;
                else if (context.User.MemberTeam is ExplorerTeam)
                {
                    for (int ii = 0; ii < ((ExplorerTeam)context.User.MemberTeam).GetInvCount(); ii++)
                    {
                        InvItem item = ((ExplorerTeam)context.User.MemberTeam).GetInv(ii);
                        if (item.ID == unlock.UnlockItem && !item.Cursed)
                        {
                            itemSlot = ii;
                            break;
                        }
                    }
                }

                ItemData itemEntry = DataManager.Instance.GetItem(unlock.UnlockItem);
                DungeonScene.Instance.PendingLeaderAction = giveLockedResponse(itemSlot, itemEntry);
            }
        }

        private IEnumerator<YieldInstruction> giveLockedResponse(int itemSlot, ItemData item)
        {
            if (DataManager.Instance.CurrentReplay != null)
                yield break;

            if (itemSlot > -2)
            {
                DialogueBox box = MenuManager.Instance.CreateQuestion(Text.FormatGrammar(new StringKey("DLG_LOCK_KEY").ToLocal(), item.GetIconName()),
                () => { MenuManager.Instance.EndAction = DungeonScene.Instance.ProcessPlayerInput(new GameAction(GameAction.ActionType.UseItem, Dir8.None, itemSlot, -1)); },
                () => { });
                yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.ProcessMenuCoroutine(box));
            }
            else
                yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.SetSign(Text.FormatGrammar(new StringKey("DLG_LOCK").ToLocal(), item.GetIconName())));
        }

    }

    [Serializable]
    public class NoticeEvent : SingleCharEvent
    {
        public NoticeEvent() { }
        public override GameEvent Clone() { return new NoticeEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (context.User == DungeonScene.Instance.ActiveTeam.Leader)
            {
                NoticeState notice = ((EffectTile)owner).TileStates.GetWithDefault<NoticeState>();
                if (notice == null)
                    yield break;
                GameManager.Instance.SE("Menu/Confirm");

                DungeonScene.Instance.PendingLeaderAction = processNotice(notice);
                yield break;
            }
        }

        private IEnumerator<YieldInstruction> processNotice(NoticeState notice)
        {
            if (DataManager.Instance.CurrentReplay != null)
                yield break;

            if (!notice.Title.Key.IsValid())
                yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.SetSign(notice.Content.FormatLocal()));
            else
                yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.ProcessMenuCoroutine(MenuManager.Instance.CreateNotice(notice.Title.FormatLocal(), notice.Content.FormatLocal())));
        }
    }

    [Serializable]
    public class SingleCharStateScriptEvent : SingleCharEvent
    {
        public override GameEvent Clone() { return new SingleCharStateScriptEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            TileScriptState state = ((EffectTile)owner).TileStates.GetWithDefault<TileScriptState>();
            if (state == null)
                yield break;

            LuaTable args = LuaEngine.Instance.RunString("return " + state.ArgTable).First() as LuaTable;
            object[] parameters = new object[] { owner, ownerChar, context, args };
            string name = "SINGLE_CHAR_SCRIPT." + state.Script;
            LuaFunction func_iter = LuaEngine.Instance.CreateCoroutineIterator(name, parameters);

            yield return CoroutineManager.Instance.StartCoroutine(ScriptEvent.ApplyFunc(name, func_iter));
        }
    }

    [Serializable]
    public class AskLeaderEvent : SingleCharEvent
    {
        public AskLeaderEvent() { }
        public override GameEvent Clone() { return new AskLeaderEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (context.User == DungeonScene.Instance.ActiveTeam.Leader)
                DungeonScene.Instance.PendingLeaderAction = PromptTileCheck(owner);
            yield break;
        }

        public IEnumerator<YieldInstruction> PromptTileCheck(GameEventOwner owner)
        {
            if (DataManager.Instance.CurrentReplay != null)
                yield break;

            EffectTile tile = (EffectTile)owner;
            Loc baseLoc = tile.TileLoc;
            if (DungeonScene.Instance.ActiveTeam.Leader.CharLoc == baseLoc && ZoneManager.Instance.CurrentMap.Tiles[baseLoc.X][baseLoc.Y].Effect == tile)
            {
                GameManager.Instance.SE("Menu/Confirm");
                yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.ProcessMenuCoroutine(new TileUnderfootMenu(tile.ID)));

            }
        }
    }


    [Serializable]
    public class AskIfDangerEvent : SingleCharEvent
    {
        public AskIfDangerEvent() { }
        public override GameEvent Clone() { return new AskIfDangerEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            EffectTile effectTile = (EffectTile)owner;
            DangerState danger;
            if (effectTile.TileStates.TryGet<DangerState>(out danger))
            {
                if (danger.Danger)
                {
                    if (DataManager.Instance.CurrentReplay != null)
                    {
                        int index = DataManager.Instance.CurrentReplay.ReadUI();
                        if (index == -1)
                        {
                            context.CancelState.Cancel = true;
                            context.TurnCancel.Cancel = true;
                        }
                    }
                    else
                    {
                        int index = 0;
                        DialogueBox box = MenuManager.Instance.CreateQuestion(Text.FormatKey("MSG_DANGER_CONFIRM"),
                                () => { },
                                () =>
                                {
                                    context.CancelState.Cancel = true;
                                    context.TurnCancel.Cancel = true;
                                    index = -1;
                                });

                        yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.ProcessMenuCoroutine(box));

                        if (DataManager.Instance.CurrentReplay == null)
                            DataManager.Instance.LogUIPlay(index);

                    }
                }
            }
        }
    }


    [Serializable]
    public class StealthEvoEvent : SingleCharEvent
    {
        [DataType(1, DataManager.DataType.Monster, false)]
        public HashSet<string> CheckSpecies;
        public int PercentChance;

        public StealthEvoEvent() { CheckSpecies = new HashSet<string>(); }
        public StealthEvoEvent(int chance, params string[] species)
        {
            PercentChance = chance;
            CheckSpecies = new HashSet<string>();
            foreach(string monster in species)
                CheckSpecies.Add(monster);
        }
        public StealthEvoEvent(StealthEvoEvent other)
        {
            PercentChance = other.PercentChance;
            CheckSpecies = new HashSet<string>();
            foreach (string monster in other.CheckSpecies)
                CheckSpecies.Add(monster);
        }
        public override GameEvent Clone() { return new StealthEvoEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            //only if part of the team, and not the leader
            if (context.User != null && context.User.MemberTeam == DungeonScene.Instance.ActiveTeam
                && DungeonScene.Instance.ActiveTeam.Leader != context.User && !context.User.MemberTeam.GetCharIndex(context.User).Guest)
            {
                if (CheckSpecies.Contains(context.User.BaseForm.Species))
                {
                    if (DataManager.Instance.Save.Rand.Next(100) < PercentChance)
                    {
                        MonsterData oldEntry = DataManager.Instance.GetMonster(context.User.BaseForm.Species);
                        PromoteBranch branch = oldEntry.Promotions[0];
                        if (branch.IsQualified(context.User, true))
                            yield return CoroutineManager.Instance.StartCoroutine(beginEvo(context.User, branch));
                    }
                }
            }
            yield break;
        }

        private IEnumerator<YieldInstruction> beginEvo(Character character, PromoteBranch branch)
        {
            //evolve
            MonsterID newData = character.BaseForm;
            newData.Species = branch.Result;
            branch.BeforePromote(character, true, ref newData);
            character.Promote(newData);
            branch.OnPromote(character, true, false);

            int oldFullness = character.Fullness;
            character.FullRestore();
            character.Fullness = oldFullness;

            DataManager.Instance.Save.RegisterMonster(character.BaseForm.Species);
            DataManager.Instance.Save.RogueUnlockMonster(character.BaseForm.Species);
            yield break;
        }
    }

    [Serializable]
    public class AskEvoEvent : SingleCharEvent
    {
        [JsonConverter(typeof(ItemConverter))]
        [DataType(0, DataManager.DataType.Item, false)]
        public string ExceptionItem;

        public AskEvoEvent() { ExceptionItem = ""; }
        public AskEvoEvent(string exceptItem) { ExceptionItem = exceptItem; }
        public AskEvoEvent(AskEvoEvent other) { ExceptionItem = other.ExceptionItem; }
        public override GameEvent Clone() { return new AskEvoEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            // Only player team, and do not include guests
            if (context.User.MemberTeam != DungeonScene.Instance.ActiveTeam)
                yield break;
            if (context.User.MemberTeam.GetCharIndex(context.User).Guest)
                yield break;

            CharAnimation standAnim = new CharAnimIdle(context.User.CharLoc, context.User.CharDir);
            standAnim.MajorAnim = true;
            yield return CoroutineManager.Instance.StartCoroutine(context.User.StartAnim(standAnim));

            if (DataManager.Instance.CurrentReplay != null)
            {
                int index = DataManager.Instance.CurrentReplay.ReadUI();
                if (index > -1)
                {
                    string currentSong = GameManager.Instance.Song;
                    GameManager.Instance.BGM("", true);

                    yield return CoroutineManager.Instance.StartCoroutine(beginEvo(context.User, index));

                    GameManager.Instance.BGM(currentSong, true);
                }
            }
            else
            {
                string currentSong = GameManager.Instance.Song;
                GameManager.Instance.BGM("", true);

                yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(20));

                int index = -1;

                yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.SetDialogue(Text.FormatGrammar(new StringKey("DLG_EVO_INTRO").ToLocal())));
                yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.ProcessMenuCoroutine(createEvoQuestion(context.User, (int slot) => { index = slot; })));

                if (DataManager.Instance.CurrentReplay == null)
                    DataManager.Instance.LogUIPlay(index);

                if (index > -1)
                    yield return CoroutineManager.Instance.StartCoroutine(beginEvo(context.User, index));

                yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.SetDialogue(Text.FormatGrammar(new StringKey("DLG_EVO_END").ToLocal())));

                GameManager.Instance.BGM(currentSong, true);

                yield return new WaitForFrames(1);
            }
        }

        private DialogueBox createEvoQuestion(Character character, VertChoiceMenu.OnChooseSlot action)
        {
            return MenuManager.Instance.CreateQuestion(Text.FormatGrammar(new StringKey("DLG_EVO_ASK").ToLocal()), () =>
            {
                //check for valid branches
                MonsterData entry = DataManager.Instance.GetMonster(character.BaseForm.Species);
                bool bypass = character.EquippedItem.ID == ExceptionItem;
                bool hasReq = false;
                List<int> validEvos = new List<int>();
                for (int ii = 0; ii < entry.Promotions.Count; ii++)
                {
                    if (!DataManager.Instance.DataIndices[DataManager.DataType.Monster].Get(entry.Promotions[ii].Result).Released)
                        continue;
                    bool hardReq = false;
                    if (entry.Promotions[ii].IsQualified(character, true))
                        validEvos.Add(ii);
                    else
                    {
                        foreach (PromoteDetail detail in entry.Promotions[ii].Details)
                        {
                            if (detail.IsHardReq() && !detail.GetReq(character, true))
                            {
                                hardReq = true;
                                break;
                            }
                        }
                    }
                    if (!hardReq)
                    {
                        if (bypass)
                            validEvos.Add(ii);
                        else
                            hasReq = true;
                    }
                }
                if (validEvos.Count == 0)
                {
                    if (hasReq)
                        MenuManager.Instance.AddMenu(MenuManager.Instance.CreateDialogue(Text.FormatGrammar(new StringKey("DLG_EVO_NONE_NOW").ToLocal(), character.GetDisplayName(true))), false);
                    else
                        MenuManager.Instance.AddMenu(MenuManager.Instance.CreateDialogue(Text.FormatGrammar(new StringKey("DLG_EVO_NONE").ToLocal(), character.GetDisplayName(true))), false);
                }
                else if (validEvos.Count == 1)
                    MenuManager.Instance.AddMenu(createTryEvoQuestion(character, action, validEvos[0]), false);
                else
                {
                    List<DialogueChoice> choices = new List<DialogueChoice>();
                    foreach (int validEvo in validEvos)
                    {
                        choices.Add(new DialogueChoice(DataManager.Instance.GetMonster(entry.Promotions[validEvo].Result).GetColoredName(),
                            () => { MenuManager.Instance.AddMenu(createTryEvoQuestion(character, action, validEvo), false); }));
                    }
                    choices.Add(new DialogueChoice(Text.FormatKey("MENU_CANCEL"), () => { }));
                    MenuManager.Instance.AddMenu(MenuManager.Instance.CreateMultiQuestion(Text.FormatGrammar(new StringKey("DLG_EVO_CHOICE").ToLocal(), character.GetDisplayName(true)), true, choices, 0, choices.Count - 1), false);
                }
            }, () => { });
        }

        private DialogueBox createTryEvoQuestion(Character character, VertChoiceMenu.OnChooseSlot action, int branchIndex)
        {
            MonsterData entry = DataManager.Instance.GetMonster(character.BaseForm.Species);
            PromoteBranch branch = entry.Promotions[branchIndex];
            bool bypass = character.EquippedItem.ID == ExceptionItem;
            string evoItem = "";
            foreach (PromoteDetail detail in branch.Details)
            {
                evoItem = detail.GetReqItem(character);
                if (!String.IsNullOrEmpty(evoItem))
                    break;
            }
            //factor in exception item to this question
            if (bypass)
                evoItem = ExceptionItem;
            string question = !String.IsNullOrEmpty(evoItem) ? Text.FormatGrammar(new StringKey("DLG_EVO_CONFIRM_ITEM").ToLocal(), character.GetDisplayName(true), DataManager.Instance.GetItem(evoItem).GetIconName(), DataManager.Instance.GetMonster(branch.Result).GetColoredName()) : Text.FormatGrammar(new StringKey("DLG_EVO_CONFIRM").ToLocal(), character.GetDisplayName(true), DataManager.Instance.GetMonster(branch.Result).GetColoredName());
            return MenuManager.Instance.CreateQuestion(question, () => { action(branchIndex); }, () => { });
        }

        private IEnumerator<YieldInstruction> beginEvo(Character character, int branchIndex)
        {
            MonsterData oldEntry = DataManager.Instance.GetMonster(character.BaseForm.Species);
            PromoteBranch branch = oldEntry.Promotions[branchIndex];
            bool bypass = character.EquippedItem.ID == ExceptionItem;

            if (DataManager.Instance.CurrentReplay == null)
                yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.SetDialogue(Text.FormatGrammar(new StringKey("DLG_EVO_BEGIN").ToLocal())));
            character.CharDir = Dir8.Down;
            //fade
            GameManager.Instance.BattleSE("EVT_Evolution_Start");
            yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.FadeOut(true));
            string oldName = character.GetDisplayName(true);
            //evolve
            MonsterData entry = DataManager.Instance.GetMonster(branch.Result);
            MonsterID newData = character.BaseForm;
            newData.Species = branch.Result;
            branch.BeforePromote(character, true, ref newData);
            character.Promote(newData);
            branch.OnPromote(character, true, bypass);
            if (bypass)
                yield return CoroutineManager.Instance.StartCoroutine(character.DequipItem());

            int oldFullness = character.Fullness;
            character.FullRestore();
            character.Fullness = oldFullness;
            //restore HP and status problems
            //{
            //    context.User.HP = context.User.MaxHP;

            //    List<int> statuses = new List<int>();
            //    foreach (StatusEffect oldStatus in context.User.IterateStatusEffects())
            //        statuses.Add(oldStatus.ID);

            //    foreach (int statusID in statuses)
            //        yield return CoroutineManager.Instance.StartCoroutine(context.User.RemoveStatusEffect(statusID, false));
            //}

            yield return new WaitForFrames(30);
            //fade
            GameManager.Instance.BattleSE("EVT_Title_Intro");
            yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.FadeIn());
            //evolution chime
            GameManager.Instance.Fanfare("Fanfare/Promotion");
            //proclamation

            yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.LogSkippableMsg(Text.FormatGrammar(new StringKey("DLG_EVO_COMPLETE").ToLocal(), oldName, entry.GetColoredName())));

            DataManager.Instance.Save.RegisterMonster(character.BaseForm.Species);
            DataManager.Instance.Save.RogueUnlockMonster(character.BaseForm.Species);
            yield return CoroutineManager.Instance.StartCoroutine(character.OnMapStart());

            yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.CheckLevelSkills(character, 0));
            if (character.Level > 1)
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.CheckLevelSkills(character, character.Level - 1));
        }
    }

    [Serializable]
    public class PrepareLevelEvent : SingleCharEvent
    {
        public int Level;
        public PrepareLevelEvent() { }
        public PrepareLevelEvent(int level) { Level = level; }
        protected PrepareLevelEvent(PrepareLevelEvent other) { Level = other.Level; }
        public override GameEvent Clone() { return new PrepareLevelEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            foreach(Character player in DungeonScene.Instance.ActiveTeam.EnumerateChars())
                DataManager.Instance.Save.RestrictCharLevel(player, Level, false, false);
            foreach (Character player in DungeonScene.Instance.ActiveTeam.Assembly)
                DataManager.Instance.Save.RestrictCharLevel(player, Level, false, false);
            yield break;
        }
    }


    [Serializable]
    public class ResetFloorEvent : SingleCharEvent
    {
        public ResetFloorEvent() { }
        public override GameEvent Clone() { return new ResetFloorEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (context.User == DungeonScene.Instance.ActiveTeam.Leader)
            {
                GameManager.Instance.BattleSE("DUN_Tile_Step");
                yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.FadeOut(false));
                GameManager.Instance.SceneOutcome = resetFloor(ZoneManager.Instance.CurrentZone.CurrentMapID, new LocRay8(DungeonScene.Instance.ActiveTeam.Leader.CharLoc, DungeonScene.Instance.ActiveTeam.Leader.CharDir));
            }
            else if (context.User.MemberTeam == DungeonScene.Instance.ActiveTeam)
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_LEADER_ONLY_TILE").ToLocal()));
        }


        public IEnumerator<YieldInstruction> resetFloor(SegLoc destStruct, LocRay8 dest)
        {
            DungeonScene.Instance.Exit();

            ZoneManager.Instance.CurrentZone.SetCurrentMap(destStruct);

            DungeonScene.Instance.EnterFloor(dest);
            yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.InitFloor());
            yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.FadeIn());
            yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.BeginFloor());
        }
    }

    [Serializable]
    public class RescueEvent : SingleCharEvent
    {
        public RescueEvent() { }
        public override GameEvent Clone() { return new RescueEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            //a case for rescues; leader only!
            //everyone can check a tile.
            //however, only the leader of a team can choose to advance
            if (context.User == DungeonScene.Instance.ActiveTeam.Leader)
            {
                ZoneSegmentBase structure = ZoneManager.Instance.CurrentZone.Segments[ZoneManager.Instance.CurrentMapID.Segment];
                GameManager.Instance.BattleSE("DUN_Stairs_Down");
                yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.EndSegment(GameProgress.ResultType.Rescue));
            }
            else if (context.User.MemberTeam == DungeonScene.Instance.ActiveTeam)
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_LEADER_ONLY_TILE").ToLocal()));
                context.CancelState.Cancel = true;
            }
            context.TurnCancel.Cancel = true;
        }
    }

    [Serializable]
    public class NextFloorEvent : SingleCharEvent
    {
        public NextFloorEvent() { }
        public override GameEvent Clone() { return new NextFloorEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (((EffectTile)owner).TileStates.Contains<DestState>())
                yield break;

            //a case for changing floor; leader only!
            //everyone can check a tile.
            //however, only the leader of a team can choose to advance
            if (context.User == DungeonScene.Instance.ActiveTeam.Leader)
            {
                if (ZoneManager.Instance.InDevZone) //editor considerations
                    GameManager.Instance.SceneOutcome = GameManager.Instance.ReturnToEditor();
                else
                {
                    for (int ii = DungeonScene.Instance.ActiveTeam.GetInvCount() - 1; ii >= 0; ii--)
                    {
                        if (DungeonScene.Instance.ActiveTeam.GetInv(ii).Price > 0)
                            DungeonScene.Instance.ActiveTeam.RemoveFromInv(ii);
                    }

                    ZoneSegmentBase structure = ZoneManager.Instance.CurrentZone.Segments[ZoneManager.Instance.CurrentMapID.Segment];
                    GameManager.Instance.BattleSE("DUN_Stairs_Down");
                    if (structure.FloorCount < 0 || ZoneManager.Instance.CurrentMapID.ID + 1 < structure.FloorCount)
                    {
                        yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.FadeOut(false));
                        GameManager.Instance.SceneOutcome = GameManager.Instance.MoveToZone(new ZoneLoc(ZoneManager.Instance.CurrentZoneID, new SegLoc(ZoneManager.Instance.CurrentMapID.Segment, ZoneManager.Instance.CurrentMapID.ID + 1)));
                    }
                    else
                        yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.EndSegment(GameProgress.ResultType.Cleared, false));
                }
            }
            else if (context.User.MemberTeam == DungeonScene.Instance.ActiveTeam)
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_LEADER_ONLY_TILE").ToLocal()));
                context.CancelState.Cancel = true;
            }
            context.TurnCancel.Cancel = true;
        }
    }


    [Serializable]
    public class SwitchMapEvent : SingleCharEvent
    {
        public SwitchMapEvent() { }
        public override GameEvent Clone() { return new SwitchMapEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            DestState destState = ((EffectTile)owner).TileStates.GetWithDefault<DestState>();

            if (destState == null)
                yield break;

            if (context.User == DungeonScene.Instance.ActiveTeam.Leader)
            {
                GameManager.Instance.BattleSE("DUN_Stairs_Down");

                if (ZoneManager.Instance.InDevZone) //editor considerations
                    GameManager.Instance.SceneOutcome = GameManager.Instance.ReturnToEditor();
                else
                {
                    for (int ii = DungeonScene.Instance.ActiveTeam.GetInvCount() - 1; ii >= 0; ii--)
                    {
                        if (DungeonScene.Instance.ActiveTeam.GetInv(ii).Price > 0)
                            DungeonScene.Instance.ActiveTeam.RemoveFromInv(ii);
                    }

                    if (destState.Relative)
                    {
                        yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.FadeOut(false));

                        int endSegment = ZoneManager.Instance.CurrentMapID.Segment + destState.Dest.Segment;
                        int endFloor = ZoneManager.Instance.CurrentMapID.ID + destState.Dest.ID;

                        if (endSegment >= 0 && endFloor >= 0 && endSegment < ZoneManager.Instance.CurrentZone.Segments.Count && (ZoneManager.Instance.CurrentZone.Segments[endSegment].FloorCount < 0 || endFloor < ZoneManager.Instance.CurrentZone.Segments[endSegment].FloorCount))
                            GameManager.Instance.SceneOutcome = GameManager.Instance.MoveToZone(new ZoneLoc(ZoneManager.Instance.CurrentZoneID, new SegLoc(endSegment, endFloor)), false, destState.PreserveMusic);
                        else
                            yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.EndSegment(GameProgress.ResultType.Cleared, destState.PreserveMusic));
                    }
                    else if (!destState.Dest.IsValid())
                        yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.EndSegment(GameProgress.ResultType.Cleared, destState.PreserveMusic));
                    else//go to a designated dungeon structure
                    {
                        yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.FadeOut(false));
                        GameManager.Instance.SceneOutcome = GameManager.Instance.MoveToZone(new ZoneLoc(ZoneManager.Instance.CurrentZoneID, destState.Dest), false, destState.PreserveMusic);
                    }
                }
            }
            else if (context.User.MemberTeam == DungeonScene.Instance.ActiveTeam)
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_LEADER_ONLY_TILE").ToLocal()));
                context.CancelState.Cancel = true;
            }
            context.TurnCancel.Cancel = true;
        }
    }

    [Serializable]
    public class TransportOnElementEvent : SingleCharEvent
    {
        [JsonConverter(typeof(ElementConverter))]
        [DataType(0, DataManager.DataType.Element, false)]
        public string TargetElement;

        [Sound(0)]
        public string Sound;

        public StringKey FailMessage;

        public StringKey SuccessMessage;

        public TransportOnElementEvent() { }
        public TransportOnElementEvent(string element, string sound, StringKey fail, StringKey success)
        {
            TargetElement = element;
            this.Sound = sound;
            FailMessage = fail;
            SuccessMessage = success;
        }
        public TransportOnElementEvent(TransportOnElementEvent other)
        {
            TargetElement = other.TargetElement;
            this.Sound = other.Sound;
            FailMessage = other.FailMessage;
            SuccessMessage = other.SuccessMessage;
        }
        public override GameEvent Clone() { return new TransportOnElementEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            DestState destState = ((EffectTile)owner).TileStates.GetWithDefault<DestState>();

            if (destState == null)
                yield break;

            if (context.User.MemberTeam != DungeonScene.Instance.ActiveTeam)
                yield break;

            string currentSong = GameManager.Instance.Song;
            GameManager.Instance.BGM("", true);

            yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(20));

            yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.LogSkippableMsg(Text.FormatGrammar(FailMessage.ToLocal())));

            MonsterID formData = context.User.BaseForm;
            BaseMonsterForm form = DataManager.Instance.GetMonster(formData.Species).Forms[formData.Form];

            if (form.Element1 == TargetElement || form.Element2 == TargetElement)
            {
                yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.LogSkippableMsg(Text.FormatGrammar(SuccessMessage.ToLocal(), context.User.BaseName)));

                if (ZoneManager.Instance.InDevZone) //editor considerations
                    GameManager.Instance.SceneOutcome = GameManager.Instance.ReturnToEditor();
                else
                {
                    for (int ii = DungeonScene.Instance.ActiveTeam.GetInvCount() - 1; ii >= 0; ii--)
                    {
                        if (DungeonScene.Instance.ActiveTeam.GetInv(ii).Price > 0)
                            DungeonScene.Instance.ActiveTeam.RemoveFromInv(ii);
                    }

                    GameManager.Instance.BattleSE(Sound);

                    yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.FadeOut(true));

                    yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(60) + 30);

                    GameManager.Instance.SetFade(true, false);
                    if (destState.Relative)
                    {
                        int endSegment = ZoneManager.Instance.CurrentMapID.Segment + destState.Dest.Segment;
                        int endFloor = ZoneManager.Instance.CurrentMapID.ID + destState.Dest.ID;

                        if (endSegment >= 0 && endFloor >= 0 && endSegment < ZoneManager.Instance.CurrentZone.Segments.Count && (ZoneManager.Instance.CurrentZone.Segments[endSegment].FloorCount < 0 || endFloor < ZoneManager.Instance.CurrentZone.Segments[endSegment].FloorCount))
                            GameManager.Instance.SceneOutcome = GameManager.Instance.MoveToZone(new ZoneLoc(ZoneManager.Instance.CurrentZoneID, new SegLoc(endSegment, endFloor)));
                        else
                            yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.EndSegment(GameProgress.ResultType.Cleared, destState.PreserveMusic));
                    }
                    else if (!destState.Dest.IsValid())
                        yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.EndSegment(GameProgress.ResultType.Cleared, destState.PreserveMusic));
                    else//go to a designated dungeon structure
                    {
                        GameManager.Instance.SceneOutcome = GameManager.Instance.MoveToZone(new ZoneLoc(ZoneManager.Instance.CurrentZoneID, destState.Dest));
                    }
                }
            }
            else
            {
                GameManager.Instance.BGM(currentSong, true);
                yield return new WaitForFrames(1);
            }
        }
    }

    [Serializable]
    public class EndGameEvent : SingleCharEvent
    {
        public EndGameEvent() { }
        public override GameEvent Clone() { return new EndGameEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            for (int ii = DungeonScene.Instance.ActiveTeam.GetInvCount() - 1; ii >= 0; ii--)
            {
                if (DungeonScene.Instance.ActiveTeam.GetInv(ii).Price > 0)
                    DungeonScene.Instance.ActiveTeam.RemoveFromInv(ii);
            }

            GameManager.Instance.BattleSE("DUN_Stairs_Down");
            yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.EndSegment(GameProgress.ResultType.Cleared));
        }
    }


    [Serializable]
    public class DialogueEvent : SingleCharEvent
    {
        public StringKey Message;

        public DialogueEvent() { }
        public DialogueEvent(StringKey message)
        {
            Message = message;
        }
        protected DialogueEvent(DialogueEvent other)
        {
            Message = other.Message;
        }
        public override GameEvent Clone() { return new DialogueEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (context.User.MemberTeam == DungeonScene.Instance.ActiveTeam && DataManager.Instance.CurrentReplay == null)
                yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.SetDialogue(Text.FormatGrammar(Message.ToLocal())));
        }

    }

    [Serializable]
    public class PrepareCutsceneEvent : SingleCharEvent
    {
        public PrepareCutsceneEvent() { }
        public override GameEvent Clone() { return new PrepareCutsceneEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            GraphicsManager.GlobalIdle = 0;
            DataManager.Instance.Save.CutsceneMode = true;
            yield break;
        }
    }

    [Serializable]
    public class PrepareCameraEvent : SingleCharEvent
    {
        public Loc CamCenter;
        public bool Relative;

        public PrepareCameraEvent() { }
        public PrepareCameraEvent(Loc loc) { CamCenter = loc; }
        protected PrepareCameraEvent(PrepareCameraEvent other)
        {
            CamCenter = other.CamCenter;
            Relative = other.Relative;
        }
        public override GameEvent Clone() { return new PrepareCameraEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (Relative)
                ZoneManager.Instance.CurrentMap.ViewOffset = CamCenter;
            else
                ZoneManager.Instance.CurrentMap.ViewCenter = CamCenter;
            yield break;
        }
    }

    /// <summary>
    /// Sets the position of team members for starting a battle.
    /// The first loc in the list is where team member 1 should be
    /// The second is where team member 2 should be
    /// etc.
    /// All positions are relative to the first entrance.
    /// </summary>
    [Serializable]
    public class BattlePositionEvent : SingleCharEvent
    {
        public LocRay8[] StartLocs;

        // TODO: Delete in v1.1
        [NonEdited]
        public Loc[] Positions;

        public BattlePositionEvent() { StartLocs = new LocRay8[0]; }
        public BattlePositionEvent(params LocRay8[] positions) { StartLocs = positions; }
        public BattlePositionEvent(BattlePositionEvent other)
        {
            StartLocs = new LocRay8[other.StartLocs.Length];
            other.StartLocs.CopyTo(StartLocs, 0);
        }
        public override GameEvent Clone() { return new BattlePositionEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (context.User != null)
                yield break;

            int total_alive = 0;
            foreach (Character target in DungeonScene.Instance.ActiveTeam.IterateByRank())
            {
                if (!target.Dead)
                {
                    target.HP = target.MaxHP;
                    MoveChar(target, total_alive);

                    total_alive++;
                }
            }
        }
        
        public void MoveChar(Character character, int total_alive)
        {
            character.CharDir = ZoneManager.Instance.CurrentMap.EntryPoints[0].Dir;
            if (total_alive < StartLocs.Length)
            {
                character.CharLoc = ZoneManager.Instance.CurrentMap.EntryPoints[0].Loc + StartLocs[total_alive].Loc;
                character.CharDir = StartLocs[total_alive].Dir;
            }
            else //default to close to leader
            {
                Loc? result = ZoneManager.Instance.CurrentMap.GetClosestTileForChar(character, ZoneManager.Instance.CurrentMap.EntryPoints[0].Loc);
                if (result.HasValue)
                    character.CharLoc = result.Value;
                else
                    character.CharLoc = ZoneManager.Instance.CurrentMap.EntryPoints[0].Loc;
            }
        }

        [OnDeserialized]
        internal void OnDeserializedMethod(StreamingContext context)
        {
            if (Serializer.OldVersion < new Version(0, 7, 17) && Positions != null)
            {
                StartLocs = new LocRay8[Positions.Length];
                for (int ii = 0; ii < StartLocs.Length; ii++)
                    StartLocs[ii] = new LocRay8(Positions[ii], Dir8.Up);
            }
        }
    }

    [Serializable]
    public class FadeTitleEvent : SingleCharEvent
    {
        public FadeTitleEvent() { }
        public override GameEvent Clone() { return new FadeTitleEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (context.User != null)
                yield break;

            // title drop if faded, but do not fade directly
            if (GameManager.Instance.IsFaded())
            {
                yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.FadeTitle(true, ZoneManager.Instance.CurrentMap.Name.ToLocal()));
                yield return new WaitForFrames(30);
                yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.FadeTitle(false, ""));
            }
        }
    }

    [Serializable]
    public class FadeInEvent : SingleCharEvent
    {
        public FadeInEvent() { }
        public override GameEvent Clone() { return new FadeInEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (context.User != null)
                yield break;
            yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.FadeIn());
        }
    }

    [Serializable]
    public class SpecialIntroEvent : SingleCharEvent
    {
        public SpecialIntroEvent() { }
        public override GameEvent Clone() { return new SpecialIntroEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (context.User != null)
                yield break;
            foreach (Character member in DungeonScene.Instance.ActiveTeam.EnumerateChars())
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.SpecialIntro(member));

        }
    }

    [Serializable]
    public class ReactivateItemsEvent : SingleCharEvent
    {
        public ReactivateItemsEvent() { }
        public override GameEvent Clone() { return new ReactivateItemsEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (context.User != null)
                yield break;

            foreach (InvItem item in DungeonScene.Instance.ActiveTeam.EnumerateInv())
            {
                ItemData entry = DataManager.Instance.GetItem(item.ID);
                if (entry.MaxStack < 0 && entry.UsageType == ItemData.UseType.UseOther)
                    item.HiddenValue = "";
            }
        }
    }

    /// <summary>
    /// Sets Team Mode On for boss battles and adds a map condition for checking if all enemies have been defeated.
    /// </summary>
    [Serializable]
    public class BeginBattleEvent : SingleCharEvent
    {
        [JsonConverter(typeof(MapStatusConverter))]
        [DataType(0, DataManager.DataType.MapStatus, false)]
        public string CheckClearStatus;

        public BeginBattleEvent() { CheckClearStatus = ""; }
        public BeginBattleEvent(string checkClear) { CheckClearStatus = checkClear; }
        public BeginBattleEvent(BeginBattleEvent other)
        {
            CheckClearStatus = other.CheckClearStatus;
        }
        public override GameEvent Clone() { return new BeginBattleEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (context.User != null)
                yield break;

            if (DungeonScene.Instance.CanUseTeamMode())
                DungeonScene.Instance.SetTeamMode(true);

            //for scanning when all enemies have been defeated
            MapStatus status = new MapStatus(CheckClearStatus);
            status.LoadFromData();
            MapCheckState check = status.StatusStates.GetWithDefault<MapCheckState>();
            check.CheckEvents.Add(new CheckBossClearEvent());
            yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.AddMapStatus(status));
        }
    }

    [Serializable]
    public class CheckBossClearEvent : SingleCharEvent
    {
        public CheckBossClearEvent() { }
        public override GameEvent Clone() { return new CheckBossClearEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            //fail if someone is still alive
            foreach (Team team in ZoneManager.Instance.CurrentMap.MapTeams)
            {
                foreach (Character chara in team.IterateMainByRank())
                {
                    if (!chara.Dead)
                        yield break;
                }
            }

            //all dead, clear the game
            MapCheckState checks = ((MapStatus)owner).StatusStates.GetWithDefault<MapCheckState>();
            checks.CheckEvents.Remove(this);
            if (DataManager.Instance.CurrentReplay == null)
                yield return CoroutineManager.Instance.StartCoroutine(endSequence());
            else
                yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.EndSegment(GameProgress.ResultType.Cleared));
        }

        private IEnumerator<YieldInstruction> endSequence()
        {

            GameManager.Instance.BGM("", true);

            yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.FadeOut(false));

            DungeonScene.Instance.ResetTurns();

            //restore all and remove all map status
            List<string> statusToRemove = new List<string>();
            foreach (string status in ZoneManager.Instance.CurrentMap.Status.Keys)
                statusToRemove.Add(status);
            foreach (string status in statusToRemove)
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.RemoveMapStatus(status, false));

            foreach (Character character in DungeonScene.Instance.ActiveTeam.IterateMainByRank())
                character.FullRestore();

            yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.EndSegment(GameProgress.ResultType.Cleared));
        }
    }



    [Serializable]
    public class RevealFrontTrapEvent : SingleCharEvent
    {
        public override GameEvent Clone() { return new RevealFrontTrapEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            Loc destTile = context.User.CharLoc + context.User.CharDir.GetLoc();
            if (!ZoneManager.Instance.CurrentMap.GetLocInMapBounds(ref destTile))
                yield break;

            if (context.User.MemberTeam is ExplorerTeam)
            {
                Tile tile = ZoneManager.Instance.CurrentMap.Tiles[destTile.X][destTile.Y];
                if (!String.IsNullOrEmpty(tile.Effect.ID) && !tile.Effect.Revealed)
                {
                    tile.Effect.Revealed = true;

                    if (!context.User.Unidentifiable)
                    {
                        EmoteData emoteData = DataManager.Instance.GetEmote("exclaim");
                        context.User.StartEmote(new Emote(emoteData.Anim, emoteData.LocHeight, 1));
                    }

                    yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(20)+20);
                }
            }
        }
    }

    [Serializable]
    public class TriggerUnderfootEvent : SingleCharEvent
    {
        public TriggerUnderfootEvent() { }
        public override GameEvent Clone() { return new TriggerUnderfootEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            Tile tile = ZoneManager.Instance.CurrentMap.Tiles[context.User.CharLoc.X][context.User.CharLoc.Y];
            if (!String.IsNullOrEmpty(tile.Effect.ID))
            {
                DungeonScene.Instance.QueueTrap(context.User.CharLoc);
                //yield return CoroutineManager.Instance.StartCoroutine(tile.Effect.InteractWithTile(context.User));
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that applies the trap behavior
    /// </summary>
    [Serializable]
    public class InvokeTrapEvent : SingleCharEvent
    {
        /// <summary>
        /// Data on the hitbox of the attack. Controls range and targeting
        /// </summary>
        public CombatAction HitboxAction;
        
        /// <summary>
        /// Optional data to specify a splash effect on the tiles hit
        /// </summary>
        public ExplosionData Explosion;
        
        /// <summary>
        /// Events that occur with this trap.
        /// Before it's used, when it hits, after it's used, etc
        /// </summary>
        public BattleData NewData; 
        
        /// <summary>
        /// The message displayed when the trap is triggered
        /// </summary>
        [StringKey(0, true)]
        public StringKey Message;
                
        /// <summary>
        /// Whether the trap can be activated only once
        /// </summary>
        public bool OneTime;
        
        public InvokeTrapEvent() { }
        public InvokeTrapEvent(CombatAction action, ExplosionData explosion, BattleData moveData, StringKey msg, bool oneTime)
        {
            HitboxAction = action;
            Explosion = explosion;
            NewData = moveData;
            Message = msg;
            OneTime = oneTime;
        }
        protected InvokeTrapEvent(InvokeTrapEvent other)
        {
            HitboxAction = other.HitboxAction;
            Explosion = other.Explosion;
            NewData = new BattleData(other.NewData);
            Message = other.Message;
            OneTime = other.OneTime;
        }
        public override GameEvent Clone() { return new InvokeTrapEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            EffectTile effectTile = (EffectTile)owner;

            //don't activate on an ally
            if (ZoneManager.Instance.CurrentMap.GetTileOwner(context.User) == effectTile.Owner)
                yield break;

            if (context.User.CharStates.Contains<TrapState>())
                yield break;

            //don't activate if already triggering
            if (effectTile.TileStates.Contains<TriggeringState>())
                yield break;

            effectTile.TileStates.Set(new TriggeringState());

            CharAnimation standAnim = new CharAnimIdle(context.User.CharLoc, context.User.CharDir);
            standAnim.MajorAnim = true;
            yield return CoroutineManager.Instance.StartCoroutine(context.User.StartAnim(standAnim));

            GameManager.Instance.BattleSE("DUN_Tile_Step");
            effectTile.Revealed = true;


            BattleContext newContext = new BattleContext(BattleActionType.Trap);
            newContext.User = context.User;
            newContext.UsageSlot = BattleContext.FORCED_SLOT;

            newContext.StartDir = newContext.User.CharDir;

            //change move effects
            newContext.Data = new BattleData(NewData);
            newContext.Data.ID = owner.GetID();
            newContext.Data.DataType = DataManager.DataType.Tile;

            newContext.Explosion = new ExplosionData(Explosion);
            newContext.HitboxAction = HitboxAction.Clone();
            //recenter the attack on the tile
            newContext.HitboxAction.HitOffset = effectTile.TileLoc - context.User.CharLoc;
            newContext.Strikes = 1;
            newContext.Item = new InvItem();

            TileData entry = DataManager.Instance.GetTile(owner.GetID());
            newContext.SetActionMsg(Text.FormatGrammar(Message.ToLocal(), newContext.User.GetDisplayName(false), entry.Name.ToLocal()));

            //process the attack
            
            yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.PreProcessAction(newContext));

            //Handle Use
            newContext.PrintActionMsg();

            yield return CoroutineManager.Instance.StartCoroutine(TrapExecuteAction(newContext));
            if (newContext.CancelState.Cancel) { yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.CancelWait(effectTile.TileLoc)); yield break; }
            yield return CoroutineManager.Instance.StartCoroutine(TrapRepeatActions(newContext));

            effectTile.TileStates.Remove<TriggeringState>();

            if (OneTime)
            {
                Loc baseLoc = effectTile.TileLoc;
                Tile tile = ZoneManager.Instance.CurrentMap.Tiles[baseLoc.X][baseLoc.Y];
                if (tile.Effect == owner)
                    tile.Effect = new EffectTile(tile.Effect.TileLoc);
            }
        }


        public IEnumerator<YieldInstruction> TrapExecuteAction(BattleContext baseContext)
        {
            BattleContext context = new BattleContext(baseContext, true);

            yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.PerformAction(context));
            if (context.CancelState.Cancel) yield break;
        }


        public IEnumerator<YieldInstruction> TrapRepeatActions(BattleContext context)
        {
            //increment for multistrike
            context.StrikesMade++;
            while (context.StrikesMade < context.Strikes)
            {
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.PreProcessAction(context));
                yield return CoroutineManager.Instance.StartCoroutine(TrapExecuteAction(context));

                context.StrikesMade++;
            }
        }
    }


    [Serializable]
    public class TempTileToStairsEvent : SingleCharEvent
    {
        [DataType(0, DataManager.DataType.Tile, false)]
        public string ResultTile;

        [DataType(0, DataManager.DataType.MapStatus, true)]
        public string RemoveMapStatus;

        public TempTileToStairsEvent()
        {
        }
        public TempTileToStairsEvent(string resultTile, string removeMapStatus)
        {
            ResultTile = resultTile;
            RemoveMapStatus = removeMapStatus;
        }
        public TempTileToStairsEvent(TempTileToStairsEvent other)
        {
            ResultTile = other.ResultTile;
            RemoveMapStatus = other.RemoveMapStatus;
        }
        public override GameEvent Clone() { return new TempTileToStairsEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            EffectTile effectTile = (EffectTile)owner;

            Loc baseLoc = effectTile.TileLoc;
            Tile tile = ZoneManager.Instance.CurrentMap.Tiles[baseLoc.X][baseLoc.Y];
            if (tile.Effect == owner)
            {
                tile.Effect = new EffectTile(ResultTile, true, tile.Effect.TileLoc);
                tile.Effect.TileStates = effectTile.TileStates.Clone();
            }

            yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.RemoveMapStatus(RemoveMapStatus));

            DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_STAIRS").ToLocal()));
            yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(90));
        }

    }


    [Serializable]
    public class TempTileCollapseEvent : SingleCharEvent
    {
        public List<AnimEvent> Anims;

        public TempTileCollapseEvent() { Anims = new List<AnimEvent>(); }
        public TempTileCollapseEvent(params AnimEvent[] anims)
        {
            Anims = new List<AnimEvent>();
            Anims.AddRange(anims);
        }
        protected TempTileCollapseEvent(TempTileCollapseEvent other)
        {
            Anims = new List<AnimEvent>();
            foreach (AnimEvent anim in other.Anims)
                Anims.Add((AnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new TempTileCollapseEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (context.User == null)
            {
                MapCountDownState countdown = ((MapStatus)owner).StatusStates.GetWithDefault<MapCountDownState>();
                if (countdown != null && countdown.Counter > -1)
                {
                    countdown.Counter--;
                    if (countdown.Counter <= 0)
                    {
                        context.User = DungeonScene.Instance.ActiveTeam.Leader;
                        foreach (AnimEvent anim in Anims)
                            yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));
                        context.User = null;

                        yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.RemoveMapStatus(owner.GetID()));

                        MapLocState locState = ((MapStatus)owner).StatusStates.GetWithDefault<MapLocState>();
                        if (locState != null)
                        {
                            Tile tile = ZoneManager.Instance.CurrentMap.Tiles[locState.Target.X][locState.Target.Y];
                            tile.Effect = new EffectTile(tile.Effect.TileLoc);
                        }
                    }
                }
            }
        }
    }

    [Serializable]
    public class OpenVaultEvent : SingleCharEvent
    {
        public List<Loc> OpenLocs;
        public OpenVaultEvent() { OpenLocs = new List<Loc>(); }
        public OpenVaultEvent(List<Loc> locs) { OpenLocs = locs; }
        public OpenVaultEvent(OpenVaultEvent other) { OpenLocs = other.OpenLocs; }
        public override GameEvent Clone() { return new OpenVaultEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            //invoke the unlock sound and animation
            GameManager.Instance.BattleSE("DUN_Open_Chamber");

            foreach (Loc loc in OpenLocs)
            {
                //remove all specified tiles; both the effect and the terrain, if there is one
                Tile tile = ZoneManager.Instance.CurrentMap.Tiles[loc.X][loc.Y];
                tile.Effect = new EffectTile(tile.Effect.TileLoc);
                SingleEmitter emitter = new SingleEmitter(new AnimData("Vault_Key_Open", 3));
                emitter.SetupEmit(loc * GraphicsManager.TileSize + new Loc(GraphicsManager.TileSize / 2), loc * GraphicsManager.TileSize + new Loc(GraphicsManager.TileSize / 2), Dir8.Down);
                DungeonScene.Instance.CreateAnim(emitter, DrawLayer.NoDraw);
                yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(30));
                {
                    tile.Data = new TerrainTile(DataManager.Instance.GenFloor);
                    int distance = 0;
                    Loc startLoc = loc - new Loc(distance + 2);
                    Loc sizeLoc = new Loc((distance + 2) * 2 + 1);
                    ZoneManager.Instance.CurrentMap.MapModified(startLoc, sizeLoc);
                }
            }
        }

    }


    [Serializable]
    public class OpenSelfEvent : SingleCharEvent
    {
        public BattleFX Anim;

        public bool PreOpen;

        public bool Fanfare;

        public OpenSelfEvent()
        {
            Anim = new BattleFX();
        }
        public OpenSelfEvent(BattleFX anim, bool preOpen, bool fanfare)
        {
            Anim = anim;
            PreOpen = preOpen;
            Fanfare = fanfare;
        }
        public OpenSelfEvent(OpenSelfEvent other)
        {
            Anim = other.Anim;
            PreOpen = other.PreOpen;
            Fanfare = other.Fanfare;
        }
        public override GameEvent Clone() { return new OpenSelfEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            EffectTile effectTile = (EffectTile)owner;

            CharAnimation standAnim = new CharAnimIdle(context.User.CharLoc, context.User.CharDir);
            standAnim.MajorAnim = true;
            yield return CoroutineManager.Instance.StartCoroutine(context.User.StartAnim(standAnim));

            //remove all specified tiles; both the effect and the terrain, if there is one
            Loc baseLoc = effectTile.TileLoc;
            Tile tile = ZoneManager.Instance.CurrentMap.Tiles[baseLoc.X][baseLoc.Y];
            if (PreOpen)
            {
                if (tile.Effect == owner)
                    tile.Effect = new EffectTile(tile.Effect.TileLoc);
            }

            yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.ProcessBattleFX(baseLoc, baseLoc, Dir8.Down, Anim));
            {
                if (!PreOpen)
                    tile.Effect = new EffectTile(tile.Effect.TileLoc);
                tile.Data = new TerrainTile(DataManager.Instance.GenFloor);
                int distance = 0;
                Loc startLoc = baseLoc - new Loc(distance + 2);
                Loc sizeLoc = new Loc((distance + 2) * 2 + 1);
                ZoneManager.Instance.CurrentMap.MapModified(startLoc, sizeLoc);
            }


            yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(60));

            //say, "The vault doors opened!"/with fanfare
            DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_LOCK_OPEN").ToLocal()));
            if (Fanfare)
            {
                GameManager.Instance.Fanfare("Fanfare/Treasure");
                yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(90));
            }
        }

    }


    [Serializable]
    public class OpenOtherPassageEvent : SingleCharEvent
    {
        [JsonConverter(typeof(MapStatusConverter))]
        [DataType(0, DataManager.DataType.MapStatus, false)]
        public string TimeLimitStatus;
        public FiniteEmitter Emitter;
        public string WarningBGM;
        public StringKey Warning;
        public string WarningSE;

        public OpenOtherPassageEvent()
        {
            TimeLimitStatus = "";
            Emitter = new EmptyFiniteEmitter();
            WarningBGM = "";
            WarningSE = "";
        }

        protected OpenOtherPassageEvent(OpenOtherPassageEvent other)
        {
            TimeLimitStatus = other.TimeLimitStatus;
            Emitter = other.Emitter;
            WarningBGM = other.WarningBGM;
            Warning = other.Warning;
            WarningSE = other.WarningSE;
        }
        public override GameEvent Clone() { return new OpenOtherPassageEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            EffectTile effectTile = (EffectTile)owner;
            //unlock the other doors
            //play the sound
            TileListState tilesState = effectTile.TileStates.GetWithDefault<TileListState>();
            if (tilesState != null)
            {
                List<Loc> locs = tilesState.Tiles;
                if (locs.Count > 0)
                {
                    GameManager.Instance.BattleSE("DUN_Open_Chamber");
                    //remove the tile, and create vfx for each one
                    foreach (Loc loc in locs)
                    {
                        Tile exTile = ZoneManager.Instance.CurrentMap.GetTile(loc);
                        exTile.Effect = new EffectTile(exTile.Effect.TileLoc);

                        SingleEmitter altEmitter = new SingleEmitter(new AnimData("Vault_Open", 3));
                        altEmitter.SetupEmit(loc * GraphicsManager.TileSize + new Loc(GraphicsManager.TileSize / 2), loc * GraphicsManager.TileSize + new Loc(GraphicsManager.TileSize / 2), Dir8.Down);
                        DungeonScene.Instance.CreateAnim(altEmitter, DrawLayer.NoDraw);
                    }
                    yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(30));

                    foreach (Loc loc in locs)
                    {
                        Tile exTile = ZoneManager.Instance.CurrentMap.GetTile(loc);
                        {
                            exTile.Data = new TerrainTile(DataManager.Instance.GenFloor);
                            int distance = 0;
                            Loc startLoc = exTile.Effect.TileLoc - new Loc(distance + 2);
                            Loc sizeLoc = new Loc((distance + 2) * 2 + 1);
                            ZoneManager.Instance.CurrentMap.MapModified(startLoc, sizeLoc);
                        }

                    }

                    //mention the other doors
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_LOCK_OPEN_FLOOR").ToLocal()));
                }
            }

            DangerState danger;
            if (!effectTile.TileStates.TryGet<DangerState>(out danger))
                yield break;

            if (danger.Danger)
            {

                bool aboveTimeLimit = true;
                MapStatus timeLimit;
                if (ZoneManager.Instance.CurrentMap.Status.TryGetValue(TimeLimitStatus, out timeLimit))
                {
                    if (timeLimit.StatusStates.GetWithDefault<MapCountDownState>().Counter <= TimeLimitEvent.WARN_1)
                        aboveTimeLimit = false;
                }

                if (aboveTimeLimit)
                {
                    //cut out music
                    //fanfare wind blowing
                    //play a sudden gust of wind

                    GameManager.Instance.BGM("", true);
                    yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(90) + 30);



                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(Warning.ToLocal()));

                    FiniteEmitter endEmitter = (FiniteEmitter)Emitter.Clone();
                    endEmitter.SetupEmit(DungeonScene.Instance.GetFocusedMapLoc(), DungeonScene.Instance.GetFocusedMapLoc(), Dir8.None);
                    DungeonScene.Instance.CreateAnim(endEmitter, DrawLayer.NoDraw);

                    GameManager.Instance.Fanfare("Battle/" + WarningSE);
                    yield return new WaitForFrames(180);
                    GameManager.Instance.BGM(WarningBGM, true);


                    //start timer

                    MapStatus status = new MapStatus(TimeLimitStatus);
                    status.LoadFromData();
                    MapCountDownState timeState = status.StatusStates.GetWithDefault<MapCountDownState>();
                    timeState.Counter = TimeLimitEvent.WARN_1;
                    status.Hidden = false;
                    yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.AddMapStatus(status));
                }
                else
                    GameManager.Instance.BGM(WarningBGM, true);
            }
        }

    }


    [Serializable]
    public class ChangedSongEvent : SingleCharEvent
    {
        public ChangedSongEvent() { }
        public override GameEvent Clone() { return new ChangedSongEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            EffectTile effectTile = (EffectTile)owner;

            //play ominous music
            SongState song = ((EffectTile)owner).TileStates.GetWithDefault<SongState>();
            if (song.Song != null)
                GameManager.Instance.BGM(song.Song, true);
            yield break;
        }

    }


    [Serializable]
    public class TriggerSwitchEvent : SingleCharEvent
    {
        public bool Simultaneous;

        public TriggerSwitchEvent() { }
        public TriggerSwitchEvent(bool simultaneous)
        {
            Simultaneous = simultaneous;
        }
        protected TriggerSwitchEvent(TriggerSwitchEvent other)
        {
            Simultaneous = other.Simultaneous;
        }
        public override GameEvent Clone() { return new TriggerSwitchEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            EffectTile effectTile = (EffectTile)owner;

            if (context.User.CharStates.Contains<TrapState>())
                yield break;

            CharAnimation standAnim = new CharAnimIdle(context.User.CharLoc, context.User.CharDir);
            standAnim.MajorAnim = true;
            yield return CoroutineManager.Instance.StartCoroutine(context.User.StartAnim(standAnim));

            GameManager.Instance.BattleSE("DUN_Tile_Step");

            yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(30));
            if (!Simultaneous)
            {
                Loc baseLoc = effectTile.TileLoc;
                Tile tile = ZoneManager.Instance.CurrentMap.Tiles[baseLoc.X][baseLoc.Y];
                if (tile.Effect == owner)
                    tile.Effect = new EffectTile(tile.Effect.TileLoc);

                TileReqListState tilesState = effectTile.TileStates.GetWithDefault<TileReqListState>();
                if (tilesState != null)
                {
                    int switchesLeft = tilesState.Tiles.Count;
                    foreach (Loc loc in tilesState.Tiles)
                    {
                        //check that all other target tiles no longer have the switches there
                        Tile checkTile = ZoneManager.Instance.CurrentMap.Tiles[loc.X][loc.Y];
                        if (checkTile.Effect.ID != effectTile.ID)
                            switchesLeft--;
                    }

                    if (switchesLeft > 0)
                    {
                        if (switchesLeft > 1)
                            DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_SWITCH_NEEDED_MULTI").ToLocal(), switchesLeft));
                        else
                            DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_SWITCH_NEEDED_ONE").ToLocal(), switchesLeft));

                        //emote
                        EmoteData emoteData = DataManager.Instance.GetEmote("question");
                        context.User.StartEmote(new Emote(emoteData.Anim, emoteData.LocHeight, 1));
                        GameManager.Instance.BattleSE("EVT_Emote_Confused");

                        context.CancelState.Cancel = true;
                        yield break;
                    }
                }
            }
            else
            {
                TileReqListState tilesState = effectTile.TileStates.GetWithDefault<TileReqListState>();
                if (tilesState != null)
                {
                    bool allowOpen = true;
                    foreach (Loc loc in tilesState.Tiles)
                    {
                        bool foundMember = false;
                        //check that all other target tiles have a player on them
                        foreach (Character player in context.User.MemberTeam.Players)
                        {
                            if (player.CharLoc == loc)
                            {
                                foundMember = true;
                                break;
                            }
                        }
                        if (!foundMember)
                        {
                            allowOpen = false;
                            break;
                        }
                    }

                    if (!allowOpen)
                    {
                        DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_SWITCH_NEEDED_SYNC").ToLocal(), tilesState.Tiles.Count));

                        //emote
                        EmoteData emoteData = DataManager.Instance.GetEmote("question");
                        context.User.StartEmote(new Emote(emoteData.Anim, emoteData.LocHeight, 1));
                        GameManager.Instance.BattleSE("EVT_Emote_Confused");

                        context.CancelState.Cancel = true;
                        yield break;
                    }


                    //if all have been met, remove all switches
                    foreach (Loc loc in tilesState.Tiles)
                    {
                        Tile tile = ZoneManager.Instance.CurrentMap.Tiles[loc.X][loc.Y];
                        if (tile.Effect.ID == effectTile.ID)
                            tile.Effect = new EffectTile(tile.Effect.TileLoc);
                    }
                }
            }
        }


    }

    [Serializable]
    public class ChestEvent : SingleCharEvent
    {
        /// <summary>
        /// The animation to play when the chest opens.
        /// Defaults to Chest_Open.
        /// </summary>
        public AnimData ChestAnimation;

        /// <summary>
        /// The empty chest tile to spawn after the chest is opened.
        /// Defaults to chest_empty.
        /// </summary>
        [DataType(0, DataManager.DataType.Tile, true)]
        public string ChestEmptyTile;

        public ChestEvent()
        {
        }
        public override GameEvent Clone() { return new ChestEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            //TODO: remove hardcoded everything in this block...
            EffectTile effectTile = (EffectTile)owner;

            CharAnimation standAnim = new CharAnimIdle(context.User.CharLoc, context.User.CharDir);
            standAnim.MajorAnim = true;
            yield return CoroutineManager.Instance.StartCoroutine(context.User.StartAnim(standAnim));

            //open chest animation/sound
            Loc baseLoc = effectTile.TileLoc;
            {
                Tile chest = ZoneManager.Instance.CurrentMap.Tiles[baseLoc.X][baseLoc.Y];
                SingleEmitter emitter = new SingleEmitter(ChestAnimation);
                emitter.SetupEmit(baseLoc * GraphicsManager.TileSize + new Loc(GraphicsManager.TileSize / 2), baseLoc * GraphicsManager.TileSize + new Loc(GraphicsManager.TileSize / 2), Dir8.Down);
                DungeonScene.Instance.CreateAnim(emitter, DrawLayer.NoDraw);
            }
            yield return new WaitForFrames(8);
            GameManager.Instance.BattleSE("EVT_Fade_White");
            {
                SingleEmitter emitter = new SingleEmitter(new AnimData("Chest_Light", 4));
                emitter.SetupEmit(baseLoc * GraphicsManager.TileSize + new Loc(GraphicsManager.TileSize / 2) + new Loc(-100, 72), baseLoc * GraphicsManager.TileSize + new Loc(GraphicsManager.TileSize / 2) + new Loc(-80, 52), Dir8.Left);
                DungeonScene.Instance.CreateAnim(emitter, DrawLayer.NoDraw);
            }
            {
                SingleEmitter emitter = new SingleEmitter(new AnimData("Chest_Light", 4));
                emitter.SetupEmit(baseLoc * GraphicsManager.TileSize + new Loc(GraphicsManager.TileSize / 2) + new Loc(100, 72), baseLoc * GraphicsManager.TileSize + new Loc(GraphicsManager.TileSize / 2) + new Loc(80, 52), Dir8.Right);
                DungeonScene.Instance.CreateAnim(emitter, DrawLayer.NoDraw);
            }

            yield return new WaitForFrames(16);

            //fade to white
            yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.FadeOut(true, 20));

            //change the chest to open
            Tile tile = ZoneManager.Instance.CurrentMap.Tiles[baseLoc.X][baseLoc.Y];
            if (tile.Effect == owner)
                tile.Effect = new EffectTile(ChestEmptyTile, true, tile.Effect.TileLoc);// magic number

            //spawn the items
            Rect bounds = ((EffectTile)owner).TileStates.GetWithDefault<BoundsState>().Bounds;
            //items are specified by the state tags
            List<MapItem> items = ((EffectTile)owner).TileStates.GetWithDefault<ItemSpawnState>().Spawns;
            //find the open tiles to spawn in
            List<Loc> freeTiles = Grid.FindTilesInBox(bounds.Start + new Loc(1), bounds.Size - new Loc(2),
                (Loc testLoc) =>
                {
                    Tile testTile = ZoneManager.Instance.CurrentMap.GetTile(testLoc);
                    if (testTile.Data.GetData().BlockType == TerrainData.Mobility.Passable && String.IsNullOrEmpty(testTile.Effect.ID))//hardcoded Walkable check
                    {
                        foreach (MapItem item in ZoneManager.Instance.CurrentMap.Items)
                        {
                            if (item.TileLoc == testLoc)
                                return false;
                        }
                        return true;
                    }
                    return false;
                });

            yield return new WaitForFrames(30);

            int waitTime = (10 + GameManager.Instance.ModifyBattleSpeed(20) - ItemAnim.ITEM_ACTION_TIME + 1);
            List<MapItem> spawnItems = new List<MapItem>();
            //spawn their animations
            for (int ii = 0; ii < items.Count; ii++)
            {
                //Loc? newLoc = ZoneManager.Instance.CurrentMap.FindItemlessTile(baseLoc, bounds.Start + new Loc(1), bounds.Size - new Loc(2), false);
                if (freeTiles.Count == 0)
                    break;

                MapItem item = new MapItem(items[ii]);
                int randIndex = DataManager.Instance.Save.Rand.Next(freeTiles.Count);
                Loc itemTargetLoc = freeTiles[randIndex];
                item.TileLoc = ZoneManager.Instance.CurrentMap.WrapLoc(itemTargetLoc);
                spawnItems.Add(item);
                freeTiles.RemoveAt(randIndex);
                //start the animations
                //NOTE: the animation is a little funky here for wrapped maps
                ItemAnim itemAnim = new ItemAnim(baseLoc * GraphicsManager.TileSize + new Loc(GraphicsManager.TileSize / 2), itemTargetLoc * GraphicsManager.TileSize + new Loc(GraphicsManager.TileSize / 2), item.IsMoney ? GraphicsManager.MoneySprite : DataManager.Instance.GetItem(item.Value).Sprite, GraphicsManager.TileSize / 2, Math.Max(0, waitTime));
                DungeonScene.Instance.CreateAnim(itemAnim, DrawLayer.Normal);
            }
            //they can be thematic, or use whatever's on the floor
            //if it's the former, the postproc has a list that it will copy to this tile
            //if it's the latter, the postproc will just use the spawnlist
            //boundaries for item spawning are specified by the state tags using a rectangle

            //fade back
            yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.FadeIn());

            if (waitTime < 0)
                yield return new WaitForFrames(-waitTime);
            //place the actual items
            for (int ii = 0; ii < spawnItems.Count; ii++)
                ZoneManager.Instance.CurrentMap.Items.Add(spawnItems[ii]);

            GameManager.Instance.Fanfare("Fanfare/Treasure");

            //say, "treasure appeared!"/with fanfare
            DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_TREASURE").ToLocal()));
            yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(60));

            DangerState danger = effectTile.TileStates.Get<DangerState>();
            if (danger.Danger)
            {
                LockdownTileEvent lockdown = new LockdownTileEvent("map_clear_check");// magic number
                MonsterHouseTileEvent monsterHouse = new MonsterHouseTileEvent();
                yield return CoroutineManager.Instance.StartCoroutine(lockdown.Apply(owner, ownerChar, context));
                yield return CoroutineManager.Instance.StartCoroutine(monsterHouse.Apply(owner, ownerChar, context));
            }
        }

    }
    
    /// <summary>
    /// This event spawns a number of items from the source tile/object onto the tiles around it, like the ChestEvent
    /// item spawning.
    /// </summary>
     [Serializable]
    public class SpawnItemsEvent : SingleCharEvent
    {
        public List<MapItem> Items;
        
        /// <summary>
        /// Max range/distance to spawn from the origin.
        /// </summary>
        public int MaxRangeWidth;
        
        /// <summary>
        /// Max range/distance to spawn from the origin.
        /// </summary>
        public int MaxRangeHeight;
        public SpawnItemsEvent() { }
        public override GameEvent Clone() { return new SpawnItemsEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            EffectTile effectTile = (EffectTile)owner;

            Loc baseLoc = effectTile.TileLoc;
            
            //spawn the items
            int locX = baseLoc.X;
            int locY = baseLoc.Y;
            int xWithBorders = MaxRangeWidth + 1;
            int yWithBorders = MaxRangeHeight + 1;
            Rect bounds = new Rect(locX - xWithBorders, locY - yWithBorders, (2 * xWithBorders) + 1, (2 * yWithBorders) + 1);
            //find the open tiles to spawn in
            List<Loc> freeTiles = Grid.FindTilesInBox(bounds.Start + new Loc(1), bounds.Size - new Loc(2),
                (Loc testLoc) =>
                {
                    Tile testTile = ZoneManager.Instance.CurrentMap.GetTile(testLoc);
                    if (testTile != null && !ZoneManager.Instance.CurrentMap.TileBlocked(testLoc) && testTile.Data.GetData().BlockType == TerrainData.Mobility.Passable && String.IsNullOrEmpty(testTile.Effect.ID))//hardcoded Walkable check
                    {
                        foreach (MapItem item in ZoneManager.Instance.CurrentMap.Items)
                        {
                            if (item.TileLoc == testLoc)
                                return false;
                        }
                        return true;
                    }
                    return false;
                });

            int waitTime = GameManager.Instance.ModifyBattleSpeed(ItemAnim.ITEM_ACTION_TIME);
            
            List<MapItem> spawnItems = new List<MapItem>();
            //spawn their animations
            for (int ii = 0; ii < Items.Count; ii++)
            {
                if (freeTiles.Count == 0)
                    break;

                MapItem item = new MapItem(Items[ii]);
                int randIndex = DataManager.Instance.Save.Rand.Next(freeTiles.Count);
                Loc itemTargetLoc = freeTiles[randIndex];
                item.TileLoc = ZoneManager.Instance.CurrentMap.WrapLoc(itemTargetLoc);
                spawnItems.Add(item);
                freeTiles.RemoveAt(randIndex);
                //start the animations
                //NOTE: the animation is a little funky here for wrapped maps
                ItemAnim itemAnim = new ItemAnim(baseLoc * GraphicsManager.TileSize + new Loc(GraphicsManager.TileSize / 2), itemTargetLoc * GraphicsManager.TileSize + new Loc(GraphicsManager.TileSize / 2), item.IsMoney ? GraphicsManager.MoneySprite : DataManager.Instance.GetItem(item.Value).Sprite, GraphicsManager.TileSize / 2, Math.Max(0, waitTime));
                DungeonScene.Instance.CreateAnim(itemAnim, DrawLayer.Normal);
            }

            if (waitTime > 0)
                yield return new WaitForFrames(waitTime);
            
            //place the actual items
            for (int ii = 0; ii < spawnItems.Count; ii++)
                ZoneManager.Instance.CurrentMap.Items.Add(spawnItems[ii]);

            yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(0));
        }

    }
    
    /// <summary>
    /// This event spawns a number of enemy mobs from the source tile/object onto the tiles around it, like the ChestEvent
    /// item spawning.
    /// </summary>
     [Serializable]
    public class SpawnEnemiesEvent : SingleCharEvent
    {
        public List<MobSpawn> Enemies;
        
        /// <summary>
        /// Max range/distance to spawn from the origin.
        /// </summary>
        public int MaxRangeWidth;
        /// <summary>
        /// Max range/distance to spawn from the origin.
        /// </summary>
        public int MaxRangeHeight;
        public SpawnEnemiesEvent() { }
        public override GameEvent Clone() { return new SpawnEnemiesEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            EffectTile effectTile = (EffectTile)owner;

            Loc baseLoc = effectTile.TileLoc;
            
            int locX = baseLoc.X;
            int locY = baseLoc.Y;
            int xWithBorders = MaxRangeWidth + 1;
            int yWithBorders = MaxRangeHeight + 1;
            Rect bounds = new Rect(locX - xWithBorders, locY - yWithBorders, (2 * xWithBorders) + 1, (2 * yWithBorders) + 1);
            //find the open tiles to spawn in
            List<Loc> freeTiles = Grid.FindTilesInBox(bounds.Start + new Loc(1), bounds.Size - new Loc(2),
                (Loc testLoc) =>
                {
                    Tile testTile = ZoneManager.Instance.CurrentMap.GetTile(testLoc);
                    if (testTile != null && !ZoneManager.Instance.CurrentMap.TileBlocked(testLoc) && testTile.Data.GetData().BlockType == TerrainData.Mobility.Passable && String.IsNullOrEmpty(testTile.Effect.ID))//hardcoded Walkable check
                    {
                        foreach (Character chara in ZoneManager.Instance.CurrentMap.ActiveTeam.EnumerateChars())
                        {
                            if (chara.CharLoc == testLoc)
                                return false;
                        }
                        foreach (Team team in ZoneManager.Instance.CurrentMap.AllyTeams)
                        {
                            foreach (Character chara in team.EnumerateChars())
                            {
                                if (chara.CharLoc == testLoc)
                                    return false;
                            }
                        }
                        foreach (Team team in ZoneManager.Instance.CurrentMap.MapTeams)
                        {
                            foreach (Character chara in team.EnumerateChars())
                            {
                                if (chara.CharLoc == testLoc)
                                    return false;
                            }
                        }
                        return true;
                    }
                    return false;
                });
            
            //spawn in mobs
            List<Character> respawns = new List<Character>();
            for (int ii = 0; ii < Enemies.Count; ii++)
            {
                if (freeTiles.Count == 0)
                    break;

                MonsterTeam team = new MonsterTeam();
                Character mob = Enemies[ii].Spawn(team, ZoneManager.Instance.CurrentMap);
                int randIndex = DataManager.Instance.Save.Rand.Next(freeTiles.Count);
                mob.CharLoc = freeTiles[randIndex];
                ZoneManager.Instance.CurrentMap.MapTeams.Add(team);
                mob.RefreshTraits();

                CharAnimDrop dropAnim = new CharAnimDrop();
                dropAnim.CharLoc = mob.CharLoc;
                dropAnim.CharDir = mob.CharDir;
                yield return CoroutineManager.Instance.StartCoroutine(mob.StartAnim(dropAnim));
                freeTiles.RemoveAt(randIndex);

                mob.Tactic.Initialize(mob);

                respawns.Add(mob);
                if (ii % Math.Max(1, Enemies.Count / 5) == 0)
                    yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(10));
            }


            //trigger their map entry methods
            foreach (Character respawn in respawns)
            {
                if (!respawn.Dead)
                {
                    yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.SpecialIntro(respawn));

                    yield return CoroutineManager.Instance.StartCoroutine(respawn.OnMapStart());

                    ZoneManager.Instance.CurrentMap.UpdateExploration(respawn);
                }
            }

            //force everyone to skip their turn
            DungeonScene.Instance.SkipRemainingTurns();

            yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(0));
        }

    }
     
     /// <summary>
     /// This event spawns a number of items from the source tile/object onto the tiles around it, like the ChestEvent
     /// item spawning.  It picks the data randomly from a spawn list.
     /// </summary>
     [Serializable]
    public class SpawnRandomItemsEvent : SingleCharEvent
    {
        public SpawnList<MapItem> Items;

        public int MinAmount;
        public int MaxAmount;
        
        /// <summary>
        /// Max range/distance to spawn from the origin.
        /// </summary>
        public int MaxRangeWidth;
        
        /// <summary>
        /// Max range/distance to spawn from the origin.
        /// </summary>
        public int MaxRangeHeight;
        public SpawnRandomItemsEvent() { }
        public override GameEvent Clone() { return new SpawnItemsEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            EffectTile effectTile = (EffectTile)owner;

            Loc baseLoc = effectTile.TileLoc;
            
            //spawn the items
            int locX = baseLoc.X;
            int locY = baseLoc.Y;
            int xWithBorders = MaxRangeWidth + 1;
            int yWithBorders = MaxRangeHeight + 1;
            Rect bounds = new Rect(locX - xWithBorders, locY - yWithBorders, (2 * xWithBorders) + 1, (2 * yWithBorders) + 1);
            //find the open tiles to spawn in
            List<Loc> freeTiles = Grid.FindTilesInBox(bounds.Start + new Loc(1), bounds.Size - new Loc(2),
                (Loc testLoc) =>
                {
                    Tile testTile = ZoneManager.Instance.CurrentMap.GetTile(testLoc);
                    if (testTile != null && !ZoneManager.Instance.CurrentMap.TileBlocked(testLoc) && testTile.Data.GetData().BlockType == TerrainData.Mobility.Passable && String.IsNullOrEmpty(testTile.Effect.ID))//hardcoded Walkable check
                    {
                        foreach (MapItem item in ZoneManager.Instance.CurrentMap.Items)
                        {
                            if (item.TileLoc == testLoc)
                                return false;
                        }
                        return true;
                    }
                    return false;
                });

            int waitTime = GameManager.Instance.ModifyBattleSpeed(ItemAnim.ITEM_ACTION_TIME);
            
            List<MapItem> spawnItems = new List<MapItem>();
            int amount = DataManager.Instance.Save.Rand.Next(MinAmount, MaxAmount);
            //spawn their animations
            for (int ii = 0; ii < amount; ii++)
            {
                if (freeTiles.Count == 0)
                    break;

                int spawnIndex = Items.PickIndex(ZoneManager.Instance.CurrentMap.Rand);
                MapItem item = new MapItem(Items.GetSpawn(spawnIndex));
                int randIndex = DataManager.Instance.Save.Rand.Next(freeTiles.Count);
                Loc itemTargetLoc = freeTiles[randIndex];
                item.TileLoc = ZoneManager.Instance.CurrentMap.WrapLoc(itemTargetLoc);
                spawnItems.Add(item);
                freeTiles.RemoveAt(randIndex);
                //start the animations
                //NOTE: the animation is a little funky here for wrapped maps
                ItemAnim itemAnim = new ItemAnim(baseLoc * GraphicsManager.TileSize + new Loc(GraphicsManager.TileSize / 2), itemTargetLoc * GraphicsManager.TileSize + new Loc(GraphicsManager.TileSize / 2), item.IsMoney ? GraphicsManager.MoneySprite : DataManager.Instance.GetItem(item.Value).Sprite, GraphicsManager.TileSize / 2, Math.Max(0, waitTime));
                DungeonScene.Instance.CreateAnim(itemAnim, DrawLayer.Normal);
            }

            if (waitTime > 0)
                yield return new WaitForFrames(waitTime);
            
            //place the actual items
            for (int ii = 0; ii < spawnItems.Count; ii++)
                ZoneManager.Instance.CurrentMap.Items.Add(spawnItems[ii]);

            yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(0));
        }

    }
    
     /// <summary>
     /// This event spawns a number of enemy mobs from the source tile/object onto the tiles around it, like the ChestEvent
     /// item spawning.  It picks the data randomly from a spawn list.
     /// </summary>
     [Serializable]
    public class SpawnRandomEnemiesEvent : SingleCharEvent
    {
        public SpawnList<MobSpawn> Enemies;
        public int MinAmount;
        public int MaxAmount;
        
        /// <summary>
        /// Max range/distance to spawn from the origin.
        /// </summary>
        public int MaxRangeWidth;
        /// <summary>
        /// Max range/distance to spawn from the origin.
        /// </summary>
        public int MaxRangeHeight;
        public SpawnRandomEnemiesEvent() { }
        public override GameEvent Clone() { return new SpawnEnemiesEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            EffectTile effectTile = (EffectTile)owner;

            Loc baseLoc = effectTile.TileLoc;
            
            int locX = baseLoc.X;
            int locY = baseLoc.Y;
            int xWithBorders = MaxRangeWidth + 1;
            int yWithBorders = MaxRangeHeight + 1;
            Rect bounds = new Rect(locX - xWithBorders, locY - yWithBorders, (2 * xWithBorders) + 1, (2 * yWithBorders) + 1);
            //find the open tiles to spawn in
            List<Loc> freeTiles = Grid.FindTilesInBox(bounds.Start + new Loc(1), bounds.Size - new Loc(2),
                (Loc testLoc) =>
                {
                    Tile testTile = ZoneManager.Instance.CurrentMap.GetTile(testLoc);
                    if (testTile != null && !ZoneManager.Instance.CurrentMap.TileBlocked(testLoc) && testTile.Data.GetData().BlockType == TerrainData.Mobility.Passable && String.IsNullOrEmpty(testTile.Effect.ID))//hardcoded Walkable check
                    {
                        foreach (Character chara in ZoneManager.Instance.CurrentMap.ActiveTeam.EnumerateChars())
                        {
                            if (chara.CharLoc == testLoc)
                                return false;
                        }
                        foreach (Team team in ZoneManager.Instance.CurrentMap.AllyTeams)
                        {
                            foreach (Character chara in team.EnumerateChars())
                            {
                                if (chara.CharLoc == testLoc)
                                    return false;
                            }
                        }
                        foreach (Team team in ZoneManager.Instance.CurrentMap.MapTeams)
                        {
                            foreach (Character chara in team.EnumerateChars())
                            {
                                if (chara.CharLoc == testLoc)
                                    return false;
                            }
                        }
                        return true;
                    }
                    return false;
                });

            
            //spawn in mobs
            List<Character> respawns = new List<Character>();
            int amount = DataManager.Instance.Save.Rand.Next(MinAmount, MaxAmount);
            for (int ii = 0; ii < amount; ii++)
            {
                if (freeTiles.Count == 0)
                    break;
                
                MonsterTeam team = new MonsterTeam();
                int spawnIndex = Enemies.PickIndex(ZoneManager.Instance.CurrentMap.Rand);
                Character mob = Enemies.GetSpawn(spawnIndex).Copy().Spawn(team, ZoneManager.Instance.CurrentMap);
                int randIndex = DataManager.Instance.Save.Rand.Next(freeTiles.Count);
                mob.CharLoc = freeTiles[randIndex];
                ZoneManager.Instance.CurrentMap.MapTeams.Add(team);
                mob.RefreshTraits();

                CharAnimDrop dropAnim = new CharAnimDrop();
                dropAnim.CharLoc = mob.CharLoc;
                dropAnim.CharDir = mob.CharDir;
                yield return CoroutineManager.Instance.StartCoroutine(mob.StartAnim(dropAnim));
                freeTiles.RemoveAt(randIndex);

                mob.Tactic.Initialize(mob);

                respawns.Add(mob);
                if (ii % Math.Max(1, Enemies.Count / 5) == 0)
                    yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(10));
            }


            //trigger their map entry methods
            foreach (Character respawn in respawns)
            {
                if (!respawn.Dead)
                {
                    yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.SpecialIntro(respawn));

                    yield return CoroutineManager.Instance.StartCoroutine(respawn.OnMapStart());

                    ZoneManager.Instance.CurrentMap.UpdateExploration(respawn);
                }
            }

            //force everyone to skip their turn
            DungeonScene.Instance.SkipRemainingTurns();

            yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(0));
        }

    }

     /// <summary>
     /// This event transforms a specific tile into another tile.
     /// </summary>
     [Serializable]
     public class TransformTileEvent : SingleCharEvent
     {
         public EffectTile TileToTransformInto;
         public TransformTileEvent() { }
         public override GameEvent Clone() { return new TransformTileEvent(); }

         public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar,SingleCharContext context)
         {
             EffectTile effectTile = (EffectTile)owner;
             Loc baseLoc = effectTile.TileLoc;
             Tile tile = ZoneManager.Instance.CurrentMap.Tiles[baseLoc.X][baseLoc.Y];
             if (tile.Effect == owner)
                 tile.Effect = new EffectTile(TileToTransformInto.ID, true, tile.Effect.TileLoc);// magic number
             
             yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(10));
         }

     }
    

    [Serializable]
    public class LockedTile
    {
        public Loc LockedLoc;
        public string OldTerrain;

        public LockedTile(Loc loc, string terrain)
        {
            LockedLoc = loc;
            OldTerrain = terrain;
        }
    }

    [Serializable]
    public abstract class LockdownEvent : SingleCharEvent
    {
        [JsonConverter(typeof(MapStatusConverter))]
        [DataType(0, DataManager.DataType.MapStatus, false)]
        public string CheckClearStatus;

        public LockdownEvent() { CheckClearStatus = ""; }
        public LockdownEvent(string checkClearStatus) { CheckClearStatus = checkClearStatus; }
        public LockdownEvent(LockdownEvent other) { CheckClearStatus = other.CheckClearStatus; }

        protected abstract Rect GetBounds(GameEventOwner owner, Character ownerChar, Character character);

        protected abstract List<SingleCharEvent> GetResultEvents(GameEventOwner owner, Character ownerChar, Character character);


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {

            Rect bounds = GetBounds(owner, ownerChar, context.User);

            yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(60));
            //lock the exits with a "thud"
            GameManager.Instance.BattleSE("EVT_Door_Open");
            DungeonScene.Instance.SetScreenShake(new ScreenMover(3, 6, 15));
            List<LockedTile> blockedLocs = new List<LockedTile>();
            List<Character> moveChars = new List<Character>();
            //exits are the tiles that intersect the bounds specified by the state tags
            for (int yy = 0; yy < bounds.Size.Y; yy++)
            {
                lockTile(new Loc(bounds.X, bounds.Y + yy), blockedLocs, moveChars);
                lockTile(new Loc(bounds.End.X - 1, bounds.Y + yy), blockedLocs, moveChars);
            }
            for (int xx = 1; xx < bounds.Size.X - 1; xx++)
            {
                lockTile(new Loc(bounds.X + xx, bounds.Y), blockedLocs, moveChars);
                lockTile(new Loc(bounds.X + xx, bounds.End.Y - 1), blockedLocs, moveChars);
            }
            //keep track of the locked tiles

            //shove the affected chars off
            foreach(Character moveChar in moveChars)
            {
                //if (moveChar.MemberTeam == ZoneManager.Instance.CurrentMap.ActiveTeam)
                //    continue;

                yield return CoroutineManager.Instance.StartCoroutine(shoveTo(moveChar, moveChar.CharLoc, ZoneManager.Instance.CurrentMap.EntryPoints[0].Loc));
            }

            MapStatus checkStatus;
            if (ZoneManager.Instance.CurrentMap.Status.TryGetValue(CheckClearStatus, out checkStatus))
            {
                MapCheckState check = checkStatus.StatusStates.GetWithDefault<MapCheckState>();
                //add a singlechareffect to map.CheckEffects for counting the enemies and allies in the box each time, with a tab kept on what tiles to unlock when finished
                foreach (SingleCharEvent charEvent in check.CheckEvents)
                {
                    CheckHouseClearEvent checkEvent = charEvent as CheckHouseClearEvent;
                    if (checkEvent != null)
                        bounds = Rect.Intersect(bounds, checkEvent.Bounds);
                }
            }

            List<SingleCharEvent> resultEvents = GetResultEvents(owner, ownerChar, context.User);
            CheckHouseClearEvent checkEnd = new CheckHouseClearEvent(bounds);
            checkEnd.LockedLocs = blockedLocs;
            foreach (SingleCharEvent result in resultEvents)
                checkEnd.ResultEvents.Add((SingleCharEvent)result.Clone());

            {
                MapStatus status = new MapStatus(CheckClearStatus);
                status.LoadFromData();
                MapCheckState check = status.StatusStates.GetWithDefault<MapCheckState>();
                check.CheckEvents.Add(checkEnd);
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.AddMapStatus(status));
            }

            //various members of the team emote
            EmoteData emoteData = DataManager.Instance.GetEmote("sweating");
            EmoteData altEmoteData = DataManager.Instance.GetEmote("shock");
            context.User.StartEmote(new Emote(emoteData.Anim, emoteData.LocHeight, 1));
            Loc? insideLoc = null;
            foreach (Character target in ZoneManager.Instance.CurrentMap.ActiveTeam.IterateByRank())
            {
                if (!target.Dead)
                {
                    if (DataManager.Instance.Save.Rand.Next(2) == 0)
                        context.User.StartEmote(new Emote(altEmoteData.Anim, altEmoteData.LocHeight, 1));

                    if (insideLoc == null && ZoneManager.Instance.CurrentMap.InBounds(bounds, target.CharLoc))
                        insideLoc = target.CharLoc;
                }
            }
            if (insideLoc == null)
                insideLoc = bounds.Center;


            yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(40));

            Rect innerBounds = bounds;
            innerBounds.Inflate(-1, -1);
            //warp stragglers
            foreach (Character target in ZoneManager.Instance.CurrentMap.ActiveTeam.IterateByRank())
            {
                if (!target.Dead)
                {
                    if (!ZoneManager.Instance.CurrentMap.InBounds(innerBounds, target.CharLoc))
                    {
                        yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.WarpNear(target, insideLoc.Value, false));
                        //yield return CoroutineManager.Instance.StartCoroutine(shoveTo(target, insideLoc.Value, insideLoc.Value));
                    }
                }
            }
        }

        private IEnumerator<YieldInstruction> shoveTo(Character moveChar, Loc wantedDest, Loc backupDest)
        {
            Loc? dest = ZoneManager.Instance.CurrentMap.GetClosestTileForChar(moveChar, wantedDest);
            if (dest == null)
                dest = backupDest;

            //do a "shoving" animation
            CharAnimThrown thrown = new CharAnimThrown();
            thrown.FromLoc = moveChar.CharLoc;
            thrown.CharDir = moveChar.CharDir;
            thrown.ToLoc = dest.Value;
            thrown.RecoilLoc = dest.Value;

            thrown.MajorAnim = true;
            yield return CoroutineManager.Instance.StartCoroutine(moveChar.StartAnim(thrown));

            //take care of what happens when they land there
            ZoneManager.Instance.CurrentMap.UpdateExploration(moveChar);
            //or not?
            //yield return CoroutinesManager.Instance.StartCoroutine(DungeonScene.Instance.ArriveOnTile(moveChar));
        }

        private void lockTile(Loc changePoint, List<LockedTile> blockedLocs, List<Character> moveChars)
        {
            Tile modTile = ZoneManager.Instance.CurrentMap.GetTile(changePoint);
            //it ignores the tiles that are already locked
            if (modTile.Data.ID != DataManager.Instance.GenUnbreakable)
            {
                string oldData = modTile.Data.ID;
                modTile.Data = new TerrainTile(DataManager.Instance.GenUnbreakable);
                //if an effect tile is on those tiles, remove the effect
                modTile.Effect = new EffectTile(changePoint);
                int distance = 0;
                Loc startLoc = changePoint - new Loc(distance + 2);
                Loc sizeLoc = new Loc((distance + 2) * 2 + 1);
                ZoneManager.Instance.CurrentMap.MapModified(startLoc, sizeLoc);

                //if a context.User is on those tiles, shove them off
                Character moveChar = ZoneManager.Instance.CurrentMap.GetCharAtLoc(changePoint);
                if (moveChar != null)
                    moveChars.Add(moveChar);

                blockedLocs.Add(new LockedTile(changePoint, oldData));
            }
        }
    }

    [Serializable]
    public class LockdownTileEvent : LockdownEvent
    {
        //activate by tiles; use the tile state variables
        public LockdownTileEvent() { }
        public LockdownTileEvent(string checkClear) : base(checkClear) { }
        protected LockdownTileEvent(LockdownTileEvent other) : base(other)
        { }
        public override GameEvent Clone() { return new LockdownTileEvent(this); }

        protected override Rect GetBounds(GameEventOwner owner, Character ownerChar, Character character)
        {
            BoundsState state = ((EffectTile)owner).TileStates.GetWithDefault<BoundsState>();
            if (state != null)
                return state.Bounds;
            else
                return Rect.Empty;
        }
        protected override List<SingleCharEvent> GetResultEvents(GameEventOwner owner, Character ownerChar, Character character)
        {
            ResultEventState state = ((EffectTile)owner).TileStates.GetWithDefault<ResultEventState>();
            if (state != null)
                return state.ResultEvents;
            else
                return new List<SingleCharEvent>();
        }
    }
    [Serializable]
    public class LockdownMapEvent : LockdownEvent
    {
        //activated by map; use the variables set here
        public Rect Bounds;

        public List<SingleCharEvent> ResultEvents;
        public LockdownMapEvent() { }
        public LockdownMapEvent(string checkClear) : base(checkClear) { }
        protected LockdownMapEvent(LockdownMapEvent other) : base(other)
        {
            Bounds = other.Bounds;
            ResultEvents = new List<SingleCharEvent>();
            foreach (SingleCharEvent result in other.ResultEvents)
                ResultEvents.Add((SingleCharEvent)result.Clone());
        }
        public override GameEvent Clone() { return new LockdownMapEvent(this); }

        protected override Rect GetBounds(GameEventOwner owner, Character ownerChar, Character character)
        {
            return Bounds;
        }
        protected override List<SingleCharEvent> GetResultEvents(GameEventOwner owner, Character ownerChar, Character character)
        {
            return ResultEvents;
        }
    }

    [Serializable]
    public abstract class MonsterHouseEvent : SingleCharEvent
    {
        protected virtual IEnumerator<YieldInstruction> FailSpawn(GameEventOwner owner, Character ownerChar, SingleCharContext context) { yield break; }
        protected abstract Rect GetBounds(GameEventOwner owner, Character ownerChar, Character character);
        protected abstract List<MobSpawn> GetMonsters(GameEventOwner owner, Character ownerChar, Character character);
        protected abstract bool NeedTurnEnd { get; }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            yield return new WaitUntil(DungeonScene.Instance.AnimationsOver);

            Rect bounds = GetBounds(owner, ownerChar, context.User);

            //spawn all contents with the landing animation
            //spawn list is specified by the state tags.  same as items
            List<MobSpawn> mobs = GetMonsters(owner, ownerChar, context.User);
            //find the open tiles to spawn in
            List<Loc> freeTiles = Grid.FindTilesInBox(bounds.Start, bounds.Size,
                (Loc testLoc) =>
                {
                    if (ZoneManager.Instance.CurrentMap.TileBlocked(testLoc))
                        return false;

                    if (ZoneManager.Instance.CurrentMap.GetCharAtLoc(testLoc) != null)
                        return false;

                    return true;
                });

            if (mobs.Count > 0 && freeTiles.Count > 0)
            {
                //it's a monster house!
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_MONSTER_HOUSE").ToLocal()));
                //kick up the music.
                GameManager.Instance.BGM("", false);
                GameManager.Instance.BGM(GraphicsManager.MonsterBGM, false);
            }
            else
                yield return CoroutineManager.Instance.StartCoroutine(FailSpawn(owner, ownerChar, context));

            //spawn them
            List<Character> respawns = new List<Character>();
            for (int ii = 0; ii < mobs.Count; ii++)
            {
                if (freeTiles.Count == 0)
                    break;

                MonsterTeam team = new MonsterTeam();
                Character mob = mobs[ii].Spawn(team, ZoneManager.Instance.CurrentMap);
                int randIndex = DataManager.Instance.Save.Rand.Next(freeTiles.Count);
                mob.CharLoc = freeTiles[randIndex];
                ZoneManager.Instance.CurrentMap.MapTeams.Add(team);
                mob.RefreshTraits();

                CharAnimDrop dropAnim = new CharAnimDrop();
                dropAnim.CharLoc = mob.CharLoc;
                dropAnim.CharDir = mob.CharDir;
                yield return CoroutineManager.Instance.StartCoroutine(mob.StartAnim(dropAnim));
                freeTiles.RemoveAt(randIndex);

                mob.Tactic.Initialize(mob);

                respawns.Add(mob);
                if (ii % Math.Max(1, mobs.Count / 5) == 0)
                    yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(10));
            }

            //turn them to face the highest ranking member on the team within the bounds
            Character turnTarget = null;
            foreach (Character target in ZoneManager.Instance.CurrentMap.ActiveTeam.IterateByRank())
            {
                if (!target.Dead && ZoneManager.Instance.CurrentMap.InBounds(bounds, target.CharLoc))
                {
                    turnTarget = target;
                    break;
                }
            }

            yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(30));


            //trigger their map entry methods
            foreach (Character respawn in respawns)
            {
                if (!respawn.Dead)
                {
                    yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.SpecialIntro(respawn));

                    yield return CoroutineManager.Instance.StartCoroutine(respawn.OnMapStart());

                    if (turnTarget != null)
                    {
                        Dir8 dir = ZoneManager.Instance.CurrentMap.ApproximateClosestDir8(respawn.CharLoc, turnTarget.CharLoc);
                        if (dir > Dir8.None)
                            respawn.CharDir = dir;
                    }

                    ZoneManager.Instance.CurrentMap.UpdateExploration(respawn);
                }
            }

            //force everyone to skip their turn for this entire session
            if (NeedTurnEnd)
                DungeonScene.Instance.SkipRemainingTurns();
        }
    }

    [Serializable]
    public class MonsterHouseTileEvent : MonsterHouseEvent
    {
        //activated by tile; get context info from tile states
        public MonsterHouseTileEvent() { }
        public override GameEvent Clone() { return new MonsterHouseTileEvent(); }
        protected override bool NeedTurnEnd { get { return true; } }

        protected override Rect GetBounds(GameEventOwner owner, Character ownerChar, Character character)
        {
            return ((EffectTile)owner).TileStates.GetWithDefault<BoundsState>().Bounds;
        }
        protected override List<MobSpawn> GetMonsters(GameEventOwner owner, Character ownerChar, Character character)
        {
            return ((EffectTile)owner).TileStates.GetWithDefault<MobSpawnState>().Spawns;
        }
    }

    [Serializable]
    public class MonsterHouseOwnerEvent : MonsterHouseEvent
    {
        /// <summary>
        /// Number of monsters to spawn.
        /// </summary>
        public RandRange MobRange;

        /// <summary>
        /// Percent of monsters carrying items.
        /// </summary>
        public int ItemPercent;

        //activated by user, get mob spawn data from map and locally
        public MonsterHouseOwnerEvent()
        { }
        public MonsterHouseOwnerEvent(RandRange mobRange, int itemPercent)
        {
            MobRange = mobRange;
            ItemPercent = itemPercent;
        }
        public MonsterHouseOwnerEvent(MonsterHouseOwnerEvent other)
        {
            MobRange = other.MobRange;
            ItemPercent = other.ItemPercent;
        }
        public override GameEvent Clone() { return new MonsterHouseOwnerEvent(this); }
        protected override bool NeedTurnEnd { get { return true; } }

        protected override IEnumerator<YieldInstruction> FailSpawn(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            GameManager.Instance.BGM("", false);
            yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(30) + 20);
            DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_NOTHING_HAPPENED").ToLocal()));
            GameManager.Instance.BGM(ZoneManager.Instance.CurrentMap.Music, true);
            yield break;
        }

        protected override Rect GetBounds(GameEventOwner owner, Character ownerChar, Character character)
        {
            return new Rect(character.CharLoc - new Loc(5), new Loc(11));
        }
        protected override List<MobSpawn> GetMonsters(GameEventOwner owner, Character ownerChar, Character character)
        {
            Map map = ZoneManager.Instance.CurrentMap;
            int mobCount = MobRange.Pick(map.Rand);
            List<MobSpawn> chosenMobs = new List<MobSpawn>();

            for (int ii = 0; ii < mobCount; ii++)
            {
                if (map.TeamSpawns.CanPick)
                {
                    List<MobSpawn> exampleList = map.TeamSpawns.Pick(map.Rand).ChooseSpawns(map.Rand);
                    if (exampleList.Count > 0)
                        chosenMobs.Add(exampleList[map.Rand.Next(exampleList.Count)]);
                }
            }

            List<MobSpawn> houseMobs = new List<MobSpawn>();
            foreach (MobSpawn mob in chosenMobs)
            {
                MobSpawn copyMob = mob.Copy();
                if (DataManager.Instance.Save.Rand.Next(PMDC.LevelGen.MonsterHouseBaseStep<MapGenContext>.ALT_COLOR_ODDS) == 0)
                {
                    SkinTableState table = DataManager.Instance.UniversalEvent.UniversalStates.GetWithDefault<SkinTableState>();
                    copyMob.BaseForm.Skin = table.AltColor;
                }
                if (DataManager.Instance.Save.Rand.Next(100) < ItemPercent)
                {
                    MobSpawnItem item = new MobSpawnItem(false);
                    item.Items.Add(map.ItemSpawns.Pick(DataManager.Instance.Save.Rand), 10);
                    copyMob.SpawnFeatures.Add(item);
                }
                houseMobs.Add(copyMob);
            }
            return houseMobs;
        }
    }

    [Serializable]
    public class MonsterHouseMapEvent : MonsterHouseEvent
    {
        //activated by map; use the variables set here
        public Rect Bounds;
        public List<MobSpawn> Mobs;

        public MonsterHouseMapEvent() { Mobs = new List<MobSpawn>(); }
        protected MonsterHouseMapEvent(MonsterHouseMapEvent other) : this()
        {
            Bounds = other.Bounds;
            Mobs = new List<MobSpawn>();
            foreach (MobSpawn spawner in other.Mobs)
                Mobs.Add(spawner.Copy());
        }
        public override GameEvent Clone() { return new MonsterHouseMapEvent(this); }
        protected override bool NeedTurnEnd { get { return false; } }

        protected override Rect GetBounds(GameEventOwner owner, Character ownerChar, Character character)
        {
            return Bounds;
        }
        protected override List<MobSpawn> GetMonsters(GameEventOwner owner, Character ownerChar, Character character)
        {
            return Mobs;
        }
    }

    [Serializable]
    public class MonsterHallMapEvent : SingleCharEvent
    {
        //activated by map; use the variables set here
        public List<Rect> Phases;
        public List<List<MobSpawn>> Mobs;
        public bool Continuation;

        public MonsterHallMapEvent() { Mobs = new List<List<MobSpawn>>(); Phases = new List<Rect>(); }
        protected MonsterHallMapEvent(MonsterHallMapEvent other) : this()
        {
            Phases.AddRange(other.Phases);
            foreach (List<MobSpawn> spawnList in other.Mobs)
            {
                List<MobSpawn> newList = new List<MobSpawn>();
                foreach (MobSpawn spawner in spawnList)
                {
                    newList.Add(spawner.Copy());
                }
                Mobs.Add(newList);
            }
            Continuation = other.Continuation;
        }
        public override GameEvent Clone() { return new MonsterHallMapEvent(this); }
        protected bool NeedTurnEnd { get { return false; } }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {

            yield return new WaitUntil(DungeonScene.Instance.AnimationsOver);

            Rect bounds = Phases[0];

            if (!Continuation)
            {
                //kick up the music.
                GameManager.Instance.BGM("", false);
            }

            //spawn all contents with the landing animation
            //spawn list is specified by the state tags.  same as items
            List<MobSpawn> mobs = Mobs[0];
            //find the open tiles to spawn in
            List<Loc> freeTiles = new List<Loc>();

            GameManager.Instance.BattleSE("DUN_One_Room_Orb");
            for (int xx = bounds.X; xx < bounds.End.X; xx++) 
            {
                for (int yy = bounds.Y; yy < bounds.End.Y; yy++)
                {
                    Tile modTile = ZoneManager.Instance.CurrentMap.GetTile(new Loc(xx, yy));
                    if (modTile.Data.ID == DataManager.Instance.GenUnbreakable)//if impassable
                    {
                        modTile.Data = new TerrainTile(DataManager.Instance.GenFloor);

                        //animate with a crumble

                        Loc animLoc = new Loc(xx, yy);
                        SingleEmitter emitter = new SingleEmitter(new AnimData("Wall_Break", 2));
                        emitter.SetupEmit(animLoc * GraphicsManager.TileSize + new Loc(GraphicsManager.TileSize / 2), animLoc * GraphicsManager.TileSize + new Loc(GraphicsManager.TileSize / 2), Dir8.Down);
                        DungeonScene.Instance.CreateAnim(emitter, DrawLayer.NoDraw);

                        freeTiles.Add(new Loc(xx, yy));
                    }
                }
            }
            ZoneManager.Instance.CurrentMap.MapModified(bounds.Start - new Loc(2), bounds.Size + new Loc(4));

            //spawn them
            List<Character> respawns = new List<Character>();
            for (int ii = 0; ii < mobs.Count; ii++)
            {
                if (freeTiles.Count == 0)
                    break;

                MonsterTeam team = new MonsterTeam();
                Character mob = mobs[ii].Spawn(team, ZoneManager.Instance.CurrentMap);
                int randIndex = DataManager.Instance.Save.Rand.Next(freeTiles.Count);
                mob.CharLoc = freeTiles[randIndex];
                ZoneManager.Instance.CurrentMap.MapTeams.Add(team);
                mob.RefreshTraits();

                CharAnimDrop dropAnim = new CharAnimDrop();
                dropAnim.CharLoc = mob.CharLoc;
                dropAnim.CharDir = mob.CharDir;
                freeTiles.RemoveAt(randIndex);

                mob.Tactic.Initialize(mob);

                respawns.Add(mob);
            }

            yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(30) + 20);

            if (!Continuation)
            {
                //it's a monster house!
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_MONSTER_HOUSE").ToLocal()));
                GameManager.Instance.BGM(GraphicsManager.MonsterBGM, false);
            }

            //turn them to face the highest ranking member on the team within the bounds
            Character turnTarget = null;
            foreach (Character target in ZoneManager.Instance.CurrentMap.ActiveTeam.IterateByRank())
            {
                if (!target.Dead && ZoneManager.Instance.CurrentMap.InBounds(bounds, target.CharLoc))
                {
                    turnTarget = target;
                    break;
                }
            }
            if (turnTarget == null)
                turnTarget = ZoneManager.Instance.CurrentMap.ActiveTeam.Leader;

            yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(30));

            //trigger their map entry methods
            foreach (Character respawn in respawns)
            {
                if (!respawn.Dead)
                {
                    yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.SpecialIntro(respawn));

                    yield return CoroutineManager.Instance.StartCoroutine(respawn.OnMapStart());

                    if (turnTarget != null)
                    {
                        Dir8 dir = ZoneManager.Instance.CurrentMap.ApproximateClosestDir8(respawn.CharLoc, turnTarget.CharLoc);
                        if (dir > Dir8.None)
                            respawn.CharDir = dir;
                    }

                    ZoneManager.Instance.CurrentMap.UpdateExploration(respawn);
                }
            }

            //add a monster hall continuation check event
            if (Phases.Count > 1)
            {
                CheckTurnsPassedEvent check = new CheckTurnsPassedEvent();
                check.TurnTotal = DataManager.Instance.Save.TotalTurns+1;

                MonsterHallMapEvent house = new MonsterHallMapEvent();
                house.Continuation = true;
                for(int ii = 1; ii < Phases.Count; ii++)
                    house.Phases.Add(Phases[ii]);
                for (int ii = 1; ii < Mobs.Count; ii++)
                    house.Mobs.Add(Mobs[ii]);
                check.Effects.Add(house);

                MapCheckState checks = ((MapStatus)owner).StatusStates.GetWithDefault<MapCheckState>();
                checks.CheckEvents.Add(check);
            }
        }
    }


    [Serializable]
    public class BossSpawnEvent : SingleCharEvent
    {
        public BossSpawnEvent() { }
        public override GameEvent Clone() { return new BossSpawnEvent(); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {

            yield return new WaitUntil(DungeonScene.Instance.AnimationsOver);

            GameManager.Instance.BGM("", true);

            GameManager.Instance.BattleSE("EVT_CH01_Transition");
            yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.FadeOut(true, 40));

            EffectTile effectTile = (EffectTile)owner;

            //remove the triggering tile
            Loc baseLoc = effectTile.TileLoc;
            Tile tile = ZoneManager.Instance.CurrentMap.Tiles[baseLoc.X][baseLoc.Y];
            if (tile.Effect == owner)
                tile.Effect = new EffectTile(tile.Effect.TileLoc);

            Rect bounds = ((EffectTile)owner).TileStates.GetWithDefault<BoundsState>().Bounds;

            int playerIndex = 0;
            Character turnTarget = null;
            foreach (Character target in DungeonScene.Instance.ActiveTeam.IterateByRank())
            {
                if (!target.Dead)
                {
                    target.HP = target.MaxHP;

                    List<string> statuses = new List<string>();
                    foreach (StatusEffect oldStatus in target.IterateStatusEffects())
                        statuses.Add(oldStatus.ID);

                    foreach (string statusID in statuses)
                        yield return CoroutineManager.Instance.StartCoroutine(target.RemoveStatusEffect(statusID, false));

                    switch (playerIndex)
                    {
                        case 0:
                            target.CharLoc = baseLoc;
                            break;
                        case 1:
                            target.CharLoc = baseLoc + Dir8.Down.GetLoc();
                            break;
                        case 2:
                            target.CharLoc = baseLoc + Dir8.DownLeft.GetLoc();
                            break;
                        case 3:
                            target.CharLoc = baseLoc + Dir8.DownRight.GetLoc();
                            break;
                        default:
                            Loc? extraLoc = ZoneManager.Instance.CurrentMap.GetClosestTileForChar(target, baseLoc);
                            if (extraLoc.HasValue)
                                target.CharLoc = extraLoc.Value;
                            else
                                target.CharLoc = baseLoc;
                            break;
                    }

                    target.CharDir = Dir8.Up;

                    if (turnTarget == null && ZoneManager.Instance.CurrentMap.InBounds(bounds, target.CharLoc))
                        turnTarget = target;

                    playerIndex++;
                }
            }

            //move all existing mobs to the exit
            foreach (Team team in ZoneManager.Instance.CurrentMap.MapTeams)
            {
                foreach (Character player in team.EnumerateChars())
                {
                    if (!player.Dead && ZoneManager.Instance.CurrentMap.InBounds(bounds, player.CharLoc))
                        shoveTo(player, ZoneManager.Instance.CurrentMap.EntryPoints[0].Loc);
                }
            }

            List<MobSpawn> mobs = ((EffectTile)owner).TileStates.GetWithDefault<MobSpawnState>().Spawns;

            //spawn them
            List<Character> respawns = new List<Character>();
            for (int ii = 0; ii < mobs.Count; ii++)
            {
                MonsterTeam team = new MonsterTeam();
                Character mob = mobs[ii].Spawn(team, ZoneManager.Instance.CurrentMap);
                ZoneManager.Instance.CurrentMap.MapTeams.Add(team);
                mob.RefreshTraits();

                mob.Tactic.Initialize(mob);

                if (turnTarget != null)
                {
                    Dir8 dir = ZoneManager.Instance.CurrentMap.ApproximateClosestDir8(mob.CharLoc, turnTarget.CharLoc);
                    if (dir > Dir8.None)
                        mob.CharDir = dir;
                }

                respawns.Add(mob);
            }

            //move all players away from the respawn
            foreach (Character target in ZoneManager.Instance.CurrentMap.ActiveTeam.IterateByRank())
            {
                if (!target.Dead)
                    shoveTo(target, target.CharLoc);
            }


            yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(30) + 20);

            yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.FadeIn(40));


            SongState song = ((EffectTile)owner).TileStates.GetWithDefault<SongState>();
            if (song != null)
                GameManager.Instance.BGM(song.Song, true);

            //trigger their map entry methods
            foreach (Character respawn in respawns)
            {
                if (!respawn.Dead)
                {
                    yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.SpecialIntro(respawn));

                    yield return CoroutineManager.Instance.StartCoroutine(respawn.OnMapStart());

                    ZoneManager.Instance.CurrentMap.UpdateExploration(respawn);
                }
            }

            //force everyone to skip their turn for this entire session
            DungeonScene.Instance.SkipRemainingTurns();
        }


        private void shoveTo(Character moveChar, Loc wantedDest)
        {
            Loc? dest = ZoneManager.Instance.CurrentMap.GetClosestTileForChar(moveChar, wantedDest);
            if (dest == null)
                dest = wantedDest;

            moveChar.CharLoc = dest.Value;

            //take care of what happens when they land there
            ZoneManager.Instance.CurrentMap.UpdateExploration(moveChar);
        }
    }


    [Serializable]
    public class CheckIntrudeBoundsEvent : SingleCharEvent
    {
        //activated by map; use the variables set here
        public Rect Bounds;
        public List<SingleCharEvent> Effects;

        public CheckIntrudeBoundsEvent() { Effects = new List<SingleCharEvent>(); }
        protected CheckIntrudeBoundsEvent(CheckIntrudeBoundsEvent other) : this()
        {
            Bounds = other.Bounds;
            Effects = new List<SingleCharEvent>();
            foreach (SingleCharEvent effect in other.Effects)
                Effects.Add((SingleCharEvent)effect.Clone());
        }
        public override GameEvent Clone() { return new CheckIntrudeBoundsEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            TurnOrder currentTurn = ZoneManager.Instance.CurrentMap.CurrentTurnMap.CurrentOrder;
            if (currentTurn.Faction == Faction.Player && currentTurn.TurnIndex == 0 && currentTurn.TurnTier == 0)//only check on a fresh turn
            {
                foreach (Character player in ZoneManager.Instance.CurrentMap.ActiveTeam.Players)
                {
                    if (!player.Dead && ZoneManager.Instance.CurrentMap.InBounds(Bounds, player.CharLoc))
                    {
                        //remove this from the map
                        MapCheckState checks = ((MapStatus)owner).StatusStates.GetWithDefault<MapCheckState>();
                        checks.CheckEvents.Remove(this);
                        //activate the single char effects
                        for (int ii = 0; ii < Effects.Count; ii++)
                            yield return CoroutineManager.Instance.StartCoroutine(Effects[ii].Apply(owner, ownerChar, context));

                        yield break;
                    }
                }
            }
        }
    }

    [Serializable]
    public class CheckTurnsPassedEvent : SingleCharEvent
    {
        //activated by map; use the variables set here
        public int TurnTotal;
        public List<SingleCharEvent> Effects;

        public CheckTurnsPassedEvent() { Effects = new List<SingleCharEvent>(); }
        protected CheckTurnsPassedEvent(CheckTurnsPassedEvent other) : this()
        {
            TurnTotal = other.TurnTotal;
            Effects = new List<SingleCharEvent>();
            foreach (SingleCharEvent effect in other.Effects)
                Effects.Add((SingleCharEvent)effect.Clone());
        }
        public override GameEvent Clone() { return new CheckTurnsPassedEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            TurnOrder currentTurn = ZoneManager.Instance.CurrentMap.CurrentTurnMap.CurrentOrder;
            if (currentTurn.Faction == Faction.Player && currentTurn.TurnIndex == 0 && currentTurn.TurnTier == 0)//only check on a fresh turn
            {
                if (DataManager.Instance.Save.TotalTurns >= TurnTotal)
                {
                    //remove this from the map
                    MapCheckState checks = ((MapStatus)owner).StatusStates.GetWithDefault<MapCheckState>();
                    checks.CheckEvents.Remove(this);
                    //activate the single char effects
                    for (int ii = 0; ii < Effects.Count; ii++)
                        yield return CoroutineManager.Instance.StartCoroutine(Effects[ii].Apply(owner, ownerChar, context));

                    yield break;
                }
            }
        }
    }

    [Serializable]
    public class CheckHouseClearEvent : SingleCharEvent
    {
        public Rect Bounds;
        public List<LockedTile> LockedLocs;
        public List<SingleCharEvent> ResultEvents;
        public CheckHouseClearEvent() { LockedLocs = new List<LockedTile>(); ResultEvents = new List<SingleCharEvent>(); }
        public CheckHouseClearEvent(Rect bounds) : this() { Bounds = bounds; }
        protected CheckHouseClearEvent(CheckHouseClearEvent other) : this()
        {
            Bounds = other.Bounds;
            LockedLocs.AddRange(other.LockedLocs);
            foreach (SingleCharEvent result in other.ResultEvents)
                ResultEvents.Add((SingleCharEvent)result.Clone());
        }
        public override GameEvent Clone() { return new CheckHouseClearEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            //for counting the enemies and allies in the box each time, with a tab kept on what tiles to unlock when finished
            bool noFoes = true;
            foreach (Team team in ZoneManager.Instance.CurrentMap.MapTeams)
            {
                foreach (Character player in team.Players)
                {
                    if (!player.Dead && ZoneManager.Instance.CurrentMap.InBounds(Bounds, player.CharLoc))
                    {
                        noFoes = false;
                        break;
                    }
                }
            }

            //when either runs out, resume normal music, and the singlechareffect must be removed from the checklist
            if (noFoes)
            {
                MapCheckState checks = ((MapStatus)owner).StatusStates.GetWithDefault<MapCheckState>();
                checks.CheckEvents.Remove(this);
                bool returnMusic = true;
                //check to make sure no other check event is also active
                foreach (SingleCharEvent charEvent in checks.CheckEvents)
                {
                    CheckHouseClearEvent checkEvent = charEvent as CheckHouseClearEvent;
                    if (checkEvent != null)
                        returnMusic = false;
                }
                if (returnMusic)
                    GameManager.Instance.BGM(ZoneManager.Instance.CurrentMap.Music, true);

                //remove the barriers with a "thud".
                yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(80));
                GameManager.Instance.BattleSE("DUN_Eruption");
                DungeonScene.Instance.SetScreenShake(new ScreenMover(3, 6, 10));
                unlockTile();
                yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(30));
                foreach (SingleCharEvent result in ResultEvents)
                    yield return CoroutineManager.Instance.StartCoroutine(result.Apply(owner, ownerChar, context));
            }
            yield break;
        }

        private void unlockTile()
        {
            foreach (LockedTile lockedTile in LockedLocs)
            {
                Loc changePoint = lockedTile.LockedLoc;
                Tile modTile = ZoneManager.Instance.CurrentMap.Tiles[changePoint.X][changePoint.Y];

                modTile.Data = new TerrainTile(lockedTile.OldTerrain);
                int distance = 0;
                Loc startLoc = changePoint - new Loc(distance + 2);
                Loc sizeLoc = new Loc((distance + 2) * 2 + 1);
                ZoneManager.Instance.CurrentMap.MapModified(startLoc, sizeLoc);
            }
        }
    }

    [Serializable]
    public class CheckTriggersEvent : SingleCharEvent
    {
        public CheckTriggersEvent() { }
        public override GameEvent Clone() { return new CheckTriggersEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            MapCheckState checks = ((MapStatus)owner).StatusStates.GetWithDefault<MapCheckState>();
            for (int ii = checks.CheckEvents.Count - 1; ii >= 0; ii--)
            {
                SingleCharEvent effect = checks.CheckEvents[ii];
                yield return CoroutineManager.Instance.StartCoroutine(effect.Apply(owner, ownerChar, context));
            }
        }
    }


    [Serializable]
    public class PeriodicSpawnEntranceGuards : SingleCharEvent
    {
        public int Period;
        public int Maximum;

        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string GuardStatus;

        public PeriodicSpawnEntranceGuards() { GuardStatus = ""; }
        public PeriodicSpawnEntranceGuards(int period, int maximum, string guardStatus) { Period = period; Maximum = maximum; GuardStatus = guardStatus; }
        public PeriodicSpawnEntranceGuards(PeriodicSpawnEntranceGuards other) { this.Period = other.Period; Maximum = other.Maximum; GuardStatus = other.GuardStatus; }
        public override GameEvent Clone() { return new PeriodicSpawnEntranceGuards(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (context.User != null)
                yield break;

            if (ZoneManager.Instance.CurrentMap.MapTurns % Period != 0)
                yield break;

            if (ZoneManager.Instance.CurrentMap.MapTeams.Count >= Maximum)
                yield break;

            MapStatus status = (MapStatus)owner;
            // spawn a guard at the entrance
            ShopSecurityState securityState = status.StatusStates.Get<ShopSecurityState>();

            List<Loc> exitLocs = WarpToEndEvent.FindExits();
            //spawn once specifically on the stairs
            foreach (Loc exitLoc in exitLocs)
            {
                Loc? dest = ZoneManager.Instance.CurrentMap.GetClosestTileForChar(null, exitLoc);
                if (!dest.HasValue)
                    continue;

                MobSpawn spawn = securityState.Security.Pick(DataManager.Instance.Save.Rand);
                yield return CoroutineManager.Instance.StartCoroutine(PlaceGuard(spawn, dest.Value, GuardStatus));
            }

            // if they're not there, spawn in a random location
            if (exitLocs.Count == 0)
            {
                List<Loc> randLocs = ZoneManager.Instance.CurrentMap.GetFreeToSpawnTiles();

                if (randLocs.Count == 0)
                    yield break;

                Loc dest = randLocs[DataManager.Instance.Save.Rand.Next(randLocs.Count)];
                MobSpawn spawn = securityState.Security.Pick(DataManager.Instance.Save.Rand);
                yield return CoroutineManager.Instance.StartCoroutine(PlaceGuard(spawn, dest, GuardStatus));
            }
        }


        public static IEnumerator<YieldInstruction> PlaceGuard(MobSpawn spawn, Loc dest, string guardStatusId)
        {
            ExplorerTeam team = new ExplorerTeam();
            team.SetRank("normal");
            Character mob = spawn.Spawn(team, ZoneManager.Instance.CurrentMap);

            //add guard status
            StatusEffect guardStatus = new StatusEffect(guardStatusId);
            guardStatus.LoadFromData();
            mob.StatusEffects.Add(guardStatus.ID, guardStatus);

            mob.CharLoc = dest;
            ZoneManager.Instance.CurrentMap.MapTeams.Add(team);
            mob.RefreshTraits();

            CharAnimDrop dropAnim = new CharAnimDrop();
            dropAnim.CharLoc = mob.CharLoc;
            dropAnim.CharDir = mob.CharDir;
            yield return CoroutineManager.Instance.StartCoroutine(mob.StartAnim(dropAnim));
            mob.Tactic.Initialize(mob);

            yield return CoroutineManager.Instance.StartCoroutine(mob.OnMapStart());
            ZoneManager.Instance.CurrentMap.UpdateExploration(mob);
        }
    }

    [Serializable]
    public class InitShopPriceEvent : SingleCharEvent
    {
        [JsonConverter(typeof(TileConverter))]
        [DataType(0, DataManager.DataType.Tile, false)]
        public string ShopTile;

        public InitShopPriceEvent() { }
        public InitShopPriceEvent(string shopTile) { ShopTile = shopTile; }
        public InitShopPriceEvent(InitShopPriceEvent other) { ShopTile = other.ShopTile; }
        public override GameEvent Clone() { return new InitShopPriceEvent(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (context.User != null)
                yield break;

            int price = 0;
            // iterate all items
            foreach (MapItem item in ZoneManager.Instance.CurrentMap.Items)
            {
                Tile tile = ZoneManager.Instance.CurrentMap.Tiles[item.TileLoc.X][item.TileLoc.Y];
                if (tile.Effect.ID == ShopTile)
                    price += item.Price;
            }

            // all items that have a price and are on top of a mat are counted.
            MapStatus status = (MapStatus)owner;
            ShopPriceState priceState = status.StatusStates.Get<ShopPriceState>();
            priceState.Amount = price;
        }
    }

    [Serializable]
    public class EndShopEvent : SingleCharEvent
    {
        [JsonConverter(typeof(TileConverter))]
        [DataType(0, DataManager.DataType.Tile, false)]
        public string ShopTile;

        public EndShopEvent() { }
        public EndShopEvent(string shopTile) { ShopTile = shopTile; }
        public EndShopEvent(EndShopEvent other) { this.ShopTile = other.ShopTile; }
        public override GameEvent Clone() { return new EndShopEvent(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            Loc baseLoc = DungeonScene.Instance.ActiveTeam.Leader.CharLoc;
            Tile tile = ZoneManager.Instance.CurrentMap.Tiles[baseLoc.X][baseLoc.Y];

            if (tile.Effect.ID != ShopTile)
            {
                GameManager.Instance.BGM(ZoneManager.Instance.CurrentMap.Music, true);
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.RemoveMapStatus(owner.GetID()));
            }
        }
    }


    [Serializable]
    public class NullCharEvent : SingleCharEvent
    {
        public SingleCharEvent BaseEvent;

        public NullCharEvent()
        { }
        public NullCharEvent(SingleCharEvent baseEvent)
        {
            BaseEvent = baseEvent;
        }
        protected NullCharEvent(NullCharEvent other)
        {
            BaseEvent = (SingleCharEvent)other.BaseEvent.Clone();
        }
        public override GameEvent Clone() { return new NullCharEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (context.User == null)
                yield return CoroutineManager.Instance.StartCoroutine(BaseEvent.Apply(owner, ownerChar, context));
        }
    }



    [Serializable]
    public class PlayerCharEvent : SingleCharEvent
    {
        public SingleCharEvent BaseEvent;

        public PlayerCharEvent()
        { }
        public PlayerCharEvent(SingleCharEvent baseEvent)
        {
            BaseEvent = baseEvent;
        }
        protected PlayerCharEvent(PlayerCharEvent other)
        {
            BaseEvent = (SingleCharEvent)other.BaseEvent.Clone();
        }
        public override GameEvent Clone() { return new PlayerCharEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (context.User.MemberTeam == DungeonScene.Instance.ActiveTeam)
                yield return CoroutineManager.Instance.StartCoroutine(BaseEvent.Apply(owner, ownerChar, context));
        }
    }



    [Serializable]
    public class LeaderCharEvent : SingleCharEvent
    {
        public SingleCharEvent BaseEvent;

        public LeaderCharEvent()
        { }
        public LeaderCharEvent(SingleCharEvent baseEvent)
        {
            BaseEvent = baseEvent;
        }
        protected LeaderCharEvent(LeaderCharEvent other)
        {
            BaseEvent = (SingleCharEvent)other.BaseEvent.Clone();
        }
        public override GameEvent Clone() { return new LeaderCharEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (context.User == DungeonScene.Instance.ActiveTeam.Leader)
                yield return CoroutineManager.Instance.StartCoroutine(BaseEvent.Apply(owner, ownerChar, context));
        }
    }


    [Serializable]
    public abstract class ShareEquipEvent : SingleCharEvent
    {
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, SingleCharContext context)
        {
            if (!String.IsNullOrEmpty(ownerChar.EquippedItem.ID))
            {
                ItemData entry = (ItemData)ownerChar.EquippedItem.GetData();
                if (CheckEquipPassValidityEvent.CanItemEffectBePassed(entry))
                {
                    foreach (var effect in GetEvents(entry))
                        yield return CoroutineManager.Instance.StartCoroutine(effect.Value.Apply(owner, ownerChar, context));
                }
            }
        }

        protected abstract PriorityList<SingleCharEvent> GetEvents(ItemData entry);
    }

    [Serializable]
    public class ShareOnMapStartsEvent : ShareEquipEvent
    {
        public override GameEvent Clone() { return new ShareOnMapStartsEvent(); }

        protected override PriorityList<SingleCharEvent> GetEvents(ItemData entry) => entry.OnMapStarts;
    }

    [Serializable]
    public class ShareOnMapTurnEndsEvent : ShareEquipEvent
    {
        public override GameEvent Clone() { return new ShareOnMapTurnEndsEvent(); }

        protected override PriorityList<SingleCharEvent> GetEvents(ItemData entry) => entry.OnMapTurnEnds;
    }

    [Serializable]
    public class ShareOnTurnEndsEvent : ShareEquipEvent
    {
        public override GameEvent Clone() { return new ShareOnTurnEndsEvent(); }

        protected override PriorityList<SingleCharEvent> GetEvents(ItemData entry) => entry.OnTurnStarts;
    }

    [Serializable]
    public class ShareOnTurnStartsEvent : ShareEquipEvent
    {
        public override GameEvent Clone() { return new ShareOnTurnStartsEvent(); }

        protected override PriorityList<SingleCharEvent> GetEvents(ItemData entry) => entry.OnTurnStarts;
    }

    [Serializable]
    public class ShareOnDeathsEvent : ShareEquipEvent
    {
        public override GameEvent Clone() { return new ShareOnDeathsEvent(); }

        protected override PriorityList<SingleCharEvent> GetEvents(ItemData entry) => entry.OnDeaths;
    }

    [Serializable]
    public class ShareOnWalksEvent : ShareEquipEvent
    {
        public override GameEvent Clone() { return new ShareOnWalksEvent(); }

        protected override PriorityList<SingleCharEvent> GetEvents(ItemData entry) => entry.OnWalks;
    }
}
