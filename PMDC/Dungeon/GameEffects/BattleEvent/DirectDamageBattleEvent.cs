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
    // Battle events the handle direct damage

    [Serializable]
    public abstract class DirectDamageEvent : BattleEvent
    {
        protected IEnumerator<YieldInstruction> InflictDamage(BattleContext context, int dmg)
        {
            bool fastSpeed = (DiagManager.Instance.CurSettings.BattleFlow > Settings.BattleSpeed.Fast);
            bool hasEffect = (context.Data.HitFX.Delay == 0 && context.Data.HitFX.Sound != "");//determines if a sound plays at the same frame the move hits

            if (hasEffect && fastSpeed)
            {

            }
            else
            {
                if (hasEffect)
                    yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(10, context.Target.CharLoc));
                int typeMatchup = PreTypeEvent.GetDualEffectiveness(context.User, context.Target, context.Data);

                SingleEmitter endEmitter = null;
                if (typeMatchup == PreTypeEvent.NRM_2 || fastSpeed)
                {
                    GameManager.Instance.BattleSE("DUN_Hit_Neutral");
                    endEmitter = new SingleEmitter(new AnimData("Hit_Neutral", 3));
                }
                else if (typeMatchup >= PreTypeEvent.S_E_2)
                {
                    GameManager.Instance.BattleSE("DUN_Hit_Super_Effective");
                    endEmitter = new SingleEmitter(new AnimData("Hit_Super_Effective", 3));
                }
                else
                {
                    GameManager.Instance.BattleSE("DUN_Hit_NVE");
                    endEmitter = new SingleEmitter(new AnimData("Hit_Neutral", 3));
                }

                if (!context.Target.Unidentifiable)
                {
                    endEmitter.SetupEmit(context.Target.MapLoc, context.User.MapLoc, context.Target.CharDir);
                    DungeonScene.Instance.CreateAnim(endEmitter, DrawLayer.NoDraw);
                }
            }

            bool endure = context.ContextStates.Contains<AttackEndure>();
            yield return CoroutineManager.Instance.StartCoroutine(context.Target.InflictDamage(dmg, true, endure));

            if (context.Target.HP == 0)
            {
                context.ContextStates.Set(new Knockout());
                context.AddContextStateInt<TotalKnockouts>(true, 1);
            }
        }
        protected void ReportDamage(BattleContext context, int dmg, int hpLost)
        {
            context.ContextStates.Set(new DamageDealt(dmg));
            context.AddContextStateInt<TotalDamageDealt>(true, dmg);
            context.ContextStates.Set(new HPLost(hpLost));
            context.AddContextStateInt<TotalHPLost>(true, hpLost);
        }
    }

    /// <summary>
    /// Event that OHKOs the target
    /// </summary>
    [Serializable]
    public class OHKODamageEvent : DirectDamageEvent
    {
        public OHKODamageEvent() { }
        public override GameEvent Clone() { return new OHKODamageEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int prevHP = context.Target.HP;

            int dmg = -1;

            if (!context.GetContextStateMult<DmgMult>().IsNeutralized())
            {
                int typeMatchup = PreTypeEvent.GetDualEffectiveness(context.User, context.Target, context.Data);
                if (typeMatchup <= PreTypeEvent.N_E_2)
                {
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(PreTypeEvent.EffectivenessToPhrase(typeMatchup), context.Target.GetDisplayName(false)));
                    context.AddContextStateMult<DmgMult>(false, -1, 4);
                }
            }

            int dmgMod = context.GetContextStateMult<DmgMult>().Multiply(0);
            if (dmgMod >= 0)
            {
                if (context.GetContextStateMult<DmgMult>().IsNeutralized())
                    dmg = 0;

                yield return CoroutineManager.Instance.StartCoroutine(InflictDamage(context, dmg));
            }

            int hpLost = prevHP - context.Target.HP;
            ReportDamage(context, hpLost, hpLost);
        }
    }

    [Serializable]
    public abstract class CalculatedDamageEvent : DirectDamageEvent
    {
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int damage = CalculateDamage(owner, context);

            int prevHP = context.Target.HP;
            if (damage >= 0)
                yield return CoroutineManager.Instance.StartCoroutine(InflictDamage(context, damage));

            int hpLost = prevHP - context.Target.HP;
            ReportDamage(context, Math.Max(0, damage), hpLost);
        }

        public abstract int CalculateDamage(GameEventOwner owner, BattleContext context);
    }

    /// <summary>
    /// Event that calculates the damage of the action, taking account into effectiveness, critical hits, stat boosts, and STAB
    /// </summary>
    [Serializable]
    public class DamageFormulaEvent : CalculatedDamageEvent
    {
        public DamageFormulaEvent() { }
        public override GameEvent Clone() { return new DamageFormulaEvent(); }

        public override int CalculateDamage(GameEventOwner owner, BattleContext context)
        {
            return CalculateDamageFormula(owner, context);
        }

        public static int CalculateDamageFormula(GameEventOwner owner, BattleContext context)
        {
            //PreExecuteAction: attacker attack/spAtk and level are assigned
            //in OnAction:
            //  -AttackBoost, SpAtkBoost, DefBoost, SpDefBoost, AccuracyMod are added

            //PreMoveHit: target defense/SpDef is assigned
            //in BeforeHit:
            //  -TargetAttackBoost, TargetSpAtkBoost, TargetDefenseBoost, TargetSpDefBoost, EvasionMod are added

            if (!context.GetContextStateMult<DmgMult>().IsNeutralized())
            {
                string effectivenessMsg = null;

                //modify attack based on battle tag
                int atkBoost = 0;
                int defBoost = 0;
                if (context.Data.Category == BattleData.SkillCategory.Physical || context.Data.Category == BattleData.SkillCategory.Magical)
                {
                    BattleData.SkillCategory attackCategory = context.Data.Category;
                    if (context.ContextStates.Contains<CrossCategory>())
                    {
                        if (attackCategory == BattleData.SkillCategory.Physical)
                            attackCategory = BattleData.SkillCategory.Magical;
                        else if (attackCategory == BattleData.SkillCategory.Magical)
                            attackCategory = BattleData.SkillCategory.Physical;
                    }

                    //adjust attack
                    if (attackCategory == BattleData.SkillCategory.Physical)
                        atkBoost = context.GetContextStateInt<UserAtkBoost>(0);
                    else if (attackCategory == BattleData.SkillCategory.Magical)
                        atkBoost = context.GetContextStateInt<UserSpAtkBoost>(0);

                    //adjust defense
                    if (context.Data.Category == BattleData.SkillCategory.Physical)
                        defBoost = context.GetContextStateInt<TargetDefBoost>(0);
                    else if (context.Data.Category == BattleData.SkillCategory.Magical)
                        defBoost = context.GetContextStateInt<TargetSpDefBoost>(0);
                }

                int critLevel = context.GetContextStateInt<CritLevel>(0);
                CritRateLevelTableState critTable = DataManager.Instance.UniversalEvent.UniversalStates.GetWithDefault<CritRateLevelTableState>();
                if (DataManager.Instance.Save.Rand.Next(0, 12) < critTable.GetCritChance(critLevel))
                {
                    //see if it criticals
                    if (context.User.CharStates.Contains<SnipeState>())
                        context.AddContextStateMult<DmgMult>(false, 5, 2);
                    else
                        context.AddContextStateMult<DmgMult>(false, 3, 2);

                    atkBoost = Math.Max(0, atkBoost);
                    defBoost = Math.Min(0, defBoost);

                    effectivenessMsg = Text.FormatGrammar(new StringKey("MSG_CRITICAL_HIT").ToLocal());
                    context.ContextStates.Set(new AttackCrit());
                }

                AtkDefLevelTableState dmgModTable = DataManager.Instance.UniversalEvent.UniversalStates.GetWithDefault<AtkDefLevelTableState>();
                int attackStat = dmgModTable.AtkLevelMult(context.GetContextStateInt<UserAtkStat>(1), atkBoost);
                int defenseStat = Math.Max(1, dmgModTable.DefLevelMult(context.GetContextStateInt<TargetDefStat>(1), defBoost));

                //STAB
                if (context.User.HasElement(context.Data.Element))
                    context.AddContextStateMult<DmgMult>(false, 4, 3);

                int typeMatchup = PreTypeEvent.GetDualEffectiveness(context.User, context.Target, context.Data);
                if (typeMatchup != PreTypeEvent.NRM_2)
                {
                    if (effectivenessMsg != null)
                        effectivenessMsg += (" " + Text.FormatGrammar(PreTypeEvent.EffectivenessToPhrase(typeMatchup), context.Target.GetDisplayName(false)));
                    else
                        effectivenessMsg = Text.FormatGrammar(PreTypeEvent.EffectivenessToPhrase(typeMatchup), context.Target.GetDisplayName(false));

                    int effectiveness = PreTypeEvent.GetEffectivenessMult(typeMatchup);
                    if (effectiveness == 0)
                        effectiveness = -1;

                    context.AddContextStateMult<DmgMult>(false, effectiveness, PreTypeEvent.GetEffectivenessMult(PreTypeEvent.NRM_2));
                }

                if (effectivenessMsg != null)
                    DungeonScene.Instance.LogMsg(effectivenessMsg);

                if (context.GetContextStateMult<DmgMult>().IsNeutralized())
                    return context.GetContextStateMult<DmgMult>().Multiply(0);

                int power = context.Data.SkillStates.GetWithDefault<BasePowerState>().Power;
                int damage = context.GetContextStateMult<DmgMult>().Multiply((context.GetContextStateInt<UserLevel>(0) / 3 + 6) * attackStat * power) / defenseStat / 50 * DataManager.Instance.Save.Rand.Next(90, 101) / 100;

                if (!(context.ActionType == BattleActionType.Skill && context.Data.ID == DataManager.Instance.DefaultSkill))
                    damage = Math.Max(1, damage);

                return damage;
            }
            else
                return context.GetContextStateMult<DmgMult>().Multiply(0);
        }
    }


    [Serializable]
    public abstract class FixedDamageEvent : CalculatedDamageEvent
    {
        public override int CalculateDamage(GameEventOwner owner, BattleContext context)
        {
            int damage = CalculateFixedDamage(owner, context);

            int typeMatchup = PreTypeEvent.GetDualEffectiveness(context.User, context.Target, context.Data);
            if (typeMatchup <= PreTypeEvent.N_E_2)
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(PreTypeEvent.EffectivenessToPhrase(typeMatchup), context.Target.GetDisplayName(false)));
                damage = -1;
            }

            return damage;
        }

        protected abstract int CalculateFixedDamage(GameEventOwner owner, BattleContext context);
    }

    [Serializable]
    public class BasePowerDamageEvent : FixedDamageEvent
    {
        public override GameEvent Clone() { return new BasePowerDamageEvent(); }
        protected override int CalculateFixedDamage(GameEventOwner owner, BattleContext context)
        {
            BasePowerState state = context.Data.SkillStates.GetWithDefault<BasePowerState>();
            if (state != null)
                return state.Power;
            return 0;
        }
    }

    /// <summary>
    /// Event that sets the specified damage the character will take 
    /// </summary>
    [Serializable]
    public class SpecificDamageEvent : FixedDamageEvent
    {
        /// <summary>
        /// The damage amount
        /// </summary>
        public int Damage;

        public SpecificDamageEvent() { }
        public SpecificDamageEvent(int dmg) { Damage = dmg; }
        public SpecificDamageEvent(SpecificDamageEvent other)
        {
            Damage = other.Damage;
        }

        public override GameEvent Clone() { return new SpecificDamageEvent(this); }
        protected override int CalculateFixedDamage(GameEventOwner owner, BattleContext context)
        {
            return Damage;
        }
    }

    /// <summary>
    /// Event that calculates the damage based on the character's level
    /// </summary>
    [Serializable]
    public class LevelDamageEvent : FixedDamageEvent
    {
        /// <summary>
        /// Whether to calculate with the target or user's level
        /// </summary>
        public bool AffectTarget;

        /// <summary>
        /// The numerator of the modifier
        /// </summary>
        public int Numerator;

        /// <summary>
        /// The denominator of the modifier
        /// </summary>
        public int Denominator;
        public LevelDamageEvent() { }
        public LevelDamageEvent(bool affectTarget, int numerator, int denominator)
        {
            AffectTarget = affectTarget;
            Numerator = numerator;
            Denominator = denominator;
        }
        protected LevelDamageEvent(LevelDamageEvent other)
        {
            AffectTarget = other.AffectTarget;
            Numerator = other.Numerator;
            Denominator = other.Denominator;
        }
        public override GameEvent Clone() { return new LevelDamageEvent(this); }
        protected override int CalculateFixedDamage(GameEventOwner owner, BattleContext context)
        {
            int level = (AffectTarget ? context.Target.Level : context.GetContextStateInt<UserLevel>(0));
            return level * Numerator / Denominator;
        }
    }

    /// <summary>
    /// Event that deals fixed damage depending on the target's distance from the attack and the user's level
    /// </summary>
    [Serializable]
    public class PsywaveDamageEvent : FixedDamageEvent
    {
        public override GameEvent Clone() { return new PsywaveDamageEvent(); }
        protected override int CalculateFixedDamage(GameEventOwner owner, BattleContext context)
        {
            // 1 2 1 0 1 2 1 0
            // sine wave function
            //TODO: this breaks in small wrapped maps
            int locDiff = ZoneManager.Instance.CurrentMap.GetClosestDist8(context.StrikeStartTile, context.Target.CharLoc);
            int diff = locDiff % 4;
            int power = (diff > 2) ? 1 : diff;
            return Math.Max(1, context.GetContextStateInt<UserLevel>(0) * power / 2);
        }
    }

    /// <summary>
    /// Event that deals fixed damage based on the user's current HP.
    /// </summary>
    [Serializable]
    public class UserHPDamageEvent : FixedDamageEvent
    {
        /// <summary>
        /// Instead, deal damage based on the HP the user is missing.
        /// </summary>
        public bool Reverse;
        public UserHPDamageEvent() { }
        public UserHPDamageEvent(bool reverse)
        {
            Reverse = reverse;
        }
        protected UserHPDamageEvent(UserHPDamageEvent other)
        {
            Reverse = other.Reverse;
        }
        public override GameEvent Clone() { return new UserHPDamageEvent(this); }
        protected override int CalculateFixedDamage(GameEventOwner owner, BattleContext context)
        {
            return Reverse ? (context.User.MaxHP - context.User.HP) : context.User.HP;
        }
    }

    /// <summary>
    /// Event that reduces the target's HP to the user's HP
    /// </summary>
    [Serializable]
    public class EndeavorEvent : FixedDamageEvent
    {
        public override GameEvent Clone() { return new EndeavorEvent(); }
        protected override int CalculateFixedDamage(GameEventOwner owner, BattleContext context)
        {
            return Math.Max(0, context.Target.HP - context.User.HP);
        }
    }

    /// <summary>
    /// Event that reduces the target's HP by half
    /// </summary>
    [Serializable]
    public class CutHPDamageEvent : FixedDamageEvent
    {
        public override GameEvent Clone() { return new CutHPDamageEvent(); }
        protected override int CalculateFixedDamage(GameEventOwner owner, BattleContext context)
        {
            return Math.Max(1, context.GetContextStateMult<HPDmgMult>().Multiply(context.Target.HP / 2));
        }
    }

    /// <summary>
    /// Event that reduces the target's HP by the specified HP fraction 
    /// </summary>
    [Serializable]
    public class MaxHPDamageEvent : FixedDamageEvent
    {
        public int HPFraction;

        public MaxHPDamageEvent() { }
        public MaxHPDamageEvent(int hpFraction)
        {
            HPFraction = hpFraction;
        }
        protected MaxHPDamageEvent(MaxHPDamageEvent other)
        {
            HPFraction = other.HPFraction;
        }
        public override GameEvent Clone() { return new MaxHPDamageEvent(this); }
        protected override int CalculateFixedDamage(GameEventOwner owner, BattleContext context)
        {
            return Math.Max(1, context.GetContextStateMult<HPDmgMult>().Multiply(context.Target.MaxHP / HPFraction));
        }
    }

}

