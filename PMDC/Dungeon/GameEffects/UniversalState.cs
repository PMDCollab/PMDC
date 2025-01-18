using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using RogueEssence.Data;
using RogueEssence.Dev;
using RogueEssence.Dungeon;

namespace PMDC.Dungeon
{

    [Serializable]
    public class ElementTableState : UniversalState
    {
        public int[][] TypeMatchup;

        public int[] Effectiveness;

        public Dictionary<string, int> TypeMap;

        public ElementTableState() { TypeMatchup = new int[0][]; Effectiveness = new int[0]; TypeMap = new Dictionary<string, int>(); }
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
            TypeMap = new Dictionary<string, int>();
            foreach (string key in other.TypeMap.Keys)
                TypeMap[key] = other.TypeMap[key];
        }
        public override GameplayState Clone() { return new ElementTableState(this); }

        public int GetMatchup(string attacking, string defending)
        {
            int attackIdx = TypeMap[attacking];
            int defendIdx = TypeMap[defending];
            return TypeMatchup[attackIdx][defendIdx];
        }



        [OnDeserialized]
        internal void OnDeserializedMethod(StreamingContext context)
        {
            if (TypeMap.Count == 0)
            {
                TypeMap["none"] = 0;
                TypeMap["bug"] = 1;
                TypeMap["dark"] = 2;
                TypeMap["dragon"] = 3;
                TypeMap["electric"] = 4;
                TypeMap["fairy"] = 5;
                TypeMap["fighting"] = 6;
                TypeMap["fire"] = 7;
                TypeMap["flying"] = 8;
                TypeMap["ghost"] = 9;
                TypeMap["grass"] = 10;
                TypeMap["ground"] = 11;
                TypeMap["ice"] = 12;
                TypeMap["normal"] = 13;
                TypeMap["poison"] = 14;
                TypeMap["psychic"] = 15;
                TypeMap["rock"] = 16;
                TypeMap["steel"] = 17;
                TypeMap["water"] = 18;
            }
        }
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
        [DataType(0, DataManager.DataType.Skin, false)]
        public string AltColor;
        [DataType(0, DataManager.DataType.Skin, false)]
        public string Challenge;

        public SkinTableState() { AltColor = ""; Challenge = ""; }
        public SkinTableState(int odds, string altColor, string challenge) { AltColorOdds = odds; AltColor = altColor; Challenge = challenge; }
        protected SkinTableState(SkinTableState other)
        {
            AltColorOdds = other.AltColorOdds;
            AltColor = other.AltColor;
            Challenge = other.Challenge;
        }
        public override GameplayState Clone() { return new SkinTableState(this); }



        [OnDeserialized]
        internal void OnDeserializedMethod(StreamingContext context)
        {
            if (String.IsNullOrEmpty(AltColor))
                AltColor = "shiny";
            if (String.IsNullOrEmpty(Challenge))
                Challenge = "shiny_square";
        }
    }

    [Serializable]
    public class MonsterHouseTableState : UniversalState
    {
        /// <summary>
        /// If this is set, this tile will be used to display a warning for monster houses.
        /// </summary>
        [DataType(0, DataManager.DataType.Tile, false)]
        public string MonsterHouseWarningTile;
        
        /// <summary>
        /// If this is set, this tile will be used to replace the chest in a chest ambush.
        /// </summary>
        [DataType(0, DataManager.DataType.Tile, false)]
        public string ChestAmbushWarningTile;

        /// <summary>
        /// If this is set to true, monster halls will never appear on tiles where you can't see the warning tile
        /// </summary>
        public bool NoMonsterHallOnBlockLightTiles;
        
        /// <summary>
        /// If this is set to true, you will not be able to spawn into a monster house upon entering a floor.
        /// </summary>
        public bool NoMonsterHouseEntrances;

        public MonsterHouseTableState()
        {
            MonsterHouseWarningTile = null;
            ChestAmbushWarningTile = null;
            NoMonsterHallOnBlockLightTiles = false;
            NoMonsterHouseEntrances = false;
        }

        public MonsterHouseTableState(string monsterHouseWarningTile, string chestAmbushWarningTile, bool noMonsterHallOnBlockLightTiles, bool noMonsterHouseEntrances)
        {
            MonsterHouseWarningTile = monsterHouseWarningTile; 
            ChestAmbushWarningTile = chestAmbushWarningTile; 
            NoMonsterHallOnBlockLightTiles = noMonsterHallOnBlockLightTiles;
            NoMonsterHouseEntrances = noMonsterHouseEntrances;
        }
        protected MonsterHouseTableState(MonsterHouseTableState other)
        {
            MonsterHouseWarningTile = other.MonsterHouseWarningTile;
            ChestAmbushWarningTile = other.ChestAmbushWarningTile;
            NoMonsterHouseEntrances = other.NoMonsterHouseEntrances;
            NoMonsterHallOnBlockLightTiles = other.NoMonsterHallOnBlockLightTiles;
        }
        public override GameplayState Clone() { return new MonsterHouseTableState(this); }
    }
}
