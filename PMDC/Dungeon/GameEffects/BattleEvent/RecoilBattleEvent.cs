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
    // Battle events that handle recoil

    /// <summary>
    /// Event that recoil damage to the user based on how much damage was dealt
    /// </summary>
    [Serializable]
    public class DamageRecoilEvent : RecoilEvent
    {
        /// <summary>
        /// The value dividing the total damage dealt representing the recoil damage
        /// </summary>
        public int Fraction;

        public DamageRecoilEvent() { }
        public DamageRecoilEvent(int damageFraction) { Fraction = damageFraction; }
        protected DamageRecoilEvent(DamageRecoilEvent other)
        {
            Fraction = other.Fraction;
        }
        public override GameEvent Clone() { return new DamageRecoilEvent(this); }

        protected override int GetRecoilDamage(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int damageDone = context.GetContextStateInt<TotalDamageDealt>(true, 0);
            return Math.Max(1, damageDone / Fraction);
        }
    }

    /// <summary>
    /// Event that deals recoil damage to the user if the move landed
    /// </summary>
    [Serializable]
    public class HPRecoilEvent : RecoilEvent
    {

        /// <summary>
        /// The value dividing the user's HP representing the recoil damage
        /// </summary>
        public int Fraction;

        /// <summary>
        /// Whether to use the user's max HP or current HP
        /// </summary>
        public bool MaxHP;

        public HPRecoilEvent() { }
        public HPRecoilEvent(int fraction, bool maxHP) { Fraction = fraction; MaxHP = maxHP; }
        protected HPRecoilEvent(HPRecoilEvent other)
        {
            Fraction = other.Fraction;
            MaxHP = other.MaxHP;
        }
        public override GameEvent Clone() { return new HPRecoilEvent(this); }

        protected override int GetRecoilDamage(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (MaxHP)
                return Math.Max(1, context.User.MaxHP / Fraction);
            else
                return Math.Max(1, context.User.HP / Fraction);
        }
    }


    [Serializable]
    public abstract class RecoilEvent : BattleEvent
    {
        public RecoilEvent() { }

        protected abstract int GetRecoilDamage(GameEventOwner owner, Character ownerChar, BattleContext context);

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int damageDone = context.GetContextStateInt<TotalDamageDealt>(true, 0);
            if (damageDone > 0)
            {
                if (!context.User.CharStates.Contains<NoRecoilState>() && !context.User.CharStates.Contains<MagicGuardState>())
                {
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_HIT_RECOIL").ToLocal(), context.User.GetDisplayName(false)));

                    GameManager.Instance.BattleSE("DUN_Hit_Neutral");
                    if (!context.User.Unidentifiable)
                    {
                        SingleEmitter endEmitter = new SingleEmitter(new AnimData("Hit_Neutral", 3));
                        endEmitter.SetupEmit(context.User.MapLoc, context.User.MapLoc, context.User.CharDir);
                        DungeonScene.Instance.CreateAnim(endEmitter, DrawLayer.NoDraw);
                    }

                    int recoil = GetRecoilDamage(owner, ownerChar, context);
                    yield return CoroutineManager.Instance.StartCoroutine(context.User.InflictDamage(recoil));
                }
            }
        }
    }

    /// <summary>
    /// Event that deals recoil damage to the user if the move missed
    /// </summary>
    [Serializable]
    public class CrashLandEvent : BattleEvent
    {

        /// <summary>
        /// The value dividing the user's max HP representing the recoil damage
        /// </summary>
        public int HPFraction;

        public CrashLandEvent() { }
        public CrashLandEvent(int damageFraction) { HPFraction = damageFraction; }
        protected CrashLandEvent(CrashLandEvent other)
        {
            HPFraction = other.HPFraction;
        }
        public override GameEvent Clone() { return new CrashLandEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.GetContextStateInt<AttackHitTotal>(true, 0) == 0)
            {
                if (!context.User.CharStates.Contains<NoRecoilState>() && !context.User.CharStates.Contains<MagicGuardState>())
                {
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_HIT_CRASH").ToLocal(), context.User.GetDisplayName(false)));
                    yield return CoroutineManager.Instance.StartCoroutine(context.User.InflictDamage(Math.Max(1, context.User.MaxHP / HPFraction)));
                }
            }
        }
    }
}

