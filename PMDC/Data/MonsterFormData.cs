using System;
using System.Collections.Generic;
using RogueElements;
using System.Drawing;
using System.Linq;
using RogueEssence;
using RogueEssence.Data;
using RogueEssence.Dungeon;
using RogueEssence.Dev;
using PMDC.Dungeon;
using Newtonsoft.Json;
using PMDC.Dev;
using System.Runtime.Serialization;

namespace PMDC.Data
{

    [Serializable]
    public class MonsterFormData : BaseMonsterForm
    {
        public const int MAX_STAT_BOOST = 256;

        /// <summary>
        /// What generation it was introduced in
        /// </summary>
        public int Generation;

        /// <summary>
        /// How often it appears as genderless (weight)
        /// </summary>
        public int GenderlessWeight;

        /// <summary>
        /// How often it appears as male (weight)
        /// </summary>
        public int MaleWeight;

        /// <summary>
        /// How often it appears as female (weight)
        /// </summary>
        public int FemaleWeight;

        /// <summary>
        /// Base HP stat
        /// </summary>
        public int BaseHP;

        /// <summary>
        /// Base attack stat
        /// </summary>
        public int BaseAtk;

        /// <summary>
        /// Base defense stat
        /// </summary>
        [SharedRow]
        public int BaseDef;

        /// <summary>
        /// Base special attack stat
        /// </summary>
        public int BaseMAtk;

        /// <summary>
        /// Base special defense stat
        /// </summary>
        [SharedRow]
        public int BaseMDef;

        /// <summary>
        /// Base speed stat
        /// </summary>
        public int BaseSpeed;

        /// <summary>
        /// Base EXP yield
        /// </summary>
        public int ExpYield;

        /// <summary>
        /// species/form height
        /// </summary>
        public double Height;

        /// <summary>
        /// species/form weight
        /// </summary>
        [SharedRow]
        public double Weight;

        /// <summary>
        /// Possible personalities (advanced)
        /// </summary>
        public List<byte> Personalities;

        /// <summary>
        /// Moves learned by TM
        /// </summary>
        public List<LearnableSkill> TeachSkills;

        /// <summary>
        /// Egg moves
        /// </summary>
        public List<LearnableSkill> SharedSkills;

        /// <summary>
        /// Tutor moves
        /// </summary>
        public List<LearnableSkill> SecretSkills;

        public MonsterFormData()
        {
            Personalities = new List<byte>();

            TeachSkills = new List<LearnableSkill>();
            SharedSkills = new List<LearnableSkill>();
            SecretSkills = new List<LearnableSkill>();
        }

        public int GetBaseStat(Stat stat)
        {
            switch (stat)
            {
                case Stat.HP:
                    return BaseHP;
                case Stat.Speed:
                    return BaseSpeed;
                case Stat.Attack:
                    return BaseAtk;
                case Stat.Defense:
                    return BaseDef;
                case Stat.MAtk:
                    return BaseMAtk;
                case Stat.MDef:
                    return BaseMDef;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Calculates stat based on level, stat type, and bonus
        /// </summary>
        /// <param name="level"></param>
        /// <param name="stat"></param>
        /// <param name="bonus"></param>
        /// <returns></returns>
        public override int GetStat(int level, Stat stat, int bonus)
        {
            int curStat = getMinStat(level, stat);
            int minStat = getMinStat(DataManager.Instance.Start.MaxLevel, stat);
            int maxStat = GetMaxStat(stat, DataManager.Instance.Start.MaxLevel);
            int statDiff = maxStat - minStat;

            return Math.Max(1, curStat + bonus * statDiff / MonsterFormData.MAX_STAT_BOOST);
        }

        /// <summary>
        /// Rolls a random skin (shinyness) this monster can spawn with
        /// </summary>
        /// <param name="rand"></param>
        /// <returns></returns>
        public override string RollSkin(IRandom rand)
        {
            SkinTableState table = DataManager.Instance.UniversalEvent.UniversalStates.GetWithDefault<SkinTableState>();
            if (table.AltColorOdds == 0)
                return DataManager.Instance.DefaultSkin;
            if (rand.Next(table.AltColorOdds) == 0)
                return table.AltColor;
            return DataManager.Instance.DefaultSkin;
        }

        /// <summary>
        /// Gets a personality type given an integer (advanced)
        /// </summary>
        /// <param name="discriminator"></param>
        /// <returns></returns>
        public override int GetPersonalityType(int discriminator)
        {
            return Personalities[discriminator / 256 % Personalities.Count];
        }

        /// <summary>
        /// Rolls a possible gender this monster can spawn as
        /// </summary>
        /// <param name="rand"></param>
        /// <returns></returns>
        public override Gender RollGender(IRandom rand)
        {
            int totalWeight = FemaleWeight + MaleWeight + GenderlessWeight;
            int roll = rand.Next(0, totalWeight);
            if (roll < FemaleWeight)
                return Gender.Female;
            roll -= FemaleWeight;
            if (roll < MaleWeight)
                return Gender.Male;
            
            return Gender.Genderless;
        }

        /// <summary>
        /// Rolls a random ability this monster can spawn as.  No hidden abilities.
        /// </summary>
        /// <param name="rand"></param>
        /// <param name="bounds"></param>
        /// <returns></returns>
        public override string RollIntrinsic(IRandom rand, int bounds)
        {
            List<string> abilities = new List<string>();
            abilities.Add(Intrinsic1);
            if (Intrinsic2 != DataManager.Instance.DefaultIntrinsic && bounds > 1)
                abilities.Add(Intrinsic2);
            if (Intrinsic3 != DataManager.Instance.DefaultIntrinsic && bounds > 2)
                abilities.Add(Intrinsic3);

            return abilities[rand.Next(abilities.Count)];
        }


        /// <summary>
        /// Gets the possible genders that can be rolled
        /// </summary>
        /// <returns></returns>
        public override List<Gender> GetPossibleGenders()
        {
            List<Gender> genders = new List<Gender>();

            if (MaleWeight > 0)
                genders.Add(Gender.Male);
            if (FemaleWeight > 0)
                genders.Add(Gender.Female);
            if (GenderlessWeight > 0 || genders.Count == 0)
                genders.Add(Gender.Genderless);
            return genders;
        }

        /// <summary>
        /// Gets the possible skins that can be rolled
        /// </summary>
        /// <returns></returns>
        public override List<string> GetPossibleSkins()
        {
            List<string> colors = new List<string>();

            SkinTableState table = DataManager.Instance.UniversalEvent.UniversalStates.GetWithDefault<SkinTableState>();
            colors.Add(DataManager.Instance.DefaultSkin);
            colors.Add(table.Challenge);

            return colors;
        }

        /// <summary>
        /// Gets the possible intrinsic slots that can be rolled
        /// </summary>
        /// <returns></returns>
        public override List<int> GetPossibleIntrinsicSlots()
        {
            List<int> abilities = new List<int>();

            abilities.Add(0);
            //if intrinsic cannot be achieved, default to first intrinsic
            if (Intrinsic2 != DataManager.Instance.DefaultIntrinsic)
                abilities.Add(1);
            if (Intrinsic3 != DataManager.Instance.DefaultIntrinsic)
                abilities.Add(2);

            return abilities;
        }
        
        // TODO: Consider moves from prior evolutions
        public List<string> GetPossibleSkills()
        {
            List<string> skills = new List<string>();
            skills.AddRange(LevelSkills.Select(x => x.Skill));
            skills.AddRange(TeachSkills.Select(x => x.Skill));
            skills.AddRange(SharedSkills.Select(x => x.Skill));
            skills.AddRange(SecretSkills.Select(x => x.Skill));
            return skills.Distinct().ToList();
        }


        private int getMinStat(int level, Stat stat)
        {
            switch (stat)
            {
                case Stat.HP:
                    return hpStatCalc(BaseHP, level);
                case Stat.Speed:
                    return genericStatCalc(BaseSpeed, level);
                case Stat.Attack:
                    return genericStatCalc(BaseAtk, level);
                case Stat.Defense:
                    return genericStatCalc(BaseDef, level);
                case Stat.MAtk:
                    return genericStatCalc(BaseMAtk, level);
                case Stat.MDef:
                    return genericStatCalc(BaseMDef, level);
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Gets its max stat for a given stat type
        /// </summary>
        /// <param name="stat"></param>
        /// <returns></returns>
        public override int GetMaxStat(Stat stat, int level)
        {
            switch (stat)
            {
                case Stat.HP:
                    return hpStatMax(BaseHP, level);
                case Stat.Speed:
                    return genericStatMax(BaseSpeed, level);
                case Stat.Attack:
                    return genericStatMax(BaseAtk, level);
                case Stat.Defense:
                    return genericStatMax(BaseDef, level);
                case Stat.MAtk:
                    return genericStatMax(BaseMAtk, level);
                case Stat.MDef:
                    return genericStatMax(BaseMDef, level);
                default:
                    return 0;
            }
        }

        public override int ReverseGetStat(Stat stat, int val, int level)
        {
            if (stat == Stat.HP)
                return (val - 10) * DataManager.Instance.Start.MaxLevel / level - 130;
            else
                return (val - 5) * DataManager.Instance.Start.MaxLevel / level - 30;
        }

        public override int GetMaxStatBonus(Stat stat)
        {
            return MAX_STAT_BOOST;
        }

        private int genericStatCalc(int baseStat, int level)
        {
            return (baseStat + 30) * level / DataManager.Instance.Start.MaxLevel + 5;
        }
        private int hpStatCalc(int baseStat, int level)
        {
            if (baseStat > 1)
                return (baseStat + 130) * level / DataManager.Instance.Start.MaxLevel + 10;
            else
                return (level / 10 + 1);
        }

        private int scaleStatTotal(int baseStat)
        {
            if (baseStat > 1)
            {
                if (BaseHP > 1)
                    return 1536 * baseStat / (BaseHP + BaseAtk + BaseDef + BaseMAtk + BaseMDef + BaseSpeed);
                else
                    return 1280 * baseStat / (BaseAtk + BaseDef + BaseMAtk + BaseMDef + BaseSpeed);
            }
            return 1;
        }

        private int genericStatMax(int baseStat, int level)
        {
            return genericStatCalc(scaleStatTotal(baseStat), level);
        }

        private int hpStatMax(int baseStat, int level)
        {
            if (baseStat > 1)
                return hpStatCalc(scaleStatTotal(baseStat), level);
            else
                return (level / 5 + 1);
        }

        /// <summary>
        /// Checks if it can learn the skill
        /// </summary>
        /// <param name="skill"></param>
        /// <returns></returns>
        public override bool CanLearnSkill(string skill)
        {
            if (LevelSkills.FindIndex(a => a.Skill == skill) > -1)
                return true;
            if (TeachSkills.FindIndex(a => a.Skill == skill) > -1)
                return true;
            if (SharedSkills.FindIndex(a => a.Skill == skill) > -1)
                return true;
            if (SecretSkills.FindIndex(a => a.Skill == skill) > -1)
                return true;
            return false;
        }

    }



}