using RogueElements;
using System;

namespace PMDC.LevelGen
{
    /// <summary>
    /// Rooms that should not have any events taking place in them.
    /// </summary>
    [Serializable]
    public class NoEventRoom : RoomComponent
    {
        public override RoomComponent Clone() { return new NoEventRoom(); }

        public override string ToString()
        {
            return "NoEvent";
        }
    }
}
