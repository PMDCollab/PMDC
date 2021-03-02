using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence.Dungeon;
using RogueEssence;
using RogueEssence.LevelGen;
using PMDO.Dungeon;

namespace PMDO.LevelGen
{
    [Serializable]
    public abstract class MonsterHouseBaseStep<T> : GenStep<T>, IMonsterHouseBaseStep
        where T : ListMapGenContext
    {
        public const int ALT_COLOR_ODDS = 32;

        //they're generated on runtime, so they use map RNG, thus use spawnlists
        public SpawnList<MapItem> Items { get; set; }
        public SpawnList<ItemTheme> ItemThemes { get; set; }
        public SpawnList<MobSpawn> Mobs { get; set; }
        public SpawnList<MobTheme> MobThemes { get; set; }

        public MonsterHouseBaseStep()
        {
            Items = new SpawnList<MapItem>();
            ItemThemes = new SpawnList<ItemTheme>();
            Mobs = new SpawnList<MobSpawn>();
            MobThemes = new SpawnList<MobTheme>();
        }
        protected MonsterHouseBaseStep(MonsterHouseBaseStep<T> other) : this()
        {
            for (int ii = 0; ii < other.Items.Count; ii++)
                Items.Add(new MapItem(other.Items.GetSpawn(ii)), other.Items.GetSpawnRate(ii));
            for (int ii = 0; ii < other.ItemThemes.Count; ii++)
                ItemThemes.Add(other.ItemThemes.GetSpawn(ii).Copy(), other.ItemThemes.GetSpawnRate(ii));
            for (int ii = 0; ii < other.Mobs.Count; ii++)
                Mobs.Add(other.Mobs.GetSpawn(ii).Copy(), other.Mobs.GetSpawnRate(ii));
            for (int ii = 0; ii < other.MobThemes.Count; ii++)
                MobThemes.Add(other.MobThemes.GetSpawn(ii).Copy(), other.MobThemes.GetSpawnRate(ii));
        }
        public abstract MonsterHouseBaseStep<T> CreateNew();
        IMonsterHouseBaseStep IMonsterHouseBaseStep.CreateNew() { return CreateNew(); }
    }

    public interface IMonsterHouseBaseStep : IGenStep
    {
        SpawnList<MapItem> Items { get; set; }
        SpawnList<ItemTheme> ItemThemes { get; set; }
        SpawnList<MobSpawn> Mobs { get; set; }
        SpawnList<MobTheme> MobThemes { get; set; }
        IMonsterHouseBaseStep CreateNew();
    }
}
