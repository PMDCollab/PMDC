using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence;
using RogueEssence.Dev;
using RogueEssence.Dungeon;
using RogueEssence.LevelGen;

namespace PMDC.LevelGen
{
    /// <summary>
    /// Generates boss battles randomly across the whole dungeon segment.
    /// </summary>
    [Serializable]
    public class SpreadBossZoneStep : SpreadZoneStep
    {
        public Priority BossRoomPriority;
        public Priority RewardPriority;

        public List<IGenPriority> VaultSteps;

        //items can be multiple lists
        [RangeBorder(0, true, true)]
        public SpawnRangeList<MapItem> Items;
        [RangeBorder(0, true, true)]
        public SpawnRangeList<AddBossRoomStep<ListMapGenContext>> BossSteps;
        //special enemies will have their level scaled according to the paramrange provided by the floor
        //levels will be a spawnrangelist of ints, autocalculated with increments of 3-4

        /// <summary>
        /// Amount for the items randomly chosen from spawnlist
        /// </summary>
        [RangeBorder(0, true, true)]
        public RangeDict<RandRange> ItemAmount;
        [RangeBorder(0, true, true)]
        public RangeDict<IStepSpawner<ListMapGenContext, MapItem>> ItemSpawners;
        [RangeBorder(0, true, true)]
        public RangeDict<RandomRoomSpawnStep<ListMapGenContext, MapItem>> ItemPlacements;
        //spreads an item through the floors
        //ensures that the space in floors between occurrences is kept tame
        public SpreadBossZoneStep()
        {
            VaultSteps = new List<IGenPriority>();
            Items = new SpawnRangeList<MapItem>();
            BossSteps = new SpawnRangeList<AddBossRoomStep<ListMapGenContext>>();
            ItemAmount = new RangeDict<RandRange>();
            ItemSpawners = new RangeDict<IStepSpawner<ListMapGenContext, MapItem>>();
            ItemPlacements = new RangeDict<RandomRoomSpawnStep<ListMapGenContext, MapItem>>();
        }

        public SpreadBossZoneStep(Priority bossRoomPriority, Priority rewardPriority, SpreadPlanBase plan) : base(plan)
        {
            VaultSteps = new List<IGenPriority>();
            Items = new SpawnRangeList<MapItem>();
            BossSteps = new SpawnRangeList<AddBossRoomStep<ListMapGenContext>>();
            ItemAmount = new RangeDict<RandRange>();
            ItemSpawners = new RangeDict<IStepSpawner<ListMapGenContext, MapItem>>();
            ItemPlacements = new RangeDict<RandomRoomSpawnStep<ListMapGenContext, MapItem>>();

            BossRoomPriority = bossRoomPriority;
            RewardPriority = rewardPriority;
        }

        protected SpreadBossZoneStep(SpreadBossZoneStep other, ulong seed) : base(other, seed)
        {
            VaultSteps = new List<IGenPriority>();
            VaultSteps.AddRange(other.VaultSteps);
            Items = other.Items.CopyState();
            BossSteps = other.BossSteps.CopyState();
            ItemAmount = other.ItemAmount;
            ItemSpawners = other.ItemSpawners;
            ItemPlacements = other.ItemPlacements;

            BossRoomPriority = other.BossRoomPriority;
            RewardPriority = other.RewardPriority;
        }

        public override ZoneStep Instantiate(ulong seed) { return new SpreadBossZoneStep(this, seed); }

        protected override bool ApplyToFloor(ZoneGenContext zoneContext, IGenContext context, StablePriorityQueue<Priority, IGenStep> queue, int dropIdx)
        {
            int id = zoneContext.CurrentID;
            {
                SpawnList<AddBossRoomStep<ListMapGenContext>> bossListSlice = BossSteps.GetSpawnList(id);
                if (!bossListSlice.CanPick)
                    return false;
                AddBossRoomStep<ListMapGenContext> bossStep = bossListSlice.Pick(context.Rand).Copy();
                queue.Enqueue(BossRoomPriority, bossStep);
            }

            foreach (IGenPriority vaultStep in VaultSteps)
                queue.Enqueue(vaultStep.Priority, vaultStep.GetItem());

            {
                SpawnList<MapItem> itemListSlice = Items.GetSpawnList(id);
                PickerSpawner<ListMapGenContext, MapItem> constructedSpawns = new PickerSpawner<ListMapGenContext, MapItem>(new LoopedRand<MapItem>(itemListSlice, ItemAmount[id]));

                IStepSpawner<ListMapGenContext, MapItem> treasures = ItemSpawners[id].Copy();

                PresetMultiRand<IStepSpawner<ListMapGenContext, MapItem>> groupRand = new PresetMultiRand<IStepSpawner<ListMapGenContext, MapItem>>(constructedSpawns, treasures);

                RandomRoomSpawnStep<ListMapGenContext, MapItem> detourItems = ItemPlacements[id].Copy();
                detourItems.Spawn = new MultiStepSpawner<ListMapGenContext, MapItem>(groupRand);
                queue.Enqueue(RewardPriority, detourItems);
            }
            return true;
        }
    }
}
