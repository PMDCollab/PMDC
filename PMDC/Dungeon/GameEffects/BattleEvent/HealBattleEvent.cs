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
    // Battle events related to recovering HP
    public interface IHealEvent
    {
        int HPNum { get; }
        int HPDen { get; }
    }

    /// <summary>
    /// Event that heals the character based on the specified fraction of the character's max HP 
    /// Stat drops will heal the character instead
    /// </summary>
    [Serializable]
    public class RestoreHPEvent : BattleEvent, IHealEvent
    {
        /// <summary>
        /// The numerator of the fraction
        /// </summary>
        public int Numerator;

        /// <summary>
        /// The denominator of the fraction
        /// </summary>
        public int Denominator;

        /// <summary>
        /// Whether to affect the target or user 
        /// </summary>
        public bool AffectTarget;

        public int HPNum { get { return Numerator; } }
        public int HPDen { get { return Denominator; } }

        public RestoreHPEvent() { }
        public RestoreHPEvent(int numerator, int denominator, bool affectTarget) { Numerator = numerator; Denominator = denominator; AffectTarget = affectTarget; }
        protected RestoreHPEvent(RestoreHPEvent other)
        {
            Numerator = other.Numerator;
            Denominator = other.Denominator;
            AffectTarget = other.AffectTarget;
        }
        public override GameEvent Clone() { return new RestoreHPEvent(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = (AffectTarget ? context.Target : context.User);
            if (target.Dead)
                yield break;

            int dmg = target.MaxHP * Numerator / Denominator;
            yield return CoroutineManager.Instance.StartCoroutine(target.RestoreHP(dmg));
            context.ContextStates.Set(new DamageHealedTarget(dmg));
        }
    }


    /// <summary>
    /// Event that heals the character depending on the map status
    /// </summary>
    [Serializable]
    public class WeatherHPEvent : BattleEvent, IHealEvent
    {
        /// <summary>
        /// The map status mapped to a bool
        /// The bool indicates whether the heal will be boosted or reduced 
        /// </summary>
        [JsonConverter(typeof(MapStatusBoolDictConverter))]
        [DataType(1, DataManager.DataType.MapStatus, false)]
        public Dictionary<string, bool> WeatherPair;

        /// <summary>
        /// The numerator of the fractional heal
        /// </summary>
        public int HPDiv;

        public int HPNum { get { return HPDiv; } }
        public int HPDen { get { return 12; } }

        public WeatherHPEvent() { WeatherPair = new Dictionary<string, bool>(); }
        public WeatherHPEvent(int hpDiv, Dictionary<string, bool> weather)
        {
            HPDiv = hpDiv;
            WeatherPair = weather;
        }
        protected WeatherHPEvent(WeatherHPEvent other) : this()
        {
            HPDiv = other.HPDiv;
            foreach (string weather in other.WeatherPair.Keys)
                WeatherPair.Add(weather, other.WeatherPair[weather]);
        }
        public override GameEvent Clone() { return new WeatherHPEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int numerator = HPDiv;

            foreach (string weather in WeatherPair.Keys)
            {
                if (ZoneManager.Instance.CurrentMap.Status.ContainsKey(weather))
                {
                    if (WeatherPair[weather])
                        numerator *= 2;
                    else
                        numerator /= 2;
                    break;
                }
            }

            int dmg = context.Target.MaxHP * numerator / HPDen;
            yield return CoroutineManager.Instance.StartCoroutine(context.Target.RestoreHP(dmg));

            context.ContextStates.Set(new DamageHealedTarget(dmg));
        }
    }

    /// <summary>
    /// Event that restores the user's HP based on the damage the move dealt 
    /// </summary>
    [Serializable]
    public class HPDrainEvent : BattleEvent
    {
        /// <summary>
        /// The amount of HP restored calculated by damage dealt divided by this value
        /// </summary>
        public int DrainFraction;

        public HPDrainEvent() { }
        public HPDrainEvent(int drainFraction) { DrainFraction = drainFraction; }
        protected HPDrainEvent(HPDrainEvent other)
        {
            DrainFraction = other.DrainFraction;
        }
        public override GameEvent Clone() { return new HPDrainEvent(this); }


        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int damageDone = context.GetContextStateInt<TotalDamageDealt>(true, 0);
            if (damageDone > 0)
            {
                TaintedDrain taintedDrain;
                if (context.GlobalContextStates.TryGet<TaintedDrain>(out taintedDrain))
                {
                    GameManager.Instance.BattleSE("DUN_Toxic");
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_LIQUID_OOZE").ToLocal(), context.User.GetDisplayName(false)));
                    yield return CoroutineManager.Instance.StartCoroutine(context.User.InflictDamage(Math.Max(1, damageDone * taintedDrain.Mult / DrainFraction)));
                }
                else
                    yield return CoroutineManager.Instance.StartCoroutine(context.User.RestoreHP(Math.Max(1, damageDone / DrainFraction)));
            }
        }
    }

    /// <summary>
    /// Event that revives all fainted party memebers
    /// </summary>
    [Serializable]
    public class ReviveAllEvent : BattleEvent
    {
        public ReviveAllEvent() { }
        public override GameEvent Clone() { return new ReviveAllEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            bool revived = false;
            foreach (Character character in context.User.MemberTeam.EnumerateChars())
            {
                if (character.Dead)
                {
                    Loc? endLoc = ZoneManager.Instance.CurrentMap.GetClosestTileForChar(character, context.User.CharLoc);
                    if (endLoc == null)
                        endLoc = context.User.CharLoc;
                    character.CharLoc = endLoc.Value;

                    character.HP = character.MaxHP;
                    character.Dead = false;
                    character.DefeatAt = "";

                    character.UpdateFrame();
                    ZoneManager.Instance.CurrentMap.UpdateExploration(character);

                    GameManager.Instance.BattleSE("DUN_Send_Home");
                    SingleEmitter emitter = new SingleEmitter(new BeamAnimData("Column_Yellow", 3));
                    emitter.Layer = DrawLayer.Front;
                    emitter.SetupEmit(character.MapLoc, character.MapLoc, character.CharDir);
                    DungeonScene.Instance.CreateAnim(emitter, DrawLayer.NoDraw);

                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_REVIVE").ToLocal(), character.GetDisplayName(false)));

                    yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(20));
                    revived = true;
                }
            }
            if (!revived)
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_REVIVE_NONE").ToLocal()));
        }
    }

}

