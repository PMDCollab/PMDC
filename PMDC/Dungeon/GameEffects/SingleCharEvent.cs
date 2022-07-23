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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            ((StatusEffect)owner).StatusStates.GetWithDefault<CountDownState>().Counter--;
            if (((StatusEffect)owner).StatusStates.GetWithDefault<CountDownState>().Counter <= 0)
            {
                for (int ii = 0; ii < Effects.Count; ii++)
                    yield return CoroutineManager.Instance.StartCoroutine(Effects[ii].Apply(owner, ownerChar, character));
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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            ItemData entry = DataManager.Instance.GetItem(owner.GetID());
            FamilyState family;
            if (!entry.ItemStates.TryGet<FamilyState>(out family))
                yield break;
            //TODO: String Assets
            if (family.Members.Contains(ownerChar.BaseForm.Species.ToString()))
                yield return CoroutineManager.Instance.StartCoroutine(BaseEvent.Apply(owner, ownerChar, character));
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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            Tile tile = ZoneManager.Instance.CurrentMap.Tiles[character.CharLoc.X][character.CharLoc.Y];
            if (tile.ID == Terrain)
            {
                yield return CoroutineManager.Instance.StartCoroutine(BaseEvent.Apply(owner, ownerChar, character));
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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            if (((StatusEffect)owner).StatusStates.GetWithDefault<CountDownState>().Counter < 0)
                yield break;
            //if (((StatusEffect)owner).StatusStates.Get<RecentState>() != null)
            //    yield break;

            ((StatusEffect)owner).StatusStates.GetWithDefault<CountDownState>().Counter--;
            if (((StatusEffect)owner).StatusStates.GetWithDefault<CountDownState>().Counter <= 0)
                yield return CoroutineManager.Instance.StartCoroutine(character.RemoveStatusEffect(((StatusEffect)owner).ID, ShowMessage));
        }
    }

    [Serializable]
    public class CountUpEvent : SingleCharEvent
    {
        public override GameEvent Clone() { return new CountUpEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            yield return CoroutineManager.Instance.StartCoroutine(character.RestoreHP(((StatusEffect)owner).StatusStates.GetWithDefault<HPState>().HP));
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


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            foreach (AnimEvent anim in Anims)
                yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, character));

            foreach (Character target in ZoneManager.Instance.CurrentMap.IterateCharacters())
            {
                if (!character.Dead && DungeonScene.Instance.GetMatchup(character, target) != Alignment.Foe && ZoneManager.Instance.CurrentMap.InRange(character.CharLoc, target.CharLoc, Range))
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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            if (((StatusEffect)owner).TargetChar == null)
                yield return CoroutineManager.Instance.StartCoroutine(character.RemoveStatusEffect(((StatusEffect)owner).ID, ShowMessage));
        }
    }

    [Serializable]
    public class SoundEvent : SingleCharEvent
    {
        public string Sound;
        
        public override GameEvent Clone() { return new SoundEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            yield return CoroutineManager.Instance.StartCoroutine(character.RemoveStatusEffect(((StatusEffect)owner).ID, ShowMessage));
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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            if (character != null)
                DungeonScene.Instance.LogMsg(String.Format(Message.ToLocal(), character.GetDisplayName(false)));
            else
                DungeonScene.Instance.LogMsg(String.Format(Message.ToLocal()));
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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            DungeonScene.Instance.LogMsg(String.Format(Message.ToLocal(), character.GetDisplayName(false), owner.GetDisplayName()));
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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            GameManager.Instance.BattleSE(Sound);

            if (!character.Unidentifiable)
            {
                FiniteEmitter endEmitter = (FiniteEmitter)Emitter.Clone();
                endEmitter.SetupEmit(character.MapLoc, character.MapLoc, character.CharDir);
                DungeonScene.Instance.CreateAnim(endEmitter, DrawLayer.NoDraw);
            }
            yield return new WaitForFrames(Delay);
        }
    }

    [Serializable]
    public class FractionDamageEvent : SingleCharEvent
    {
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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            if (Message != null)
                DungeonScene.Instance.LogMsg(String.Format(Message, character.GetDisplayName(false)));
            yield return CoroutineManager.Instance.StartCoroutine(character.InflictDamage(Math.Max(1, character.MaxHP / HPFraction)));
        }
    }

    [Serializable]
    public class FractionHealEvent : SingleCharEvent
    {
        public int HPFraction;
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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            if (character.HP < character.MaxHP)
            {
                if (Message.IsValid())
                    DungeonScene.Instance.LogMsg(String.Format(Message.ToLocal(), character.GetDisplayName(false), owner.GetDisplayName()));
                yield return CoroutineManager.Instance.StartCoroutine(character.RestoreHP(Math.Max(1, character.MaxHP / HPFraction), false));
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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            if (!Collision.InBounds(ZoneManager.Instance.CurrentMap.Width, ZoneManager.Instance.CurrentMap.Height, character.CharLoc))
                yield break;

            Tile tile = ZoneManager.Instance.CurrentMap.Tiles[character.CharLoc.X][character.CharLoc.Y];
            if (TileTypes.Contains(tile.Data.ID))
            {
                tile.Data = new TerrainTile(DataManager.Instance.GenFloor);
                int distance = 0;
                Loc startLoc = character.CharLoc - new Loc(distance + 2);
                Loc sizeLoc = new Loc((distance + 2) * 2 + 1);
                ZoneManager.Instance.CurrentMap.MapModified(startLoc, sizeLoc);
            }
        }
    }

    [Serializable]
    public class RemoveLocTrapEvent : SingleCharEvent
    {
        public override GameEvent Clone() { return new RemoveLocTrapEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            if (!Collision.InBounds(ZoneManager.Instance.CurrentMap.Width, ZoneManager.Instance.CurrentMap.Height, character.CharLoc))
                yield break;

            Tile tile = ZoneManager.Instance.CurrentMap.Tiles[character.CharLoc.X][character.CharLoc.Y];
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
        [DataType(1, DataManager.DataType.MapStatus, false)]
        public List<int> States;

        public SingleCharEvent BaseEvent;


        public SingleMapStatusExceptEvent() { States = new List<int>(); }
        public SingleMapStatusExceptEvent(int mapStatus, SingleCharEvent baseEvent) : this()
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


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            //check if the attacker has the right charstate
            bool hasState = false;
            foreach (int state in States)
            {
                if (ZoneManager.Instance.CurrentMap.Status.ContainsKey(state))
                    hasState = true;
            }
            if (!hasState)
                yield return CoroutineManager.Instance.StartCoroutine(BaseEvent.Apply(owner, ownerChar, character));
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


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            //check if the attacker has the right charstate
            bool hasState = false;
            foreach (FlagType state in States)
            {
                if (character.CharStates.Contains(state.FullType))
                    hasState = true;
            }
            if (!hasState)
            {
                yield return CoroutineManager.Instance.StartCoroutine(BaseEvent.Apply(owner, ownerChar, character));
            }
        }

    }

    [Serializable]
    public class GiveStatusEvent : SingleCharEvent
    {
        [DataType(0, DataManager.DataType.Status, false)]
        public int StatusID;
        public bool SilentCheck;
        [SubGroup]
        public StateCollection<StatusState> States;
        public StringKey TriggerMsg;
        [Sound(0)]
        public string TriggerSound;
        public FiniteEmitter TriggerEmitter;

        public GiveStatusEvent() { States = new StateCollection<StatusState>(); }
        public GiveStatusEvent(int statusID, StateCollection<StatusState> states) : this(statusID, states, false) { }
        public GiveStatusEvent(int statusID, StateCollection<StatusState> states, bool silentCheck)
        {
            StatusID = statusID;
            States = states;
            SilentCheck = silentCheck;
            TriggerSound = "";
            TriggerEmitter = new EmptyFiniteEmitter();
        }
        public GiveStatusEvent(int statusID, StateCollection<StatusState> states, bool silentCheck, StringKey trigger)
        {
            StatusID = statusID;
            States = states;
            SilentCheck = silentCheck;
            TriggerMsg = trigger;
            TriggerEmitter = new EmptyFiniteEmitter();
        }
        public GiveStatusEvent(int statusID, StateCollection<StatusState> states, bool silentCheck, StringKey trigger, string triggerSound, FiniteEmitter emitter)
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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            StatusEffect status = new StatusEffect(StatusID);
            status.LoadFromData();
            foreach (StatusState state in States)
                status.StatusStates.Set(state.Clone<StatusState>());

            if (!TriggerMsg.IsValid() && TriggerSound == "")
                yield return CoroutineManager.Instance.StartCoroutine(character.AddStatusEffect(null, status, null, !SilentCheck, true));
            else
            {
                StatusCheckContext statusContext = new StatusCheckContext(null, character, status, false);

                yield return CoroutineManager.Instance.StartCoroutine(character.BeforeStatusCheck(statusContext));
                if (statusContext.CancelState.Cancel)
                    yield break;

                if (TriggerMsg.IsValid())
                    DungeonScene.Instance.LogMsg(String.Format(TriggerMsg.ToLocal(), ownerChar.GetDisplayName(false), owner.GetDisplayName()));
                statusContext.msg = true;

                GameManager.Instance.BattleSE(TriggerSound);

                if (!character.Unidentifiable)
                {
                    FiniteEmitter endEmitter = (FiniteEmitter)TriggerEmitter.Clone();
                    endEmitter.SetupEmit(character.MapLoc, character.MapLoc, character.CharDir);
                    DungeonScene.Instance.CreateAnim(endEmitter, DrawLayer.NoDraw);
                }

                yield return CoroutineManager.Instance.StartCoroutine(character.ExecuteAddStatus(statusContext));
            }
        }
    }
    [Serializable]
    public class RemoveStatusEvent : SingleCharEvent
    {
        [DataType(0, DataManager.DataType.Status, false)]
        public int StatusID;

        public RemoveStatusEvent() { }
        public RemoveStatusEvent(int statusID)
        {
            StatusID = statusID;
        }
        protected RemoveStatusEvent(RemoveStatusEvent other)
        {
            StatusID = other.StatusID;
        }
        public override GameEvent Clone() { return new RemoveStatusEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            yield return CoroutineManager.Instance.StartCoroutine(character.RemoveStatusEffect(StatusID));
        }
    }


    [Serializable]
    public class InvokeAttackEvent : SingleCharEvent
    {
        public CombatAction HitboxAction;
        public ExplosionData Explosion;
        public BattleData NewData;
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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            CharAnimation standAnim = new CharAnimIdle(character.CharLoc, character.CharDir);
            standAnim.MajorAnim = true;
            yield return CoroutineManager.Instance.StartCoroutine(character.StartAnim(standAnim));

            BattleContext newContext = new BattleContext(BattleActionType.Trap);
            newContext.User = character;
            newContext.UsageSlot = BattleContext.FORCED_SLOT;

            newContext.StartDir = newContext.User.CharDir;

            //change move effects
            newContext.Data = new BattleData(NewData);
            newContext.Data.ID = owner.GetID();

            newContext.Explosion = new ExplosionData(Explosion);
            newContext.HitboxAction = HitboxAction.Clone();
            newContext.Strikes = 1;
            newContext.Item = new InvItem();

            if (Msg.IsValid())
                newContext.SetActionMsg(String.Format(Msg.ToLocal(), newContext.User.GetDisplayName(false)));

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
        public int ReqSpecies;
        public int DefaultForme;
        public Dictionary<int, int> WeatherPair;

        public WeatherFormeEvent() { WeatherPair = new Dictionary<int, int>(); }
        public WeatherFormeEvent(int reqSpecies, int defaultForme, Dictionary<int, int> weather)
        {
            ReqSpecies = reqSpecies;
            DefaultForme = defaultForme;
            WeatherPair = weather;
        }
        protected WeatherFormeEvent(WeatherFormeEvent other) : this()
        {
            ReqSpecies = other.ReqSpecies;
            DefaultForme = other.DefaultForme;

            foreach (int weather in other.WeatherPair.Keys)
                WeatherPair.Add(weather, other.WeatherPair[weather]);
        }
        public override GameEvent Clone() { return new WeatherFormeEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
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
            }

            yield break;
        }
    }


    [Serializable]
    public class PreDeathEvent : SingleCharEvent
    {
        public PreDeathEvent() { }
        public override GameEvent Clone() { return new PreDeathEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            int animTime = 10 + GameManager.Instance.ModifyBattleSpeed(50, character.CharLoc);

            if (character.MemberTeam == DungeonScene.Instance.ActiveTeam)
            {
                CharAnimDefeated defeatAnim = new CharAnimDefeated();
                defeatAnim.CharLoc = character.CharLoc;
                defeatAnim.CharDir = character.CharDir;
                defeatAnim.MajorAnim = true;
                defeatAnim.AnimTime = animTime;
                yield return CoroutineManager.Instance.StartCoroutine(character.StartAnim(defeatAnim));
                DungeonScene.Instance.LogMsg(Text.FormatKey("MSG_DEFEAT", character.GetDisplayName(true)));
            }
            else
            {
                CharAnimDefeated defeatAnim = new CharAnimDefeated();
                defeatAnim.CharLoc = character.CharLoc;
                defeatAnim.CharDir = character.CharDir;
                defeatAnim.MajorAnim = true;
                defeatAnim.AnimTime = animTime;
                yield return CoroutineManager.Instance.StartCoroutine(character.StartAnim(defeatAnim));
                DungeonScene.Instance.LogMsg(Text.FormatKey("MSG_DEFEAT_FOE", character.GetDisplayName(true)));

            }

            yield return new WaitForFrames(animTime - 1);

            character.HP = 0;
            character.Dead = true;
        }
    }

    [Serializable]
    public class SetDeathEvent : SingleCharEvent
    {
        public SetDeathEvent() { }
        public override GameEvent Clone() { return new SetDeathEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            character.HP = 0;
            character.Dead = true;

            yield break;
        }
    }

    [Serializable]
    public abstract class HandoutExpEvent : SingleCharEvent
    {
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            if (!character.Dead)
                yield break;


            if (character.MemberTeam == DungeonScene.Instance.ActiveTeam)
                yield return new WaitForFrames(60);
            else
            {
                if (character.EXPMarked)
                {
                    if (character.MemberTeam is ExplorerTeam)
                    {
                        //TODO: hand out EXP only when the final member is defeated
                    }
                    else
                    {
                        for (int ii = 0; ii < DungeonScene.Instance.ActiveTeam.Players.Count; ii++)
                        {
                            if (ii >= DungeonScene.Instance.GainedEXP.Count)
                                DungeonScene.Instance.GainedEXP.Add(0);

                            int exp = GetExp(owner, ownerChar, character, ii);
                            DungeonScene.Instance.GainedEXP[ii] += exp;
                        }
                    }
                }
                DataManager.Instance.Save.SeenMonster(character.BaseForm.Species);
            }
        }

        protected abstract int GetExp(GameEventOwner owner, Character ownerChar, Character character, int idx);
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
        protected HandoutScaledExpEvent(HandoutScaledExpEvent other)
        {
            this.Numerator = other.Numerator;
            this.Denominator = other.Denominator;
        }
        public override GameEvent Clone() { return new HandoutScaledExpEvent(this); }

        protected override int GetExp(GameEventOwner owner, Character ownerChar, Character character, int idx)
        {
            MonsterData monsterData = DataManager.Instance.GetMonster(character.BaseForm.Species);
            MonsterFormData monsterForm = (MonsterFormData)monsterData.Forms[character.BaseForm.Form];
            return expFormula(monsterForm.ExpYield, character.Level);
        }

        private int expFormula(int expYield, int level)
        {
            return (int)((ulong)expYield * (ulong)Numerator * (ulong)level / (ulong)Denominator) + 1;
        }
    }

    /// <summary>
    /// EXP handed out for defeating an enemy is equal to its base EXP yield without any changes.
    /// </summary>
    [Serializable]
    public class HandoutConstantExpEvent : HandoutExpEvent
    {
        public HandoutConstantExpEvent() { }
        public override GameEvent Clone() { return new HandoutConstantExpEvent(); }

        protected override int GetExp(GameEventOwner owner, Character ownerChar, Character character, int idx)
        {
            MonsterData monsterData = DataManager.Instance.GetMonster(character.BaseForm.Species);
            MonsterFormData monsterForm = (MonsterFormData)monsterData.Forms[character.BaseForm.Form];
            return monsterForm.ExpYield;
        }
    }

    /// <summary>
    /// EXP handed out to each team member is scaled based on the team member's level relative to the defeated enemy's level.
    /// BaseEXP * Numerator * (2 * EnemyLv + LevelBuffer) ^ 3 / (EnemyLv + PlayerLv + LevelBuffer) ^ 3 / Denominator + 1
    /// </summary>
    [Serializable]
    public class HandoutRelativeExpEvent : HandoutExpEvent
    {
        public int Numerator;
        public int Denominator;
        public int LevelBuffer;
        public HandoutRelativeExpEvent() { }
        public HandoutRelativeExpEvent(int numerator, int denominator, int levelBuffer) { Numerator = numerator; Denominator = denominator; LevelBuffer = levelBuffer; }
        protected HandoutRelativeExpEvent(HandoutRelativeExpEvent other)
        {
            this.Numerator = other.Numerator;
            this.Denominator = other.Denominator;
            this.LevelBuffer = other.LevelBuffer;
        }
        public override GameEvent Clone() { return new HandoutRelativeExpEvent(this); }

        protected override int GetExp(GameEventOwner owner, Character ownerChar, Character character, int idx)
        {
            int levelDiff = 0;
            Character player = DungeonScene.Instance.ActiveTeam.Players[idx];
            string growth = DataManager.Instance.GetMonster(player.BaseForm.Species).EXPTable;
            GrowthData growthData = DataManager.Instance.GetGrowth(growth);
            while (player.Level + levelDiff < DataManager.Instance.MaxLevel && player.EXP + DungeonScene.Instance.GainedEXP[idx] >= growthData.GetExpTo(player.Level, player.Level + levelDiff + 1))
                levelDiff++;

            MonsterData monsterData = DataManager.Instance.GetMonster(character.BaseForm.Species);
            MonsterFormData monsterForm = (MonsterFormData)monsterData.Forms[character.BaseForm.Form];
            return expFormula(monsterForm.ExpYield, character.Level, player.Level + levelDiff);
        }

        private int expFormula(int expYield, int level, int recipientLv)
        {
            int multNum = 2 * level + LevelBuffer;
            int multDen = recipientLv + level + LevelBuffer;
            return (int)((ulong)expYield * (ulong)Numerator * (ulong)level * (ulong)multNum * (ulong)multNum * (ulong)multNum / (ulong)multDen / (ulong)multDen / (ulong)multDen / (ulong)Denominator) + 1;
        }
    }


    [Serializable]
    public class ImpostorReviveEvent : SingleCharEvent
    {
        public int AbilityID;
        public ImpostorReviveEvent() { }
        public ImpostorReviveEvent(int abilityID) { AbilityID = abilityID; }
        protected ImpostorReviveEvent(ImpostorReviveEvent other) { this.AbilityID = other.AbilityID; }
        public override GameEvent Clone() { return new ImpostorReviveEvent(this); }
        
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            if (!character.Dead)
                yield break;

            if (character.CurrentForm.Species == character.BaseForm.Species)
                yield break;

            foreach (int id in character.BaseIntrinsics)
            {
                if (id == AbilityID)
                {
                    character.OnRemove();
                    character.HP = character.MaxHP;
                    character.Dead = false;
                    character.DefeatAt = "";

                    //smoke poof
                    GameManager.Instance.BattleSE("DUN_Substitute");
                    SingleEmitter emitter = new SingleEmitter(new AnimData("Puff_Green", 3));
                    emitter.SetupEmit(character.MapLoc, character.MapLoc, character.CharDir);
                    DungeonScene.Instance.CreateAnim(emitter, DrawLayer.NoDraw);

                    DungeonScene.Instance.LogMsg(String.Format(new StringKey("MSG_IMPOSTER").ToLocal(), character.GetDisplayName(false)));

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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            if (!character.Dead)
                yield break;
            if (character.MemberTeam == DungeonScene.Instance.ActiveTeam && !AffectPlayers)
                yield break;
            if (character.MemberTeam != DungeonScene.Instance.ActiveTeam && !AffectEnemies)
                yield break;

            int choseRevive = 0;
            if (AskToUse && character.MemberTeam == DungeonScene.Instance.ActiveTeam)
            {
                if (DataManager.Instance.CurrentReplay != null)
                    choseRevive = DataManager.Instance.CurrentReplay.ReadUI();
                else
                {
                    yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.ProcessMenuCoroutine(MenuManager.Instance.CreateQuestion(String.Format(new StringKey("DLG_ASK_FREE_REVIVE").ToLocal(), character.GetDisplayName(false)), true, () => { choseRevive = 1; }, () => { choseRevive = 0; })));

                    DataManager.Instance.LogUIPlay(choseRevive);
                }
            }

            if (choseRevive != 0)
            {
                character.OnRemove();
                character.HP = character.MaxHP;
                character.Dead = false;
                character.DefeatAt = "";

                GameManager.Instance.BattleSE("DUN_Send_Home");
                SingleEmitter emitter = new SingleEmitter(new BeamAnimData("Column_Yellow", 3));
                emitter.Layer = DrawLayer.Front;
                emitter.SetupEmit(character.MapLoc, character.MapLoc, character.CharDir);
                DungeonScene.Instance.CreateAnim(emitter, DrawLayer.NoDraw);

                DungeonScene.Instance.LogMsg(String.Format(new StringKey("MSG_REVIVE").ToLocal(), character.GetDisplayName(false)));

            }
        }
    }



    [Serializable]
    public class AutoReviveEvent : SingleCharEvent
    {
        public bool AskToUse;
        public int ChangeTo;

        public AutoReviveEvent() { }
        public AutoReviveEvent(bool askToUse, int changeTo)
        {
            AskToUse = askToUse;
            ChangeTo = changeTo;
        }
        protected AutoReviveEvent(AutoReviveEvent other)
        {
            AskToUse = other.AskToUse;
            ChangeTo = other.ChangeTo;
        }
        public override GameEvent Clone() { return new AutoReviveEvent(this); }

        private bool isAutoReviveItem(int itemId)
        {
            ItemData entry = DataManager.Instance.GetItem(itemId);

            foreach(SingleCharEvent effect in entry.OnDeaths.EnumerateInOrder())
            {
                if (effect is AutoReviveEvent)
                {
                    if (((AutoReviveEvent)effect).AskToUse)
                        return true;
                }
            }
            return false;
        }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            if (!character.Dead)
                yield break;

            int useIndex = -1;
            int useSlot = -1;

            if (character.MemberTeam is ExplorerTeam)
            {
                ExplorerTeam team = character.MemberTeam as ExplorerTeam;
                Dictionary<int, int> candidateItems = new Dictionary<int, int>();
                if (character.EquippedItem.ID > -1 && !character.EquippedItem.Cursed)
                {
                    if (isAutoReviveItem(character.EquippedItem.ID))
                        candidateItems.Add(character.EquippedItem.ID, BattleContext.EQUIP_ITEM_SLOT);
                }

                //iterate over the inventory, get a list of the lowest/highest-costing eligible items
                for (int ii = 0; ii < team.GetInvCount(); ii++)
                {
                    InvItem item = team.GetInv(ii);
                    if (!candidateItems.ContainsKey(item.ID))
                    {
                        if (isAutoReviveItem(item.ID) && !item.Cursed)
                            candidateItems.Add(item.ID, ii);
                    }
                }

                if (AskToUse && character.MemberTeam == DungeonScene.Instance.ActiveTeam)
                {
                    if (candidateItems.Count > 0)
                    {
                        if (DataManager.Instance.CurrentReplay != null)
                        {
                            useIndex = DataManager.Instance.CurrentReplay.ReadUI();
                            if (useIndex > -1)
                                useSlot = candidateItems[useIndex];
                        }
                        else
                        {
                            List<DialogueChoice> choices = new List<DialogueChoice>();
                            foreach (int itemId in candidateItems.Keys)
                            {
                                ItemData entry = DataManager.Instance.GetItem(itemId);
                                choices.Add(new DialogueChoice(entry.GetIconName(), () =>
                                {
                                    useIndex = itemId;
                                    useSlot = candidateItems[itemId];
                                }));
                            }
                            choices.Add(new DialogueChoice(Text.FormatKey("MENU_CANCEL"), () =>
                            {
                                useIndex = -1;
                                useSlot = -1;
                            }));

                            yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.ProcessMenuCoroutine(MenuManager.Instance.CreateMultiQuestion(String.Format(new StringKey("DLG_ASK_REVIVE").ToLocal()), true, choices, 0, choices.Count - 1)));

                            DataManager.Instance.LogUIPlay(useIndex);
                        }
                    }
                }
                else
                {
                    //use the reviver if the monster is an item master, or if the reviver doesn't ask to use
                    AIPlan plan = (AIPlan)character.Tactic.Plans[0];
                    if (!AskToUse || (plan.IQ & AIFlags.ItemMaster) != AIFlags.None)
                    {
                        foreach (int itemId in candidateItems.Keys)
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
                AIPlan plan = (AIPlan)character.Tactic.Plans[0];
                if (!AskToUse || (plan.IQ & AIFlags.ItemMaster) != AIFlags.None)
                {
                    if (character.EquippedItem.ID > -1 && !character.EquippedItem.Cursed)
                    {
                        if (isAutoReviveItem(character.EquippedItem.ID))
                        {
                            useIndex = character.EquippedItem.ID;
                            useSlot = BattleContext.EQUIP_ITEM_SLOT;
                        }
                    }
                }
            }

            if (useIndex > -1)
            {
                character.OnRemove();
                character.HP = character.MaxHP;
                character.Dead = false;
                character.DefeatAt = "";

                GameManager.Instance.BattleSE("DUN_Send_Home");
                SingleEmitter emitter = new SingleEmitter(new BeamAnimData("Column_Yellow", 3));
                emitter.Layer = DrawLayer.Front;
                emitter.SetupEmit(character.MapLoc, character.MapLoc, character.CharDir);
                DungeonScene.Instance.CreateAnim(emitter, DrawLayer.NoDraw);

                DungeonScene.Instance.LogMsg(String.Format(new StringKey("MSG_REVIVE").ToLocal(), character.GetDisplayName(false)));


                ItemData entry = DataManager.Instance.GetItem(useIndex);

                int changeTo = -1;
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
                    if (changeTo > -1)
                    {
                        character.EquippedItem.ID = ChangeTo;
                        character.EquipItem(character.EquippedItem);
                    }
                    else
                        character.DequipItem();
                }
                else if (character.MemberTeam is ExplorerTeam)
                {
                    ExplorerTeam team = (ExplorerTeam)character.MemberTeam;
                    if (changeTo > -1)
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
    public class PerishEvent : SingleCharEvent
    {
        public int Mult;

        public PerishEvent() { }
        public PerishEvent(int mult)
        {
            Mult = mult;
        }
        protected PerishEvent(PerishEvent other)
        {
            Mult = other.Mult;
        }
        public override GameEvent Clone() { return new PerishEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            CountDownState counter = ((StatusEffect)owner).StatusStates.GetWithDefault<CountDownState>();
            if (counter.Counter < 0)
                yield break;

            counter.Counter--;

            if (counter.Counter % Mult == 0)
                DungeonScene.Instance.LogMsg(String.Format(new StringKey("MSG_PERISH_COUNT").ToLocal(), character.GetDisplayName(false), counter.Counter / Mult));
            if (counter.Counter <= 0)
            {
                yield return CoroutineManager.Instance.StartCoroutine(character.RemoveStatusEffect(((StatusEffect)owner).ID, false));
                GameManager.Instance.BattleSE("DUN_Hit_Super_Effective");
                yield return CoroutineManager.Instance.StartCoroutine(character.InflictDamage(-1));
            }
        }
    }

    [Serializable]
    public class PartialTrapEvent : SingleCharEvent
    {
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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            if (character.CharStates.Contains<MagicGuardState>())
                yield break;
            
            if (Message.IsValid())
                DungeonScene.Instance.LogMsg(String.Format(Message.ToLocal(), character.GetDisplayName(false), owner.GetDisplayName()));

            foreach (AnimEvent anim in Anims)
                yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, character));

            int stack = 1;
            stack += ((StatusEffect)owner).StatusStates.GetWithDefault<StackState>().Stack;
            int trapdmg = Math.Max(1, character.MaxHP * stack / 16);
            yield return CoroutineManager.Instance.StartCoroutine(character.InflictDamage(trapdmg));
            
        }
    }

    [Serializable]
    public class NightmareEvent : SingleCharEvent
    {
        public int SleepID;
        public int Denominator;
        public StringKey Msg;
        public List<AnimEvent> Anims;

        public NightmareEvent()
        {
            Anims = new List<AnimEvent>();
        }
        public NightmareEvent(int sleepID, int denominator, StringKey msg, params AnimEvent[] anims)
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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            StatusEffect sleep = character.GetStatusEffect(SleepID);
            if (sleep != null)
            {
                if (Denominator < 0 && character.HP >= character.MaxHP)
                    yield break;

                DungeonScene.Instance.LogMsg(String.Format(Msg.ToLocal(), character.GetDisplayName(false), owner.GetDisplayName(), ownerChar.GetDisplayName(false)));

                foreach (AnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, character));

                if (Denominator < 0)
                    yield return CoroutineManager.Instance.StartCoroutine(character.RestoreHP(Math.Max(1, character.MaxHP / -Denominator), false));
                else
                    yield return CoroutineManager.Instance.StartCoroutine(character.InflictDamage(Math.Max(1, character.MaxHP / Denominator)));
            }
        }
    }

    [Serializable]
    public class LeechSeedEvent : SingleCharEvent
    {
        public LeechSeedEvent() { }
        public override GameEvent Clone() { return new LeechSeedEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            if (character.CharStates.Contains<MagicGuardState>())
                yield break;
                    
            //check for someone within 4 tiles away; if there's no one, then remove the status
            List<Character> targets = AreaAction.GetTargetsInArea(character, character.CharLoc, Alignment.Foe, 4);
            int lowestDist = Int32.MaxValue;
            Character target = null;
            for (int ii = 0; ii < targets.Count; ii++)
            {
                int newDist = (targets[ii].CharLoc - character.CharLoc).DistSquared();
                if (newDist < lowestDist)
                {
                    target = targets[ii];
                    lowestDist = newDist;
                }
            }

            if (target == null)
                yield return CoroutineManager.Instance.StartCoroutine(character.RemoveStatusEffect(((StatusEffect)owner).ID));
            else
            {
                int seeddmg = Math.Max(1, character.MaxHP / 12);

                DungeonScene.Instance.LogMsg(String.Format(new StringKey("MSG_LEECH_SEED").ToLocal(), character.GetDisplayName(false)));
                
                GameManager.Instance.BattleSE("DUN_Hit_Neutral");
                if (!character.Unidentifiable)
                {
                    SingleEmitter endEmitter = new SingleEmitter(new AnimData("Hit_Neutral", 3));
                    endEmitter.SetupEmit(character.MapLoc, character.MapLoc, character.CharDir);
                    DungeonScene.Instance.CreateAnim(endEmitter, DrawLayer.NoDraw);
                }

                yield return CoroutineManager.Instance.StartCoroutine(character.InflictDamage(seeddmg, false));

                if (character.CharStates.Contains<DrainDamageState>())
                {
                    GameManager.Instance.BattleSE("DUN_Toxic");
                    DungeonScene.Instance.LogMsg(String.Format(new StringKey("MSG_LIQUID_OOZE").ToLocal(), target.GetDisplayName(false)));
                    yield return CoroutineManager.Instance.StartCoroutine(target.InflictDamage(seeddmg * 4, false));
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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            Character chaser = ownerChar;
            if (chaser != null && !ZoneManager.Instance.CurrentMap.InRange(character.CharLoc, chaser.CharLoc, 1))
            {
                if (chaser.CharStates.Contains<AnchorState>())
                    DungeonScene.Instance.LogMsg(String.Format(new StringKey("MSG_CHASE_ANCHOR").ToLocal(), chaser.GetDisplayName(false), character.GetDisplayName(false)));
                else
                {
                    for (int ii = 0; ii < DirRemap.FOCUSED_DIR8.Length; ii++)
                    {
                        Dir8 dir = DirExt.AddAngles(DirRemap.FOCUSED_DIR8[ii], character.CharDir);
                        if (!ZoneManager.Instance.CurrentMap.DirBlocked(dir, character.CharLoc, chaser.Mobility))
                        {
                            Loc targetLoc = character.CharLoc + dir.GetLoc();
                            yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.PointWarp(chaser, targetLoc, false));
                            DungeonScene.Instance.LogMsg(String.Format(new StringKey("MSG_CHASE").ToLocal(), chaser.GetDisplayName(false), character.GetDisplayName(false)));
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
        public int SleepID;
        public EarlyBirdEvent() { }
        public EarlyBirdEvent(int sleepID)
        {
            SleepID = sleepID;
        }
        protected EarlyBirdEvent(EarlyBirdEvent other)
        {
            SleepID = other.SleepID;
        }
        public override GameEvent Clone() { return new EarlyBirdEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            StatusEffect status = character.GetStatusEffect(SleepID);
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
        public override GameEvent Clone() { return new BurnEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            if (!character.CharStates.Contains<HeatproofState>() && !character.CharStates.Contains<MagicGuardState>())
            {
                yield return CoroutineManager.Instance.StartCoroutine(character.InflictDamage(Math.Max(1, character.MaxHP / 16), false));
            }
        }
    }
    [Serializable]
    public class AlternateParalysisEvent : SingleCharEvent
    {
        public override GameEvent Clone() { return new AlternateParalysisEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            ParalyzeState para = ((StatusEffect)owner).StatusStates.GetWithDefault<ParalyzeState>();
            para.Recent = !para.Recent;
            character.RefreshTraits();
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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            if (character.HP == character.MaxHP)
                yield return CoroutineManager.Instance.StartCoroutine(BaseEvent.Apply(owner, ownerChar, character));
        }
    }

    [Serializable]
    public class WeatherNeededSingleEvent : SingleCharEvent
    {
        public int WeatherID;
        public SingleCharEvent BaseEvent;

        public WeatherNeededSingleEvent() { }
        public WeatherNeededSingleEvent(int id, SingleCharEvent baseEffect) { WeatherID = id; BaseEvent = baseEffect; }
        protected WeatherNeededSingleEvent(WeatherNeededSingleEvent other)
        {
            WeatherID = other.WeatherID;
            BaseEvent = (SingleCharEvent)other.BaseEvent.Clone();
        }
        public override GameEvent Clone() { return new WeatherNeededSingleEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            if (ZoneManager.Instance.CurrentMap.Status.ContainsKey(WeatherID))
                yield return CoroutineManager.Instance.StartCoroutine(BaseEvent.Apply(owner, ownerChar, character));
        }
    }

    [Serializable]
    public class RegeneratorEvent : SingleCharEvent
    {
        public int Range;

        public RegeneratorEvent() { }
        public RegeneratorEvent(int range) { Range = range; }
        protected RegeneratorEvent(RegeneratorEvent other)
        {
            Range = other.Range;
        }
        public override GameEvent Clone() { return new RegeneratorEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            foreach (Character target in ZoneManager.Instance.CurrentMap.IterateCharacters())
            {
                if (!target.Dead && DungeonScene.Instance.GetMatchup(character, target) == Alignment.Foe && ZoneManager.Instance.CurrentMap.InRange(character.CharLoc, target.CharLoc, Range))
                    yield break;
            }
            if (character.HP < character.MaxHP)
                yield return CoroutineManager.Instance.StartCoroutine(character.RestoreHP(Math.Max(1, character.MaxHP / 8), false));
        }
    }

    [Serializable]
    public class RoyalVeilEvent : SingleCharEvent
    {
        public int Range;

        public RoyalVeilEvent() { }
        public RoyalVeilEvent(int range) { Range = range; }
        protected RoyalVeilEvent(RoyalVeilEvent other)
        {
            Range = other.Range;
        }
        public override GameEvent Clone() { return new RoyalVeilEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            if (character.HP == character.MaxHP)
            {
                foreach (Character target in ZoneManager.Instance.CurrentMap.IterateCharacters())
                {
                    if (!target.Dead && DungeonScene.Instance.GetMatchup(character, target) == Alignment.Friend && ZoneManager.Instance.CurrentMap.InRange(character.CharLoc, target.CharLoc, Range))
                    {
                        if (target.HP < target.MaxHP)
                            yield return CoroutineManager.Instance.StartCoroutine(target.RestoreHP(Math.Max(1, target.MaxHP / 16), false));
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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            if (DataManager.Instance.Save.Rand.Next(100) < Chance)
                yield return CoroutineManager.Instance.StartCoroutine(BaseEvent.Apply(owner, ownerChar, character));
        }
    }

    [Serializable]
    public class CureAllEvent : SingleCharEvent
    {
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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            List<int> badStatuses = new List<int>();
            foreach (StatusEffect status in character.IterateStatusEffects())
            {
                if (status.StatusStates.Contains<BadStatusState>())
                    badStatuses.Add(status.ID);
            }

            if (badStatuses.Count > 0)
            {
                if (Message.IsValid())
                    DungeonScene.Instance.LogMsg(String.Format(Message.ToLocal(), character.GetDisplayName(false), owner.GetDisplayName()));

                foreach (AnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, character));
            }

            foreach (int statusID in badStatuses)
                yield return CoroutineManager.Instance.StartCoroutine(character.RemoveStatusEffect(statusID, false));

        }
    }


    [Serializable]
    public class AllyReviverEvent : SingleCharEvent
    {
        public AllyReviverEvent() { }
        public override GameEvent Clone() { return new AllyReviverEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            foreach (Character member in character.MemberTeam.EnumerateChars())
            {
                if (member.Dead)
                {
                    Loc? endLoc = ZoneManager.Instance.CurrentMap.GetClosestTileForChar(member, character.CharLoc);
                    if (endLoc == null)
                        endLoc = character.CharLoc;
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

                    DungeonScene.Instance.LogMsg(String.Format(new StringKey("MSG_REVIVE").ToLocal(), member.GetDisplayName(false)));

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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            EffectTile effectTile = (EffectTile)owner;
            TileListState destState = effectTile.TileStates.GetWithDefault<TileListState>();

            if (destState == null)
                yield break;

            CharAnimation standAnim = new CharAnimIdle(character.CharLoc, character.CharDir);
            standAnim.MajorAnim = true;
            yield return CoroutineManager.Instance.StartCoroutine(character.StartAnim(standAnim));

            GameManager.Instance.BattleSE("DUN_Tile_Step");
            effectTile.Revealed = true;

            TileData entry = DataManager.Instance.GetTile(owner.GetID());
            DungeonScene.Instance.LogMsg(String.Format(new StringKey("MSG_TILE_CHECK").ToLocal(), character.GetDisplayName(false), entry.Name.ToLocal()));

            yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(30));

            DungeonScene.Instance.LogMsg(String.Format(new StringKey("MSG_TREASURE_SENSOR").ToLocal()));

            foreach (Loc loc in destState.Tiles)
            {
                Tile tile = ZoneManager.Instance.CurrentMap.GetTile(loc);
                if (!EligibleTiles.Contains(tile.Effect.ID))
                    continue;

                Dir8 stairsDir = ZoneManager.Instance.CurrentMap.ApproximateClosestDir8(character.CharLoc, loc);
                if (stairsDir == Dir8.None)
                    continue;

                FiniteEmitter endEmitter = (FiniteEmitter)Emitter.Clone();
                endEmitter.SetupEmit(character.MapLoc + stairsDir.GetLoc() * 16, character.MapLoc + stairsDir.GetLoc() * 16, stairsDir);
                DungeonScene.Instance.CreateAnim(endEmitter, DrawLayer.NoDraw);
            }

            yield break;
        }
    }


    [Serializable]
    public class StairSensorEvent : SingleCharEvent
    {
        [DataType(0, DataManager.DataType.MapStatus, false)]
        public int SniffedStatusID;
        public FiniteEmitter Emitter;

        public StairSensorEvent()
        {
            Emitter = new EmptyFiniteEmitter();
        }
        public StairSensorEvent(int sniffedID, FiniteEmitter emitter)
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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            if (!ZoneManager.Instance.CurrentMap.Status.ContainsKey(SniffedStatusID))
            {
                Loc? loc = Grid.FindClosestConnectedTile(character.CharLoc - new Loc(CharAction.MAX_RANGE), new Loc(CharAction.MAX_RANGE * 2 + 1),
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
                    character.CharLoc);

                if (loc != null && loc != character.CharLoc)
                {
                    DungeonScene.Instance.LogMsg(String.Format(new StringKey("MSG_STAIR_SENSOR").ToLocal(), ownerChar.GetDisplayName(false), owner.GetDisplayName()));

                    Dir8 stairsDir = ZoneManager.Instance.CurrentMap.ApproximateClosestDir8(character.CharLoc, loc.Value);

                    FiniteEmitter endEmitter = (FiniteEmitter)Emitter.Clone();
                    endEmitter.SetupEmit(character.MapLoc + stairsDir.GetLoc() * 16, character.MapLoc + stairsDir.GetLoc() * 16, stairsDir);
                    DungeonScene.Instance.CreateAnim(endEmitter, DrawLayer.NoDraw);

                }

                MapStatus status = new MapStatus(SniffedStatusID);
                status.LoadFromData();
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.AddMapStatus(status, false));
            }
        }
    }

    [Serializable]
    public class AcuteSnifferEvent : SingleCharEvent
    {
        [DataType(0, DataManager.DataType.MapStatus, false)]
        public int SniffedStatusID;
        public List<AnimEvent> Anims;

        public AcuteSnifferEvent()
        {
            Anims = new List<AnimEvent>();
        }
        public AcuteSnifferEvent(int sniffedID, params AnimEvent[] anims)
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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            if (!ZoneManager.Instance.CurrentMap.Status.ContainsKey(SniffedStatusID))
            {
                DungeonScene.Instance.LogMsg(String.Format(new StringKey("MSG_ACUTE_SNIFFER").ToLocal(), ownerChar.GetDisplayName(false), owner.GetDisplayName(), ZoneManager.Instance.CurrentMap.Items.Count));

                foreach (AnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, character));

                MapStatus status = new MapStatus(SniffedStatusID);
                status.LoadFromData();
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.AddMapStatus(status, false));
            }
        }
    }



    [Serializable]
    public class MapSurveyorEvent : SingleCharEvent
    {
        [DataType(0, DataManager.DataType.MapStatus, false)]
        public int SniffedStatusID;
        public int Radius;

        public MapSurveyorEvent()
        { }
        public MapSurveyorEvent(int sniffedID, int radius)
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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
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
                                Loc loc = character.CharLoc + diff;
                                if (!ZoneManager.Instance.CurrentMap.GetLocInMapBounds(ref loc))
                                    continue;
                                if (ZoneManager.Instance.CurrentMap.DiscoveryArray[loc.X][loc.Y] == Map.DiscoveryState.None)
                                    ZoneManager.Instance.CurrentMap.DiscoveryArray[loc.X][loc.Y] = Map.DiscoveryState.Hinted;
                            }
                        }
                    }
                    yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(4));
                }


                DungeonScene.Instance.LogMsg(String.Format(new StringKey("MSG_MAP_SURVEYOR").ToLocal(), ownerChar.GetDisplayName(false), owner.GetDisplayName()));
            }
        }
    }



    [Serializable]
    public class RevealAllEvent : SingleCharEvent
    {
        public RevealAllEvent() { }
        public override GameEvent Clone() { return new RevealAllEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
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
        [DataType(0, DataManager.DataType.MapStatus, false)]
        public int StatusID;
        public int Counter;
        public StringKey MsgOverride;

        public GiveMapStatusSingleEvent() { }
        public GiveMapStatusSingleEvent(int id)
        {
            StatusID = id;
        }
        public GiveMapStatusSingleEvent(int id, int counter)
        {
            StatusID = id;
            Counter = counter;
        }
        public GiveMapStatusSingleEvent(int id, int counter, StringKey msg)
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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
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
                    DungeonScene.Instance.LogMsg(String.Format(MsgOverride.ToLocal(), ownerChar.GetDisplayName(false)));
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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            //do not activate if already holding an item
            if (character.EquippedItem.ID != -1)
                yield break;

            //do not activate if inv is full
            if (character.MemberTeam is ExplorerTeam)
            {
                if (((ExplorerTeam)character.MemberTeam).GetMaxInvSlots(ZoneManager.Instance.CurrentZone) <= character.MemberTeam.GetInvCount())
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
                DungeonScene.Instance.LogMsg(String.Format(new StringKey("MSG_PICKUP").ToLocal(), character.GetDisplayName(false), item.GetDisplayName()));

                foreach (AnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, character));

                character.EquipItem(item);
                //}
            }

        }
    }

    [Serializable]
    public class GatherEvent : SingleCharEvent
    {
        public int GatherItem;
        public int Chance;
        public List<AnimEvent> Anims;

        public GatherEvent()
        {
            Anims = new List<AnimEvent>();
        }
        public GatherEvent(int gatherItem, int chance, params AnimEvent[] anims)
        {
            GatherItem = gatherItem;
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
            GatherItem = other.GatherItem;
        }
        public override GameEvent Clone() { return new GatherEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            //do not activate if already holding an item
            if (character.EquippedItem.ID != -1)
                yield break;

            //do not activate if inv is full
            if (character.MemberTeam is ExplorerTeam)
            {
                if (((ExplorerTeam)character.MemberTeam).GetMaxInvSlots(ZoneManager.Instance.CurrentZone) <= character.MemberTeam.GetInvCount())
                    yield break;
            }

            if (ZoneManager.Instance.CurrentMap.MapTurns == 0 && DataManager.Instance.Save.Rand.Next(100) < Chance)
            {
                InvItem invItem = new InvItem(GatherItem);
                DungeonScene.Instance.LogMsg(String.Format(new StringKey("MSG_PICKUP").ToLocal(), character.GetDisplayName(false), invItem.GetDisplayName()));

                foreach (AnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, character));

                character.EquipItem(invItem);
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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            int minSlot = -1;
            int minAmount = 100;
            bool canRecover = false;
            for (int ii = 0; ii < character.Skills.Count; ii++)
            {
                if (character.Skills[ii].Element.SkillNum > -1)
                {
                    SkillData data = DataManager.Instance.GetSkill(character.Skills[ii].Element.SkillNum);
                    if (character.Skills[ii].Element.Charges < data.BaseCharges + character.ChargeBoost)
                    {
                        if (character.Skills[ii].Element.Charges < minAmount)
                        {
                            minSlot = ii;
                            minAmount = character.Skills[ii].Element.Charges;
                        }
                        canRecover = true;
                    }
                }
            }

            if (RestoreAll)
            {
                if (canRecover)
                {
                    yield return CoroutineManager.Instance.StartCoroutine(character.RestoreCharges(-1, 1, true, false));
                }
            }
            else
            {
                if (minSlot > -1)
                {
                    yield return CoroutineManager.Instance.StartCoroutine(character.RestoreCharges(minSlot, 1, true, false));
                }
            }
        }
    }


    [Serializable]
    public class PlateElementEvent : SingleCharEvent
    {
        [JsonConverter(typeof(ItemElementDictConverter))]
        [DataType(2, DataManager.DataType.Element, false)]
        public Dictionary<int, string> TypePair;

        public PlateElementEvent() { TypePair = new Dictionary<int, string>(); }
        public PlateElementEvent(Dictionary<int, string> typePair)
        {
            TypePair = typePair;
        }
        protected PlateElementEvent(PlateElementEvent other)
            : this()
        {
            foreach (int plate in other.TypePair.Keys)
                TypePair.Add(plate, other.TypePair[plate]);
        }
        public override GameEvent Clone() { return new PlateElementEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            string element;
            if (!TypePair.TryGetValue(character.EquippedItem.ID, out element))
                element = "normal";

            if (!(character.Element1 == element && character.Element2 == DataManager.Instance.DefaultElement))
                yield return CoroutineManager.Instance.StartCoroutine(character.ChangeElement(element, DataManager.Instance.DefaultElement));
        }
    }

    [Serializable]
    public class GiveIllusionEvent : SingleCharEvent
    {
        public int IllusionID;

        public GiveIllusionEvent() { }
        public GiveIllusionEvent(int illusionID)
        {
            IllusionID = illusionID;
        }
        protected GiveIllusionEvent(GiveIllusionEvent other)
        {
            IllusionID = other.IllusionID;
        }
        public override GameEvent Clone() { return new GiveIllusionEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            if (ZoneManager.Instance.CurrentMap.TeamSpawns.CanPick)
            {
                TeamSpawner spawner = ZoneManager.Instance.CurrentMap.TeamSpawns.Pick(DataManager.Instance.Save.Rand);
                List<MobSpawn> candidateSpecies = spawner.ChooseSpawns(DataManager.Instance.Save.Rand);

                if (candidateSpecies.Count > 0)
                {
                    StatusEffect status = new StatusEffect(IllusionID);
                    status.LoadFromData();
                    MonsterID id = candidateSpecies[DataManager.Instance.Save.Rand.Next(candidateSpecies.Count)].BaseForm;
                    id.Form = Math.Max(0, id.Form);
                    id.Skin = Math.Max(0, id.Skin);
                    id.Gender = (Gender)Math.Max(0, (int)id.Gender);
                    status.StatusStates.Set(new MonsterIDState(id));
                    if (character.MemberTeam == DungeonScene.Instance.ActiveTeam)
                        DungeonScene.Instance.LogMsg(String.Format(new StringKey("MSG_ILLUSION_START").ToLocal(), character.GetDisplayName(true)));
                    yield return CoroutineManager.Instance.StartCoroutine(character.AddStatusEffect(status));
                }
            }
        }
    }

    [Serializable]
    public class WeatherAlignedEvent : SingleCharEvent
    {
        public int BadWeatherID;
        public int GoodWeatherID;

        public WeatherAlignedEvent() { }
        public WeatherAlignedEvent(int badId, int goodId) { BadWeatherID = badId; GoodWeatherID = goodId; }
        protected WeatherAlignedEvent(WeatherAlignedEvent other)
        {
            BadWeatherID = other.BadWeatherID;
            GoodWeatherID = other.GoodWeatherID;
        }
        public override GameEvent Clone() { return new WeatherAlignedEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            int hp = Math.Max(1, character.MaxHP / 12);
            if (ZoneManager.Instance.CurrentMap.Status.ContainsKey(BadWeatherID))
            {
                if (character.CharStates.Contains<MagicGuardState>())
                    yield break;

                MapStatus status = ZoneManager.Instance.CurrentMap.Status[BadWeatherID];
                if (status.StatusStates.GetWithDefault<MapTickState>().Counter % 5 == 0)
                    yield return CoroutineManager.Instance.StartCoroutine(character.InflictDamage(hp, false));
            }
            else if (ZoneManager.Instance.CurrentMap.Status.ContainsKey(GoodWeatherID))
            {
                if (character.HP < character.MaxHP)
                {
                    MapStatus status = ZoneManager.Instance.CurrentMap.Status[GoodWeatherID];
                    if (status.StatusStates.GetWithDefault<MapTickState>().Counter % 5 == 0)
                        yield return CoroutineManager.Instance.StartCoroutine(character.RestoreHP(hp, false));
                }
            }

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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            if (character != null && ((MapStatus)owner).StatusStates.GetWithDefault<MapTickState>().Counter % 5 == 0)
            {
                foreach (FlagType state in States)
                {
                    if (character.CharStates.Contains(state.FullType))
                        yield break;
                }

                foreach (string element in ExceptionElements)
                {
                    if (character.HasElement(element))
                        yield break;
                }

                yield return CoroutineManager.Instance.StartCoroutine(character.InflictDamage(Math.Max(1, character.MaxHP / 12), false));
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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            if (character != null && ((MapStatus)owner).StatusStates.GetWithDefault<MapTickState>().Counter % 5 == 0)
            {
                foreach (string element in ExceptionElements)
                {
                    if (character.HasElement(element))
                        yield break;
                }

                if (character.HP < character.MaxHP)
                    yield return CoroutineManager.Instance.StartCoroutine(character.RestoreHP(Math.Max(1, character.MaxHP / 12), false));
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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            if (character.MemberTeam is ExplorerTeam)
                character.FullnessRemainder += HungerAmount;
            yield break;
        }
    }


    [Serializable]
    public class WeatherFillEvent : SingleCharEvent
    {
        public WeatherFillEvent() { }
        public override GameEvent Clone() { return new WeatherFillEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            if (character == null)
            {
                bool hasWeather = false;
                foreach (MapStatus removeStatus in ZoneManager.Instance.CurrentMap.Status.Values)
                {
                    if (removeStatus.StatusStates.Contains<MapWeatherState>())
                        hasWeather = true;
                }
                if (!hasWeather)
                {
                    MapIndexState weatherIndex = ((MapStatus)owner).StatusStates.GetWithDefault<MapIndexState>();
                    if (weatherIndex != null)
                    {
                        MapStatus status = new MapStatus(weatherIndex.Index);
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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            if (character == null)
            {
                MapIndexState weatherIndex = ((MapStatus)owner).StatusStates.GetWithDefault<MapIndexState>();
                if (weatherIndex != null)
                {
                    bool hasWeather = false;
                    foreach (MapStatus removeStatus in ZoneManager.Instance.CurrentMap.Status.Values)
                    {
                        if (removeStatus.ID == weatherIndex.Index)
                            hasWeather = true;
                    }
                    if (!hasWeather)
                    {
                        MapStatus status = new MapStatus(weatherIndex.Index);
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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            if (character == null)
            {
                MapCountDownState countdown = ((MapStatus)owner).StatusStates.GetWithDefault<MapCountDownState>();
                if (countdown != null && countdown.Counter > -1)
                {
                    countdown.Counter--;
                    if (countdown.Counter <= 0)//TODO: String Assets owner.GetID()
                        yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.RemoveMapStatus(((MapStatus)owner).ID));
                }
            }
        }
    }

    [Serializable]
    public class MapTickEvent : SingleCharEvent
    {
        public MapTickEvent() { }
        public override GameEvent Clone() { return new MapTickEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            if (character == null)
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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            if (character == null)
            {
                MapCountDownState countdown = ((MapStatus)owner).StatusStates.GetWithDefault<MapCountDownState>();
                if (countdown != null && countdown.Counter > -1)
                {
                    countdown.Counter--;
                    if (countdown.Counter == WARN_1)
                    {
                        ((MapStatus)owner).Hidden = false;
                        DungeonScene.Instance.LogMsg(String.Format(Warning1.ToLocal()));

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
                        DungeonScene.Instance.LogMsg(String.Format(Warning2.ToLocal()));

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
                        DungeonScene.Instance.LogMsg(String.Format(Warning3.ToLocal()));

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
                        //TODO: String Assets owner.GetID()
                        yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.RemoveMapStatus(((MapStatus)owner).ID));
                        DungeonScene.Instance.LogMsg(String.Format(TimeOut.ToLocal()));

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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            EffectTile effectTile = (EffectTile)owner;

            if (!effectTile.Revealed)
            {
                GameManager.Instance.BattleSE("DUN_Smokescreen");
                SingleEmitter emitter = new SingleEmitter(new AnimData("Puff_Brown", 3));
                emitter.Layer = DrawLayer.Front;
                emitter.SetupEmit(effectTile.MapLoc, effectTile.MapLoc, character.CharDir);
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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            if (character == DungeonScene.Instance.ActiveTeam.Leader)
            {
                UnlockState unlock = ((EffectTile)owner).TileStates.GetWithDefault<UnlockState>();
                if (unlock == null)
                    yield break;

                int itemSlot = -2;

                if (character.EquippedItem.ID == unlock.UnlockItem && !character.EquippedItem.Cursed)
                    itemSlot = BattleContext.EQUIP_ITEM_SLOT;
                else if (character.MemberTeam is ExplorerTeam)
                {
                    for (int ii = 0; ii < ((ExplorerTeam)character.MemberTeam).GetInvCount(); ii++)
                    {
                        InvItem item = ((ExplorerTeam)character.MemberTeam).GetInv(ii);
                        if (item.ID == unlock.UnlockItem && !item.Cursed)
                        {
                            itemSlot = ii;
                            break;
                        }
                    }
                }

                ItemData itemEntry = DataManager.Instance.GetItem(unlock.UnlockItem);
                if (itemSlot > -2)
                    DungeonScene.Instance.PendingLeaderAction = MenuManager.Instance.ProcessMenuCoroutine(askItemUseQuestion(itemSlot, itemEntry));
                else
                    DungeonScene.Instance.PendingLeaderAction = MenuManager.Instance.SetSign(String.Format(new StringKey("DLG_LOCK").ToLocal(), itemEntry.GetIconName()));

            }
        }

        private DialogueBox askItemUseQuestion(int itemSlot, ItemData item)
        {
            return MenuManager.Instance.CreateQuestion(String.Format(new StringKey("DLG_LOCK_KEY").ToLocal(), item.GetIconName()),
                () => { MenuManager.Instance.EndAction = DungeonScene.Instance.ProcessPlayerInput(new GameAction(GameAction.ActionType.UseItem, Dir8.None, itemSlot, -1)); },
                () => { });
        }


    }

    [Serializable]
    public class NoticeEvent : SingleCharEvent
    {
        public NoticeEvent() { }
        public override GameEvent Clone() { return new NoticeEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            if (character == DungeonScene.Instance.ActiveTeam.Leader)
            {
                NoticeState notice = ((EffectTile)owner).TileStates.GetWithDefault<NoticeState>();
                if (notice == null)
                    yield break;
                GameManager.Instance.SE("Menu/Confirm");
                if (!notice.Title.Key.IsValid())
                    DungeonScene.Instance.PendingLeaderAction = MenuManager.Instance.SetSign(notice.Content.FormatLocal());
                else
                    DungeonScene.Instance.PendingLeaderAction = MenuManager.Instance.ProcessMenuCoroutine(MenuManager.Instance.CreateNotice(notice.Title.FormatLocal(), notice.Content.FormatLocal()));
                yield break;
            }
        }
    }

    [Serializable]
    public class SingleCharStateScriptEvent : SingleCharEvent
    {
        public override GameEvent Clone() { return new SingleCharStateScriptEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            TileScriptState state = ((EffectTile)owner).TileStates.GetWithDefault<TileScriptState>();
            if (state == null)
                yield break;

            LuaTable args = LuaEngine.Instance.RunString("return " + state.ArgTable).First() as LuaTable;
            object[] parameters = new object[] { owner, ownerChar, character, args };
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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            if (character == DungeonScene.Instance.ActiveTeam.Leader)
                DungeonScene.Instance.PendingLeaderAction = PromptTileCheck(owner);
            yield break;
        }

        public IEnumerator<YieldInstruction> PromptTileCheck(GameEventOwner owner)
        {
            Loc baseLoc = ((EffectTile)owner).TileLoc;
            if (DungeonScene.Instance.ActiveTeam.Leader.CharLoc == baseLoc && ZoneManager.Instance.CurrentMap.Tiles[baseLoc.X][baseLoc.Y].Effect == owner)
            {
                GameManager.Instance.SE("Menu/Confirm");//TODO: String Assets owner.GetID()
                yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.ProcessMenuCoroutine(new TileUnderfootMenu(((EffectTile)owner).ID, ((EffectTile)owner).Danger)));

            }
        }
    }

    [Serializable]
    public class AskEvoEvent : SingleCharEvent
    {
        public int ExceptionItem;

        public AskEvoEvent() { }
        public AskEvoEvent(int exceptItem) { ExceptionItem = exceptItem; }
        public AskEvoEvent(AskEvoEvent other) { ExceptionItem = other.ExceptionItem; }
        public override GameEvent Clone() { return new AskEvoEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            if (character.MemberTeam == DungeonScene.Instance.ActiveTeam)
            {
                CharAnimation standAnim = new CharAnimIdle(character.CharLoc, character.CharDir);
                standAnim.MajorAnim = true;
                yield return CoroutineManager.Instance.StartCoroutine(character.StartAnim(standAnim));

                if (DataManager.Instance.CurrentReplay != null)
                {
                    int index = DataManager.Instance.CurrentReplay.ReadUI();
                    if (index > -1)
                    {
                        string currentSong = GameManager.Instance.Song;
                        GameManager.Instance.BGM("", true);

                        yield return CoroutineManager.Instance.StartCoroutine(beginEvo(character, index));

                        GameManager.Instance.BGM(currentSong, true);
                    }
                }
                else
                {
                    string currentSong = GameManager.Instance.Song;
                    GameManager.Instance.BGM("", true);

                    int index = -1;

                    yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.SetDialogue(String.Format(new StringKey("DLG_EVO_INTRO").ToLocal())));
                    yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.ProcessMenuCoroutine(createEvoQuestion(character, (int slot) => { index = slot; })));

                    if (DataManager.Instance.CurrentReplay == null)
                        DataManager.Instance.LogUIPlay(index);

                    if (index > -1)
                        yield return CoroutineManager.Instance.StartCoroutine(beginEvo(character, index));

                    yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.SetDialogue(String.Format(new StringKey("DLG_EVO_END").ToLocal())));

                    GameManager.Instance.BGM(currentSong, true);
                }
            }
        }

        private DialogueBox createEvoQuestion(Character character, VertChoiceMenu.OnChooseSlot action)
        {
            return MenuManager.Instance.CreateQuestion(String.Format(new StringKey("DLG_EVO_ASK").ToLocal()), () =>
            {
                //check for valid branches
                MonsterData entry = DataManager.Instance.GetMonster(character.BaseForm.Species);
                bool bypass = character.EquippedItem.ID == ExceptionItem;
                bool hasReq = false;
                List<int> validEvos = new List<int>();
                for (int ii = 0; ii < entry.Promotions.Count; ii++)
                {
                    if (!DataManager.Instance.DataIndices[DataManager.DataType.Monster].Entries[entry.Promotions[ii].Result.ToString()].Released)
                        continue;
                    bool hardReq = false;
                    if (entry.Promotions[ii].IsQualified(character, true))
                        validEvos.Add(ii);
                    else
                    {
                        foreach (PromoteDetail detail in entry.Promotions[ii].Details)
                        {
                            if (detail.IsHardReq() && !detail.GetReq(character))
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
                        MenuManager.Instance.AddMenu(MenuManager.Instance.CreateDialogue(String.Format(new StringKey("DLG_EVO_NONE_NOW").ToLocal(), character.GetDisplayName(true))), false);
                    else
                        MenuManager.Instance.AddMenu(MenuManager.Instance.CreateDialogue(String.Format(new StringKey("DLG_EVO_NONE").ToLocal(), character.GetDisplayName(true))), false);
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
                    MenuManager.Instance.AddMenu(MenuManager.Instance.CreateMultiQuestion(String.Format(new StringKey("DLG_EVO_CHOICE").ToLocal(), character.GetDisplayName(true)), true, choices, 0, choices.Count - 1), false);
                }
            }, () => { });
        }

        private DialogueBox createTryEvoQuestion(Character character, VertChoiceMenu.OnChooseSlot action, int branchIndex)
        {
            MonsterData entry = DataManager.Instance.GetMonster(character.BaseForm.Species);
            PromoteBranch branch = entry.Promotions[branchIndex];
            bool bypass = character.EquippedItem.ID == ExceptionItem;
            int evoItem = -1;
            foreach (PromoteDetail detail in branch.Details)
            {
                if (detail.GiveItem > -1)
                {
                    evoItem = detail.GiveItem;
                    break;
                }
            }
            //factor in exception item to this question
            if (bypass)
                evoItem = ExceptionItem;
            string question = (evoItem > -1) ? String.Format(new StringKey("DLG_EVO_CONFIRM_ITEM").ToLocal(), character.GetDisplayName(true), DataManager.Instance.GetItem(evoItem).GetIconName(), DataManager.Instance.GetMonster(branch.Result).GetColoredName()) : String.Format(new StringKey("DLG_EVO_CONFIRM").ToLocal(), character.GetDisplayName(true), DataManager.Instance.GetMonster(branch.Result).GetColoredName());
            return MenuManager.Instance.CreateQuestion(question, () => { action(branchIndex); }, () => { });
        }

        private IEnumerator<YieldInstruction> beginEvo(Character character, int branchIndex)
        {
            MonsterData oldEntry = DataManager.Instance.GetMonster(character.BaseForm.Species);
            PromoteBranch branch = oldEntry.Promotions[branchIndex];
            bool bypass = character.EquippedItem.ID == ExceptionItem;

            if (DataManager.Instance.CurrentReplay == null)
                yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.SetDialogue(String.Format(new StringKey("DLG_EVO_BEGIN").ToLocal())));
            character.CharDir = Dir8.Down;
            //fade
            GameManager.Instance.BattleSE("EVT_Evolution_Start");
            yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.FadeOut(true));
            string oldName = character.GetDisplayName(true);
            //evolve
            MonsterData entry = DataManager.Instance.GetMonster(branch.Result);
            MonsterID newData = character.BaseForm;
            newData.Species = branch.Result;
            if (newData.Form >= entry.Forms.Count)
                newData.Form = 0;
            character.Promote(newData);
            branch.OnPromote(character, true, bypass);
            if (bypass)
                character.DequipItem();

            int oldFullness = character.Fullness;
            character.FullRestore();
            character.Fullness = oldFullness;
            //restore HP and status problems
            //{
            //    character.HP = character.MaxHP;

            //    List<int> statuses = new List<int>();
            //    foreach (StatusEffect oldStatus in character.IterateStatusEffects())
            //        statuses.Add(oldStatus.ID);

            //    foreach (int statusID in statuses)
            //        yield return CoroutineManager.Instance.StartCoroutine(character.RemoveStatusEffect(statusID, false));
            //}

            yield return new WaitForFrames(30);
            //fade
            GameManager.Instance.BattleSE("EVT_Title_Intro");
            yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.FadeIn());
            //evolution chime
            GameManager.Instance.Fanfare("Fanfare/Promotion");
            //proclamation

            yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.LogSkippableMsg(String.Format(new StringKey("DLG_EVO_COMPLETE").ToLocal(), oldName, entry.GetColoredName())));

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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            foreach(Character player in DungeonScene.Instance.ActiveTeam.EnumerateChars())
                DataManager.Instance.Save.RestrictCharLevel(player, Level, false);
            foreach (Character player in DungeonScene.Instance.ActiveTeam.Assembly)
                DataManager.Instance.Save.RestrictCharLevel(player, Level, false);
            yield break;
        }
    }


    [Serializable]
    public class ResetFloorEvent : SingleCharEvent
    {
        public ResetFloorEvent() { }
        public override GameEvent Clone() { return new ResetFloorEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            if (character == DungeonScene.Instance.ActiveTeam.Leader)
            {
                GameManager.Instance.BattleSE("DUN_Tile_Step");
                yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.FadeOut(false));
                GameManager.Instance.SceneOutcome = resetFloor(ZoneManager.Instance.CurrentZone.CurrentMapID, new LocRay8(DungeonScene.Instance.ActiveTeam.Leader.CharLoc, DungeonScene.Instance.ActiveTeam.Leader.CharDir));
            }
            else if (character.MemberTeam == DungeonScene.Instance.ActiveTeam)
                DungeonScene.Instance.LogMsg(String.Format(new StringKey("MSG_LEADER_ONLY_TILE").ToLocal()));
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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            //a case for rescues; leader only!
            //everyone can check a tile.
            //however, only the leader of a team can choose to advance
            if (character == DungeonScene.Instance.ActiveTeam.Leader)
            {
                ZoneSegmentBase structure = ZoneManager.Instance.CurrentZone.Segments[ZoneManager.Instance.CurrentMapID.Segment];
                GameManager.Instance.BattleSE("DUN_Stairs_Down");
                yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.EndSegment(GameProgress.ResultType.Rescue));
            }
            else if (character.MemberTeam == DungeonScene.Instance.ActiveTeam)
                DungeonScene.Instance.LogMsg(String.Format(new StringKey("MSG_LEADER_ONLY_TILE").ToLocal()));
        }
    }

    [Serializable]
    public class NextFloorEvent : SingleCharEvent
    {
        public NextFloorEvent() { }
        public override GameEvent Clone() { return new NextFloorEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            if (((EffectTile)owner).TileStates.Contains<DestState>())
                yield break;

            //a case for changing floor; leader only!
            //everyone can check a tile.
            //however, only the leader of a team can choose to advance
            if (character == DungeonScene.Instance.ActiveTeam.Leader)
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
                    if (ZoneManager.Instance.CurrentMapID.ID + 1 < structure.FloorCount)
                    {
                        yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.FadeOut(false));
                        GameManager.Instance.SceneOutcome = GameManager.Instance.MoveToZone(new ZoneLoc(ZoneManager.Instance.CurrentZoneID, new SegLoc(ZoneManager.Instance.CurrentMapID.Segment, ZoneManager.Instance.CurrentMapID.ID + 1)));
                    }
                    else
                        yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.EndSegment(GameProgress.ResultType.Cleared));
                }
            }
            else if (character.MemberTeam == DungeonScene.Instance.ActiveTeam)
                DungeonScene.Instance.LogMsg(String.Format(new StringKey("MSG_LEADER_ONLY_TILE").ToLocal()));
        }
    }


    [Serializable]
    public class SwitchMapEvent : SingleCharEvent
    {
        public SwitchMapEvent() { }
        public override GameEvent Clone() { return new SwitchMapEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            DestState destState = ((EffectTile)owner).TileStates.GetWithDefault<DestState>();

            if (destState == null)
                yield break;

            if (character == DungeonScene.Instance.ActiveTeam.Leader)
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

                        if (endSegment >= 0 && endFloor >= 0 && endSegment < ZoneManager.Instance.CurrentZone.Segments.Count && endFloor < ZoneManager.Instance.CurrentZone.Segments[endSegment].FloorCount)
                            GameManager.Instance.SceneOutcome = GameManager.Instance.MoveToZone(new ZoneLoc(ZoneManager.Instance.CurrentZoneID, new SegLoc(endSegment, endFloor)));
                        else
                            yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.EndSegment(GameProgress.ResultType.Cleared));
                    }
                    else if (!destState.Dest.IsValid())
                        yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.EndSegment(GameProgress.ResultType.Cleared));
                    else//go to a designated dungeon structure
                    {
                        yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.FadeOut(false));
                        GameManager.Instance.SceneOutcome = GameManager.Instance.MoveToZone(new ZoneLoc(ZoneManager.Instance.CurrentZoneID, destState.Dest));
                    }
                }
            }
            else if (character.MemberTeam == DungeonScene.Instance.ActiveTeam)
                DungeonScene.Instance.LogMsg(String.Format(new StringKey("MSG_LEADER_ONLY_TILE").ToLocal()));
        }
    }

    [Serializable]
    public class EndGameEvent : SingleCharEvent
    {
        public EndGameEvent() { }
        public override GameEvent Clone() { return new EndGameEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            if (character.MemberTeam == DungeonScene.Instance.ActiveTeam && DataManager.Instance.CurrentReplay == null)
                yield return CoroutineManager.Instance.StartCoroutine(MenuManager.Instance.SetDialogue(String.Format(Message.ToLocal())));
        }

    }

    [Serializable]
    public class PrepareCutsceneEvent : SingleCharEvent
    {
        public PrepareCutsceneEvent() { }
        public override GameEvent Clone() { return new PrepareCutsceneEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
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

        public PrepareCameraEvent() { }
        public PrepareCameraEvent(Loc loc) { CamCenter = loc; }
        protected PrepareCameraEvent(PrepareCameraEvent other) { CamCenter = other.CamCenter; }
        public override GameEvent Clone() { return new PrepareCameraEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            ZoneManager.Instance.CurrentMap.ViewCenter = CamCenter;
            yield break;
        }
    }

    [Serializable]
    public class BattlePositionEvent : SingleCharEvent
    {
        public Loc[] Positions;

        public BattlePositionEvent() { Positions = new Loc[0]; }
        public BattlePositionEvent(params Loc[] positions) { Positions = positions; }
        public BattlePositionEvent(BattlePositionEvent other)
        {
            Positions = new Loc[other.Positions.Length];
            other.Positions.CopyTo(Positions, 0);
        }
        public override GameEvent Clone() { return new BattlePositionEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
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
            yield break;
        }
        
        public void MoveChar(Character character, int total_alive)
        {
            if (total_alive < Positions.Length)
                character.CharLoc = ZoneManager.Instance.CurrentMap.EntryPoints[0].Loc + Positions[total_alive];
            else //default to close to leader
            {
                Loc? result = ZoneManager.Instance.CurrentMap.GetClosestTileForChar(character, ZoneManager.Instance.CurrentMap.EntryPoints[0].Loc);
                if (result.HasValue)
                    character.CharLoc = result.Value;
                else
                    character.CharLoc = ZoneManager.Instance.CurrentMap.EntryPoints[0].Loc;
            }
            character.CharDir = ZoneManager.Instance.CurrentMap.EntryPoints[0].Dir;
        }
    }

    [Serializable]
    public class FadeInEvent : SingleCharEvent
    {
        public FadeInEvent() { }
        public override GameEvent Clone() { return new FadeInEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            if (character != null)
                yield break;
            yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.FadeIn());
        }
    }

    [Serializable]
    public class SpecialIntroEvent : SingleCharEvent
    {
        public SpecialIntroEvent() { }
        public override GameEvent Clone() { return new SpecialIntroEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            if (character != null)
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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            if (character != null)
                yield break;

            foreach (InvItem item in DungeonScene.Instance.ActiveTeam.EnumerateInv())
            {
                ItemData entry = DataManager.Instance.GetItem(item.ID);
                if (entry.MaxStack < 0 && entry.UsageType != ItemData.UseType.Box)
                    item.HiddenValue = 0;
            }
        }
    }

    [Serializable]
    public class BeginBattleEvent : SingleCharEvent
    {
        [DataType(0, DataManager.DataType.MapStatus, false)]
        public int CheckClearStatus;

        public BeginBattleEvent() { }
        public BeginBattleEvent(int checkClear) { CheckClearStatus = checkClear; }
        public BeginBattleEvent(BeginBattleEvent other)
        {
            CheckClearStatus = other.CheckClearStatus;
        }
        public override GameEvent Clone() { return new BeginBattleEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
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
            List<int> statusToRemove = new List<int>();
            foreach (int status in ZoneManager.Instance.CurrentMap.Status.Keys)
                statusToRemove.Add(status);
            foreach (int status in statusToRemove)
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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            Loc destTile = character.CharLoc + character.CharDir.GetLoc();
            if (!ZoneManager.Instance.CurrentMap.GetLocInMapBounds(ref destTile))
                yield break;

            if (character.MemberTeam is ExplorerTeam)
            {
                Tile tile = ZoneManager.Instance.CurrentMap.Tiles[destTile.X][destTile.Y];
                if (!String.IsNullOrEmpty(tile.Effect.ID) && !tile.Effect.Revealed)
                {
                    tile.Effect.Revealed = true;

                    if (!character.Unidentifiable)
                    {
                        SingleEmitter emitter = new SingleEmitter(new AnimData("Emote_Exclaim", 1));
                        emitter.LocHeight = 24;
                        emitter.SetupEmit(character.MapLoc + character.CharDir.GetLoc() * GraphicsManager.TileSize / 2, character.MapLoc + character.CharDir.GetLoc() * GraphicsManager.TileSize / 2, character.CharDir);
                        DungeonScene.Instance.CreateAnim(emitter, DrawLayer.NoDraw);
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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            Tile tile = ZoneManager.Instance.CurrentMap.Tiles[character.CharLoc.X][character.CharLoc.Y];
            if (!String.IsNullOrEmpty(tile.Effect.ID))
            {
                DungeonScene.Instance.QueueTrap(character.CharLoc);
                //yield return CoroutineManager.Instance.StartCoroutine(tile.Effect.InteractWithTile(character));
            }
            yield break;
        }
    }

    [Serializable]
    public class InvokeTrapEvent : SingleCharEvent
    {
        public CombatAction HitboxAction;
        public ExplosionData Explosion;
        public BattleData NewData;
        public bool OneTime;

        public InvokeTrapEvent() { }
        public InvokeTrapEvent(CombatAction action, ExplosionData explosion, BattleData moveData, bool oneTime)
        {
            HitboxAction = action;
            Explosion = explosion;
            NewData = moveData;
            OneTime = oneTime;
        }
        protected InvokeTrapEvent(InvokeTrapEvent other)
        {
            HitboxAction = other.HitboxAction;
            Explosion = other.Explosion;
            NewData = new BattleData(other.NewData);
            OneTime = other.OneTime;
        }
        public override GameEvent Clone() { return new InvokeTrapEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            EffectTile effectTile = (EffectTile)owner;

            //don't activate on an ally
            if (ZoneManager.Instance.CurrentMap.GetTileOwner(character) == effectTile.Owner)
                yield break;

            if (character.CharStates.Contains<TrapState>())
                yield break;

            //don't activate if already triggering
            if (effectTile.TileStates.Contains<TriggeringState>())
                yield break;

            effectTile.TileStates.Set(new TriggeringState());

            CharAnimation standAnim = new CharAnimIdle(character.CharLoc, character.CharDir);
            standAnim.MajorAnim = true;
            yield return CoroutineManager.Instance.StartCoroutine(character.StartAnim(standAnim));

            GameManager.Instance.BattleSE("DUN_Tile_Step");
            effectTile.Revealed = true;


            BattleContext newContext = new BattleContext(BattleActionType.Trap);
            newContext.User = character;
            newContext.UsageSlot = BattleContext.FORCED_SLOT;

            newContext.StartDir = newContext.User.CharDir;

            //change move effects
            newContext.Data = new BattleData(NewData);
            newContext.Data.ID = owner.GetID();

            newContext.Explosion = new ExplosionData(Explosion);
            newContext.HitboxAction = HitboxAction.Clone();
            //recenter the attack on the tile
            newContext.HitboxAction.HitOffset = effectTile.TileLoc - character.CharLoc;
            newContext.Strikes = 1;
            newContext.Item = new InvItem();

            TileData entry = DataManager.Instance.GetTile(owner.GetID());
            newContext.SetActionMsg(String.Format(new StringKey("MSG_TILE_CHECK").ToLocal(), newContext.User.GetDisplayName(false), entry.Name.ToLocal()));

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
    public class OpenVaultEvent : SingleCharEvent
    {
        public List<Loc> OpenLocs;
        public OpenVaultEvent() { OpenLocs = new List<Loc>(); }
        public OpenVaultEvent(List<Loc> locs) { OpenLocs = locs; }
        public OpenVaultEvent(OpenVaultEvent other) { OpenLocs = other.OpenLocs; }
        public override GameEvent Clone() { return new OpenVaultEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            //invoke the unlock sound and animation
            GameManager.Instance.BattleSE("DUN_Open_Chamber");

            foreach (Loc loc in OpenLocs)
            {
                //remove all specified tiles; both the effect and the terrain, if there is one
                Tile tile = ZoneManager.Instance.CurrentMap.Tiles[loc.X][loc.Y];
                tile.Effect = new EffectTile(tile.Effect.TileLoc);
                SingleEmitter emitter = new SingleEmitter(new AnimData("Vault_Key_Open", 3));
                emitter.SetupEmit(loc * GraphicsManager.TileSize, loc * GraphicsManager.TileSize, Dir8.Down);
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
        public OpenSelfEvent() { }
        public override GameEvent Clone() { return new OpenSelfEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            EffectTile effectTile = (EffectTile)owner;

            CharAnimation standAnim = new CharAnimIdle(character.CharLoc, character.CharDir);
            standAnim.MajorAnim = true;
            yield return CoroutineManager.Instance.StartCoroutine(character.StartAnim(standAnim));

            //invoke the unlock sound and animation
            GameManager.Instance.BattleSE("DUN_Open_Chamber");

            //remove all specified tiles; both the effect and the terrain, if there is one
            Loc baseLoc = effectTile.TileLoc;
            Tile tile = ZoneManager.Instance.CurrentMap.Tiles[baseLoc.X][baseLoc.Y];
            if (tile.Effect == owner)
                tile.Effect = new EffectTile(tile.Effect.TileLoc);

            SingleEmitter emitter = new SingleEmitter(new AnimData("Vault_Key_Open", 3));
            emitter.SetupEmit(baseLoc * GraphicsManager.TileSize, baseLoc * GraphicsManager.TileSize, Dir8.Down);
            DungeonScene.Instance.CreateAnim(emitter, DrawLayer.NoDraw);
            yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(30));
            {
                tile.Data = new TerrainTile(DataManager.Instance.GenFloor);
                int distance = 0;
                Loc startLoc = baseLoc - new Loc(distance + 2);
                Loc sizeLoc = new Loc((distance + 2) * 2 + 1);
                ZoneManager.Instance.CurrentMap.MapModified(startLoc, sizeLoc);
            }


            yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(60));

            //say, "The vault doors opened!"/with fanfare
            DungeonScene.Instance.LogMsg(String.Format(new StringKey("MSG_LOCK_OPEN").ToLocal()));
            GameManager.Instance.Fanfare("Fanfare/Treasure");

            yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(90));
        }

    }

    [Serializable]
    public class OpenOtherPassageEvent : SingleCharEvent
    {
        public int TimeLimitStatus;
        public FiniteEmitter Emitter;
        public string WarningBGM;
        public StringKey Warning;
        public string WarningSE;

        public OpenOtherPassageEvent()
        {
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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            EffectTile effectTile = (EffectTile)owner;
            //unlock the other doors
            //play the sound
            TileListState tilesState = ((EffectTile)owner).TileStates.GetWithDefault<TileListState>();
            if (tilesState != null)
            {
                List<Loc> locs = tilesState.Tiles;
                if (locs.Count > 0)
                {
                    GameManager.Instance.BattleSE("DUN_Open_Chamber");
                    //remove the tile, and create vfx for each one
                    foreach (Loc loc in locs)
                    {
                        Tile exTile = ZoneManager.Instance.CurrentMap.Tiles[loc.X][loc.Y];
                        exTile.Effect = new EffectTile(exTile.Effect.TileLoc);

                        SingleEmitter altEmitter = new SingleEmitter(new AnimData("Vault_Open", 3));
                        altEmitter.SetupEmit(loc * GraphicsManager.TileSize, loc * GraphicsManager.TileSize, Dir8.Down);
                        DungeonScene.Instance.CreateAnim(altEmitter, DrawLayer.NoDraw);
                    }
                    yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(30));

                    foreach (Loc loc in locs)
                    {
                        Tile exTile = ZoneManager.Instance.CurrentMap.Tiles[loc.X][loc.Y];
                        {
                            exTile.Data = new TerrainTile(DataManager.Instance.GenFloor);
                            int distance = 0;
                            Loc startLoc = exTile.Effect.TileLoc - new Loc(distance + 2);
                            Loc sizeLoc = new Loc((distance + 2) * 2 + 1);
                            ZoneManager.Instance.CurrentMap.MapModified(startLoc, sizeLoc);
                        }

                    }

                    //mention the other doors
                    DungeonScene.Instance.LogMsg(String.Format(new StringKey("MSG_LOCK_OPEN_FLOOR").ToLocal()));
                }
            }


            if (((EffectTile)owner).Danger)
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



                    DungeonScene.Instance.LogMsg(String.Format(Warning.ToLocal()));

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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
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
        public bool OneTime;

        public TriggerSwitchEvent() { }
        public TriggerSwitchEvent(bool oneTime)
        {
            OneTime = oneTime;
        }
        protected TriggerSwitchEvent(TriggerSwitchEvent other)
        {
            OneTime = other.OneTime;
        }
        public override GameEvent Clone() { return new TriggerSwitchEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            EffectTile effectTile = (EffectTile)owner;

            if (character.CharStates.Contains<TrapState>())
                yield break;

            CharAnimation standAnim = new CharAnimIdle(character.CharLoc, character.CharDir);
            standAnim.MajorAnim = true;
            yield return CoroutineManager.Instance.StartCoroutine(character.StartAnim(standAnim));

            GameManager.Instance.BattleSE("DUN_Tile_Step");

            if (OneTime)
            {
                Loc baseLoc = effectTile.TileLoc;
                Tile tile = ZoneManager.Instance.CurrentMap.Tiles[baseLoc.X][baseLoc.Y];
                if (tile.Effect == owner)
                    tile.Effect = new EffectTile(tile.Effect.TileLoc);
            }
            yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(30));
        }


    }

    [Serializable]
    public class ChestEvent : SingleCharEvent
    {
        public ChestEvent() { }
        public override GameEvent Clone() { return new ChestEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            //TODO: remove hardcoded everything in this block...
            EffectTile effectTile = (EffectTile)owner;

            CharAnimation standAnim = new CharAnimIdle(character.CharLoc, character.CharDir);
            standAnim.MajorAnim = true;
            yield return CoroutineManager.Instance.StartCoroutine(character.StartAnim(standAnim));

            //open chest animation/sound
            Loc baseLoc = effectTile.TileLoc;
            SingleEmitter emitter = new SingleEmitter(new AnimData("Chest_Open", 8));
            emitter.SetupEmit(baseLoc * GraphicsManager.TileSize, baseLoc * GraphicsManager.TileSize, Dir8.Down);
            DungeonScene.Instance.CreateAnim(emitter, DrawLayer.NoDraw);

            yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(10));
            GameManager.Instance.BattleSE("EVT_Fade_White");

            //fade to white
            yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.FadeOut(true));

            //change the chest to open
            Tile tile = ZoneManager.Instance.CurrentMap.Tiles[baseLoc.X][baseLoc.Y];
            if (tile.Effect == owner)
                tile.Effect = new EffectTile("chest_empty", true, tile.Effect.TileLoc);// magic number

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
                item.TileLoc = freeTiles[randIndex];
                spawnItems.Add(item);
                freeTiles.RemoveAt(randIndex);
                //start the animations

                ItemAnim itemAnim = new ItemAnim(baseLoc * GraphicsManager.TileSize, item.MapLoc, item.IsMoney ? GraphicsManager.MoneySprite : DataManager.Instance.GetItem(item.Value).Sprite, GraphicsManager.TileSize / 2, Math.Max(0, waitTime));
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
            DungeonScene.Instance.LogMsg(String.Format(new StringKey("MSG_TREASURE").ToLocal()));
            yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(60));


            if (((EffectTile)owner).Danger)
            {
                LockdownTileEvent lockdown = new LockdownTileEvent(34);// magic number
                MonsterHouseTileEvent monsterHouse = new MonsterHouseTileEvent();
                yield return CoroutineManager.Instance.StartCoroutine(lockdown.Apply(owner, ownerChar, character));
                yield return CoroutineManager.Instance.StartCoroutine(monsterHouse.Apply(owner, ownerChar, character));
            }
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
        [DataType(0, DataManager.DataType.MapStatus, false)]
        public int CheckClearStatus;

        public LockdownEvent() { }
        public LockdownEvent(int checkClearStatus) { CheckClearStatus = checkClearStatus; }
        public LockdownEvent(LockdownEvent other) { CheckClearStatus = other.CheckClearStatus; }

        protected abstract Rect GetBounds(GameEventOwner owner, Character ownerChar, Character character);

        protected abstract List<SingleCharEvent> GetResultEvents(GameEventOwner owner, Character ownerChar, Character character);


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {

            Rect bounds = GetBounds(owner, ownerChar, character);

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

            List<SingleCharEvent> resultEvents = GetResultEvents(owner, ownerChar, character);
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
            character.StartEmote(new Emote(emoteData.Anim, emoteData.LocHeight, 1));
            Loc? insideLoc = null;
            foreach (Character target in ZoneManager.Instance.CurrentMap.ActiveTeam.IterateByRank())
            {
                if (!target.Dead)
                {
                    if (DataManager.Instance.Save.Rand.Next(2) == 0)
                        character.StartEmote(new Emote(altEmoteData.Anim, altEmoteData.LocHeight, 1));

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

                //if a character is on those tiles, shove them off
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
        public LockdownTileEvent(int checkClear) : base(checkClear) { }
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
        public LockdownMapEvent(int checkClear) : base(checkClear) { }
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
        protected abstract Rect GetBounds(GameEventOwner owner, Character ownerChar, Character character);
        protected abstract List<MobSpawn> GetMonsters(GameEventOwner owner, Character ownerChar, Character character);
        protected abstract bool NeedTurnEnd { get; }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {

            yield return new WaitUntil(DungeonScene.Instance.AnimationsOver);

            Rect bounds = GetBounds(owner, ownerChar, character);

            //it's a monster house!
            DungeonScene.Instance.LogMsg(String.Format(new StringKey("MSG_MONSTER_HOUSE").ToLocal()));
            //kick up the music.
            GameManager.Instance.BGM("", false);
            GameManager.Instance.BGM(GraphicsManager.MonsterBGM, false);

            //spawn all contents with the landing animation
            //spawn list is specified by the state tags.  same as items
            List<MobSpawn> mobs = GetMonsters(owner, ownerChar, character);
            //find the open tiles to spawn in
            List<Loc> freeTiles = Grid.FindTilesInBox(bounds.Start + new Loc(1), bounds.Size - new Loc(2),
                (Loc testLoc) =>
                {
                    if (ZoneManager.Instance.CurrentMap.TileBlocked(testLoc))
                        return false;

                    foreach (Character testChar in ZoneManager.Instance.CurrentMap.IterateCharacters())
                    {
                        if (!testChar.Dead && testChar.CharLoc == testLoc)
                            return false;
                    }

                    return true;
                });
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
                ZoneManager.Instance.CurrentMap.CurrentTurnMap.SkipRemainingTurns();
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


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
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
                        emitter.SetupEmit(animLoc * GraphicsManager.TileSize, animLoc * GraphicsManager.TileSize, Dir8.Down);
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
                DungeonScene.Instance.LogMsg(String.Format(new StringKey("MSG_MONSTER_HOUSE").ToLocal()));
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


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
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

                    List<int> statuses = new List<int>();
                    foreach (StatusEffect oldStatus in target.IterateStatusEffects())
                        statuses.Add(oldStatus.ID);

                    foreach (int statusID in statuses)
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
            if (song.Song != null)
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
            ZoneManager.Instance.CurrentMap.CurrentTurnMap.SkipRemainingTurns();
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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
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
                            yield return CoroutineManager.Instance.StartCoroutine(Effects[ii].Apply(owner, ownerChar, character));

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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
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
                        yield return CoroutineManager.Instance.StartCoroutine(Effects[ii].Apply(owner, ownerChar, character));

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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            //for counting the enemies and allies in the box each time, with a tab kept on what tiles to unlock when finished

            bool noPlayers = true;
            foreach (Character player in ZoneManager.Instance.CurrentMap.ActiveTeam.Players)
            {
                if (!player.Dead && ZoneManager.Instance.CurrentMap.InBounds(Bounds, player.CharLoc))
                {
                    noPlayers = false;
                    break;
                }
            }
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
            if (noPlayers || noFoes)
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

                //if all enemies are cleared, also remove the barriers with a "thud".
                if (noFoes)
                {
                    yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(80));
                    GameManager.Instance.BattleSE("DUN_Eruption");
                    DungeonScene.Instance.SetScreenShake(new ScreenMover(3, 6, 10));
                    unlockTile();
                    yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(30));
                    foreach (SingleCharEvent result in ResultEvents)
                        yield return CoroutineManager.Instance.StartCoroutine(result.Apply(owner, ownerChar, character));
                }
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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            MapCheckState checks = ((MapStatus)owner).StatusStates.GetWithDefault<MapCheckState>();
            for (int ii = checks.CheckEvents.Count - 1; ii >= 0; ii--)
            {
                SingleCharEvent effect = checks.CheckEvents[ii];
                yield return CoroutineManager.Instance.StartCoroutine(effect.Apply(owner, ownerChar, character));
            }
        }
    }


    [Serializable]
    public class PeriodicSpawnEntranceGuards : SingleCharEvent
    {
        public int Period;
        public int Maximum;
        public int GuardStatus;

        public PeriodicSpawnEntranceGuards() { }
        public PeriodicSpawnEntranceGuards(int period, int maximum, int guardStatus) { Period = period; Maximum = maximum; GuardStatus = guardStatus; }
        public PeriodicSpawnEntranceGuards(PeriodicSpawnEntranceGuards other) { this.Period = other.Period; Maximum = other.Maximum; GuardStatus = other.GuardStatus; }
        public override GameEvent Clone() { return new PeriodicSpawnEntranceGuards(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            if (character != null)
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


        public static IEnumerator<YieldInstruction> PlaceGuard(MobSpawn spawn, Loc dest, int guardStatusId)
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


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            if (character != null)
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


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            Loc baseLoc = DungeonScene.Instance.ActiveTeam.Leader.CharLoc;
            Tile tile = ZoneManager.Instance.CurrentMap.Tiles[baseLoc.X][baseLoc.Y];

            if (tile.Effect.ID != ShopTile)
            {
                GameManager.Instance.BGM(ZoneManager.Instance.CurrentMap.Music, true);//TODO: String Assets owner.GetID()
                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.RemoveMapStatus(((MapStatus)owner).ID));
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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            if (character == null)
                yield return CoroutineManager.Instance.StartCoroutine(BaseEvent.Apply(owner, ownerChar, character));
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

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            if (character == DungeonScene.Instance.ActiveTeam.Leader)
                yield return CoroutineManager.Instance.StartCoroutine(BaseEvent.Apply(owner, ownerChar, character));
        }
    }


    [Serializable]
    public abstract class ShareEquipEvent : SingleCharEvent
    {
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, Character character)
        {
            if (ownerChar.EquippedItem.ID > -1)
            {
                ItemData entry = (ItemData)ownerChar.EquippedItem.GetData();
                if (CheckEquipPassValidityEvent.CanItemEffectBePassed(entry))
                {
                    foreach (var effect in GetEvents(entry))
                        yield return CoroutineManager.Instance.StartCoroutine(effect.Value.Apply(owner, ownerChar, character));
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
