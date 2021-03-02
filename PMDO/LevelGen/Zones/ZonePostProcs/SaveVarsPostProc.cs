using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence.Dungeon;
using RogueEssence.Data;
using RogueEssence.LevelGen;

namespace PMDO.LevelGen
{
    [Serializable]
    public class SaveVarsPostProc : ZonePostProc
    {
        public Priority Priority;

        public SaveVarsPostProc(Priority priority)
        {
            Priority = priority;
        }
        protected SaveVarsPostProc(SaveVarsPostProc other, ulong seed)
        {
            Priority = other.Priority;
        }

        public override ZonePostProc Instantiate(ulong seed) { return new SaveVarsPostProc(this, seed); }

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
    }
}
