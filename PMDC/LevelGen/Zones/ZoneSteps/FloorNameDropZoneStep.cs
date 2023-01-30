using System;
using RogueElements;
using RogueEssence.LevelGen;
using PMDC.Dungeon;
using RogueEssence;
using System.Runtime.Serialization;

namespace PMDC.LevelGen
{
    /// <summary>
    /// Names all floors of the dungeon segment according to a naming convention.
    /// </summary>
    [Serializable]
    public class FloorNameDropZoneStep : FloorNameIDZoneStep
    {
        /// <summary>
        /// At what point in the map start to drop the title.
        /// </summary>
        public Priority DropPriority;

        public FloorNameDropZoneStep()
        {
        }

        public FloorNameDropZoneStep(Priority priority, LocalText name, Priority dropPriority) : base(priority, name)
        {
            DropPriority = dropPriority;
        }
        protected FloorNameDropZoneStep(FloorNameDropZoneStep other, ulong seed) : base(other, seed)
        {
            DropPriority = other.DropPriority;
        }

        public override ZoneStep Instantiate(ulong seed) { return new FloorNameDropZoneStep(this, seed); }

        public override void Apply(ZoneGenContext zoneContext, IGenContext context, StablePriorityQueue<Priority, IGenStep> queue)
        {
            base.Apply(zoneContext, context, queue);

            MapEffectStep<BaseMapGenContext> fade = new MapEffectStep<BaseMapGenContext>();
            fade.Effect.OnMapStarts.Add(DropPriority, new FadeTitleEvent());
            queue.Enqueue(Priority, fade);
        }


        [OnDeserialized]
        internal void OnDeserializedMethod(StreamingContext context)
        {
            //TODO: Remove in v1.1
            if (RogueEssence.Data.Serializer.OldVersion < new Version(0, 7, 0))
                DropPriority = new Priority(-15);
        }
    }
}
