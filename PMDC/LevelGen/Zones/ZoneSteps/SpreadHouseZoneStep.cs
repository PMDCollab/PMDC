using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using RogueElements;
using RogueEssence;
using RogueEssence.Data;
using RogueEssence.Dev;
using RogueEssence.Dungeon;
using RogueEssence.LevelGen;

namespace PMDC.LevelGen
{
    /// <summary>
    /// Generates monster houses randomly across the whole dungeon segment.
    /// </summary>
    [Serializable]
    public class SpreadHouseZoneStep : SpreadZoneStep
    {
        /// <summary>
        /// At what point in the map gen process to run the step in.
        /// </summary>
        public Priority Priority;

        //they're generated on runtime, so they use map RNG, thus use spawnlists
        [RangeBorder(0, true, true)]
        public SpawnRangeList<MapItem> Items;
        [RangeBorder(0, true, true)]
        public SpawnRangeList<ItemTheme> ItemThemes;
        [RangeBorder(0, true, true)]
        public SpawnRangeList<MobSpawn> Mobs;
        //special enemies will have their level scaled according to the paramrange provided by the floor
        //levels will be a spawnrangelist of ints, autocalculated with increments of 3-4
        [RangeBorder(0, true, true)]
        public SpawnRangeList<MobTheme> MobThemes;
        public SpawnList<IMonsterHouseBaseStep> HouseStepSpawns;

        //spreads an item through the floors
        //ensures that the space in floors between occurrences is kept tame
        public SpreadHouseZoneStep()
        {
            Items = new SpawnRangeList<MapItem>();
            ItemThemes = new SpawnRangeList<ItemTheme>();
            Mobs = new SpawnRangeList<MobSpawn>();
            MobThemes = new SpawnRangeList<MobTheme>();
            HouseStepSpawns = new SpawnList<IMonsterHouseBaseStep>();
        }
        public SpreadHouseZoneStep(Priority priority, SpreadPlanBase plan) : base(plan)
        {
            Items = new SpawnRangeList<MapItem>();
            ItemThemes = new SpawnRangeList<ItemTheme>();
            Mobs = new SpawnRangeList<MobSpawn>();
            MobThemes = new SpawnRangeList<MobTheme>();
            HouseStepSpawns = new SpawnList<IMonsterHouseBaseStep>();

            Priority = priority;
        }

        protected SpreadHouseZoneStep(SpreadHouseZoneStep other, ulong seed) : base(other, seed)
        {
            Items = other.Items.CopyState();
            ItemThemes = other.ItemThemes.CopyState();
            Mobs = other.Mobs.CopyState();
            MobThemes = other.MobThemes.CopyState();
            HouseStepSpawns = (SpawnList<IMonsterHouseBaseStep>)other.HouseStepSpawns.CopyState();

            Priority = other.Priority;
        }

        public override ZoneStep Instantiate(ulong seed) { return new SpreadHouseZoneStep(this, seed); }

        protected override bool ApplyToFloor(ZoneGenContext zoneContext, IGenContext context, StablePriorityQueue<Priority, IGenStep> queue, int dropIdx)
        {
            int id = zoneContext.CurrentID;

            IMonsterHouseBaseStep monsterHouseStep = HouseStepSpawns.Pick(context.Rand).CreateNew();
            SpawnList<MapItem> itemListSlice = Items.GetSpawnList(id);
            for (int jj = 0; jj < itemListSlice.Count; jj++)
                monsterHouseStep.Items.Add(new MapItem(itemListSlice.GetSpawn(jj)), itemListSlice.GetSpawnRate(jj));
            SpawnList<ItemTheme> itemThemeListSlice = ItemThemes.GetSpawnList(id);
            for (int jj = 0; jj < itemThemeListSlice.Count; jj++)
                monsterHouseStep.ItemThemes.Add(itemThemeListSlice.GetSpawn(jj).Copy(), itemThemeListSlice.GetSpawnRate(jj));
            SpawnList<MobSpawn> mobListSlice = Mobs.GetSpawnList(id);
            for (int jj = 0; jj < mobListSlice.Count; jj++)
            {
                MobSpawn newSpawn = mobListSlice.GetSpawn(jj).Copy();
                monsterHouseStep.Mobs.Add(newSpawn, mobListSlice.GetSpawnRate(jj));
            }
            SpawnList<MobTheme> mobThemeListSlice = MobThemes.GetSpawnList(id);
            for (int jj = 0; jj < mobThemeListSlice.Count; jj++)
                monsterHouseStep.MobThemes.Add(mobThemeListSlice.GetSpawn(jj).Copy(), mobThemeListSlice.GetSpawnRate(jj));

            queue.Enqueue(Priority, monsterHouseStep);

            return true;
        }
    }
}
