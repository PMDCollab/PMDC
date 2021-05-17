using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence.Dungeon;
using RogueEssence.Data;
using RogueEssence.LevelGen;

namespace PMDC.LevelGen
{
    [Serializable]
    public class SaveVarsZoneStep : ZoneStep
    {
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
            GameProgress Progress = DataManager.Instance.Save;
            if (Progress != null && Progress.Rescue != null && Progress.Rescue.Rescuing)
            {
                if (Progress.Rescue.SOS.Goal.ID == zoneContext.CurrentZone
                    && Progress.Rescue.SOS.Goal.StructID.Segment == zoneContext.CurrentSegment
                    && Progress.Rescue.SOS.Goal.StructID.ID == zoneContext.CurrentID)
                {
                    queue.Enqueue(Priority, new RescueSpawner<BaseMapGenContext>());
                }
            }
        }

        public override string ToString()
        {
            return string.Format("{0}", this.GetType().Name);
        }
    }
}
