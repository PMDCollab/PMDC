using System;
using System.Collections.Generic;
using RogueEssence;
using RogueEssence.Dungeon;
using RogueEssence.Data;
using Newtonsoft.Json;
using RogueEssence.Dev;
using RogueElements;

namespace PMDC.Dungeon
{
    /// <summary>
    /// Attacker's attacking stat
    /// </summary>
    [Serializable]
    public class UserAtkStat : ContextIntState
    {
        public UserAtkStat() { }
        public UserAtkStat(int count) : base(count) { }
        protected UserAtkStat(UserAtkStat other) : base(other) { }
        public override GameplayState Clone() { return new UserAtkStat(this); }
    }

    /// <summary>
    /// Target's defensive stat
    /// </summary>
    [Serializable]
    public class TargetDefStat : ContextIntState
    {
        public TargetDefStat() { }
        public TargetDefStat(int count) : base(count) { }
        protected TargetDefStat(TargetDefStat other) : base(other) { }
        public override GameplayState Clone() { return new TargetDefStat(this); }
    }

    /// <summary>
    /// Attacker's hit rate stat
    /// </summary>
    [Serializable]
    public class UserHitStat : ContextIntState
    {
        public UserHitStat() { }
        public UserHitStat(int count) : base(count) { }
        protected UserHitStat(UserHitStat other) : base(other) { }
        public override GameplayState Clone() { return new UserHitStat(this); }
    }

    /// <summary>
    /// Target's dodge rate stat
    /// </summary>
    [Serializable]
    public class TargetEvadeStat : ContextIntState
    {
        public TargetEvadeStat() { }
        public TargetEvadeStat(int count) : base(count) { }
        protected TargetEvadeStat(TargetEvadeStat other) : base(other) { }
        public override GameplayState Clone() { return new TargetEvadeStat(this); }
    }

    [Serializable]
    public class UserLevel : ContextIntState
    {
        public UserLevel() { }
        public UserLevel(int count) : base(count) { }
        protected UserLevel(UserLevel other) : base(other) { }
        public override GameplayState Clone() { return new UserLevel(this); }
    }

    [Serializable]
    public class TargetLevel : ContextIntState
    {
        public TargetLevel() { }
        public TargetLevel(int count) : base(count) { }
        protected TargetLevel(TargetLevel other) : base(other) { }
        public override GameplayState Clone() { return new TargetLevel(this); }
    }

    /// <summary>
    /// Multipliers work as follows (before reaching damage calc):
    /// Num > 0: Process damage normally with msg
    /// Num = 0: Process 0 damage with msg
    /// Num < 0: Process 0 damage without msg
    /// Denominator is always > 0
    /// </summary>
    [Serializable]
    public class DmgMult : ContextMultState
    {
        public DmgMult() { }
        protected DmgMult(DmgMult other) : base(other) { }
        public override GameplayState Clone() { return new DmgMult(this); }
    }

    [Serializable]
    public class HPDmgMult : ContextMultState
    {
        public HPDmgMult() { }
        protected HPDmgMult(HPDmgMult other) : base(other) { }
        public override GameplayState Clone() { return new HPDmgMult(this); }
    }

    /// <summary>
    /// Multipliers work as follows:
    /// Num > 0: Process accuracy calcs normally with msg
    /// Num = 0: Process automatic miss with msg, unless the attack never misses. ignores miss compensation
    /// Num < 0: Process automatic miss without msg, even if the attack never misses
    /// Denominator is always > 0
    /// </summary>
    [Serializable]
    public class AccMult : ContextMultState
    {
        public AccMult() { }
        protected AccMult(AccMult other) : base(other) { }
        public override GameplayState Clone() { return new AccMult(this); }
    }

    [Serializable]
    public class HungerMult : ContextMultState
    {
        public HungerMult() { }
        protected HungerMult(HungerMult other) : base(other) { }
        public override GameplayState Clone() { return new HungerMult(this); }
    }

    [Serializable]
    public class TaintedDrain : ContextState
    {
        public int Mult;
        public TaintedDrain() { }
        public TaintedDrain(int mult) { Mult = mult; }
        public TaintedDrain(TaintedDrain other) { Mult = other.Mult; }
        public override GameplayState Clone() { return new TaintedDrain(this); }
    }

    [Serializable]
    public class MoveCharge : ContextState
    {
        public MoveCharge() { }
        public override GameplayState Clone() { return new MoveCharge(); }
    }

    [Serializable]
    public class MoveBide : ContextState
    {
        public MoveBide() { }
        public override GameplayState Clone() { return new MoveBide(); }
    }

    [Serializable]
    public class FollowUp : ContextState
    {
        public FollowUp() { }
        public override GameplayState Clone() { return new FollowUp(); }
    }

    [Serializable]
    public class SleepAttack : ContextState
    {
        public SleepAttack() { }
        public override GameplayState Clone() { return new SleepAttack(); }
    }

    [Serializable]
    public class CureAttack : ContextState
    {
        public CureAttack() { }
        public override GameplayState Clone() { return new CureAttack(); }
    }

    [Serializable]
    public class BoundAttack : ContextState
    {
        public BoundAttack() { }
        public override GameplayState Clone() { return new BoundAttack(); }
    }

    [Serializable]
    public class AttackHit : ContextState
    {
        public AttackHit() { }
        public override GameplayState Clone() { return new AttackHit(); }
    }

    [Serializable]
    public class AttackHitTotal : ContextIntState
    {
        public AttackHitTotal() { }
        public AttackHitTotal(int count) : base(count) { }
        protected AttackHitTotal(AttackHitTotal other) : base(other) { }
        public override GameplayState Clone() { return new AttackHitTotal(this); }
    }

    [Serializable]
    public class Knockout : ContextState
    {
        public Knockout() { }
        public override GameplayState Clone() { return new Knockout(); }
    }

    [Serializable]
    public class CrossCategory : ContextState
    {
        public CrossCategory() { }
        public override GameplayState Clone() { return new CrossCategory(); }
    }

    [Serializable]
    public class AttackEndure : ContextState
    {
        public AttackEndure() { }
        public override GameplayState Clone() { return new AttackEndure(); }
    }

    [Serializable]
    public class AttackCrit : ContextState
    {
        public AttackCrit() { }
        public override GameplayState Clone() { return new AttackCrit(); }
    }

    [Serializable]
    public class ItemCaught : ContextState
    {
        public ItemCaught() { }
        public override GameplayState Clone() { return new ItemCaught(); }
    }

    [Serializable]
    public class ItemDestroyed : ContextState
    {
        public ItemDestroyed() { }
        public override GameplayState Clone() { return new ItemDestroyed(); }
    }

    [Serializable]
    public class Redirected : ContextState
    {
        public Redirected() { }
        public override GameplayState Clone() { return new Redirected(); }
    }

    [Serializable]
    public class PressurePlus : ContextState
    {
        public PressurePlus() { }
        public override GameplayState Clone() { return new PressurePlus(); }
    }

    [Serializable]
    public class Corrosion : ContextState
    {
        public Corrosion() { }
        public override GameplayState Clone() { return new Corrosion(); }
    }

    [Serializable]
    public class Infiltrator : ContextState
    {
        [StringKey(0, true)]
        public StringKey Msg;
        public Infiltrator() { }
        public Infiltrator(StringKey msg) { Msg = msg; }
        protected Infiltrator(Infiltrator other) { Msg = other.Msg; }
        public override GameplayState Clone() { return new Infiltrator(this); }
    }

    [Serializable]
    public class BallFetch : ContextState
    {
        public BallFetch() { }
        public override GameplayState Clone() { return new BallFetch(); }
    }

    [Serializable]
    public class RecruitFail : ContextState
    {
        public Loc? ResultLoc;
        public RecruitFail() { }
        public RecruitFail(Loc? resultLoc) { ResultLoc = resultLoc; }
        public RecruitFail(RecruitFail other) { ResultLoc = other.ResultLoc; }
        public override GameplayState Clone() { return new RecruitFail(this); }
    }

    [Serializable]
    public class SingleDrawAbsorb : ContextState
    {
        public SingleDrawAbsorb() { }
        public override GameplayState Clone() { return new SingleDrawAbsorb(); }
    }

    [Serializable]
    public class FriendGuardProcEvent : ContextState
    {
        public FriendGuardProcEvent() { }
        public override GameplayState Clone() { return new FriendGuardProcEvent(); }
    }

    [Serializable]
    public class LastHitDist : ContextIntState
    {
        public LastHitDist() { }
        public LastHitDist(int count) : base(count) { }
        protected LastHitDist(LastHitDist other) : base(other) { }
        public override GameplayState Clone() { return new LastHitDist(this); }
    }

    [Serializable]
    public class CritLevel : ContextIntState
    {
        public CritLevel() { }
        public CritLevel(int count) : base(count) { }
        protected CritLevel(CritLevel other) : base(other) { }
        public override GameplayState Clone() { return new CritLevel(this); }
    }

    [Serializable]
    public class DamageDealt : ContextIntState
    {
        public DamageDealt() { }
        public DamageDealt(int count) : base(count) { }
        protected DamageDealt(DamageDealt other) : base(other) { }
        public override GameplayState Clone() { return new DamageDealt(this); }
    }

    [Serializable]
    public class TotalDamageDealt : ContextIntState
    {
        public TotalDamageDealt() { }
        public TotalDamageDealt(int count) : base(count) { }
        protected TotalDamageDealt(TotalDamageDealt other) : base(other) { }
        public override GameplayState Clone() { return new TotalDamageDealt(this); }
    }


    [Serializable]
    public class DamageHealedTarget : ContextIntState
    {
        public DamageHealedTarget() { }
        public DamageHealedTarget(int count) : base(count) { }
        protected DamageHealedTarget(DamageHealedTarget other) : base(other) { }
        public override GameplayState Clone() { return new DamageHealedTarget(this); }
    }


    [Serializable]
    public class RecruitBoost : ContextIntState
    {
        public RecruitBoost() { }
        public RecruitBoost(int count) : base(count) { }
        protected RecruitBoost(RecruitBoost other) : base(other) { }
        public override GameplayState Clone() { return new RecruitBoost(this); }
    }

    [Serializable]
    public class TotalKnockouts : ContextIntState
    {
        public TotalKnockouts() { }
        public TotalKnockouts(int count) : base(count) { }
        protected TotalKnockouts(TotalKnockouts other) : base(other) { }
        public override GameplayState Clone() { return new TotalKnockouts(this); }
    }

    [Serializable]
    public class HPLost : ContextIntState
    {
        public HPLost() { }
        public HPLost(int count) : base(count) { }
        protected HPLost(HPLost other) : base(other) { }
        public override GameplayState Clone() { return new HPLost(this); }
    }

    [Serializable]
    public class TotalHPLost : ContextIntState
    {
        public TotalHPLost() { }
        public TotalHPLost(int count) : base(count) { }
        protected TotalHPLost(TotalHPLost other) : base(other) { }
        public override GameplayState Clone() { return new TotalHPLost(this); }
    }





    [Serializable]
    public class UserAtkBoost : ContextIntState
    {
        public UserAtkBoost() { }
        public UserAtkBoost(int count) : base(count) { }
        protected UserAtkBoost(UserAtkBoost other) : base(other) { }
        public override GameplayState Clone() { return new UserAtkBoost(this); }
    }

    [Serializable]
    public class UserDefBoost : ContextIntState
    {
        public UserDefBoost() { }
        public UserDefBoost(int count) : base(count) { }
        protected UserDefBoost(UserDefBoost other) : base(other) { }
        public override GameplayState Clone() { return new UserDefBoost(this); }
    }

    [Serializable]
    public class UserSpAtkBoost : ContextIntState
    {
        public UserSpAtkBoost() { }
        public UserSpAtkBoost(int count) : base(count) { }
        protected UserSpAtkBoost(UserSpAtkBoost other) : base(other) { }
        public override GameplayState Clone() { return new UserSpAtkBoost(this); }
    }

    [Serializable]
    public class UserSpDefBoost : ContextIntState
    {
        public UserSpDefBoost() { }
        public UserSpDefBoost(int count) : base(count) { }
        protected UserSpDefBoost(UserSpDefBoost other) : base(other) { }
        public override GameplayState Clone() { return new UserSpDefBoost(this); }
    }

    [Serializable]
    public class UserAccuracyBoost : ContextIntState
    {
        public UserAccuracyBoost() { }
        public UserAccuracyBoost(int count) : base(count) { }
        protected UserAccuracyBoost(UserAccuracyBoost other) : base(other) { }
        public override GameplayState Clone() { return new UserAccuracyBoost(this); }
    }

    [Serializable]
    public class TargetAtkBoost : ContextIntState
    {
        public TargetAtkBoost() { }
        public TargetAtkBoost(int count) : base(count) { }
        protected TargetAtkBoost(TargetAtkBoost other) : base(other) { }
        public override GameplayState Clone() { return new TargetAtkBoost(this); }
    }

    [Serializable]
    public class TargetDefBoost : ContextIntState
    {
        public TargetDefBoost() { }
        public TargetDefBoost(int count) : base(count) { }
        protected TargetDefBoost(TargetDefBoost other) : base(other) { }
        public override GameplayState Clone() { return new TargetDefBoost(this); }
    }

    [Serializable]
    public class TargetSpAtkBoost : ContextIntState
    {
        public TargetSpAtkBoost() { }
        public TargetSpAtkBoost(int count) : base(count) { }
        protected TargetSpAtkBoost(TargetSpAtkBoost other) : base(other) { }
        public override GameplayState Clone() { return new TargetSpAtkBoost(this); }
    }

    [Serializable]
    public class TargetSpDefBoost : ContextIntState
    {
        public TargetSpDefBoost() { }
        public TargetSpDefBoost(int count) : base(count) { }
        protected TargetSpDefBoost(TargetSpDefBoost other) : base(other) { }
        public override GameplayState Clone() { return new TargetSpDefBoost(this); }
    }


    [Serializable]
    public class TargetEvasionBoost : ContextIntState
    {
        public TargetEvasionBoost() { }
        public TargetEvasionBoost(int count) : base(count) { }
        protected TargetEvasionBoost(TargetEvasionBoost other) : base(other) { }
        public override GameplayState Clone() { return new TargetEvasionBoost(this); }
    }




    [Serializable]
    public class SwitchFormContext : ContextState
    {
        public int Form;
        public SwitchFormContext() { }
        protected SwitchFormContext(SwitchFormContext other)
        {
            Form = other.Form;
        }
        public override GameplayState Clone() { return new SwitchFormContext(this); }
    }


    [Serializable]
    public class MoveLearnContext : ContextState
    {
        public string MoveLearn;
        public int ReplaceSlot;
        public MoveLearnContext() { }
        protected MoveLearnContext(MoveLearnContext other)
        {
            MoveLearn = other.MoveLearn;
            ReplaceSlot = other.ReplaceSlot;
        }
        public override GameplayState Clone() { return new MoveLearnContext(this); }
    }

    [Serializable]
    public class AbilityLearnContext : ContextState
    {
        public string AbilityLearn;
        public int ReplaceSlot;
        public AbilityLearnContext() { }
        protected AbilityLearnContext(AbilityLearnContext other)
        {
            AbilityLearn = other.AbilityLearn;
            ReplaceSlot = other.ReplaceSlot;
        }
        public override GameplayState Clone() { return new AbilityLearnContext(this); }
    }

    [Serializable]
    public class MoveDeleteContext : ContextState
    {
        public int MoveDelete;
        public MoveDeleteContext() { }
        public MoveDeleteContext(int slot) { MoveDelete = slot; }
        protected MoveDeleteContext(MoveDeleteContext other)
        {
            MoveDelete = other.MoveDelete;
        }
        public override GameplayState Clone() { return new MoveDeleteContext(this); }
    }

    [Serializable]
    public class AbilityDeleteContext : ContextState
    {
        public int AbilityDelete;
        public AbilityDeleteContext() { }
        public AbilityDeleteContext(int slot) { AbilityDelete = slot; }
        protected AbilityDeleteContext(AbilityDeleteContext other)
        {
            AbilityDelete = other.AbilityDelete;
        }
        public override GameplayState Clone() { return new AbilityDeleteContext(this); }
    }


    [Serializable]
    public class WithdrawAssemblyContext : ContextState
    {
        public int WithdrawSlot;
        public WithdrawAssemblyContext() { }
        public WithdrawAssemblyContext(int slot) { WithdrawSlot = slot; }
        protected WithdrawAssemblyContext(WithdrawAssemblyContext other)
        {
            WithdrawSlot = other.WithdrawSlot;
        }
        public override GameplayState Clone() { return new WithdrawAssemblyContext(this); }
    }

    [Serializable]
    public class WithdrawStorageContext : ContextState
    {
        public WithdrawSlot WithdrawSlot;
        public WithdrawStorageContext() { }
        public WithdrawStorageContext(WithdrawSlot slot) { WithdrawSlot = slot; }
        protected WithdrawStorageContext(WithdrawStorageContext other)
        {
            WithdrawSlot = other.WithdrawSlot;
        }
        public override GameplayState Clone() { return new WithdrawStorageContext(this); }
    }

    [Serializable]
    public class DepositStorageContext : ContextState
    {
        public InvSlot DepositSlot;
        public DepositStorageContext() { }
        public DepositStorageContext(InvSlot slot) { DepositSlot = slot; }
        protected DepositStorageContext(DepositStorageContext other)
        {
            DepositSlot = other.DepositSlot;
        }
        public override GameplayState Clone() { return new DepositStorageContext(this); }
    }

    [Serializable]
    public class JudgmentContext : ContextState
    {
        [JsonConverter(typeof(ElementListConverter))]
        public List<string> Elements;
        public JudgmentContext() { Elements = new List<string>(); }
        public JudgmentContext(List<string> elements) { Elements = elements; }
        protected JudgmentContext(JudgmentContext other) : this()
        {
            Elements.AddRange(other.Elements);
        }
        public override GameplayState Clone() { return new JudgmentContext(this); }
    }



    [Serializable]
    public class SilkState : ContextState
    {
        public SilkState() { }
        public override GameplayState Clone() { return new SilkState(); }
    }

    [Serializable]
    public class DustState : ContextState
    {
        public DustState() { }
        public override GameplayState Clone() { return new DustState(); }
    }
}
