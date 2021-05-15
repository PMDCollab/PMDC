using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence;
using RogueEssence.Dev;
using RogueEssence.Dungeon;
using RogueEssence.LevelGen;

namespace PMDC.LevelGen
{
    [Serializable]
    public class SpreadBossPostProc : ZonePostProc
    {
        public SpreadPlanBase SpreadPlan;
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
        public SpreadBossPostProc()
        {
            VaultSteps = new List<IGenPriority>();
            Items = new SpawnRangeList<MapItem>();
            BossSteps = new SpawnRangeList<AddBossRoomStep<ListMapGenContext>>();
            ItemAmount = new RangeDict<RandRange>();
            ItemSpawners = new RangeDict<IStepSpawner<ListMapGenContext, MapItem>>();
            ItemPlacements = new RangeDict<RandomRoomSpawnStep<ListMapGenContext, MapItem>>();
        }
        public SpreadBossPostProc(Priority bossRoomPriority, Priority rewardPriority) : this()
        {
            BossRoomPriority = bossRoomPriority;
            RewardPriority = rewardPriority;
        }

        public SpreadBossPostProc(Priority bossRoomPriority, Priority rewardPriority, SpreadPlanBase plan) : this(bossRoomPriority, rewardPriority)
        {
            BossRoomPriority = bossRoomPriority;
            RewardPriority = rewardPriority;
            SpreadPlan = plan;
        }

        protected SpreadBossPostProc(SpreadBossPostProc other, ulong seed) : this()
        {
            VaultSteps = other.VaultSteps;
            Items = other.Items;
            BossSteps = other.BossSteps;
            ItemAmount = other.ItemAmount;
            ItemSpawners = other.ItemSpawners;
            ItemPlacements = other.ItemPlacements;

            BossRoomPriority = other.BossRoomPriority;
            RewardPriority = other.RewardPriority;
            SpreadPlan = other.SpreadPlan.Instantiate(seed);
        }

        public override ZonePostProc Instantiate(ulong seed) { return new SpreadBossPostProc(this, seed); }

        public override void Apply(ZoneGenContext zoneContext, IGenContext context, StablePriorityQueue<Priority, IGenStep> queue)
        {
            int id = zoneContext.CurrentID;
            if (SpreadPlan.CheckIfDistributed(zoneContext, context))
            {
                {
                    SpawnList<AddBossRoomStep<ListMapGenContext>> bossListSlice = BossSteps.GetSpawnList(id);
                    if (!bossListSlice.CanPick)
                        return;
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
                    detourItems.Spawn = new StepSpawner<ListMapGenContext, MapItem>(groupRand);
                    queue.Enqueue(RewardPriority, detourItems);
                }

            }
        }
    }
}
