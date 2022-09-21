using System;
using RogueElements;
using RogueEssence.Dev;
using PMDC.Dungeon;
using RogueEssence.LevelGen;
using RogueEssence.Dev;
using System.Runtime.Serialization;

namespace PMDC.LevelGen
{
    /// <summary>
    /// Chooses the enemy limit and respawn time for the map.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class MobSpawnSettingsStep<T> : GenStep<T> where T : BaseMapGenContext
    {
        public Priority Priority;
        /// <summary>
        /// The respawn step.
        /// </summary>
        [SubGroup]
        public RespawnBaseEvent Respawn;

        /// <summary>
        /// OBSOLETE
        /// </summary>
        public int MaxFoes;
        public int RespawnTime;

        public MobSpawnSettingsStep()
        {
            
        }

        public MobSpawnSettingsStep(Priority priority, RespawnBaseEvent respawn)
        {
            Priority = priority;
            Respawn = respawn;
        }

        public override void Apply(T map)
        {
            map.Map.MapEffect.OnMapTurnEnds.Add(Priority, Respawn.Copy());
        }


        public override string ToString()
        {
            return String.Format("{0}", this.GetType().Name);
        }

        [OnDeserialized]
        internal void OnDeserializedMethod(StreamingContext context)
        {
            //TODO: Remove in v1.1
            if (Respawn == null)
            {
                Priority = new Priority(15);
                Respawn = new RespawnFromEligibleEvent(MaxFoes, RespawnTime);
            }
        }
    }
}
