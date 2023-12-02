using RogueElements;
using System;

namespace PMDC.LevelGen
{
    [Serializable]
    public class CornerRoom : RoomComponent
    {
        public override RoomComponent Clone() { return new CornerRoom(); }

        public override string ToString()
        {
            return "CardinalRoom";
        }
    }
}
