using RogueElements;
using System;

namespace PMDO.LevelGen
{
    [Serializable]
    public class BossRoom : RoomComponent
    {
        public override RoomComponent Clone() { return new BossRoom(); }

        public override string ToString()
        {
            return "BossRoom";
        }
    }
}
