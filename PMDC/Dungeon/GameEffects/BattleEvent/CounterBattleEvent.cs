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
    // Battle events that trigger a counter effect

    /// <summary>
    /// Event that reflects all damaging moves to nearby foes
    /// </summary>
    [Serializable]
    public class ReflectAllEvent : BattleEvent
    {

        /// <summary>
        /// The numerator of the damage reflected
        /// </summary>
        public int Numerator;

        /// <summary>
        /// The denominator of the damage reflected
        /// </summary>
        public int Denominator;

        /// <summary>
        /// Enemies within the radius will be dealt the reflected damage
        /// </summary>
        public int Range;

        /// <summary>
        /// The list of battle VFXs played if the condition is met
        /// </summary>
        public List<BattleAnimEvent> Anims;

        public ReflectAllEvent() { Anims = new List<BattleAnimEvent>(); }
        public ReflectAllEvent(int numerator, int denominator, int range, params BattleAnimEvent[] anims)
        {
            Numerator = numerator;
            Denominator = denominator;
            Range = range;
            Anims = new List<BattleAnimEvent>();
            Anims.AddRange(anims);
        }
        protected ReflectAllEvent(ReflectAllEvent other)
        {
            Numerator = other.Numerator;
            Denominator = other.Denominator;
            Range = other.Range;
            Anims = new List<BattleAnimEvent>();
            foreach (BattleAnimEvent anim in other.Anims)
                Anims.Add((BattleAnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new ReflectAllEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            BattleData.SkillCategory category = ((StatusEffect)owner).StatusStates.GetWithDefault<CategoryState>().Category;
            int damage = context.GetContextStateInt<DamageDealt>(0);
            if ((category == BattleData.SkillCategory.None || context.Data.Category == category) && damage > 0 && DungeonScene.Instance.GetMatchup(context.User, context.Target) != Alignment.Self)
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_REFLECT_ALL").ToLocal()));

                foreach (BattleAnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                int dmg = damage * Numerator / Denominator;

                List<Character> targets = AreaAction.GetTargetsInArea(context.Target, context.Target.CharLoc, Alignment.Foe, Range);

                for (int ii = 0; ii < targets.Count; ii++)
                {
                    HitAndRunState cancel;
                    if (targets[ii].CharStates.TryGet(out cancel))
                    {
                        DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_HIT_AND_RUN").ToLocal(), targets[ii].GetDisplayName(false), ((ItemEntrySummary)DataManager.Instance.DataIndices[DataManager.DataType.Item].Get(cancel.OriginItem)).GetIconName()));
                        continue;
                    }

                    int charDmg = dmg;

                    yield return CoroutineManager.Instance.StartCoroutine(targets[ii].InflictDamage(charDmg));
                }
            }
        }
    }



    /// <summary>
    /// Event that reflects damage back to the user if the move type matches the specified type
    /// </summary>
    [Serializable]
    public class CounterTypeEvent : BattleEvent
    {
        /// <summary>
        /// The numerator of the damage reflected
        /// </summary>
        public int Numerator;

        /// <summary>
        /// The denominator of the damage reflected
        /// </summary>
        public int Denominator;

        /// <summary>
        /// The type reflected
        /// </summary>
        [JsonConverter(typeof(ElementConverter))]
        [DataType(0, DataManager.DataType.Element, false)]
        public string CounterElement;

        public CounterTypeEvent() { CounterElement = ""; }
        public CounterTypeEvent(string element, int numerator, int denominator)
        {
            CounterElement = element;
            Numerator = numerator;
            Denominator = denominator;
        }
        protected CounterTypeEvent(CounterTypeEvent other)
        {
            Numerator = other.Numerator;
            Denominator = other.Denominator;
            CounterElement = other.CounterElement;
        }
        public override GameEvent Clone() { return new CounterTypeEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int damage = context.GetContextStateInt<DamageDealt>(0);
            if (damage > 0 && context.ActionType == BattleActionType.Skill && (CounterElement == DataManager.Instance.DefaultElement || context.Data.Element == CounterElement) && DungeonScene.Instance.GetMatchup(context.User, context.Target) != Alignment.Self)
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_REFLECT").ToLocal()));

                HitAndRunState cancel;
                if (context.User.CharStates.TryGet(out cancel))
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_HIT_AND_RUN").ToLocal(), context.User.GetDisplayName(false), ((ItemEntrySummary)DataManager.Instance.DataIndices[DataManager.DataType.Item].Get(cancel.OriginItem)).GetIconName()));
                else
                {
                    int recoil = damage * Numerator / Denominator;

                    if (recoil < 1)
                        recoil = 1;
                    yield return CoroutineManager.Instance.StartCoroutine(context.User.InflictDamage(recoil));
                }
            }
        }
    }

    /// <summary>
    /// Event that reflects damage back to the user if the action's skill category matches the specified skill category
    /// </summary>
    [Serializable]
    public class CounterCategoryEvent : BattleEvent
    {
        /// <summary>
        /// The numerator of the damage reflected
        /// </summary>
        public int Numerator;

        /// <summary>
        /// The denominator of the damage reflected
        /// </summary>
        public int Denominator;

        /// <summary>
        /// The skill cateogory affected
        /// </summary>
        public BattleData.SkillCategory Category;

        /// <summary>
        /// The list of battle VFXs played if the condition is met
        /// </summary>
        public List<BattleAnimEvent> Anims;

        public CounterCategoryEvent() { Anims = new List<BattleAnimEvent>(); }
        public CounterCategoryEvent(BattleData.SkillCategory category, int numerator, int denominator, params BattleAnimEvent[] anims)
        {
            Category = category;
            Numerator = numerator;
            Denominator = denominator;
            Anims = new List<BattleAnimEvent>();
            Anims.AddRange(anims);
        }
        protected CounterCategoryEvent(CounterCategoryEvent other)
        {
            Numerator = other.Numerator;
            Denominator = other.Denominator;
            Category = other.Category;
            Anims = new List<BattleAnimEvent>();
            foreach (BattleAnimEvent anim in other.Anims)
                Anims.Add((BattleAnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new CounterCategoryEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            if (context.ActionType != BattleActionType.Skill)
                yield break;
            if (Category != BattleData.SkillCategory.None && context.Data.Category != Category)
                yield break;
            if (context.User.Dead)
                yield break;
            if (DungeonScene.Instance.GetMatchup(context.User, context.Target) == Alignment.Self)
                yield break;

            int damage = context.GetContextStateInt<DamageDealt>(0);
            if (damage > 0)
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_REFLECT").ToLocal()));

                HitAndRunState cancel;
                if (context.User.CharStates.TryGet(out cancel))
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_HIT_AND_RUN").ToLocal(), context.User.GetDisplayName(false), ((ItemEntrySummary)DataManager.Instance.DataIndices[DataManager.DataType.Item].Get(cancel.OriginItem)).GetIconName()));
                else
                {
                    foreach (BattleAnimEvent anim in Anims)
                        yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                    int recoil = damage * Numerator / Denominator;

                    if (recoil < 1)
                        recoil = 1;
                    yield return CoroutineManager.Instance.StartCoroutine(context.User.InflictDamage(recoil));
                }
            }
        }
    }


    /// <summary>
    /// Event that reflects damage back to the user if the battle action was a regular attack or thrown item.
    /// </summary>
    [Serializable]
    public class CounterNonSkillEvent : BattleEvent
    {
        /// <summary>
        /// The numerator of the damage reflected
        /// </summary>
        public int Numerator;

        /// <summary>
        /// The denominator of the damage reflected
        /// </summary>
        public int Denominator;

        /// <summary>
        /// The list of battle VFXs played if the condition is met
        /// </summary>
        public List<BattleAnimEvent> Anims;

        public CounterNonSkillEvent() { Anims = new List<BattleAnimEvent>(); }
        public CounterNonSkillEvent(int numerator, int denominator, params BattleAnimEvent[] anims)
        {
            Numerator = numerator;
            Denominator = denominator;
            Anims = new List<BattleAnimEvent>();
            Anims.AddRange(anims);
        }
        protected CounterNonSkillEvent(CounterNonSkillEvent other)
        {
            Numerator = other.Numerator;
            Denominator = other.Denominator;
            Anims = new List<BattleAnimEvent>();
            foreach (BattleAnimEvent anim in other.Anims)
                Anims.Add((BattleAnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new CounterNonSkillEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int damage = context.GetContextStateInt<DamageDealt>(0);
            if (damage > 0 && (context.ActionType == BattleActionType.Throw || (context.ActionType == BattleActionType.Skill && context.UsageSlot == BattleContext.DEFAULT_ATTACK_SLOT)) && DungeonScene.Instance.GetMatchup(context.User, context.Target) != Alignment.Self)
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_REFLECT_BY").ToLocal(), ownerChar.GetDisplayName(false), owner.GetDisplayName()));

                HitAndRunState cancel;
                if (context.User.CharStates.TryGet(out cancel))
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_HIT_AND_RUN").ToLocal(), context.User.GetDisplayName(false), ((ItemEntrySummary)DataManager.Instance.DataIndices[DataManager.DataType.Item].Get(cancel.OriginItem)).GetIconName()));
                else
                {
                    foreach (BattleAnimEvent anim in Anims)
                        yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                    int recoil = damage * Numerator / Denominator;

                    if (recoil < 1)
                        recoil = 1;
                    yield return CoroutineManager.Instance.StartCoroutine(context.User.InflictDamage(recoil));
                }
            }
        }
    }


    /// <summary>
    /// Event that deals damage to the character if the enemy that used the status also takes damage
    /// This event can only be used in statuses  
    /// </summary> 
    [Serializable]
    public class DestinyBondEvent : BattleEvent
    {
        public DestinyBondEvent() { }
        public override GameEvent Clone() { return new DestinyBondEvent(); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int damage = context.GetContextStateInt<DamageDealt>(0);
            Character target = ((StatusEffect)owner).TargetChar;
            if (damage > 0 && target != null)
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_DESTINY_BOND").ToLocal(), context.Target.GetDisplayName(false), target.GetDisplayName(false)));

                HitAndRunState cancel;
                if (target.CharStates.TryGet(out cancel))
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_HIT_AND_RUN").ToLocal(), target.GetDisplayName(false), ((ItemEntrySummary)DataManager.Instance.DataIndices[DataManager.DataType.Item].Get(cancel.OriginItem)).GetIconName()));
                else
                {
                    yield return CoroutineManager.Instance.StartCoroutine(target.InflictDamage(damage));
                }
            }
        }
    }


    /// <summary>
    /// Event that reflects the HP healed back to the user
    /// </summary>
    [Serializable]
    public class CounterHealEvent : BattleEvent
    {
        /// <summary>
        /// The numerator of the HP reflected
        /// </summary>
        public int Numerator;

        /// <summary>
        /// The denominator of the HP reflected
        /// </summary>
        public int Denominator;

        /// <summary>
        /// The list of battle VFXs played if the condition is met
        /// </summary>
        public List<BattleAnimEvent> Anims;

        public CounterHealEvent() { Anims = new List<BattleAnimEvent>(); }
        public CounterHealEvent(int numerator, int denominator, params BattleAnimEvent[] anims)
        {
            Numerator = numerator;
            Denominator = denominator;
            Anims = new List<BattleAnimEvent>();
            Anims.AddRange(anims);
        }
        protected CounterHealEvent(CounterHealEvent other)
        {
            Numerator = other.Numerator;
            Denominator = other.Denominator;
            Anims = new List<BattleAnimEvent>();
            foreach (BattleAnimEvent anim in other.Anims)
                Anims.Add((BattleAnimEvent)anim.Clone());
        }
        public override GameEvent Clone() { return new CounterHealEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int damage = context.GetContextStateInt<DamageHealedTarget>(0);
            if (damage > 0 && (context.ActionType == BattleActionType.Throw || context.ActionType == BattleActionType.Skill || context.ActionType == BattleActionType.Item) && DungeonScene.Instance.GetMatchup(context.User, context.Target) != Alignment.Self)
            {
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_REFLECT_HEAL_BY").ToLocal(), ownerChar.GetDisplayName(false), owner.GetDisplayName()));

                foreach (BattleAnimEvent anim in Anims)
                    yield return CoroutineManager.Instance.StartCoroutine(anim.Apply(owner, ownerChar, context));

                int recoil = damage * Numerator / Denominator;
                if (recoil < 1)
                    recoil = 1;
                yield return CoroutineManager.Instance.StartCoroutine(context.User.RestoreHP(recoil));
            }
        }
    }

}

