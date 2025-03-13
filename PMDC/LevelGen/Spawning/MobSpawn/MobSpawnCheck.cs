using System;
using RogueEssence.Dungeon;
using RogueEssence.Data;
using RogueElements;
using RogueEssence.LevelGen;
using PMDC.Data;
using System.Collections.Generic;
using RogueEssence.Dev;
using RogueEssence.Script;
using NLua;
using System.Linq;

namespace PMDC.LevelGen
{
    /// <summary>
    /// Spawns the mob if the player's random seed has a specific remainder when divided by a specific number.
    /// </summary>
    [Serializable]
    public class MobCheckVersionDiff : MobSpawnCheck
    {
        /// <summary>
        /// The number to divide the player's seed by.
        /// </summary>
        public int Div;

        /// <summary>
        /// The remainder to check for when dividing the player's seed.
        /// </summary>
        public int Remainder;

        public MobCheckVersionDiff()
        {

        }
        public MobCheckVersionDiff(int remainder, int div)
        {
            Div = div;
            Remainder = remainder;
        }
        public MobCheckVersionDiff(MobCheckVersionDiff other) : this()
        {
            Remainder = other.Remainder;
            Div = other.Div;
        }
        public override MobSpawnCheck Copy() { return new MobCheckVersionDiff(this); }

        public override bool CanSpawn()
        {
            return DataManager.Instance.Save.Rand.FirstSeed % (ulong)Div == (ulong)Remainder;
        }

    }
    /// <summary>
    /// Spawns the mob if the player's savevar is true.
    /// </summary>
    [Serializable]
    public class MobCheckSaveVar : MobSpawnCheck
    {
        /// <summary>
        /// The savevar to query
        /// </summary>
        public string SaveVar;

        /// <summary>
        /// The the status to compare to in order to allow the spawn to occur.
        /// if it's set to true, it'll spawn only if the savevar is set to true
        /// if it's set to false, it'll spawn only if the savevar is set to false
        /// </summary>
        public bool Status;

        public MobCheckSaveVar()
        {
        }

        public MobCheckSaveVar(string saveVar, bool status)
        {
            SaveVar = saveVar;
            Status = status;
        }
        public MobCheckSaveVar(MobCheckSaveVar other) : this()
        {
            SaveVar = other.SaveVar;
            Status = other.Status;
        }
        public override MobSpawnCheck Copy() { return new MobCheckSaveVar(this); }

        public override bool CanSpawn()
        {
            object obj = LuaEngine.Instance.LuaState[LuaEngine.SCRIPT_VARS_NAME + "." + SaveVar];
            return object.Equals(true, obj) == Status;
        }

    }

    /// <summary>
    /// Spawns the mob if the map hasnt started yet.  DOESNT WORK.
    /// </summary>
    [Serializable]
    public class MobCheckMapStart : MobSpawnCheck
    {
        public override MobCheckMapStart Copy() { return new MobCheckMapStart(); }

        public override bool CanSpawn()
        {
            throw new NotImplementedException();
        }

    }

    /// <summary>
    /// Spawns the mob if the time of day is right.  DOESNT WORK.
    /// </summary>
    [Serializable]
    public class MobCheckTimeOfDay : MobSpawnCheck
    {
        /// <summary>
        /// The time of day
        /// </summary>
        public TimeOfDay Time;

        public MobCheckTimeOfDay()
        {

        }
        public MobCheckTimeOfDay(MobCheckTimeOfDay other) : this()
        {
            Time = other.Time;
        }
        public override MobSpawnCheck Copy() { return new MobCheckTimeOfDay(this); }

        public override bool CanSpawn()
        {
            return DataManager.Instance.Save.Time == Time;
        }

    }
}
