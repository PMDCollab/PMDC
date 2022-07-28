using System;
using System.Collections.Generic;
using RogueElements;
using System.Drawing;
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
        public const int MAX_STAT_BOOST = 128;

        public int Generation;

        public int Ratio;
        public int GenderlessWeight;
        public int MaleWeight;
        public int FemaleWeight;

        public int BaseHP;

        public int BaseAtk;

        [SharedRow]
        public int BaseDef;

        public int BaseMAtk;

        [SharedRow]
        public int BaseMDef;

        public int BaseSpeed;

        public int ExpYield;

        public double Height;

        [SharedRow]
        public double Weight;

        public List<byte> Personalities;

        public List<LearnableSkill> TeachSkills;

        public List<LearnableSkill> SharedSkills;

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
        public override int GetStat(int level, Stat stat, int bonus)
        {
            int curStat = getMinStat(level, stat);
            int minStat = getMinStat(100, stat);
            int maxStat = GetMaxStat(stat);
            int statDiff = maxStat - minStat;

            return Math.Max(1, curStat + bonus * statDiff / MonsterFormData.MAX_STAT_BOOST);
        }

        public override int RollSkin(IRandom rand)
        {
            SkinTableState table = DataManager.Instance.UniversalEvent.UniversalStates.GetWithDefault<SkinTableState>();
            if (table.AltColorOdds == 0)
                return 0;
            return (rand.Next(table.AltColorOdds) == 0) ? 1 : 0;
        }

        public override int GetPersonalityType(int discriminator)
        {
            return Personalities[discriminator / 256 % Personalities.Count];
        }

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


        public override List<int> GetPossibleSkins()
        {
            List<int> colors = new List<int>();

            colors.Add(0);
            colors.Add(2);

            return colors;
        }

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


        public override int GetMaxStat(Stat stat)
        {
            switch (stat)
            {
                case Stat.HP:
                    return hpStatMax(BaseHP);
                case Stat.Speed:
                    return genericStatMax(BaseSpeed);
                case Stat.Attack:
                    return genericStatMax(BaseAtk);
                case Stat.Defense:
                    return genericStatMax(BaseDef);
                case Stat.MAtk:
                    return genericStatMax(BaseMAtk);
                case Stat.MDef:
                    return genericStatMax(BaseMDef);
                default:
                    return 0;
            }
        }

        public override int ReverseGetStat(Stat stat, int val, int level)
        {
            if (stat == Stat.HP)
                return (val - 10) * DataManager.Instance.MaxLevel / level - 130;
            else
                return (val - 5) * DataManager.Instance.MaxLevel / level - 30;
        }

        public override int GetMaxStatBonus(Stat stat)
        {
            return MAX_STAT_BOOST;
        }

        private int genericStatCalc(int baseStat, int level)
        {
            return (baseStat + 30) * level / DataManager.Instance.MaxLevel + 5;
        }
        private int hpStatCalc(int baseStat, int level)
        {
            if (baseStat > 1)
                return (baseStat + 130) * level / DataManager.Instance.MaxLevel + 10;
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

        private int genericStatMax(int baseStat)
        {
            return genericStatCalc(scaleStatTotal(baseStat), DataManager.Instance.MaxLevel);
        }

        private int hpStatMax(int baseStat)
        {
            if (baseStat > 1)
                return hpStatCalc(scaleStatTotal(baseStat), DataManager.Instance.MaxLevel);
            else
                return 21;
        }


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



        //TODO: Created v0.5.10, delete on v0.6.1
        [OnDeserialized]
        internal void OnDeserializedMethod(StreamingContext context)
        {
            if (Serializer.OldVersion <= new Version(0, 5, 9, 0))
            {
                if (Ratio == -1)
                {
                    GenderlessWeight = 1;
                    MaleWeight = 0;
                    FemaleWeight = 0;
                }
                else
                {
                    GenderlessWeight = 0;
                    MaleWeight = 8 - Ratio;
                    FemaleWeight = Ratio;
                }
            }
        }

    }



}