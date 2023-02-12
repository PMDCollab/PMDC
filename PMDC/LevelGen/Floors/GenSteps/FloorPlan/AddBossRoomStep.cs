using System;
using System.Collections.Generic;
using System.Text;
using RogueElements;
using RogueEssence;

namespace PMDC.LevelGen
{
    /// <summary>
    /// Given a floor plan, this step attaches a boss room connected to an existing room, and then attaches a vault room that is unlocked when the player defeats the boss.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class AddBossRoomStep<T> : FloorPlanStep<T>
        where T : class, IFloorPlanGenContext
    {
        public AddBossRoomStep()
            : base()
        {
            this.BossComponents = new ComponentCollection();
            this.VaultComponents = new ComponentCollection();
            this.BossHallComponents = new ComponentCollection();
            this.VaultHallComponents = new ComponentCollection();
            this.Filters = new List<BaseRoomFilter>();
        }
        public AddBossRoomStep(IRandPicker<RoomGen<T>> bossRooms, IRandPicker<RoomGen<T>> vaultRooms, IRandPicker<PermissiveRoomGen<T>> genericHalls)
            : base()
        {
            this.BossRooms = bossRooms;
            this.TreasureRooms = vaultRooms;
            this.GenericHalls = genericHalls;
            this.BossComponents = new ComponentCollection();
            this.VaultComponents = new ComponentCollection();
            this.BossHallComponents = new ComponentCollection();
            this.VaultHallComponents = new ComponentCollection();
            this.Filters = new List<BaseRoomFilter>();
        }

        /// <summary>
        /// Determines which rooms are eligible to have the boss room added on.
        /// </summary>
        public List<BaseRoomFilter> Filters { get; set; }

        /// <summary>
        /// The room types that can be used for the boss room being added.
        /// </summary>
        public IRandPicker<RoomGen<T>> BossRooms { get; set; }

        /// <summary>
        /// Components that the newly added boss room will be labeled with.
        /// </summary>
        public ComponentCollection BossComponents { get; set; }

        /// <summary>
        /// The room types that can be used for the treasure room being added.
        /// </summary>
        public IRandPicker<RoomGen<T>> TreasureRooms { get; set; }

        /// <summary>
        /// Components that the newly added treasure room will be labeled with.
        /// </summary>
        public ComponentCollection VaultComponents { get; set; }

        /// <summary>
        /// The room types that can be used as the intermediate hall.
        /// </summary>
        public IRandPicker<PermissiveRoomGen<T>> GenericHalls { get; set; }

        /// <summary>
        /// Components that the hall between the boss room and the rest of the layout will be labeled with.
        /// </summary>
        public ComponentCollection BossHallComponents { get; set; }

        /// <summary>
        /// Components that the hall between the boss room and the vault room will be labeled with.
        /// </summary>
        public ComponentCollection VaultHallComponents { get; set; }

        public override void ApplyToPath(IRandom rand, FloorPlan floorPlan)
        {
            //attempt 10 times
            for (int kk = 0; kk < 10; kk++)
            {
                //add the boss room

                FloorPathBranch<T>.ListPathBranchExpansion? expandBossResult = this.ChooseRoomExpansion(rand, floorPlan);

                if (!expandBossResult.HasValue)
                    continue;

                var bossExpansion = expandBossResult.Value;

                RoomHallIndex from = bossExpansion.From;
                if (bossExpansion.Hall != null)
                {
                    floorPlan.AddHall(bossExpansion.Hall, this.BossHallComponents.Clone(), from);
                    from = new RoomHallIndex(floorPlan.HallCount - 1, true);
                }

                floorPlan.AddRoom(bossExpansion.Room, this.BossComponents.Clone(), from);

                RoomHallIndex bossFrom = new RoomHallIndex(floorPlan.RoomCount - 1, false);

                GenContextDebug.DebugProgress("Extended with Boss Room");

                //now, attempt to add the treasure room and remove the previous rooms if failed


                FloorPathBranch<T>.ListPathBranchExpansion? expansionResult = FloorPathBranch<T>.ChooseRandRoomExpansion(this.PrepareTreasureRoom, true, rand, floorPlan, new List<RoomHallIndex>() { bossFrom });

                if (!expansionResult.HasValue)
                {
                    //remove the previously added boss room and hall
                    floorPlan.EraseRoomHall(bossFrom);
                    floorPlan.EraseRoomHall(from);
                    continue;
                }

                var vaultExpansion = expansionResult.Value;

                if (vaultExpansion.Hall != null)
                {
                    floorPlan.AddHall(vaultExpansion.Hall, this.VaultHallComponents.Clone(), bossFrom);
                    bossFrom = new RoomHallIndex(floorPlan.HallCount - 1, true);
                }

                floorPlan.AddRoom(vaultExpansion.Room, this.VaultComponents.Clone(), bossFrom);

                GenContextDebug.DebugProgress("Extended with Treasure Room");

                return;
            }
        }

        public virtual FloorPathBranch<T>.ListPathBranchExpansion? ChooseRoomExpansion(IRandom rand, FloorPlan floorPlan)
        {
            List<RoomHallIndex> availableExpansions = new List<RoomHallIndex>();
            for (int ii = 0; ii < floorPlan.RoomCount; ii++)
            {
                if (!BaseRoomFilter.PassesAllFilters(floorPlan.GetRoomPlan(ii), this.Filters))
                    continue;
                availableExpansions.Add(new RoomHallIndex(ii, false));
            }

            for (int ii = 0; ii < floorPlan.HallCount; ii++)
            {
                if (!BaseRoomFilter.PassesAllFilters(floorPlan.GetHallPlan(ii), this.Filters))
                    continue;
                availableExpansions.Add(new RoomHallIndex(ii, true));
            }

            return FloorPathBranch<T>.ChooseRandRoomExpansion(this.PrepareBossRoom, true, rand, floorPlan, availableExpansions);
        }

        //TODO: refactor the below reduncancies

        /// <summary>
        /// Returns a random boss room or hall that can fit in the specified floor.
        /// </summary>
        /// <param name="rand"></param>
        /// <param name="floorPlan"></param>
        /// <param name="isHall"></param>
        /// <returns></returns>
        public virtual RoomGen<T> PrepareBossRoom(IRandom rand, FloorPlan floorPlan, bool isHall)
        {
            RoomGen<T> room;
            if (!isHall) // choose a room
                room = this.BossRooms.Pick(rand).Copy();
            else // chose a hall
                room = this.GenericHalls.Pick(rand).Copy();

            // decide on acceptable border/size/fulfillables
            Loc size = room.ProposeSize(rand);
            if (size.X > floorPlan.DrawRect.Width)
                size.X = floorPlan.DrawRect.Width;
            if (size.Y > floorPlan.DrawRect.Height)
                size.Y = floorPlan.DrawRect.Height;
            room.PrepareSize(rand, size);
            return room;
        }

        /// <summary>
        /// Returns a random boss room or hall that can fit in the specified floor.
        /// </summary>
        /// <param name="rand"></param>
        /// <param name="floorPlan"></param>
        /// <param name="isHall"></param>
        /// <returns></returns>
        public virtual RoomGen<T> PrepareTreasureRoom(IRandom rand, FloorPlan floorPlan, bool isHall)
        {
            RoomGen<T> room;
            if (!isHall) // choose a room
                room = this.TreasureRooms.Pick(rand).Copy();
            else // chose a hall
                room = this.GenericHalls.Pick(rand).Copy();

            // decide on acceptable border/size/fulfillables
            Loc size = room.ProposeSize(rand);
            if (size.X > floorPlan.DrawRect.Width)
                size.X = floorPlan.DrawRect.Width;
            if (size.Y > floorPlan.DrawRect.Height)
                size.Y = floorPlan.DrawRect.Height;
            room.PrepareSize(rand, size);
            return room;
        }
    }
}
