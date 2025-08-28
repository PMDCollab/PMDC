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

    /// <summary>
    /// Event that modifies the critical change rate based on how effective the move is
    /// </summary>
    [Serializable]
    public class CritEffectiveEvent : BattleEvent
    {
        
        /// <summary>
        /// Whether to check if the move is not effective instead
        /// </summary>
        public bool Reverse;
        
        /// <summary>
        /// The added critical rate chance
        /// </summary>
        public int AddCrit;

        public CritEffectiveEvent() { }
        public CritEffectiveEvent(bool reverse, int addCrit)
        {
            Reverse = reverse;
            AddCrit = addCrit;
        }
        protected CritEffectiveEvent(CritEffectiveEvent other)
        {
            Reverse = other.Reverse;
            AddCrit = other.AddCrit;
        }
        public override GameEvent Clone() { return new CritEffectiveEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int typeMatchup = PreTypeEvent.GetDualEffectiveness(context.User, context.Target, context.Data);
            typeMatchup -= PreTypeEvent.NRM_2;
            if (Reverse)
                typeMatchup *= -1;
            if (typeMatchup > 0)
                context.AddContextStateInt<CritLevel>(AddCrit);

            yield break;
        }
    }




    /// <summary>
    /// Event that modifies the specified stack boost by adding the value in the StackState status state
    /// </summary>
    [Serializable]
    public class UserStatBoostEvent : BattleEvent
    {
        /// <summary>
        /// The stat to modify
        /// </summary>
        public Stat Stat;

        public UserStatBoostEvent() { }
        public UserStatBoostEvent(Stat stat)
        {
            Stat = stat;
        }
        protected UserStatBoostEvent(UserStatBoostEvent other)
        {
            Stat = other.Stat;
        }
        public override GameEvent Clone() { return new UserStatBoostEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int boost = ((StatusEffect)owner).StatusStates.GetWithDefault<StackState>().Stack;
            switch (Stat)
            {
                case Stat.Attack:
                    context.AddContextStateInt<UserAtkBoost>(boost);
                    break;
                case Stat.Defense:
                    context.AddContextStateInt<UserDefBoost>(boost);
                    break;
                case Stat.MAtk:
                    context.AddContextStateInt<UserSpAtkBoost>(boost);
                    break;
                case Stat.MDef:
                    context.AddContextStateInt<UserSpDefBoost>(boost);
                    break;
                case Stat.HitRate:
                    context.AddContextStateInt<UserAccuracyBoost>(boost);
                    break;
                case Stat.Range:
                    context.RangeMod += boost;
                    break;
            }
            yield break;
        }
    }
    
    /// <summary>
    /// Event that modifies the specified stack boost by adding the value in the StackState status state
    /// </summary>
    [Serializable]
    public class TargetStatBoostEvent : BattleEvent
    {

        /// <summary>
        /// The stat to modify
        /// </summary>
        public Stat Stat;

        public TargetStatBoostEvent() { }
        public TargetStatBoostEvent(Stat stat)
        {
            Stat = stat;
        }
        protected TargetStatBoostEvent(TargetStatBoostEvent other)
        {
            Stat = other.Stat;
        }
        public override GameEvent Clone() { return new TargetStatBoostEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int boost = ((StatusEffect)owner).StatusStates.GetWithDefault<StackState>().Stack;
            switch (Stat)
            {
                case Stat.Attack:
                    context.AddContextStateInt<TargetAtkBoost>(boost);
                    break;
                case Stat.Defense:
                    context.AddContextStateInt<TargetDefBoost>(boost);
                    break;
                case Stat.MAtk:
                    context.AddContextStateInt<TargetSpAtkBoost>(boost);
                    break;
                case Stat.MDef:
                    context.AddContextStateInt<TargetSpDefBoost>(boost);
                    break;
                case Stat.DodgeRate:
                    context.AddContextStateInt<TargetEvasionBoost>(boost);
                    break;
            }
            yield break;
        }
    }
    
    /// <summary>
    /// Event that modifies the specified stack boost
    /// </summary>
    [Serializable]
    public class TargetStatAddEvent : BattleEvent
    {
        
        /// <summary>
        /// The stat to modify
        /// </summary>
        public Stat Stat;
        
        /// <summary>
        /// The value to modify the stat by
        /// </summary>
        public int Mod;

        public TargetStatAddEvent() { }
        public TargetStatAddEvent(Stat stat, int mod)
        {
            Stat = stat;
            Mod = mod;
        }
        protected TargetStatAddEvent(TargetStatAddEvent other)
        {
            Stat = other.Stat;
            Mod = other.Mod;
        }
        public override GameEvent Clone() { return new TargetStatAddEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            switch (Stat)
            {
                case Stat.Attack:
                    context.AddContextStateInt<TargetAtkBoost>(Mod);
                    break;
                case Stat.Defense:
                    context.AddContextStateInt<TargetDefBoost>(Mod);
                    break;
                case Stat.MAtk:
                    context.AddContextStateInt<TargetSpAtkBoost>(Mod);
                    break;
                case Stat.MDef:
                    context.AddContextStateInt<TargetSpDefBoost>(Mod);
                    break;
                case Stat.DodgeRate:
                    context.AddContextStateInt<TargetEvasionBoost>(Mod);
                    break;
            }
            yield break;
        }
    }
    

    
    /// <summary>
    /// Event that boosts the critical chance rate
    /// </summary>
    [Serializable]
    public class BoostCriticalEvent : BattleEvent
    {
        
        /// <summary>
        /// The modified critical chance rate
        /// 1 - 25%
        /// 2 - 50%
        /// 3 - 75%
        /// 4 - 100%
        /// </summary>
        public int AddCrit;

        public BoostCriticalEvent() { }
        public BoostCriticalEvent(int addCrit)
        {
            AddCrit = addCrit;
        }
        protected BoostCriticalEvent(BoostCriticalEvent other)
        {
            AddCrit = other.AddCrit;
        }
        public override GameEvent Clone() { return new BoostCriticalEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            context.AddContextStateInt<CritLevel>(AddCrit);
            yield break;
        }
    }
    
    /// <summary>
    /// Event that sets the critical rate chance to 0
    /// </summary>
    [Serializable]
    public class BlockCriticalEvent : BattleEvent
    {
        public override GameEvent Clone() { return new BlockCriticalEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            CritLevel critLevel = context.ContextStates.GetWithDefault<CritLevel>();
            if (critLevel != null)
                critLevel.Count = 0;
            yield break;
        }
    }

    /// <summary>
    /// Event that boosts the rate in the AdditionalEffectState skill state
    /// </summary>
    [Serializable]
    public class BoostAdditionalEvent : BattleEvent
    {
        public BoostAdditionalEvent() { }
        public override GameEvent Clone() { return new BoostAdditionalEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            AdditionalEffectState state = ((BattleData)context.Data).SkillStates.GetWithDefault<AdditionalEffectState>();
            if (state != null)
                state.EffectChance *= 2;
            yield break;
        }
    }

    /// <summary>
    /// Event that sets the rate in the AdditionalEffectState skill state to 0
    /// </summary>
    [Serializable]
    public class BlockAdditionalEvent : BattleEvent
    {
        public override GameEvent Clone() { return new BlockAdditionalEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            AdditionalEffectState state = ((BattleData)context.Data).SkillStates.GetWithDefault<AdditionalEffectState>();
            if (state != null)
                state.EffectChance = 0;
            yield break;
        }
    }


    /// <summary>
    /// Event that allows the user to move again
    /// </summary>
    [Serializable]
    public class PreserveTurnEvent : BattleEvent
    {
        /// <summary>
        /// The message displayed in the dungeon log  
        /// </summary>
        public StringKey Msg;
        
        /// <summary>
        /// The list of battle VFXs played if the condition is met
        /// </summary>
        public List<BattleAnimEvent> Anims;

        public PreserveTurnEvent()
        {
            Anims = new List<BattleAnimEvent>();
        }
        public PreserveTurnEvent(StringKey msg, params BattleAnimEvent[] anims)
        {
            Msg = msg;
            Anims = new List<BattleAnimEvent>();
            Anims.AddRange(anims);
        }
        protected PreserveTurnEvent(PreserveTurnEvent other)
        {
            Msg = other.Msg;
            Anims = new List<BattleAnimEvent>();
            foreach (BattleAnimEvent anim in other.Anims)
                Anims.Add((BattleAnimEvent)anim.Clone());
        }

        public override GameEvent Clone() { return new PreserveTurnEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            DungeonScene.Instance.LogMsg(Text.FormatGrammar(Msg.ToLocal(), ownerChar.GetDisplayName(false), owner.GetDisplayName()));

            foreach (BattleAnimEvent anim in Anims)
                yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

            context.TurnCancel.Cancel = true;

            yield break;
        }
    }
    
    /// <summary>
    /// Event that bounces status conditions move back to the user
    /// </summary>
    [Serializable]
    public class BounceStatusEvent : BattleEvent
    {
        /// <summary>
        /// The message displayed in the dungeon log
        /// </summary>
        public StringKey Msg;

        /// <summary>
        /// Only status effects, not all status moves
        /// </summary>
        public bool StatusOnly;

        /// <summary>
        /// The list of battle VFXs played if the condition is met
        /// </summary>
        public List<BattleAnimEvent> Anims;

        public BounceStatusEvent()
        {
            Anims = new List<BattleAnimEvent>();
        }
        public BounceStatusEvent(bool statusOnly, StringKey msg, params BattleAnimEvent[] anims)
        {
            StatusOnly = statusOnly;
            Msg = msg;
            Anims = new List<BattleAnimEvent>();
            Anims.AddRange(anims);
        }
        protected BounceStatusEvent(BounceStatusEvent other)
        {
            StatusOnly = other.StatusOnly;
            Msg = other.Msg;
            Anims = new List<BattleAnimEvent>();
            foreach (BattleAnimEvent anim in other.Anims)
                Anims.Add((BattleAnimEvent)anim.Clone());
        }

        public override GameEvent Clone() { return new BounceStatusEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Skill && context.Data.Category == BattleData.SkillCategory.Status && DungeonScene.Instance.GetMatchup(context.User, context.Target) != Alignment.Self)
            {
                bool inflictsStatus = false;
                if (StatusOnly)
                {
                    foreach (BattleEvent effect in context.Data.OnHits.EnumerateInOrder())
                    {
                        if (effect is StatusBattleEvent)
                        {
                            StatusBattleEvent giveEffect = (StatusBattleEvent)effect;
                            if (giveEffect.AffectTarget && !giveEffect.Anonymous)
                            {
                                inflictsStatus = true;
                                break;
                            }
                        }
                    }
                }
                else
                    inflictsStatus = true;

                if (inflictsStatus)
                {
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(Msg.ToLocal(), context.Target.GetDisplayName(false)));

                    foreach (BattleAnimEvent anim in Anims)
                        yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                    context.Target = context.User;
                }
            }
            yield break;
        }
    }



    /// <summary>
    /// Event that sets the AttackEndure context state if the character is at full HP
    /// </summary>
    [Serializable]
    public class FullEndureEvent : BattleEvent
    {
        public override GameEvent Clone() { return new FullEndureEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Target.HP == context.Target.MaxHP)
                context.ContextStates.Set(new AttackEndure());
            yield break;
        }
    }
    
    /// <summary>
    /// Event that sets the AttackEndure context state if the character is hit by the specified skill category
    /// </summary>
    [Serializable]
    public class EndureCategoryEvent : BattleEvent
    {
        /// <summary>
        /// The affected skill category
        /// </summary>
        public BattleData.SkillCategory Category;

        public EndureCategoryEvent() { }
        public EndureCategoryEvent(BattleData.SkillCategory category)
        {
            Category = category;
        }
        protected EndureCategoryEvent(EndureCategoryEvent other)
        {
            Category = other.Category;
        }
        public override GameEvent Clone() { return new EndureCategoryEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Data.Category == Category || Category == BattleData.SkillCategory.None)
                context.ContextStates.Set(new AttackEndure());
            yield break;
        }
    }

    /// <summary>
    /// Event that sets the AttackEndure context state if the character is hit by the specified move type
    /// </summary>
    [Serializable]
    public class EndureElementEvent : BattleEvent
    {
        /// <summary>
        /// The affected move type
        /// </summary>
        [JsonConverter(typeof(ElementConverter))]
        [DataType(0, DataManager.DataType.Element, false)]
        public string Element;

        public EndureElementEvent() { Element = ""; }
        public EndureElementEvent(string element)
        {
            Element = element;
        }
        protected EndureElementEvent(EndureElementEvent other)
        {
            Element = other.Element;
        }
        public override GameEvent Clone() { return new EndureElementEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Data.Element == Element)
                context.ContextStates.Set(new AttackEndure());
            yield break;
        }
    }

    
    
    
    /// <summary>
    /// Event that sets the user direction to face the enemy
    /// This event can only be used in statuses 
    /// </summary> 
    [Serializable]
    public class ForceFaceTargetEvent : BattleEvent
    {
        public override GameEvent Clone() { return new ForceFaceTargetEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            StatusEffect status = ((StatusEffect)owner);
            if (status.TargetChar != null)
            {
                Dir8 attackDir = ZoneManager.Instance.CurrentMap.GetClosestDir8(ownerChar.CharLoc, status.TargetChar.CharLoc);
                ownerChar.CharDir = attackDir;
            }
            yield break;
        }
    }
    
    

    
    
    /// <summary>
    /// Event that sets the AttackedThisTurnState status state to be true, indicating that the character attacked this turn
    /// This event can only be used on statuses
    /// </summary>
    [Serializable]
    public class AttackedThisTurnEvent : BattleEvent
    {
        public override GameEvent Clone() { return new AttackedThisTurnEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            AttackedThisTurnState recent = ((StatusEffect)owner).StatusStates.GetWithDefault<AttackedThisTurnState>();
            recent.Attacked = true;
            yield break;
        }
    }



    /// <summary>
    /// Event that adds the specified context state 
    /// </summary> 
    [Serializable]
    public class AddContextStateEvent : BattleEvent
    {
        /// <summary>
        /// The context state to add
        /// </summary> 
        public ContextState AddedState;
        
        /// <summary>
        /// Whether to add the context state globally
        /// </summary> 
        public bool Global;

        public AddContextStateEvent() { }
        public AddContextStateEvent(ContextState state) : this(state, false) { }
        public AddContextStateEvent(ContextState state, bool global) { AddedState = state; Global = global; }
        protected AddContextStateEvent(AddContextStateEvent other)
        {
            AddedState = other.AddedState.Clone<ContextState>();
            Global = other.Global;
        }
        public override GameEvent Clone() { return new AddContextStateEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (Global)
                context.GlobalContextStates.Set(AddedState.Clone<ContextState>());
            else
                context.ContextStates.Set(AddedState.Clone<ContextState>());
            yield break;
        }
    }
    



    /// <summary>
    /// Event that reverses the effect that speed has on hit and dodge rate.
    /// </summary> 
    [Serializable]
    public class SpeedReverseHitEvent : BattleEvent
    {
        public SpeedReverseHitEvent() { }
        protected SpeedReverseHitEvent(SpeedReverseHitEvent other)
        {

        }
        public override GameEvent Clone() { return new SpeedReverseHitEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int userSpeed = context.GetContextStateInt<UserHitStat>(1);
            int targetSpeed = context.GetContextStateInt<TargetEvadeStat>(1);

            context.ContextStates.Set(new UserHitStat(targetSpeed));
            context.ContextStates.Set(new TargetEvadeStat(userSpeed));

            yield break;
        }
    }

    /// <summary>
    /// Event that uses the target's attack stat to calculate the damage
    /// </summary> 
    [Serializable]
    public class FoulPlayEvent : BattleEvent
    {
        public FoulPlayEvent() { }
        public override GameEvent Clone() { return new FoulPlayEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Data.Category == BattleData.SkillCategory.Physical)
                context.ContextStates.Set(new UserAtkStat(context.Target.Atk));
            else if (context.Data.Category == BattleData.SkillCategory.Magical)
                context.ContextStates.Set(new UserAtkStat(context.Target.MAtk));
            context.ContextStates.Set(new UserAtkBoost(context.GetContextStateInt<TargetAtkBoost>(0)));
            context.ContextStates.Set(new UserSpAtkBoost(context.GetContextStateInt<TargetSpAtkBoost>(0)));
            yield break;
        }
    }
    
    /// <summary>
    /// Event that ignores any stat boosts the character has
    /// </summary> 
    [Serializable]
    public class IgnoreStatsEvent : BattleEvent
    {   
        /// <summary>
        /// Whether to ignore the target or user stat boosts
        /// </summary> 
        public bool AffectTarget;

        public IgnoreStatsEvent() { }
        public IgnoreStatsEvent(bool affectTarget)
        {
            AffectTarget = affectTarget;
        }
        protected IgnoreStatsEvent(IgnoreStatsEvent other)
        {
            AffectTarget = other.AffectTarget;
        }
        public override GameEvent Clone() { return new IgnoreStatsEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (AffectTarget)
            {
                context.ContextStates.Set(new TargetAtkBoost());
                context.ContextStates.Set(new TargetSpAtkBoost());
                context.ContextStates.Set(new TargetDefBoost());
                context.ContextStates.Set(new TargetSpDefBoost());
                context.ContextStates.Set(new TargetEvasionBoost());
            }
            else
            {
                context.ContextStates.Set(new UserAtkBoost());
                context.ContextStates.Set(new UserSpAtkBoost());
                context.ContextStates.Set(new UserDefBoost());
                context.ContextStates.Set(new UserSpDefBoost());
                context.ContextStates.Set(new UserAccuracyBoost());
            }
            yield break;
        }
    }
    
    /// <summary>
    /// Event that ignores the user's accuracy descrease and target's evasive boosts
    /// </summary> 
    [Serializable]
    public class IgnoreHaxEvent : BattleEvent
    {
        public override GameEvent Clone() { return new IgnoreHaxEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            context.ContextStates.Set(new TargetEvasionBoost(Math.Min(0, context.GetContextStateInt<TargetEvasionBoost>(0))));
            context.ContextStates.Set(new UserAccuracyBoost(Math.Max(0, context.GetContextStateInt<UserAccuracyBoost>(0))));
            yield break;
        }
    }



    
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


    /// <summary>
    /// Event that restores or reduces the hunger of the character by the specified amount 
    /// </summary>
    [Serializable]
    public class RestoreBellyEvent : BattleEvent
    {
        public const int MIN_MAX_FULLNESS = 50;
        public const int MAX_MAX_FULLNESS = 150;

        public List<BattleAnimEvent> BoostAnims;
        
        /// <summary>
        /// The amount of hunger to restore or reduce
        /// </summary>
        public int Heal;
        
        /// <summary>
        /// Whether to display the messages relating to hunger
        /// </summary>
        public bool Msg;
        
        /// <summary>
        /// The amount to increase or decrease the max hunger by
        /// </summary>
        public int AddMaxBelly;
        
        /// <summary>
        /// Whether full hunger is needed to add the max hunger amount
        /// </summary>
        public bool NeedFullBelly;

        public RestoreBellyEvent()
        {
            BoostAnims = new List<BattleAnimEvent>();
        }
        public RestoreBellyEvent(int heal, bool msg)
        {
            Heal = heal;
            Msg = msg;
            BoostAnims = new List<BattleAnimEvent>();
        }
        public RestoreBellyEvent(int heal, bool msg, int bellyPlus, bool needFull, params BattleAnimEvent[] boostAnims)
        {
            Heal = heal;
            Msg = msg;
            AddMaxBelly = bellyPlus;
            NeedFullBelly = needFull;
            BoostAnims = new List<BattleAnimEvent>();
            BoostAnims.AddRange(boostAnims);
        }
        protected RestoreBellyEvent(RestoreBellyEvent other)
        {
            Heal = other.Heal;
            Msg = other.Msg;
            AddMaxBelly = other.AddMaxBelly;
            NeedFullBelly = other.NeedFullBelly;
            BoostAnims = new List<BattleAnimEvent>();
            foreach (BattleAnimEvent anim in other.BoostAnims)
                BoostAnims.Add((BattleAnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new RestoreBellyEvent(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            bool fullBelly = (context.Target.Fullness == context.Target.MaxFullness);


            context.Target.Fullness += Heal;

            if (Heal < 0)
            {
                if (Msg)
                {
                    if (context.Target.Fullness <= 0)
                    {
                        if (context.Target.MemberTeam == DungeonScene.Instance.ActiveTeam)
                            DungeonScene.Instance.LogMsg(Text.FormatKey("MSG_HUNGER_EMPTY", context.Target.GetDisplayName(true)));
                        else
                            DungeonScene.Instance.LogMsg(Text.FormatKey("MSG_HUNGER_EMPTY_FOE", context.Target.GetDisplayName(true)));
                    }
                    else
                        DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_HUNGER_DROP").ToLocal(), context.Target.GetDisplayName(false)));
                }
                GameManager.Instance.BattleSE("DUN_Hunger");
            }
            else
            {
                if (Msg)
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_HUNGER_FILL").ToLocal(), context.Target.GetDisplayName(false)));
            }

            if (AddMaxBelly != 0 && (fullBelly || !NeedFullBelly))
            {
                if (Msg)
                {
                    if (AddMaxBelly < 0)
                        DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_MAX_HUNGER_DROP").ToLocal(), context.Target.GetDisplayName(false)));
                    else
                        DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_MAX_HUNGER_BOOST").ToLocal(), context.Target.GetDisplayName(false)));


                    foreach (BattleAnimEvent anim in BoostAnims)
                        yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));
                }
                context.Target.MaxFullness += AddMaxBelly;
                if (context.Target.MaxFullness < MIN_MAX_FULLNESS)
                    context.Target.MaxFullness = MIN_MAX_FULLNESS;
                if (context.Target.MaxFullness > MAX_MAX_FULLNESS)
                    context.Target.MaxFullness = MAX_MAX_FULLNESS;
            }

            if (context.Target.Fullness < 0)
                context.Target.Fullness = 0;
            if (context.Target.Fullness >= context.Target.MaxFullness)
            {
                context.Target.Fullness = context.Target.MaxFullness;
                context.Target.FullnessRemainder = 0;
            }

            yield break;
        }
    }
    
    



    /// <summary>
    /// Event that sets the character and tile sight to be clear
    /// </summary>
    [Serializable]
    public class LuminousEvent : BattleEvent
    {
        public override GameEvent Clone() { return new LuminousEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            ZoneManager.Instance.CurrentMap.CharSight = Map.SightRange.Clear;
            ZoneManager.Instance.CurrentMap.TileSight = Map.SightRange.Clear;
            yield break;
        }
    }


    
    /// <summary>
    /// Event that makes the target a neutral faction  
    /// </summary>
    [Serializable]
    public class MakeNeutralEvent : BattleEvent
    {
        
        /// <summary>
        /// Tha lua battle script that runs when interacting with the neutral in dungeons 
        /// </summary>
        public BattleScriptEvent ActionScript;

        public MakeNeutralEvent()
        { }
        public MakeNeutralEvent(BattleScriptEvent scriptEvent)
        {
            ActionScript = scriptEvent;
        }

        public MakeNeutralEvent(MakeNeutralEvent other)
        {
            ActionScript = (BattleScriptEvent)other.ActionScript.Clone();
        }

        public override GameEvent Clone() { return new MakeNeutralEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            DungeonScene.Instance.RemoveChar(context.Target);
            ExplorerTeam neutralTeam = new ExplorerTeam();
            AITactic tactic = DataManager.Instance.GetAITactic("slow_wander");
            context.Target.Tactic = new AITactic(tactic);
            neutralTeam.Players.Add(context.Target);
            DungeonScene.Instance.AddTeam(Faction.Friend, neutralTeam);
            DungeonScene.Instance.OnCharAdd(context.Target);

            context.Target.RefreshTraits();
            context.Target.Tactic.Initialize(context.Target);

            int oldFullness = context.Target.Fullness;
            context.Target.FullRestore();
            context.Target.Fullness = oldFullness;

            context.Target.ActionEvents.Clear();
            if (ActionScript != null)
                context.Target.ActionEvents.Add((BattleEvent)ActionScript.Clone());

            yield break;
        }
    }
    
    /// <summary>
    /// Event that exits out of the dungeon
    /// </summary>
    [Serializable]
    public class ExitDungeonEvent : BattleEvent
    {
        public ExitDungeonEvent() { }
        public override GameEvent Clone() { return new ExitDungeonEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.User.MemberTeam == DungeonScene.Instance.ActiveTeam)
            {
                GameManager.Instance.BGM("", true);
                yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.FadeOut(true));

                // remove all unpaid items
                for (int ii = DungeonScene.Instance.ActiveTeam.GetInvCount() - 1; ii >= 0; ii--)
                {
                    if (DungeonScene.Instance.ActiveTeam.GetInv(ii).Price > 0)
                        DungeonScene.Instance.ActiveTeam.RemoveFromInv(ii);
                }

                yield return CoroutineManager.Instance.StartCoroutine(GameManager.Instance.EndSegment(GameProgress.ResultType.Escaped));
                context.CancelState.Cancel = true;
                context.TurnCancel.Cancel = true;
            }
        }
    }

}
