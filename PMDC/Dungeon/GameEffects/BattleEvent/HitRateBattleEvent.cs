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
    // Battle events that modify the hit rate of the attack, including forced misses

    /// <summary>
    /// Event that protects the user from all moves
    /// </summary>
    [Serializable]
    public class ProtectEvent : BattleEvent
    {
        /// <summary>
        /// OBSOLETE
        /// </summary>
        [NonEdited]
        public List<BattleAnimEvent> Anims;

        /// <summary>
        /// The list of battle events applied if the condition is met
        /// </summary>
        public List<BattleEvent> Effects;

        public ProtectEvent()
        {
            Effects = new List<BattleEvent>();
        }
        public ProtectEvent(params BattleEvent[] anims)
        {
            Effects = new List<BattleEvent>();
            Effects.AddRange(anims);
        }
        protected ProtectEvent(ProtectEvent other)
        {
            Effects = new List<BattleEvent>();
            foreach (BattleEvent anim in other.Effects)
                Effects.Add((BattleEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new ProtectEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.User != context.Target)
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_PROTECT").ToLocal(), context.Target.GetDisplayName(false)));

                foreach (BattleEvent anim in Effects)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                context.AddContextStateMult<AccMult>(false, -1, 1);
            }
            yield break;
        }

        [OnDeserialized]
        internal void OnDeserializedMethod(StreamingContext context)
        {
            //TODO: remove on v1.1
            if (Serializer.OldVersion < new Version(0, 7, 15) && Anims != null)
            {
                Effects = new List<BattleEvent>();
                Effects.AddRange(Anims);
            }
        }
    }

    /// <summary>
    /// Event that causes the specified list of move to always hit while all other moves will miss.
    /// </summary>
    [Serializable]
    public class SemiInvulEvent : BattleEvent
    {
        /// <summary>
        /// The list of valid of moves that will always hit
        /// </summary>
        [JsonConverter(typeof(SkillArrayConverter))]
        [DataType(1, DataManager.DataType.Skill, false)]
        public string[] ExceptionMoves;

        public SemiInvulEvent()
        {
            ExceptionMoves = new string[0];
        }
        public SemiInvulEvent(string[] exceptionMoves)
        {
            ExceptionMoves = exceptionMoves;
        }
        protected SemiInvulEvent(SemiInvulEvent other)
        {
            ExceptionMoves = new string[other.ExceptionMoves.Length];
            other.ExceptionMoves.CopyTo(ExceptionMoves, 0);
        }
        public override GameEvent Clone() { return new SemiInvulEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Skill)
            {
                for (int ii = 0; ii < ExceptionMoves.Length; ii++)
                {
                    if (context.Data.ID == ExceptionMoves[ii])
                    {
                        context.Data.HitRate = -1;
                        yield break;
                    }
                }
            }
            context.AddContextStateMult<AccMult>(false, 0, 1);
        }
    }


    /// <summary>
    /// Event that makes the user cannot target the enemy that used the status
    /// This event can only be used in statuses
    /// </summary>
    [Serializable]
    public class CantAttackTargetEvent : BattleEvent
    {
        /// <summary>
        /// Whether to force the user to target the enemy instead
        /// </summary> 
        public bool Invert;

        /// <summary>
        /// The message displayed in the dungeon log if the condition is met
        /// </summary> 
        [StringKey(0, true)]
        public StringKey Message;

        public CantAttackTargetEvent() { }
        public CantAttackTargetEvent(bool invert, StringKey message)
        {
            Invert = invert;
            Message = message;
        }
        protected CantAttackTargetEvent(CantAttackTargetEvent other)
        {
            Invert = other.Invert;
            Message = other.Message;
        }
        public override GameEvent Clone() { return new CantAttackTargetEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Target != null && ((StatusEffect)owner).TargetChar != null && context.Target != context.User)
            {
                if ((((StatusEffect)owner).TargetChar == context.Target) != Invert)
                {
                    if (Message.IsValid())
                        DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), context.User.GetDisplayName(false), ((StatusEffect)owner).TargetChar.GetDisplayName(false)));
                    context.AddContextStateMult<AccMult>(false, -1, 1);
                }
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that causes the move to miss if the target has the specified status condition
    /// </summary>
    [Serializable]
    public class EvadeInStatusEvent : BattleEvent
    {
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string StatusID;

        public EvadeInStatusEvent() { StatusID = ""; }
        public EvadeInStatusEvent(string statusID)
        {
            StatusID = statusID;
        }
        protected EvadeInStatusEvent(EvadeInStatusEvent other)
        {
            StatusID = other.StatusID;
        }
        public override GameEvent Clone() { return new EvadeInStatusEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Skill && context.Data.ID != DataManager.Instance.DefaultSkill)
            {
                if (context.Target.GetStatusEffect(StatusID) != null && DungeonScene.Instance.GetMatchup(context.User, context.Target) == Alignment.Foe)
                {
                    if (!ZoneManager.Instance.CurrentMap.InRange(context.StrikeStartTile, context.Target.CharLoc, 1) && context.Data.HitRate > -1)
                    {
                        DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_AVOID").ToLocal(), context.Target.GetDisplayName(false), owner.GetDisplayName()));
                        context.AddContextStateMult<AccMult>(false, -1, 1);
                    }
                }
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that causes the move to miss if the user uses their strongest base power move
    /// </summary>
    [Serializable]
    public class EvadeStrongestEvent : BattleEvent
    {
        public override GameEvent Clone() { return new EvadeStrongestEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Skill && context.UsageSlot > BattleContext.DEFAULT_ATTACK_SLOT && context.UsageSlot < CharData.MAX_SKILL_SLOTS && DungeonScene.Instance.GetMatchup(context.User, context.Target) == Alignment.Foe)
            {
                int recordSlot = -1;
                int recordPower = -1;
                for (int ii = 0; ii < context.User.Skills.Count; ii++)
                {
                    if (!String.IsNullOrEmpty(context.User.Skills[ii].Element.SkillNum))
                    {
                        SkillData entry = DataManager.Instance.GetSkill(context.User.Skills[ii].Element.SkillNum);

                        int basePower = 0;
                        if (entry.Data.Category == BattleData.SkillCategory.Status)
                            basePower = -1;
                        else
                        {
                            BasePowerState state = entry.Data.SkillStates.GetWithDefault<BasePowerState>();
                            if (state != null)
                                basePower = state.Power;
                        }
                        if (basePower > recordPower)
                        {
                            recordSlot = ii;
                            recordPower = basePower;
                        }
                    }
                }

                if (context.UsageSlot == recordSlot && context.Data.HitRate > -1)
                {
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_AVOID").ToLocal(), ownerChar.GetDisplayName(false), owner.GetDisplayName()));
                    context.AddContextStateMult<AccMult>(false, -1, 1);
                }
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that causes the user's strongest super-effective move to miss
    /// </summary>
    [Serializable]
    public class EvadeStrongestEffectiveEvent : BattleEvent
    {
        public override GameEvent Clone() { return new EvadeStrongestEffectiveEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Skill && context.UsageSlot > BattleContext.DEFAULT_ATTACK_SLOT && context.UsageSlot < CharData.MAX_SKILL_SLOTS && DungeonScene.Instance.GetMatchup(context.User, context.Target) == Alignment.Foe)
            {
                int recordSlot = -1;
                int recordPower = -1;
                for (int ii = 0; ii < context.User.Skills.Count; ii++)
                {
                    if (!String.IsNullOrEmpty(context.User.Skills[ii].Element.SkillNum))
                    {
                        SkillData entry = DataManager.Instance.GetSkill(context.User.Skills[ii].Element.SkillNum);

                        int typeMatchup = PreTypeEvent.GetDualEffectiveness(context.User, context.Target, entry.Data);

                        if (typeMatchup > PreTypeEvent.NRM_2)
                        {
                            int basePower = 0;
                            if (entry.Data.Category == BattleData.SkillCategory.Status)
                                basePower = -1;
                            else
                            {
                                BasePowerState state = entry.Data.SkillStates.GetWithDefault<BasePowerState>();
                                if (state != null)
                                    basePower = state.Power;
                            }
                            if (basePower > recordPower)
                            {
                                recordSlot = ii;
                                recordPower = basePower;
                            }
                        }
                    }
                }

                if (context.UsageSlot == recordSlot && context.Data.HitRate > -1)
                {
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_AVOID").ToLocal(), ownerChar.GetDisplayName(false), owner.GetDisplayName()));
                    context.AddContextStateMult<AccMult>(false, -1, 1);
                }
            }
            yield break;
        }
    }


    /// <summary>
    /// UNUSED
    /// Event that causes the move to miss if the target is not at full HP
    /// </summary>
    [Serializable]
    public class FullHPNeededEvent : BattleEvent
    {
        public override GameEvent Clone() { return new FullHPNeededEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Target.HP < context.Target.MaxHP)
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_FULL_HP_REQ").ToLocal(), context.Target.GetDisplayName(false)));
                context.AddContextStateMult<AccMult>(false, -1, 1);
            }
            yield break;
        }
    }


    /// <summary>
    /// Event that causes the user's move to miss if it contains one of the specified SkillStates
    /// </summary>
    [Serializable]
    public class EvadeMoveStateEvent : BattleEvent
    {
        /// <summary>
        /// The list of valid SkillStates types
        /// </summary>
        [StringTypeConstraint(1, typeof(SkillState))]
        public List<FlagType> States;

        /// <summary>
        /// The list of battle VFXs played if the move type matches
        /// </summary>
        public List<BattleAnimEvent> Anims;

        public EvadeMoveStateEvent()
        {
            States = new List<FlagType>();
            Anims = new List<BattleAnimEvent>();
        }
        public EvadeMoveStateEvent(Type state, params BattleAnimEvent[] anims) : this()
        {
            States.Add(new FlagType(state));
            Anims.AddRange(anims);
        }
        protected EvadeMoveStateEvent(EvadeMoveStateEvent other) : this()
        {
            States.AddRange(other.States);
            foreach (BattleAnimEvent anim in other.Anims)
                Anims.Add((BattleAnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new EvadeMoveStateEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            bool hasState = false;
            foreach (FlagType state in States)
            {
                if (context.Data.SkillStates.Contains(state.FullType))
                    hasState = true;
            }
            if (hasState)
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_PROTECT_WITH").ToLocal(), ownerChar.GetDisplayName(false), owner.GetDisplayName()));

                foreach (BattleAnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                context.AddContextStateMult<AccMult>(false, -1, 1);
            }
            yield break;
        }

    }

    /// <summary>
    /// Event that causes the user's move to miss if the target is more than 1 tile away
    /// </summary>
    [Serializable]
    public class EvadeDistanceEvent : BattleEvent
    {
        /// <summary>
        /// Whether to check if the user is within 1 tile
        /// </summary>
        public bool Inverted;

        public EvadeDistanceEvent() { }
        public EvadeDistanceEvent(bool invert) { Inverted = invert; }
        public EvadeDistanceEvent(EvadeDistanceEvent other)
        {
            Inverted = other.Inverted;
        }

        public override GameEvent Clone() { return new EvadeDistanceEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Skill && context.Data.ID != DataManager.Instance.DefaultSkill)
            {
                if (ZoneManager.Instance.CurrentMap.InRange(context.StrikeStartTile, context.Target.CharLoc, 1) == Inverted && context.Data.HitRate > -1)
                {
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_AVOID").ToLocal(), context.Target.GetDisplayName(false), owner.GetDisplayName()));
                    context.AddContextStateMult<AccMult>(false, -1, 1);
                }
            }
            yield break;
        }
    }


    /// <summary>
    /// Event that modifies the accuracy rate if the target has the specified status condition
    /// </summary>
    [Serializable]
    public class EvasiveWhenMissEvent : BattleEvent
    {
        /// <summary>
        /// The status condition being checked for
        /// </summary>
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string StatusID;

        public EvasiveWhenMissEvent() { StatusID = ""; }
        public EvasiveWhenMissEvent(string statusID)
        {
            StatusID = statusID;
        }
        protected EvasiveWhenMissEvent(EvasiveWhenMissEvent other)
        {
            StatusID = other.StatusID;
        }
        public override GameEvent Clone() { return new EvasiveWhenMissEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Target.GetStatusEffect(StatusID) != null && DungeonScene.Instance.GetMatchup(context.User, context.Target) == Alignment.Foe)
            {
                context.AddContextStateMult<AccMult>(false, 2, 3);
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that modifies the accuracy rate if the target is below the specified HP threshold
    /// </summary>
    [Serializable]
    public class EvasiveInPinchEvent : BattleEvent
    {
        public override GameEvent Clone() { return new EvasiveInPinchEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (DungeonScene.Instance.GetMatchup(context.User, context.Target) == Alignment.Foe)
            {
                if (context.Target.HP < context.Target.MaxHP / 3)
                {
                    context.AddContextStateMult<AccMult>(false, 1, 3);
                }
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that decreases the accuracy rate the further away the distance of the action
    /// </summary>
    [Serializable]
    public class EvasiveInDistanceEvent : BattleEvent
    {
        public override GameEvent Clone() { return new EvasiveInDistanceEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Skill && context.Data.ID != DataManager.Instance.DefaultSkill)
            {
                if (DungeonScene.Instance.GetMatchup(context.User, context.Target) == Alignment.Foe)
                {
                    int diff = ZoneManager.Instance.CurrentMap.GetClosestDist8(context.StrikeStartTile, context.Target.CharLoc);
                    if (diff > 1)
                        context.AddContextStateMult<AccMult>(false, 4, 3 + diff);
                }
            }
            yield break;
        }
    }


    /// <summary>
    /// Event that decreases the accuracy rate at point blank
    /// </summary>
    [Serializable]
    public class EvasiveCloseUpEvent : BattleEvent
    {
        public override GameEvent Clone() { return new EvasiveCloseUpEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Skill && context.Data.ID != DataManager.Instance.DefaultSkill)
            {
                if (DungeonScene.Instance.GetMatchup(context.User, context.Target) == Alignment.Foe)
                {
                    if (ZoneManager.Instance.CurrentMap.InRange(context.StrikeStartTile, context.Target.CharLoc, 1))
                        context.AddContextStateMult<AccMult>(false, 1, 2);
                }
            }
            yield break;
        }
    }


    /// <summary>
    /// Event that causes the action to miss given the specified chance
    /// </summary>
    [Serializable]
    public class CustomHitRateEvent : BattleEvent
    {
        /// <summary>
        /// The numerator of the chance
        /// </summary>
        public int Numerator;

        /// <summary>
        /// The denominator of the chance
        /// </summary>
        public int Denominator;

        public CustomHitRateEvent()
        { }
        public CustomHitRateEvent(int numerator, int denominator)
        {
            Numerator = numerator;
            Denominator = denominator;
        }
        protected CustomHitRateEvent(CustomHitRateEvent other) : this()
        {
            Numerator = other.Numerator;
            Denominator = other.Denominator;
        }
        public override GameEvent Clone() { return new CustomHitRateEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.Data.HitRate > -1)
            {
                if (DataManager.Instance.Save.Rand.Next(0, Denominator) < Numerator)
                {
                    context.Data.HitRate = -1;
                }
                else
                {
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_MISS").ToLocal(), context.Target.GetDisplayName(false)));
                    context.AddContextStateMult<AccMult>(false, -1, 1);
                }
            }
            yield break;
        }
    }




    /// <summary>
    /// Event that causes the action to always hit
    /// </summary>
    [Serializable]
    public class SureShotEvent : BattleEvent
    {
        public override GameEvent Clone() { return new SureShotEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            context.Data.HitRate = -1;
            yield break;
        }
    }


    /// <summary>
    /// Event that causes the multi-strike moves to always hit
    /// </summary>
    [Serializable]
    public class SkillLinkEvent : BattleEvent
    {
        public override GameEvent Clone() { return new SkillLinkEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Skill && context.Strikes > 1)
                context.Data.HitRate = -1;
            yield break;
        }
    }


    /// <summary>
    /// Event that causes the user to avoid moves of the specified skill category and alignment
    /// </summary>
    [Serializable]
    public class EvadeCategoryEvent : BattleEvent
    {

        /// <summary>
        /// The affected alignments
        /// </summary>
        public Alignment EvadeAlignment;

        /// <summary>
        /// The affected skill category
        /// </summary> 
        public BattleData.SkillCategory Category;

        /// <summary>
        /// The list of battle VFXs played if the condition is met
        /// </summary>
        public List<BattleAnimEvent> Anims;

        public EvadeCategoryEvent()
        {
            Anims = new List<BattleAnimEvent>();
        }
        public EvadeCategoryEvent(Alignment alignment, BattleData.SkillCategory category, params BattleAnimEvent[] anims)
        {
            EvadeAlignment = alignment;
            Category = category;

            Anims = new List<BattleAnimEvent>();
            Anims.AddRange(anims);
        }
        protected EvadeCategoryEvent(EvadeCategoryEvent other)
        {
            EvadeAlignment = other.EvadeAlignment;
            Category = other.Category;
            Anims = new List<BattleAnimEvent>();
            foreach (BattleAnimEvent anim in other.Anims)
                Anims.Add((BattleAnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new EvadeCategoryEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (((DungeonScene.Instance.GetMatchup(context.User, context.Target) | EvadeAlignment) == EvadeAlignment) && context.Data.Category == Category)
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_PROTECT_WITH").ToLocal(), ownerChar.GetDisplayName(false), owner.GetDisplayName()));

                foreach (BattleAnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                context.AddContextStateMult<AccMult>(false, -1, 1);
            }
            yield break;
        }
    }


    /// <summary>
    /// Event that causes the user to avoid damaging moves of friendly targets
    /// </summary>
    [Serializable]
    public class TelepathyEvent : BattleEvent
    {
        public override GameEvent Clone() { return new TelepathyEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            BasePowerState basePower = context.Data.SkillStates.GetWithDefault<BasePowerState>();
            if (basePower != null && DungeonScene.Instance.GetMatchup(context.User, context.Target) == Alignment.Friend)
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_AVOID").ToLocal(), ownerChar.GetDisplayName(false), owner.GetDisplayName()));
                context.AddContextStateMult<AccMult>(false, -1, 1);
            }
            yield break;
        }
    }


    /// <summary>
    /// Event that modifies the accuracy rate
    /// </summary>
    [Serializable]
    public class MultiplyAccuracyEvent : BattleEvent
    {
        public int Numerator;
        public int Denominator;

        public MultiplyAccuracyEvent() { }
        public MultiplyAccuracyEvent(int numerator, int denominator)
        {
            Numerator = numerator;
            Denominator = denominator;
        }
        protected MultiplyAccuracyEvent(MultiplyAccuracyEvent other)
        {
            Numerator = other.Numerator;
            Denominator = other.Denominator;
        }
        public override GameEvent Clone() { return new MultiplyAccuracyEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            context.AddContextStateMult<AccMult>(false, Numerator, Denominator);
            yield break;
        }
    }


    /// <summary>
    /// Event that causes the move to miss if the range of the move is executed from a distance greater than the specified amount
    /// </summary>
    [Serializable]
    public class DistantGuardEvent : BattleEvent
    {
        /// <summary>
        /// Attacks greater than this distances will be blocked.
        /// </summary>
        public int Distance;

        /// <summary>
        /// The list of battle events that will be applied
        /// </summary>
        public List<BattleAnimEvent> Anims;

        public DistantGuardEvent()
        {
            Anims = new List<BattleAnimEvent>();
        }
        public DistantGuardEvent(int distance, params BattleAnimEvent[] anims)
        {
            Distance = distance;
            Anims = new List<BattleAnimEvent>();
            Anims.AddRange(anims);
        }
        protected DistantGuardEvent(DistantGuardEvent other)
        {
            Distance = other.Distance;
            Anims = new List<BattleAnimEvent>();
            foreach (BattleAnimEvent anim in other.Anims)
                Anims.Add((BattleAnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new DistantGuardEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Skill && context.Data.ID != DataManager.Instance.DefaultSkill)
            {
                if (!ZoneManager.Instance.CurrentMap.InRange(context.StrikeStartTile, context.Target.CharLoc, Distance))
                {
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_PROTECT_WITH").ToLocal(), context.Target.GetDisplayName(false), owner.GetDisplayName()));

                    foreach (BattleAnimEvent anim in Anims)
                        yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                    context.AddContextStateMult<AccMult>(false, -1, 1);
                }
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that causes the move to miss if the range of the move is greater than the specified amount
    /// </summary>
    [Serializable]
    public class LongRangeGuardEvent : BattleEvent
    {
        public List<BattleAnimEvent> Anims;

        public LongRangeGuardEvent()
        {
            Anims = new List<BattleAnimEvent>();
        }
        public LongRangeGuardEvent(params BattleAnimEvent[] anims)
        {
            Anims = new List<BattleAnimEvent>();
            Anims.AddRange(anims);
        }
        protected LongRangeGuardEvent(LongRangeGuardEvent other)
        {
            Anims = new List<BattleAnimEvent>();
            foreach (BattleAnimEvent anim in other.Anims)
                Anims.Add((BattleAnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new LongRangeGuardEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.User != context.Target && context.HitboxAction.GetEffectiveDistance() > 2)
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_PROTECT_WITH").ToLocal(), context.Target.GetDisplayName(false), owner.GetDisplayName()));

                foreach (BattleAnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                context.AddContextStateMult<AccMult>(false, -1, 1);
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that causes the move to miss if the move is wide or an explosion
    /// </summary>
    [Serializable]
    public class WideGuardEvent : BattleEvent
    {

        /// <summary>
        /// The list of battle VFXs played if the condition is met
        /// </summary>
        public List<BattleAnimEvent> Anims;

        public WideGuardEvent()
        {
            Anims = new List<BattleAnimEvent>();
        }
        public WideGuardEvent(params BattleAnimEvent[] anims)
        {
            Anims = new List<BattleAnimEvent>();
            Anims.AddRange(anims);
        }
        protected WideGuardEvent(WideGuardEvent other)
        {
            Anims = new List<BattleAnimEvent>();
            foreach (BattleAnimEvent anim in other.Anims)
                Anims.Add((BattleAnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new WideGuardEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.User != context.Target && (context.HitboxAction.IsWide() || context.Explosion.Range > 0))
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_PROTECT_WITH").ToLocal(), context.Target.GetDisplayName(false), owner.GetDisplayName()));

                foreach (BattleAnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                context.AddContextStateMult<AccMult>(false, -1, 1);
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that only allows super-effective moves to hit
    /// </summary>
    [Serializable]
    public class WonderGuardEvent : BattleEvent
    {
        /// <summary>
        /// The list of battle VFXs played if the move type matches
        /// </summary>
        public List<BattleAnimEvent> Anims;

        public WonderGuardEvent()
        {
            Anims = new List<BattleAnimEvent>();
        }
        public WonderGuardEvent(params BattleAnimEvent[] anims)
        {
            Anims = new List<BattleAnimEvent>();
            Anims.AddRange(anims);
        }
        protected WonderGuardEvent(WonderGuardEvent other)
        {
            Anims = new List<BattleAnimEvent>();
            foreach (BattleAnimEvent anim in other.Anims)
                Anims.Add((BattleAnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new WonderGuardEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            //typeless attacks bypass
            if (context.Data.Element == DataManager.Instance.DefaultElement)
                yield break;

            int typeMatchup = PreTypeEvent.GetDualEffectiveness(context.User, context.Target, context.Data);
            if (typeMatchup <= PreTypeEvent.NRM_2 && (context.Data.Category == BattleData.SkillCategory.Physical || context.Data.Category == BattleData.SkillCategory.Magical))
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_PROTECT_WITH").ToLocal(), ownerChar.GetDisplayName(false), owner.GetDisplayName()));

                foreach (BattleAnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                context.AddContextStateMult<AccMult>(false, -1, 1);
            }
            yield break;
        }
    }


    /// <summary>
    /// Event that causes the battle action to miss if the attacker isn't due for a sure hit.
    /// </summary>
    [Serializable]
    public class EvadeIfPossibleEvent : BattleEvent
    {
        public EvadeIfPossibleEvent() { }
        public override GameEvent Clone() { return new EvadeIfPossibleEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (!context.User.MustHitNext)
                context.AddContextStateMult<AccMult>(false, 0, 1);
            yield break;
        }
    }


    /// <summary>
    /// Event that makes the move never miss and always land a critical hit if all moves have the same PP
    /// </summary>
    [Serializable]
    public class BetterOddsEvent : BattleEvent
    {
        public override GameEvent Clone() { return new BetterOddsEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Skill && context.UsageSlot > BattleContext.DEFAULT_ATTACK_SLOT && context.UsageSlot < CharData.MAX_SKILL_SLOTS)
            {
                Skill baseMove = context.User.Skills[context.UsageSlot].Element;
                bool allEqual = true;
                for (int ii = 0; ii < context.User.Skills.Count; ii++)
                {
                    if (ii == context.UsageSlot)
                        continue;
                    Skill move = context.User.Skills[ii].Element;
                    if (String.IsNullOrEmpty(move.SkillNum))
                        continue;
                    if (move.Charges != baseMove.Charges + 1)
                    {
                        allEqual = false;
                        break;
                    }

                }
                if (allEqual)
                {
                    context.Data.HitRate = -1;
                    context.AddContextStateInt<CritLevel>(4);
                }
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that makes the move never miss and always land a critical hit if the move is on its last PP
    /// </summary>
    [Serializable]
    public class FinalOddsEvent : BattleEvent
    {
        public override GameEvent Clone() { return new FinalOddsEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Skill && context.UsageSlot > BattleContext.DEFAULT_ATTACK_SLOT && context.UsageSlot < CharData.MAX_SKILL_SLOTS)
            {
                Skill move = context.User.Skills[context.UsageSlot].Element;
                if (!String.IsNullOrEmpty(move.SkillNum) && move.Charges == 0)
                {
                    context.Data.HitRate = -1;
                    context.AddContextStateInt<CritLevel>(4);
                }
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that sets the accuracy of the move
    /// </summary>
    [Serializable]
    public class SetAccuracyEvent : BattleEvent
    {

        /// <summary>
        /// The new accuracy
        /// </summary>
        public int Accuracy;

        public SetAccuracyEvent() { }
        public SetAccuracyEvent(int accuracy)
        {
            Accuracy = accuracy;
        }
        protected SetAccuracyEvent(SetAccuracyEvent other)
        {
            Accuracy = other.Accuracy;
        }
        public override GameEvent Clone() { return new SetAccuracyEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            context.Data.HitRate = Accuracy;
            yield break;
        }
    }



    /// <summary>
    /// Event that causes the battle action to miss if it isn't used at max distance.
    /// </summary>
    [Serializable]
    public class TipOnlyEvent : BattleEvent
    {
        public TipOnlyEvent() { }
        public override GameEvent Clone() { return new TipOnlyEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            //TODO: this breaks in small wrapped maps
            int diff = ZoneManager.Instance.CurrentMap.GetClosestDist8(context.StrikeStartTile, context.Target.CharLoc);
            if (diff != context.HitboxAction.GetEffectiveDistance())
                context.AddContextStateMult<AccMult>(false, 0, 1);
            yield break;
        }
    }

    /// <summary>
    /// Event that causes the battle action to miss if the user is the next to the target.
    /// </summary>
    [Serializable]
    public class DistanceOnlyEvent : BattleEvent
    {
        public DistanceOnlyEvent() { }
        public override GameEvent Clone() { return new DistanceOnlyEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (ZoneManager.Instance.CurrentMap.InRange(context.StrikeStartTile, context.Target.CharLoc, 1))
                context.AddContextStateMult<AccMult>(false, 0, 1);
            yield break;
        }
    }


    /// <summary>
    /// Event that causes the character to dodge items that contain the EdibleState item state
    /// </summary>
    [Serializable]
    public class DodgeFoodEvent : BattleEvent
    {
        /// <summary>
        /// The message displayed in the dungeon log if the condition is met
        /// </summary>
        public StringKey Message;

        public DodgeFoodEvent() { }
        public DodgeFoodEvent(StringKey message)
        {
            Message = message;
        }
        protected DodgeFoodEvent(DodgeFoodEvent other)
        {
            Message = other.Message;
        }
        public override GameEvent Clone() { return new DodgeFoodEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType == BattleActionType.Item || context.ActionType == BattleActionType.Throw)
            {
                ItemData entry = DataManager.Instance.GetItem(context.Item.ID);
                if (entry.ItemStates.Contains<EdibleState>())
                {
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(Message.ToLocal(), context.Target.GetDisplayName(false)));
                    context.AddContextStateMult<AccMult>(false, -1, 1);
                }
            }
            yield break;
        }
    }

}

