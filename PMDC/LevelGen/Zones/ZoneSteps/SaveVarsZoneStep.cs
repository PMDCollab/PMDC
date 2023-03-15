using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence.Dungeon;
using RogueEssence.Data;
using RogueEssence.LevelGen;

namespace PMDC.LevelGen
{
    /// <summary>
    /// The zone step responsible for placing the rescue tile and monster house when a rescue is active.
    /// Does nothing otherwise.
    /// </summary>
    [Serializable]
    public class SaveVarsZoneStep : ZoneStep
    {
        /// <summary>
        /// At what point in the map gen process to run the step in.
        /// </summary>
        public Priority Priority;

        public SaveVarsZoneStep(Priority priority)
        {
            Priority = priority;
        }
        protected SaveVarsZoneStep(SaveVarsZoneStep other, ulong seed)
        {
            Priority = other.Priority;
        }

        public override ZoneStep Instantiate(ulong seed) { return new SaveVarsZoneStep(this, seed); }

        public override void Apply(ZoneGenContext zoneContext, IGenContext context, StablePriorityQueue<Priority, IGenStep> queue)
        {
            GameProgress progress = DataManager.Instance.Save;
            if (progress != null && progress.Rescue != null && progress.Rescue.Rescuing)
            {
                if (progress.Rescue.SOS.Goal.ID == zoneContext.CurrentZone
                    && progress.Rescue.SOS.Goal.StructID.Segment == zoneContext.CurrentSegment
                    && progress.Rescue.SOS.Goal.StructID.ID == zoneContext.CurrentID)
                {
                    queue.Enqueue(Priority, new RescueSpawner<BaseMapGenContext>());
                }
            }
        }

        public override string ToString()
        {
            return string.Format("{0}", this.GetType().GetFormattedTypeName());
        }
    }
}
