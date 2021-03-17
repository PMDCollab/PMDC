using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence.Dungeon;
using RogueEssence.LevelGen;

namespace PMDC.LevelGen
{
    [Serializable]
    public class SpreadHousePostProc : ZonePostProc
    {
        public SpreadPlanBase SpreadPlan;
        public Priority Priority;
        
        //they're generated on runtime, so they use map RNG, thus use spawnlists
        public SpawnRangeList<MapItem> Items;
        public SpawnRangeList<ItemTheme> ItemThemes;
        public SpawnRangeList<MobSpawn> Mobs;
        //special enemies will have their level scaled according to the paramrange provided by the floor
        //levels will be a spawnrangelist of ints, autocalculated with increments of 3-4
        public SpawnRangeList<MobTheme> MobThemes;
        public SpawnList<IMonsterHouseBaseStep> PostProcSpawns;

        //spreads an item through the floors
        //ensures that the space in floors between occurrences is kept tame
        public SpreadHousePostProc()
        {
            Items = new SpawnRangeList<MapItem>();
            ItemThemes = new SpawnRangeList<ItemTheme>();
            Mobs = new SpawnRangeList<MobSpawn>();
            MobThemes = new SpawnRangeList<MobTheme>();
            PostProcSpawns = new SpawnList<IMonsterHouseBaseStep>();
        }
        public SpreadHousePostProc(Priority priority) : this()
        {
            Priority = priority;
        }

        public SpreadHousePostProc(Priority priority, SpreadPlanBase plan) : this(priority)
        {
            Priority = priority;
            SpreadPlan = plan;
        }

        protected SpreadHousePostProc(SpreadHousePostProc other, ulong seed) : this()
        {
            Items = other.Items;
            ItemThemes = other.ItemThemes;
            Mobs = other.Mobs;
            MobThemes = other.MobThemes;
            PostProcSpawns = other.PostProcSpawns;

            Priority = other.Priority;
            SpreadPlan = other.SpreadPlan.Instantiate(seed);
        }

        public override ZonePostProc Instantiate(ulong seed) { return new SpreadHousePostProc(this, seed); }

        public override void Apply(ZoneGenContext zoneContext, IGenContext context, StablePriorityQueue<Priority, IGenStep> queue)
        {
            int id = zoneContext.CurrentID;
            if (SpreadPlan.CheckIfDistributed(zoneContext, context))
            {
                IMonsterHouseBaseStep monsterHousePostProc = PostProcSpawns.Pick(context.Rand).CreateNew();
                SpawnList<MapItem> itemListSlice = Items.GetSpawnList(id);
                for (int jj = 0; jj < itemListSlice.Count; jj++)
                    monsterHousePostProc.Items.Add(new MapItem(itemListSlice.GetSpawn(jj)), itemListSlice.GetSpawnRate(jj));
                SpawnList<ItemTheme> itemThemeListSlice = ItemThemes.GetSpawnList(id);
                for (int jj = 0; jj < itemThemeListSlice.Count; jj++)
                    monsterHousePostProc.ItemThemes.Add(itemThemeListSlice.GetSpawn(jj).Copy(), itemThemeListSlice.GetSpawnRate(jj));
                SpawnList<MobSpawn> mobListSlice = Mobs.GetSpawnList(id);
                for (int jj = 0; jj < mobListSlice.Count; jj++)
                {
                    MobSpawn newSpawn = mobListSlice.GetSpawn(jj).Copy();
                    monsterHousePostProc.Mobs.Add(newSpawn, mobListSlice.GetSpawnRate(jj));
                }
                SpawnList<MobTheme> mobThemeListSlice = MobThemes.GetSpawnList(id);
                for (int jj = 0; jj < mobThemeListSlice.Count; jj++)
                    monsterHousePostProc.MobThemes.Add(mobThemeListSlice.GetSpawn(jj).Copy(), mobThemeListSlice.GetSpawnRate(jj));

                queue.Enqueue(Priority, monsterHousePostProc);
            }
        }
    }
}
