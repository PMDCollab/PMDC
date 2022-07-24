using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence.Dungeon;
using RogueEssence;
using RogueEssence.LevelGen;
using PMDC.Dungeon;

namespace PMDC.LevelGen
{
    [Serializable]
    public abstract class MonsterHouseBaseStep<T> : GenStep<T>, IMonsterHouseBaseStep
        where T : ListMapGenContext
    {
        public const int ALT_COLOR_ODDS = 32;

        /// <summary>
        /// Items that can be found in the monster house.
        /// This is in addition to the items naturally found on the map.
        /// </summary>
        public SpawnList<MapItem> Items { get; set; }

        /// <summary>
        /// Themes that items in the item pool will be filtered by.
        /// </summary>
        public SpawnList<ItemTheme> ItemThemes { get; set; }

        /// <summary>
        /// Mobs that can be found in the monster house.
        /// This is in addition to the mobs naturally found on the map.
        /// </summary>
        public SpawnList<MobSpawn> Mobs { get; set; }

        /// <summary>
        /// Themes that mobs in the mob pool will be filtered by.
        /// </summary>
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

        protected void AddIntrudeStep(T map, CheckIntrudeBoundsEvent check)
        {
            //TODO: remove this magic number
            string intrudeStatus = "intrusion_check";
            MapStatus status;
            if (map.Map.Status.TryGetValue(intrudeStatus, out status))
            {
                MapCheckState destChecks = status.StatusStates.GetWithDefault<MapCheckState>();
                destChecks.CheckEvents.Add(check);
            }
            else
            {
                status = new MapStatus(intrudeStatus);
                status.LoadFromData();
                MapCheckState checkState = status.StatusStates.GetWithDefault<MapCheckState>();
                checkState.CheckEvents.Add(check);
                map.Map.Status.Add(intrudeStatus, status);
            }
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
