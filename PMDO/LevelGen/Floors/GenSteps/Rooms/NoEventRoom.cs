using RogueElements;
using System;

namespace PMDO.LevelGen
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
