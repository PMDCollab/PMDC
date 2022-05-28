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
    public class SpreadHouseZoneStep : ZoneStep
    {
        /// <summary>
        /// Determines how many floors to distribute the step to, and how spread apart they are.
        /// </summary>
        public SpreadPlanBase SpreadPlan;

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

        [StringTypeConstraint(0, typeof(ModGenState))]
        public List<FlagType> ModStates;

        //spreads an item through the floors
        //ensures that the space in floors between occurrences is kept tame
        public SpreadHouseZoneStep()
        {
            Items = new SpawnRangeList<MapItem>();
            ItemThemes = new SpawnRangeList<ItemTheme>();
            Mobs = new SpawnRangeList<MobSpawn>();
            MobThemes = new SpawnRangeList<MobTheme>();
            HouseStepSpawns = new SpawnList<IMonsterHouseBaseStep>();
            ModStates = new List<FlagType>();
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
            Items = other.Items.CopyState();
            ItemThemes = other.ItemThemes.CopyState();
            Mobs = other.Mobs.CopyState();
            MobThemes = other.MobThemes.CopyState();
            HouseStepSpawns = (SpawnList<IMonsterHouseBaseStep>)other.HouseStepSpawns.CopyState();
            ModStates.AddRange(other.ModStates);

            Priority = other.Priority;
            SpreadPlan = other.SpreadPlan.Instantiate(seed);
        }

        public override ZoneStep Instantiate(ulong seed) { return new SpreadHouseZoneStep(this, seed); }

        public override void Apply(ZoneGenContext zoneContext, IGenContext context, StablePriorityQueue<Priority, IGenStep> queue)
        {
            bool added = false;
            foreach (int floorId in SpreadPlan.DropPoints)
            {
                if (floorId != zoneContext.CurrentID)
                    continue;

                addToQueue(zoneContext, context, queue);
                added = true;
            }

            if (added)
                return;

            GameProgress progress = DataManager.Instance.Save;
            if (progress != null && progress.ActiveTeam != null)
            {
                int totalMod = 0;
                foreach (Character chara in progress.ActiveTeam.Players)
                {
                    foreach (FlagType state in ModStates)
                    {
                        CharState foundState;
                        if (chara.CharStates.TryGet(state.FullType, out foundState))
                            totalMod += ((ModGenState)foundState).Mod;
                    }
                }
                if (context.Rand.Next(100) < totalMod)
                {
                    addToQueue(zoneContext, context, queue);
                    return;
                }
            }
        }

        private void addToQueue(ZoneGenContext zoneContext, IGenContext context, StablePriorityQueue<Priority, IGenStep> queue)
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
        }

        //TODO: Created v0.5.2, delete on v0.6.1
        [OnDeserialized]
        internal void OnDeserializedMethod(StreamingContext context)
        {
            if (ModStates == null)
                ModStates = new List<FlagType>();
        }
    }
}
