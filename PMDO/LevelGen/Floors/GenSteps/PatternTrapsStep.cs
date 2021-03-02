using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence.Dungeon;

namespace PMDO.LevelGen
{
    [Serializable]
    public class PatternTrapsStep<T> : GenStep<T> where T : class, IFloorPlanGenContext
    {
        //assign traps in patterns:
        public enum TrapPattern
        {
            Single,
            Minefield,
            WavyWall,
            GappedWall,
            Array,
            Mesh
        }


        public RandRange Amount;
        public SpawnList<EffectTile> Spawns;
        public SpawnList<TrapPattern> PatternSpawns;

        public List<BaseRoomFilter> Filters { get; set; }

        public PatternTrapsStep()
        {
            Spawns = new SpawnList<EffectTile>();
            PatternSpawns = new SpawnList<TrapPattern>();
            Filters = new List<BaseRoomFilter>();
        }

        public PatternTrapsStep(RandRange trapAmount)
        {
            Amount = trapAmount;
            Spawns = new SpawnList<EffectTile>();
            PatternSpawns = new SpawnList<TrapPattern>();
        }

        public override void Apply(T map)
        {
            int chosenAmount = Amount.Pick(map.Rand);
            if (chosenAmount > 0 && Spawns.Count > 0 && PatternSpawns.Count > 0)
            {

                List<int> openRooms = new List<int>();
                //get all places that traps are eligible
                for (int ii = 0; ii < map.RoomPlan.RoomCount; ii++)
                {
                    if (BaseRoomFilter.PassesAllFilters(map.RoomPlan.GetRoomPlan(ii), this.Filters))
                        openRooms.Add(ii);
                }

                for (int ii = 0; ii < chosenAmount; ii++)
                {
                    // add traps
                    if (openRooms.Count > 0)
                    {
                        int randIndex = map.Rand.Next(openRooms.Count);
                        TrapPattern pattern = PatternSpawns.Pick(map.Rand);
                    }
                }
            }
        }

    }
}
