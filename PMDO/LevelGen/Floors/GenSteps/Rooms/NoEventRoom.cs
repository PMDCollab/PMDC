using RogueElements;
using System;

namespace PMDC.LevelGen
{
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
