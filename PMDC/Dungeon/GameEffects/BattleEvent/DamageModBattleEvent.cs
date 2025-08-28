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
    // Battle events that modify damage through DmgMult

    /// <summary>
    /// Event that boosts/reduces the attacks of the chosen type
    /// </summary>
    [Serializable]
    public class MultiplyElementEvent : BattleEvent
    {
        /// <summary>
        /// The type affected
        /// </summary>
        [JsonConverter(typeof(ElementConverter))]
        [DataType(0, DataManager.DataType.Element, false)]
        public string MultElement;

        /// <summary>
        /// The numerator of the modifier
        /// </summary>
        public int Numerator;

        /// <summary>
        /// The denominator of the modifier
        /// </summary>
        public int Denominator;

        /// <summary>
        /// The list of battle VFXs played if the condition is met
        /// </summary>
        public List<BattleAnimEvent> Anims;

        /// <summary>
        /// Whether to display a message if the move type matches
        /// </summary>
        public bool Msg;

        public MultiplyElementEvent()
        {
            MultElement = "";
            Anims = new List<BattleAnimEvent>();
        }
        public MultiplyElementEvent(string element, int numerator, int denominator, bool msg)
        {
            MultElement = element;
            Numerator = numerator;
            Denominator = denominator;
            Msg = msg;
            Anims = new List<BattleAnimEvent>();
        }
        public MultiplyElementEvent(string element, int numerator, int denominator, bool msg, params BattleAnimEvent[] anims)
        {
            MultElement = element;
            Numerator = numerator;
            Denominator = denominator;
            Msg = msg;
            Anims = new List<BattleAnimEvent>();
            Anims.AddRange(anims);
        }
        protected MultiplyElementEvent(MultiplyElementEvent other)
        {
            MultElement = other.MultElement;
            Numerator = other.Numerator;
            Denominator = other.Denominator;
            Msg = other.Msg;
            Anims = new List<BattleAnimEvent>();
            foreach (BattleAnimEvent anim in other.Anims)
                Anims.Add((BattleAnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new MultiplyElementEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Data.Element == MultElement &&
                (context.Data.Category == BattleData.SkillCategory.Physical || context.Data.Category == BattleData.SkillCategory.Magical))
            {
                if (Msg)
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_PROTECT_WITH").ToLocal(), ownerChar.GetDisplayName(false), owner.GetDisplayName()));
                foreach (BattleAnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                context.AddContextStateMult<DmgMult>(false, Numerator, Denominator);
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that boosts/reduces the attacks of non-matching types
    /// </summary>
    [Serializable]
    public class MultiplyNotElementEvent : BattleEvent
    {
        /// <summary>
        /// The types not affected by the modifier
        /// </summary>
        [DataType(1, DataManager.DataType.Element, false)]
        public HashSet<string> NotMultElement;

        /// <summary>
        /// The numerator of the modifier
        /// </summary>
        public int Numerator;

        /// <summary>
        /// The denominator of the modifier
        /// </summary>
        public int Denominator;

        /// <summary>
        /// The list of battle VFXs played if the condition is met
        /// </summary>
        public List<BattleAnimEvent> Anims;

        /// <summary>
        /// Whether to display a message if the move type does not match
        /// </summary>
        public bool Msg;

        public MultiplyNotElementEvent()
        {
            NotMultElement = new HashSet<string>();
            Anims = new List<BattleAnimEvent>();
        }
        public MultiplyNotElementEvent(string element, int numerator, int denominator, bool msg)
        {
            NotMultElement = new HashSet<string>();
            NotMultElement.Add(element);
            Numerator = numerator;
            Denominator = denominator;
            Msg = msg;
            Anims = new List<BattleAnimEvent>();
        }
        public MultiplyNotElementEvent(string element, int numerator, int denominator, bool msg, params BattleAnimEvent[] anims)
        {
            NotMultElement = new HashSet<string>();
            NotMultElement.Add(element);
            Numerator = numerator;
            Denominator = denominator;
            Msg = msg;
            Anims = new List<BattleAnimEvent>();
            Anims.AddRange(anims);
        }
        protected MultiplyNotElementEvent(MultiplyNotElementEvent other)
        {
            NotMultElement = new HashSet<string>();
            foreach (string element in other.NotMultElement)
                NotMultElement.Add(element);
            Numerator = other.Numerator;
            Denominator = other.Denominator;
            Msg = other.Msg;
            Anims = new List<BattleAnimEvent>();
            foreach (BattleAnimEvent anim in other.Anims)
                Anims.Add((BattleAnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new MultiplyNotElementEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (!NotMultElement.Contains(context.Data.Element) && context.Data.Element != DataManager.Instance.DefaultElement &&
                (context.Data.Category == BattleData.SkillCategory.Physical || context.Data.Category == BattleData.SkillCategory.Magical))
            {
                if (Msg)
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_PROTECT_WITH").ToLocal(), ownerChar.GetDisplayName(false), owner.GetDisplayName()));
                foreach (BattleAnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                context.AddContextStateMult<DmgMult>(false, Numerator, Denominator);
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that boosts/reduces the attacks if the attack's type matches the type in ElementState (StatusState)
    /// </summary>
    [Serializable]
    public class MultiplyStatusElementEvent : BattleEvent
    {
        /// <summary>
        /// The numerator of the modifier
        /// </summary>
        public int Numerator;

        /// <summary>
        /// The denominator of the modifier
        /// </summary>
        public int Denominator;

        /// <summary>
        /// The list of battle VFXs played if the type matches
        /// </summary>
        public List<BattleAnimEvent> Anims;

        public MultiplyStatusElementEvent()
        {
            Anims = new List<BattleAnimEvent>();
        }
        public MultiplyStatusElementEvent(int numerator, int denominator)
        {
            Numerator = numerator;
            Denominator = denominator;
            Anims = new List<BattleAnimEvent>();
        }
        public MultiplyStatusElementEvent(int numerator, int denominator, params BattleAnimEvent[] anims)
        {
            Numerator = numerator;
            Denominator = denominator;
            Anims = new List<BattleAnimEvent>();
            Anims.AddRange(anims);
        }
        protected MultiplyStatusElementEvent(MultiplyStatusElementEvent other)
        {
            Numerator = other.Numerator;
            Denominator = other.Denominator;
            Anims = new List<BattleAnimEvent>();
            foreach (BattleAnimEvent anim in other.Anims)
                Anims.Add((BattleAnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new MultiplyStatusElementEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Data.Element == ((StatusEffect)owner).StatusStates.GetWithDefault<ElementState>().Element)
            {
                foreach (BattleAnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                context.AddContextStateMult<DmgMult>(false, Numerator, Denominator);
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that modifies the damage multiplier based on how many teammates are defeated.
    /// </summary>
    [Serializable]
    public class MultiplyFromFallenEvent : BattleEvent
    {
        /// <summary>
        /// Denominator of the modifier
        /// </summary>
        public int Denominator;

        public MultiplyFromFallenEvent() { }
        public MultiplyFromFallenEvent(int denominator) : this()
        {
            Denominator = denominator;
        }
        protected MultiplyFromFallenEvent(MultiplyFromFallenEvent other) : this()
        {
            Denominator = other.Denominator;
        }
        public override GameEvent Clone() { return new MultiplyFromFallenEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int numerator = Denominator;
            foreach (Character player in context.User.MemberTeam.Players)
            {
                if (player.Dead)
                    numerator++;
            }
            if (numerator != Denominator)
                context.AddContextStateMult<DmgMult>(false, numerator, Denominator);
            yield break;
        }
    }



    /// <summary>
    /// Event that changes the battle event depending on the total amount of team members of the same type
    /// </summary>
    [Serializable]
    public class TeamReduceEvent : BattleEvent
    {
        /// <summary>
        /// The qualifying type
        /// </summary>
        [JsonConverter(typeof(ElementConverter))]
        [DataType(0, DataManager.DataType.Element, false)]
        public string QualifyingElement;

        /// <summary>
        /// Battle event that occurs if only 1 team member has the type
        /// </summary>
        public BattleEvent Tier1Event;

        /// <summary>
        /// Battle event that occurs if only 2 team members has the type
        /// </summary>
        public BattleEvent Tier2Event;

        /// <summary>
        /// Battle event that occurs if only 3 team members has the type
        /// </summary>
        public BattleEvent Tier3Event;

        /// <summary>
        /// Battle event that occurs if 4 or more team members has the type
        /// </summary>
        public BattleEvent Tier4Event;

        public TeamReduceEvent() { QualifyingElement = ""; }
        public TeamReduceEvent(string element, BattleEvent tier1, BattleEvent tier2, BattleEvent tier3, BattleEvent tier4)
        {
            QualifyingElement = element;
            Tier1Event = tier1;
            Tier2Event = tier2;
            Tier3Event = tier3;
            Tier4Event = tier4;
        }
        protected TeamReduceEvent(TeamReduceEvent other)
        {
            QualifyingElement = other.QualifyingElement;
            if (Tier1Event != null)
                Tier1Event = (BattleEvent)other.Tier1Event.Clone();
            if (Tier2Event != null)
                Tier2Event = (BattleEvent)other.Tier2Event.Clone();
            if (Tier3Event != null)
                Tier3Event = (BattleEvent)other.Tier3Event.Clone();
            if (Tier4Event != null)
                Tier4Event = (BattleEvent)other.Tier4Event.Clone();
        }
        public override GameEvent Clone() { return new TeamReduceEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Target.HasElement(QualifyingElement))
            {
                Team team = context.Target.MemberTeam;
                int totalMembers = 0;
                foreach (Character member in team.EnumerateChars())
                {
                    if (member.HasElement(QualifyingElement))
                        totalMembers++;
                }
                if (totalMembers > 3 && Tier4Event != null)
                    yield return CoroutineManager.Instance.StartCoroutine(Tier4Event.Apply(owner, ownerChar, context));
                else if (totalMembers == 3 && Tier3Event != null)
                    yield return CoroutineManager.Instance.StartCoroutine(Tier3Event.Apply(owner, ownerChar, context));
                else if (totalMembers == 2 && Tier2Event != null)
                    yield return CoroutineManager.Instance.StartCoroutine(Tier2Event.Apply(owner, ownerChar, context));
                else if (totalMembers == 1 && Tier1Event != null)
                    yield return CoroutineManager.Instance.StartCoroutine(Tier1Event.Apply(owner, ownerChar, context));
            }
        }
    }

    // TODO: Remove hardcode
    /// <summary>
    /// Event that boosts/reduces an attack type of the user if their HP is low.
    /// </summary>
    [Serializable]
    public class PinchEvent : BattleEvent
    {

        /// <summary>
        /// The qualifying type
        /// </summary>
        [JsonConverter(typeof(ElementConverter))]
        [DataType(0, DataManager.DataType.Element, false)]
        public string PinchElement;

        public PinchEvent() { PinchElement = ""; }
        public PinchEvent(string element)
        {
            PinchElement = element;
        }
        protected PinchEvent(PinchEvent other)
        {
            PinchElement = other.PinchElement;
        }
        public override GameEvent Clone() { return new PinchEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Data.Element == PinchElement && context.User.HP <= context.User.MaxHP / 4)
                context.AddContextStateMult<DmgMult>(false, 2, 1);
            yield break;
        }
    }


    /// <summary>
    /// Event that boosts the attack if the move type is the same type as the character 
    /// </summary>
    [Serializable]
    public class AdaptabilityEvent : BattleEvent
    {
        public override GameEvent Clone() { return new AdaptabilityEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.User.HasElement(context.Data.Element))
                context.AddContextStateMult<DmgMult>(false, 5, 4);
            yield break;
        }
    }


    /// <summary>
    /// Event that boosts/reduces attacks of a skill category (ex: physical and special)
    /// </summary>
    [Serializable]
    public class MultiplyCategoryEvent : BattleEvent
    {

        /// <summary>
        /// The skill category affected
        /// </summary>
        public BattleData.SkillCategory Category;

        /// <summary>
        /// The numerator of the modifier
        /// </summary>
        public int Numerator;

        /// <summary>
        /// The denominator of the modififer
        /// </summary>
        public int Denominator;

        /// <summary>
        /// The list of battle VFXs played if the condition is met
        /// </summary>
        public List<BattleAnimEvent> Anims;

        public MultiplyCategoryEvent()
        {
            Anims = new List<BattleAnimEvent>();
        }
        public MultiplyCategoryEvent(BattleData.SkillCategory category, int numerator, int denominator)
        {
            Category = category;
            Numerator = numerator;
            Denominator = denominator;
            Anims = new List<BattleAnimEvent>();
        }
        public MultiplyCategoryEvent(BattleData.SkillCategory category, int numerator, int denominator, params BattleAnimEvent[] anims)
        {
            Category = category;
            Numerator = numerator;
            Denominator = denominator;
            Anims = new List<BattleAnimEvent>();
            Anims.AddRange(anims);
        }
        protected MultiplyCategoryEvent(MultiplyCategoryEvent other)
        {
            Category = other.Category;
            Numerator = other.Numerator;
            Denominator = other.Denominator;
            Anims = new List<BattleAnimEvent>();
            foreach (BattleAnimEvent anim in other.Anims)
                Anims.Add((BattleAnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new MultiplyCategoryEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Data.Category == Category || Category == BattleData.SkillCategory.None)
            {

                foreach (BattleAnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                context.AddContextStateMult<DmgMult>(false, Numerator, Denominator);
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that boosts/reduces attacks of a skill category of a certain type
    /// </summary>
    [Serializable]
    public class TypeSpecificMultCategoryEvent : BattleEvent
    {

        /// <summary>
        /// The type affected
        /// </summary>
        [JsonConverter(typeof(ElementConverter))]
        [DataType(0, DataManager.DataType.Element, false)]
        public string Element;

        /// <summary>
        /// Context state to prevent boost stacking
        /// </summary>
        public ContextState NoDupeState;

        /// <summary>
        /// The skill category affected
        /// </summary>
        public BattleData.SkillCategory Category;

        /// <summary>
        /// The numerator of the modifier + the denominator
        /// </summary>
        public int NumeratorAdd;

        /// <summary>
        /// The denominator of the modifier
        /// </summary>
        public int Denominator;

        public TypeSpecificMultCategoryEvent()
        { Element = ""; }
        public TypeSpecificMultCategoryEvent(string element, ContextState state, BattleData.SkillCategory category, int denominator, int numerator)
        {
            Element = element;
            NoDupeState = state;
            Category = category;
            NumeratorAdd = numerator;
            Denominator = denominator;
        }
        protected TypeSpecificMultCategoryEvent(TypeSpecificMultCategoryEvent other)
        {
            Element = other.Element;
            NoDupeState = other.NoDupeState.Clone<ContextState>();
            Category = other.Category;
            NumeratorAdd = other.NumeratorAdd;
            Denominator = other.Denominator;
        }
        public override GameEvent Clone() { return new TypeSpecificMultCategoryEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (ownerChar.HasElement(Element) && context.Data.Category == Category)
            {
                if (!context.ContextStates.Contains(NoDupeState.GetType()))
                {
                    context.AddContextStateMult<DmgMult>(false, NumeratorAdd + Denominator, Denominator);
                    context.ContextStates.Set(NoDupeState);
                }
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that modifies the damage multplier for multiple attacks taken in a turn.
    /// </summary>
    [Serializable]
    public class BarrageGuardEvent : BattleEvent
    {

        /// <summary>
        /// Status that keeps track of the move last hit
        /// This status should usually be "was_hurt_last_turn"
        /// </summary>
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string PrevHitID;

        /// <summary>
        /// Numerator of the modifier
        /// </summary>
        public int Numerator;

        /// <summary>
        /// Denominator of the modifier
        /// </summary>
        public int Denominator;

        /// <summary>
        /// The list of battle VFXs played if the condition is met
        /// </summary>
        public List<BattleAnimEvent> Anims;

        public BarrageGuardEvent()
        {
            Anims = new List<BattleAnimEvent>();
            PrevHitID = "";
        }
        public BarrageGuardEvent(string prevHitID, int numerator, int denominator)
        {
            PrevHitID = prevHitID;
            Numerator = numerator;
            Denominator = denominator;
            Anims = new List<BattleAnimEvent>();
        }
        public BarrageGuardEvent(string prevHitID, int numerator, int denominator, params BattleAnimEvent[] anims)
        {
            PrevHitID = prevHitID;
            Numerator = numerator;
            Denominator = denominator;
            Anims = new List<BattleAnimEvent>();
            Anims.AddRange(anims);
        }
        protected BarrageGuardEvent(BarrageGuardEvent other)
        {
            PrevHitID = other.PrevHitID;
            Numerator = other.Numerator;
            Denominator = other.Denominator;
            Anims = new List<BattleAnimEvent>();
            foreach (BattleAnimEvent anim in other.Anims)
                Anims.Add((BattleAnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new BarrageGuardEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            StatusEffect recentHitStatus = context.Target.GetStatusEffect(PrevHitID);
            if (recentHitStatus != null && DungeonScene.Instance.GetMatchup(context.User, context.Target) == Alignment.Foe)
            {
                int timesHit = recentHitStatus.StatusStates.GetWithDefault<StackState>().Stack;
                foreach (BattleAnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                context.AddContextStateMult<DmgMult>(false, Numerator, Denominator + timesHit);
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that modifies the damage multiplier for moves that have recoil damage
    /// The move must be a RecoilEvent or CrashLandEvent
    /// </summary>
    [Serializable]
    public class MultiplyRecklessEvent : BattleEvent
    {

        /// <summary>
        /// Numerator of the modifier
        /// </summary>
        public int Numerator;

        /// <summary>
        /// Denominator of the modifier
        /// </summary>
        public int Denominator;

        public MultiplyRecklessEvent() { }
        public MultiplyRecklessEvent(int numerator, int denominator)
        {
            Numerator = numerator;
            Denominator = denominator;
        }
        protected MultiplyRecklessEvent(MultiplyRecklessEvent other)
        {
            Numerator = other.Numerator;
            Denominator = other.Denominator;
        }
        public override GameEvent Clone() { return new MultiplyRecklessEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            bool reckless = false;
            foreach (BattleEvent effect in context.Data.AfterActions.EnumerateInOrder())
            {
                if (effect is RecoilEvent || effect is CrashLandEvent)
                {
                    reckless = true;
                    break;
                }
            }
            if (reckless)
                context.AddContextStateMult<DmgMult>(false, Numerator, Denominator);
            yield break;
        }
    }


    /// <summary>
    /// Event that modifies the damage multiplier if the move contains one of the specified SkillStates
    /// </summary>
    [Serializable]
    public class MultiplyMoveStateEvent : BattleEvent
    {

        /// <summary>
        /// The list of valid SkillStates types
        /// </summary>
        [StringTypeConstraint(1, typeof(SkillState))]
        public List<FlagType> States;

        /// <summary>
        /// Numerator of the modifier
        /// </summary>
        public int Numerator;

        /// <summary>
        /// Denominator of the modifier
        /// </summary>
        public int Denominator;

        public MultiplyMoveStateEvent() { States = new List<FlagType>(); }
        public MultiplyMoveStateEvent(Type state, int numerator, int denominator) : this()
        {
            States.Add(new FlagType(state));
            Numerator = numerator;
            Denominator = denominator;
        }
        protected MultiplyMoveStateEvent(MultiplyMoveStateEvent other) : this()
        {
            States.AddRange(other.States);
            Numerator = other.Numerator;
            Denominator = other.Denominator;
        }
        public override GameEvent Clone() { return new MultiplyMoveStateEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            bool hasState = false;
            foreach (FlagType state in States)
            {
                if (context.Data.SkillStates.Contains(state.FullType))
                    hasState = true;
            }
            if (hasState)
                context.AddContextStateMult<DmgMult>(false, Numerator, Denominator);
            yield break;
        }
    }


    /// <summary>
    /// Event that modifies the damage multiplier of a skill category under a map status
    /// </summary>
    [Serializable]
    public class MultiplyCategoryInWeatherEvent : BattleEvent
    {

        /// <summary>
        /// The map status to check for
        /// </summary>
        [JsonConverter(typeof(MapStatusConverter))]
        [DataType(0, DataManager.DataType.MapStatus, false)]
        public string WeatherID;

        /// <summary>
        /// The skill category affected
        /// </summary>
        public BattleData.SkillCategory Category;

        /// <summary>
        /// The numerator of the modifier
        /// </summary>
        public int Numerator;

        /// <summary>
        /// The denominator of the modifier
        /// </summary>
        public int Denominator;

        public MultiplyCategoryInWeatherEvent() { WeatherID = ""; }
        public MultiplyCategoryInWeatherEvent(string weatherId, BattleData.SkillCategory category, int numerator, int denominator)
        {
            WeatherID = weatherId;
            Category = category;
            Numerator = numerator;
            Denominator = denominator;
        }
        protected MultiplyCategoryInWeatherEvent(MultiplyCategoryInWeatherEvent other)
        {
            WeatherID = other.WeatherID;
            Category = other.Category;
            Numerator = other.Numerator;
            Denominator = other.Denominator;
        }
        public override GameEvent Clone() { return new MultiplyCategoryInWeatherEvent(this); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (ZoneManager.Instance.CurrentMap.Status.ContainsKey(WeatherID))
            {
                if (context.Data.Category == Category || Category == BattleData.SkillCategory.None)
                    context.AddContextStateMult<DmgMult>(false, Numerator, Denominator);
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that modifies the damage multiplier of a skill category under a major status condition
    /// </summary>
    [Serializable]
    public class MultiplyCategoryInMajorStatusEvent : BattleEvent
    {

        /// <summary>
        /// The skill category affected
        /// </summary>
        public BattleData.SkillCategory Category;

        /// <summary>
        /// The numerator of the modifier
        /// </summary>
        public int Numerator;

        /// <summary>
        /// The denominator of the modifier
        /// </summary>
        public int Denominator;

        /// <summary>
        /// Whether to affect the user or target
        /// </summary>
        public bool AffectTarget;

        public MultiplyCategoryInMajorStatusEvent() { }
        public MultiplyCategoryInMajorStatusEvent(BattleData.SkillCategory category, int numerator, int denominator, bool affectTarget)
        {
            Category = category;
            Numerator = numerator;
            Denominator = denominator;
            AffectTarget = affectTarget;
        }
        protected MultiplyCategoryInMajorStatusEvent(MultiplyCategoryInMajorStatusEvent other)
        {
            Category = other.Category;
            Numerator = other.Numerator;
            Denominator = other.Denominator;
            AffectTarget = other.AffectTarget;
        }
        public override GameEvent Clone() { return new MultiplyCategoryInMajorStatusEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = (AffectTarget ? context.Target : context.User);

            if (context.Data.Category == Category || Category == BattleData.SkillCategory.None)
            {
                foreach (StatusEffect status in target.IterateStatusEffects())
                {
                    if (status.StatusStates.Contains<MajorStatusState>())
                    {
                        context.AddContextStateMult<DmgMult>(false, Numerator, Denominator);
                        break;
                    }
                }
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that modifies the damage multiplier of a skill category if not affected by the specified status condition.
    /// </summary>
    [Serializable]
    public class MultiplyCategoryWithoutStatusEvent : BattleEvent
    {

        /// <summary>
        /// The status condition being checked for
        /// </summary>
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string StatusID;

        /// <summary>
        /// The skill category affected
        /// </summary>
        public BattleData.SkillCategory Category;

        /// <summary>
        /// The numerator of the modifier
        /// </summary>
        public int Numerator;

        /// <summary>
        /// The denominator of the modifier
        /// </summary>
        public int Denominator;

        /// <summary>
        /// Whether to affect the user or target
        /// </summary>
        public bool AffectTarget;

        public MultiplyCategoryWithoutStatusEvent() { StatusID = ""; }
        public MultiplyCategoryWithoutStatusEvent(string statusID, BattleData.SkillCategory category, int numerator, int denominator, bool affectTarget)
        {
            StatusID = statusID;
            Category = category;
            Numerator = numerator;
            Denominator = denominator;
            AffectTarget = affectTarget;
        }
        protected MultiplyCategoryWithoutStatusEvent(MultiplyCategoryWithoutStatusEvent other)
        {
            StatusID = other.StatusID;
            Category = other.Category;
            Numerator = other.Numerator;
            Denominator = other.Denominator;
            AffectTarget = other.AffectTarget;
        }
        public override GameEvent Clone() { return new MultiplyCategoryWithoutStatusEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = (AffectTarget ? context.Target : context.User);

            if (context.Data.Category == Category || Category == BattleData.SkillCategory.None)
            {
                if (target.GetStatusEffect(StatusID) == null)
                    context.AddContextStateMult<DmgMult>(false, Numerator, Denominator);
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that modifies the damage multiplier of a skill category if affected by the specified status condition
    /// </summary>
    [Serializable]
    public class MultiplyCategoryInStatusEvent : BattleEvent
    {

        /// <summary>
        /// The status condition being checked for
        /// </summary>
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string StatusID;

        /// <summary>
        /// The skill category affected
        /// </summary>
        public BattleData.SkillCategory Category;

        /// <summary>
        /// The numerator of the modifier
        /// </summary>
        public int Numerator;

        /// <summary>
        /// The denominator of the modifier
        /// </summary>
        public int Denominator;

        /// <summary>
        /// Whether to affect the user or target
        /// </summary>
        public bool AffectTarget;

        public MultiplyCategoryInStatusEvent() { StatusID = ""; }
        public MultiplyCategoryInStatusEvent(string statusID, BattleData.SkillCategory category, int numerator, int denominator, bool affectTarget)
        {
            StatusID = statusID;
            Category = category;
            Numerator = numerator;
            Denominator = denominator;
            AffectTarget = affectTarget;
        }
        protected MultiplyCategoryInStatusEvent(MultiplyCategoryInStatusEvent other)
        {
            StatusID = other.StatusID;
            Category = other.Category;
            Numerator = other.Numerator;
            Denominator = other.Denominator;
            AffectTarget = other.AffectTarget;
        }
        public override GameEvent Clone() { return new MultiplyCategoryInStatusEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = (AffectTarget ? context.Target : context.User);

            if (context.Data.Category == Category || Category == BattleData.SkillCategory.None)
            {
                if (target.GetStatusEffect(StatusID) != null)
                    context.AddContextStateMult<DmgMult>(false, Numerator, Denominator);
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that modifies the damage multiplier
    /// </summary>
    [Serializable]
    public class MultiplyDamageEvent : BattleEvent
    {

        /// <summary>
        /// The numerator of the modifier
        /// </summary>
        public int Numerator;

        /// <summary>
        /// The denominator of the modifier
        /// </summary>
        public int Denominator;

        /// <summary>
        /// The list of battle events that will be applied
        /// </summary>
        public List<BattleEvent> Anims;

        public MultiplyDamageEvent()
        {
            Anims = new List<BattleEvent>();
        }
        public MultiplyDamageEvent(int numerator, int denominator)
        {
            Numerator = numerator;
            Denominator = denominator;
            Anims = new List<BattleEvent>();
        }
        public MultiplyDamageEvent(int numerator, int denominator, params BattleEvent[] anims)
        {
            Numerator = numerator;
            Denominator = denominator;

            Anims = new List<BattleEvent>();
            Anims.AddRange(anims);
        }
        protected MultiplyDamageEvent(MultiplyDamageEvent other)
        {
            Numerator = other.Numerator;
            Denominator = other.Denominator;

            Anims = new List<BattleEvent>();
            foreach (BattleEvent anim in other.Anims)
                Anims.Add((BattleEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new MultiplyDamageEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Data.Category == BattleData.SkillCategory.Physical || context.Data.Category == BattleData.SkillCategory.Magical)
            {
                foreach (BattleEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                context.AddContextStateMult<DmgMult>(false, Numerator, Denominator);
            }
        }
    }

    /// <summary>
    /// Event that modifies the damage multiplier if the user's gender is the same as the target
    /// </summary>
    [Serializable]
    public class RivalryEvent : BattleEvent
    {
        public override GameEvent Clone() { return new RivalryEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.User.CurrentForm.Gender == context.Target.CurrentForm.Gender)
                context.AddContextStateMult<DmgMult>(false, 5, 4);
            yield break;
        }
    }


    /// <summary>
    /// Event that boosts the damage multiplier based on moves used consecutively until a different move is used
    /// </summary>
    [Serializable]
    public class RepeatHitEvent : BattleEvent
    {

        /// <summary>
        /// The status that contains the last used move in IDState status state
        /// This should usually be "last_used_move"
        /// </summary>
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string LastMoveStatusID;

        /// <summary>
        /// The status that contains how times a move is used in the CountDownState status state
        /// This should usually be "times_move_used"
        /// </summary>
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string MoveRepeatStatusID;

        /// <summary>
        /// The maximum numerator of the move calculated by the denominator + how many times the same move is used
        /// </summary>
        public int Maximum;

        /// <summary>
        /// The denominator of the modifier
        /// </summary>
        public int Denominator;

        /// <summary>
        /// Whether the move must be used every turn in order to count
        /// </summary>
        public bool EveryTurn;

        public RepeatHitEvent() { LastMoveStatusID = ""; MoveRepeatStatusID = ""; }
        public RepeatHitEvent(string moveStatusID, string repeatStatusID, int maximum, int denominator, bool everyTurn)
        {
            LastMoveStatusID = moveStatusID;
            MoveRepeatStatusID = repeatStatusID;
            Maximum = maximum;
            Denominator = denominator;
            EveryTurn = everyTurn;
        }
        protected RepeatHitEvent(RepeatHitEvent other)
        {
            LastMoveStatusID = other.LastMoveStatusID;
            MoveRepeatStatusID = other.MoveRepeatStatusID;
            Maximum = other.Maximum;
            Denominator = other.Denominator;
            EveryTurn = other.EveryTurn;
        }
        public override GameEvent Clone() { return new RepeatHitEvent(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            //check that the last move used is equal to this move
            StatusEffect moveStatus = context.User.GetStatusEffect(LastMoveStatusID);
            StatusEffect repeatStatus = context.User.GetStatusEffect(MoveRepeatStatusID);
            if (moveStatus == null || repeatStatus == null)
                yield break;
            if (moveStatus.StatusStates.GetWithDefault<IDState>().ID != context.Data.ID)
                yield break;
            if (!repeatStatus.StatusStates.Contains<RecentState>())
                yield break;
            if (EveryTurn && repeatStatus.StatusStates.GetWithDefault<CountDownState>().Counter > 1)
                yield break;

            int repetitions = repeatStatus.StatusStates.GetWithDefault<CountState>().Count;
            context.AddContextStateMult<DmgMult>(false, Math.Min(Maximum, Denominator + repetitions), Denominator);
        }
    }

    /// <summary>
    /// Event that boosts moves with low base power
    /// </summary>
    [Serializable]
    public class TechnicianEvent : BattleEvent
    {
        public TechnicianEvent() { }
        public override GameEvent Clone() { return new TechnicianEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            BasePowerState basePower = context.Data.SkillStates.GetWithDefault<BasePowerState>();
            if (basePower != null && basePower.Power <= 40)
                context.AddContextStateMult<DmgMult>(false, 3, 2);
            yield break;
        }
    }

    /// <summary>
    /// Event that modifies the damage multiplier based on how effective the move is
    /// </summary>
    [Serializable]
    public class MultiplyEffectiveEvent : BattleEvent
    {

        /// <summary>
        /// Whether to check if the move is not effective instead
        /// </summary>
        public bool Reverse;

        /// <summary>
        /// The numerator of the modifier
        /// </summary>
        public int Numerator;

        /// <summary>
        /// The denominator of the modifier
        /// </summary>
        public int Denominator;

        /// <summary>
        /// The list of battle VFXs played if the move type matches
        /// </summary>
        public List<BattleAnimEvent> Anims;

        public MultiplyEffectiveEvent()
        {
            Anims = new List<BattleAnimEvent>();
        }
        public MultiplyEffectiveEvent(bool reverse, int numerator, int denominator)
        {
            Reverse = reverse;
            Numerator = numerator;
            Denominator = denominator;

            Anims = new List<BattleAnimEvent>();
        }
        public MultiplyEffectiveEvent(bool reverse, int numerator, int denominator, params BattleAnimEvent[] anims)
        {
            Reverse = reverse;
            Numerator = numerator;
            Denominator = denominator;

            Anims = new List<BattleAnimEvent>();
            Anims.AddRange(anims);
        }
        protected MultiplyEffectiveEvent(MultiplyEffectiveEvent other)
        {
            Reverse = other.Reverse;
            Numerator = other.Numerator;
            Denominator = other.Denominator;

            Anims = new List<BattleAnimEvent>();
            foreach (BattleAnimEvent anim in other.Anims)
                Anims.Add((BattleAnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new MultiplyEffectiveEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int typeMatchup = PreTypeEvent.GetDualEffectiveness(context.User, context.Target, context.Data);
            typeMatchup -= PreTypeEvent.NRM_2;
            if (Reverse)
                typeMatchup *= -1;
            if (typeMatchup > 0)
            {
                foreach (BattleAnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                context.AddContextStateMult<DmgMult>(false, Numerator, Denominator);
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that modifies the damage multiplier if the user's type matches the target's
    /// </summary> 
    [Serializable]
    public class SynchroTypeEvent : BattleEvent
    {
        public SynchroTypeEvent() { }
        public override GameEvent Clone() { return new SynchroTypeEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.User.Element1 != DataManager.Instance.DefaultElement && context.Target.HasElement(context.User.Element1)
                || context.User.Element2 != DataManager.Instance.DefaultElement && context.Target.HasElement(context.User.Element2))
            {

            }
            else
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_SYNCHRO_FAIL").ToLocal(), context.Target.GetDisplayName(false), context.User.GetDisplayName(false)));
                context.AddContextStateMult<DmgMult>(false, 1, 4);
            }
            yield break;
        }
    }


    /// <summary>
    /// Event that causes the move to deal no damage if the target is part of a explorer team
    /// </summary>
    [Serializable]
    public class ExplorerImmuneEvent : BattleEvent
    {
        public override GameEvent Clone() { return new ExplorerImmuneEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Target.MemberTeam is ExplorerTeam)
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_EXPLORER_IMMUNE").ToLocal(), context.Target.GetDisplayName(false)));
                context.AddContextStateMult<DmgMult>(false, -1, 1);
            }
            yield break;
        }
    }

    /// <summary>
    /// UNUSED
    /// Event that causes the move to deal no damage if the target's level is higher than the user
    /// </summary>
    [Serializable]
    public class HigherLevelImmuneEvent : BattleEvent
    {
        public override GameEvent Clone() { return new HigherLevelImmuneEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Target.Level > context.User.Level)
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_LEVEL_IMMUNE").ToLocal(), context.Target.GetDisplayName(false)));
                context.AddContextStateMult<DmgMult>(false, -1, 1);
            }
            yield break;
        }
    }

    /// <summary>
    /// UNUSED
    /// Event that causes a move to deal no damage if it's a OHKO move
    /// </summary>
    [Serializable]
    public class OHKOImmuneEvent : BattleEvent
    {
        public override GameEvent Clone() { return new OHKOImmuneEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            bool ohko = false;
            foreach (BattleEvent effect in context.Data.OnHits.EnumerateInOrder())
            {
                if (effect is OHKODamageEvent)
                {
                    ohko = true;
                    break;
                }
            }
            if (ohko)
                context.AddContextStateMult<DmgMult>(false, 0, 1);
            yield break;
        }
    }

    /// <summary>
    /// Event that modifies the damage multiplier for explosion splash damage
    /// </summary>
    [Serializable]
    public class BlastProofEvent : BattleEvent
    {
        /// <summary>
        /// Protects the target from explosion splash damage up to this many tiles away
        /// </summary>
        public int Range;

        /// <summary>
        /// Numerator of the modifier
        /// </summary>
        public int Numerator;

        /// <summary>
        /// Denominator of the modifier
        /// </summary>
        public int Denominator;

        /// <summary>
        /// The list of battle VFXs played if the condition is met
        /// </summary>
        public List<BattleAnimEvent> Anims;

        /// <summary>
        /// Whether to log the message if the condition is met
        /// </summary>
        public bool Msg;

        public BlastProofEvent() { Anims = new List<BattleAnimEvent>(); }
        public BlastProofEvent(int range, int numerator, int denominator, bool msg, params BattleAnimEvent[] anims)
        {
            Range = range;
            Numerator = numerator;
            Denominator = denominator;
            Msg = msg;
            Anims = new List<BattleAnimEvent>();
            Anims.AddRange(anims);
        }
        protected BlastProofEvent(BlastProofEvent other)
        {
            Range = other.Range;
            Numerator = other.Numerator;
            Denominator = other.Denominator;
            Msg = other.Msg;
            Anims = new List<BattleAnimEvent>();
            foreach (BattleAnimEvent anim in other.Anims)
                Anims.Add((BattleAnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new BlastProofEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            //only block explosions
            if (context.Explosion.Range == 0)
                yield break;

            //make sure to exempt round?

            if (!ZoneManager.Instance.CurrentMap.InRange(context.ExplosionTile, context.Target.CharLoc, Range - 1))
            {
                if (Msg)
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_PROTECT_WITH").ToLocal(), ownerChar.GetDisplayName(false), owner.GetDisplayName()));
                foreach (BattleAnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                context.AddContextStateMult<DmgMult>(false, Numerator, Denominator);
                if (Numerator > 0)
                    context.AddContextStateMult<HPDmgMult>(false, Numerator, Denominator);
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that decreases damage the further the target is.
    /// </summary>
    [Serializable]
    public class DistanceDropEvent : BattleEvent
    {
        public DistanceDropEvent() { }
        public override GameEvent Clone() { return new DistanceDropEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int diff = ZoneManager.Instance.CurrentMap.GetClosestDist8(context.StrikeStartTile, context.Target.CharLoc);
            for (int ii = 1; ii < diff; ii++)
                context.AddContextStateMult<DmgMult>(false, 1, 2);
            yield break;
        }
    }

    /// <summary>
    /// Event that boosts the battle action damage multiplier the further away the user is from the target
    /// </summary>
    [Serializable]
    public class TipPowerEvent : BattleEvent
    {
        public TipPowerEvent() { }
        public override GameEvent Clone() { return new TipPowerEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            //TODO: this breaks in small wrapped maps
            int diff = ZoneManager.Instance.CurrentMap.GetClosestDist8(context.StrikeStartTile, context.Target.CharLoc);
            for (int ii = 0; ii < diff; ii++)
                context.AddContextStateMult<DmgMult>(false, 2, 1);
            yield break;
        }
    }


    /// <summary>
    /// Event that modifies the damage multiplier if the character is at full HP
    /// </summary>
    [Serializable]
    public class MultiScaleEvent : BattleEvent
    {
        /// <summary>
        /// The list of battle VFXs played if the condition is met
        /// </summary>
        public List<BattleAnimEvent> Anims;

        public MultiScaleEvent() { Anims = new List<BattleAnimEvent>(); }
        public MultiScaleEvent(params BattleAnimEvent[] anims)
        {
            Anims = new List<BattleAnimEvent>();
            Anims.AddRange(anims);
        }
        protected MultiScaleEvent(MultiScaleEvent other)
        {
            Anims = new List<BattleAnimEvent>();
            foreach (BattleAnimEvent anim in other.Anims)
                Anims.Add((BattleAnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new MultiScaleEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Target.HP == context.Target.MaxHP &&
                (context.Data.Category == BattleData.SkillCategory.Physical || context.Data.Category == BattleData.SkillCategory.Magical))
            {
                foreach (BattleAnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                context.AddContextStateMult<DmgMult>(false, 1, 2);
            }
            yield break;
        }
    }


    /// <summary>
    /// Event that modifies the damage multplier if the user has the specified status condition.
    /// Intended for a "missed last turn" status
    /// </summary>
    [Serializable]
    public class MultWhenMissEvent : BattleEvent
    {

        /// <summary>
        /// The status condition being checked for
        /// </summary>
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string StatusID;

        /// <summary>
        /// Numerator of the modifier
        /// </summary>
        public int Numerator;

        /// <summary>
        /// Denominator of the modifier
        /// </summary>
        public int Denominator;

        public MultWhenMissEvent() { StatusID = ""; }
        public MultWhenMissEvent(string statusID, int numerator, int denominator)
        {
            StatusID = statusID;
            Numerator = numerator;
            Denominator = denominator;
        }
        protected MultWhenMissEvent(MultWhenMissEvent other)
        {
            StatusID = other.StatusID;
            Numerator = other.Numerator;
            Denominator = other.Denominator;
        }
        public override GameEvent Clone() { return new MultWhenMissEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.User.GetStatusEffect(StatusID) != null && DungeonScene.Instance.GetMatchup(context.User, context.Target) == Alignment.Foe)
            {
                context.AddContextStateMult<DmgMult>(false, Numerator, Denominator);
            }
            yield break;
        }
    }




    /// <summary>
    /// Event that causes the battle action to deal no damage if the character doesn't have the specified status
    /// </summary>
    [Serializable]
    public class TargetStatusNeededEvent : BattleEvent
    {
        /// <summary>
        /// The status ID to check for
        /// </summary>
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string StatusID;

        /// <summary>
        /// Whether to check the status on the target or user 
        /// </summary>
        public bool AffectTarget;

        /// <summary>
        /// The message displayed in the dungeon log if the conditon is met 
        /// </summary> 
        public StringKey Message;

        public TargetStatusNeededEvent() { StatusID = ""; }
        public TargetStatusNeededEvent(string statusID, bool affectTarget, StringKey msg)
        {
            StatusID = statusID;
            AffectTarget = affectTarget;
            Message = msg;
        }
        protected TargetStatusNeededEvent(TargetStatusNeededEvent other)
        {
            StatusID = other.StatusID;
            AffectTarget = other.AffectTarget;
            Message = other.Message;
        }
        public override GameEvent Clone() { return new TargetStatusNeededEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = (AffectTarget ? context.Target : context.User);

            if (target.GetStatusEffect(StatusID) == null)
            {
                context.AddContextStateMult<DmgMult>(false, -1, 1);
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), target.GetDisplayName(false)));
            }
            yield break;
        }
    }


    /// <summary>
    /// Event that reduces the damage of a different type depending on the held item 
    /// </summary>
    [Serializable]
    public class PlateProtectEvent : BattleEvent
    {
        /// <summary>
        /// The item mapped to a type
        /// </summary>
        [JsonConverter(typeof(ElementItemDictConverter))]
        [DataType(1, DataManager.DataType.Element, false)]
        [DataType(2, DataManager.DataType.Item, false)]
        public Dictionary<string, string> TypePair;

        public PlateProtectEvent() { TypePair = new Dictionary<string, string>(); }
        public PlateProtectEvent(Dictionary<string, string> weather)
        {
            TypePair = weather;
        }
        protected PlateProtectEvent(PlateProtectEvent other)
            : this()
        {
            foreach (string element in other.TypePair.Keys)
                TypePair.Add(element, other.TypePair[element]);
        }
        public override GameEvent Clone() { return new PlateProtectEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Target.MemberTeam is ExplorerTeam)
            {
                string reqItem;
                if (TypePair.TryGetValue(context.Data.Element, out reqItem))
                {
                    //make sure not already protected
                    if (reqItem != context.Target.EquippedItem.ID)
                    {
                        //search the bag for the item
                        ExplorerTeam team = (ExplorerTeam)context.Target.MemberTeam;
                        for (int ii = 0; ii < team.GetInvCount(); ii++)
                        {
                            if (team.GetInv(ii).ID == reqItem && !team.GetInv(ii).Cursed)
                            {
                                context.AddContextStateMult<DmgMult>(false, 1, 2);
                                yield break;
                            }
                        }
                    }
                }
            }
        }
    }


    /// <summary>
    /// Event that sets the rate in the AdditionalEffectState skill state to 0 and boosts the damage multiplier
    /// </summary>
    [Serializable]
    public class SheerForceEvent : BattleEvent
    {
        public override GameEvent Clone() { return new BlockAdditionalEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            AdditionalEffectState state = ((BattleData)context.Data).SkillStates.GetWithDefault<AdditionalEffectState>();
            if (state != null)
            {
                state.EffectChance = 0;
                context.AddContextStateMult<DmgMult>(false, 4, 3);
            }
            yield break;
        }
    }

}

