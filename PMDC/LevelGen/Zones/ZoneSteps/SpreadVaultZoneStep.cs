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
    public class SpreadVaultZoneStep : ZoneStep
    {
        public SpreadPlanBase SpreadPlan;
        public Priority ItemPriority;
        public Priority MobPriority;

        public List<IGenPriority> VaultSteps;

        //items can be multiple lists
        [RangeBorder(0, true, true)]
        public SpawnRangeList<MapItem> Items;
        [RangeBorder(0, true, true)]
        public SpawnRangeList<MobSpawn> Mobs;
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
        [RangeBorder(0, true, true)]
        public RangeDict<PlaceRandomMobsStep<ListMapGenContext>> MobPlacements;
        //spreads an item through the floors
        //ensures that the space in floors between occurrences is kept tame
        public SpreadVaultZoneStep()
        {
            VaultSteps = new List<IGenPriority>();
            Items = new SpawnRangeList<MapItem>();
            Mobs = new SpawnRangeList<MobSpawn>();
            ItemAmount = new RangeDict<RandRange>();
            ItemSpawners = new RangeDict<IStepSpawner<ListMapGenContext, MapItem>>();
            ItemPlacements = new RangeDict<RandomRoomSpawnStep<ListMapGenContext, MapItem>>();
            MobPlacements = new RangeDict<PlaceRandomMobsStep<ListMapGenContext>>();
        }
        public SpreadVaultZoneStep(Priority itemPriority, Priority mobPriority) : this()
        {
            MobPriority = itemPriority;
        }

        public SpreadVaultZoneStep(Priority itemPriority, Priority mobPriority, SpreadPlanBase plan) : this(itemPriority, mobPriority)
        {
            ItemPriority = itemPriority;
            MobPriority = mobPriority;
            SpreadPlan = plan;
        }

        protected SpreadVaultZoneStep(SpreadVaultZoneStep other, ulong seed) : this()
        {
            VaultSteps = other.VaultSteps;
            Items = other.Items;
            Mobs = other.Mobs;
            ItemAmount = other.ItemAmount;
            ItemSpawners = other.ItemSpawners;
            ItemPlacements = other.ItemPlacements;
            MobPlacements = other.MobPlacements;

            ItemPriority = other.ItemPriority;
            MobPriority = other.MobPriority;
            SpreadPlan = other.SpreadPlan.Instantiate(seed);
        }

        public override ZoneStep Instantiate(ulong seed) { return new SpreadVaultZoneStep(this, seed); }

        public override void Apply(ZoneGenContext zoneContext, IGenContext context, StablePriorityQueue<Priority, IGenStep> queue)
        {
            int id = zoneContext.CurrentID;
            if (SpreadPlan.CheckIfDistributed(zoneContext, context))
            {
                foreach(IGenPriority vaultStep in VaultSteps)
                    queue.Enqueue(vaultStep.Priority, vaultStep.GetItem());

                {
                    SpawnList<MapItem> itemListSlice = Items.GetSpawnList(id);
                    PickerSpawner<ListMapGenContext, MapItem> constructedSpawns = new PickerSpawner<ListMapGenContext, MapItem>(new LoopedRand<MapItem>(itemListSlice, ItemAmount[id]));

                    List<IStepSpawner<ListMapGenContext, MapItem>> steps = new List<IStepSpawner<ListMapGenContext, MapItem>>();
                    steps.Add(constructedSpawns);
                    if (ItemSpawners.ContainsItem(id))
                    {
                        IStepSpawner<ListMapGenContext, MapItem> treasures = ItemSpawners[id].Copy();
                        steps.Add(treasures);
                    }
                    PresetMultiRand<IStepSpawner<ListMapGenContext, MapItem>> groupRand = new PresetMultiRand<IStepSpawner<ListMapGenContext, MapItem>>(steps.ToArray());
                    RandomRoomSpawnStep<ListMapGenContext, MapItem> detourItems = ItemPlacements[id].Copy();
                    detourItems.Spawn = new StepSpawner<ListMapGenContext, MapItem>(groupRand);
                    queue.Enqueue(ItemPriority, detourItems);
                }


                SpawnList<MobSpawn> mobListSlice = Mobs.GetSpawnList(id);
                if (mobListSlice.CanPick)
                {
                    //secret enemies
                    SpecificTeamSpawner specificTeam = new SpecificTeamSpawner();

                    MobSpawn newSpawn = mobListSlice.Pick(context.Rand).Copy();
                    specificTeam.Spawns.Add(newSpawn);

                    //use bruteforce clone for this
                    PlaceRandomMobsStep<ListMapGenContext> secretMobPlacement = MobPlacements[id].Copy();
                    secretMobPlacement.Spawn = new TeamPickerSpawner<ListMapGenContext>(specificTeam);
                    queue.Enqueue(MobPriority, secretMobPlacement);
                }
            }
        }
    }
}
