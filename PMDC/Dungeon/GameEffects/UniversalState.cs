using System;
using RogueElements;
using RogueEssence;
using RogueEssence.Dungeon;
using RogueEssence.LevelGen;

namespace PMDC.Dungeon
{

    [Serializable]
    public class ElementTableState : UniversalState
    {
        public int[][] TypeMatchup;

        public int[] Effectiveness;

        public ElementTableState() { TypeMatchup = new int[0][]; Effectiveness = new int[0]; }
        protected ElementTableState(ElementTableState other)
        {
            TypeMatchup = new int[other.TypeMatchup.Length][];
            for (int ii = 0; ii < TypeMatchup.Length; ii++)
            {
                TypeMatchup[ii] = new int[other.TypeMatchup[ii].Length];
                other.TypeMatchup[ii].CopyTo(TypeMatchup[ii], 0);
            }
            Effectiveness = new int[other.Effectiveness.Length];
            other.Effectiveness.CopyTo(Effectiveness, 0);
        }
        public override GameplayState Clone() { return new ElementTableState(this); }
    }

    [Serializable]
    public class AtkDefLevelTableState : UniversalState
    {
        public int MinAtk;
        public int MaxAtk;
        public int MinDef;
        public int MaxDef;

        public int AtkBase;
        public int DefBase;

        public int AtkLevelMult(int stat, int level)
        {
            int bound_level = Math.Min(Math.Max(MinAtk, level), MaxAtk);
            if (bound_level < 0)
                return stat * AtkBase / (AtkBase - level);
            else if (bound_level > 0)
                return stat * (AtkBase + level) / AtkBase;
            else
                return stat;
        }
        public int DefLevelMult(int stat, int level)
        {
            int bound_level = Math.Min(Math.Max(MinDef, level), MaxDef);
            if (bound_level < 0)
                return stat * DefBase / (DefBase - level);
            else if (bound_level > 0)
                return stat * (DefBase + level) / DefBase;
            else
                return stat;
        }

        public AtkDefLevelTableState() { }
        public AtkDefLevelTableState(int minAtk, int maxAtk, int minDef, int maxDef, int atkBase, int defBase)
        {
            MinAtk = minAtk;
            MaxAtk = maxAtk;
            MinDef = minDef;
            MaxDef = maxDef;

            AtkBase = atkBase;
            DefBase = defBase;
        }
        protected AtkDefLevelTableState(AtkDefLevelTableState other)
        {
            MinAtk = other.MinAtk;
            MaxAtk = other.MaxAtk;
            MinDef = other.MinDef;
            MaxDef = other.MaxDef;

            AtkBase = other.AtkBase;
            DefBase = other.DefBase;
        }
        public override GameplayState Clone() { return new AtkDefLevelTableState(this); }
    }

    [Serializable]
    public class CritRateLevelTableState : UniversalState
    {
        public int[] CritLevels;

        public int GetCritChance(int level)
        {
            int bound_level = Math.Min(Math.Max(0, level), CritLevels.Length - 1);
            return CritLevels[bound_level];
        }

        public CritRateLevelTableState() { CritLevels = new int[0]; }
        protected CritRateLevelTableState(CritRateLevelTableState other)
        {
            CritLevels = new int[other.CritLevels.Length];
            other.CritLevels.CopyTo(CritLevels, 0);
        }
        public override GameplayState Clone() { return new CritRateLevelTableState(this); }
    }

    [Serializable]
    public class HitRateLevelTableState : UniversalState
    {
        public int[] AccuracyLevels;
        public int[] EvasionLevels;

        public int MinAccuracy;
        public int MaxAccuracy;
        public int MinEvasion;
        public int MaxEvasion;

        public int ApplyAccuracyMod(int baseAcc, int statStage)
        {
            int bound_level = Math.Min(Math.Max(0, statStage - MinAccuracy), AccuracyLevels.Length - 1);
            return baseAcc * AccuracyLevels[bound_level];
        }

        public int ApplyEvasionMod(int baseAcc, int statStage)
        {
            int bound_level = Math.Min(Math.Max(0, statStage - MinEvasion), EvasionLevels.Length - 1);
            return baseAcc * EvasionLevels[bound_level];
        }

        public HitRateLevelTableState() { AccuracyLevels = new int[0]; EvasionLevels = new int[0]; }
        public HitRateLevelTableState(int minAcc, int maxAcc, int minEvade, int maxEvade) : this()
        {
            MinAccuracy = minAcc;
            MaxAccuracy = maxAcc;
            MinEvasion = minEvade;
            MaxEvasion = maxEvade;
        }
        public HitRateLevelTableState(HitRateLevelTableState other)
        {
            AccuracyLevels = new int[other.AccuracyLevels.Length];
            other.AccuracyLevels.CopyTo(AccuracyLevels, 0);
            EvasionLevels = new int[other.EvasionLevels.Length];
            other.EvasionLevels.CopyTo(EvasionLevels, 0);
            MinAccuracy = other.MinAccuracy;
            MaxAccuracy = other.MaxAccuracy;
            MinEvasion = other.MinEvasion;
            MaxEvasion = other.MaxEvasion;
        }

        public override GameplayState Clone() { return new HitRateLevelTableState(this); }
    }

    [Serializable]
    public class SkinTableState : UniversalState
    {
        public int AltColorOdds;

        public SkinTableState() { }
        public SkinTableState(int odds) { AltColorOdds = odds; }
        protected SkinTableState(SkinTableState other)
        {
            AltColorOdds = other.AltColorOdds;
        }
        public override GameplayState Clone() { return new SkinTableState(this); }
    }

}
