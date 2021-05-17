using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence.Dev;
using RogueEssence.Dungeon;
using RogueEssence.LevelGen;

namespace PMDC.LevelGen
{
    [Serializable]
    public class SpreadHouseZoneStep : ZoneStep
    {
        public SpreadPlanBase SpreadPlan;
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
        public SpreadHouseZoneStep(Priority priority) : this()
        {
            Priority = priority;
        }

        public SpreadHouseZoneStep(Priority priority, SpreadPlanBase plan) : this(priority)
        {
            Priority = priority;
            SpreadPlan = plan;
        }

        protected SpreadHouseZoneStep(SpreadHouseZoneStep other, ulong seed) : this()
        {
            Items = other.Items;
            ItemThemes = other.ItemThemes;
            Mobs = other.Mobs;
            MobThemes = other.MobThemes;
            HouseStepSpawns = other.HouseStepSpawns;

            Priority = other.Priority;
            SpreadPlan = other.SpreadPlan.Instantiate(seed);
        }

        public override ZoneStep Instantiate(ulong seed) { return new SpreadHouseZoneStep(this, seed); }

        public override void Apply(ZoneGenContext zoneContext, IGenContext context, StablePriorityQueue<Priority, IGenStep> queue)
        {
            int id = zoneContext.CurrentID;
            if (SpreadPlan.CheckIfDistributed(zoneContext, context))
            {
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
            }
        }
    }
}
