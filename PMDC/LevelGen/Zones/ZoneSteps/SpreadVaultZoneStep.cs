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
    /// Generates vaults randomly across the whole dungeon segment.
    /// </summary>
    [Serializable]
    public class SpreadVaultZoneStep : SpreadZoneStep
    {
        /// <summary>
        /// At what point in the map gen process to run the item placement steps in.
        /// </summary>
        public Priority ItemPriority;

        /// <summary>
        /// At what point in the map gen process to run the tile placement steps in.
        /// </summary>
        public Priority TilePriority;

        /// <summary>
        /// At what point in the map gen process to run the mob placement steps in.
        /// </summary>
        public Priority MobPriority;

        public List<IGenPriority> VaultSteps;

        /// <summary>
        /// Encounter table for items found in the vault.
        /// </summary>
        [RangeBorder(0, true, true)]
        public SpawnRangeList<MapItem> Items;

        /// <summary>
        /// Amount of the items randomly chosen from spawnlist
        /// </summary>
        [RangeBorder(0, true, true)]
        public RangeDict<RandRange> ItemAmount;

        /// <summary>
        /// Spawners for specific items
        /// </summary>
        [RangeBorder(0, true, true)]
        public RangeDict<IStepSpawner<ListMapGenContext, MapItem>> ItemSpawners;

        /// <summary>
        /// Step used to place the items
        /// </summary>
        [RangeBorder(0, true, true)]
        public RangeDict<RandomRoomSpawnStep<ListMapGenContext, MapItem>> ItemPlacements;

        /// <summary>
        /// Spawners for specific items
        /// </summary>
        [RangeBorder(0, true, true)]
        public RangeDict<IStepSpawner<ListMapGenContext, EffectTile>> TileSpawners;

        [RangeBorder(0, true, true)]
        public RangeDict<RandomRoomSpawnStep<ListMapGenContext, EffectTile>> TilePlacements;

        /// <summary>
        /// Encounter table for mobs found in the vault.
        /// </summary>
        [RangeBorder(0, true, true)]
        public SpawnRangeList<MobSpawn> Mobs;
        //special enemies will have their level scaled according to the paramrange provided by the floor
        //levels will be a spawnrangelist of ints, autocalculated with increments of 3-4

        [RangeBorder(0, true, true)]
        public RangeDict<RandRange> MobAmount;
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
            TileSpawners = new RangeDict<IStepSpawner<ListMapGenContext, EffectTile>>();
            TilePlacements = new RangeDict<RandomRoomSpawnStep<ListMapGenContext, EffectTile>>();
            MobAmount = new RangeDict<RandRange>();
            MobPlacements = new RangeDict<PlaceRandomMobsStep<ListMapGenContext>>();
        }
        
        public SpreadVaultZoneStep(Priority itemPriority, Priority tilePriority, Priority mobPriority, SpreadPlanBase plan) : base(plan)
        {
            VaultSteps = new List<IGenPriority>();
            Items = new SpawnRangeList<MapItem>();
            Mobs = new SpawnRangeList<MobSpawn>();
            ItemAmount = new RangeDict<RandRange>();
            ItemSpawners = new RangeDict<IStepSpawner<ListMapGenContext, MapItem>>();
            ItemPlacements = new RangeDict<RandomRoomSpawnStep<ListMapGenContext, MapItem>>();
            TileSpawners = new RangeDict<IStepSpawner<ListMapGenContext, EffectTile>>();
            TilePlacements = new RangeDict<RandomRoomSpawnStep<ListMapGenContext, EffectTile>>();
            MobAmount = new RangeDict<RandRange>();
            MobPlacements = new RangeDict<PlaceRandomMobsStep<ListMapGenContext>>();

            ItemPriority = itemPriority;
            TilePriority = tilePriority;
            MobPriority = mobPriority;
        }

        protected SpreadVaultZoneStep(SpreadVaultZoneStep other, ulong seed) : base(other, seed)
        {
            VaultSteps = new List<IGenPriority>();
            VaultSteps.AddRange(other.VaultSteps);
            Items = other.Items.CopyState();
            Mobs = other.Mobs.CopyState();
            ItemAmount = other.ItemAmount;
            ItemSpawners = other.ItemSpawners;
            ItemPlacements = other.ItemPlacements;
            TileSpawners = other.TileSpawners;
            TilePlacements = other.TilePlacements;
            MobAmount = other.MobAmount;
            MobPlacements = other.MobPlacements;

            ItemPriority = other.ItemPriority;
            TilePriority = other.TilePriority;
            MobPriority = other.MobPriority;
        }

        public override ZoneStep Instantiate(ulong seed) { return new SpreadVaultZoneStep(this, seed); }

        protected override bool ApplyToFloor(ZoneGenContext zoneContext, IGenContext context, StablePriorityQueue<Priority, IGenStep> queue, int dropIdx)
        {
            int id = zoneContext.CurrentID;

            foreach (IGenPriority vaultStep in VaultSteps)
                queue.Enqueue(vaultStep.Priority, vaultStep.GetItem());

            if (ItemPlacements.ContainsItem(id))
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
                detourItems.Spawn = new MultiStepSpawner<ListMapGenContext, MapItem>(groupRand);
                queue.Enqueue(ItemPriority, detourItems);
            }

            if (TilePlacements.ContainsItem(id))
            {
                List<IStepSpawner<ListMapGenContext, EffectTile>> steps = new List<IStepSpawner<ListMapGenContext, EffectTile>>();
                if (TileSpawners.ContainsItem(id))
                {
                    IStepSpawner<ListMapGenContext, EffectTile> treasures = TileSpawners[id].Copy();
                    steps.Add(treasures);
                }
                PresetMultiRand<IStepSpawner<ListMapGenContext, EffectTile>> groupRand = new PresetMultiRand<IStepSpawner<ListMapGenContext, EffectTile>>(steps.ToArray());
                RandomRoomSpawnStep<ListMapGenContext, EffectTile> detourItems = TilePlacements[id].Copy();
                detourItems.Spawn = new MultiStepSpawner<ListMapGenContext, EffectTile>(groupRand);
                queue.Enqueue(TilePriority, detourItems);
            }


            SpawnList<MobSpawn> mobListSlice = Mobs.GetSpawnList(id);
            if (mobListSlice.CanPick && MobPlacements.ContainsItem(id))
            {
                //secret enemies
                SpecificTeamSpawner specificTeam = new SpecificTeamSpawner();

                MobSpawn newSpawn = mobListSlice.Pick(context.Rand).Copy();
                specificTeam.Spawns.Add(newSpawn);

                //use bruteforce clone for this
                PlaceRandomMobsStep<ListMapGenContext> secretMobPlacement = MobPlacements[id].Copy();
                secretMobPlacement.Spawn = new LoopedTeamSpawner<ListMapGenContext>(specificTeam, MobAmount[id]);
                queue.Enqueue(MobPriority, secretMobPlacement);
            }
            return true;
        }
    }
}
