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
    // Battle events that replace the effect of the battledata with different or new data

    /// <summary>
    /// Event that uses a different battle action if the character is a certain type.
    /// </summary>
    [Serializable]
    public class ElementDifferentUseEvent : BattleEvent
    {
        /// <summary>
        /// The type in order for this battle action to activate
        /// </summary>
        [JsonConverter(typeof(ElementConverter))]
        [DataType(0, DataManager.DataType.Element, false)]
        public string Element;
        //also need to somehow specify alternative animations/sounds
        /// <summary>
        /// Data on the hitbox of the attack. Controls range and targeting
        /// </summary>
        public CombatAction HitboxAction;

        /// <summary>
        /// Optional data to specify a splash effect on the tiles hit
        /// </summary>
        public ExplosionData Explosion;

        /// <summary>
        /// Events that occur with this skill
        /// Before it's used, when it hits, after it's used, etc
        /// </summary>
        public BattleData NewData;

        public ElementDifferentUseEvent() { Element = ""; }
        public ElementDifferentUseEvent(string element, CombatAction action, ExplosionData explosion, BattleData moveData)
        {
            Element = element;
            HitboxAction = action;
            Explosion = explosion;
            NewData = moveData;
        }
        protected ElementDifferentUseEvent(ElementDifferentUseEvent other)
            : this()
        {
            Element = other.Element;
            HitboxAction = other.HitboxAction;
            Explosion = other.Explosion;
            NewData = new BattleData(other.NewData);
        }
        public override GameEvent Clone() { return new ElementDifferentUseEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            //different effects for element
            if (context.User.HasElement(Element))
            {
                //change hitboxaction
                context.HitboxAction = HitboxAction.Clone();

                //change explosion
                context.Explosion = new ExplosionData(Explosion);

                //change move effects
                string id = context.Data.ID;
                DataManager.DataType dataType = context.Data.DataType;
                context.Data = new BattleData(NewData);
                context.Data.ID = id;
                context.Data.DataType = dataType;
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that uses a different battle data if the target is an ally
    /// </summary>
    [Serializable]
    public class AlignmentDifferentEvent : BattleEvent
    {
        public Alignment Alignments;
        /// <summary>
        /// Events that occur with this skill
        /// Before it's used, when it hits, after it's used, etc
        /// </summary>
        public BattleData NewData;

        public AlignmentDifferentEvent() { }
        public AlignmentDifferentEvent(Alignment alignments, BattleData moveData)
        {
            Alignments = alignments;
            NewData = moveData;
        }
        protected AlignmentDifferentEvent(AlignmentDifferentEvent other)
        {
            Alignments = other.Alignments;
            NewData = new BattleData(other.NewData);
        }
        public override GameEvent Clone() { return new AlignmentDifferentEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            //different effects for allies
            if ((DungeonScene.Instance.GetMatchup(context.User, context.Target) & Alignments) != Alignment.None)
            {
                string id = context.Data.ID;
                DataManager.DataType dataType = context.Data.DataType;
                context.Data = new BattleData(NewData);
                context.Data.ID = id;
                context.Data.DataType = dataType;
            }
            yield break;
        }

        [OnDeserialized]
        internal void OnDeserializedMethod(StreamingContext context)
        {
            //TODO: remove on v1.1
            if (Serializer.OldVersion < new Version(0, 7, 21) && Alignments == Alignment.None)
            {
                Alignments = Alignment.Self | Alignment.Friend;
            }
        }
    }


    /// <summary>
    /// Event that checks whether an item can be caught and changes the battle data if so
    /// </summary>
    [Serializable]
    public class CatchableEvent : BattleEvent
    {
        /// <summary>
        /// Events that occur when the item is caught
        /// Before it's used, when it hits, after it's used, etc
        /// </summary>
        public BattleData NewData;

        public CatchableEvent() { }
        public CatchableEvent(BattleData moveData)
        {
            NewData = moveData;
        }
        protected CatchableEvent(CatchableEvent other)
        {
            NewData = new BattleData(other.NewData);
        }
        public override GameEvent Clone() { return new CatchableEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            //can't catch pierce
            if (context.HitboxAction is LinearAction && !((LinearAction)context.HitboxAction).StopAtHit)
                yield break;

            //can't catch when holding
            if (!String.IsNullOrEmpty(context.Target.EquippedItem.ID))
                yield break;

            //can't catch when inv full
            if (context.Target.MemberTeam is ExplorerTeam && ((ExplorerTeam)context.Target.MemberTeam).GetInvCount() >= ((ExplorerTeam)context.Target.MemberTeam).GetMaxInvSlots(ZoneManager.Instance.CurrentZone))
                yield break;

            if (context.Target.MemberTeam is MonsterTeam)
            {
                //can't catch if it's a wild team, and it's a use-item
                ItemData entry = DataManager.Instance.GetItem(context.Item.ID);
                //can't catch if it's a wild team, and it's an edible or ammo
                if (entry.ItemStates.Contains<EdibleState>() || entry.ItemStates.Contains<AmmoState>())
                    yield break;
            }

            context.ContextStates.Set(new ItemCaught());

            string id = context.Data.ID;
            DataManager.DataType dataType = context.Data.DataType;
            context.Data = new BattleData(NewData);
            context.Data.ID = id;
            context.Data.DataType = dataType;
        }
    }

    /// <summary>
    /// Event that changes the hitbox action
    /// </summary>
    [Serializable]
    public class ChangeActionEvent : BattleEvent
    {
        /// <summary>
        /// Data on the hitbox of the attack. Controls range and targeting
        /// </summary>
        public CombatAction NewAction;

        public ChangeActionEvent() { }
        public ChangeActionEvent(CombatAction newAction)
        {
            NewAction = newAction;
        }
        protected ChangeActionEvent(ChangeActionEvent other)
            : this()
        {
            NewAction = other.NewAction.Clone();
        }
        public override GameEvent Clone() { return new ChangeActionEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            //change hitboxaction
            context.HitboxAction = NewAction.Clone();
            yield break;
        }
    }

    /// <summary>
    /// Event that changes the battle data
    /// </summary>
    [Serializable]
    public class ChangeDataEvent : BattleEvent
    {
        /// <summary>
        /// Events that occur with this skill
        /// Before it's used, when it hits, after it's used, etc
        /// </summary>
        public BattleData NewAction;

        public ChangeDataEvent() { }
        public ChangeDataEvent(BattleData newAction)
        {
            NewAction = newAction;
        }
        protected ChangeDataEvent(ChangeDataEvent other)
            : this()
        {
            NewAction = new BattleData(other.NewAction);
        }
        public override GameEvent Clone() { return new ChangeDataEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            //change data
            context.Data = new BattleData(NewAction);
            yield break;
        }
    }

    /// <summary>
    /// Event that changes the explosion data
    /// </summary>
    [Serializable]
    public class ChangeExplosionEvent : BattleEvent
    {
        /// <summary>
        /// Optional data to specify a splash effect on the tiles hit
        /// </summary>
        public ExplosionData NewAction;

        public ChangeExplosionEvent() { }
        public ChangeExplosionEvent(ExplosionData newAction)
        {
            NewAction = newAction;
        }
        protected ChangeExplosionEvent(ChangeExplosionEvent other)
            : this()
        {
            NewAction = new ExplosionData(other.NewAction);
        }
        public override GameEvent Clone() { return new ChangeExplosionEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            //change data
            context.Explosion = new ExplosionData(NewAction);
            yield break;
        }
    }



    /// <summary>
    /// Event that passes the affect of berries to nearby allies.
    /// </summary>
    [Serializable]
    public class BerryAoEEvent : BattleEvent
    {

        /// <summary>
        /// The message displayed in the dungeon log  
        /// </summary>
        public StringKey Msg;

        /// <summary>
        /// The particle VFX
        /// </summary>
        public FiniteEmitter Emitter;

        /// <summary>
        /// The sound effect of the VFX
        /// </summary>
        [Sound(0)]
        public string Sound;


        public BerryAoEEvent() { Emitter = new EmptyFiniteEmitter(); }
        public BerryAoEEvent(StringKey msg, FiniteEmitter emitter, string sound)
            : this()
        {
            Msg = msg;
            Emitter = emitter;
            Sound = sound;
        }
        protected BerryAoEEvent(BerryAoEEvent other)
            : this()
        {
            Emitter = (FiniteEmitter)other.Emitter.Clone();
            Sound = other.Sound;
        }
        public override GameEvent Clone() { return new BerryAoEEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Item)
            {
                ItemData itemData = DataManager.Instance.GetItem(context.Item.ID);
                if (itemData.ItemStates.Contains<BerryState>())
                {
                    AreaAction newAction = new AreaAction();
                    newAction.TargetAlignments = (Alignment.Self | Alignment.Friend);
                    newAction.Range = 1;
                    newAction.ActionFX.Emitter = Emitter;
                    newAction.Speed = 10;
                    newAction.ActionFX.Sound = Sound;
                    newAction.ActionFX.Delay = 30;
                    context.HitboxAction = newAction;
                    context.Explosion.TargetAlignments = (Alignment.Self | Alignment.Friend);

                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(Msg.ToLocal(), ownerChar.GetDisplayName(false), owner.GetDisplayName()));
                }
            }
            yield break;
        }
    }


    /// <summary>
    /// Event that uses different skill data depending on the stack number of the status
    /// </summary>
    [Serializable]
    public class StatusStackDifferentEvent : BattleEvent
    {
        /// <summary>
        /// The status condition to track
        /// </summary>
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string StatusID;

        /// <summary>
        /// The message displayed in the dungeon log if the character doesn't have this status or the stack amount does not map to a skill data
        /// </summary>
        public StringKey FailMsg;

        /// <summary>
        /// The stack amount mapped to a skill data
        /// </summary>
        public Dictionary<int, Tuple<CombatAction, ExplosionData, BattleData>> StackPair;

        public StatusStackDifferentEvent() { StackPair = new Dictionary<int, Tuple<CombatAction, ExplosionData, BattleData>>(); StatusID = ""; }
        public StatusStackDifferentEvent(string statusID, StringKey failMsg, Dictionary<int, Tuple<CombatAction, ExplosionData, BattleData>> stack)
        {
            StatusID = statusID;
            FailMsg = failMsg;
            StackPair = stack;
        }
        protected StatusStackDifferentEvent(StatusStackDifferentEvent other)
            : this()
        {
            StatusID = other.StatusID;
            FailMsg = other.FailMsg;
            foreach (int stack in other.StackPair.Keys)
                StackPair.Add(stack, new Tuple<CombatAction, ExplosionData, BattleData>(other.StackPair[stack].Item1.Clone(), new ExplosionData(other.StackPair[stack].Item2), new BattleData(other.StackPair[stack].Item3)));
        }
        public override GameEvent Clone() { return new StatusStackDifferentEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            StatusEffect status = context.User.GetStatusEffect(StatusID);
            if (status == null)
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(FailMsg.ToLocal(), context.User.GetDisplayName(false)));
                yield break;
            }

            StackState stack = status.StatusStates.GetWithDefault<StackState>();
            if (StackPair.ContainsKey(stack.Stack))
            {
                //change hitboxaction
                context.HitboxAction = StackPair[stack.Stack].Item1.Clone();

                //change explosion
                context.Explosion = new ExplosionData(StackPair[stack.Stack].Item2);

                //change move effects
                string id = context.Data.ID;
                DataManager.DataType dataType = context.Data.DataType;
                context.Data = new BattleData(StackPair[stack.Stack].Item3);
                context.Data.ID = id;
                context.Data.DataType = dataType;
            }
            else
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(FailMsg.ToLocal(), context.User.GetDisplayName(false)));
        }
    }


    /// <summary>
    /// Event that uses different battle data depending on map status
    /// </summary>
    [Serializable]
    public class WeatherDifferentEvent : BattleEvent
    {
        /// <summary>
        /// The map status ID mapped to a battle data
        /// </summary>
        [JsonConverter(typeof(MapStatusBattleDataDictConverter))]
        public Dictionary<string, BattleData> WeatherPair;

        public WeatherDifferentEvent() { WeatherPair = new Dictionary<string, BattleData>(); }
        public WeatherDifferentEvent(Dictionary<string, BattleData> weather)
        {
            WeatherPair = weather;
        }
        protected WeatherDifferentEvent(WeatherDifferentEvent other)
            : this()
        {
            foreach (string weather in other.WeatherPair.Keys)
                WeatherPair.Add(weather, new BattleData(other.WeatherPair[weather]));
        }
        public override GameEvent Clone() { return new WeatherDifferentEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            foreach (string weather in WeatherPair.Keys)
            {
                if (ZoneManager.Instance.CurrentMap.Status.ContainsKey(weather))
                {
                    string id = context.Data.ID;
                    DataManager.DataType dataType = context.Data.DataType;
                    context.Data = new BattleData(WeatherPair[weather]);
                    context.Data.ID = id;
                    context.Data.DataType = dataType;
                    break;
                }
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that activates if the character is hit by a super-effective move
    /// </summary>
    [Serializable]
    public class AbsorbWeaknessEvent : BattleEvent
    {
        /// <summary>
        /// The list of battle events applied if the condition is met
        /// </summary>
        public List<BattleEvent> BaseEvents;

        /// <summary>
        /// The particle VFX that plays if the condition is met
        /// </summary>
        public FiniteEmitter Emitter;

        /// <summary>
        /// The sound effect that plays if the condition is met
        /// </summary>
        [Sound(0)]
        public string Sound;

        public AbsorbWeaknessEvent() { BaseEvents = new List<BattleEvent>(); Emitter = new EmptyFiniteEmitter(); }
        public AbsorbWeaknessEvent(FiniteEmitter emitter, string sound, params BattleEvent[] effects)
            : this()
        {
            foreach (BattleEvent battleEffect in effects)
                BaseEvents.Add(battleEffect);
            Emitter = emitter;
            Sound = sound;
        }
        protected AbsorbWeaknessEvent(AbsorbWeaknessEvent other)
            : this()
        {
            foreach (BattleEvent battleEffect in other.BaseEvents)
                BaseEvents.Add((BattleEvent)battleEffect.Clone());
            Emitter = (FiniteEmitter)other.Emitter.Clone();
            Sound = other.Sound;
        }
        public override GameEvent Clone() { return new AbsorbWeaknessEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int typeMatchup = PreTypeEvent.GetDualEffectiveness(context.User, context.Target, context.Data);
            typeMatchup -= PreTypeEvent.NRM_2;
            if (typeMatchup > 0 && context.User != context.Target)
            {
                string id = context.Data.ID;
                DataManager.DataType dataType = context.Data.DataType;
                BattleData newData = new BattleData();
                newData.Element = context.Data.Element;
                newData.Category = context.Data.Category;
                newData.HitRate = context.Data.HitRate;
                foreach (SkillState state in context.Data.SkillStates)
                    newData.SkillStates.Set(state.Clone<SkillState>());
                //add the absorption effects
                //newData.OnHits.Add(new BattleLogBattleEvent(new StringKey(new StringKey("MSG_ABSORB").ToLocal()), false, true));
                newData.OnHits.Add(0, new BattleAnimEvent((FiniteEmitter)Emitter.Clone(), Sound, true, 10));
                foreach (BattleEvent battleEffect in BaseEvents)
                    newData.OnHits.Add(0, (BattleEvent)battleEffect.Clone());

                foreach (BattleFX fx in context.Data.IntroFX)
                    newData.IntroFX.Add(new BattleFX(fx));
                newData.HitFX = new BattleFX(context.Data.HitFX);
                context.Data = newData;
                context.Data.ID = id;
                context.Data.DataType = dataType;
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that activates if the character is hit by the chosen type
    /// </summary>
    [Serializable]
    public class AbsorbElementEvent : BattleEvent
    {
        /// <summary>
        /// The type to absorb
        /// </summary>
        [JsonConverter(typeof(ElementConverter))]
        [DataType(0, DataManager.DataType.Element, false)]
        public string AbsorbElement;


        /// <summary>
        /// Whether or not multiple 
        /// </summary>
        public bool SingleDraw;

        /// <summary>
        /// Whether to display the message if absorbed
        /// </summary>
        public bool GiveMsg;

        /// <summary>
        /// Battle events that occur if hit by the certain type
        /// </summary>
        public List<BattleEvent> BaseEvents;

        /// <summary>
        /// The particle VFX
        /// </summary>
        public FiniteEmitter Emitter;

        /// <summary>
        /// The sound effect that plays if hit by a super-effective move
        /// </summary>
        [Sound(0)]
        public string Sound;

        public AbsorbElementEvent() { BaseEvents = new List<BattleEvent>(); Emitter = new EmptyFiniteEmitter(); AbsorbElement = ""; }
        public AbsorbElementEvent(string element, params BattleEvent[] effects)
            : this(element, false, effects) { }
        public AbsorbElementEvent(string element, bool singleDraw, params BattleEvent[] effects)
            : this(element, singleDraw, false, new EmptyFiniteEmitter(), "", effects) { }
        public AbsorbElementEvent(string element, bool singleDraw, bool giveMsg, FiniteEmitter emitter, string sound, params BattleEvent[] effects)
            : this()
        {
            AbsorbElement = element;
            SingleDraw = singleDraw;
            GiveMsg = giveMsg;
            foreach (BattleEvent battleEffect in effects)
                BaseEvents.Add(battleEffect);
            Emitter = emitter;
            Sound = sound;
        }
        protected AbsorbElementEvent(AbsorbElementEvent other) : this()
        {
            AbsorbElement = other.AbsorbElement;
            SingleDraw = other.SingleDraw;
            foreach (BattleEvent battleEffect in other.BaseEvents)
                BaseEvents.Add((BattleEvent)battleEffect.Clone());
            Emitter = (FiniteEmitter)other.Emitter.Clone();
            Sound = other.Sound;
        }
        public override GameEvent Clone() { return new AbsorbElementEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Data.Element == AbsorbElement && context.User != context.Target)
            {
                string id = context.Data.ID;
                DataManager.DataType dataType = context.Data.DataType;
                BattleData newData = new BattleData();
                newData.Element = context.Data.Element;
                newData.Category = context.Data.Category;
                newData.HitRate = context.Data.HitRate;
                foreach (SkillState state in context.Data.SkillStates)
                    newData.SkillStates.Set(state.Clone<SkillState>());
                //add the absorption effects
                if (!SingleDraw || !context.GlobalContextStates.Contains<SingleDrawAbsorb>())
                {
                    if (GiveMsg)
                    {
                        newData.OnHits.Add(0, new FormatLogLocalEvent(Text.FormatGrammar(new StringKey("MSG_ABSORB").ToLocal(), ownerChar.GetDisplayName(false), owner.GetDisplayName()), false));
                        newData.OnHits.Add(0, new BattleAnimEvent((FiniteEmitter)Emitter.Clone(), Sound, true, 10));
                    }
                    foreach (BattleEvent battleEffect in BaseEvents)
                        newData.OnHits.Add(0, (BattleEvent)battleEffect.Clone());
                }

                foreach (BattleFX fx in context.Data.IntroFX)
                    newData.IntroFX.Add(new BattleFX(fx));
                newData.HitFX = new BattleFX(context.Data.HitFX);
                context.Data = newData;
                context.Data.ID = id;
                context.Data.DataType = dataType;
            }
            yield break;
        }
    }


    [Serializable]
    public class SetDamageEvent : BattleEvent
    {
        public BattleEvent BaseEvent;

        public List<BattleAnimEvent> Anims;

        public SetDamageEvent() { Anims = new List<BattleAnimEvent>(); }
        public SetDamageEvent(BattleEvent battleEffect, params BattleAnimEvent[] anims)
            : this()
        {
            BaseEvent = battleEffect;
            Anims.AddRange(anims);
        }
        protected SetDamageEvent(SetDamageEvent other) : this()
        {
            BaseEvent = other.BaseEvent;
            Anims = new List<BattleAnimEvent>();
            foreach (BattleAnimEvent anim in other.Anims)
                Anims.Add((BattleAnimEvent)anim.Clone());
        }

        public override GameEvent Clone() { return new SetDamageEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.User != context.Target)
            {
                BattleData newData = new BattleData(context.Data);

                foreach (BattleAnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                foreach (Priority priority in newData.OnHits.GetPriorities())
                {
                    int count = newData.OnHits.GetCountAtPriority(priority);
                    for (int jj = 0; jj < count; jj++)
                    {
                        BattleEvent effect = newData.OnHits.Get(priority, jj);
                        if (effect is DirectDamageEvent)
                            newData.OnHits.Set(priority, jj, (BattleEvent)BaseEvent.Clone());
                    }
                }

                context.Data = newData;
            }
            yield break;
        }
    }


    /// <summary>
    /// Event that maps the current map status to a battle event.
    /// If there is no match, it maps the current map type to a battle event.
    /// </summary>
    [Serializable]
    public class NatureSpecialEvent : BattleEvent
    {
        /// <summary>
        /// The map status mapped to a battle event
        /// </summary>
        [JsonConverter(typeof(MapStatusBattleEventDictConverter))]
        [DataType(0, DataManager.DataType.MapStatus, false)]
        public Dictionary<string, BattleEvent> TerrainPair;

        /// <summary>
        /// The type mapped to a battle event
        /// </summary>
        [JsonConverter(typeof(ElementBattleEventDictConverter))]
        [DataType(1, DataManager.DataType.Element, false)]
        public Dictionary<string, BattleEvent> NaturePair;

        public NatureSpecialEvent()
        {
            TerrainPair = new Dictionary<string, BattleEvent>();
            NaturePair = new Dictionary<string, BattleEvent>();
        }
        public NatureSpecialEvent(Dictionary<string, BattleEvent> terrain, Dictionary<string, BattleEvent> moves)
        {
            TerrainPair = terrain;
            NaturePair = moves;
        }
        protected NatureSpecialEvent(NatureSpecialEvent other)
            : this()
        {
            foreach (string terrain in other.TerrainPair.Keys)
                TerrainPair.Add(terrain, (BattleEvent)other.TerrainPair[terrain].Clone());
            foreach (string element in other.NaturePair.Keys)
                NaturePair.Add(element, (BattleEvent)other.NaturePair[element].Clone());
        }
        public override GameEvent Clone() { return new NatureSpecialEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            foreach (string terrain in TerrainPair.Keys)
            {
                if (ZoneManager.Instance.CurrentMap.Status.ContainsKey(terrain))
                {
                    yield return CoroutineManager.Instance.StartCoroutine(TerrainPair[terrain].Apply(owner, ownerChar, context));
                    yield break;
                }
            }

            BattleEvent effect;
            if (NaturePair.TryGetValue(ZoneManager.Instance.CurrentMap.Element, out effect))
                yield return CoroutineManager.Instance.StartCoroutine(effect.Apply(owner, ownerChar, context));
            else
                yield break;
        }
    }

}

