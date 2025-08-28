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
    // Battle events that deal with a character's progression, including EXP, levels, stats


    /// <summary>
    /// Event that boosts the character's stat depending on the effectiveness of the specified type to the character's type.
    /// Super effective: Defense and Special Defense
    /// Not effective: Attack and Special Attack
    /// Neutral: Speed and HP
    /// Same type: Boost all stats
    /// </summary>
    [Serializable]
    public class GummiEvent : BattleEvent
    {

        /// <summary>
        /// The gummi type
        /// </summary>
        [JsonConverter(typeof(ElementConverter))]
        [DataType(0, DataManager.DataType.Element, false)]
        public string TargetElement;

        /// <summary>
        /// If checked, must be base type not current type
        /// </summary>
        public bool RequireBase;

        public GummiEvent() { TargetElement = ""; }
        public GummiEvent(string element)
        {
            TargetElement = element;
        }
        protected GummiEvent(GummiEvent other)
        {
            TargetElement = other.TargetElement;
            RequireBase = other.RequireBase;
        }
        public override GameEvent Clone() { return new GummiEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            MonsterID formData = context.Target.BaseForm;
            BaseMonsterForm form = DataManager.Instance.GetMonster(formData.Species).Forms[formData.Form];

            string element1 = context.Target.Element1;
            string element2 = context.Target.Element2;

            if (RequireBase)
            {
                element1 = form.Element1;
                element2 = form.Element2;
            }

            int typeMatchup = PreTypeEvent.CalculateTypeMatchup(TargetElement, element1);
            typeMatchup += PreTypeEvent.CalculateTypeMatchup(TargetElement, element2);

            int heal = 5;
            List<Stat> stats = new List<Stat>();
            if (TargetElement == DataManager.Instance.DefaultElement || element1 == TargetElement || element2 == TargetElement)
            {
                heal = 20;
                stats.Add(Stat.HP);
                stats.Add(Stat.Attack);
                stats.Add(Stat.Defense);
                stats.Add(Stat.MAtk);
                stats.Add(Stat.MDef);
                stats.Add(Stat.Speed);
            }
            else if (typeMatchup < PreTypeEvent.NRM_2)
            {
                heal = 10;
                stats.Add(Stat.Attack);
                stats.Add(Stat.MAtk);
            }
            else if (typeMatchup > PreTypeEvent.NRM_2)
            {
                heal = 10;
                stats.Add(Stat.Defense);
                stats.Add(Stat.MDef);
            }
            else
            {
                heal = 5;
                stats.Add(Stat.HP);
                stats.Add(Stat.Speed);
            }

            foreach (Stat stat in stats)
                AddStat(stat, context);

            if (heal > 15)
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_HUNGER_FILL").ToLocal(), context.Target.GetDisplayName(false)));
            else if (heal > 5)
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_HUNGER_FILL_MIN").ToLocal(), context.Target.GetDisplayName(false)));

            context.Target.Fullness += heal;

            if (context.Target.Fullness >= context.Target.MaxFullness)
            {
                context.Target.Fullness = context.Target.MaxFullness;
                context.Target.FullnessRemainder = 0;
            }

            yield break;
        }

        private void AddStat(Stat stat, BattleContext context)
        {
            int prevStat = 0;
            int newStat = 0;
            switch (stat)
            {
                case Stat.HP:
                    if (context.Target.MaxHPBonus < MonsterFormData.MAX_STAT_BOOST)
                    {
                        prevStat = context.Target.MaxHP;
                        context.Target.MaxHPBonus++;
                        newStat = context.Target.MaxHP;
                    }
                    break;
                case Stat.Attack:
                    if (context.Target.AtkBonus < MonsterFormData.MAX_STAT_BOOST)
                    {
                        prevStat = context.Target.BaseAtk;
                        context.Target.AtkBonus++;
                        newStat = context.Target.BaseAtk;
                    }
                    break;
                case Stat.Defense:
                    if (context.Target.DefBonus < MonsterFormData.MAX_STAT_BOOST)
                    {
                        prevStat = context.Target.BaseDef;
                        context.Target.DefBonus++;
                        newStat = context.Target.BaseDef;
                    }
                    break;
                case Stat.MAtk:
                    if (context.Target.MAtkBonus < MonsterFormData.MAX_STAT_BOOST)
                    {
                        prevStat = context.Target.BaseMAtk;
                        context.Target.MAtkBonus++;
                        newStat = context.Target.BaseMAtk;
                    }
                    break;
                case Stat.MDef:
                    if (context.Target.MDefBonus < MonsterFormData.MAX_STAT_BOOST)
                    {
                        prevStat = context.Target.BaseMDef;
                        context.Target.MDefBonus++;
                        newStat = context.Target.BaseMDef;
                    }
                    break;
                case Stat.Speed:
                    if (context.Target.SpeedBonus < MonsterFormData.MAX_STAT_BOOST)
                    {
                        prevStat = context.Target.BaseSpeed;
                        context.Target.SpeedBonus++;
                        newStat = context.Target.BaseSpeed;
                    }
                    break;
            }
            if (newStat - prevStat > 0)
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_STAT_BOOST").ToLocal(), context.Target.GetDisplayName(false), stat.ToLocal(), (newStat - prevStat).ToString()));
        }
    }

    /// <summary>
    /// Normally raises one stat. Also raises other stats if matching type.
    /// Matching = main stat + 2, other stats + 1, 
    /// Super-effective = main stat + 2, two other stats (top 2 of the species) + 1
    /// Normal effect = main stat + 2
    /// NVE = main stat + 1
    /// Immune = nothing
    /// </summary>
    [Serializable]
    public class VitaGummiEvent : BattleEvent
    {

        /// <summary>
        /// The gummi type
        /// </summary>
        [JsonConverter(typeof(ElementConverter))]
        [DataType(0, DataManager.DataType.Element, false)]
        public string TargetElement;

        /// <summary>
        /// If checked, must be base type not current type
        /// </summary>
        public bool RequireBase;

        /// <summary>
        /// The stat to boost
        /// </summary>
        public Stat BoostedStat;

        /// <summary>
        /// If checked, changes super-effective and matching type to the following:
        /// Matching = All stats + 2
        /// Super-effective = main stat + 2, other stats + 1
        /// </summary>
        public bool FullEffect;

        public VitaGummiEvent() { TargetElement = ""; }
        public VitaGummiEvent(string element, bool requireBase, Stat defaultStat)
        {
            TargetElement = element;
            RequireBase = requireBase;
            BoostedStat = defaultStat;
        }
        protected VitaGummiEvent(VitaGummiEvent other)
        {
            TargetElement = other.TargetElement;
            RequireBase = other.RequireBase;
            BoostedStat = other.BoostedStat;
            FullEffect = other.FullEffect;
        }
        public override GameEvent Clone() { return new VitaGummiEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            MonsterID formData = context.Target.BaseForm;
            MonsterFormData form = (MonsterFormData)DataManager.Instance.GetMonster(formData.Species).Forms[formData.Form];

            string element1 = context.Target.Element1;
            string element2 = context.Target.Element2;

            if (RequireBase)
            {
                element1 = form.Element1;
                element2 = form.Element2;
            }

            int typeMatchup = PreTypeEvent.CalculateTypeMatchup(TargetElement, element1);
            typeMatchup += PreTypeEvent.CalculateTypeMatchup(TargetElement, element2);

            int heal;
            int mainAdd;
            int subAdd;
            List<Stat> stats = new List<Stat>();
            if (TargetElement == DataManager.Instance.DefaultElement || element1 == TargetElement || element2 == TargetElement)
            {
                heal = 20;
                mainAdd = 2;
                if (FullEffect)
                    subAdd = 2;
                else
                    subAdd = 1;
                stats.Add(Stat.HP);
                stats.Add(Stat.Attack);
                stats.Add(Stat.Defense);
                stats.Add(Stat.MAtk);
                stats.Add(Stat.MDef);
                stats.Add(Stat.Speed);
            }
            else if (typeMatchup >= PreTypeEvent.S_E_2)
            {
                heal = 15;
                mainAdd = 2;
                subAdd = 1;

                stats.Add(Stat.HP);
                stats.Add(Stat.Attack);
                stats.Add(Stat.Defense);
                stats.Add(Stat.MAtk);
                stats.Add(Stat.MDef);
                stats.Add(Stat.Speed);

                if (!FullEffect)
                {
                    List<Stat> baseStats = new List<Stat>();
                    foreach (Stat stat in stats)
                    {
                        if (stat == BoostedStat)
                            continue;
                        baseStats.Add(stat);
                    }

                    //get a sorted list of highest base stats other than the boosted stat
                    baseStats.Sort((a, b) => form.GetBaseStat(b).CompareTo(form.GetBaseStat(a)));

                    //delete the stats from the main stat list that aren't the boosted stat, or the top two in the baseStats
                    for (int ii = stats.Count - 1; ii >= 0; ii--)
                    {
                        if (stats[ii] == BoostedStat)
                            continue;
                        if (stats[ii] == baseStats[0] || stats[ii] == baseStats[1])
                            continue;
                        stats.RemoveAt(ii);
                    }
                }
            }
            else if (typeMatchup == PreTypeEvent.NRM_2)
            {
                heal = 10;
                mainAdd = 2;
                subAdd = 0;
                stats.Add(BoostedStat);
            }
            else if (typeMatchup > PreTypeEvent.N_E_2)
            {
                heal = 5;
                mainAdd = 1;
                subAdd = 0;
                stats.Add(BoostedStat);
            }
            else
            {
                heal = 5;
                mainAdd = 0;
                subAdd = 0;
            }

            foreach (Stat stat in stats)
                AddStat(stat, stat == BoostedStat ? mainAdd : subAdd, context);

            if (heal > 15)
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_HUNGER_FILL").ToLocal(), context.Target.GetDisplayName(false)));
            else if (heal > 5)
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_HUNGER_FILL_MIN").ToLocal(), context.Target.GetDisplayName(false)));

            context.Target.Fullness += heal;

            if (context.Target.Fullness >= context.Target.MaxFullness)
            {
                context.Target.Fullness = context.Target.MaxFullness;
                context.Target.FullnessRemainder = 0;
            }

            yield break;
        }

        private void AddStat(Stat stat, int amount, BattleContext context)
        {
            int prevStat = 0;
            int newStat = 0;
            switch (stat)
            {
                case Stat.HP:
                    if (context.Target.MaxHPBonus < MonsterFormData.MAX_STAT_BOOST)
                    {
                        prevStat = context.Target.MaxHP;
                        context.Target.MaxHPBonus = Math.Min(context.Target.MaxHPBonus + amount, MonsterFormData.MAX_STAT_BOOST);
                        newStat = context.Target.MaxHP;
                    }
                    break;
                case Stat.Attack:
                    if (context.Target.AtkBonus < MonsterFormData.MAX_STAT_BOOST)
                    {
                        prevStat = context.Target.BaseAtk;
                        context.Target.AtkBonus = Math.Min(context.Target.AtkBonus + amount, MonsterFormData.MAX_STAT_BOOST);
                        newStat = context.Target.BaseAtk;
                    }
                    break;
                case Stat.Defense:
                    if (context.Target.DefBonus < MonsterFormData.MAX_STAT_BOOST)
                    {
                        prevStat = context.Target.BaseDef;
                        context.Target.DefBonus = Math.Min(context.Target.DefBonus + amount, MonsterFormData.MAX_STAT_BOOST);
                        newStat = context.Target.BaseDef;
                    }
                    break;
                case Stat.MAtk:
                    if (context.Target.MAtkBonus < MonsterFormData.MAX_STAT_BOOST)
                    {
                        prevStat = context.Target.BaseMAtk;
                        context.Target.MAtkBonus = Math.Min(context.Target.MAtkBonus + amount, MonsterFormData.MAX_STAT_BOOST);
                        newStat = context.Target.BaseMAtk;
                    }
                    break;
                case Stat.MDef:
                    if (context.Target.MDefBonus < MonsterFormData.MAX_STAT_BOOST)
                    {
                        prevStat = context.Target.BaseMDef;
                        context.Target.MDefBonus = Math.Min(context.Target.MDefBonus + amount, MonsterFormData.MAX_STAT_BOOST);
                        newStat = context.Target.BaseMDef;
                    }
                    break;
                case Stat.Speed:
                    if (context.Target.SpeedBonus < MonsterFormData.MAX_STAT_BOOST)
                    {
                        prevStat = context.Target.BaseSpeed;
                        context.Target.SpeedBonus = Math.Min(context.Target.SpeedBonus + amount, MonsterFormData.MAX_STAT_BOOST);
                        newStat = context.Target.BaseSpeed;
                    }
                    break;
            }
            if (newStat - prevStat > 0)
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_STAT_BOOST").ToLocal(), context.Target.GetDisplayName(false), stat.ToLocal(), (newStat - prevStat).ToString()));
        }


        [OnDeserialized]
        internal void OnDeserializedMethod(StreamingContext context)
        {
            //TODO: remove on v1.1
            if (Serializer.OldVersion < new Version(0, 7, 25))
            {
                FullEffect = true;
            }
        }
    }

    /// <summary>
    /// Event that boosts the specified stat by the specified amount
    /// </summary>
    [Serializable]
    public class VitaminEvent : BattleEvent
    {

        /// <summary>
        /// The stat to boost
        /// </summary>
        public Stat BoostedStat;

        /// <summary>
        /// The boost amount 
        /// </summary>
        public int Change;

        /// <summary>
        /// If the stat didn't change, keep adding to the stat until it does.
        /// </summary>
        public bool ForceDiff;

        public VitaminEvent() { }
        public VitaminEvent(Stat stat, int change)
        {
            BoostedStat = stat;
            Change = change;
        }
        protected VitaminEvent(VitaminEvent other)
        {
            BoostedStat = other.BoostedStat;
            Change = other.Change;
            ForceDiff = other.ForceDiff;
        }
        public override GameEvent Clone() { return new VitaminEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Loc boosted = Loc.Zero;
            if (BoostedStat > Stat.None)
                boosted += boostStat(BoostedStat, context.Target);
            else
            {
                boosted += boostStat(Stat.HP, context.Target);
                boosted += boostStat(Stat.Attack, context.Target);
                boosted += boostStat(Stat.Defense, context.Target);
                boosted += boostStat(Stat.MAtk, context.Target);
                boosted += boostStat(Stat.MDef, context.Target);
                boosted += boostStat(Stat.Speed, context.Target);
            }
            if (boosted.Y == 0)
                DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_NOTHING_HAPPENED").ToLocal()));
            else if (boosted.X == 0)
            {
                if (BoostedStat > Stat.None)
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_STAT_BOOST_MIN").ToLocal(), context.Target.GetDisplayName(false), BoostedStat.ToLocal()));
                else
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_STAT_BOOST_MULTI_MIN").ToLocal(), context.Target.GetDisplayName(false)));
            }
            yield break;
        }

        private Loc boostStat(Stat stat, Character target)
        {
            int change = Change;

            int prevStat = 0;
            int newStat = 0;
            int prevBoost = 0;
            int newBoost = 0;

            //continue to increment the bonus until a stat increase is seen
            switch (stat)
            {
                case Stat.HP:
                    prevStat = target.MaxHP;
                    prevBoost = target.MaxHPBonus;
                    target.MaxHPBonus = Math.Min(target.MaxHPBonus + change, MonsterFormData.MAX_STAT_BOOST);
                    if (ForceDiff)
                    {
                        while (target.MaxHP == prevStat && target.MaxHPBonus < MonsterFormData.MAX_STAT_BOOST)
                            target.MaxHPBonus++;
                    }
                    newStat = target.MaxHP;
                    newBoost = target.MaxHPBonus;
                    break;
                case Stat.Attack:
                    prevStat = target.BaseAtk;
                    prevBoost = target.AtkBonus;
                    target.AtkBonus = Math.Min(target.AtkBonus + change, MonsterFormData.MAX_STAT_BOOST);
                    if (ForceDiff)
                    {
                        while (target.BaseAtk == prevStat && target.AtkBonus < MonsterFormData.MAX_STAT_BOOST)
                            target.AtkBonus++;
                    }
                    newStat = target.BaseAtk;
                    newBoost = target.AtkBonus;
                    break;
                case Stat.Defense:
                    prevStat = target.BaseDef;
                    prevBoost = target.DefBonus;
                    target.DefBonus = Math.Min(target.DefBonus + change, MonsterFormData.MAX_STAT_BOOST);
                    if (ForceDiff)
                    {
                        while (target.BaseDef == prevStat && target.DefBonus < MonsterFormData.MAX_STAT_BOOST)
                            target.DefBonus++;
                    }
                    newStat = target.BaseDef;
                    newBoost = target.DefBonus;
                    break;
                case Stat.MAtk:
                    prevStat = target.BaseMAtk;
                    prevBoost = target.MAtkBonus;
                    target.MAtkBonus = Math.Min(target.MAtkBonus + change, MonsterFormData.MAX_STAT_BOOST);
                    if (ForceDiff)
                    {
                        while (target.BaseMAtk == prevStat && target.MAtkBonus < MonsterFormData.MAX_STAT_BOOST)
                            target.MAtkBonus++;
                    }
                    newStat = target.BaseMAtk;
                    newBoost = target.MAtkBonus;
                    break;
                case Stat.MDef:
                    prevStat = target.BaseMDef;
                    prevBoost = target.MDefBonus;
                    target.MDefBonus = Math.Min(target.MDefBonus + change, MonsterFormData.MAX_STAT_BOOST);
                    if (ForceDiff)
                    {
                        while (target.BaseMDef == prevStat && target.MDefBonus < MonsterFormData.MAX_STAT_BOOST)
                            target.MDefBonus++;
                    }
                    newStat = target.BaseMDef;
                    newBoost = target.MDefBonus;
                    break;
                case Stat.Speed:
                    prevStat = target.BaseSpeed;
                    prevBoost = target.SpeedBonus;
                    target.SpeedBonus = Math.Min(target.SpeedBonus + change, MonsterFormData.MAX_STAT_BOOST);
                    if (ForceDiff)
                    {
                        while (target.BaseSpeed == prevStat && target.SpeedBonus < MonsterFormData.MAX_STAT_BOOST)
                            target.SpeedBonus++;
                    }
                    newStat = target.BaseSpeed;
                    newBoost = target.SpeedBonus;
                    break;
            }
            if (newBoost > prevBoost)
            {
                if (newStat > prevStat)
                    DungeonScene.Instance.LogMsg(Text.FormatGrammar(new StringKey("MSG_STAT_BOOST").ToLocal(), target.GetDisplayName(false), stat.ToLocal(), (newStat - prevStat).ToString()));
            }
            return new Loc(newStat - prevStat, newBoost - prevBoost);
        }


        [OnDeserialized]
        internal void OnDeserializedMethod(StreamingContext context)
        {
            //TODO: remove on v1.1
            if (Serializer.OldVersion < new Version(0, 7, 25))
            {
                ForceDiff = true;
            }
        }
    }

    /// <summary>
    /// Event that changes the character's level by the specified amount 
    /// </summary>
    [Serializable]
    public class LevelChangeEvent : BattleEvent
    {
        /// <summary>
        /// The level change
        /// </summary> 
        public int Level;

        /// <summary>
        /// Whether to affect the target or user
        /// </summary>
        public bool AffectTarget;

        public LevelChangeEvent() { }
        public LevelChangeEvent(int level, bool affectTarget)
        {
            Level = level;
            AffectTarget = affectTarget;
        }
        protected LevelChangeEvent(LevelChangeEvent other)
        {
            Level = other.Level;
            AffectTarget = other.AffectTarget;
        }
        public override GameEvent Clone() { return new LevelChangeEvent(this); }

        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            Character target = (AffectTarget ? context.Target : context.User);

            if (target.Dead)
                yield break;

            target.EXP = 0;
            string growth = DataManager.Instance.GetMonster(target.BaseForm.Species).EXPTable;
            GrowthData growthData = DataManager.Instance.GetGrowth(growth);
            if (Level < 0)
            {
                int levelsChanged = 0;
                while (levelsChanged > Level && target.Level + levelsChanged > 1)
                {
                    target.EXP -= growthData.GetExpToNext(target.Level + levelsChanged - 1);
                    levelsChanged--;
                }
            }
            else if (Level > 0)
            {
                int levelsChanged = 0;
                while (levelsChanged < Level && target.Level + levelsChanged < DataManager.Instance.Start.MaxLevel)
                {
                    target.EXP += growthData.GetExpToNext(target.Level + levelsChanged);
                    levelsChanged++;
                }
            }
            DungeonScene.Instance.LevelGains.Add(ZoneManager.Instance.CurrentMap.GetCharIndex(target));
            yield break;
        }
    }

    /// <summary>
    /// Event that adds EXP to the character based on the damage dealt
    /// </summary>
    [Serializable]
    public class DamageEXPEvent : BattleEvent
    {
        public override GameEvent Clone() { return new DamageEXPEvent(); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            int damage = context.GetContextStateInt<DamageDealt>(0);
            int gainedExp = damage * 10;
            if (gainedExp > 0)
            {
                Team playerTeam = context.User.MemberTeam;
                foreach (Character player in playerTeam.EnumerateChars())
                {
                    if (player.Level < DataManager.Instance.Start.MaxLevel)
                    {
                        player.EXP += gainedExp;
                        DungeonScene.Instance.MeterChanged(player.CharLoc, gainedExp, true);

                        string growth = DataManager.Instance.GetMonster(player.BaseForm.Species).EXPTable;
                        GrowthData growthData = DataManager.Instance.GetGrowth(growth);
                        if (player.EXP >= growthData.GetExpToNext(player.Level) || player.EXP < 0)
                            DungeonScene.Instance.LevelGains.Add(ZoneManager.Instance.CurrentMap.GetCharIndex(context.User));
                    }
                }
            }
            yield break;
        }
    }

    /// <summary>
    /// Event that marks whether EXP can be gained from the target
    /// </summary>
    [Serializable]
    public class ToggleEXPEvent : BattleEvent
    {
        /// <summary>
        /// Whether to make target EXP marked or not
        /// </summary>
        public bool EXPMarked;

        public ToggleEXPEvent() { }
        public ToggleEXPEvent(bool exp) { EXPMarked = exp; }
        protected ToggleEXPEvent(ToggleEXPEvent other)
        {
            EXPMarked = other.EXPMarked;
        }
        public override GameEvent Clone() { return new ToggleEXPEvent(this); }
        public override IEnumerator<YieldInstruction> Apply(GameEventOwner owner, Character ownerChar, BattleContext context)
        {
            context.Target.EXPMarked = EXPMarked;
            yield break;
        }
    }
}

