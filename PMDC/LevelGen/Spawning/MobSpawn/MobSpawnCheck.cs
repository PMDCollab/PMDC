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
    /// Spawns the mob if it matches version ID.
    /// </summary>
    [Serializable]
    public class MobCheckVersionDiff : MobSpawnCheck
    {
        public int Remainder;
        public int Div;

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

}
