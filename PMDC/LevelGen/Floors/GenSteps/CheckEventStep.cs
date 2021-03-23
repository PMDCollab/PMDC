using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence.Dungeon;
using RogueEssence;
using RogueEssence.LevelGen;
using PMDC.Dungeon;

namespace PMDC.LevelGen
{
    [Serializable]
    public class PrepareEventStep<T> : GenStep<T> where T : BaseMapGenContext
    {
        public List<SingleCharEvent> Events;
        public PrepareEventStep() { Events = new List<SingleCharEvent>(); }
        public PrepareEventStep(params SingleCharEvent[] events) : this()
        {
            Events.AddRange(events);
        }

        public override void Apply(T map)
        {
            foreach(SingleCharEvent check in Events)
                map.PrepareEvents.Add((SingleCharEvent)check.Clone());
        }
    }

    [Serializable]
    public class StartEventStep<T> : GenStep<T> where T : BaseMapGenContext
    {
        public List<SingleCharEvent> Events;
        public StartEventStep() { Events = new List<SingleCharEvent>(); }
        public StartEventStep(params SingleCharEvent[] events) : this()
        {
            Events.AddRange(events);
        }

        public override void Apply(T map)
        {
            foreach (SingleCharEvent check in Events)
                map.StartEvents.Add((SingleCharEvent)check.Clone());
        }
    }

    [Serializable]
    public class CheckEventStep<T> : GenStep<T> where T : BaseMapGenContext
    {
        public List<SingleCharEvent> Events;
        public CheckEventStep() { Events = new List<SingleCharEvent>(); }
        public CheckEventStep(params SingleCharEvent[] events) : this()
        {
            Events.AddRange(events);
        }

        public override void Apply(T map)
        {
            foreach (SingleCharEvent check in Events)
                map.CheckEvents.Add((SingleCharEvent)check.Clone());
        }
    }
}
