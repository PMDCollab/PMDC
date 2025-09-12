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
